using System;
using System.IO;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace HomeTasks.Editor
{
    public static partial class HomeSceneBuilder
    {
        [MenuItem("Home Focus/Repair VR controls in active scene")]
        public static void RepairVRInActiveScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before saving VR repairs.");
            var scene = SceneManager.GetActiveScene();
            var nodes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            T[] Find<T>() where T : Component => nodes.SelectMany(t => t.GetComponents<T>()).ToArray();
            session = Find<HomeSession>().Single();
            var router = Find<PlayerModeRouter>().Single();
            if (router.questRig == null || router.desktopRig == null) throw new InvalidOperationException("Assign the VR and desktop player roots first.");
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null) throw new InvalidOperationException("TextMeshPro resources are missing.");
            if (session.meeting == null) session.meeting = Find<MeetingController>().Single();

            var origin = router.questRig.GetComponent<XROrigin>();
            var interactionManager = Find<XRInteractionManager>().Single();
            var body = router.questRig.GetComponentInChildren<XRBodyTransformer>(true);
            var mediator = router.questRig.GetComponentInChildren<LocomotionMediator>(true);
            var teleport = router.questRig.GetComponentInChildren<TeleportationProvider>(true);
            body.xrOrigin = origin;
            foreach (var provider in router.questRig.GetComponentsInChildren<LocomotionProvider>(true))
            {
                provider.mediator = mediator;
                PrefabUtility.RecordPrefabInstancePropertyModifications(provider);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(body);
            var inputManagers = router.questRig.GetComponentsInChildren<ControllerInputActionManager>(true);
            foreach (var controller in inputManagers)
            {
                var bindings = new SerializedObject(controller);
                var near = controller.GetComponentInChildren<NearFarInteractor>(true);
                var ray = controller.GetComponentsInChildren<XRRayInteractor>(true).First(r => r.name.Contains("Teleport"));
                bindings.FindProperty("m_NearFarInteractor").objectReferenceValue = near;
                bindings.FindProperty("m_TeleportInteractor").objectReferenceValue = ray;
                bindings.FindProperty("m_SmoothMotionEnabled").boolValue = false;
                bindings.FindProperty("m_SmoothTurnEnabled").boolValue = false;
                bindings.ApplyModifiedPropertiesWithoutUndo();
                near.enableUIInteraction = true;
                near.interactionManager = interactionManager;
                ConfigureHandGrabs(near);
                ray.interactionManager = interactionManager;
                ray.interactionLayers = unchecked((int)0x80000000);
                ray.enableUIInteraction = false;
                ray.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
                PrefabUtility.RecordPrefabInstancePropertyModifications(near);
                PrefabUtility.RecordPrefabInstancePropertyModifications(ray);
                PrefabUtility.RecordPrefabInstancePropertyModifications(ray.gameObject);
            }

            var eventSystems = Find<EventSystem>();
            var events = eventSystems.FirstOrDefault(e => e.gameObject.activeSelf);
            if (events == null) events = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            foreach (var duplicate in eventSystems) if (duplicate != events) duplicate.enabled = false;
            var module = events.GetComponent<XRUIInputModule>() ?? events.gameObject.AddComponent<XRUIInputModule>();
            foreach (var other in events.GetComponents<BaseInputModule>()) other.enabled = other == module;
            module.enableXRInput = true;
            module.enableMouseInput = true;
            events.enabled = true;

            var areas = Find<TeleportationArea>().ToList();
            var home = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Home");
            var footpath = home != null ? home.GetComponentsInChildren<Collider>(true).FirstOrDefault(c => c.name == "Footpath") : null;
            if (footpath != null && footpath.GetComponent<TeleportationArea>() == null) areas.Add(footpath.gameObject.AddComponent<TeleportationArea>());
            areas = areas.Select(AlignFloorTeleportArea).Distinct().ToList();
            foreach (var area in areas)
            {
                area.enabled = true;
                area.interactionLayers = unchecked((int)0x80000000);
                area.interactionManager = interactionManager;
                area.teleportationProvider = teleport;
                area.filterSelectionByHitNormal = true;
                area.upNormalToleranceDegrees = 30;
                area.matchOrientation = MatchOrientation.WorldSpaceUp;
                if (area.colliders.Count == 0)
                    area.colliders.AddRange(area.GetComponents<Collider>().Where(c => c.enabled && !c.isTrigger));
                if (area.colliders.Count == 0) throw new InvalidOperationException("No floor collider found for " + area.name);
                EditorUtility.SetDirty(area);
            }

            var movement = router.questRig.GetComponent<VRMovementOptions>() ?? router.questRig.AddComponent<VRMovementOptions>();
            movement.leftController = inputManagers.Single(c => c.name.Contains("Left"));
            movement.rightController = inputManagers.Single(c => c.name.Contains("Right"));
            movement.moveProvider = router.questRig.GetComponentInChildren<ContinuousMoveProvider>(true);
            movement.walkingSpeed = 1.5f;
            movement.moveProvider.enabled = false;
            if (movement.moveProvider is DynamicMoveProvider dynamicMove)
            {
                dynamicMove.headTransform = origin.Camera.transform;
                dynamicMove.leftControllerTransform = movement.leftController.transform;
                dynamicMove.rightControllerTransform = movement.rightController.transform;
                dynamicMove.leftHandMovementDirection = DynamicMoveProvider.MovementDirection.HeadRelative;
                PrefabUtility.RecordPrefabInstancePropertyModifications(dynamicMove);
            }
            foreach (var hud in Find<DesktopHUD>())
            {
                hud.session = session;
                hud.player = router.desktopRig.GetComponent<DesktopPlayer>();
                hud.player.meeting = session.meeting;
                EditorUtility.SetDirty(hud);
                PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
            }
            foreach (var wrist in Find<VRTaskDisplay>())
            {
                wrist.session = session;
                wrist.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(wrist);
                PrefabUtility.RecordPrefabInstancePropertyModifications(wrist.gameObject);
            }
            foreach (var dashboard in Find<HomeDashboard>()) dashboard.session = session;
            var wallBoard = Find<HomeDashboard>().First(d => !d.transform.IsChildOf(router.questRig.transform));
            router.worldTaskBoard = wallBoard.gameObject;
            var arm = router.questRig.GetComponentInChildren<VRArmTaskBoard>(true);
            if (arm == null) arm = CreateInteractiveArmBoard(movement.leftController.transform, movement);
            arm.movement = movement;
            ConfigureArmHeight(arm, origin);
            router.armTaskBoard = arm.gameObject;

            foreach (var canvas in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)))
            {
                if (canvas.renderMode != RenderMode.WorldSpace) continue;
                canvas.worldCamera = router.desktopRig.GetComponentInChildren<Camera>(true);
                var graphics = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() ?? canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                var tracked = canvas.GetComponent<TrackedDeviceGraphicRaycaster>() ?? canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
                graphics.enabled = tracked.enabled = true;
                foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true)) text.raycastTarget = false;
                foreach (var button in canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true)) if (button.targetGraphic != null) button.targetGraphic.raycastTarget = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(canvas);
            }
            router.questRig.SetActive(false);
            router.desktopRig.SetActive(true);
            router.worldTaskBoard.SetActive(true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(router.questRig);
            PrefabUtility.RecordPrefabInstancePropertyModifications(router.desktopRig);
            EditorUtility.SetDirty(session);
            EditorUtility.SetDirty(router);
            EditorUtility.SetDirty(movement);
            PrefabUtility.RecordPrefabInstancePropertyModifications(router);
            PrefabUtility.RecordPrefabInstancePropertyModifications(movement);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Home Focus] Repaired XR UI and {areas.Count} teleport surfaces, added controller walking and the left-arm task board in {scene.path}.");
        }

        public static void ConfigureHandGrabs(NearFarInteractor interactor)
        {
            interactor.farAttachMode = InteractorFarAttachMode.Near;
            if (interactor.TryGetComponent(out InteractionAttachController attachment))
            {
                attachment.useManipulationInput = false;
                attachment.manipulationInput.inputSourceMode = XRInputValueReader.InputSourceMode.Unused;
                EditorUtility.SetDirty(attachment);
                PrefabUtility.RecordPrefabInstancePropertyModifications(attachment);
            }
            EditorUtility.SetDirty(interactor);
            PrefabUtility.RecordPrefabInstancePropertyModifications(interactor);
        }

        public static TeleportationArea AlignFloorTeleportArea(TeleportationArea area)
        {
            const string targetName = "Floor teleport target";
            var target = area.transform.Find(targetName);
            if (target == null && Vector3.Dot(area.transform.up, Vector3.up) > 0.9999f) return area;

            // XRI compares hits with the interactable's local up, including in WorldSpaceUp mode.
            if (target == null)
            {
                target = new GameObject(targetName).transform;
                target.SetParent(area.transform, false);
            }
            target.rotation = Quaternion.identity;
            var aligned = target.GetComponent<TeleportationArea>();
            if (aligned == null)
            {
                aligned = target.gameObject.AddComponent<TeleportationArea>();
                EditorUtility.CopySerialized(area, aligned);
                if (aligned.colliders.Count == 0)
                    aligned.colliders.AddRange(area.GetComponents<Collider>().Where(c => c.enabled && !c.isTrigger));
            }
            area.enabled = false;
            aligned.enabled = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(area);
            EditorUtility.SetDirty(aligned);
            return aligned;
        }

        static VRArmTaskBoard CreateInteractiveArmBoard(Transform leftController, VRMovementOptions movement)
        {
            var canvas = WorldCanvas(leftController, "Arm task board", new Vector3(0, 0.12f, -0.20f), 360, 445, 0.001f);
            canvas.localRotation = Quaternion.Euler(70, 0, 0);
            var arm = canvas.gameObject.AddComponent<VRArmTaskBoard>();
            arm.movement = movement;
            var content = Rect(canvas, "Arm menu content", 0, 0, 360, 445);
            arm.content = content.gameObject;
            Image(content, "Panel", 0, 0, 360, 445, Ink);
            Image(content, "Accent", 0, 0, 360, 4, Sage);
            Text(content, "Title", 16, 15, 328, 27, 22, Cream, "HOME FOCUS", FontStyles.Bold);
            var dashboard = canvas.gameObject.AddComponent<HomeDashboard>();
            dashboard.session = session;
            dashboard.timer = Text(content, "Timer", 207, 20, 137, 21, 11, Muted, "YOUR PACE  00:00");
            dashboard.status = Text(content, "Current quest", 16, 49, 328, 25, 15, Gold, "QUEST 1 OF 4  /  WASH DISHES", FontStyles.Bold);
            dashboard.counts = new TMP_Text[4];
            dashboard.bars = new UnityEngine.UI.Image[4];
            var names = new[] { "01  Wash dishes", "02  Organize dishes", "03  Take trash outside", "04  Meeting + quiz" };
            var accents = new[] { Gold, Sage, Clay, Blue };
            for (var i = 0; i < 4; i++)
            {
                var y = 84 + i * 44;
                Text(content, names[i], 16, y, 246, 24, 16, Cream, names[i]);
                dashboard.counts[i] = Text(content, "Task progress " + i, 267, y + 1, 77, 23, 14, accents[i], i == 0 ? "0 / 6" : "LOCKED");
                dashboard.counts[i].alignment = TextAlignmentOptions.TopRight;
                var track = Image(content, "Progress track " + i, 16, y + 29, 328, 3, new Color(1, 1, 1, 0.13f));
                var fill = Image(track.transform, "Fill", 0, 0, 328, 3, accents[i]);
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = new Vector2(0, 1);
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                dashboard.bars[i] = fill;
            }
            dashboard.footer = Text(content, "Objective", 16, 267, 328, 66, 15, Muted, "Wash all dishes in the running sink to unlock organizing.");
            var toggle = Button(content, "Toggle controller walking", 16, 337, 328, 36, Sage, Ink, "MOVEMENT: TELEPORT", movement.ToggleWalking);
            arm.movementLabel = toggle.GetComponentInChildren<TMP_Text>();
            arm.movementLabel.fontSize = 15;
            arm.movementLabel.rectTransform.anchoredPosition = new Vector2(8, -5);
            arm.movementLabel.rectTransform.sizeDelta = new Vector2(312, 26);
            var restart = Button(content, "Restart session", 16, 383, 156, 31, new Color(1, 1, 1, 0.12f), Cream, "RESTART", session.RestartSession);
            var recover = Button(content, "Recover loose items", 188, 383, 156, 31, new Color(1, 1, 1, 0.12f), Cream, "RECOVER ITEMS", session.RecoverLooseItems);
            foreach (var button in new[] { restart, recover })
            {
                var caption = button.GetComponentInChildren<TMP_Text>();
                caption.fontSize = 13;
                caption.rectTransform.anchoredPosition = new Vector2(8, -4);
                caption.rectTransform.sizeDelta = new Vector2(140, 23);
            }
            Text(content, "Controls", 16, 424, 328, 17, 11, Muted, "Y: show/hide  |  Right trigger: press  |  Right stick: teleport");
            return arm;
        }

        public static void ConfigureArmHeight(VRArmTaskBoard arm, XROrigin origin)
        {
            var height = origin.GetComponent<VRHeightAdjustment>() ?? origin.gameObject.AddComponent<VRHeightAdjustment>();
            arm.heightAdjustment = height;
            if (arm.heightLabel != null) return;

            font = arm.movementLabel.font;
            var content = (RectTransform)arm.content.transform;
            foreach (var rect in new[] { (RectTransform)arm.transform, content, (RectTransform)content.Find("Panel") })
                rect.sizeDelta += new Vector2(0, 44);
            foreach (var name in new[] { "Restart session", "Recover loose items", "Controls" })
                ((RectTransform)content.Find(name)).anchoredPosition += new Vector2(0, -44);

            var lower = Button(content, "Lower VR height", 16, 381, 52, 38, Sage, Ink, "-", height.Lower);
            var reset = Button(content, "Reset VR height", 76, 381, 208, 38, new Color(1, 1, 1, 0.15f), Cream, "HEIGHT 0 cm / RESET", height.ResetHeight);
            var raise = Button(content, "Raise VR height", 292, 381, 52, 38, Sage, Ink, "+", height.Raise);
            foreach (var button in new[] { lower, reset, raise })
            {
                var caption = button.GetComponentInChildren<TMP_Text>();
                var rect = caption.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(4, 0);
                rect.offsetMax = new Vector2(-4, 0);
                caption.fontSize = button == reset ? 15 : 24;
            }
            arm.heightLabel = reset.GetComponentInChildren<TMP_Text>();
            EditorUtility.SetDirty(arm);
            PrefabUtility.RecordPrefabInstancePropertyModifications(arm);
        }
    }
}
