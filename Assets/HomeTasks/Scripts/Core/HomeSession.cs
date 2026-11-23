using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HomeTasks
{
    public enum HomeQuest { Washing, Organizing, Trash, Meeting, Complete }

    public sealed class HomeSession : MonoBehaviour
    {
        public const string GameplayScenePath = "Assets/HomeTasks/Scenes/HomeFocus.unity";
        public const string ResultsScenePath = "Assets/HomeTasks/Scenes/HomeResults.unity";
        public static HomeSession Instance { get; private set; }
        public static HomeSessionResult LastResult { get; private set; }
        public static string LastPlayerCode { get; private set; }
        public static string LastGameplayScenePath { get; private set; } = GameplayScenePath;
        public HomeTaskSettings settings;
        public List<DishItem> dishes = new List<DishItem>();
        public List<TrashItem> rubbish = new List<TrashItem>();
        public MeetingController meeting;
        public AudioSource feedback;

        public int Washed => dishes.Count(d => d != null && d.IsClean);
        public int Organized => dishes.Count(d => d != null && d.IsStored);
        public int Disposed => rubbish.Count(t => t != null && t.IsDisposed);
        public bool WashingComplete => dishes.Count > 0 && Washed == dishes.Count;
        public bool SortingComplete => dishes.Count > 0 && Organized == dishes.Count;
        public bool TrashComplete => rubbish.Count > 0 && Disposed == rubbish.Count;
        public bool AllComplete => WashingComplete && SortingComplete && TrashComplete && meeting != null && meeting.Completed;
        public int CompletedTasks => (WashingComplete ? 1 : 0) + (SortingComplete ? 1 : 0) + (TrashComplete ? 1 : 0) + (meeting != null && meeting.Completed ? 1 : 0);
        public float Elapsed => finished ? finishTime : WaitingForStart ? 0f : Time.time - startedAt;
        public bool Finished => finished;
        internal bool WaitingForStart { get; private set; }
        public string Toast => Time.unscaledTime < toastUntil ? toast : string.Empty;
        public HomeQuest CurrentQuest => !WashingComplete ? HomeQuest.Washing : !SortingComplete ? HomeQuest.Organizing : !TrashComplete ? HomeQuest.Trash : meeting == null || !meeting.Completed ? HomeQuest.Meeting : HomeQuest.Complete;
        public string CurrentQuestName => QuestName(CurrentQuest);
        public string CurrentObjective => WaitingForStart ? GameText.Get("Choose how to start this session.") : CurrentQuest switch
        {
            HomeQuest.Washing => GameText.Format("Quest 1/4: Wash all {0} dishes in the running sink.", dishes.Count),
            HomeQuest.Organizing => GameText.Get("Quest 2/4: Put all clean dishes on their matching shelves."),
            HomeQuest.Trash => GameText.Format("Quest 3/4: Take all {0} bags to the outdoor bin.", rubbish.Count),
            HomeQuest.Meeting => meeting != null && meeting.seat != null ? meeting.State switch
            {
                MeetingState.Listening => GameText.Get("Quest 4/4: Stay seated and listen to the meeting."),
                MeetingState.Quiz => GameText.Get("Quest 4/4: Stay seated and answer on the meeting screen."),
                _ => GameText.Get("Quest 4/4: Select the chair at the desk to sit and listen.")
            } : GameText.Get("Quest 4/4: Join the meeting. Listen, then answer once."),
            _ => GameText.Get("All four quests complete. Your results are ready.")
        };

        float startedAt;
        float finishTime;
        bool finished;
        bool restarting;
        string toast;
        float toastUntil;
        AudioClip successTone;
        HomeQuest previousQuest;
        readonly DistractionLedger distractions = new DistractionLedger();
        SessionAnalytics analytics;

        internal void RecordDistraction(DistractionKind kind, bool began, float duration)
        {
            if (!WaitingForStart && !finished && !restarting) distractions.Record(kind, began, duration);
        }

        internal DistractionRecord[] SnapshotDistractions() => distractions.Snapshot();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            LastResult = null;
            LastPlayerCode = null;
            LastGameplayScenePath = GameplayScenePath;
        }

        void Awake()
        {
            Instance = this;
            LastGameplayScenePath = gameObject.scene.path;
            LastResult = null;
            LastPlayerCode = null;
            startedAt = Time.time;
            analytics = SessionAnalytics.Initialize();
            if (analytics != null)
            {
                analytics.PrepareRun();
                if (analytics.ServerConfigured || analytics.HasDeviceSetup) WaitingForStart = true;
                else analytics.Begin(this);
            }
            successTone = AudioClip.Create("Small win", 6000, 1, 24000, false);
            var samples = new float[6000];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / 24000f;
                samples[i] = Mathf.Sin(2 * Mathf.PI * (i < 3000 ? 660 : 880) * t) * 0.08f * (1 - i / 6000f);
            }
            successTone.SetData(samples, 0);
        }

        void Start()
        {
            if (!WaitingForStart) return;
            var router = FindFirstObjectByType<PlayerModeRouter>();
            var camera = router != null ? router.ActiveCamera : Camera.main;
            var vr = router != null ? router.IsVR : Application.platform == RuntimePlatform.Android;
            if (vr && camera == null)
            {
                Debug.LogWarning("Focus Shift could not show the study start panel without a VR camera.");
                StartRun(null);
                return;
            }
            if (analytics.ServerConfigured)
            {
                var requestId = System.Guid.NewGuid().ToString("D");
                StudyStartPanel.ShowEnrollment(camera, vr,
                    done => StartCoroutine(EnrollAndStart(requestId, done)), () => StartRun(null));
                return;
            }
            var (code, remaining) = analytics.DeviceStatus();
            StudyStartPanel.Show(camera, vr, code, remaining, StartRun);
        }

        IEnumerator EnrollAndStart(string requestId, System.Action<bool> done)
        {
            yield return analytics.Enroll(requestId, (code, ticket) =>
            {
                if (ticket == null || !WaitingForStart) { done(false); return; }
                startedAt = Time.time;
                if (!analytics.Begin(this, ticket: ticket)) { done(false); return; }
                LastPlayerCode = code;
                WaitingForStart = false;
                Notify(GameText.Format("Player {0} • Results will be sent when the round ends.", code));
                done(true);
            });
        }

        bool StartRun(string participantCode)
        {
            if (!WaitingForStart) return false;
            startedAt = Time.time;
            if (participantCode != null && !analytics.Begin(this, participantCode)) return false;
            WaitingForStart = false;
            Notify(CurrentObjective);
            return true;
        }

        void Update()
        {
            if (WaitingForStart) return;
            var quest = CurrentQuest;
            if (!finished && quest != previousQuest)
            {
                previousQuest = quest;
                Notify(CurrentObjective, true);
            }
            if (!finished && AllComplete)
            {
                finished = true;
                finishTime = Time.time - startedAt;
                LastResult = new HomeSessionResult(this);
                analytics?.End(this, LastResult);
                Notify(GameText.Get("All four tasks complete. Your home is ready!"), true);
                StartCoroutine(ShowResults());
            }
        }

        void LateUpdate() => analytics?.Checkpoint(this);

        void OnApplicationPause(bool paused)
        {
            if (paused) analytics?.Checkpoint(this, true);
        }

        void OnApplicationQuit() => analytics?.End(this, null);

        public bool IsQuestActive(HomeQuest quest) => !WaitingForStart && !finished && !restarting && CurrentQuest == quest;

        public bool CanPerform(HomeQuest quest, bool showHint = false)
        {
            if (IsQuestActive(quest)) return true;
            if (showHint) Notify(CurrentObjective);
            return false;
        }

        public string QuestStatus(HomeQuest quest, string progress)
        {
            if (quest > CurrentQuest) return GameText.Get("LOCKED");
            return quest < CurrentQuest ? GameText.Get("DONE") : progress;
        }

        public static string QuestName(HomeQuest quest) => quest switch
        {
            HomeQuest.Washing => GameText.Get("Wash dishes"),
            HomeQuest.Organizing => GameText.Get("Organize dishes"),
            HomeQuest.Trash => GameText.Get("Take trash outside"),
            HomeQuest.Meeting => GameText.Get("Meeting + quiz"),
            _ => GameText.Get("Home complete")
        };

        IEnumerator ShowResults()
        {
            yield return SceneFade.FadeOut();
            yield return SceneManager.LoadSceneAsync(ResultsScenePath);
        }

        public void Notify(string message, bool smallWin = false)
        {
            toast = message;
            toastUntil = Time.unscaledTime + 4;
            if (smallWin && feedback != null) feedback.PlayOneShot(successTone, 0.5f);
        }

        public void RecoverLooseItems()
        {
            if (WaitingForStart || finished || restarting) return;
            foreach (var item in FindObjectsByType<HomeItem>(FindObjectsSortMode.None))
                if (!item.IsHeld && !item.IsLocked) item.ReturnHome();
            Notify(GameText.Get("Loose items returned to their starting places. Completed tasks are kept."));
        }

        public void RestartSession()
        {
            if (WaitingForStart || restarting || finished) return;
            analytics?.End(this, null);
            restarting = true;
            StartCoroutine(Restart());
        }

        IEnumerator Restart()
        {
            var scenePath = gameObject.scene.path;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            yield return SceneFade.FadeOut();
            yield return SceneManager.LoadSceneAsync(scenePath);
        }

        void OnDestroy()
        {
            analytics?.Release(this);
            if (Instance == this) Instance = null;
            if (successTone != null) Destroy(successTone);
        }
    }
}
