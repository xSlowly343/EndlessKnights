using System;
using UnityEngine;

namespace Game.Presentation.Strip
{
    public sealed class FocusModeButton : MonoBehaviour
    {
        private SpriteRenderer _renderer = null!;
        private Collider2D _collider = null!;

        public event Action? Clicked;

        public static FocusModeButton Create(Vector2 position, float size)
        {
            var icon = new GameObject("FocusModeButton");
            icon.transform.position = position;
            icon.transform.localScale = new Vector3(size, size, 1f);
            var button = icon.AddComponent<FocusModeButton>();
            button._renderer = icon.AddComponent<SpriteRenderer>();
            button._renderer.sprite = StripIcons.Focus;
            button._collider = icon.AddComponent<BoxCollider2D>();
            return button;
        }

        public bool Contains(Vector2 worldPoint) => _collider.OverlapPoint(worldPoint);

        public void Click() => Clicked?.Invoke();

        public void ShowNoFocus(bool noFocus)
        {
            _renderer.sprite = noFocus ? StripIcons.NoFocus : StripIcons.Focus;
        }
    }
}
