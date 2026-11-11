using TMPro;
using UnityEngine;

namespace HomeTasks
{
    public sealed class VRTaskDisplay : MonoBehaviour
    {
        public HomeSession session;
        public TMP_Text summary;
        public TMP_Text message;
        float nextUpdate;

        void Update()
        {
            if (session == null || Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 0.15f;
            summary.text = $"HOME FOCUS   {session.CompletedTasks}/4\nWash {session.Washed}/{session.dishes.Count}    Sort {session.Organized}/{session.dishes.Count}\nTrash {session.Disposed}/{session.rubbish.Count}    Call {(session.meeting.Completed ? "done" : session.meeting.State.ToString().ToLower())}";
            message.text = string.IsNullOrEmpty(session.Toast) ? "Choose one small task." : session.Toast;
        }
    }
}
