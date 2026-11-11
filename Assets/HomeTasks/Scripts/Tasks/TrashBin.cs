using UnityEngine;

namespace HomeTasks
{
    public sealed class TrashBin : MonoBehaviour
    {
        public BoxCollider volume;
        public Transform depositPoint;

        void OnTriggerStay(Collider other)
        {
            var item = other.GetComponentInParent<TrashItem>();
            if (item != null) TryDeposit(item);
        }

        public bool TryDeposit(TrashItem item)
        {
            if (item == null || item.IsHeld || item.IsDisposed || !TaskVolume.Contains(volume, item.transform.position)) return false;
            return item.DisposeAt(depositPoint);
        }
    }
}
