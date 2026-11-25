using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HomeTasks
{
    public static class GameText
    {
        public const string TableName = "Home Focus";
        public const string EnglishCode = "en";
        public const string ThaiCode = "th";
        public const string LanguagePreferenceKey = "home-focus-language";
        const string PreviousThaiCode = "x-homefocus";

        static StringTable activeTable;
        static int loadVersion;
        public static bool IsReady { get; private set; }
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            if (PlayerPrefs.GetString(LanguagePreferenceKey) == PreviousThaiCode)
                PlayerPrefs.SetString(LanguagePreferenceKey, ThaiCode);
            activeTable = null;
            loadVersion++;
            IsReady = false;
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            if (!LocalizationSettings.HasSettings) return;
            LocalizationSettings.SelectedLocaleChanged -= Load;
            LocalizationSettings.SelectedLocaleChanged += Load;
            var initialization = LocalizationSettings.InitializationOperation;
            if (initialization.IsDone) LoadInitial(initialization);
            else initialization.Completed += LoadInitial;
        }

        static void LoadInitial(AsyncOperationHandle<LocalizationSettings> operation)
        {
            if (operation.Status != AsyncOperationStatus.Succeeded) return;
            var selected = LocalizationSettings.SelectedLocaleAsync;
            if (selected.IsDone) ApplyInitialLocale(selected);
            else selected.Completed += ApplyInitialLocale;
        }

        static void ApplyInitialLocale(AsyncOperationHandle<Locale> operation)
        {
            if (operation.Status == AsyncOperationStatus.Succeeded) Load(operation.Result);
        }

        static void Load(Locale locale)
        {
            if (locale == null) return;
            var version = ++loadVersion;
            var table = LocalizationSettings.StringDatabase.GetTableAsync(TableName, locale);
            if (table.IsDone) Apply(table);
            else table.Completed += Apply;

            void Apply(AsyncOperationHandle<StringTable> operation)
            {
                if (version != loadVersion) return;
                activeTable = operation.Status == AsyncOperationStatus.Succeeded ? operation.Result : null;
                IsReady = true;
                Changed?.Invoke();
            }
        }

        public static string Get(string source)
        {
            if (activeTable == null || string.IsNullOrWhiteSpace(source)) return source;
            var translation = activeTable.GetEntry(source)?.Value;
            return string.IsNullOrWhiteSpace(translation) ? source : translation;
        }

        public static string Format(string source, params object[] arguments)
        {
            var translation = Get(source);
            try { return string.Format(translation, arguments); }
            catch (FormatException) when (translation != source) { return string.Format(source, arguments); }
        }
    }
}
