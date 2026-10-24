using System.Collections.Generic;
using System.IO;
using Game.Presentation.Ui;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Game.Editor
{
    public static class UiAssets
    {
        private const string EssentialsPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string FontsFolder = "Assets/Game/Art/Fonts";
        private const string ThemeFolder = "Assets/Game/Art/Resources/Ui";
        private const int SamplingPointSize = 64;
        private const int AtlasPadding = 6;
        private const int AtlasSize = 1024;

        [MenuItem("Game/Create UI Assets")]
        public static void Create()
        {
            if (!HasTmpEssentials())
                throw new IOException($"TMP Essential Resources are missing: import {EssentialsPackage} (Window > TextMeshPro > Import TMP Essential Resources).");
            var text = FontAsset("Rubik-Regular");
            var bold = FontAsset("Rubik-Bold");
            var numbers = FontAsset("LilitaOne-Regular");
            numbers.fallbackFontAssetTable = new List<TMP_FontAsset> { text };
            bold.fallbackFontAssetTable = new List<TMP_FontAsset> { text };
            EditorUtility.SetDirty(numbers);
            EditorUtility.SetDirty(bold);
            var theme = Theme();
            theme.UseFonts(text, bold, numbers);
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            Debug.Log("UI assets are ready: " + AssetDatabase.GetAssetPath(theme));
        }

        private static bool HasTmpEssentials() => AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath) != null;

        private static TMP_FontAsset FontAsset(string fontName)
        {
            var path = $"{FontsFolder}/{fontName} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null)
                return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontsFolder}/{fontName}.ttf");
            if (font == null)
                throw new FileNotFoundException("Font not found", $"{FontsFolder}/{fontName}.ttf");
            var asset = TMP_FontAsset.CreateFontAsset(font, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            asset.name = fontName + " SDF";
            AssetDatabase.CreateAsset(asset, path);
            var atlas = asset.atlasTextures[0];
            atlas.name = fontName + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);
            asset.material.name = fontName + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            return asset;
        }

        private static UiTheme Theme()
        {
            var path = ThemeFolder + "/UiTheme.asset";
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(path);
            if (theme != null)
                return theme;
            Directory.CreateDirectory(ThemeFolder);
            AssetDatabase.Refresh();
            theme = ScriptableObject.CreateInstance<UiTheme>();
            AssetDatabase.CreateAsset(theme, path);
            return theme;
        }
    }
}
