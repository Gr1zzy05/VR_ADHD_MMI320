using TMPro;
using UnityEngine;

namespace HomeTasks
{
    public sealed class PhoneNotifications : MonoBehaviour
    {
        static readonly string[] Senders = { "Group chat", "Puzzle club", "Weekend plans" };
        static readonly string[] Messages =
        {
            "Nina\nAnyone up for noodles after work? The place by the park reopened.",
            "New challenge\nFind three hidden stars in today's puzzle.",
            "Maya\nI shared the walking route. Meet by the park entrance at nine."
        };

        public TMP_Text sender;
        public TMP_Text body;
        public TMP_Text positionLabel;
        int index;

        void OnEnable()
        {
            index = 0;
            Refresh();
        }

        public void Next()
        {
            index = (index + 1) % Messages.Length;
            Refresh();
        }

        void Refresh()
        {
            sender.text = Senders[index];
            body.text = Messages[index];
            positionLabel.text = $"{index + 1} / {Messages.Length}";
        }
    }
}
