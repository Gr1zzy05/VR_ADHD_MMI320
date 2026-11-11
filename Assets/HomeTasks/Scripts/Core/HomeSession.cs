using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HomeTasks
{
    public sealed class HomeSession : MonoBehaviour
    {
        public static HomeSession Instance { get; private set; }
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

        float startedAt;
        float finishTime;
        bool finished;
        bool restarting;
        string toast;
        float toastUntil;
        AudioClip successTone;

        void Awake()
        {
            Instance = this;
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
            if (!finished && AllComplete)
            {
                finished = true;
                finishTime = Time.time - startedAt;
                Notify("All four tasks complete. Your home is ready!", true);
            }
        }

        public void Notify(string message, bool smallWin = false)
        {
            toast = message;
            toastUntil = Time.unscaledTime + 4;
            if (smallWin && feedback != null) feedback.PlayOneShot(successTone, 0.5f);
        }

        public void RecoverLooseItems()
        {
            foreach (var item in FindObjectsByType<HomeItem>(FindObjectsSortMode.None))
                if (!item.IsHeld && !item.IsLocked) item.ReturnHome();
            Notify("Loose items returned to their starting places. Completed tasks are kept.");
        }

        public void RestartSession()
        {
            if (!restarting) StartCoroutine(Restart());
        }

        IEnumerator Restart()
        {
            restarting = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            yield return null;
            yield return SceneManager.LoadSceneAsync(gameObject.scene.buildIndex);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (successTone != null) Destroy(successTone);
        }
    }
}
