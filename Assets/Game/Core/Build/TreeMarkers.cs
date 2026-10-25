using System.Collections.Generic;
using Game.Core.Content.Tree;

namespace Game.Core.Build
{
    public sealed class TreeMarkers
    {
        public const int StartPriority = 128;
        public const int HighlightPriority = 64;
        public const int OfferedPriority = 32;
        public const int TypePriorityStep = 4;
        public const int TakenPriority = 2;
        private const int IndexBits = 20;
        private const long IndexMask = (1L << IndexBits) - 1;
        private const int MaxPriority = 1 << 10;

        private readonly double[] _x;
        private readonly double[] _y;
        private readonly long[] _order;
        private readonly int[] _next;
        private readonly bool[] _shown;
        private readonly Dictionary<long, int> _heads = new Dictionary<long, int>();

        public TreeMarkers(IReadOnlyList<TreeNodeDef> nodes)
        {
            _x = new double[nodes.Count];
            _y = new double[nodes.Count];
            for (var i = 0; i < nodes.Count; i++)
            {
                _x[i] = nodes[i].X;
                _y[i] = nodes[i].Y;
            }
            _order = new long[nodes.Count];
            _next = new int[nodes.Count];
            _shown = new bool[nodes.Count];
        }

        public int Count => _shown.Length;

        public bool IsShown(int index) => _shown[index];

        public static int PriorityOf(TreeNodeType type, bool highlighted, bool offered, bool taken) =>
            (type == TreeNodeType.Start ? StartPriority : 0) + (highlighted ? HighlightPriority : 0) + (offered ? OfferedPriority : 0) + TypeRank(type) * TypePriorityStep + (taken ? TakenPriority : 0);

        public static int TypeRank(TreeNodeType type) => type switch
        {
            TreeNodeType.Start or TreeNodeType.Keystone => 4,
            TreeNodeType.Notable => 3,
            TreeNodeType.Small => 1,
            _ => 0
        };

        public void Pick(IReadOnlyList<double> halves, IReadOnlyList<int> priorities, double gap)
        {
            var count = _shown.Length;
            var largest = 0.0;
            for (var i = 0; i < count; i++)
            {
                largest = System.Math.Max(largest, halves[i]);
                var priority = System.Math.Max(0, System.Math.Min(MaxPriority - 1, priorities[i]));
                _order[i] = ((long)(MaxPriority - 1 - priority) << IndexBits) | (uint)i;
                _shown[i] = false;
            }
            System.Array.Sort(_order);
            var cell = System.Math.Max(1e-6, 2 * largest + gap);
            _heads.Clear();
            for (var k = 0; k < count; k++)
            {
                var i = (int)(_order[k] & IndexMask);
                var cx = CellOf(_x[i], cell);
                var cy = CellOf(_y[i], cell);
                if (Collides(i, cx, cy, halves, gap))
                    continue;
                _shown[i] = true;
                var key = KeyOf(cx, cy);
                _next[i] = _heads.TryGetValue(key, out var head) ? head : -1;
                _heads[key] = i;
            }
        }

        private bool Collides(int i, long cx, long cy, IReadOnlyList<double> halves, double gap)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (!_heads.TryGetValue(KeyOf(cx + dx, cy + dy), out var j))
                        continue;
                    for (; j >= 0; j = _next[j])
                    {
                        var reach = halves[i] + halves[j] + gap;
                        var ox = _x[i] - _x[j];
                        var oy = _y[i] - _y[j];
                        if (ox * ox + oy * oy < reach * reach)
                            return true;
                    }
                }
            }
            return false;
        }

        private static long CellOf(double value, double cell) => (long)System.Math.Floor(value / cell);

        private static long KeyOf(long cx, long cy) => (cx << 32) ^ (cy & 0xFFFFFFFFL);
    }
}
