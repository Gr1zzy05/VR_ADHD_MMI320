using UnityEngine;

namespace HomeTasks
{
    public sealed class FidgetJoystick : FidgetControl
    {
        [SerializeField, Range(0f, 45f)] float maxTiltAngle = 21f;
        [SerializeField, Min(0.001f)] float travelForMaxTilt = 0.013f;
        [SerializeField, Min(1f)] float degreesPerSecond = 270f;

        Quaternion restRotation;
        Vector3 startPoint;
        Vector2 direction;

        void Awake() => restRotation = visualPivot.localRotation;

        protected override void OnContactStarted(Vector3 localPoint) => startPoint = localPoint;

        protected override void OnContactMoved(Vector3 localPoint, float deltaTime)
        {
            var delta = localPoint - startPoint;
            direction = Vector2.ClampMagnitude(new Vector2(delta.x, delta.y) / travelForMaxTilt, 1f);
        }

        protected override void OnContactEnded() => direction = Vector2.zero;

        void Update()
        {
            var axis = new Vector3(-direction.y, direction.x, 0f);
            var target = Quaternion.AngleAxis(maxTiltAngle * direction.magnitude, axis.normalized) * restRotation;
            visualPivot.localRotation = Quaternion.RotateTowards(visualPivot.localRotation, target, degreesPerSecond * Time.deltaTime);
        }
    }
}
