using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

namespace HomeTasks
{
    [RequireComponent(typeof(XROrigin))]
    public sealed class VRHeightAdjustment : MonoBehaviour
    {
        const string PreferenceKey = "HomeFocus.HeightOffsetCm";
        const int LimitCentimeters = 50;
        const int StepCentimeters = 5;

        public int OffsetCentimeters { get; private set; }

        XROrigin origin;
        Transform trackingSpace;
        float initialTrackingHeight;
        float seatedOffset;

        void Awake()
        {
            origin = GetComponent<XROrigin>();
            var offsetObject = origin.CameraFloorOffsetObject;
            if (offsetObject == null || offsetObject == origin.Origin) return;

            trackingSpace = offsetObject.transform;
            initialTrackingHeight = trackingSpace.localPosition.y;
            OffsetCentimeters = Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey), -LimitCentimeters, LimitCentimeters);
        }

        void OnEnable() => ApplyHeight();
        void LateUpdate() => ApplyHeight();

        public void Raise() => SetOffset(OffsetCentimeters + StepCentimeters);
        public void Lower() => SetOffset(OffsetCentimeters - StepCentimeters);
        public void ResetHeight() => SetOffset(0);

        void SetOffset(int centimeters)
        {
            var offset = Mathf.Clamp(centimeters, -LimitCentimeters, LimitCentimeters);
            if (offset == OffsetCentimeters) return;
            OffsetCentimeters = offset;
            ApplyHeight();
            PlayerPrefs.SetInt(PreferenceKey, offset);
            PlayerPrefs.Save();
        }

        internal void SetSeatedEyeHeight(float heightAboveFloor)
        {
            ApplyHeight();
            // Use the current tracked pose so seated and standing VR players enter at the same virtual height.
            var trackedEyeHeight = origin.CameraInOriginSpaceHeight - seatedOffset - OffsetCentimeters * 0.01f;
            seatedOffset = heightAboveFloor - trackedEyeHeight;
            ApplyHeight();
        }

        internal void ClearSeatedEyeHeight()
        {
            seatedOffset = 0;
            ApplyHeight();
        }

        void ApplyHeight()
        {
            if (trackingSpace == null) return;

            // Reapply on the shared parent in case XROrigin resets its offset during a tracking origin update.
            var baseline = origin.CurrentTrackingOriginMode switch
            {
                TrackingOriginModeFlags.Floor => 0,
                TrackingOriginModeFlags.Device or TrackingOriginModeFlags.Unbounded => origin.CameraYOffset,
                _ => initialTrackingHeight
            };
            var position = trackingSpace.localPosition;
            var adjustedHeight = baseline + OffsetCentimeters * 0.01f + seatedOffset;
            if (Mathf.Approximately(position.y, adjustedHeight)) return;
            position.y = adjustedHeight;
            trackingSpace.localPosition = position;
        }
    }
}
