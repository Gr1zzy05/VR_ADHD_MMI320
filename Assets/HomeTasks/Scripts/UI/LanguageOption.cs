using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace HomeTasks
{
    public sealed class LanguageOption : MonoBehaviour
    {
        public TMP_Text label;
        public Button button;

        void OnEnable()
        {
            GameText.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => GameText.Changed -= Refresh;

        void Refresh()
        {
            var selected = GameText.IsReady ? LocalizationSettings.SelectedLocaleAsync.Result : null;
            button.interactable = selected != null;
            var language = selected == null ? "English" : selected.Identifier.Code == GameText.ThaiCode ? "ไทย" : selected.LocaleName;
            label.text = GameText.Format("LANGUAGE  /  {0}", language);
        }

        public void ToggleLanguage()
        {
            if (!GameText.IsReady || !button.interactable) return;
            var selected = LocalizationSettings.SelectedLocaleAsync.Result;
            var code = selected.Identifier.Code == GameText.ThaiCode ? GameText.EnglishCode : GameText.ThaiCode;
            var next = LocalizationSettings.AvailableLocales.GetLocale(code);
            if (next == null) return;
            button.interactable = false;
            LocalizationSettings.SelectedLocale = next;
        }
    }
}
