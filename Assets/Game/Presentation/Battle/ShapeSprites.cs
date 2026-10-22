using UnityEngine;

namespace Game.Presentation.Battle
{
    public static class ShapeSprites
    {
        private const int CircleResolution = 64;

        private static Sprite? _square;
        private static Sprite? _circle;

        public static Sprite Square => _square ??= CreateSquare();
        public static Sprite Circle => _circle ??= CreateCircle();

        private static Sprite CreateSquare()
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        private static Sprite CreateCircle()
        {
            var texture = new Texture2D(CircleResolution, CircleResolution) { filterMode = FilterMode.Bilinear };
            var radius = CircleResolution / 2f;
            for (var y = 0; y < CircleResolution; y++)
            {
                for (var x = 0; x < CircleResolution; x++)
                {
                    var dx = x + 0.5f - radius;
                    var dy = y + 0.5f - radius;
                    var inside = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, inside));
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, CircleResolution, CircleResolution), new Vector2(0.5f, 0.5f), CircleResolution);
        }

        public static SpriteRenderer Create(string name, Sprite sprite, Color color, Vector2 position, Vector2 size, int sortingOrder, Transform? parent = null)
        {
            var host = new GameObject(name);
            if (parent != null)
                host.transform.SetParent(parent, false);
            host.transform.position = position;
            host.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = host.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }
    }
}
