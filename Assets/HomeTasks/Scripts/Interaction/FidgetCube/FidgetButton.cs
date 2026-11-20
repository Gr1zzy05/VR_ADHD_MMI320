using UnityEngine;

namespace HomeTasks
{
    public sealed class FidgetButton : FidgetControl
    {
        [SerializeField] Vector3 pressDirection = Vector3.back;
        [SerializeField, Min(0f)] float pressDistance = 0.0015f;
        [SerializeField, Min(0.01f)] float animationSpeed = 14f;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip click;

        Vector3 restPosition;
        float travel;

        void Awake() => restPosition = visualPivot.localPosition;

        protected override void OnContactStarted(Vector3 localPoint)
        {
            if (audioSource != null && click != null) audioSource.PlayOneShot(click);
        }

        void Update()
        {
            travel = Mathf.MoveTowards(travel, IsEngaged ? 1f : 0f, animationSpeed * Time.deltaTime);
            visualPivot.localPosition = restPosition + pressDirection.normalized * (pressDistance * travel);
        }
    }
}
