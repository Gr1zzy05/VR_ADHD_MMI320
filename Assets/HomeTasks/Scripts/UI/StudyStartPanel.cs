using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace HomeTasks
{
    internal static class StudyStartPanel
    {
        static readonly Color Paper = new Color32(246, 250, 252, 255);
        static readonly Color Ink = new Color32(27, 44, 66, 255);
        static readonly Color Blue = new Color32(29, 91, 164, 255);

        internal static void Show(Camera camera, bool vr, string code, int remaining, Func<string, bool> start)
        {
            var overlay = CreateCanvas(camera, vr);
            var card = Card(overlay.transform);
            var instructions = code != null && remaining > 0
                ? GameText.Format("Participant {0}   •   Rounds available: {1}\nStart records and shares this round with authorized study staff.", code, remaining)
                : GameText.Get("No study rounds are available. Ask staff to check the device setup, or play without sharing.");
            var detail = Label(card, instructions, 25, Ink, 1, 125);

            var share = AddButton(card, GameText.Get("Start and share results"), Blue, Color.white, -98, () => {
                if (start(code)) UnityEngine.Object.Destroy(overlay);
                else detail.text = GameText.Get("The study round could not be started. Ask staff to check the setup.");
            });
            share.interactable = code != null && remaining > 0;
            AddButton(card, GameText.Get("Play without sharing"), new Color32(225, 234, 243, 255), Ink, -170, () => {
                if (start(null)) UnityEngine.Object.Destroy(overlay);
            });
        }

        internal static void ShowEnrollment(Camera camera, bool vr, Action<Action<bool>> enroll, Action practice)
        {
            var overlay = CreateCanvas(camera, vr);
            var card = Card(overlay.transform);
            var detail = Label(card, GameText.Get("Share activity time, tasks, quiz results and distraction interactions with authorized study staff under an anonymous player code."), 25, Ink, 1, 125);
            Button record = null, trial = null;
            record = AddButton(card, GameText.Get("Start and record results"), Blue, Color.white, -98, () =>
            {
                record.interactable = trial.interactable = false;
                detail.text = GameText.Get("Getting your player code…");
                enroll(success =>
                {
                    if (overlay == null) return;
                    if (success) UnityEngine.Object.Destroy(overlay);
                    else
                    {
                        detail.text = GameText.Get("Could not get a player code. Try again, or continue in practice mode.");
                        record.interactable = trial.interactable = true;
                    }
                });
            });
            trial = AddButton(card, GameText.Get("Practice without sharing"), new Color32(225, 234, 243, 255), Ink, -170, () =>
            {
                practice();
                UnityEngine.Object.Destroy(overlay);
            });
        }

        static GameObject CreateCanvas(Camera camera, bool vr)
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
            return overlay;
        }

        static RectTransform Card(Transform parent)
        {
            var card = Box(parent, "Study confirmation", new Vector2(730, 400), Vector2.zero);
            card.gameObject.AddComponent<Image>().color = Paper;
            Label(card, "FOCUS SHIFT", 21, Blue, 141, 30);
            Label(card, GameText.Get("Start VR activity"), 42, Ink, 90, 62);
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

        static TMP_Text Label(Transform parent, string text, float size, Color color, float y, float height)
        {
            var box = Box(parent, "Label", new Vector2(650, height), new Vector2(0, y));
            var label = box.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        static Button AddButton(Transform parent, string text, Color background, Color ink, float y, Action click)
        {
            var box = Box(parent, text, new Vector2(520, 54), new Vector2(0, y));
            var image = box.gameObject.AddComponent<Image>();
            image.color = background;
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => click());
            Label(box, text, 26, ink, 0, 54).rectTransform.sizeDelta = box.sizeDelta;
            return button;
        }
    }
}

