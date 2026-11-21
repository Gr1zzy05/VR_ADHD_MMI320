using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace FocusShift.Toys
{
    [RequireComponent(typeof(XRGrabInteractable), typeof(AudioSource))]
    public sealed class PopItController : MonoBehaviour
    {
        public PopItBubble[] bubbles;
        public AudioClip popSound;
        [Min(.02f)] public float pressAnimationDuration = .13f;
        [Tooltip("Fraction of the full 18 mm dome travel.")]
        [Range(.65f, 1f)] public float bubbleDepressionDepth = 1f;
        [Range(0f, 1f)] public float popSoundVolume = .28f;
        [Range(0f, 1f)] public float hapticIntensity = .16f;
        [Min(.01f)] public float nearReach = .13f;
        readonly List<IXRInteractor> hands = new List<IXRInteractor>(16);
        XRGrabInteractable grab;
        AudioSource speaker;

        void Awake() { grab = GetComponent<XRGrabInteractable>(); speaker = GetComponent<AudioSource>(); }
        void LateUpdate()
        {
            if (grab.interactionManager == null) return;
            grab.interactionManager.GetRegisteredInteractors(hands);
            foreach (var interactor in hands)
            {
                if (interactor is not NearFarInteractor hand || !hand.isActiveAndEnabled || hand.hasSelection ||
                    !hand.enableNearCasting || (hand.interactionLayers.value & grab.interactionLayers.value) == 0 ||
                    !hand.activateInput.ReadWasPerformedThisFrame()) continue;
                var tip = hand.nearInteractionCaster?.effectiveCastOrigin ?? hand.transform;
                var ray = new Ray(tip.position - tip.forward * .025f, tip.forward);
                if (Physics.Raycast(ray, out var hit, nearReach + .025f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && hit.rigidbody == grab.GetComponent<Rigidbody>() && TryPress(hit.point, hit.normal) && hapticIntensity > 0f)
                    hand.SendHapticImpulse(hapticIntensity, .035f);
            }
        }
        public bool TryPress(Vector3 point, Vector3 normal)
        {
            if (Mathf.Abs(Vector3.Dot(normal, transform.up)) < .5f) return false;
            var local = transform.InverseTransformPoint(point);
            var xy = new Vector2(local.x, local.z);
            foreach (var bubble in bubbles)
            {
                const float membraneRadius = .01455f;
                if ((xy - bubble.centerOnBoard).sqrMagnitude > membraneRadius * membraneRadius) continue;
                if (!bubble.Toggle(pressAnimationDuration, bubbleDepressionDepth)) return false;
                if (popSound != null && popSoundVolume > 0f) speaker.PlayOneShot(popSound, popSoundVolume);
                return true;
            }
            return false;
        }
        [ContextMenu("Reset All Bubbles")]
        public void ResetAllBubbles()
        {
            foreach (var b in bubbles) b.ResetBubble(pressAnimationDuration);
        }
    }
}
