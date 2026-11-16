using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;

namespace TiltBrush
{
    public class TestRussianLocalizationRender
    {
        [Test]
        public void NewsTextBindingsUpdateTheVisibleText()
        {
            var russian = (StringTable)LocalizationEditorSettings.GetStringTableCollection("Strings").GetTable("ru");
            var prefab = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Panels/WhatsNewPanel.prefab");
            try
            {
                var count = 0;
                foreach (var localizer in prefab.GetComponentsInChildren<LocalizeStringEvent>(true))
                {
                    var text = localizer.GetComponent<TMP_Text>();
                    var key = localizer.StringReference.TableEntryReference.Key;
                    if (text == null || key == null || !key.StartsWith("WHATSNEW_")) continue;
                    var translation = russian.GetEntry(key).LocalizedValue;
                    // Production bindings run in Play Mode. Enable them for this isolated
                    // Edit Mode prefab instance without changing the saved prefab.
                    for (var i = 0; i < localizer.OnUpdateString.GetPersistentEventCount(); ++i)
                        localizer.OnUpdateString.SetPersistentListenerState(i,
                            UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
                    localizer.OnUpdateString.Invoke(translation);
                    Assert.That(text.text, Is.EqualTo(translation), key);
                    ++count;
                }
                Assert.That(count, Is.EqualTo(27));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        [Test]
        public void CyrillicRendersThroughTheUiFontFallback()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var previous = RenderTexture.active;
            RenderTexture target = null;
            Texture2D image = null;
            try
            {
                var cameraObject = new GameObject("Russian localization preview camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene;
                camera.orthographic = true;
                camera.orthographicSize = 5;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.035f, 0.05f, 0.075f);

                var textObject = new GameObject("Russian UI sample");
                SceneManager.MoveGameObjectToScene(textObject, scene);
                var text = textObject.AddComponent<TextMeshPro>();
                text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Oswald-Light SDF.asset");
                text.fontSize = 5;
                text.color = Color.white;
                text.enableWordWrapping = false;
                text.alignment = TextAlignmentOptions.TopLeft;
                text.rectTransform.pivot = new Vector2(0, 1);
                text.rectTransform.sizeDelta = new Vector2(14, 8);
                text.transform.position = new Vector3(-7, 4, 0);
                text.text = "Русский язык / VRInclusion\n\nНастройки    Язык    Кисти\nТело человека\nМужская фигура    Женская фигура\nСохранить эскиз\nНажмите курок для подтверждения\n\nАБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ\nабвгдеёжзийклмнопрстуфхцчшщъыьэюя";
                text.ForceMeshUpdate(true, true);
                for (var i = 0; i < text.textInfo.characterCount; ++i)
                {
                    var character = text.textInfo.characterInfo[i];
                    if (character.character >= '\u0400' && character.character <= '\u04ff')
                        Assert.That(character.textElement.unicode, Is.EqualTo((uint)character.character));
                }

                target = new RenderTexture(1440, 900, 24);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(1440, 900, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/RussianLocalization-Preview.png", image.EncodeToPNG());
                camera.targetTexture = null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (image != null) Object.DestroyImmediate(image);
                if (target != null) Object.DestroyImmediate(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
