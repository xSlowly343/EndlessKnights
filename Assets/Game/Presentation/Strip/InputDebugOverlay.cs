using UnityEngine;

namespace Game.Presentation.Strip
{
    public sealed class InputDebugOverlay : MonoBehaviour
    {
        private const string BuiltinFontName = "LegacyRuntime.ttf";
        private const int FontSize = 48;
        private const float CharacterSize = 0.25f;

        private TextMesh _text = null!;
        private int _shownKeyDowns = -1;

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
            overlay.Show(0);
            return overlay;
        }

        public void Show(int totalKeyDowns)
        {
            if (totalKeyDowns == _shownKeyDowns)
                return;
            _shownKeyDowns = totalKeyDowns;
            _text.text = "keys " + totalKeyDowns;
        }
    }
}
