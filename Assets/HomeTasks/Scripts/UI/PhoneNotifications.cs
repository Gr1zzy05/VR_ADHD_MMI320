using TMPro;
using UnityEngine;

namespace HomeTasks
{
    public sealed class PhoneNotifications : MonoBehaviour
    {
        const int NotificationCount = 3;

        public TMP_Text sender;
        public TMP_Text body;
        public TMP_Text positionLabel;
        int index;

        void OnEnable()
        {
            index = 0;
            GameText.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => GameText.Changed -= Refresh;

        public void Next()
        {
            index = (index + 1) % NotificationCount;
            Refresh();
        }

        void Refresh()
        {
            (sender.text, body.text) = index switch
            {
                0 => (GameText.Get("Group chat"), GameText.Get("Nina\nAnyone up for noodles after work? The place by the park reopened.")),
                1 => (GameText.Get("Puzzle club"), GameText.Get("New challenge\nFind three hidden stars in today's puzzle.")),
                _ => (GameText.Get("Weekend plans"), GameText.Get("Maya\nI shared the walking route. Meet by the park entrance at nine."))
            };
            positionLabel.text = $"{index + 1} / {NotificationCount}";
        }
    }
}
