using UnityEngine;

namespace Game.Presentation.Strip
{
    public static class PlaceholderSquare
    {
        private const float PixelsPerUnit = 1f;

        public static GameObject Create(Color color, float size)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            var square = new GameObject("PlaceholderSquare");
            var renderer = square.AddComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            renderer.color = color;
            square.transform.localScale = new Vector3(size, size, 1f);
            return square;
        }
    }
}
