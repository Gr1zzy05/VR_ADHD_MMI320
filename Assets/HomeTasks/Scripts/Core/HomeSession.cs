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
        public float Elapsed => finished ? finishTime : Time.time - startedAt;
        public bool Finished => finished;
        public string Toast => Time.unscaledTime < toastUntil ? toast : string.Empty;
        public HomeQuest CurrentQuest => !WashingComplete ? HomeQuest.Washing : !SortingComplete ? HomeQuest.Organizing : !TrashComplete ? HomeQuest.Trash : meeting == null || !meeting.Completed ? HomeQuest.Meeting : HomeQuest.Complete;
        public string CurrentQuestName => QuestName(CurrentQuest);
        public string CurrentObjective => CurrentQuest switch
        {
            HomeQuest.Washing => $"Quest 1/4: Wash all {dishes.Count} dishes in the running sink.",
            HomeQuest.Organizing => "Quest 2/4: Put all clean dishes on their matching shelves.",
            HomeQuest.Trash => $"Quest 3/4: Take all {rubbish.Count} bags to the outdoor bin.",
            HomeQuest.Meeting => meeting != null && meeting.seat != null ? meeting.State switch
            {
                MeetingState.Listening => "Quest 4/4: Stay seated and listen to the meeting.",
                MeetingState.Quiz => "Quest 4/4: Stay seated and answer on the meeting screen.",
                _ => "Quest 4/4: Select the chair at the desk to sit and listen."
            } : "Quest 4/4: Join the meeting. Listen, then answer once.",
            _ => "All four quests complete. Your results are ready."
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

        internal void RecordDistraction(DistractionKind kind, bool began, float duration)
        {
            if (!finished && !restarting) distractions.Record(kind, began, duration);
        }

        internal DistractionRecord[] SnapshotDistractions() => distractions.Snapshot();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            LastResult = null;
            LastGameplayScenePath = GameplayScenePath;
        }

        void Awake()
        {
            Instance = this;
            LastGameplayScenePath = gameObject.scene.path;
            LastResult = null;
            startedAt = Time.time;
            successTone = AudioClip.Create("Small win", 6000, 1, 24000, false);
            var samples = new float[6000];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / 24000f;
                samples[i] = Mathf.Sin(2 * Mathf.PI * (i < 3000 ? 660 : 880) * t) * 0.08f * (1 - i / 6000f);
            }
            successTone.SetData(samples, 0);
        }

        void Update()
        {
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
                Notify("All four tasks complete. Your home is ready!", true);
                StartCoroutine(ShowResults());
            }
        }

        public bool IsQuestActive(HomeQuest quest) => !finished && !restarting && CurrentQuest == quest;

        public bool CanPerform(HomeQuest quest, bool showHint = false)
        {
            if (IsQuestActive(quest)) return true;
            if (showHint) Notify(CurrentObjective);
            return false;
        }

        public string QuestStatus(HomeQuest quest, string progress)
        {
            if (quest > CurrentQuest) return "LOCKED";
            return quest < CurrentQuest ? "DONE" : progress;
        }

        public static string QuestName(HomeQuest quest) => quest switch
        {
            HomeQuest.Washing => "Wash dishes",
            HomeQuest.Organizing => "Organize dishes",
            HomeQuest.Trash => "Take trash outside",
            HomeQuest.Meeting => "Meeting + quiz",
            _ => "Home complete"
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
            if (finished || restarting) return;
            foreach (var item in FindObjectsByType<HomeItem>(FindObjectsSortMode.None))
                if (!item.IsHeld && !item.IsLocked) item.ReturnHome();
            Notify("Loose items returned to their starting places. Completed tasks are kept.");
        }

        public void RestartSession()
        {
            if (restarting || finished) return;
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
            if (Instance == this) Instance = null;
            if (successTone != null) Destroy(successTone);
        }
    }
}
