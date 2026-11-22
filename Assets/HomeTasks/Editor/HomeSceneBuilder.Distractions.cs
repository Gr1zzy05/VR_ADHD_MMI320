using System;
using System.Linq;
using TMPro;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace HomeTasks.Editor
{
    public static partial class HomeSceneBuilder
    {
        public static void AddDistractionsToNewModel()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.isDirty || scene.path != Root + "/Scenes/NewModel.unity")
                throw new InvalidOperationException("Open the saved NewModel scene in Edit mode.");

            var roots = scene.GetRootGameObjects();
            var home = roots.Single(root => root.name == "Home").transform;
            var router = roots.SelectMany(root => root.GetComponentsInChildren<PlayerModeRouter>(true)).Single();
            var meeting = roots.SelectMany(root => root.GetComponentsInChildren<MeetingController>(true)).Single();
            var manager = roots.SelectMany(root => root.GetComponentsInChildren<XRInteractionManager>(true)).Single();
            var television = home.Find("livingRoom/Cube.010");
            var desk = home.Find("table 1");
            var toys = new[] { roots.Single(root => root.name == "FidgetCubeInteractive"),
                roots.Single(root => root.name == "RainbowPopIt"), roots.Single(root => root.name == "ButterSquishy") };
            if (roots.Any(root => root.name == "Distractions") || television == null || desk == null || meeting.seat == null)
                throw new InvalidOperationException("The current scene must have the original desk, TV, and seat, without a distraction group.");

            font = meeting.GetComponent<MeetingPanel>().body.font;
            var screen = TelevisionScreenBounds(television);
            var deskSurface = desk.GetComponent<Renderer>().bounds.max.y;
            var playerCamera = router.desktopRig.GetComponentInChildren<Camera>();
            var parent = Group("Distractions", null, Vector3.zero);
            CreateTelevision(parent, television, screen, router, playerCamera, manager);
            CreatePhone(parent, new Vector3(-2.105f, deskSurface, 7.19f), meeting.seat.transform, router, playerCamera, manager);
            CreatePlushie(parent, new Vector3(-3.61f, deskSurface + 0.005f, 7.16f), manager);
            var kinds = new[] { DistractionKind.FidgetCube, DistractionKind.PopIt, DistractionKind.Squishy };
            for (var i = 0; i < toys.Length; i++) toys[i].AddComponent<DistractionUse>().kind = kinds[i];

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var results = EditorSceneManager.OpenScene(HomeSession.ResultsScenePath, OpenSceneMode.Additive);
            try
            {
                foreach (var panel in results.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HomeResultsPanel>(true)))
                    ConfigureDistractionResults(panel);
                EditorSceneManager.MarkSceneDirty(results);
                EditorSceneManager.SaveScene(results);
            }
            finally
            {
                SceneManager.SetActiveScene(scene);
                EditorSceneManager.CloseScene(results, true);
            }
        }

        static Bounds TelevisionScreenBounds(Transform television)
        {
            var renderer = television.GetComponent<MeshRenderer>();
            var screen = Array.FindIndex(renderer.sharedMaterials, material => material != null && material.name == "TV");
            if (screen < 0) throw new InvalidOperationException("The television needs a TV screen material slot.");

            using var mesh = MeshUtility.AcquireReadOnlyMeshData(television.GetComponent<MeshFilter>().sharedMesh);
            var data = mesh[0];
            using var points = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
            using var indices = new NativeArray<int>(data.GetSubMesh(screen).indexCount, Allocator.Temp);
            data.GetVertices(points);
            data.GetIndices(indices, screen);
            var bounds = new Bounds(television.TransformPoint(points[indices[0]]), Vector3.zero);
            foreach (var index in indices) bounds.Encapsulate(television.TransformPoint(points[index]));
            return bounds;
        }

        static void CreateTelevision(Transform parent, Transform television, Bounds screen, PlayerModeRouter router,
            Camera playerCamera, XRInteractionManager manager)
        {
            var materialPath = Root + "/Materials/LakesideTV.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Materials/LakesideTV.shader");
                if (shader == null) throw new InvalidOperationException("The Lakeside television shader is missing.");
                material = new Material(shader) { name = "Lakeside TV" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            var program = Primitive(PrimitiveType.Quad, parent, "Lakeside program", screen.center - Vector3.forward * 0.006f,
                new Vector3(screen.size.x - 0.006f, screen.size.y - 0.006f, 1f), material, false);
            var renderer = program.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            program.SetActive(false);

            var controls = WorldCanvas(parent, "Television controls", screen.center - Vector3.forward * 0.018f, 1000, 530, screen.size.x / 1000);
            ConfigureDistractionCanvas(controls, playerCamera);
            var activity = television.gameObject.AddComponent<ScreenDistraction>();
            activity.kind = DistractionKind.Television;
            activity.player = router;
            activity.displayPoint = controls;
            activity.content = program;
            activity.inactiveLabel = "WATCH TV";
            activity.activeLabel = "TURN OFF TV";
            activity.viewingDistance = 5.7f;
            activity.viewingAngle = 28f;
            var button = Button(controls, "TV power", 719, 446, 257, 59, Ink, Cream, activity.inactiveLabel, activity.Toggle);
            activity.buttonLabel = button.GetComponentInChildren<TMP_Text>();
            BindDistractionAction(activity, television.GetComponent<Collider>(), manager);
        }

        static void CreatePhone(Transform parent, Vector3 position, Transform seat, PlayerModeRouter router,
            Camera playerCamera, XRInteractionManager manager)
        {
            var phone = Group("Desk phone", parent, position);
            Block(phone, "Case", new Vector3(0, 0.013f, 0), new Vector3(0.137f, 0.026f, 0.234f), Mat("Desk phone case", "2A3C3A"), false);
            Block(phone, "Glass", new Vector3(0, 0.027f, 0.004f), new Vector3(0.121f, 0.004f, 0.212f), Mat("Desk phone display", "263F4C", 0.5f), false);
            Block(phone, "Earpiece", new Vector3(0, 0.030f, -0.083f), new Vector3(0.035f, 0.002f, 0.004f), Mat("Desk phone case", "2A3C3A"), false);
            var shape = phone.gameObject.AddComponent<BoxCollider>();
            shape.center = Vector3.up * 0.016f;
            shape.size = new Vector3(0.137f, 0.031f, 0.234f);

            var displayPosition = position + new Vector3(0, 0.30f, -0.07f);
            var screen = WorldCanvas(parent, "Phone controls", displayPosition, 438, 576, 0.00095f);
            screen.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(displayPosition - seat.position, Vector3.up));
            ConfigureDistractionCanvas(screen, playerCamera);
            var content = Rect(screen, "Notifications", 0, 0, 438, 469);
            var feed = content.gameObject.AddComponent<PhoneNotifications>();
            Image(content, "Phone card", 0, 0, 438, 469, Ink);
            Image(content, "Phone accent", 0, 0, 438, 6, Blue);
            Text(content, "Notifications heading", 25, 23, 385, 30, 22, Muted, "NOTIFICATIONS", FontStyles.Bold);
            feed.sender = Text(content, "Sender", 25, 70, 385, 43, 32, Gold, "Group chat", FontStyles.Bold);
            feed.body = Text(content, "Message", 25, 130, 385, 200, 28, Cream, "Nina");
            feed.positionLabel = Text(content, "Message number", 25, 338, 385, 28, 20, Muted, "1 / 3");
            Button(content, "Next notification", 25, 386, 388, 60, Sage, Ink, "NEXT NOTIFICATION", feed.Next);
            content.gameObject.SetActive(false);

            var activity = phone.gameObject.AddComponent<ScreenDistraction>();
            activity.kind = DistractionKind.Phone;
            activity.player = router;
            activity.displayPoint = screen;
            activity.content = content.gameObject;
            activity.inactiveLabel = "CHECK PHONE";
            activity.activeLabel = "CLOSE PHONE";
            activity.viewingDistance = 1.85f;
            activity.viewingAngle = 25f;
            activity.closeWhenOutOfRange = true;
            var button = Button(screen, "Check phone", 0, 486, 438, 74, Blue, Ink, activity.inactiveLabel, activity.Toggle);
            activity.buttonLabel = button.GetComponentInChildren<TMP_Text>();
            BindDistractionAction(activity, shape, manager);
        }

        static void ConfigureDistractionCanvas(RectTransform canvas, Camera playerCamera)
        {
            canvas.GetComponent<Canvas>().worldCamera = playerCamera;
            canvas.GetComponent<TrackedDeviceGraphicRaycaster>().checkFor3DOcclusion = true;
        }

        static void BindDistractionAction(ScreenDistraction activity, Collider shape, XRInteractionManager manager)
        {
            var action = activity.gameObject.AddComponent<HomeAction>();
            action.instruction = activity.inactiveLabel.ToLowerInvariant();
            UnityEventTools.AddPersistentListener(action.onUse, activity.Toggle);
            var interactable = activity.GetComponent<XRSimpleInteractable>();
            interactable.interactionManager = manager;
            interactable.colliders.Clear();
            interactable.colliders.Add(shape);
        }

        static void CreatePlushie(Transform parent, Vector3 position, XRInteractionManager manager)
        {
            var bear = Group("Plushie", parent, position);
            bear.rotation = Quaternion.Euler(0, 25, 0);
            var fur = Mat("Plushie cinnamon", "B57E5C");
            var patch = Mat("Plushie cream", "E4C498");
            var stitches = Mat("Plushie stitches", "403128");
            void Part(string name, Vector3 point, Vector3 size, Material material) =>
                Primitive(PrimitiveType.Sphere, bear, name, point, size, material, false);

            Part("Body", new Vector3(0, 0.086f, 0), new Vector3(0.152f, 0.161f, 0.122f), fur);
            Part("Belly patch", new Vector3(0, 0.089f, 0.049f), new Vector3(0.092f, 0.107f, 0.036f), patch);
            Part("Head", new Vector3(0, 0.200f, 0), new Vector3(0.179f, 0.149f, 0.139f), fur);
            Part("Muzzle", new Vector3(0, 0.175f, 0.057f), new Vector3(0.088f, 0.052f, 0.044f), patch);
            Part("Nose", new Vector3(0, 0.186f, 0.077f), new Vector3(0.029f, 0.015f, 0.015f), stitches);
            foreach (var side in new[] { -1, 1 })
            {
                Part("Ear", new Vector3(side * 0.069f, 0.261f, 0), new Vector3(0.060f, 0.067f, 0.058f), fur);
                Part("Inner ear", new Vector3(side * 0.071f, 0.266f, 0.023f), new Vector3(0.038f, 0.041f, 0.021f), patch);
                Part("Eye", new Vector3(side * 0.036f, 0.212f, 0.062f), new Vector3(0.015f, 0.021f, 0.011f), stitches);
                Part("Arm", new Vector3(side * 0.083f, 0.094f, 0.009f), new Vector3(0.058f, 0.10f, 0.064f), fur);
                Part("Foot", new Vector3(side * 0.049f, 0.03f, 0.043f), new Vector3(0.074f, 0.06f, 0.087f), fur);
                Part("Paw pad", new Vector3(side * 0.049f, 0.035f, 0.078f), new Vector3(0.049f, 0.040f, 0.024f), patch);
            }
            var shape = bear.gameObject.AddComponent<BoxCollider>();
            shape.center = new Vector3(0, 0.147f, 0.014f);
            shape.size = new Vector3(0.225f, 0.288f, 0.165f);
            ConfigureGrab(bear.gameObject, shape, 0.17f);
            bear.GetComponent<XRGrabInteractable>().interactionManager = manager;
            bear.gameObject.AddComponent<Plushie>().displayName = "plushie";
            bear.gameObject.AddComponent<DistractionUse>().kind = DistractionKind.Plushie;
        }

        static void ConfigureDistractionResults(HomeResultsPanel panel)
        {
            if (panel.distractionNames != null) return;
            font = panel.title.font;
            var root = panel.transform;
            panel.chores.rectTransform.anchoredPosition = new Vector2(42, -281);
            panel.chores.rectTransform.sizeDelta = new Vector2(415, 126);
            panel.chores.fontSize = 23;
            panel.score.rectTransform.anchoredPosition = new Vector2(42, -456);
            panel.score.rectTransform.sizeDelta = new Vector2(415, 90);
            panel.score.fontSize = 31;
            panel.feedback.rectTransform.anchoredPosition = new Vector2(42, -554);
            panel.feedback.rectTransform.sizeDelta = new Vector2(415, 104);
            panel.feedback.fontSize = 22;
            ((RectTransform)root.Find("Meeting divider")).sizeDelta = new Vector2(415, 1);

            Image(root, "Distraction table", 483, 261, 475, 382, new Color(1, 1, 1, 0.07f));
            Text(root, "Distractions heading", 509, 281, 418, 35, 25, Gold, "DISTRACTIONS", FontStyles.Bold);
            var timeHeading = Text(root, "Time column", 731, 329, 106, 26, 18, Muted, "TIME", FontStyles.Bold);
            var usesHeading = Text(root, "Uses column", 867, 329, 61, 26, 18, Muted, "USES", FontStyles.Bold);
            timeHeading.alignment = usesHeading.alignment = TextAlignmentOptions.TopRight;
            Image(root, "Distraction divider", 509, 362, 419, 1, new Color(1, 1, 1, 0.14f));
            panel.distractionNames = Text(root, "Distraction names", 509, 379, 210, 246, 25, Cream,
                "Fidget cube\nRainbow pop-it\nButter squishy\nTelevision\nPlushie\nPhone");
            panel.distractionTimes = Text(root, "Distraction times", 731, 379, 106, 246, 25, Cream,
                "0:00.0\n0:00.0\n0:00.0\n0:00.0\n0:00.0\n0:00.0");
            panel.distractionUses = Text(root, "Distraction uses", 867, 379, 61, 246, 25, Cream, "0\n0\n0\n0\n0\n0");
            foreach (var column in new[] { panel.distractionNames, panel.distractionTimes, panel.distractionUses }) column.lineSpacing = 18;
            panel.distractionTimes.alignment = panel.distractionUses.alignment = TextAlignmentOptions.TopRight;
        }
    }
}
