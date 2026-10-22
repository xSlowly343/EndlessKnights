using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content.Tree;
using UnityEngine;

namespace Game.Presentation.Tree
{
    public enum TreeNodeLook
    {
        Normal,
        Offered,
        Taken,
        OwnStart,
        OtherStart,
        Locked
    }

    public sealed class TreeView : MonoBehaviour
    {
        public static readonly Vector2 WorldOffset = new Vector2(5000f, 5000f);

        private const float LinkWidth = 0.12f;
        private const float CameraDistance = 10f;
        private const int CircleTexturePx = 64;

        private static readonly Color32 NormalColor = new Color(0.09f, 0.09f, 0.11f);
        private static readonly Color32 OfferedColor = new Color(0.95f, 0.95f, 1f);
        private static readonly Color32 TakenColor = new Color(0.95f, 0.75f, 0.25f);
        private static readonly Color32 OwnStartColor = new Color(0.9f, 0.25f, 0.2f);
        private static readonly Color32 OtherStartColor = new Color(0.2f, 0.1f, 0.1f);
        private static readonly Color32 LockedColor = new Color(0.05f, 0.05f, 0.06f);
        private static readonly Color32 LinkDark = new Color(0.07f, 0.07f, 0.08f);
        private static readonly Color32 LinkOffered = new Color(0.6f, 0.6f, 0.66f);
        private static readonly Color BackgroundColor = new Color(0.07f, 0.06f, 0.07f, 1f);
        private static readonly Color SelectionColor = new Color(0.3f, 0.85f, 1f);

        private readonly List<(int From, int To)> _links = new List<(int From, int To)>();
        private IReadOnlyList<TreeNodeDef> _nodes = null!;
        private Mesh _nodeMesh = null!;
        private Mesh _linkMesh = null!;
        private Color32[] _nodeColors = null!;
        private Color32[] _linkColors = null!;
        private TreeNodeLook[] _looks = null!;
        private Camera _camera = null!;
        private SpriteRenderer _selection = null!;

        public static TreeView Create(PassiveTree tree, Transform parent)
        {
            var host = new GameObject("TreeView");
            host.transform.SetParent(parent, false);
            host.transform.position = WorldOffset;
            var view = host.AddComponent<TreeView>();
            view.Build(tree);
            return view;
        }

        public void ShowViewport(TreeViewport viewport, Rect screenRect)
        {
            _camera.rect = screenRect;
            _camera.orthographicSize = (float)(viewport.VisibleHeight / 2);
            _camera.transform.position = new Vector3(WorldOffset.x + (float)viewport.CentreX, WorldOffset.y + (float)viewport.CentreY, -CameraDistance);
        }

        public void Recolor(TreeNodeLook[] looks)
        {
            for (var i = 0; i < _nodes.Count; i++)
            {
                _looks[i] = looks[i];
                var color = ColorOf(looks[i]);
                for (var corner = 0; corner < 4; corner++)
                    _nodeColors[i * 4 + corner] = color;
            }
            for (var i = 0; i < _links.Count; i++)
            {
                var (from, to) = _links[i];
                var color = LinkColor(_looks[from], _looks[to]);
                for (var corner = 0; corner < 4; corner++)
                    _linkColors[i * 4 + corner] = color;
            }
            _nodeMesh.colors32 = _nodeColors;
            _linkMesh.colors32 = _linkColors;
        }

        public void Select(TreeNodeDef? node)
        {
            _selection.gameObject.SetActive(node != null);
            if (node == null)
                return;
            var size = Radius(node.Type) * 2.8f;
            _selection.transform.position = new Vector3(WorldOffset.x + (float)node.X, WorldOffset.y + (float)node.Y, 0f);
            _selection.transform.localScale = new Vector3(size, size, 1f);
        }

        public static float Radius(TreeNodeType type) => type switch
        {
            TreeNodeType.Notable => 0.55f,
            TreeNodeType.Keystone => 0.85f,
            TreeNodeType.Mastery => 0.6f,
            TreeNodeType.Jewel => 0.5f,
            TreeNodeType.Start => 1.1f,
            _ => 0.32f
        };

        private void Build(PassiveTree tree)
        {
            _nodes = tree.Nodes;
            _looks = new TreeNodeLook[_nodes.Count];
            var index = new Dictionary<TreeNodeDef, int>();
            for (var i = 0; i < _nodes.Count; i++)
                index[_nodes[i]] = i;
            for (var i = 0; i < _nodes.Count; i++)
                foreach (var neighbour in _nodes[i].Neighbours)
                    if (index.TryGetValue(neighbour, out var j) && j > i)
                        _links.Add((i, j));

            var circle = CircleSprite();
            var template = new GameObject("MaterialTemplate").AddComponent<SpriteRenderer>();
            var spriteMaterial = template.sharedMaterial;
            Destroy(template.gameObject);

            _linkMesh = BuildLinks();
            _linkColors = new Color32[_links.Count * 4];
            AddRenderer("Links", _linkMesh, new Material(spriteMaterial) { mainTexture = Texture2D.whiteTexture }, 0);
            _nodeMesh = BuildNodes();
            _nodeColors = new Color32[_nodes.Count * 4];
            AddRenderer("Nodes", _nodeMesh, new Material(spriteMaterial) { mainTexture = circle.texture }, 2);

            _selection = new GameObject("Selection").AddComponent<SpriteRenderer>();
            _selection.transform.SetParent(transform, false);
            _selection.sprite = circle;
            _selection.color = SelectionColor;
            _selection.sortingOrder = 1;
            _selection.gameObject.SetActive(false);

            _camera = new GameObject("TreeCamera").AddComponent<Camera>();
            _camera.transform.SetParent(transform, false);
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = BackgroundColor;
            _camera.depth = (Camera.main != null ? Camera.main.depth : 0f) + 1f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = CameraDistance * 2f;
        }

        private Mesh BuildNodes()
        {
            var vertices = new Vector3[_nodes.Count * 4];
            var uvs = new Vector2[_nodes.Count * 4];
            var triangles = new int[_nodes.Count * 6];
            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                var r = Radius(node.Type);
                var centre = new Vector3((float)node.X, (float)node.Y, 0f);
                AddQuad(vertices, uvs, triangles, i, centre + new Vector3(-r, -r), centre + new Vector3(-r, r), centre + new Vector3(r, r), centre + new Vector3(r, -r));
            }
            return NewMesh("TreeNodes", vertices, uvs, triangles);
        }

        private Mesh BuildLinks()
        {
            var vertices = new Vector3[_links.Count * 4];
            var uvs = new Vector2[_links.Count * 4];
            var triangles = new int[_links.Count * 6];
            for (var i = 0; i < _links.Count; i++)
            {
                var a = new Vector3((float)_nodes[_links[i].From].X, (float)_nodes[_links[i].From].Y, 0f);
                var b = new Vector3((float)_nodes[_links[i].To].X, (float)_nodes[_links[i].To].Y, 0f);
                var direction = (b - a).normalized;
                var side = new Vector3(-direction.y, direction.x, 0f) * (LinkWidth / 2f);
                AddQuad(vertices, uvs, triangles, i, a - side, a + side, b + side, b - side);
            }
            return NewMesh("TreeLinks", vertices, uvs, triangles);
        }

        private static void AddQuad(Vector3[] vertices, Vector2[] uvs, int[] triangles, int quad, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            var v = quad * 4;
            vertices[v] = p0;
            vertices[v + 1] = p1;
            vertices[v + 2] = p2;
            vertices[v + 3] = p3;
            uvs[v] = new Vector2(0f, 0f);
            uvs[v + 1] = new Vector2(0f, 1f);
            uvs[v + 2] = new Vector2(1f, 1f);
            uvs[v + 3] = new Vector2(1f, 0f);
            var t = quad * 6;
            triangles[t] = v;
            triangles[t + 1] = v + 1;
            triangles[t + 2] = v + 2;
            triangles[t + 3] = v;
            triangles[t + 4] = v + 2;
            triangles[t + 5] = v + 3;
        }

        private static Mesh NewMesh(string name, Vector3[] vertices, Vector2[] uvs, int[] triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.colors32 = new Color32[vertices.Length];
            mesh.RecalculateBounds();
            return mesh;
        }

        private void AddRenderer(string name, Mesh mesh, Material material, int sortingOrder)
        {
            var host = new GameObject(name);
            host.transform.SetParent(transform, false);
            host.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = host.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
        }

        private static Sprite CircleSprite()
        {
            var texture = new Texture2D(CircleTexturePx, CircleTexturePx, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var half = CircleTexturePx / 2f;
            for (var y = 0; y < CircleTexturePx; y++)
            {
                for (var x = 0; x < CircleTexturePx; x++)
                {
                    var distance = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) + (y + 0.5f - half) * (y + 0.5f - half));
                    var alpha = Mathf.Clamp01(half - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, CircleTexturePx, CircleTexturePx), new Vector2(0.5f, 0.5f), CircleTexturePx);
        }

        private static Color32 ColorOf(TreeNodeLook look) => look switch
        {
            TreeNodeLook.Offered => OfferedColor,
            TreeNodeLook.Taken => TakenColor,
            TreeNodeLook.OwnStart => OwnStartColor,
            TreeNodeLook.OtherStart => OtherStartColor,
            TreeNodeLook.Locked => LockedColor,
            _ => NormalColor
        };

        private static Color32 LinkColor(TreeNodeLook a, TreeNodeLook b)
        {
            var aTaken = a == TreeNodeLook.Taken || a == TreeNodeLook.OwnStart;
            var bTaken = b == TreeNodeLook.Taken || b == TreeNodeLook.OwnStart;
            if (aTaken && bTaken)
                return TakenColor;
            if (aTaken || bTaken)
                return LinkOffered;
            return LinkDark;
        }
    }
}
