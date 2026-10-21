using UnityEngine;

namespace Game.Presentation.Strip
{
    public sealed class InputDebugOverlay : MonoBehaviour
    {
        private const string BuiltinFontName = "LegacyRuntime.ttf";
        private const int FontSize = 48;
        private const float CharacterSize = 0.25f;

        private TextMesh _text = null!;
        private (int Keys, int Enter, int Backspace, int Space, int MousePx) _shown = (-1, 0, 0, 0, 0);

        public static InputDebugOverlay Create(Vector2 position)
        {
            var host = new GameObject("InputDebugOverlay");
            host.transform.position = position;
            var text = host.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>(BuiltinFontName);
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.fontSize = FontSize;
            text.characterSize = CharacterSize;
            text.anchor = TextAnchor.LowerLeft;
            text.color = Color.white;
            var overlay = host.AddComponent<InputDebugOverlay>();
            overlay._text = text;
            overlay.Show(0, 0, 0, 0, 0);
            return overlay;
        }

        public void Show(int totalKeyDowns, int enterDowns, int backspaceDowns, int spaceDowns, int mousePx)
        {
            var counts = (totalKeyDowns, enterDowns, backspaceDowns, spaceDowns, mousePx);
            if (counts == _shown)
                return;
            _shown = counts;
            _text.text = $"keys {totalKeyDowns}  enter {enterDowns}  bksp {backspaceDowns}  space {spaceDowns}\nmouse {mousePx} px";
        }
    }
}
