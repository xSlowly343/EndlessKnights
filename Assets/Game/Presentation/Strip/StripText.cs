using UnityEngine;

namespace Game.Presentation.Strip
{
    public static class StripText
    {
        private const string BuiltinFontName = "LegacyRuntime.ttf";
        private const int FontSize = 48;

        public static TextMesh Create(string name, string text, Vector2 position, float characterSize, TextAnchor anchor, Color color, Transform? parent = null, int sortingOrder = 10)
        {
            var host = new GameObject(name);
            if (parent != null)
                host.transform.SetParent(parent, false);
            host.transform.position = position;
            var mesh = host.AddComponent<TextMesh>();
            mesh.font = Resources.GetBuiltinResource<Font>(BuiltinFontName);
            var renderer = mesh.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mesh.font.material;
            renderer.sortingOrder = sortingOrder;
            mesh.fontSize = FontSize;
            mesh.characterSize = characterSize;
            mesh.anchor = anchor;
            mesh.color = color;
            mesh.text = text;
            return mesh;
        }
    }
}
