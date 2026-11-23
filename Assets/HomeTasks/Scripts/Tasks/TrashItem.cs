using UnityEngine;

namespace HomeTasks
{
    public sealed class TrashItem : HomeItem
    {
        public bool IsDisposed { get; private set; }
        protected override bool AvailableFor(HomeQuest quest) => quest == HomeQuest.Trash;

        public bool DisposeAt(Transform bin)
        {
            if (IsHeld || IsDisposed || bin == null) return false;
            if (HomeSession.Instance != null && !HomeSession.Instance.IsQuestActive(HomeQuest.Trash)) return false;
            LockTo(bin);
            IsDisposed = true;
            gameObject.SetActive(false);
            HomeSession.Instance?.Notify(GameText.Get("Rubbish taken outside and placed in the bin."), true);
            return true;
        }
    }
}
