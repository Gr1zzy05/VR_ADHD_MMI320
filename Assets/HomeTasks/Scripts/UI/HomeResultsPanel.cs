using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HomeTasks
{
    public sealed class HomeResultsPanel : MonoBehaviour
    {
        public TMP_Text title;
        public TMP_Text duration;
        public TMP_Text chores;
        public TMP_Text score;
        public TMP_Text feedback;
        public TMP_Text distractionNames;
        public TMP_Text distractionTimes;
        public TMP_Text distractionUses;
        public UnityEngine.UI.Button restart;
        bool transitioning = true;

        void OnEnable() => GameText.Changed += RefreshSummary;
        void OnDisable() => GameText.Changed -= RefreshSummary;

        IEnumerator Start()
        {
            restart.interactable = false;
            RefreshSummary();
            if (Application.platform != RuntimePlatform.Android && !UnityEngine.XR.XRSettings.isDeviceActive)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            yield return SceneFade.FadeIn();
            transitioning = false;
            restart.interactable = true;
        }

        void RefreshSummary()
        {
            var result = HomeSession.LastResult;
            title.text = result != null ? GameText.Get("Four quests. Well done.") : GameText.Get("Your session results");
            if (result != null)
            {
                var seconds = Mathf.FloorToInt(result.Elapsed);
                duration.text = GameText.Format("YOUR TIME   {0:00}:{1:00}", seconds / 60, seconds % 60);
                chores.text = GameText.Format("01   {0}<pos=80%>{1}/{2}\n02   {3}<pos=80%>{4}/{2}\n03   {5}<pos=80%>{6}/{7}",
                    HomeSession.QuestName(HomeQuest.Washing), result.Washed, result.DishCount,
                    HomeSession.QuestName(HomeQuest.Organizing), result.Organized,
                    HomeSession.QuestName(HomeQuest.Trash), result.Disposed, result.TrashCount);
                score.text = GameText.Format("04   MEETING + QUIZ\n{0} / {1} correct", result.CorrectAnswers, result.QuestionCount);
                feedback.text = (result.QuizPassed ? GameText.Get("Listening target reached.") :
                    GameText.Format("Listening target: {0}/{1} correct.", result.RequiredScore, result.QuestionCount))
                    + "\n" + GameText.Get("One attempt recorded. All four quests are complete.");
                if (HomeSession.LastPlayerCode != null)
                    feedback.text += "\n" + GameText.Format("Player code: {0}", HomeSession.LastPlayerCode);
            }
            else
            {
                duration.text = GameText.Get("NO COMPLETED SESSION");
                chores.text = GameText.Get("Complete the four quests to see your progress here.");
                score.text = GameText.Get("Meeting score: --");
                feedback.text = GameText.Get("Start a new session when you are ready.");
            }
            if (distractionNames != null)
            {
                var distractions = result?.Distractions ?? new DistractionLedger().Snapshot();
                distractionNames.text = FormatDistractionColumn(distractions.Select(row => row.Name));
                distractionTimes.text = FormatDistractionColumn(distractions.Select(row => FormatTime(row.Seconds)));
                distractionUses.text = FormatDistractionColumn(distractions.Select(row => row.Uses.ToString()));
            }
        }

        // Keep rows aligned when Thai names and Latin numerals use different font metrics.
        static string FormatDistractionColumn(IEnumerable<string> rows) =>
            "<line-height=25>" + string.Join("\n", rows) + "</line-height>";

        public void RestartSession()
        {
            if (transitioning) return;
            transitioning = true;
            restart.interactable = false;
            StartCoroutine(Restart());
        }

        static string FormatTime(float seconds)
        {
            var tenths = Mathf.RoundToInt(seconds * 10f);
            return $"{tenths / 600}:{tenths / 10 % 60:00}.{tenths % 10}";
        }

        IEnumerator Restart()
        {
            yield return SceneFade.FadeOut();
            var gameplay = HomeSession.LastGameplayScenePath;
            if (!Application.CanStreamedLevelBeLoaded(gameplay)) gameplay = SceneUtility.GetScenePathByBuildIndex(0);
            yield return SceneManager.LoadSceneAsync(gameplay);
        }
    }
}
