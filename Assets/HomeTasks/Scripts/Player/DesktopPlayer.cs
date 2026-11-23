using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace HomeTasks
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class DesktopPlayer : MonoBehaviour
    {
        public Camera view;
        public MeetingController meeting;
        public float moveSpeed = 2.8f;
        public float reach = 2.7f;
        public string Prompt { get; private set; }
        public HomeItem HeldItem => held;
        public bool MouseCaptured => Cursor.lockState == CursorLockMode.Locked;
        CharacterController controller;
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        readonly FidgetMouseContact fidgetContact = new FidgetMouseContact();
        HomeItem held;
        FidgetCubeController heldCube;
        Quaternion cubeViewRotation;
        RaycastHit aimHit;
        bool hasHit;
        float pitch;
        float fallingSpeed;
        float holdDistance = 0.8f;
        float itemYaw;
        float standingEyeHeight;

        void Awake() => controller = GetComponent<CharacterController>();

        void Update()
        {
            if (HomeSession.Instance != null && HomeSession.Instance.Finished)
            {
                Drop();
                Prompt = GameText.Get("All quests complete. Preparing your results.");
                SetCapture(false);
                return;
            }
            if (held != null && !held.CanInteract) Drop();
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
            if (heldCube != null)
            {
                PlayWithCube(keyboard, mouse);
                return;
            }
            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame)
            {
                SetCapture(!MouseCaptured && !keyboard.escapeKey.wasPressedThisFrame);
                if (held is SpongeTool tool) tool.DesktopScrubbing = false;
            }
            var move = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0), (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            if (!MouseCaptured)
            {
                var seated = meeting != null && meeting.seat != null && meeting.seat.IsPlayerInSeat;
                Prompt = seated ? GameText.Get("Click meeting buttons  |  WASD: leave the chair") :
                    GameText.Get("Click the game to look around  |  Tab: mouse for screen buttons");
                if (seated && move != Vector2.zero) MoveCharacter(move);
                if (mouse.leftButton.wasPressedThisFrame && !PointerIsOverUI(mouse.position.ReadValue())) SetCapture(true);
                return;
            }

            var delta = mouse.delta.ReadValue() * 0.1f;
            transform.Rotate(Vector3.up, delta.x);
            pitch = Mathf.Clamp(pitch - delta.y, -78, 78);
            view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            MoveCharacter(move);

            FindTarget();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (held != null) Drop();
                else if (hasHit)
                {
                    if (aimHit.collider.TryGetComponent<FidgetCubeController>(out var cube) && TryPickUpCube(cube))
                        return;
                    var item = aimHit.collider.GetComponentInParent<HomeItem>();
                    if (item != null && !item.IsLocked && !item.IsHeld)
                    {
                        if (item.CanInteract) PickUp(item);
                        else HomeSession.Instance?.Notify(HomeSession.Instance.CurrentObjective);
                    }
                }
            }
            if (keyboard.qKey.wasPressedThisFrame) Drop();
            if (keyboard.rKey.wasPressedThisFrame) itemYaw += 90;
            if (held != null) holdDistance = Mathf.Clamp(holdDistance + mouse.scroll.ReadValue().y * 0.0015f, 0.4f, 1.4f);
            if (keyboard.eKey.wasPressedThisFrame && hasHit)
            {
                var action = aimHit.collider.GetComponentInParent<HomeAction>();
                if (action != null) action.Use();
            }
            UpdateHeldPose(keyboard.eKey.isPressed);

            if (meeting != null && Vector3.Distance(view.transform.position, meeting.transform.position) < 3.5f)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) meeting.AnswerFirst();
                if (keyboard.digit2Key.wasPressedThisFrame) meeting.AnswerSecond();
                if (keyboard.digit3Key.wasPressedThisFrame) meeting.AnswerThird();
            }
            UpdatePrompt();
        }

        bool PointerIsOverUI(Vector2 position)
        {
            var events = EventSystem.current;
            if (events == null) return false;
            events.RaycastAll(new PointerEventData(events) { position = position }, uiHits);
            return uiHits.Exists(hit => hit.module is UnityEngine.UI.GraphicRaycaster);
        }

        void MoveCharacter(Vector2 move)
        {
            move = Vector2.ClampMagnitude(move, 1);
            if (controller.isGrounded && fallingSpeed < 0) fallingSpeed = -1;
            fallingSpeed += Physics.gravity.y * Time.deltaTime;
            controller.Move(((transform.right * move.x + transform.forward * move.y) * moveSpeed + Vector3.up * fallingSpeed) * Time.deltaTime);
        }

        void FindTarget()
        {
            hasHit = false;
            var hits = Physics.RaycastAll(view.ViewportPointToRay(new Vector3(0.5f, 0.5f)), reach, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (held != null && hit.collider.transform.IsChildOf(held.transform)) continue;
                aimHit = hit;
                hasHit = true;
                break;
            }
        }

        void UpdateHeldPose(bool scrub)
        {
            if (held == null) return;
            var target = view.transform.position + view.transform.forward * holdDistance + view.transform.right * 0.16f - Vector3.up * 0.2f;
            if (held is SpongeTool sponge)
            {
                var dish = hasHit ? aimHit.collider.GetComponentInParent<DishItem>() : null;
                sponge.DesktopScrubbing = scrub && dish != null && !dish.IsClean;
                if (sponge.DesktopScrubbing) target = aimHit.point + aimHit.normal * 0.035f;
            }
            held.transform.SetPositionAndRotation(Vector3.Lerp(held.transform.position, target, Mathf.Clamp01(Time.deltaTime * 22)), Quaternion.Euler(0, transform.eulerAngles.y + itemYaw, 0));
        }

        void UpdatePrompt()
        {
            Prompt = held != null ? GameText.Format("Holding {0}  |  Click / Q: release  |  Scroll: reach  |  R: rotate", GameText.Get(held.displayName)) :
                GameText.Get("WASD: walk  |  Mouse: look  |  Click: grab  |  E: use  |  Tab: screen buttons");
            if (!hasHit) return;
            if (held == null && aimHit.collider.TryGetComponent<FidgetCubeController>(out _))
            {
                Prompt = GameText.Get("Click: pick up Fidget Cube");
                return;
            }
            var action = aimHit.collider.GetComponentInParent<HomeAction>();
            if (action != null) { Prompt = GameText.Format("E: {0}", GameText.Get(action.instruction)); return; }
            var item = aimHit.collider.GetComponentInParent<HomeItem>();
            if (item != null && !item.IsLocked && !item.CanInteract)
            {
                Prompt = GameText.Format("LOCKED  |  {0}", HomeSession.Instance?.CurrentObjective);
                return;
            }
            if (item is DishItem dish)
            {
                if (held is SpongeTool) Prompt = GameText.Format("Hold E: scrub {0} in the running sink  |  Washed {1:P0}",
                    GameText.Get(dish.kind.ToString().ToLowerInvariant()), dish.WashProgress);
                else if (!dish.IsStored) Prompt = GameText.Format("{0}  |  {1}  |  Click: grab", GameText.Get(dish.kind.ToString()),
                    dish.IsClean ? GameText.Get("CLEAN - ready to sort") : GameText.Format("DIRTY - washed {0:P0}", dish.WashProgress));
            }
            else if (item != null && held == null) Prompt = GameText.Format("Click: pick up {0}", GameText.Get(item.displayName));
        }

        void PickUp(HomeItem item)
        {
            held = item;
            itemYaw = 0;
            holdDistance = 0.8f;
            held.SetDesktopHeld(true);
            Physics.IgnoreCollision(held.Shape, controller, true);
        }

        public void Drop()
        {
            if (heldCube != null)
            {
                fidgetContact.End();
                Physics.IgnoreCollision(heldCube.GetComponent<BoxCollider>(), controller, false);
                heldCube.SetDesktopHeld(false);
                heldCube = null;
            }
            if (held == null) return;
            if (held is SpongeTool sponge) sponge.DesktopScrubbing = false;
            Physics.IgnoreCollision(held.Shape, controller, false);
            held.SetDesktopHeld(false);
            held.Body.linearVelocity = view.transform.forward * 0.1f;
            held = null;
        }

        bool TryPickUpCube(FidgetCubeController cube)
        {
            if (!cube.SetDesktopHeld(true)) return false;
            heldCube = cube;
            cubeViewRotation = Quaternion.Inverse(view.transform.rotation) * cube.transform.rotation;
            Physics.IgnoreCollision(cube.GetComponent<BoxCollider>(), controller, true);
            SetCapture(false);
            PositionCube();
            Prompt = GameText.Get("Fidget Cube  |  Click / drag: play  |  Right drag / arrows: turn  |  Q: drop");
            return true;
        }

        void PlayWithCube(Keyboard keyboard, Mouse mouse)
        {
            if (keyboard.qKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame)
            {
                Drop();
                SetCapture(keyboard.qKey.wasPressedThisFrame);
                Prompt = MouseCaptured ? GameText.Get("WASD: walk  |  Mouse: look") : GameText.Get("Click the game to look around");
                return;
            }
            if (!Application.isFocused)
            {
                fidgetContact.End();
                return;
            }

            MoveCharacter(Vector2.zero);
            var yaw = (keyboard.rightArrowKey.wasPressedThisFrame ? 90f : 0f) - (keyboard.leftArrowKey.wasPressedThisFrame ? 90f : 0f);
            var tilt = (keyboard.upArrowKey.wasPressedThisFrame ? 90f : 0f) - (keyboard.downArrowKey.wasPressedThisFrame ? 90f : 0f);
            if (mouse.rightButton.isPressed)
            {
                var drag = mouse.delta.ReadValue() * 0.45f;
                yaw -= drag.x;
                tilt += drag.y;
            }
            if (yaw != 0f || tilt != 0f)
            {
                fidgetContact.End();
                cubeViewRotation = Quaternion.Euler(tilt, yaw, 0f) * cubeViewRotation;
            }
            PositionCube();
            if (mouse.rightButton.isPressed || !mouse.leftButton.isPressed)
            {
                fidgetContact.End();
                return;
            }
            var cursor = mouse.position.ReadValue();
            if (!view.pixelRect.Contains(cursor)) return;
            var ray = view.ScreenPointToRay(cursor);
            if (mouse.leftButton.wasPressedThisFrame) fidgetContact.Begin(heldCube, ray, 0.6f);
            fidgetContact.Move(ray);
        }

        void PositionCube()
        {
            var position = view.transform.TransformPoint(0f, 0f, 0.25f);
            var rotation = view.transform.rotation * cubeViewRotation;
            heldCube.Body.position = position;
            heldCube.Body.rotation = rotation;
            heldCube.transform.SetPositionAndRotation(position, rotation);
        }

        void SetCapture(bool capture)
        {
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
        }

        internal void SitForMeeting(Transform seat, float eyeHeight)
        {
            Drop();
            standingEyeHeight = view.transform.localPosition.y;
            MoveToMeetingPoint(seat);
            SetEyeHeight(eyeHeight);
            SetCapture(false);
        }

        internal void LeaveMeetingSeat(Transform standPoint)
        {
            if (standPoint != null) MoveToMeetingPoint(standPoint);
            SetEyeHeight(standingEyeHeight);
        }

        void MoveToMeetingPoint(Transform point)
        {
            transform.SetPositionAndRotation(point.position, point.rotation);
            view.transform.localRotation = Quaternion.identity;
            pitch = 0;
            fallingSpeed = 0;
        }

        void SetEyeHeight(float height)
        {
            var position = view.transform.localPosition;
            position.y = height;
            view.transform.localPosition = position;
        }

        void OnDisable() { Drop(); SetCapture(false); }
    }
}
