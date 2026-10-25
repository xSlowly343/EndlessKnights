using System;
using Game.Presentation.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Strip
{
    public sealed class FocusModeButton : MonoBehaviour
    {
        private static readonly Color FocusGlyph = new Color32(0xEE, 0xF2, 0xFF, 0xFF);
        private static readonly Color NoFocusGlyph = new Color32(0x73, 0xBF, 0xFF, 0xFF);
        private static readonly Color Border = new Color32(0x4A, 0x56, 0x77, 0xFF);

        private Image _glyph = null!;
        private Collider2D _collider = null!;

        public event Action? Clicked;

        public static FocusModeButton Create(Vector2 position)
        {
            var view = StripIconButtonView.Create("FocusModeButton", position, StripIcons.Focus, FocusGlyph, Border);
            var button = view.Root.gameObject.AddComponent<FocusModeButton>();
            button._glyph = view.Glyph;
            button._collider = view.Root.GetComponent<BoxCollider2D>();
            return button;
        }

        public Sprite Glyph => _glyph.sprite;

        public bool Contains(Vector2 worldPoint) => _collider.OverlapPoint(worldPoint);

        public void Click() => Clicked?.Invoke();

        public void ShowPassingClicks(bool passing)
        {
            _glyph.sprite = passing ? StripIcons.NoFocus : StripIcons.Focus;
            _glyph.color = passing ? NoFocusGlyph : FocusGlyph;
        }
    }
}
