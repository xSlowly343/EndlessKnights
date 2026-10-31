namespace Game.Core.Sim
{
    public sealed class SlatePlacement
    {
        public SlatePlacement(SlateItem slate, int x, int y, int rotation)
        {
            Slate = slate;
            X = x;
            Y = y;
            Rotation = rotation;
        }

        public SlateItem Slate { get; }
        public int X { get; }
        public int Y { get; }
        public int Rotation { get; }
    }
}
