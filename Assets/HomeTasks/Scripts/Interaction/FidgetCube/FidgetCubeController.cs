using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace HomeTasks
{
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public sealed class FidgetCubeController : MonoBehaviour
    {
        [SerializeField, Min(0.02f)] float controllerReach = 0.12f;
        [SerializeField, Min(0.5f)] float recoveryDrop = 3f;
        [SerializeField, Min(1f)] float recoveryRadius = 45f;

        readonly List<IXRInteractor> interactors = new List<IXRInteractor>();
        readonly List<HandContact> contacts = new List<HandContact>(2);
        readonly RaycastHit[] rayHits = new RaycastHit[16];
        Pose spawnPose;
        XRGrabInteractable grab;
        Rigidbody body;
        bool desktopHeld;
        RigidbodyInterpolation releaseInterpolation;

        struct HandContact
        {
            public NearFarInteractor hand;
            public FidgetControl control;
            public Vector3 tipToContact;
        }

        public Rigidbody Body => body;
        public bool IsGrabbed => grab.isSelected;
        public bool IsHeld => desktopHeld || IsGrabbed;

        internal bool SetDesktopHeld(bool held)
        {
            if (held == desktopHeld || (held && (IsGrabbed || !isActiveAndEnabled))) return false;
            desktopHeld = held;
            if (held)
            {
                // Desktop places the cube against the current camera pose instead of interpolated physics poses.
                releaseInterpolation = body.interpolation;
                body.interpolation = RigidbodyInterpolation.None;
            }
            else body.interpolation = releaseInterpolation;
            body.isKinematic = held;
            grab.enabled = !held;
            if (!held) body.WakeUp();
            return true;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
            spawnPose = new Pose(transform.position, transform.rotation);
        }

        void LateUpdate()
        {
            if (!IsGrabbed && !body.isKinematic &&
                (body.position.y < spawnPose.position.y - recoveryDrop ||
                 (body.position - spawnPose.position).sqrMagnitude > recoveryRadius * recoveryRadius))
                ReturnToSpawn();

            if (grab.interactionManager == null) return;
            grab.interactionManager.GetRegisteredInteractors(interactors);
            UpdateContacts();

            foreach (var interactor in interactors)
            {
                if (interactor is not NearFarInteractor hand || !CanUseHand(hand) ||
                    !hand.activateInput.ReadWasPerformedThisFrame() || !hand.activateInput.ReadIsPerformed())
                    continue;

                var tip = hand.nearInteractionCaster?.effectiveCastOrigin ?? hand.transform;
                // Back up slightly so pointing still works when the near origin enters a small trigger.
                const float rearOffset = 0.025f;
                var pointing = new Ray(tip.position - tip.forward * rearOffset, tip.forward);
                if (!TryPoint(pointing, controllerReach + rearOffset, out var control, out var hitPoint) ||
                    !control.TryBeginContact(hand, hitPoint)) continue;

                contacts.Add(new HandContact
                {
                    hand = hand,
                    control = control,
                    tipToContact = control.transform.InverseTransformVector(hitPoint - tip.position)
                });
            }
        }

        bool CanUseHand(NearFarInteractor hand) => hand != null && hand.isActiveAndEnabled &&
            hand.interactionManager == grab.interactionManager && !hand.hasSelection && hand.enableNearCasting &&
            (hand.interactionLayers.value & grab.interactionLayers.value) != 0;

        void UpdateContacts()
        {
            for (var i = contacts.Count - 1; i >= 0; i--)
            {
                var contact = contacts[i];
                var hand = contact.hand;
                var control = contact.control;
                if (control != null && control.isActiveAndEnabled && CanUseHand(hand) && hand.activateInput.ReadIsPerformed())
                {
                    var tip = hand.nearInteractionCaster?.effectiveCastOrigin ?? hand.transform;
                    if ((tip.position - control.transform.position).sqrMagnitude < 4f * controllerReach * controllerReach)
                    {
                        var point = tip.position + control.transform.TransformVector(contact.tipToContact);
                        control.MoveContact(hand, point, Time.deltaTime);
                        continue;
                    }
                }

                if (control != null) control.EndContact(hand);
                contacts.RemoveAt(i);
            }
        }

        internal bool TryPoint(Ray ray, float reach, out FidgetControl control, out Vector3 point)
        {
            var hits = rayHits;
            var count = Physics.RaycastNonAlloc(ray, hits, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            if (count == hits.Length)
            {
                hits = Physics.RaycastAll(ray, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
                count = hits.Length;
            }

            control = null;
            point = default;
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.distance >= nearest) continue;
                var candidate = hit.collider.isTrigger ? hit.collider.GetComponent<FidgetControl>() : null;
                if (hit.collider.isTrigger && candidate == null) continue;
                nearest = hit.distance;
                control = candidate;
                point = hit.point;
            }

            return control != null && control.transform.IsChildOf(transform) &&
                Vector3.Dot(ray.direction, control.transform.forward) < -0.1f;
        }

        public void ReturnToSpawn()
        {
            if (IsGrabbed) return;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.position = spawnPose.position;
            body.rotation = spawnPose.rotation;
            transform.SetPositionAndRotation(spawnPose.position, spawnPose.rotation);
        }

        void OnDisable()
        {
            foreach (var contact in contacts)
            {
                if (contact.control != null) contact.control.EndContact(contact.hand);
            }
            contacts.Clear();
        }
    }
}
