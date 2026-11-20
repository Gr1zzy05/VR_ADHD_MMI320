using UnityEngine;

namespace HomeTasks
{
    [RequireComponent(typeof(BoxCollider))]
    public abstract class FidgetControl : MonoBehaviour
    {
        // The control stays fixed against the cube. Only its visual pivot animates.
        [SerializeField] protected Transform visualPivot;

        object contactOwner;
        public Transform VisualPivot => visualPivot;
        public bool IsEngaged => contactOwner != null;

        public bool TryBeginContact(object owner, Vector3 worldPoint)
        {
            if (owner == null || IsEngaged || !isActiveAndEnabled || !CanBeginContact()) return false;
            contactOwner = owner;
            OnContactStarted(transform.InverseTransformPoint(worldPoint));
            return true;
        }

        public void MoveContact(object owner, Vector3 worldPoint, float deltaTime)
        {
            if (owner != null && contactOwner == owner)
                OnContactMoved(transform.InverseTransformPoint(worldPoint), deltaTime);
        }

        public void EndContact(object owner)
        {
            if (owner == null || contactOwner != owner) return;
            contactOwner = null;
            OnContactEnded();
        }

        protected virtual bool CanBeginContact() => true;
        protected virtual void OnContactStarted(Vector3 localPoint) { }
        protected virtual void OnContactMoved(Vector3 localPoint, float deltaTime) { }
        protected virtual void OnContactEnded() { }

        protected virtual void OnDisable() => EndContact(contactOwner);
    }
}
