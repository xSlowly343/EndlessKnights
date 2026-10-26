namespace Game.Core.Content.Gems
{
    public sealed class SkillCondition
    {
        public double ResourceAtLeast { get; set; }
        public int ComboAbove { get; set; }
        public double BossLastSeconds { get; set; }
        public bool Boss { get; set; }
        public bool BossRetry { get; set; }
        public string? FamilyActive { get; set; }
        public double AfterComboStrikeSeconds { get; set; }
        public bool MouseMoving { get; set; }
        public bool Reading { get; set; }
        public double DotStacksAtLeast { get; set; }
        public int CursesAtLeast { get; set; }
        public int MinionsAtLeast { get; set; }
        public int AurasAtLeast { get; set; }
    }
}
