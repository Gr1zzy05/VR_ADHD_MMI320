using UnityEngine;

namespace HomeTasks
{
    public sealed class TrashBin : MonoBehaviour
    {
        public BoxCollider volume;
        public Transform depositPoint;
        float nextHint;

        void OnTriggerStay(Collider other)
        {
            var item = other.GetComponentInParent<TrashItem>();
            if (item == null) return;
            if (HomeSession.Instance != null && !HomeSession.Instance.IsQuestActive(HomeQuest.Trash))
            {
                if (Time.time > nextHint)
                {
                    nextHint = Time.time + 4;
                    HomeSession.Instance.Notify(HomeSession.Instance.CurrentObjective);
                }
                return;
            }
            TryDeposit(item);
        }

        public bool TryDeposit(TrashItem item)
        {
            if (HomeSession.Instance != null && !HomeSession.Instance.IsQuestActive(HomeQuest.Trash)) return false;
            if (item == null || item.IsHeld || item.IsDisposed || !TaskVolume.Contains(volume, item.transform.position)) return false;
            return item.DisposeAt(depositPoint);
        }
    }
}
