using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace FocusShift.Toys
{
    [RequireComponent(typeof(Camera))]
    public sealed class ToyDemoMouse : MonoBehaviour
    {
        public PopItController board;
        Camera view;
        Rigidbody held;
        ButterSquishyController pressing;
        XRGrabInteractable grip;
        bool leftHeld, rightHeld;
        Plane dragPlane;
        Vector3 offset;
        void Awake() => view = GetComponent<Camera>();
        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !Application.isFocused) { Release(); return; }
            HandlePointer(mouse, Keyboard.current);
        }
        void HandlePointer(Mouse mouse, Keyboard keys)
        {
            bool beginLeft = !leftHeld && (mouse.leftButton.isPressed || mouse.leftButton.wasPressedThisFrame);
            bool beginRight = !rightHeld && (mouse.rightButton.isPressed || mouse.rightButton.wasPressedThisFrame);
            leftHeld = mouse.leftButton.isPressed;
            rightHeld = mouse.rightButton.isPressed;
            if (keys != null && keys.rKey.wasPressedThisFrame) board.ResetAllBubbles();
            var ray = view.ScreenPointToRay(mouse.position.ReadValue());
            bool hitToy = Physics.Raycast(ray, out var hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.rigidbody != null;
            if (beginRight && hitToy &&
                hit.rigidbody.TryGetComponent(out XRGrabInteractable candidate) && !candidate.isSelected)
            {
                held = hit.rigidbody;
                grip = candidate;
                grip.enabled = false;
                held.isKinematic = true;
                dragPlane = new Plane(view.transform.forward, hit.point);
                offset = held.position - hit.point;
            }
            if (held != null && mouse.rightButton.isPressed && dragPlane.Raycast(ray, out float distance))
            {
                held.position = ray.GetPoint(Mathf.Max(view.nearClipPlane, distance - .075f)) + offset;
                if (keys != null && keys.fKey.wasPressedThisFrame)
                    held.rotation = Quaternion.AngleAxis(180f, held.transform.right) * held.rotation;
            }
            if (beginLeft && hitToy)
            {
                if (hit.rigidbody.TryGetComponent(out pressing)) pressing.SetDesktopPressure(1f);
                else if (hit.rigidbody.TryGetComponent<PopItController>(out var pop)) pop.TryPress(hit.point, hit.normal);
            }
            if (!mouse.leftButton.isPressed && pressing != null) { pressing.SetDesktopPressure(0f); pressing = null; }
            if (!mouse.rightButton.isPressed && held != null) Drop();
        }
        void Drop() { held.isKinematic = false; grip.enabled = true; held.WakeUp(); held = null; }
        void Release()
        {
            leftHeld = rightHeld = false;
            if (held != null) Drop();
            if (pressing != null) { pressing.SetDesktopPressure(0f); pressing = null; }
        }
        void OnDisable() => Release();

        void OnGUI()
        {
            GUI.Box(new Rect(8, Screen.height - 34, Mathf.Min(470, Screen.width - 16), 26),
                "Left: squeeze / pop     Right drag: lift     F: flip while held     R: reset bubbles");
        }
    }
}
