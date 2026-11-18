using Unity.XR.CoreUtils;
using UnityEngine;

namespace HomeTasks
{
    public sealed class MeetingSeat : MonoBehaviour
    {
        public MeetingController meeting;
        public PlayerModeRouter player;
        public Collider chairCollider;
        public Transform standPoint;
        [Min(0.6f)] public float seatedEyeHeight = 1.35f;
        [Min(0.25f)] public float seatRadius = 0.6f;

        Camera seatedView;
        CharacterController body;
        XROrigin xrOrigin;
        VRHeightAdjustment heightAdjustment;
        DesktopPlayer desktop;
        bool ignoredChairCollision;

        public bool IsPlayerInSeat
        {
            get
            {
                if (seatedView == null || !seatedView.isActiveAndEnabled || body == null) return false;
                // Check the headset or desktop camera so room-scale steps also count as leaving.
                var offset = seatedView.transform.position - transform.position;
                var distanceSquared = offset.x * offset.x + offset.z * offset.z;
                return distanceSquared <= seatRadius * seatRadius && Mathf.Abs(body.transform.position.y - transform.position.y) < 0.45f;
            }
        }

        public void Sit()
        {
            if (!isActiveAndEnabled || seatedView != null || meeting == null || meeting.seat != this || player == null || !meeting.CanStart()) return;

            var rig = player.IsVR ? player.questRig : player.desktopRig;
            if (rig == null || !rig.activeInHierarchy || !rig.TryGetComponent(out CharacterController controller)) return;
            if (player.IsVR)
            {
                if (!rig.TryGetComponent(out XROrigin origin) || origin.Camera == null ||
                    !rig.TryGetComponent(out VRHeightAdjustment adjustment) || !adjustment.isActiveAndEnabled) return;
                xrOrigin = origin;
                heightAdjustment = adjustment;
                seatedView = xrOrigin.Camera;
            }
            else
            {
                if (!rig.TryGetComponent(out desktop) || desktop.view == null) return;
                seatedView = desktop.view;
            }

            body = controller;
            if (chairCollider != null && !Physics.GetIgnoreCollision(body, chairCollider))
            {
                Physics.IgnoreCollision(body, chairCollider, true);
                ignoredChairCollision = true;
            }

            if (xrOrigin != null)
            {
                MoveXRTo(transform);
                heightAdjustment.SetSeatedEyeHeight(seatedEyeHeight);
            }
            else desktop.SitForMeeting(transform, seatedEyeHeight);

            meeting.Begin();
            if (meeting.State != MeetingState.Listening) LeaveSeat(standPoint);
        }

        public void Stand()
        {
            LeaveSeat(IsPlayerInSeat ? standPoint : null);
        }

        void LateUpdate()
        {
            if (seatedView != null && !IsPlayerInSeat) LeaveSeat(null);
        }

        void OnDisable() => LeaveSeat(null);

        void MoveXRTo(Transform point)
        {
            xrOrigin.MatchOriginUpCameraForward(Vector3.up, point.forward);
            var trackingOrigin = xrOrigin.Origin.transform;
            var roomOffset = Vector3.ProjectOnPlane(seatedView.transform.position - trackingOrigin.position, Vector3.up);
            trackingOrigin.position = point.position - roomOffset;
        }

        void LeaveSeat(Transform exit)
        {
            if (seatedView == null) return;
            if (xrOrigin != null)
            {
                if (exit != null) MoveXRTo(exit);
                heightAdjustment.ClearSeatedEyeHeight();
            }
            else if (desktop != null) desktop.LeaveMeetingSeat(exit);

            if (ignoredChairCollision && body != null && chairCollider != null)
                Physics.IgnoreCollision(body, chairCollider, false);

            seatedView = null;
            body = null;
            xrOrigin = null;
            heightAdjustment = null;
            desktop = null;
            ignoredChairCollision = false;
            if (meeting != null) meeting.SeatVacated();
        }
    }
}
