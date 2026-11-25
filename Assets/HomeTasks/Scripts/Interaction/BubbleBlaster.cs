using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace HomeTasks
{
    public sealed class BubbleBlaster : HomeItem
    {
        public ParticleSystem bubbles;
        public AudioSource bubbleSound;

        float nextBurst;

        protected override bool AvailableFor(HomeQuest quest) => true;

        void OnEnable()
        {
            Grab.selectEntered.AddListener(ShowTriggerHint);
            Grab.activated.AddListener(Activate);
        }

        void OnDisable()
        {
            Grab.selectEntered.RemoveListener(ShowTriggerHint);
            Grab.activated.RemoveListener(Activate);
        }

        void ShowTriggerHint(SelectEnterEventArgs _) =>
            HomeSession.Instance?.Notify(GameText.Get("Press the trigger to blow bubbles."));

        void Activate(ActivateEventArgs _) => BlowBubbles();

        public void BlowBubbles()
        {
            if (!isActiveAndEnabled || !IsHeld || !CanInteract || bubbles == null || Time.time < nextBurst) return;

            nextBurst = Time.time + 0.25f;
            bubbles.Play();
            bubbles.Emit(10);
            if (bubbleSound != null && bubbleSound.clip != null) bubbleSound.PlayOneShot(bubbleSound.clip);
        }
    }
}
