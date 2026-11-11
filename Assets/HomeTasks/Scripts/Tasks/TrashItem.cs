using UnityEngine;

namespace HomeTasks
{
    public sealed class TrashItem : HomeItem
    {
        public bool IsDisposed { get; private set; }

        public bool DisposeAt(Transform bin)
        {
            if (IsHeld || IsDisposed || bin == null) return false;
            LockTo(bin);
            IsDisposed = true;
            gameObject.SetActive(false);
            HomeSession.Instance?.Notify("Rubbish taken outside and placed in the bin.", true);
            return true;
        }
    }
}
