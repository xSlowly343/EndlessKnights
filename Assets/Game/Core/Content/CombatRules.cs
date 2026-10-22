namespace Game.Core.Content
{
    public sealed class CombatRules
    {
        public const string FileName = "combat.json";
        public const int SupportedVersion = 1;

        private CombatRules(double keyDamage, double clickDamage, double mouseDamage, double mousePxPerHit, FocusRules focus, EnergyRules energy, double critChance, double critMultiplier, ClassRules classes, OfflineRules offline, HeroRules hero)
        {
            KeyDamage = keyDamage;
            ClickDamage = clickDamage;
            MouseDamage = mouseDamage;
            MousePxPerHit = mousePxPerHit;
            Focus = focus;
            Energy = energy;
            CritChance = critChance;
            CritMultiplier = critMultiplier;
            Classes = classes;
            Offline = offline;
            Hero = hero;
        }

        public double KeyDamage { get; }
        public double ClickDamage { get; }
        public double MouseDamage { get; }
        public double MousePxPerHit { get; }
        public FocusRules Focus { get; }
        public EnergyRules Energy { get; }
        public double CritChance { get; }
        public double CritMultiplier { get; }
        public ClassRules Classes { get; }
        public OfflineRules Offline { get; }
        public HeroRules Hero { get; }

        public static CombatRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var hits = root.Field("hits");
            var mouse = hits.Field("mouse");
            var crit = root.Field("crit");
            return new CombatRules(
                hits.Field("keyboard").Field("damage").AsNumber(0, 1e6),
                hits.Field("click").Field("damage").AsNumber(0, 1e6),
                mouse.Field("damage").AsNumber(0, 1e6),
                mouse.Field("pxPerHit").AsNumber(1, 1e6),
                FocusRules.Read(root.Field("focus")),
                EnergyRules.Read(root.Field("energy")),
                crit.Field("chance").AsNumber(0, 1),
                crit.Field("multiplier").AsNumber(1, 1000),
                ClassRules.Read(root.Field("classes")),
                OfflineRules.Read(root.Field("offline")),
                HeroRules.Read(root.Field("hero")));
        }
    }
}
