namespace Game.Core.Sim
{
    public enum ForgeTarget
    {
        None,
        Weapon,
        Gear
    }

    public enum ForgeWork
    {
        Temper,
        Hone
    }

    public sealed class ForgeState
    {
        public ForgeWork Work { get; set; }
        public ForgeTarget Target { get; set; }
        public GearSlot Slot { get; set; }
        public double Hammers { get; set; }
        public double Heat { get; set; }
        public long Smelted { get; set; }
        public long Tempered { get; set; }
        public long SalvageOre { get; set; }
    }
}
