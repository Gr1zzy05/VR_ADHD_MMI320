using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HomeTasks.Editor
{
    public static partial class HomeSceneBuilder
    {
        const string LocalizationFolder = Root + "/Localization";

        public static void InitializeLanguageTemplate()
        {
            var home = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || home.isDirty || home.path != Root + "/Scenes/NewModel.unity")
                throw new InvalidOperationException("Open the saved NewModel scene in Edit mode.");

            if (!AssetDatabase.IsValidFolder(LocalizationFolder)) AssetDatabase.CreateFolder(Root, "Localization");
            var english = FindOrCreateLocale(GameText.EnglishCode, "English", 0);
            var second = FindOrCreateLocale(GameText.SecondLocaleCode, "Second language", 1);
            if (!second.Metadata.GetMetadatas<FallbackLocale>().Any(fallback => fallback.Locale == english))
            {
                second.Metadata.AddMetadata(new FallbackLocale { Locale = english });
                EditorUtility.SetDirty(second);
            }

            var localization = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (localization == null)
            {
                localization = ScriptableObject.CreateInstance<LocalizationSettings>();
                var selectors = localization.GetStartupLocaleSelectors();
                selectors.Clear();
                selectors.Add(new PlayerPrefLocaleSelector { PlayerPreferenceKey = "home-focus-language" });
                selectors.Add(new SpecificLocaleSelector { LocaleId = english.Identifier });
                AssetDatabase.CreateAsset(localization, LocalizationFolder + "/Localization Settings.asset");
                LocalizationEditorSettings.ActiveLocalizationSettings = localization;
            }
            LocalizationSettings.Instance = localization;
            // Resolve the local tables before the scene's labels request a default locale.
            LocalizationSettings.InitializeSynchronously = true;
            EditorUtility.SetDirty(localization);
            var addressables = AddressableAssetSettingsDefaultObject.Settings;
            addressables.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            EditorUtility.SetDirty(addressables);

            var collection = LocalizationEditorSettings.GetStringTableCollection(GameText.TableName) ??
                LocalizationEditorSettings.CreateStringTableCollection(GameText.TableName, LocalizationFolder);
            collection.SetPreloadTableFlag(true);
            var englishTable = (StringTable)collection.GetTable(english.Identifier);
            var secondTable = (StringTable)collection.GetTable(second.Identifier);

            var results = EditorSceneManager.OpenScene(HomeSession.ResultsScenePath, OpenSceneMode.Additive);
            try
            {
                WireLanguageButtons(home, results);
                var keys = ReadRuntimeKeys();
                var labelsToBind = new Dictionary<TMP_Text, string>();
                foreach (var scene in new[] { home, results })
                {
                    var labels = FindIn<TMP_Text>(scene).Except(UpdatedInCode(scene));
                    foreach (var label in labels)
                    {
                        var source = label.text;
                        if (string.IsNullOrWhiteSpace(source) || !source.Any(char.IsLetter)) continue;
                        keys.Add(source);
                        if (!label.TryGetComponent<LocalizeStringEvent>(out _)) labelsToBind.Add(label, source);
                    }
                }

                foreach (var screen in FindIn<ScreenDistraction>(home))
                    keys.UnionWith(new[] { screen.inactiveLabel, screen.activeLabel, screen.inactiveLabel.ToLowerInvariant(), screen.activeLabel.ToLowerInvariant() });
                keys.UnionWith(FindIn<HomeAction>(home).Select(action => action.instruction));
                keys.UnionWith(FindIn<HomeItem>(home).Select(item => item.displayName));
                foreach (DishKind kind in Enum.GetValues(typeof(DishKind)))
                    keys.UnionWith(new[] { kind.ToString(), kind.ToString().ToLowerInvariant(), kind.ToString().ToLowerInvariant() + "s" });
                foreach (var meeting in FindIn<MeetingController>(home))
                {
                    keys.UnionWith(meeting.content.segments.Select(segment => segment.speaker + ": " + segment.transcript));
                    keys.UnionWith(meeting.content.questions.SelectMany(question => question.answers.Prepend(question.prompt)));
                }

                foreach (var key in keys.Where(key => !string.IsNullOrWhiteSpace(key)))
                {
                    if (englishTable.GetEntry(key) == null) englishTable.AddEntry(key, key);
                    if (secondTable.GetEntry(key) == null) secondTable.AddEntry(key, string.Empty);
                }
                foreach (var asset in new UnityEngine.Object[] { collection, collection.SharedData, englishTable, secondTable }) EditorUtility.SetDirty(asset);
                foreach (var label in labelsToBind) BindLocalizedLabel(label.Key, label.Value);
                LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
                AssetDatabase.SaveAssets();
                foreach (var scene in new[] { home, results })
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                SceneManager.SetActiveScene(home);
                EditorSceneManager.CloseScene(results, true);
            }
        }

        static Locale FindOrCreateLocale(string code, string name, ushort sortOrder)
        {
            var identifier = new LocaleIdentifier(code);
            var existing = LocalizationEditorSettings.GetLocales().FirstOrDefault(locale => locale.Identifier == identifier);
            if (existing != null) return existing;
            var locale = Locale.CreateLocale(identifier);
            locale.LocaleName = name;
            locale.SortOrder = sortOrder;
            AssetDatabase.CreateAsset(locale, $"{LocalizationFolder}/{name}.asset");
            LocalizationEditorSettings.AddLocale(locale);
            return locale;
        }

        static IEnumerable<T> FindIn<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

        static SortedSet<string> ReadRuntimeKeys()
        {
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            var calls = new Regex(@"GameText\.(?:Get|Format)\(\s*""((?:\\.|[^""\\])*)""", RegexOptions.CultureInvariant);
            foreach (var file in Directory.EnumerateFiles(Root + "/Scripts", "*.cs", SearchOption.AllDirectories))
                foreach (Match match in calls.Matches(File.ReadAllText(file)))
                    keys.Add(Regex.Unescape(match.Groups[1].Value));
            return keys;
        }

        static IEnumerable<TMP_Text> UpdatedInCode(Scene scene)
        {
            foreach (var hud in FindIn<DesktopHUD>(scene))
                foreach (var label in new[] { hud.taskSummary, hud.prompt, hud.toast }) yield return label;
            foreach (var board in FindIn<HomeDashboard>(scene))
                foreach (var label in board.counts.Concat(new[] { board.status, board.timer, board.footer })) yield return label;
            foreach (var arm in FindIn<VRArmTaskBoard>(scene))
                foreach (var label in new[] { arm.movementLabel, arm.heightLabel }) yield return label;
            foreach (var display in FindIn<VRTaskDisplay>(scene))
                foreach (var label in new[] { display.summary, display.message }) yield return label;
            foreach (var panel in FindIn<MeetingPanel>(scene))
                foreach (var label in panel.optionLabels.Concat(new[] { panel.connection, panel.progress, panel.body, panel.result, panel.beginLabel })) yield return label;
            foreach (var screen in FindIn<ScreenDistraction>(scene)) yield return screen.buttonLabel;
            foreach (var phone in FindIn<PhoneNotifications>(scene))
                foreach (var label in new[] { phone.sender, phone.body, phone.positionLabel }) yield return label;
            foreach (var result in FindIn<HomeResultsPanel>(scene))
                foreach (var label in new[] { result.title, result.duration, result.chores, result.score, result.feedback,
                    result.distractionNames, result.distractionTimes, result.distractionUses }) yield return label;
            foreach (var language in FindIn<LanguageOption>(scene)) yield return language.label;
        }

        static void BindLocalizedLabel(TMP_Text label, string source)
        {
            var binding = label.gameObject.AddComponent<LocalizeStringEvent>();
            binding.StringReference.SetReference(GameText.TableName, source);
            binding.StringReference.FallbackState = FallbackBehavior.UseFallback;
            var writeText = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), label, "set_text");
            UnityEventTools.AddPersistentListener(binding.OnUpdateString, writeText);
            binding.OnUpdateString.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
            PrefabUtility.RecordPrefabInstancePropertyModifications(binding);
        }

        static void WireLanguageButtons(Scene home, Scene results)
        {
            foreach (var arm in FindIn<VRArmTaskBoard>(home))
            {
                var content = (RectTransform)arm.content.transform;
                if (content.Find("Language option") != null) continue;
                font = arm.movementLabel.font;
                var controls = (RectTransform)content.Find("Controls");
                var y = -controls.anchoredPosition.y - 1;
                foreach (var rect in new[] { (RectTransform)arm.transform, content, (RectTransform)content.Find("Panel") })
                    rect.sizeDelta += new Vector2(0, 44);
                controls.anchoredPosition += new Vector2(0, -44);
                AddLanguageButton(content, 16, y, 328, 36, 15);
            }
            foreach (var desktop in FindIn<DesktopHUD>(home))
            {
                font = desktop.taskSummary.font;
                if (!desktop.TryGetComponent<GraphicRaycaster>(out _)) desktop.gameObject.AddComponent<GraphicRaycaster>();
                AddLanguageButton(desktop.transform, 24, 116, 246, 39, 16);
            }
            foreach (var result in FindIn<HomeResultsPanel>(results))
            {
                font = result.title.font;
                AddLanguageButton(result.transform, 684, 24, 274, 50, 17);
            }
        }

        static void AddLanguageButton(Transform parent, float x, float y, float width, float height, int size)
        {
            if (parent.Find("Language option") != null) return;
            var button = Button(parent, "Language option", x, y, width, height, Sage, Ink, "LANGUAGE  /  English", null);
            var option = button.gameObject.AddComponent<LanguageOption>();
            option.button = button;
            option.label = button.GetComponentInChildren<TMP_Text>();
            option.label.fontSize = size;
            var rect = option.label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8, 1);
            rect.offsetMax = new Vector2(-8, -1);
            UnityEventTools.AddPersistentListener(button.onClick, option.ToggleLanguage);
        }
    }
}
