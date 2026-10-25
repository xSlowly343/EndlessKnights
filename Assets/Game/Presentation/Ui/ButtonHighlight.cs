using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Ui
{
    public sealed class ButtonHighlight
    {
        private static readonly Color HoverTint = new Color(1f, 1f, 1f, 0.14f);
        private static readonly Color PressTint = new Color(0f, 0f, 0f, 0.22f);

        private readonly Image _overlay;
        private UiButton? _target;
        private bool _pressed;

        public ButtonHighlight(Transform parent)
        {
            _overlay = UiElements.Fill("ButtonHighlight", parent, HoverTint);
            _overlay.gameObject.SetActive(false);
        }

        public void Point(Vector2 worldPoint, bool held) => Show(ButtonAt(worldPoint), held);

        public void Clear() => Show(null, false);

        private static UiButton? ButtonAt(Vector2 worldPoint)
        {
            var buttons = UiButton.All;
            for (var i = buttons.Count - 1; i >= 0; i--)
            {
                var button = buttons[i];
                if (button.Back != null && button.Back.color.a > 0f && button.Contains(worldPoint))
                    return button;
            }
            return null;
        }

        private void Show(UiButton? target, bool pressed)
        {
            if (target == _target && pressed == _pressed)
                return;
            _target = target;
            _pressed = pressed;
            if (target == null)
            {
                _overlay.gameObject.SetActive(false);
                return;
            }
            var rect = _overlay.rectTransform;
            rect.SetParent(target.Rect, false);
            rect.SetAsLastSibling();
            UiElements.Stretch(rect);
            _overlay.sprite = target.Back.sprite;
            _overlay.type = target.Back.type;
            _overlay.pixelsPerUnitMultiplier = target.Back.pixelsPerUnitMultiplier;
            _overlay.color = pressed ? PressTint : HoverTint;
            _overlay.gameObject.SetActive(true);
        }
    }
}
