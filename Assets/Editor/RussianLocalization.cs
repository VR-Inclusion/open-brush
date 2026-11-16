using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Localization;
using UnityEditor.Localization.Addressables;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.TextCore.LowLevel;

namespace TiltBrush
{
    // The JSON file is the editable source; the generated assets also work without this tool.
    public static class RussianLocalization
    {
        const string kRoot = "Assets/Settings/Localization/";
        const string kFontPath = "Assets/Fonts/Roboto-Medium Cyrillic SDF.asset";
        const string kAlphabet = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";

        class NewsTranslation
        {
            public string English;
            public string Russian;
        }

        [MenuItem("Open Brush/Localization/Import Russian translations")]
        public static void Import()
        {
            var translations = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                File.ReadAllText("Support/Localization/Russian.json"));
            var collection = LocalizationEditorSettings.GetStringTableCollection("Strings");
            // These existing English entries and prefab references were missing shared keys.
            AddSharedKey(collection.SharedData, "POPUP_ACCOUNTS_VIVERSEINFO_TITLE", 423748078849216512);
            AddSharedKey(collection.SharedData, "POPUP_ACCOUNTS_VIVERSEINFO_DESCRIPTION", 423748375394897920);
            var english = (StringTable)collection.GetTable("en");
            ImportNews(english, translations);

            var locale = LocalizationEditorSettings.GetLocale("ru");
            if (locale == null)
            {
                locale = Locale.CreateLocale("ru");
                locale.LocaleName = "Русский";
                locale.SortOrder = 7;
                locale.Metadata.AddMetadata(new FallbackLocale(LocalizationEditorSettings.GetLocale("en")));
                AssetDatabase.CreateAsset(locale, kRoot + "Russian (ru).asset");
                LocalizationEditorSettings.AddLocale(locale);
            }

            var russian = (StringTable)(collection.GetTable("ru") ?? collection.AddNewTable("ru"));
            foreach (var shared in collection.SharedData.Entries)
            {
                if (!translations.TryGetValue(shared.Key, out var value) || string.IsNullOrWhiteSpace(value))
                    throw new InvalidDataException("Missing Russian translation: " + shared.Key);
                var source = english.GetEntry(shared.Id);
                if (source != null)
                    ValidateTokens(shared.Key, source.LocalizedValue, value);
                russian.AddEntry(shared.Id, value).IsSmart = source != null && source.IsSmart;
            }
            if (translations.Count != collection.SharedData.Entries.Select(e => e.Key).Distinct().Count())
                throw new InvalidDataException("Russian source contains unknown keys.");
            LocalizationEditorSettings.SetPreloadTableFlag(russian, true);
            // Android's AAR writer can replace Cyrillic filename characters with '?',
            // which Gradle cannot extract on Windows. Keep bundle filenames in ASCII.
            ConfigureAddressableGroup(locale, russian, collection);
            EditorUtility.SetDirty(russian);
            EditorUtility.SetDirty(english);
            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(collection);

            var manifest = AssetDatabase.LoadAssetAtPath<TiltBrushManifest>("Assets/Manifest.asset");
            if (!manifest.Locales.Contains(locale))
                manifest.Locales = manifest.Locales.Concat(new[] { locale }).ToArray();
            EditorUtility.SetDirty(manifest);

            // Explicit overrides and saved choices still take priority. Device language does
            // not override Russian on a new installation. English remains the source/fallback.
            var selectors = LocalizationSettings.StartupLocaleSelectors;
            selectors.RemoveAll(s => s is SystemLocaleSelector || s is SpecificLocaleSelector);
            selectors.Add(new SpecificLocaleSelector { LocaleId = "ru" });
            EditorUtility.SetDirty(LocalizationSettings.Instance);

            CreateFont(translations.Values);
            AssetDatabase.SaveAssets();
            Validate();
        }

        static void ConfigureAddressableGroup(Locale locale, StringTable table, StringTableCollection collection)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(table));
            var group = settings.FindGroup("Localization-String-Tables-Russian");
            if (group == null)
            {
                group = settings.FindAssetEntry(guid).parentGroup;
                group.Name = "Localization-String-Tables-Russian";
            }
            // Localization refreshes group assignments before builds. Persist the rule as
            // well as the group name so that refresh cannot recreate a Cyrillic filename.
            var rules = AddressableGroupRules.Instance;
            if (!EditorUtility.IsPersistent(rules))
            {
                rules = ScriptableObject.CreateInstance<AddressableGroupRules>();
                AssetDatabase.CreateAsset(rules, kRoot + "Addressable Group Rules.asset");
            }
            rules.StringTablesResolver.AddLocaleGroup(locale.Identifier, group);
            AddressableGroupRules.Instance = rules;
            EditorUtility.SetDirty(rules);
            collection.RefreshAddressables();
            foreach (var empty in settings.groups.Where(g => g != null && g.entries.Count == 0 &&
                g.Name == "Localization-String-Tables-Русский").ToArray())
                settings.RemoveGroup(empty);
        }

        static string Normalize(string value) => Regex.Replace(value ?? "", @"\s+", " ").Trim();

        static void ImportNews(StringTable english,
            Dictionary<string, string> translations)
        {
            var news = JsonConvert.DeserializeObject<Dictionary<string, NewsTranslation>>(
                File.ReadAllText("Support/Localization/RussianNews.json"));
            var keysByText = new Dictionary<string, string>();
            foreach (var item in news)
            {
                english.AddEntry(item.Key, item.Value.English);
                translations.Add(item.Key, item.Value.Russian);
                keysByText[Normalize(item.Value.English)] = item.Key;
                keysByText[Normalize(item.Value.Russian)] = item.Key;
            }

            const string path = "Assets/Prefabs/Panels/WhatsNewPanel.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (!keysByText.TryGetValue(Normalize(text.text), out var key))
                        continue;
                    var localizer = text.GetComponent<LocalizeStringEvent>();
                    if (localizer == null)
                    {
                        localizer = text.gameObject.AddComponent<LocalizeStringEvent>();
                        var setter = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>),
                            text, typeof(TMP_Text).GetProperty(nameof(TMP_Text.text)).GetSetMethod());
                        UnityEventTools.AddPersistentListener(localizer.OnUpdateString, setter);
                    }
                    localizer.StringReference = new LocalizedString("Strings", key);
                }
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        static void AddSharedKey(SharedTableData shared, string key, long id)
        {
            if (shared.GetEntry(id) == null)
                shared.AddKey(key, id);
        }

        static void ValidateTokens(string key, string source, string translation)
        {
            foreach (var pattern in new[] { @"\{[^{}]+\}", @"<[^>]+>" })
            {
                var expected = Regex.Matches(source, pattern).Cast<Match>().Select(m => m.Value).OrderBy(x => x);
                var actual = Regex.Matches(translation, pattern).Cast<Match>().Select(m => m.Value).OrderBy(x => x);
                if (!expected.SequenceEqual(actual))
                    throw new InvalidDataException("Formatting tokens differ: " + key);
            }
        }

        static void CreateFont(IEnumerable<string> translations)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(kFontPath);
            if (font == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Roboto-Medium.ttf");
                font = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA,
                    1024, 1024, AtlasPopulationMode.Dynamic, false);
                if (font == null)
                    throw new InvalidOperationException("Could not create Cyrillic font.");
                font.name = "Roboto-Medium Cyrillic SDF";
                var characters = new string(translations.SelectMany(v => v).Concat(kAlphabet)
                    .Concat(Enumerable.Range(32, 95).Select(c => (char)c))
                    .Where(c => !char.IsControl(c)).Distinct().OrderBy(c => c).ToArray());
                if (!font.TryAddCharacters(characters, out string missing))
                    throw new InvalidOperationException("Missing font glyphs: " + missing);
                // Bake glyphs for Android; runtime font generation is unnecessary for Russian.
                font.atlasPopulationMode = AtlasPopulationMode.Static;
                AssetDatabase.CreateAsset(font, kFontPath);
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var texture in font.atlasTextures)
                    AssetDatabase.AddObjectToAsset(texture, font);
                EditorUtility.SetDirty(font);
            }

            foreach (var path in new[] {
                "Assets/Fonts/Oswald-Regular_0 SDF.asset",
                "Assets/Fonts/Oswald-Light SDF.asset",
                "Assets/Fonts/Oswald-Bold SDF.asset",
                "Assets/Fonts/RobotoMono-Regular SDF.asset"
            })
            {
                var owner = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (!owner.fallbackFontAssetTable.Contains(font))
                    owner.fallbackFontAssetTable.Insert(0, font);
                EditorUtility.SetDirty(owner);
            }
        }

        [MenuItem("Open Brush/Localization/Validate Russian translations")]
        public static void Validate()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection("Strings");
            var russian = (StringTable)collection.GetTable("ru");
            var english = (StringTable)collection.GetTable("en");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(kFontPath);
            if (russian == null || font == null)
                throw new InvalidDataException("Russian assets have not been imported.");
            foreach (var shared in collection.SharedData.Entries)
            {
                var value = russian.GetEntry(shared.Id)?.LocalizedValue;
                if (string.IsNullOrWhiteSpace(value))
                    throw new InvalidDataException("Missing Russian translation: " + shared.Key);
                var source = english.GetEntry(shared.Id);
                if (source != null)
                    ValidateTokens(shared.Key, source.LocalizedValue, value);
                foreach (var character in value.Where(c => c >= '\u0400' && c <= '\u04ff'))
                    if (!font.HasCharacter(character))
                        throw new InvalidDataException("Missing Cyrillic glyph: " + character);
            }
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            foreach (var asset in new UnityEngine.Object[] { russian, LocalizationEditorSettings.GetLocale("ru") })
            {
                var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
                if (settings.FindAssetEntry(guid) == null)
                    throw new InvalidDataException("Russian asset is missing from Addressables: " + asset.name);
            }
            var expectedGroup = AddressableGroupRules.Instance.StringTablesResolver.GetExpectedGroupName(
                new[] { russian.LocaleIdentifier }, russian, settings);
            if (expectedGroup.Any(c => c > 127))
                throw new InvalidDataException("Russian bundle group must have an ASCII name.");
            Debug.Log($"Russian localization validated: {russian.Count} entries, formatting tokens and Cyrillic glyphs.");
        }

        // Run with Unity -batchmode -buildTarget Android -executeMethod
        // TiltBrush.RussianLocalization.BuildAndroid -quit.
        public static void BuildAndroid()
        {
            Import();
            EditorUserBuildSettings.buildAppBundle = false;
            BuildTiltBrush.GuiSelectedSdk = XrSdkMode.OpenXR;
            BuildTiltBrush.GuiSelectedBuildTarget = BuildTarget.Android;
            // Rebuild the catalog so a previous local build cannot leave out Russian.
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetSettings.CleanPlayerContent(settings.ActivePlayerDataBuilder);
            AddressableAssetSettings.BuildPlayerContent(out var content);
            if (!string.IsNullOrEmpty(content.Error))
                throw new InvalidOperationException(content.Error);
            foreach (var path in Directory.GetFiles(UnityEngine.AddressableAssets.Addressables.BuildPath,
                "*.bundle", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(path).Any(c => c > 127 || c == '?'))
                    throw new InvalidDataException("Invalid Android bundle filename: " + path);
            }
            var previous = settings.BuildAddressablesWithPlayerBuild;
            try
            {
                // Content is already built above; avoid building it a second time.
                settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
                BuildTiltBrush.DoBuild(new BuildTiltBrush.TiltBuildOptions
                {
                    Target = BuildTarget.Android,
                    XrSdk = XrSdkMode.OpenXR,
                    Il2Cpp = true,
                    Location = "Builds/Russian/VRInclusion-Russian.apk",
                    Stamp = "ru",
                    UnityOptions = BuildOptions.None
                });
            }
            finally
            {
                settings.BuildAddressablesWithPlayerBuild = previous;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
