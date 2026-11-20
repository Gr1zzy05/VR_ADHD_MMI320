using UnityEngine;

namespace HomeTasks
{
    public sealed class FidgetSpinner : FidgetControl
    {
        [SerializeField] Vector3 spinAxis = Vector3.forward;
        [SerializeField, Min(1f)] float maxAngularSpeed = 1440f;
        [SerializeField, Min(0.01f)] float damping = 3.5f;
        [SerializeField, Min(0.001f)] float centerSwipeRadius = 0.012f;

        Quaternion restRotation;
        Vector3 previousPoint;
        float angle;
        public float AngularSpeed { get; private set; }

        void Awake() => restRotation = visualPivot.localRotation;

        protected override void OnContactStarted(Vector3 localPoint)
        {
            previousPoint = localPoint;
            AngularSpeed = 0f;
        }

        protected override void OnContactMoved(Vector3 localPoint, float deltaTime)
        {
            if (deltaTime <= 0f) return;
            var axis = spinAxis.normalized;
            var pivot = visualPivot.localPosition;
            var from = Vector3.ProjectOnPlane(previousPoint - pivot, axis);
            var to = Vector3.ProjectOnPlane(localPoint - pivot, axis);
            previousPoint = localPoint;
            var delta = Vector3.SignedAngle(from, to, axis);
            if (Mathf.Min(from.sqrMagnitude, to.sqrMagnitude) < 0.000009f)
            {
                var reference = Mathf.Abs(Vector3.Dot(axis, Vector3.right)) > 0.9f ? Vector3.up : Vector3.right;
                var tangent = Vector3.ProjectOnPlane(reference, axis).normalized;
                delta = Vector3.Dot(to - from, tangent) / centerSwipeRadius * Mathf.Rad2Deg;
            }
            var speed = Mathf.Clamp(delta / deltaTime, -maxAngularSpeed, maxAngularSpeed);
            AngularSpeed = Mathf.Lerp(AngularSpeed, speed, 1f - Mathf.Exp(-35f * deltaTime));
            Rotate(speed * deltaTime);
        }

        void Update()
        {
            if (IsEngaged || AngularSpeed == 0f) return;
            var decay = Mathf.Exp(-damping * Time.deltaTime);
            Rotate(AngularSpeed * (1f - decay) / damping);
            AngularSpeed *= decay;
            if (Mathf.Abs(AngularSpeed) < 0.5f) AngularSpeed = 0f;
        }

        void Rotate(float delta)
        {
            angle = Mathf.Repeat(angle + delta, 360f);
            visualPivot.localRotation = Quaternion.AngleAxis(angle, spinAxis.normalized) * restRotation;
        }
    }
}
