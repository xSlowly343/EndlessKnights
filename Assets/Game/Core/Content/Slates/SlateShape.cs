using System.Collections.Generic;

namespace Game.Core.Content.Slates
{
    public sealed class SlateShape
    {
        public const int Rotations = 4;

        private readonly (int X, int Y)[][] _rotated;

        public SlateShape(string id, IReadOnlyList<(int X, int Y)> cells, int lines, double power)
        {
            Id = id;
            Lines = lines;
            Power = power;
            _rotated = new (int X, int Y)[Rotations][];
            for (var rotation = 0; rotation < Rotations; rotation++)
                _rotated[rotation] = Rotate(cells, rotation);
        }

        public string Id { get; }
        public int Lines { get; }
        public double Power { get; }
        public int Size => _rotated[0].Length;

        public IReadOnlyList<(int X, int Y)> Cells(int rotation) => _rotated[((rotation % Rotations) + Rotations) % Rotations];

        private static (int X, int Y)[] Rotate(IReadOnlyList<(int X, int Y)> cells, int rotation)
        {
            var turned = new (int X, int Y)[cells.Count];
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            for (var i = 0; i < cells.Count; i++)
            {
                var (x, y) = cells[i];
                for (var turn = 0; turn < rotation; turn++)
                    (x, y) = (-y, x);
                turned[i] = (x, y);
                minX = System.Math.Min(minX, x);
                minY = System.Math.Min(minY, y);
            }
            for (var i = 0; i < turned.Length; i++)
                turned[i] = (turned[i].X - minX, turned[i].Y - minY);
            return turned;
        }
    }
}
