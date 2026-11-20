using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Object = UnityEngine.Object;

namespace HomeTasks.Editor
{
    public static class FidgetCubeSetup
    {
        const string ModelPath = "Assets/HomeTasks/Model/FidgetCube.fbx";
        const string PrefabPath = "Assets/HomeTasks/Prefabs/FidgetCubeInteractive.prefab";
        const string TestScenePath = "Assets/HomeTasks/Scenes/FidgetCubeTest.unity";
        const string QuestRigPath = "Assets/HomeTasks/Prefabs/Quest Player (OpenXR) Variant.prefab";
        const string Menu = "Home Focus/Fidget Cube/";
        const float BodyWidth = 0.07f;
        static readonly string[] PartNames =
        {
            "Button01", "Button02", "Button03", "Button04", "Button05",
            "Switch", "Spinner", "Gears01", "Gears02", "Gears03", "Joystick"
        };

        [MenuItem(Menu + "Create Missing Assets")]
        public static void CreateMissingAssets()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = BuildPrefab();
            ValidatePrefab(prefab);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath) == null) BuildTestScene(prefab);
        }

        [MenuItem(Menu + "Create Missing Assets", true)]
        static bool CanCreateMissingAssets() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(Menu + "Configure Selected Model")]
        static void ConfigureSelectedModel()
        {
            var source = PrefabUtility.GetNearestPrefabInstanceRoot(Selection.activeGameObject);
            var scene = source.scene;
            var oldBody = FindBody(source.GetComponentsInChildren<MeshFilter>(true));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = BuildPrefab();
            ValidatePrefab(prefab);

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            const string action = "Configure Fidget Cube";
            Undo.SetCurrentGroupName(action);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetPositionAndRotation(oldBody.transform.TransformPoint(oldBody.sharedMesh.bounds.center), oldBody.transform.rotation);
            Undo.RegisterCreatedObjectUndo(instance, action);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            Undo.RecordObject(source, action);
            source.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(source);
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = instance;
        }

        [MenuItem(Menu + "Configure Selected Model", true)]
        static bool CanConfigureSelectedModel()
        {
            var selection = Selection.activeGameObject;
            if (EditorApplication.isPlayingOrWillChangePlaymode || selection == null ||
                !selection.scene.IsValid() || EditorSceneManager.IsPreviewSceneObject(selection) ||
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selection) != ModelPath) return false;
            return PrefabUtility.GetNearestPrefabInstanceRoot(selection).activeInHierarchy;
        }

        [MenuItem(Menu + "Validate Prefab and Active Scene")]
        public static void ValidateAssets()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException($"Missing {PrefabPath}.");
            ValidatePrefab(prefab);
            var scene = SceneManager.GetActiveScene();
            var sceneRoots = scene.GetRootGameObjects();
            var interactors = sceneRoots.SelectMany(root => root.GetComponentsInChildren<NearFarInteractor>(true));
            var compatible = interactors.Count(interactor => interactor.enabled && interactor.enableNearCasting &&
                (interactor.interactionLayers.value & prefab.GetComponent<XRGrabInteractable>().interactionLayers.value) != 0);
            var sceneCubes = sceneRoots.SelectMany(root => root.GetComponentsInChildren<FidgetCubeController>(true)).ToArray();
            foreach (var cube in sceneCubes) ValidatePrefab(cube.gameObject);
            Debug.Log($"Fidget Cube prefab and {sceneCubes.Length} instance(s) validated. {scene.name} has {compatible} compatible Near-Far interactor(s).", prefab);
        }

        // Built in a preview scene so authoring never edits the imported model or open scenes.
        static GameObject BuildPrefab()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null || PrefabUtility.GetPrefabAssetType(source) != PrefabAssetType.Model)
                throw new InvalidOperationException($"Expected an imported model at {ModelPath}.");

            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                var meshes = model.GetComponentsInChildren<MeshFilter>(true);
                var body = FindBody(meshes);
                var parts = PartNames.Select(name => meshes.SingleOrDefault(mesh => mesh.name.Trim().TrimStart('_') == name)
                    ?? throw new InvalidOperationException($"Missing Fidget Cube mesh: {name}.")).ToArray();

                var root = NewObject("FidgetCubeInteractive", preview).transform;
                root.SetPositionAndRotation(body.transform.position, body.transform.rotation);
                model.transform.SetParent(root, true);
                model.name = "Cube_Body";

                var bodyBounds = BoundsIn(body, root);
                var originalWidth = LargestDimension(bodyBounds.size);
                if (originalWidth <= 0f) throw new InvalidOperationException("Cube_Body has empty mesh bounds.");

                var scale = BodyWidth / originalWidth;
                model.transform.localScale *= scale;
                model.transform.localPosition *= scale;
                model.transform.localPosition -= BoundsIn(body, root).center;
                bodyBounds = BoundsIn(body, root);

                var shape = root.gameObject.AddComponent<BoxCollider>();
                shape.center = bodyBounds.center;
                shape.size = bodyBounds.size;
                var rigidbody = root.gameObject.AddComponent<Rigidbody>();
                rigidbody.mass = 0.09f;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                ConfigureParts(root, bodyBounds, parts);
                ConfigureGrab(root.gameObject, shape);
                root.position = Vector3.zero;
                var saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                if (saved == null) throw new InvalidOperationException($"Could not save {PrefabPath}.");
                Debug.Log($"Fidget Cube body width: {originalWidth:F5} m imported, {LargestDimension(bodyBounds.size):F5} m in prefab.", saved);
                return saved;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        static MeshFilter FindBody(MeshFilter[] meshes) => meshes.SingleOrDefault(mesh =>
            mesh.name.Trim() == "Cube_Body" || (mesh.sharedMesh != null && mesh.sharedMesh.name.Trim() == "Cube_Body"))
            ?? throw new InvalidOperationException("Missing Cube_Body mesh.");

        static GameObject NewObject(string name, Scene scene)
        {
            var instance = new GameObject(name);
            SceneManager.MoveGameObjectToScene(instance, scene);
            return instance;
        }

        static Bounds BoundsIn(MeshFilter filter, Transform frame)
        {
            var bounds = filter.sharedMesh.bounds;
            var localBounds = new Bounds(frame.InverseTransformPoint(filter.transform.TransformPoint(bounds.center)), Vector3.zero);
            for (var corner = 0; corner < 8; corner++)
            {
                var point = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                localBounds.Encapsulate(frame.InverseTransformPoint(filter.transform.TransformPoint(point)));
            }
            return localBounds;
        }

        static float LargestDimension(Vector3 size) => Mathf.Max(size.x, size.y, size.z);

        static int DominantAxis(Vector3 vector)
        {
            var axis = Mathf.Abs(vector.x) > Mathf.Abs(vector.y) ? 0 : 1;
            return Mathf.Abs(vector.z) > Mathf.Abs(vector[axis]) ? 2 : axis;
        }

        static Vector3 Axis(int index)
        {
            var direction = Vector3.zero;
            direction[index] = 1f;
            return direction;
        }

        static void ConfigureParts(Transform root, Bounds body, MeshFilter[] parts)
        {
            var audio = root.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.minDistance = 0.08f;
            audio.maxDistance = 2f;
            foreach (var buttonMesh in parts.Take(5))
            {
                var button = AddControl<FidgetButton>(root, body, buttonMesh);
                Edit(button, serialized => serialized.FindProperty("audioSource").objectReferenceValue = audio);
            }

            AddControl<FidgetSwitch>(root, body, parts[5]);
            AddControl<FidgetSpinner>(root, body, parts[6]);
            var gears = parts.Skip(7).Take(3).Select(mesh => AddControl<FidgetGear>(root, body, mesh)).ToArray();
            Edit(gears[0], serialized =>
            {
                var followers = serialized.FindProperty("followers");
                followers.arraySize = gears.Length - 1;
                for (var i = 1; i < gears.Length; i++)
                    followers.GetArrayElementAtIndex(i - 1).objectReferenceValue = gears[i];
            });
            for (var i = 1; i < gears.Length; i++)
            {
                var reverse = i % 2 != 0;
                Edit(gears[i], serialized =>
                {
                    serialized.FindProperty("trainRoot").objectReferenceValue = gears[0];
                    serialized.FindProperty("reverseDirection").boolValue = reverse;
                });
            }
            AddControl<FidgetJoystick>(root, body, parts[10]);
        }

        static T AddControl<T>(Transform root, Bounds body, MeshFilter mesh) where T : FidgetControl
        {
            var visualBounds = BoundsIn(mesh, root);
            var offset = visualBounds.center - body.center;
            var faceAxis = DominantAxis(offset);
            var faceSign = Mathf.Sign(offset[faceAxis]);
            var outward = Axis(faceAxis) * faceSign;
            var surface = body.extents[faceAxis] + body.center[faceAxis] * faceSign;
            var pivot = root.InverseTransformPoint(mesh.transform.position);

            if (typeof(T) == typeof(FidgetSwitch))
            {
                pivot = visualBounds.center;
                pivot[faceAxis] = (surface - 0.002f) * faceSign;
            }

            var handle = NewObject(mesh.name.Trim().TrimStart('_'), root.gameObject.scene).transform;
            handle.SetParent(root, false);
            handle.localPosition = pivot;
            var up = faceAxis == 1 ? Vector3.forward : Vector3.up;
            handle.localRotation = Quaternion.LookRotation(outward, up);

            var bounds = BoundsIn(mesh, handle);
            var near = surface - Vector3.Dot(pivot, outward) - 0.001f;
            var far = Mathf.Max(near + 0.002f, bounds.max.z + (typeof(T) == typeof(FidgetSwitch) ? 0.0055f : 0.001f));
            var sensor = handle.gameObject.AddComponent<BoxCollider>();
            sensor.isTrigger = true;
            sensor.center = new Vector3(bounds.center.x, bounds.center.y, (near + far) * 0.5f);
            sensor.size = new Vector3(bounds.size.x, bounds.size.y, far - near);

            var visualPivot = NewObject("Visual Pivot", root.gameObject.scene).transform;
            visualPivot.SetParent(handle, false);
            mesh.transform.SetParent(visualPivot, true);
            mesh.name = mesh.name.Trim().TrimStart('_') + " Mesh";

            var control = handle.gameObject.AddComponent<T>();
            Edit(control, serialized =>
            {
                serialized.FindProperty("visualPivot").objectReferenceValue = visualPivot;
                if (control is FidgetSwitch || control is FidgetGear)
                {
                    var tangentAxes = Enumerable.Range(0, 3).Where(index => index != faceAxis);
                    var thinAxis = tangentAxes.OrderBy(index => visualBounds.size[index]).First();
                    var localAxis = Quaternion.Inverse(handle.localRotation) * Axis(thinAxis);
                    if (control is FidgetSwitch)
                        serialized.FindProperty("hingeAxis").vector3Value = localAxis;
                    else
                    {
                        serialized.FindProperty("axle").vector3Value = localAxis;
                        var tread = tangentAxes.Single(index => index != thinAxis);
                        serialized.FindProperty("rollRadius").floatValue = visualBounds.extents[tread];
                    }
                }
            });
            return control;
        }

        static void Edit(Object component, Action<SerializedObject> edit)
        {
            var serialized = new SerializedObject(component);
            edit(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureGrab(GameObject root, BoxCollider shape)
        {
            var grab = root.AddComponent<XRGrabInteractable>();
            grab.colliders.Clear();
            grab.colliders.Add(shape);
            grab.interactionLayers = 1; // Default on both Near-Far controllers, separate from Teleport.
            grab.selectMode = InteractableSelectMode.Single;
            grab.useDynamicAttach = true;
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.farAttachMode = InteractableFarAttachMode.Near;
            grab.attachEaseInTime = 0.08f;
            grab.throwVelocityScale = 0.8f;
            grab.throwAngularVelocityScale = 0.8f;
            root.AddComponent<FidgetCubeController>();
        }

        static void ValidatePrefab(GameObject prefab)
        {
            var body = prefab.GetComponent<BoxCollider>();
            var grab = prefab.GetComponent<XRGrabInteractable>();
            var controls = prefab.GetComponentsInChildren<FidgetControl>(true);
            var worldWidth = body == null ? 0f : LargestDimension(Vector3.Scale(body.size, prefab.transform.lossyScale));
            if (worldWidth < 0.06f || worldWidth > 0.08f || body.isTrigger)
                throw new InvalidOperationException("Fidget Cube needs a solid body collider 6–8 cm wide.");
            if (grab == null || grab.colliders.Count != 1 || grab.colliders[0] != body ||
                grab.interactionLayers.value != 1 || prefab.GetComponentsInChildren<Rigidbody>(true).Length != 1)
                throw new InvalidOperationException("Fidget Cube needs one rigidbody and one XR grab collider on the Default interaction layer.");
            if (!controls.Select(control => control.name).OrderBy(name => name).SequenceEqual(PartNames.OrderBy(name => name)) ||
                controls.Any(control => control.VisualPivot == null ||
                    !control.TryGetComponent<BoxCollider>(out var sensor) || !sensor.isTrigger))
                throw new InvalidOperationException("Fidget Cube needs all eleven control pivots and trigger colliders.");
        }

        static void BuildTestScene(GameObject prefab)
        {
            var questAsset = AssetDatabase.LoadAssetAtPath<GameObject>(QuestRigPath);
            if (questAsset == null) throw new InvalidOperationException($"Missing {QuestRigPath}.");

            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var cube = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                cube.transform.position = new Vector3(0f, 1.065f, 0f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(cube.transform);
                CreateBlock("Test platform", new Vector3(0f, 0.48f, 0f), new Vector3(0.24f, 0.96f, 0.24f));
                CreateBlock("Floor", new Vector3(0f, -0.05f, 0f), new Vector3(4f, 0.1f, 4f));

                var light = NewObject("Directional Light", scene).AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.4f;
                light.transform.rotation = Quaternion.Euler(43f, 137f, 0f);

                var desktop = NewObject("Desktop Test Camera", scene);
                var view = desktop.AddComponent<Camera>();
                desktop.AddComponent<AudioListener>();
                view.nearClipPlane = 0.015f;
                view.farClipPlane = 12f;
                view.fieldOfView = 36f;
                view.clearFlags = CameraClearFlags.SolidColor;
                view.backgroundColor = new Color(0.12f, 0.15f, 0.17f);
                view.stereoTargetEye = StereoTargetEyeMask.None;
                desktop.transform.position = new Vector3(-0.15f, 1.155f, -0.23f);
                desktop.transform.LookAt(new Vector3(0f, 1.035f, 0f));
                var tester = desktop.AddComponent<FidgetCubeDesktopTester>();
                Edit(tester, serialized => serialized.FindProperty("cube").objectReferenceValue = cube.GetComponent<FidgetCubeController>());

                NewObject("XR Interaction Manager", scene).AddComponent<XRInteractionManager>();
                var quest = (GameObject)PrefabUtility.InstantiatePrefab(questAsset, scene);
                quest.transform.SetPositionAndRotation(new Vector3(0f, 0f, -0.48f), Quaternion.identity);
                quest.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(quest);
                PrefabUtility.RecordPrefabInstancePropertyModifications(quest.transform);
                var router = NewObject("Player Mode", scene).AddComponent<PlayerModeRouter>();
                router.desktopRig = desktop;
                router.questRig = quest;

                if (!EditorSceneManager.SaveScene(scene, TestScenePath))
                    throw new InvalidOperationException($"Could not save {TestScenePath}.");

                void CreateBlock(string name, Vector3 position, Vector3 scale)
                {
                    var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    SceneManager.MoveGameObjectToScene(block, scene);
                    block.name = name;
                    block.transform.position = position;
                    block.transform.localScale = scale;
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
