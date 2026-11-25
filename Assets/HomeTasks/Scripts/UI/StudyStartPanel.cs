using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace HomeTasks
{
    internal sealed class StudyStartPanel : MonoBehaviour
    {
        static readonly Color Paper = new Color32(246, 250, 252, 255);
        static readonly Color Ink = new Color32(27, 44, 66, 255);
        static readonly Color Blue = new Color32(29, 91, 164, 255);
        static readonly Color SecondaryButton = new Color32(225, 234, 243, 255);
        Action refreshLabels;

        void OnEnable() => GameText.Changed += RefreshLabels;
        void OnDisable() => GameText.Changed -= RefreshLabels;
        void RefreshLabels() => refreshLabels?.Invoke();

        internal static void Show(Camera camera, bool vr, string code, int remaining, Func<string, bool> start)
        {
            var overlay = CreateCanvas(camera, vr);
            var card = overlay.Card();
            var canShare = code != null && remaining > 0;
            Func<string> instructions = () => canShare
                ? GameText.Format("Participant {0}   •   Rounds available: {1}\nStart records and shares this round with authorized study staff.", code, remaining)
                : GameText.Get("No study rounds are available. Ask staff to check the device setup, or play without sharing.");
            overlay.Label(card, () => instructions(), 25, Ink, 1, 125);

            var share = overlay.AddButton(card, "Start", () => GameText.Get("Start"), Blue, Color.white, -98, () =>
            {
                if (start(code)) Destroy(overlay.gameObject);
                else
                {
                    instructions = () => GameText.Get("The study round could not be started. Ask staff to check the setup.");
                    overlay.RefreshLabels();
                }
            }, x: -130);
            share.interactable = canShare;
            overlay.AddButton(card, "Test", () => GameText.Get("Test"), SecondaryButton, Ink, -98, () =>
            {
                if (start(null)) Destroy(overlay.gameObject);
            }, x: 130);
            overlay.AddLanguageButton(card, -161);
        }

        internal static void ShowEnrollment(Camera camera, bool vr, Action<Action<bool>> enroll, Action practice)
        {
            var overlay = CreateCanvas(camera, vr);
            var card = Box(overlay.transform, "Start or test", new Vector2(620, 324), Vector2.zero);
            card.gameObject.AddComponent<Image>().color = Paper;
            overlay.Label(card, () => GameText.Get("FOCUS SHIFT"), 32, Ink, 100, 52, 540);
            Func<string> instructions = () => GameText.Get("Start shares your results with study staff.\nTest plays without recording.");
            overlay.Label(card, () => instructions(), 24, Ink, 20, 88, 540);
            Button record = null, trial = null;
            record = overlay.AddButton(card, "Start", () => GameText.Get("Start"), Blue, Color.white, -66, () =>
            {
                record.interactable = trial.interactable = false;
                instructions = () => GameText.Get("Getting your player code…");
                overlay.RefreshLabels();
                enroll(success =>
                {
                    if (overlay == null) return;
                    if (success) Destroy(overlay.gameObject);
                    else
                    {
                        instructions = () => GameText.Get("Could not get a player code.\nTry Start again, or choose Test.");
                        overlay.RefreshLabels();
                        record.interactable = trial.interactable = true;
                    }
                });
            }, x: -130);
            trial = overlay.AddButton(card, "Test", () => GameText.Get("Test"), SecondaryButton, Ink, -66, () =>
            {
                practice();
                Destroy(overlay.gameObject);
            }, x: 130);
            overlay.AddLanguageButton(card, -126);
        }

        static StudyStartPanel CreateCanvas(Camera camera, bool vr)
        {
            var overlay = new GameObject("Start VR activity", typeof(RectTransform), typeof(Canvas));
            var canvas = overlay.GetComponent<Canvas>();
            canvas.sortingOrder = 200;
            var rect = (RectTransform)overlay.transform;
            if (vr)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                rect.SetParent(camera.transform, false);
                rect.localPosition = new Vector3(0, 0, 1.55f);
                rect.localScale = Vector3.one * 0.00165f;
                rect.sizeDelta = new Vector2(730, 400);
                overlay.AddComponent<TrackedDeviceGraphicRaycaster>();
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = overlay.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.matchWidthOrHeight = 0.5f;
                overlay.AddComponent<GraphicRaycaster>();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            return overlay.AddComponent<StudyStartPanel>();
        }

        RectTransform Card()
        {
            var card = Box(transform, "Study confirmation", new Vector2(730, 400), Vector2.zero);
            card.gameObject.AddComponent<Image>().color = Paper;
            Label(card, () => GameText.Get("FOCUS SHIFT"), 21, Blue, 142, 34);
            Label(card, () => GameText.Get("Start VR activity"), 42, Ink, 90, 66);
            return card;
        }

        static RectTransform Box(Transform parent, string name, Vector2 size, Vector2 position)
        {
            var box = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            box.SetParent(parent, false);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.sizeDelta = size;
            box.anchoredPosition = position;
            return box;
        }

        void Label(Transform parent, Func<string> text, float size, Color color, float y, float height, float width = 650)
        {
            var box = Box(parent, "Label", new Vector2(width, height), new Vector2(0, y));
            var label = box.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = string.Empty;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            if (text == null) return;
            label.text = text();
            refreshLabels += () => label.text = text();
        }

        Button AddButton(Transform parent, string name, Func<string> text, Color background, Color ink, float y, Action click,
            float x = 0, float height = 54, float fontSize = 26)
        {
            const float width = 234;
            var box = Box(parent, name, new Vector2(width, height), new Vector2(x, y));
            var image = box.gameObject.AddComponent<Image>();
            image.color = background;
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (click != null) button.onClick.AddListener(() => click());
            Label(box, text, fontSize, ink, 0, height, width);
            box.gameObject.AddComponent<ButtonPressFeedback>();
            return button;
        }

        void AddLanguageButton(Transform card, float y)
        {
            var button = AddButton(card, "Language option", null, SecondaryButton, Ink, y, null, height: 38, fontSize: 20);
            button.gameObject.SetActive(false);
            var option = button.gameObject.AddComponent<LanguageOption>();
            option.button = button;
            option.label = button.GetComponentInChildren<TMP_Text>(true);
            button.onClick.AddListener(option.ToggleLanguage);
            button.gameObject.SetActive(true);
        }
    }
}

