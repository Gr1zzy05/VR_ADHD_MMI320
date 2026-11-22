using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace HomeTasks.Editor
{
    public static partial class HomeSceneBuilder
    {
        public const string ResultsScenePath = HomeSession.ResultsScenePath;

        [MenuItem("Home Focus/Apply ordered quests and results scene")]
        public static void ApplyQuestFlow()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating the quest flow.");
            var home = SceneManager.GetActiveScene();
            if (home.path != ScenePath) throw new InvalidOperationException("Open HomeFocus before applying the quest flow.");
            session = UnityEngine.Object.FindFirstObjectByType<HomeSession>();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (session == null || font == null) throw new InvalidOperationException("The home session and TMP font must be present.");
            var panel = UnityEngine.Object.FindFirstObjectByType<MeetingPanel>();
            if (panel.retry != null)
            {
                panel.retry.gameObject.SetActive(false);
                panel.retry.interactable = false;
                while (panel.retry.onClick.GetPersistentEventCount() > 0)
                    UnityEventTools.RemovePersistentListener(panel.retry.onClick, 0);
            }
            panel.begin.gameObject.name = "Join meeting - one attempt";
            panel.begin.interactable = false;
            panel.beginLabel.text = "LOCKED  /  COMPLETE QUESTS 1-3";
            panel.connection.text = "QUEST 4  /  LOCKED";
            panel.progress.text = "FINISH THE EARLIER QUESTS TO JOIN";
            panel.body.text = "Quest 1/4: Wash all dishes in the running sink.";
            panel.result.text = "One meeting and one quiz attempt per session.";
            foreach (var action in UnityEngine.Object.FindObjectsByType<HomeAction>(FindObjectsSortMode.None))
                if (Enumerable.Range(0, action.onUse.GetPersistentEventCount()).Any(i => action.onUse.GetPersistentTarget(i) == session.meeting))
                    action.instruction = "join the meeting (quest 4, one attempt)";
            var board = UnityEngine.Object.FindFirstObjectByType<HomeDashboard>();
            board.footer.text = "Quest 1/4: Wash every dish to unlock organizing.";
            board.status.text = "QUEST 1 OF 4  /  WASH DISHES";
            board.status.fontSize = 22;
            for (var i = 1; i < board.counts.Length; i++) board.counts[i].text = "LOCKED";
            foreach (var display in UnityEngine.Object.FindObjectsByType<VRTaskDisplay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                display.message.text = "Quest 1/4: Wash every dish to unlock organizing.";
            EditorSceneManager.MarkSceneDirty(home);
            EditorSceneManager.SaveScene(home);
            EnsureResultsScene(UnityEngine.Object.FindFirstObjectByType<PlayerModeRouter>());
            AssetDatabase.SaveAssets();
        }

        static void EnsureResultsScene(PlayerModeRouter source)
        {
            if (!File.Exists(ResultsScenePath))
            {
                var home = SceneManager.GetActiveScene();
                var results = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(results);
                try
                {
                    var interactionManager = new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
                    new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
                    var quest = UnityEngine.Object.Instantiate(source.questRig);
                    quest.name = "Quest Results Player (OpenXR)";
                    quest.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    foreach (var provider in quest.GetComponentsInChildren<LocomotionProvider>(true)) provider.enabled = false;
                    foreach (var display in quest.GetComponentsInChildren<VRTaskDisplay>(true))
                    {
                        display.session = null;
                        display.gameObject.SetActive(false);
                    }
                    foreach (var interactor in quest.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>(true))
                        interactor.interactionManager = interactionManager;
                    var xrCamera = quest.GetComponent<Unity.XR.CoreUtils.XROrigin>().Camera;
                    xrCamera.backgroundColor = Ink;
                    xrCamera.clearFlags = CameraClearFlags.SolidColor;
                    var desktop = new GameObject("Desktop Results Player");
                    var cameraObject = new GameObject("Desktop Results Camera", typeof(Camera), typeof(AudioListener));
                    cameraObject.transform.SetParent(desktop.transform, false);
                    cameraObject.transform.localPosition = new Vector3(0, 1.65f, 0);
                    cameraObject.tag = "MainCamera";
                    var camera = cameraObject.GetComponent<Camera>();
                    camera.nearClipPlane = 0.05f;
                    camera.farClipPlane = 30;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = Ink;
                    camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                    var router = new GameObject("Results player mode").AddComponent<PlayerModeRouter>();
                    router.questRig = quest;
                    router.desktopRig = desktop;
                    var world = WorldCanvas(quest.transform, "VR results", new Vector3(0, 1.65f, 2.4f), 1000, 800, 0.002f);
                    CreateResultsUI(world);
                    var overlay = new GameObject("Desktop results", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
                    overlay.transform.SetParent(desktop.transform, false);
                    overlay.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                    var scaler = overlay.GetComponent<UnityEngine.UI.CanvasScaler>();
                    scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1600, 900);
                    scaler.matchWidthOrHeight = 0.5f;
                    var card = Rect(overlay.transform, "Results card", 0, 0, 1000, 800);
                    card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
                    card.anchoredPosition = Vector2.zero;
                    CreateResultsUI(card);
                    quest.SetActive(false);
                    desktop.SetActive(true);
                    EditorSceneManager.SaveScene(results, ResultsScenePath);
                }
                finally
                {
                    SceneManager.SetActiveScene(home);
                    EditorSceneManager.CloseScene(results, true);
                }
            }
            var scenes = EditorBuildSettings.scenes.ToList();
            var index = scenes.FindIndex(s => s.path == ResultsScenePath);
            if (index < 0) scenes.Add(new EditorBuildSettingsScene(ResultsScenePath, true));
            else scenes[index] = new EditorBuildSettingsScene(ResultsScenePath, true);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void CreateResultsUI(RectTransform canvas)
        {
            Image(canvas, "Results background", 0, 0, 1000, 800, Ink);
            Image(canvas, "Results accent", 0, 0, 1000, 7, Sage);
            Text(canvas, "Brand", 42, 36, 916, 32, 22, Muted, "HOME FOCUS  /  SESSION RESULTS");
            var panel = canvas.gameObject.AddComponent<HomeResultsPanel>();
            panel.title = Text(canvas, "Results title", 42, 95, 916, 80, 54, Cream, "Four quests. Well done.", FontStyles.Bold);
            panel.duration = Text(canvas, "Session duration", 42, 186, 916, 36, 24, Gold, "YOUR TIME   00:00");
            Image(canvas, "Chores divider", 42, 246, 916, 1, new Color(1, 1, 1, 0.15f));
            panel.chores = Text(canvas, "Chore results", 42, 270, 916, 145, 29, Cream, "Wash dishes\nOrganize dishes\nTake trash outside");
            Image(canvas, "Meeting divider", 42, 433, 916, 1, new Color(1, 1, 1, 0.15f));
            panel.score = Text(canvas, "Meeting score", 42, 460, 916, 100, 38, Blue, "04   MEETING + QUIZ\n-- / 3 correct", FontStyles.Bold);
            panel.feedback = Text(canvas, "Meeting feedback", 42, 570, 916, 80, 24, Muted, "One meeting and one quiz attempt per session.");
            panel.restart = Button(canvas, "Start new session", 42, 685, 916, 70, Sage, Ink, "START NEW SESSION", panel.RestartSession);
            Text(canvas, "Footer", 42, 769, 916, 24, 18, Muted, "ONE SPACE. FOUR SMALL WINS.");
            ConfigureDistractionResults(panel);
        }
    }
}
