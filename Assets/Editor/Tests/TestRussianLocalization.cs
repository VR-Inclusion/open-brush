using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace TiltBrush
{
    public class TestRussianLocalization
    {
        public class LocaleSelectionProbe : LocalizationSettings
        {
            public UnityEngine.Localization.Locale Select() => SelectLocaleUsingStartupSelectors();
        }

        [Test]
        public void AllRussianEntriesHaveFormattingAndGlyphs()
        {
            RussianLocalization.Validate();
        }

        [TestCase(null, "ru")]
        [TestCase("en", "en")]
        [TestCase("fr", "fr")]
        [TestCase("ru", "ru")]
        [TestCase("unknown-language", "ru")]
        public void StartupUsesRussianUnlessAValidLanguageWasSaved(string saved, string expected)
        {
            // Exercise Unity's actual startup selection without changing the user's settings.
            var original = LocalizationSettings.Instance;
            var selectors = original.GetStartupLocaleSelectors().ToArray();
            var probe = ScriptableObject.CreateInstance<LocaleSelectionProbe>();
            var preference = "RussianLocalizationTest-" + Guid.NewGuid();
            try
            {
                var locales = new LocalesProvider();
                foreach (var locale in LocalizationEditorSettings.GetLocales())
                    locales.AddLocale(locale);
                probe.SetAvailableLocales(locales);
                probe.GetStartupLocaleSelectors().Clear();
                foreach (var selector in selectors)
                {
                    probe.GetStartupLocaleSelectors().Add(selector is PlayerPrefLocaleSelector
                        ? new PlayerPrefLocaleSelector { PlayerPreferenceKey = preference }
                        : selector);
                }
                if (saved != null)
                    PlayerPrefs.SetString(preference, saved);
                Assert.That(probe.Select().Identifier.Code, Is.EqualTo(expected));
            }
            finally
            {
                PlayerPrefs.DeleteKey(preference);
                UnityEngine.Object.DestroyImmediate(probe);
                LocalizationSettings.Instance = original;
            }
        }

        [TestCase("HumanBodyCategory", "Тело человека")]
        [TestCase("HumanBodyMale", "Мужская фигура")]
        [TestCase("HumanBodyFemale", "Женская фигура")]
        public void CustomEnvironmentsReferenceRussianNames(string assetName, string expected)
        {
            var environment = AssetDatabase.LoadAssetAtPath<Environment>(
                "Assets/Resources/Environments/HumanBody/" + assetName + ".asset");
            var russian = (StringTable)LocalizationEditorSettings.GetStringTableCollection("Strings").GetTable("ru");
            var id = environment.m_EnvironmentDescription.TableEntryReference.KeyId;
            Assert.That(russian.GetEntry(id).LocalizedValue, Is.EqualTo(expected));
            if (environment.m_IsCategory)
                Assert.That(environment.m_ChildEnvironments.Length, Is.EqualTo(2));
        }

        [TestCase("Oswald-Light SDF")]
        [TestCase("Oswald-Regular_0 SDF")]
        [TestCase("Oswald-Bold SDF")]
        [TestCase("RobotoMono-Regular SDF")]
        public void UiFontsResolveTheWholeRussianAlphabet(string name)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/" + name + ".asset");
            const string alphabet = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";
            Assert.That(font.HasCharacters(alphabet, out uint[] missing, true, false), Is.True,
                "Missing glyphs: " + string.Join(", ", missing ?? Array.Empty<uint>()));
        }

        [Test]
        public void RussianIsInTheLanguageMenu()
        {
            var manifest = AssetDatabase.LoadAssetAtPath<TiltBrushManifest>("Assets/Manifest.asset");
            Assert.That(manifest.Locales.Any(l => l.Identifier.Code == "ru"), Is.True);
        }
    }
}
