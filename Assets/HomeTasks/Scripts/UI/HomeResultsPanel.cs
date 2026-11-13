using System.Collections;
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
        public UnityEngine.UI.Button restart;
        bool transitioning = true;

        IEnumerator Start()
        {
            restart.interactable = false;
            var result = HomeSession.LastResult;
            title.text = result != null ? "Four quests. Well done." : "Your session results";
            if (result != null)
            {
                var seconds = Mathf.FloorToInt(result.Elapsed);
                duration.text = $"YOUR TIME   {seconds / 60:00}:{seconds % 60:00}";
                chores.text = $"01   Wash dishes<pos=80%>{result.Washed}/{result.DishCount}\n02   Organize dishes<pos=80%>{result.Organized}/{result.DishCount}\n03   Take trash outside<pos=80%>{result.Disposed}/{result.TrashCount}";
                score.text = $"04   MEETING + QUIZ\n{result.CorrectAnswers} / {result.QuestionCount} correct";
                feedback.text = (result.QuizPassed ? "Listening target reached." : $"Listening target: {result.RequiredScore}/{result.QuestionCount} correct.") + "\nOne attempt recorded. All four quests are complete.";
            }
            else
            {
                duration.text = "NO COMPLETED SESSION";
                chores.text = "Complete the four quests to see your progress here.";
                score.text = "Meeting score: --";
                feedback.text = "Start a new session when you are ready.";
            }
            if (Application.platform != RuntimePlatform.Android && !UnityEngine.XR.XRSettings.isDeviceActive)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            yield return SceneFade.FadeIn();
            transitioning = false;
            restart.interactable = true;
        }

        public void RestartSession()
        {
            if (transitioning) return;
            transitioning = true;
            restart.interactable = false;
            StartCoroutine(Restart());
        }

        IEnumerator Restart()
        {
            yield return SceneFade.FadeOut();
            yield return SceneManager.LoadSceneAsync(HomeSession.GameplayScenePath);
        }
    }
}
