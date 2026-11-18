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
        HomeItem held;
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
                Prompt = "All quests complete. Preparing your results.";
                SetCapture(false);
                return;
            }
            if (held != null && !held.CanInteract) Drop();
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame)
            {
                SetCapture(!MouseCaptured && !keyboard.escapeKey.wasPressedThisFrame);
                if (held is SpongeTool tool) tool.DesktopScrubbing = false;
            }
            var move = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0), (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            if (!MouseCaptured)
            {
                var seated = meeting != null && meeting.seat != null && meeting.seat.IsPlayerInSeat;
                Prompt = seated ? "Click meeting buttons  |  WASD: leave the chair" : "Click the game to look around  |  Tab: mouse for screen buttons";
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
            Prompt = held != null ? $"Holding {held.displayName}  |  Click / Q: release  |  Scroll: reach  |  R: rotate" : "WASD: walk  |  Mouse: look  |  Click: grab  |  E: use  |  Tab: screen buttons";
            if (!hasHit) return;
            var action = aimHit.collider.GetComponentInParent<HomeAction>();
            if (action != null) { Prompt = "E: " + action.instruction; return; }
            var item = aimHit.collider.GetComponentInParent<HomeItem>();
            if (item != null && !item.IsLocked && !item.CanInteract)
            {
                Prompt = "LOCKED  |  " + HomeSession.Instance?.CurrentObjective;
                return;
            }
            if (item is DishItem dish)
            {
                if (held is SpongeTool) Prompt = $"Hold E: scrub {dish.kind.ToString().ToLower()} in the running sink  |  Washed {dish.WashProgress:P0}";
                else if (!dish.IsStored) Prompt = $"{dish.kind}  |  {(dish.IsClean ? "CLEAN - ready to sort" : $"DIRTY - washed {dish.WashProgress:P0}")}  |  Click: grab";
            }
            else if (item != null && held == null) Prompt = "Click: pick up " + item.displayName;
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
            if (held == null) return;
            if (held is SpongeTool sponge) sponge.DesktopScrubbing = false;
            Physics.IgnoreCollision(held.Shape, controller, false);
            held.SetDesktopHeld(false);
            held.Body.linearVelocity = view.transform.forward * 0.1f;
            held = null;
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
