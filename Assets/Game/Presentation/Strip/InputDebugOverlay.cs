using Game.Core.Input;
using Game.Presentation.Ui;
using TMPro;
using UnityEngine;

namespace Game.Presentation.Strip
{
    public sealed class InputDebugOverlay : MonoBehaviour
    {
        private const int SortingOrder = 40;
        private const float WidthPx = 470f;
        private const float HeightPx = 48f;
        private const float FontSizePx = 10f;

        private TextMeshProUGUI _text = null!;
        private InputTotals _shown;

        public string Text => _text.text;

        public static InputDebugOverlay Create(Vector2 position)
        {
            var host = new GameObject("InputDebugOverlay", typeof(RectTransform));
            var rect = (RectTransform)host.transform;
            var canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = SortingOrder;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(WidthPx, HeightPx);
            rect.localScale = Vector3.one / StripSpace.PixelsPerUnit;
            rect.position = new Vector3(position.x, position.y, 0f);
            var text = UiElements.Label("Text", rect, UiTheme.Load().TextFont, FontSizePx, Color.white, TextAlignmentOptions.BottomLeft);
            UiElements.Stretch(text.rectTransform);
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
