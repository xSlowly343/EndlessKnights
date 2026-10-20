using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Strip
{
    public sealed class FocusModeButton : MonoBehaviour
    {
        private static readonly Color NormalColor = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color NoFocusColor = new Color(0.25f, 0.6f, 1f);

        private SpriteRenderer _renderer = null!;
        private Collider2D _collider = null!;
        private Camera _camera = null!;

        public event Action? Clicked;

        public static FocusModeButton Create(Camera camera, Vector2 position, float size)
        {
            var icon = PlaceholderSquare.Create(NormalColor, size);
            icon.name = "FocusModeButton";
            icon.transform.position = position;
            var button = icon.AddComponent<FocusModeButton>();
            button._renderer = icon.GetComponent<SpriteRenderer>();
            button._collider = icon.AddComponent<BoxCollider2D>();
            button._camera = camera;
            return button;
        }

        public void ShowNoFocus(bool noFocus)
        {
            _renderer.color = noFocus ? NoFocusColor : NormalColor;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;

            Vector2 worldPoint = _camera.ScreenToWorldPoint(mouse.position.ReadValue());
            if (_collider.OverlapPoint(worldPoint))
                Clicked?.Invoke();
        }
    }
}
