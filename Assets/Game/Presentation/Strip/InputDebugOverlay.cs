using Game.Core.Input;
using UnityEngine;

namespace Game.Presentation.Strip
{
    public sealed class InputDebugOverlay : MonoBehaviour
    {
        private const string BuiltinFontName = "LegacyRuntime.ttf";
        private const int FontSize = 48;
        private const float CharacterSize = 0.25f;

        private TextMesh _text = null!;
        private InputTotals _shown;

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
            overlay.Render(default);
            return overlay;
        }

        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        public void Show(InputTotals totals)
        {
            if (totals.Equals(_shown))
                return;
            Render(totals);
        }

        private void Render(InputTotals totals)
        {
            _shown = totals;
            _text.text =
                $"keys {totals.Keys}  enter {totals.Enter}  bksp {totals.Backspace}  space {totals.Space}\n" +
                $"mouse {totals.MousePx} px  flicks {totals.Flicks}  circles {totals.Circles}\n" +
                $"clicks L{totals.LeftClicks} R{totals.RightClicks} M{totals.MiddleClicks}  wheel {totals.WheelTicks}  rhythm {totals.RhythmCv:0.00}" + (totals.Suppressed ? "  PAUSED" : string.Empty);
        }
    }
}
