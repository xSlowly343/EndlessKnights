namespace Game.Core.Sim
{
    public sealed class MountState
    {
        public int[] Levels { get; } = new int[Game.Core.Content.MountRules.Slots];
        public double Scan { get; set; }
        public long Cutters { get; set; }
    }
}
