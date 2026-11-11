using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HomeTasks
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class HomeAction : MonoBehaviour
    {
        public string instruction = "Use";
        public UnityEvent onUse = new UnityEvent();
        XRSimpleInteractable interactable;
        float lastUse = -1;

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(Selected);
        }

        void Selected(SelectEnterEventArgs _) => Use();

        public void Use()
        {
            if (Time.unscaledTime - lastUse < 0.3f) return;
            lastUse = Time.unscaledTime;
            onUse.Invoke();
        }

        void OnDestroy()
        {
            if (interactable != null) interactable.selectEntered.RemoveListener(Selected);
        }
    }
}
