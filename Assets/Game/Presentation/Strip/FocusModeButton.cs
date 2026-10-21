using System;
using UnityEngine;

namespace Game.Presentation.Strip
{
    public sealed class FocusModeButton : MonoBehaviour
    {
        private static readonly Color NormalColor = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color NoFocusColor = new Color(0.25f, 0.6f, 1f);

        private SpriteRenderer _renderer = null!;
        private Collider2D _collider = null!;

        public event Action? Clicked;

        public static FocusModeButton Create(Vector2 position, float size)
        {
            var icon = PlaceholderSquare.Create(NormalColor, size);
            icon.name = "FocusModeButton";
            icon.transform.position = position;
            var button = icon.AddComponent<FocusModeButton>();
            button._renderer = icon.GetComponent<SpriteRenderer>();
            button._collider = icon.GetComponent<BoxCollider2D>();
            return button;
        }

        public bool Contains(Vector2 worldPoint) => _collider.OverlapPoint(worldPoint);

        public void Click() => Clicked?.Invoke();

        public void ShowNoFocus(bool noFocus)
        {
            _renderer.color = noFocus ? NoFocusColor : NormalColor;
        }
    }
}
