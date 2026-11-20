using UnityEngine;

namespace HomeTasks
{
    public sealed class FidgetGear : FidgetControl
    {
        [SerializeField] Vector3 axle = Vector3.right;
        [SerializeField] bool reverseDirection;
        [SerializeField, Min(0.01f)] float gearRatio = 1f;
        [SerializeField, Min(0.001f)] float rollRadius = 0.01f;
        [SerializeField] FidgetGear trainRoot;
        [SerializeField] FidgetGear[] followers = System.Array.Empty<FidgetGear>();

        Quaternion restRotation;
        Vector3 previousPoint;
        FidgetGear activeGear;
        float angle;
        FidgetGear Train => trainRoot != null ? trainRoot : this;
        float SignedRatio => reverseDirection ? -gearRatio : gearRatio;

        void Awake() => restRotation = visualPivot.localRotation;

        protected override bool CanBeginContact() => Train.activeGear == null;

        protected override void OnContactStarted(Vector3 localPoint)
        {
            Train.activeGear = this;
            previousPoint = localPoint;
        }

        protected override void OnContactMoved(Vector3 localPoint, float deltaTime)
        {
            var tangent = Vector3.Cross(axle.normalized, Vector3.forward).normalized;
            var delta = Vector3.Dot(localPoint - previousPoint, tangent) / rollRadius * Mathf.Rad2Deg;
            previousPoint = localPoint;

            var train = Train;
            var trainDelta = delta / SignedRatio;
            train.Rotate(trainDelta * train.SignedRatio);
            foreach (var gear in train.followers)
            {
                if (gear != null) gear.Rotate(trainDelta * gear.SignedRatio);
            }
        }

        protected override void OnContactEnded() => Train.activeGear = null;

        void Rotate(float delta)
        {
            angle = Mathf.Repeat(angle + delta, 360f);
            visualPivot.localRotation = Quaternion.AngleAxis(angle, axle.normalized) * restRotation;
        }
    }
}
