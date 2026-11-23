using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UI;
using Net.Codecrete.QrCodeGenerator;
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

        Texture2D qrTexture;
        void OnDestroy() { if (qrTexture != null) Destroy(qrTexture); }

        void OnEnable() => GameText.Changed += RefreshSummary;
        void OnDisable() => GameText.Changed -= RefreshSummary;

        IEnumerator Start()
        {
            restart.interactable = false;
            RefreshSummary();
            AddMobileResults();
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
                if (distractionNames != null)
                {
                    distractionNames.text = string.Join("\n", result.Distractions.Select(row => row.Name));
                    distractionTimes.text = string.Join("\n", result.Distractions.Select(row => FormatTime(row.Seconds)));
                    distractionUses.text = string.Join("\n", result.Distractions.Select(row => row.Uses));
                }
            }
            else
            {
                duration.text = GameText.Get("NO COMPLETED SESSION");
                chores.text = GameText.Get("Complete the four quests to see your progress here.");
                score.text = GameText.Get("Meeting score: --");
                feedback.text = GameText.Get("Start a new session when you are ready.");
                if (distractionNames != null)
                    distractionNames.text = string.Join("\n", new DistractionLedger().Snapshot().Select(row => row.Name));
            }
        }

                void AddMobileResults()
        {
            var result = HomeSession.LastResult;
            var origin = GameServerOrigin.Normalize(Resources.Load<TextAsset>("FocusShiftServer")?.text);
            if (result == null || HomeSession.LastPlayerCode == null || origin == null) return;

            // Fragment stays on the phone. No account ID, server credential or database lookup.
            var values = new List<int> { 1, Mathf.FloorToInt(result.Elapsed), result.Washed,
                result.Organized, result.DishCount, result.Disposed, result.TrashCount,
                result.CorrectAnswers, result.QuestionCount };
            foreach (var row in result.Distractions)
                values.AddRange(new[] { (int)row.Kind, row.Uses, Mathf.RoundToInt(row.Seconds * 10f) });
            var url = origin + "/result.html#" + string.Join(".", values.Select(n => n.ToString(CultureInfo.InvariantCulture)));

            var button = Instantiate(restart, restart.transform.parent);
            button.name = "Mobile result QR";
            button.onClick = new Button.ButtonClickedEvent();
            button.interactable = true;
            var original = (RectTransform)restart.transform;
            float half = (original.rect.width - 16) / 2;
            original.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, half);
            var rect = (RectTransform)button.transform;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, half);
            rect.anchoredPosition = original.anchoredPosition + Vector2.right * (half + 16);
            var label = button.GetComponentInChildren<TMP_Text>();
            var binding = label.GetComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();
            if (binding != null) binding.enabled = false;
            foreach (var caption in new[] { label, restart.GetComponentInChildren<TMP_Text>() })
            {
                var bounds = caption.rectTransform;
                bounds.anchorMin = Vector2.zero;
                bounds.anchorMax = Vector2.one;
                bounds.offsetMin = new Vector2(8, 2);
                bounds.offsetMax = new Vector2(-8, -2);
            }
            label.text = "SCAN QR";

            var image = new GameObject("Scan with phone", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(transform, false);
            image.raycastTarget = false;
            var box = image.rectTransform;
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 1);
            box.anchoredPosition = new Vector2(0, -78);
            box.sizeDelta = Vector2.one * 590;
            image.gameObject.SetActive(false);
            button.onClick.AddListener(() =>
            {
                if (qrTexture == null)
                {
                    var qr = QrCode.EncodeText(url, QrCode.Ecc.Medium);
                    var side = qr.Size + 8;
                    qrTexture = new Texture2D(side, side, TextureFormat.RGB24, false);
                    qrTexture.filterMode = FilterMode.Point;
                    for (var y = 0; y < side; y++)
                        for (var x = 0; x < side; x++)
                            qrTexture.SetPixel(x, side - 1 - y, qr.GetModule(x - 4, y - 4) ? Color.black : Color.white);
                    qrTexture.Apply();
                    image.texture = qrTexture;
                }
                bool show = !image.gameObject.activeSelf;
                image.gameObject.SetActive(show);
                label.text = show ? "CLOSE QR" : "SCAN QR";
            });
        }
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
