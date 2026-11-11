using TMPro;
using UnityEngine;

namespace HomeTasks
{
    public sealed class DesktopHUD : MonoBehaviour
    {
        public HomeSession session;
        public DesktopPlayer player;
        public TMP_Text taskSummary;
        public TMP_Text prompt;
        public TMP_Text toast;
        public GameObject crosshair;
        float nextUpdate;

        void Update()
        {
            crosshair.SetActive(player.MouseCaptured);
            prompt.text = player.Prompt;
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 0.1f;
            taskSummary.text = $"<b>YOUR FOUR SMALL WINS</b>\n\nWash dishes    {session.Washed}/{session.dishes.Count}\nOrganize dishes    {session.Organized}/{session.dishes.Count}\nTake trash outside    {session.Disposed}/{session.rubbish.Count}\nMeeting + quiz    {(session.meeting.Completed ? "Done" : session.meeting.State.ToString())}";
            toast.text = session.Toast;
        }
    }
}
