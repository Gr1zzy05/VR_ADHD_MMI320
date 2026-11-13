using TMPro;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace HomeTasks.Editor
{
    public static partial class HomeSceneBuilder
    {
        public static void ApplyScenePolish()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode before saving scene changes.");
            environment = GameObject.Find("Environment_Placeholder_ReplaceWithYourHome").transform;
            session = Object.FindFirstObjectByType<HomeSession>();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            var sun = environment.GetComponentInChildren<Light>();
            sun.intensity = 0.95f;
            if (environment.Find("Removable blockout ceiling") == null)
            {
                var ceiling = Block(environment, "Removable blockout ceiling", new Vector3(0, 3.25f, 0.5f), new Vector3(12.2f, 0.1f, 9.2f), Mat("Ceiling", "E5E4D9"));
                ceiling.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ceiling.isStatic = true;
                foreach (var x in new[] { -3.6f, 0f, 3.7f })
                    Block(environment, "Ceiling light panel", new Vector3(x, 3.19f, 1.0f), new Vector3(0.7f, 0.025f, 0.7f), Mat("Light diffuser", "F2EEE0"), false).isStatic = true;
            }
            var hud = Object.FindFirstObjectByType<DesktopHUD>(FindObjectsInactive.Include);
            hud.transform.Find("Subtitle").GetComponent<TMP_Text>().fontSize = 14;
            hud.taskSummary.fontSize = 19;
            var router = Object.FindFirstObjectByType<PlayerModeRouter>();
            CreateWristUI(router.questRig.transform);
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
            EditorSceneManager.SaveScene(session.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        static void CreateWristUI(Transform rig)
        {
            var controller = rig.GetComponentsInChildren<Transform>(true).First(t => t.name == "Left Controller");
            if (controller.Find("Wrist task display") != null) return;
            var canvas = WorldCanvas(controller, "Wrist task display", new Vector3(0, 0.13f, -0.05f), 440, 265, 0.00075f);
            canvas.localRotation = Quaternion.Euler(65, 0, 0);
            Image(canvas, "Wrist background", 0, 0, 440, 265, new Color(Ink.r, Ink.g, Ink.b, 0.96f));
            Image(canvas, "Wrist accent", 0, 0, 440, 5, Sage);
            var display = canvas.gameObject.AddComponent<VRTaskDisplay>();
            display.session = session;
            display.summary = Text(canvas, "Wrist progress", 20, 23, 400, 108, 26, Cream, "HOME FOCUS  0/4\nWash 0/6   Sort 0/6\nTrash 0/3   Call ready");
            display.message = Text(canvas, "Wrist feedback", 20, 153, 400, 98, 23, Gold, "Quest 1/4: Wash every dish to unlock organizing.");
        }

        static void CreateBoard()
        {
            var root = Group("Task Board_MoveToYourWall", gameplay, new Vector3(0, 1.72f, 4.76f));
            var canvas = WorldCanvas(root, "Home Focus task board", Vector3.zero, 960, 1000, 0.00245f);
            Image(canvas, "Board background", 0, 0, 960, 1000, Ink);
            Image(canvas, "Top accent", 0, 0, 960, 8, Sage);
            Text(canvas, "Brand", 42, 38, 550, 30, 22, Muted, "HOME FOCUS  /  DAILY LIVING");
            Text(canvas, "Title", 42, 91, 880, 127, 59, Cream, "A little focus.\nA lighter home.", FontStyles.Bold);
            var dashboard = root.gameObject.AddComponent<HomeDashboard>();
            dashboard.session = session;
            dashboard.status = Text(canvas, "Completion", 44, 236, 550, 36, 22, Gold, "QUEST 1 OF 4  /  WASH DISHES");
            dashboard.timer = Text(canvas, "Your pace", 630, 243, 290, 26, 19, Muted, "YOUR PACE  00:00");
            dashboard.counts = new TMP_Text[4];
            dashboard.bars = new UnityEngine.UI.Image[4];
            var names = new[] { "Wash the dishes", "Find their place", "Take the trash outside", "Listen. Then answer." };
            var details = new[] { "Running water + sponge. Six dishes.", "Clean plates, bowls and cups on matching shelves.", "Carry three bags through the doorway to the bin.", "A short meeting, followed by three questions." };
            var accents = new[] { Gold, Sage, Clay, Blue };
            for (var i = 0; i < 4; i++)
            {
                var y = 302 + i * 120;
                Image(canvas, "Task divider", 44, y - 16, 872, 1, new Color(1, 1, 1, 0.13f));
                Image(canvas, "Task number tile", 44, y + 8, 53, 53, accents[i]);
                Text(canvas, "Task number", 50, y + 16, 43, 32, 25, Ink, (i + 1).ToString("00"), FontStyles.Bold);
                Text(canvas, names[i], 119, y + 2, 630, 40, 31, Cream, names[i], FontStyles.Bold);
                Text(canvas, "Description", 119, y + 45, 720, 32, 21, Muted, details[i]);
                dashboard.counts[i] = Text(canvas, "Progress", 777, y + 6, 140, 39, 28, accents[i], i == 0 ? "0 / 6" : "LOCKED");
                var track = Image(canvas, "Progress track", 119, y + 88, 792, 5, new Color(1, 1, 1, 0.12f));
                var fill = Image(track.transform, "Progress fill", 0, 0, 792, 5, accents[i]);
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = new Vector2(0, 1);
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                dashboard.bars[i] = fill;
            }
            dashboard.footer = Text(canvas, "Encouragement", 44, 802, 866, 64, 25, Muted, "Quest 1/4: Wash every dish to unlock organizing.");
            Button(canvas, "Restart session", 44, 902, 410, 64, Sage, Ink, "RESTART SESSION", session.RestartSession);
            Button(canvas, "Recover loose items", 478, 902, 438, 64, new Color(1, 1, 1, 0.12f), Cream, "RECOVER LOOSE ITEMS", session.RecoverLooseItems);

            var howTo = WorldCanvas(gameplay, "Controls and welcome", new Vector3(0, 1.65f, -3.85f), 880, 600, 0.0025f);
            howTo.localRotation = Quaternion.Euler(0, 180, 0);
            Image(howTo, "Welcome panel", 0, 0, 880, 600, Ink);
            Text(howTo, "Welcome heading", 40, 35, 800, 70, 45, Cream, "One space. Four small wins.", FontStyles.Bold);
            Text(howTo, "Quest controls", 40, 135, 800, 180, 27, Muted, "META QUEST\nGrip to grab or press the gold tap control.\nPoint + trigger for screen buttons.\nRight stick forward: aim teleport; release to travel.\nRight stick sideways: snap turn.");
            Text(howTo, "Desktop controls", 40, 355, 800, 175, 25, Cream, "DESKTOP TESTING\nWASD walk / mouse look / click grab or release.\nE use / Q drop / scroll reach / R rotate.\nHold E with the sponge aimed at a dish to scrub.\nTab frees the cursor for buttons; 1 / 2 / 3 answer the quiz.");
        }

        static void CreateMeetingUI(Transform root, MeetingController meeting)
        {
            var canvas = WorldCanvas(root, "Meeting screen", new Vector3(0, 1.78f, 0.162f), 860, 780, 0.0021f);
            Image(canvas, "Screen", 0, 0, 860, 780, Hex("F0EEE5"));
            Image(canvas, "Header accent", 0, 0, 860, 8, Blue);
            var panel = root.gameObject.AddComponent<MeetingPanel>();
            panel.meeting = meeting;
            panel.connection = Text(canvas, "Connection", 35, 29, 790, 27, 19, Hex("58756B"), "SIMULATED CALL  /  AUDIO + CAPTIONS");
            Text(canvas, "Meeting title", 35, 73, 790, 60, 40, Ink, "Neighbourhood check-in", FontStyles.Bold);
            Image(canvas, "Participant strip", 35, 145, 790, 58, Hex("DFE6DB"));
            Image(canvas, "Host avatar", 48, 154, 40, 40, Blue);
            Text(canvas, "Host initial", 56, 157, 30, 35, 26, Ink, "M", FontStyles.Bold);
            Text(canvas, "Participants", 106, 163, 695, 30, 22, Ink, "Maya  /  Host                          You  /  At home");
            panel.progress = Text(canvas, "Call progress", 35, 223, 790, 32, 21, Hex("58756B"), "LISTEN FIRST. ANSWER AT THE END.", FontStyles.Bold);
            panel.body = Text(canvas, "Captions and question", 35, 270, 790, 191, 27, Ink, "Join the meeting, listen to the full briefing, then answer three questions.");
            panel.result = Text(canvas, "Quiz feedback", 35, 467, 790, 125, 27, Hex("52775A"), string.Empty);
            panel.options = new UnityEngine.UI.Button[3];
            panel.optionLabels = new TMP_Text[3];
            UnityAction[] answers = { meeting.AnswerFirst, meeting.AnswerSecond, meeting.AnswerThird };
            for (var i = 0; i < 3; i++)
            {
                panel.options[i] = Button(canvas, "Answer " + (i + 1), 35, 467 + i * 69, 790, 59, Hex("DFE6DB"), Ink, (i + 1) + "   Answer", answers[i]);
                panel.optionLabels[i] = panel.options[i].GetComponentInChildren<TMP_Text>();
                panel.options[i].gameObject.SetActive(false);
            }
            panel.begin = Button(canvas, "Join meeting - one attempt", 35, 695, 790, 56, Ink, Cream, "LOCKED  /  COMPLETE QUESTS 1-3", meeting.Begin);
            panel.begin.interactable = false;
            panel.beginLabel = panel.begin.GetComponentInChildren<TMP_Text>();
        }

        static void CreateDesktopUI(Transform parent, DesktopPlayer player)
        {
            var go = new GameObject("Desktop HUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            var hud = go.AddComponent<DesktopHUD>();
            hud.session = session;
            hud.player = player;
            Image(go.transform, "Brand background", 24, 24, 290, 84, new Color(Cream.r, Cream.g, Cream.b, 0.94f));
            Text(go.transform, "Brand", 43, 35, 253, 40, 31, Ink, "HOME FOCUS", FontStyles.Bold);
            Text(go.transform, "Subtitle", 44, 77, 250, 23, 14, Hex("58756B"), "ONE SPACE. FOUR SMALL WINS.");
            var summary = Image(go.transform, "Task summary panel", 1250, 24, 324, 260, new Color(Ink.r, Ink.g, Ink.b, 0.93f));
            summary.rectTransform.anchorMin = summary.rectTransform.anchorMax = Vector2.one;
            summary.rectTransform.pivot = Vector2.one;
            summary.rectTransform.anchoredPosition = new Vector2(-24, -24);
            hud.taskSummary = Text(summary.transform, "Task summary", 22, 23, 285, 220, 19, Cream, "YOUR FOUR SMALL WINS");
            var promptPanel = Image(go.transform, "Interaction bar", 100, 832, 1400, 47, new Color(Ink.r, Ink.g, Ink.b, 0.88f));
            promptPanel.rectTransform.anchorMin = promptPanel.rectTransform.anchorMax = new Vector2(0.5f, 0);
            promptPanel.rectTransform.pivot = new Vector2(0.5f, 0);
            promptPanel.rectTransform.anchoredPosition = new Vector2(0, 20);
            hud.prompt = Text(promptPanel.transform, "Interaction prompt", 15, 12, 1370, 33, 21, Cream, "Click the game to look around.");
            hud.prompt.alignment = TextAlignmentOptions.Center;
            hud.toast = Text(go.transform, "Small win notification", 300, 746, 1000, 60, 28, Cream, string.Empty, FontStyles.Bold);
            hud.toast.rectTransform.anchorMin = hud.toast.rectTransform.anchorMax = new Vector2(0.5f, 0);
            hud.toast.rectTransform.pivot = new Vector2(0.5f, 0);
            hud.toast.rectTransform.anchoredPosition = new Vector2(0, 88);
            hud.toast.alignment = TextAlignmentOptions.Center;
            var center = Image(go.transform, "Aim dot", 0, 0, 5, 5, Cream);
            center.rectTransform.anchorMin = center.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            center.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            center.rectTransform.anchoredPosition = Vector2.zero;
            hud.crosshair = center.gameObject;
        }

        static void StationSign(Transform parent, Vector3 position, string title, string description, Color accent, float yaw, float scale = 0.0018f)
        {
            var canvas = WorldCanvas(parent, title + " sign", position, 760, 200, scale);
            canvas.localRotation = Quaternion.Euler(0, yaw, 0);
            Image(canvas, "Sign background", 0, 0, 760, 200, Ink);
            Image(canvas, "Accent strip", 0, 0, 9, 200, accent);
            Text(canvas, "Station title", 34, 22, 700, 53, 37, accent, title, FontStyles.Bold);
            Text(canvas, "Station instructions", 34, 91, 700, 97, 29, Cream, description);
        }

        static RectTransform WorldCanvas(Transform parent, string name, Vector3 position, float width, float height, float scale)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localPosition = position;
            rect.localScale = Vector3.one * scale;
            rect.sizeDelta = new Vector2(width, height);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            go.GetComponent<UnityEngine.UI.CanvasScaler>().dynamicPixelsPerUnit = 10;
            return rect;
        }

        static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        static UnityEngine.UI.Image Image(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static TMP_Text Text(Transform parent, string name, float x, float y, float width, float height, int size, Color color, string value, FontStyles style = FontStyles.Normal)
        {
            var text = Rect(parent, name, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.text = value;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static UnityEngine.UI.Button Button(Transform parent, string name, float x, float y, float width, float height, Color background, Color foreground, string label, UnityAction action)
        {
            var image = Image(parent, name, x, y, width, height, background);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1);
            colors.pressedColor = new Color(0.75f, 0.9f, 0.78f, 1);
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            var navigation = button.navigation;
            navigation.mode = UnityEngine.UI.Navigation.Mode.None;
            button.navigation = navigation;
            var caption = Text(image.transform, "Label", 14, 13, width - 28, height - 19, 23, foreground, label, FontStyles.Bold);
            caption.alignment = TextAlignmentOptions.Center;
            UnityEventTools.AddPersistentListener(button.onClick, action);
            return button;
        }
    }
}
