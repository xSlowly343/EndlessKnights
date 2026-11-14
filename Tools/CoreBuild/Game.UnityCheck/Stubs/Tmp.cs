using UnityEngine;
using UnityEngine.UI;
namespace TMPro
{
    public enum TextAlignmentOptions { TopLeft, Top, TopRight, Left, Center, Right, BottomLeft, Bottom, BottomRight, MidlineLeft, Midline, MidlineRight, BaselineLeft, Baseline, BaselineRight, CaplineLeft, Capline, CaplineRight }
    public enum TextOverflowModes { Overflow, Ellipsis, Masking, Truncate, ScrollRect, Page, Linked }
    public enum TextWrappingModes { NoWrap, Normal, PreserveWhitespace, PreserveWhitespaceNoWrap }
    [System.Flags] public enum FontStyles { Normal = 0, Bold = 1, Italic = 2, Underline = 4, UpperCase = 16 }
    public class TMP_FontAsset : ScriptableObject { public Material material; public Texture2D atlasTexture; public static TMP_FontAsset CreateFontAsset(Font font) => null; public static TMP_FontAsset CreateFontAsset(Font font, int a, int b, UnityEngine.TextCore.LowLevel.GlyphRenderMode m, int w, int h) => null; public bool TryAddCharacters(string s) => true; public bool TryAddCharacters(string s, out string missing) { missing = ""; return true; } public System.Collections.Generic.List<TMP_FontAsset> fallbackFontAssetTable; public bool HasCharacter(char c) => true; public bool HasCharacters(string s) => true; public UnityEngine.TextCore.FaceInfo faceInfo; public bool isMultiAtlasTexturesEnabled; public AtlasPopulationMode atlasPopulationMode; }
    public static class TMP_Settings { public static TMP_FontAsset defaultFontAsset; }
    public enum AtlasPopulationMode { Static, Dynamic, DynamicOS }
    public class TMP_TextInfo { public int characterCount; public int lineCount; }
    public abstract class TMP_Text : MaskableGraphic
    {
        public string text; public float fontSize; public float fontSizeMin; public float fontSizeMax; public bool enableAutoSizing; public TMP_FontAsset font;
        public TextAlignmentOptions alignment; public TextOverflowModes overflowMode; public TextWrappingModes textWrappingMode; public bool enableWordWrapping; public FontStyles fontStyle;
        public float lineSpacing; public float characterSpacing; public bool richText; public Vector4 margin; public float preferredWidth; public float preferredHeight; public float alpha; public Color32 outlineColor; public float outlineWidth;
        public Material fontMaterial; public Material fontSharedMaterial; public TMP_TextInfo textInfo; public int maxVisibleCharacters; public bool isTextTruncated; public Bounds textBounds; public int maxVisibleLines; public float paragraphSpacing; public float wordSpacing; public bool extraPadding;
        public void ForceMeshUpdate(bool a = false, bool b = false) {}
        public void SetText(string s) {} public void SetText(string s, float arg0) {}
        public Vector2 GetPreferredValues() => default; public Vector2 GetPreferredValues(string s) => default; public Vector2 GetPreferredValues(float w, float h) => default; public Vector2 GetPreferredValues(string s, float w, float h) => default;
    }
    public class TextMeshProUGUI : TMP_Text {}
    public class TextMeshPro : TMP_Text { public int sortingOrder; public int sortingLayerID; public Renderer renderer; }
}
