using System;
using UnityEngine;

namespace Game.Presentation.Strip
{
    public sealed class StripButton : MonoBehaviour
    {
        private Collider2D _collider = null!;

        public event Action? Clicked;

        public static StripButton Create(string name, Color color, Vector2 position, Vector2 size, Transform? parent = null)
        {
            var sprite = PlaceholderSquare.Create(color, 1f);
            sprite.name = name;
            if (parent != null)
                sprite.transform.SetParent(parent, false);
            sprite.transform.position = new Vector3(position.x, position.y, sprite.transform.position.z);
            sprite.transform.localScale = new Vector3(size.x, size.y, 1f);
            var button = sprite.AddComponent<StripButton>();
            button._collider = sprite.GetComponent<BoxCollider2D>();
            return button;
        }

        public static StripButton CreateIcon(string name, Sprite icon, Vector2 position, float size)
        {
            var host = new GameObject(name);
            host.transform.position = position;
            host.transform.localScale = new Vector3(size, size, 1f);
            host.AddComponent<SpriteRenderer>().sprite = icon;
            var button = host.AddComponent<StripButton>();
            button._collider = host.AddComponent<BoxCollider2D>();
            return button;
        }

        public bool Contains(Vector2 worldPoint) => isActiveAndEnabled && _collider.OverlapPoint(worldPoint);

        public void Click() => Clicked?.Invoke();
    }
}
