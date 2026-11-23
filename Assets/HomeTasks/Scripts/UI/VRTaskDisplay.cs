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
            if (session == null) session = HomeSession.Instance;
            if (session == null || Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 0.15f;
            summary.text = GameText.Format("HOME FOCUS   {0}/4\n{1}\nWash {2}/{3}   Sort {4}/{3}   Trash {5}/{6}",
                session.CompletedTasks, session.CurrentQuestName, session.Washed, session.dishes.Count,
                session.Organized, session.Disposed, session.rubbish.Count);
            message.text = string.IsNullOrEmpty(session.Toast) ? session.CurrentObjective : session.Toast;
        }
    }
}
