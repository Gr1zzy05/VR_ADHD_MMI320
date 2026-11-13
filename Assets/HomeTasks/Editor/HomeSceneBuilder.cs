using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HomeTasks;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace HomeTasks.Editor
{
    public static partial class HomeSceneBuilder
    {
        public const string Root = "Assets/HomeTasks";
        public const string ScenePath = HomeSession.GameplayScenePath;
        static readonly Color Ink = Hex("203D36");
        static readonly Color Cream = Hex("F4F1E7");
        static readonly Color Sage = Hex("82AB91");
        static readonly Color Muted = Hex("AEC6B6");
        static readonly Color Gold = Hex("E9C677");
        static readonly Color Clay = Hex("C98060");
        static readonly Color Blue = Hex("7BAABB");
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        static TMP_FontAsset font;
        static Transform environment;
        static Transform gameplay;
        static HomeSession session;

        [MenuItem("Home Focus/Create playable home scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before creating the home.");
            for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save your open scene changes before creating the home.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("HomeFocus already exists. Open it from Assets/HomeTasks/Scenes; the builder does not overwrite your edits.");
            foreach (var folder in new[] { "Scenes", "Prefabs", "Materials", "Meshes", "Config", "Audio" }) Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null) throw new InvalidOperationException("Import TMP Essential Resources before building the scene.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            environment = Group("Environment_Placeholder_ReplaceWithYourHome", null, Vector3.zero);
            gameplay = Group("Gameplay_KeepWhenReplacingEnvironment", null, Vector3.zero);
            var sessionObject = Group("Home Focus Session", null, Vector3.zero).gameObject;
            session = sessionObject.AddComponent<HomeSession>();
            session.settings = LoadOrCreate<HomeTaskSettings>(Root + "/Config/TaskSettings.asset");
            session.feedback = sessionObject.AddComponent<AudioSource>();
            session.feedback.playOnAwake = false;
            CreateLight();
            CreateHome();
            CreateWashStation();
            CreateSortingStation();
            CreateTrashStation();
            CreateMeetingStation();
            CreateBoard();
            CreatePlayer();
            EnsureResultsScene(UnityEngine.Object.FindFirstObjectByType<PlayerModeRouter>());
            QuestProjectSetup.Configure();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var others = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            others.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = others.ToArray();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = sessionObject;
            SceneView.lastActiveSceneView?.LookAt(new Vector3(0, 1, 0), Quaternion.Euler(35, 0, 0), 12);
            Debug.Log("[Home Focus] Playable scene created: " + ScenePath);
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void CreateLight()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.79f, 0.85f, 0.83f);
            RenderSettings.ambientEquatorColor = new Color(0.64f, 0.69f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.38f, 0.31f);
            RenderSettings.ambientIntensity = 1;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Hex("C7D8CA");
            RenderSettings.fogStartDistance = 28;
            RenderSettings.fogEndDistance = 55;
            var sun = Group("Morning sun", environment, Vector3.zero).gameObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Hex("FFF3DA");
            sun.intensity = 0.95f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(46, -32, 0);
            RenderSettings.sun = sun;
            var mobile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            if (mobile != null)
            {
                mobile.msaaSampleCount = 4;
                mobile.renderScale = 1;
                mobile.supportsHDR = false;
                mobile.shadowDistance = 18;
                mobile.shadowCascadeCount = 1;
                EditorUtility.SetDirty(mobile);
            }
        }

        static void CreateHome()
        {
            var wall = Mat("Warm plaster", "EAE8DC");
            var sage = Mat("Sage cabinetry", "7E9C88");
            var oak = Mat("Oak", "BC956C");
            var floor = Mat("Oak floor", "CEB28D");
            var stone = Mat("Ivory stone", "E4DFCF");
            var trim = Mat("Deep green", "294A3F");
            var ground = Block(environment, "Garden ground (teleportable)", new Vector3(0, -0.16f, -1), new Vector3(30, 0.22f, 28), Mat("Lawn", "879C70"));
            Teleportable(ground);
            var houseFloor = Block(environment, "House floor (teleportable)", new Vector3(0, -0.08f, 0.5f), new Vector3(12, 0.16f, 9), floor);
            Teleportable(houseFloor);
            for (var i = 0; i < 40; i++)
                Block(environment, "Floorboard joint", new Vector3(-5.85f + i * 0.30f, 0.003f, 0.5f), new Vector3(0.008f, 0.003f, 8.96f), oak, false);
            Block(environment, "Back wall", new Vector3(0, 1.6f, 5), new Vector3(12.3f, 3.2f, 0.2f), wall);
            var ceiling = Block(environment, "Removable blockout ceiling", new Vector3(0, 3.25f, 0.5f), new Vector3(12.2f, 0.1f, 9.2f), Mat("Ceiling", "E5E4D9"));
            ceiling.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            foreach (var x in new[] { -3.6f, 0f, 3.7f })
                Block(environment, "Ceiling light panel", new Vector3(x, 3.19f, 1.0f), new Vector3(0.7f, 0.025f, 0.7f), Mat("Light diffuser", "F2EEE0"), false);
            foreach (var x in new[] { -6f, 6f })
            {
                Block(environment, "Side wall", new Vector3(x, 1.6f, 0.5f), new Vector3(0.2f, 3.2f, 9), wall);
                Block(environment, "Window glass", new Vector3(x + (x < 0 ? 0.12f : -0.12f), 1.95f, -0.8f), new Vector3(0.035f, 1.45f, 2.6f), Mat("Window sky", "AACCC9"));
                for (var j = 0; j < 3; j++) Block(environment, "Window frame", new Vector3(x + (x < 0 ? 0.15f : -0.15f), 1.95f, -2.1f + j * 1.3f), new Vector3(0.06f, 1.55f, 0.06f), trim, false);
                foreach (var y in new[] { 1.2f, 2.7f }) Block(environment, "Window sill", new Vector3(x + (x < 0 ? 0.15f : -0.15f), y, -0.8f), new Vector3(0.12f, 0.07f, 2.7f), trim);
            }
            Block(environment, "Front wall left", new Vector3(-1.55f, 1.6f, -4), new Vector3(8.9f, 3.2f, 0.2f), wall);
            Block(environment, "Front wall right", new Vector3(5.65f, 1.6f, -4), new Vector3(0.7f, 3.2f, 0.2f), wall);
            Block(environment, "Open doorway header", new Vector3(4.1f, 2.95f, -4), new Vector3(2.4f, 0.5f, 0.25f), oak);
            foreach (var x in new[] { 2.94f, 5.26f }) Block(environment, "Door trim", new Vector3(x, 1.4f, -3.88f), new Vector3(0.10f, 2.8f, 0.12f), trim);
            var patio = Block(environment, "Patio (teleportable)", new Vector3(3.3f, -0.035f, -6.3f), new Vector3(5.5f, 0.07f, 4.6f), stone);
            Teleportable(patio);
            for (var i = 0; i < 5; i++) Block(environment, "Path tile", new Vector3(4.1f, 0.007f, -4.5f - i * 0.65f), new Vector3(1.1f, 0.012f, 0.5f), Mat("Patio tile", "C3C5B6"), false);
            for (var i = 0; i < 5; i++)
            {
                Block(environment, "Fence post", new Vector3(-8 + i * 4, 0.6f, -10.5f), new Vector3(0.10f, 1.2f, 0.10f), oak);
                Block(environment, "Fence rail", new Vector3(-6 + i * 4, 0.75f, -10.5f), new Vector3(3.9f, 0.14f, 0.08f), oak);
            }
            foreach (var position in new[] { new Vector3(-8, 0, -7), new Vector3(8, 0, -7), new Vector3(-8, 0, 6), new Vector3(8, 0, 6) }) Tree(position);
            // These furniture pieces belong to the disposable blockout, not to any task's logic.
            Block(environment, "Kitchen tile inset", new Vector3(-3.6f, 0.006f, 3), new Vector3(4.5f, 0.01f, 3.5f), stone, false);
            Block(environment, "Dirty dish counter base", new Vector3(-4.45f, 0.43f, 3.95f), new Vector3(2.5f, 0.86f, 1.25f), sage);
            Block(environment, "Dirty dish countertop", new Vector3(-4.45f, 0.91f, 3.95f), new Vector3(2.58f, 0.10f, 1.32f), stone);
            Block(environment, "Right kitchen cabinet", new Vector3(-1.25f, 0.43f, 3.95f), new Vector3(0.8f, 0.86f, 1.25f), sage);
            Block(environment, "Right kitchen countertop", new Vector3(-1.25f, 0.91f, 3.95f), new Vector3(0.86f, 0.10f, 1.32f), stone);
            foreach (var x in new[] { -5.25f, -4.45f, -3.65f, -1.25f }) Block(environment, "Cabinet handle", new Vector3(x, 0.75f, 3.3f), new Vector3(0.3f, 0.025f, 0.025f), trim, false);
            Block(environment, "Office rug", new Vector3(3.7f, 0.008f, 2.35f), new Vector3(3.4f, 0.015f, 2.4f), Mat("Woven rug", "C9B895"), false);
            Block(environment, "Meeting desk top", new Vector3(3.7f, 0.83f, 3.8f), new Vector3(3.0f, 0.09f, 1.05f), oak);
            foreach (var x in new[] { 2.45f, 4.95f }) foreach (var z in new[] { 3.4f, 4.2f }) Block(environment, "Desk leg", new Vector3(x, 0.4f, z), new Vector3(0.07f, 0.8f, 0.07f), trim);
            Block(environment, "Sofa seat", new Vector3(-2.5f, 0.43f, -2.5f), new Vector3(2.7f, 0.38f, 1.0f), Mat("Linen", "B8C3AC"));
            Block(environment, "Sofa back", new Vector3(-2.5f, 0.85f, -2.96f), new Vector3(2.7f, 0.7f, 0.22f), Mat("Linen", "B8C3AC"));
            foreach (var x in new[] { -3.95f, -1.05f }) Block(environment, "Sofa arm", new Vector3(x, 0.65f, -2.5f), new Vector3(0.23f, 0.6f, 1.1f), Mat("Linen", "B8C3AC"));
            Plant(environment, new Vector3(5.35f, 0, 0.9f), 1.0f);
            Plant(environment, new Vector3(-1.3f, 0.97f, 4.5f), 0.35f);
            foreach (Transform child in environment.GetComponentsInChildren<Transform>()) child.gameObject.isStatic = true;
        }

        static void CreateWashStation()
        {
            var root = Group("01_WashingStation_MoveToYourSink", gameplay, new Vector3(-2.5f, 0, 3.9f));
            var metal = Mat("Sink steel", "9AAFA9", 0.65f, 0.25f);
            Block(root, "Basin bottom", new Vector3(0, 0.76f, 0), new Vector3(1.22f, 0.06f, 1.15f), metal);
            foreach (var x in new[] { -0.64f, 0.64f }) Block(root, "Basin side", new Vector3(x, 0.86f, 0), new Vector3(0.07f, 0.22f, 1.2f), metal);
            foreach (var z in new[] { -0.58f, 0.58f }) Block(root, "Basin rim", new Vector3(0, 0.86f, z), new Vector3(1.35f, 0.22f, 0.06f), metal);
            Block(root, "Water in basin", new Vector3(0, 0.8f, 0), new Vector3(1.16f, 0.01f, 1.06f), Mat("Water", "9DC7C1", 0.7f), false);
            Cylinder(root, "Tap stem", new Vector3(0, 1.2f, 0.5f), new Vector3(0.055f, 0.25f, 0.055f), metal);
            var spout = Cylinder(root, "Tap spout", new Vector3(0, 1.44f, 0.26f), new Vector3(0.055f, 0.24f, 0.055f), metal);
            spout.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var station = root.gameObject.AddComponent<WashStation>();
            var volume = Group("RunningWaterVolume", root, new Vector3(0, 1.02f, 0)).gameObject.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = new Vector3(1.20f, 0.62f, 1.08f);
            station.washVolume = volume;
            station.waterStream = Cylinder(root, "Running water", new Vector3(0, 1.15f, 0.03f), new Vector3(0.025f, 0.28f, 0.025f), Mat("Water stream", "B5E1DC"), false);
            station.waterStream.SetActive(false);
            station.waterAudio = root.gameObject.AddComponent<AudioSource>();
            station.waterAudio.playOnAwake = false;
            station.waterAudio.spatialBlend = 1;
            station.waterAudio.minDistance = 1;
            station.waterAudio.maxDistance = 5;
            var tap = Primitive(PrimitiveType.Sphere, root, "Tap control - grip or E", new Vector3(0.51f, 1.08f, -0.35f), Vector3.one * 0.14f, Mat("Tap accent", "E9C677"));
            tap.AddComponent<XRSimpleInteractable>();
            var action = tap.AddComponent<HomeAction>();
            action.instruction = "turn the water on / off";
            UnityEventTools.AddPersistentListener(action.onUse, station.ToggleWater);
            var spongePrefab = CreateSpongePrefab();
            station.sponge = Spawn<SpongeTool>(spongePrefab, root, new Vector3(1.12f, 1.02f, -0.15f));
            for (var i = 0; i < 6; i++)
            {
                var kind = (DishKind)(i % 3);
                var prefab = CreateDishPrefab(kind);
                var item = Spawn<DishItem>(prefab, root, new Vector3(-2.65f + (i % 3) * 0.6f, 0.98f, -0.28f + (i / 3) * 0.58f));
                item.name = $"Dirty {kind} {i / 3 + 1}";
                session.dishes.Add(item);
            }
            StationSign(root, new Vector3(0, 2.1f, 0.75f), "01  /  WASH", "Water on. Sponge in hand.\nRub each dish inside the sink.", Gold, 0);
        }

        static void CreateSortingStation()
        {
            var root = Group("02_SortingShelves_MoveToYourCabinet", gameplay, Vector3.zero);
            for (var i = 0; i < 3; i++)
            {
                var kind = (DishKind)i;
                var rackRoot = Group(kind + " shelf", root, new Vector3(-5.36f, 0, i * 1.2f - 0.1f));
                var shelfMat = Mat("Oak", "BC956C");
                Block(rackRoot, "Shelf", new Vector3(0, 0.88f, 0), new Vector3(1.05f, 0.09f, 1.04f), shelfMat);
                Block(rackRoot, "Shelf back", new Vector3(-0.44f, 1.18f, 0), new Vector3(0.07f, 0.65f, 1.04f), Mat("Sage cabinetry", "7E9C88"));
                foreach (var z in new[] { -0.51f, 0.51f }) Block(rackRoot, "Shelf side", new Vector3(0, 1.1f, z), new Vector3(1.05f, 0.42f, 0.05f), shelfMat);
                var rack = rackRoot.gameObject.AddComponent<DishRack>();
                rack.accepts = kind;
                rack.volume = rackRoot.GetComponent<BoxCollider>();
                rack.volume.center = new Vector3(0, 1.12f, 0);
                rack.volume.size = new Vector3(1.08f, 0.44f, 1.0f);
                rack.volume.isTrigger = true;
                rack.slots = new[] { Group("Slot 1", rackRoot, new Vector3(0, 0.935f, -0.24f)), Group("Slot 2", rackRoot, new Vector3(0, 0.935f, 0.24f)) };
                StationSign(rackRoot, new Vector3(-0.46f, 1.8f, 0), "02  /  " + kind.ToString().ToUpper() + "S", "Clean dishes only.\nRelease inside the shelf.", Sage, -90, 0.0014f);
            }
        }

        static void CreateTrashStation()
        {
            var bags = Group("03_IndoorRubbish_MoveToYourHome", gameplay, new Vector3(4.8f, 0, -2.6f));
            var prefab = CreateTrashPrefab();
            for (var i = 0; i < 3; i++) session.rubbish.Add(Spawn<TrashItem>(prefab, bags, new Vector3((i - 1) * 0.48f, 0.03f, 0)));
            StationSign(bags, new Vector3(0, 1.25f, -0.1f), "03  /  TAKE IT OUT", "Three bags. One outdoor bin.\nThrough the open doorway.", Clay, 180, 0.0016f);
            var root = Group("03_OutdoorBin_MoveOutsideYourHome", gameplay, new Vector3(4.35f, 0, -7.45f));
            var binMat = Mat("Outdoor bin", "3B6050");
            Block(root, "Bin bottom", new Vector3(0, 0.10f, 0), new Vector3(0.95f, 0.15f, 0.85f), binMat);
            foreach (var x in new[] { -0.47f, 0.47f }) Block(root, "Bin side", new Vector3(x, 0.52f, 0), new Vector3(0.06f, 0.9f, 0.9f), binMat);
            foreach (var z in new[] { -0.42f, 0.42f }) Block(root, "Bin wall", new Vector3(0, 0.52f, z), new Vector3(0.92f, 0.9f, 0.06f), binMat);
            var lid = Block(root, "Open lid", new Vector3(0, 1.28f, -0.45f), new Vector3(1.02f, 0.08f, 0.94f), binMat);
            lid.transform.localRotation = Quaternion.Euler(-73, 0, 0);
            var bin = root.gameObject.AddComponent<TrashBin>();
            bin.volume = root.gameObject.AddComponent<BoxCollider>();
            bin.volume.center = new Vector3(0, 0.85f, 0);
            bin.volume.size = new Vector3(0.86f, 0.95f, 0.76f);
            bin.volume.isTrigger = true;
            bin.depositPoint = Group("Deposit point", root, new Vector3(0, 0.3f, 0));
            StationSign(root, new Vector3(0, 1.85f, -0.52f), "OUTDOOR BIN", "Release each bag inside.\nThank you for keeping it tidy.", Clay, 180, 0.0018f);
        }

        static void CreateMeetingStation()
        {
            var root = Group("04_MeetingStation_MoveToYourDesk", gameplay, new Vector3(3.7f, 0, 3.8f));
            var meeting = root.gameObject.AddComponent<MeetingController>();
            meeting.settings = session.settings;
            meeting.content = CreateMeetingContent();
            root.GetComponent<AudioSource>().playOnAwake = false;
            session.meeting = meeting;
            var monitor = Block(root, "Meeting monitor frame", new Vector3(0, 1.78f, 0.22f), new Vector3(1.91f, 1.72f, 0.09f), Mat("Deep green", "294A3F"));
            Block(root, "Monitor stand", new Vector3(0, 0.98f, 0.28f), new Vector3(0.14f, 0.24f, 0.14f), Mat("Deep green", "294A3F"));
            CreateMeetingUI(root, meeting);
            var join = Block(root, "Join meeting shortcut - E", new Vector3(0, 0.93f, -0.28f), new Vector3(0.5f, 0.06f, 0.25f), Mat("Meeting blue", "7BAABB"));
            join.AddComponent<XRSimpleInteractable>();
            var action = join.AddComponent<HomeAction>();
            action.instruction = "join the meeting (quest 4, one attempt)";
            UnityEventTools.AddPersistentListener(action.onUse, meeting.Begin);
        }

        static MeetingContent CreateMeetingContent()
        {
            var path = Root + "/Config/NeighbourhoodMeeting.asset";
            var existing = AssetDatabase.LoadAssetAtPath<MeetingContent>(path);
            if (existing != null) return existing;
            var content = ScriptableObject.CreateInstance<MeetingContent>();
            var words = new[]
            {
                "Hi, welcome to our neighbourhood check-in. I am Maya, your host. Thanks for joining from home. Keep listening as you work. At the end, I will ask you three short questions about our community garden event.",
                "First, the schedule. Our community garden event is on Saturday. Please arrive at ten thirty in the morning. That is Saturday at ten thirty A M. We will plant herbs together and then share a short tea break.",
                "Next, what to bring. Please bring the blue folder with your volunteer notes. You only need to remember the blue folder. Meet the group at the front gate of the community garden. The front gate is our meeting point.",
                "Before we finish, here is the plan once more. Saturday at ten thirty in the morning. Bring the blue folder, and meet us at the front gate. Thanks for listening. The briefing is over, and you can now answer the three questions on the screen."
            };
            content.segments = words.Select((text, i) => new MeetingSegment { transcript = text, voice = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/Meeting_{i + 1:00}.wav"), minimumDuration = 4 }).ToArray();
            if (content.segments.Any(s => s.voice == null)) throw new InvalidOperationException("Generate the four meeting WAV clips before creating the scene.");
            content.questions = new[]
            {
                new ListeningQuestion { prompt = "When should you arrive at the community garden?", answers = new[] { "Saturday at 10:30 AM", "Sunday at 9:00 AM", "Saturday at 2:00 PM" }, correctAnswer = 0, explanation = "Maya asked everyone to arrive on Saturday at 10:30 AM." },
                new ListeningQuestion { prompt = "What should you bring to the event?", answers = new[] { "A red notebook", "The blue folder", "A yellow umbrella" }, correctAnswer = 1, explanation = "Bring the blue folder with the volunteer notes." },
                new ListeningQuestion { prompt = "Where will the group meet?", answers = new[] { "Inside the tea shop", "Beside the playground", "At the front gate" }, correctAnswer = 2, explanation = "The group meets at the community garden's front gate." }
            };
            AssetDatabase.CreateAsset(content, path);
            return content;
        }

        static void CreatePlayer()
        {
            Group("XR Interaction Manager", null, Vector3.zero).gameObject.AddComponent<XRInteractionManager>();
            var events = Group("EventSystem", null, Vector3.zero).gameObject;
            events.AddComponent<EventSystem>();
            events.AddComponent<XRUIInputModule>();
            var rigPath = "Assets/Samples/XR Interaction Toolkit/3.7.0/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rigPath);
            if (prefab == null) throw new InvalidOperationException("Import XRI Starter Assets before creating the scene.");
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rig.name = "Quest Player (OpenXR)";
            rig.transform.position = new Vector3(0.2f, 0.04f, -1.5f);
            var origin = rig.GetComponent<XROrigin>();
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            origin.CameraYOffset = 1.65f;
            origin.Camera.nearClipPlane = 0.05f;
            origin.Camera.farClipPlane = 60;
            origin.Camera.backgroundColor = Hex("C7D8CA");
            origin.Camera.clearFlags = CameraClearFlags.SolidColor;
            origin.Camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            // Teleport and 45-degree snap-turn are available; disable continuous move for the default comfort setup.
            foreach (var behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour.GetType().Name == "DynamicMoveProvider" || behaviour.GetType().Name == "ContinuousMoveProvider") behaviour.enabled = false;
                if (behaviour.GetType().Name == "ControllerInputActionManager")
                {
                    var inputs = new SerializedObject(behaviour);
                    inputs.FindProperty("m_SmoothMotionEnabled").boolValue = false;
                    inputs.FindProperty("m_SmoothTurnEnabled").boolValue = false;
                    inputs.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
            }
            CreateWristUI(rig.transform);
            rig.SetActive(false);

            var desktop = Group("Desktop Player (no headset required)", null, new Vector3(0.2f, 0.04f, -1.5f)).gameObject;
            var character = desktop.AddComponent<CharacterController>();
            character.height = 1.75f;
            character.radius = 0.22f;
            character.center = new Vector3(0, 0.9f, 0);
            character.stepOffset = 0.2f;
            var cameraObject = Group("Desktop Camera", desktop.transform, new Vector3(0, 1.65f, 0)).gameObject;
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.04f;
            camera.farClipPlane = 65;
            camera.fieldOfView = 70;
            camera.backgroundColor = Hex("C7D8CA");
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.AddComponent<AudioListener>();
            var player = desktop.AddComponent<DesktopPlayer>();
            player.view = camera;
            player.meeting = session.meeting;
            CreateDesktopUI(desktop.transform, player);
            var router = Group("Automatic VR or Desktop", null, Vector3.zero).gameObject.AddComponent<PlayerModeRouter>();
            router.questRig = rig;
            router.desktopRig = desktop;
        }

        static GameObject CreateDishPrefab(DishKind kind)
        {
            var path = Root + "/Prefabs/Dirty" + kind + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = Group("Dirty " + kind, null, Vector3.zero);
            var glaze = Mat("Ceramic " + kind, kind == DishKind.Cup ? "A6C4BC" : kind == DishKind.Bowl ? "D8B583" : "F2EFE3", 0.55f);
            var collider = root.gameObject.AddComponent<BoxCollider>();
            if (kind == DishKind.Plate)
            {
                Cylinder(root, "Glazed plate", new Vector3(0, 0.022f, 0), new Vector3(0.36f, 0.012f, 0.36f), glaze, false);
                MeshObject(root, "Raised rim", TorusMesh("PlateRim", 0.159f, 0.012f), new Vector3(0, 0.038f, 0), glaze);
                collider.size = new Vector3(0.37f, 0.06f, 0.37f);
                collider.center = new Vector3(0, 0.026f, 0);
            }
            else if (kind == DishKind.Bowl)
            {
                MeshObject(root, "Ceramic bowl", LatheMesh("Bowl", new[] { new Vector2(0, 0.01f), new Vector2(0.07f, 0.01f), new Vector2(0.13f, 0.08f), new Vector2(0.145f, 0.13f), new Vector2(0.13f, 0.13f), new Vector2(0.11f, 0.075f), new Vector2(0.06f, 0.028f), new Vector2(0, 0.028f) }), Vector3.zero, glaze);
                collider.size = new Vector3(0.30f, 0.14f, 0.30f);
                collider.center = new Vector3(0, 0.07f, 0);
            }
            else
            {
                MeshObject(root, "Ceramic cup", LatheMesh("Cup", new[] { new Vector2(0, 0.008f), new Vector2(0.061f, 0.008f), new Vector2(0.078f, 0.18f), new Vector2(0.064f, 0.18f), new Vector2(0.049f, 0.028f), new Vector2(0, 0.028f) }), Vector3.zero, glaze);
                var handle = MeshObject(root, "Cup handle", TorusMesh("CupHandle", 0.049f, 0.012f), new Vector3(0.08f, 0.1f, 0), glaze);
                handle.transform.localRotation = Quaternion.Euler(90, 0, 0);
                collider.size = new Vector3(0.23f, 0.19f, 0.17f);
                collider.center = new Vector3(0.025f, 0.095f, 0);
            }
            var dirt = new List<Renderer>();
            for (var i = 0; i < 4; i++)
            {
                var angle = i * Mathf.PI * 0.5f;
                var position = kind == DishKind.Cup ? new Vector3(Mathf.Sin(angle) * 0.07f, 0.1f, Mathf.Cos(angle) * 0.07f) : new Vector3(Mathf.Sin(angle) * 0.06f, kind == DishKind.Plate ? 0.038f : 0.065f, Mathf.Cos(angle) * 0.06f);
                var size = kind == DishKind.Cup ? new Vector3(0.035f, 0.065f, 0.023f) : new Vector3(0.07f, 0.015f, 0.06f);
                dirt.Add(Primitive(PrimitiveType.Sphere, root, "Food stain", position, size, Mat("Food stains", "8C663D"), false).GetComponent<Renderer>());
            }
            ConfigureGrab(root.gameObject, collider, 0.3f);
            var dish = root.gameObject.AddComponent<DishItem>();
            dish.kind = kind;
            dish.displayName = kind.ToString().ToLower();
            dish.dirtMarks = dirt.ToArray();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return prefab;
        }

        static GameObject CreateSpongePrefab()
        {
            var path = Root + "/Prefabs/Sponge.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = Group("Sponge", null, Vector3.zero);
            Block(root, "Soft sponge", new Vector3(0, 0.02f, 0), new Vector3(0.19f, 0.065f, 0.12f), Mat("Sponge gold", "D6B85C"), false);
            Block(root, "Scouring pad", new Vector3(0, -0.019f, 0), new Vector3(0.19f, 0.016f, 0.12f), Mat("Scouring green", "426B50"), false);
            var shape = root.gameObject.AddComponent<BoxCollider>();
            shape.size = new Vector3(0.19f, 0.08f, 0.12f);
            ConfigureGrab(root.gameObject, shape, 0.12f);
            root.gameObject.AddComponent<SpongeTool>().displayName = "sponge";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return prefab;
        }

        static GameObject CreateTrashPrefab()
        {
            var path = Root + "/Prefabs/TrashBag.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = Group("Rubbish bag", null, Vector3.zero);
            var plastic = Mat("Bag charcoal", "35413C", 0.35f);
            Primitive(PrimitiveType.Sphere, root, "Filled bag", new Vector3(0, 0.24f, 0), new Vector3(0.40f, 0.47f, 0.38f), plastic, false);
            Primitive(PrimitiveType.Sphere, root, "Bag knot", new Vector3(0, 0.5f, 0), new Vector3(0.12f, 0.13f, 0.10f), plastic, false);
            var shape = root.gameObject.AddComponent<BoxCollider>();
            shape.size = new Vector3(0.40f, 0.54f, 0.38f);
            shape.center = new Vector3(0, 0.27f, 0);
            ConfigureGrab(root.gameObject, shape, 0.6f);
            root.gameObject.AddComponent<TrashItem>().displayName = "rubbish bag";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return prefab;
        }

        static void ConfigureGrab(GameObject root, Collider shape, float mass)
        {
            var body = root.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var grab = root.AddComponent<XRGrabInteractable>();
            grab.colliders.Clear();
            grab.colliders.Add(shape);
            grab.useDynamicAttach = true;
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = true;
        }

        static T Spawn<T>(GameObject prefab, Transform parent, Vector3 localPosition) where T : Component
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.identity;
            return instance.GetComponent<T>();
        }

        static void Teleportable(GameObject floor)
        {
            var area = floor.AddComponent<TeleportationArea>();
            area.interactionLayers = 1 << 31;
            area.filterSelectionByHitNormal = true;
            area.upNormalToleranceDegrees = 30;
        }

        static void Plant(Transform parent, Vector3 position, float scale)
        {
            var pot = Group("Plant", parent, position);
            pot.localScale = Vector3.one * scale;
            Cylinder(pot, "Terracotta pot", new Vector3(0, 0.18f, 0), new Vector3(0.38f, 0.18f, 0.38f), Mat("Terracotta", "C98060"));
            for (var i = 0; i < 7; i++)
            {
                var angle = i * 51f;
                var leaf = Primitive(PrimitiveType.Sphere, pot, "Leaf", new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * 0.16f, 0.55f + (i % 2) * 0.13f, Mathf.Cos(angle * Mathf.Deg2Rad) * 0.16f), new Vector3(0.16f, 0.6f, 0.12f), Mat("Plant leaves", "52775A"), false);
                leaf.transform.localRotation = Quaternion.Euler(20, angle, 20);
            }
        }

        static void Tree(Vector3 position)
        {
            var tree = Group("Garden tree", environment, position);
            Cylinder(tree, "Trunk", new Vector3(0, 1.1f, 0), new Vector3(0.32f, 1.1f, 0.32f), Mat("Bark", "8C775C"));
            Primitive(PrimitiveType.Sphere, tree, "Crown", new Vector3(0, 3.1f, 0), new Vector3(2.6f, 3.0f, 2.6f), Mat("Tree leaves", "65825B"), false);
        }

        static Transform Group(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        static GameObject Block(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool solid = true) => Primitive(PrimitiveType.Cube, parent, name, position, scale, material, solid);
        static GameObject Cylinder(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool solid = true) => Primitive(PrimitiveType.Cylinder, parent, name, position, scale, material, solid);

        static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static Material Mat(string name, string hex, float smoothness = 0.15f, float metallic = 0)
        {
            if (Materials.TryGetValue(name, out var cached) && cached != null) return cached;
            var path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
                material = new Material(shader) { name = name };
                material.SetColor("_BaseColor", Hex(hex));
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Metallic", metallic);
                material.enableInstancing = true;
                AssetDatabase.CreateAsset(material, path);
            }
            Materials[name] = material;
            return material;
        }

        static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var color); return color; }

        static GameObject MeshObject(Transform parent, string name, Mesh mesh, Vector3 position, Material material)
        {
            var go = Group(name, parent, position).gameObject;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static Mesh TorusMesh(string name, float radius, float tube)
        {
            var path = Root + "/Meshes/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            const int rings = 32, sides = 8;
            var vertices = new Vector3[(rings + 1) * (sides + 1)];
            var triangles = new List<int>();
            for (var r = 0; r <= rings; r++) for (var s = 0; s <= sides; s++)
            {
                var a = r * Mathf.PI * 2 / rings;
                var b = s * Mathf.PI * 2 / sides;
                vertices[r * (sides + 1) + s] = new Vector3((radius + tube * Mathf.Cos(b)) * Mathf.Cos(a), tube * Mathf.Sin(b), (radius + tube * Mathf.Cos(b)) * Mathf.Sin(a));
                if (r == rings || s == sides) continue;
                var k = r * (sides + 1) + s;
                triangles.AddRange(new[] { k, k + 1, k + sides + 1, k + 1, k + sides + 2, k + sides + 1 });
            }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Mesh LatheMesh(string name, Vector2[] profile)
        {
            var path = Root + "/Meshes/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            const int sides = 32;
            var vertices = new Vector3[(sides + 1) * profile.Length];
            var triangles = new List<int>();
            for (var s = 0; s <= sides; s++) for (var p = 0; p < profile.Length; p++)
            {
                var angle = s * Mathf.PI * 2 / sides;
                vertices[s * profile.Length + p] = new Vector3(profile[p].x * Mathf.Cos(angle), profile[p].y, profile[p].x * Mathf.Sin(angle));
                if (s == sides || p == profile.Length - 1) continue;
                var k = s * profile.Length + p;
                triangles.AddRange(new[] { k, k + profile.Length, k + 1, k + 1, k + profile.Length, k + profile.Length + 1 });
            }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
