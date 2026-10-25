using System.Collections.Generic;
using Game.Core.Content.Tree;

namespace Game.Core.Build
{
    public sealed class TreeViewport
    {
        public const double MinVisibleHeight = 3.5;
        public const double ZoomStep = 1.5;
        private const double FitMargin = 1.05;

        private readonly double _minX;
        private readonly double _maxX;
        private readonly double _minY;
        private readonly double _maxY;
        private double _maxVisibleHeight;
        private double _closestHeight = MinVisibleHeight;

        public TreeViewport(IReadOnlyList<TreeNodeDef> nodes, double aspect)
        {
            Aspect = aspect;
            _minX = double.MaxValue;
            _minY = double.MaxValue;
            _maxX = double.MinValue;
            _maxY = double.MinValue;
            foreach (var node in nodes)
            {
                _minX = System.Math.Min(_minX, node.X);
                _maxX = System.Math.Max(_maxX, node.X);
                _minY = System.Math.Min(_minY, node.Y);
                _maxY = System.Math.Max(_maxY, node.Y);
            }
            if (nodes.Count == 0)
                _minX = _maxX = _minY = _maxY = 0;
            _maxVisibleHeight = FitHeight(aspect);
            VisibleHeight = _maxVisibleHeight;
            CentreX = (_minX + _maxX) / 2;
            CentreY = (_minY + _maxY) / 2;
        }

        public double Aspect { get; private set; }
        public double CentreX { get; private set; }
        public double CentreY { get; private set; }
        public double VisibleHeight { get; private set; }
        public double VisibleWidth => VisibleHeight * Aspect;

        public (double X, double Y) ToTree(double u, double v) =>
            (CentreX + (u - 0.5) * VisibleWidth, CentreY + (v - 0.5) * VisibleHeight);

        public void Pan(double du, double dv)
        {
            CentreX = Clamp(CentreX - du * VisibleWidth, _minX, _maxX);
            CentreY = Clamp(CentreY - dv * VisibleHeight, _minY, _maxY);
        }

        public double ClosestHeight => System.Math.Min(_closestHeight, _maxVisibleHeight);

        public void ZoomIn() => VisibleHeight = Clamp(VisibleHeight / ZoomStep, ClosestHeight, _maxVisibleHeight);

        public void ZoomOut() => VisibleHeight = Clamp(VisibleHeight * ZoomStep, ClosestHeight, _maxVisibleHeight);

        public void LimitZoom(double closestHeight)
        {
            var wasClosest = VisibleHeight <= ClosestHeight;
            _closestHeight = System.Math.Max(MinVisibleHeight, closestHeight);
            VisibleHeight = Clamp(wasClosest ? ClosestHeight : VisibleHeight, ClosestHeight, _maxVisibleHeight);
        }

        public void FocusOn(double x, double y, double visibleHeight)
        {
            CentreX = Clamp(x, _minX, _maxX);
            CentreY = Clamp(y, _minY, _maxY);
            VisibleHeight = Clamp(visibleHeight, ClosestHeight, _maxVisibleHeight);
        }

        public void Resize(double aspect)
        {
            Aspect = aspect;
            _maxVisibleHeight = FitHeight(aspect);
            VisibleHeight = Clamp(VisibleHeight, ClosestHeight, _maxVisibleHeight);
        }

        public static TreeNodeDef? NodeAt(IReadOnlyList<TreeNodeDef> nodes, double x, double y, double radius)
        {
            TreeNodeDef? best = null;
            var bestDistance = radius * radius;
            foreach (var node in nodes)
            {
                var dx = node.X - x;
                var dy = node.Y - y;
                var distance = dx * dx + dy * dy;
                if (distance > bestDistance)
                    continue;
                bestDistance = distance;
                best = node;
            }
            return best;
        }

        private double FitHeight(double aspect) =>
            System.Math.Max(MinVisibleHeight, System.Math.Max(_maxY - _minY, (_maxX - _minX) / aspect) * FitMargin);

        private static double Clamp(double value, double min, double max) => System.Math.Max(min, System.Math.Min(max, value));
    }
}
