using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Ui
{
    public sealed class TreeMesh : MaskableGraphic
    {
        private Vector2[] _corners = System.Array.Empty<Vector2>();
        private Vector2[] _uvs = System.Array.Empty<Vector2>();
        private Color32[] _colors = System.Array.Empty<Color32>();
        private Texture? _texture;

        public override Texture mainTexture => _texture != null ? _texture : s_WhiteTexture;

        public int QuadCount => _colors.Length;

        public static TreeMesh Create(string name, RectTransform parent, Texture? texture, int quads)
        {
            var rect = UiElements.Rect(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = parent.pivot;
            rect.gameObject.AddComponent<CanvasRenderer>();
            var mesh = rect.gameObject.AddComponent<TreeMesh>();
            mesh.raycastTarget = false;
            mesh._texture = texture;
            mesh._corners = new Vector2[quads * 4];
            mesh._uvs = new Vector2[quads * 4];
            mesh._colors = new Color32[quads];
            return mesh;
        }

        public Color32 ColorOf(int quad) => _colors[quad];

        public void SetQuad(int quad, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Rect uv)
        {
            var v = quad * 4;
            _corners[v] = p0;
            _corners[v + 1] = p1;
            _corners[v + 2] = p2;
            _corners[v + 3] = p3;
            _uvs[v] = new Vector2(uv.xMin, uv.yMin);
            _uvs[v + 1] = new Vector2(uv.xMin, uv.yMax);
            _uvs[v + 2] = new Vector2(uv.xMax, uv.yMax);
            _uvs[v + 3] = new Vector2(uv.xMax, uv.yMin);
        }

        public void SetSquare(int quad, Vector2 centre, float half, Rect uv) =>
            SetQuad(quad, centre + new Vector2(-half, -half), centre + new Vector2(-half, half), centre + new Vector2(half, half), centre + new Vector2(half, -half), uv);

        public void SetColor(int quad, Color32 color) => _colors[quad] = color;

        public void Apply() => SetVerticesDirty();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var vertex = UIVertex.simpleVert;
            for (var i = 0; i < _corners.Length; i++)
            {
                vertex.position = _corners[i];
                vertex.uv0 = _uvs[i];
                vertex.color = _colors[i / 4];
                vh.AddVert(vertex);
            }
            for (var v = 0; v < _corners.Length; v += 4)
            {
                vh.AddTriangle(v, v + 1, v + 2);
                vh.AddTriangle(v, v + 2, v + 3);
            }
        }
    }
}
