using UnityEngine;
using UnityEngine.InputSystem;

namespace HomeTasks
{
    [RequireComponent(typeof(Camera))]
    public sealed class FidgetCubeDesktopTester : MonoBehaviour
    {
        [SerializeField] FidgetCubeController cube;
        [SerializeField, Min(0.05f)] float rotationPerPixel = 0.45f;

        Camera view;
        readonly FidgetMouseContact contact = new FidgetMouseContact();
        bool holding;

        void Awake() => view = GetComponent<Camera>();

        void Update()
        {
            if (cube == null) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null || !Application.isFocused)
            {
                contact.End();
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                contact.End();
                cube.ReturnToSpawn();
            }
            if (keyboard.spaceKey.wasPressedThisFrame) SetHolding(!holding);
            if (holding) RotateCube(keyboard, mouse);

            if (!mouse.leftButton.isPressed) contact.End();

            var cursor = mouse.position.ReadValue();
            if (!view.pixelRect.Contains(cursor)) return;
            var ray = view.ScreenPointToRay(cursor);
            if (mouse.leftButton.wasPressedThisFrame && !InstructionsRect.Contains(new Vector2(cursor.x, Screen.height - cursor.y)))
                contact.Begin(cube, ray, view.farClipPlane);
            contact.Move(ray);
        }

        void RotateCube(Keyboard keyboard, Mouse mouse)
        {
            var yaw = (keyboard.rightArrowKey.wasPressedThisFrame ? 90f : 0f) - (keyboard.leftArrowKey.wasPressedThisFrame ? 90f : 0f);
            var pitch = (keyboard.upArrowKey.wasPressedThisFrame ? 90f : 0f) - (keyboard.downArrowKey.wasPressedThisFrame ? 90f : 0f);
            if (mouse.rightButton.isPressed)
            {
                var drag = mouse.delta.ReadValue() * rotationPerPixel;
                yaw -= drag.x;
                pitch += drag.y;
            }
            if (yaw == 0f && pitch == 0f) return;
            contact.End();
            var rotation = Quaternion.AngleAxis(yaw, view.transform.up) * Quaternion.AngleAxis(pitch, view.transform.right) * cube.transform.rotation;
            cube.Body.rotation = rotation;
            cube.transform.rotation = rotation;
        }

        void SetHolding(bool value)
        {
            if (value == holding || cube.IsGrabbed) return;
            contact.End();
            holding = value;
            if (holding)
            {
                cube.ReturnToSpawn();
                cube.Body.isKinematic = true;
            }
            else
            {
                cube.Body.isKinematic = false;
                cube.Body.WakeUp();
            }
        }

        void OnDisable()
        {
            contact.End();
            if (cube != null && cube.Body != null && holding) SetHolding(false);
        }

        void OnGUI()
        {
            GUILayout.BeginArea(InstructionsRect, GUI.skin.box);
            GUILayout.Label(holding ? "Space • Drop    RMB drag • Rotate    Arrows • Turn 90°" : "Space • Pick up");
            GUILayout.Label("Left click • Press / toggle    Left drag • Spin / roll / tilt");
            GUILayout.Label("R • Reset cube pose");
            GUILayout.EndArea();
        }

        static Rect InstructionsRect => new Rect(12f, 12f, Mathf.Min(Screen.width - 24f, 380f), 76f);
    }

    internal sealed class FidgetMouseContact
    {
        FidgetControl control;
        Vector3 localHitPoint;

        public void Begin(FidgetCubeController cube, Ray ray, float reach)
        {
            End();
            Physics.SyncTransforms();
            if (!cube.TryPoint(ray, reach, out var target, out var point) || !target.TryBeginContact(this, point)) return;
            control = target;
            localHitPoint = target.transform.InverseTransformPoint(point);
        }

        public void Move(Ray ray)
        {
            if (control == null) return;
            var frame = control.transform;
            var plane = new Plane(frame.forward, frame.TransformPoint(localHitPoint));
            if (plane.Raycast(ray, out var distance)) control.MoveContact(this, ray.GetPoint(distance), Time.deltaTime);
        }

        public void End()
        {
            if (control != null) control.EndContact(this);
            control = null;
        }
    }
}
