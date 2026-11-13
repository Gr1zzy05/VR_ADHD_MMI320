using UnityEngine;

namespace HomeTasks
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class DishRack : MonoBehaviour
    {
        public DishKind accepts;
        public Transform[] slots;
        public BoxCollider volume;
        public int StoredCount { get; private set; }
        float nextHint;

        void OnTriggerStay(Collider other)
        {
            var dish = other.GetComponentInParent<DishItem>();
            if (dish == null || dish.IsHeld || dish.IsStored) return;
            if (!TryStore(dish) && Time.time > nextHint)
            {
                nextHint = Time.time + 4;
                var session = HomeSession.Instance;
                session?.Notify(!session.IsQuestActive(HomeQuest.Organizing) ? session.CurrentObjective : !dish.IsClean ? "Wash this dish before organizing it." : $"This shelf is for {accepts.ToString().ToLower()}s.");
            }
        }

        public bool TryStore(DishItem dish)
        {
            if (HomeSession.Instance != null && !HomeSession.Instance.IsQuestActive(HomeQuest.Organizing)) return false;
            if (dish == null || !dish.IsClean || dish.IsHeld || dish.IsStored || dish.kind != accepts) return false;
            if (slots == null || StoredCount >= slots.Length || !TaskVolume.Contains(volume, dish.transform.position)) return false;
            if (!dish.Store(slots[StoredCount])) return false;
            StoredCount++;
            return true;
        }
    }
}
