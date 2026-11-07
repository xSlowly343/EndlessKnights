namespace Game.Core.Content
{
    public sealed class CombatRules
    {
        public const string FileName = "combat.json";
        public const int SupportedVersion = 2;

        private CombatRules(double keyDamage, double mouseDamage, double mousePxPerHit, FocusRules focus, ResourceRules resources, double critChance, double critMultiplier, ClassRules classes, OfflineRules offline, HeroRules hero, StrikeUpgradeRules strikeUpgrade, ConditionRules conditions, CurseRules curses, DotRules dots, MinionRules skeletons, MinionRules beasts, PathRules paths, EncounterRules encounters)
        {
            Paths = paths;
            Encounters = encounters;
            Skeletons = skeletons;
            Beasts = beasts;
            Dots = dots;
            StrikeDamage = keyDamage;
            MouseDamage = mouseDamage;
            MousePxPerHit = mousePxPerHit;
            Focus = focus;
            Resources = resources;
            CritChance = critChance;
            CritMultiplier = critMultiplier;
            Classes = classes;
            Offline = offline;
            Hero = hero;
            StrikeUpgrade = strikeUpgrade;
            Conditions = conditions;
            Curses = curses;
        }

        public double StrikeDamage { get; }
        public double MouseDamage { get; }
        public double MousePxPerHit { get; }
        public FocusRules Focus { get; }
        public ResourceRules Resources { get; }
        public double CritChance { get; }
        public double CritMultiplier { get; }
        public ClassRules Classes { get; }
        public OfflineRules Offline { get; }
        public HeroRules Hero { get; }
        public StrikeUpgradeRules StrikeUpgrade { get; }
        public ConditionRules Conditions { get; }
        public CurseRules Curses { get; }
        public DotRules Dots { get; }
        public MinionRules Skeletons { get; }
        public MinionRules Beasts { get; }
        public PathRules Paths { get; }
        public EncounterRules Encounters { get; }
        public double BasePierce { get; private set; }
        public PierceRules Pierce { get; private set; } = new PierceRules();
        public AttackRules Attack { get; private set; } = new AttackRules();
        public MountRules Mount { get; private set; } = new MountRules();
        public FacetRules Facets { get; private set; } = new FacetRules();
        public FractureRules Fracture { get; private set; } = new FractureRules();
        public DefenseRules Defense { get; private set; } = new DefenseRules();
        public ElementRules Elements { get; private set; } = new ElementRules();
        public TempoRules Tempo { get; private set; } = new TempoRules();

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
                hits.Field("strike").Field("damage").AsNumber(0, 1e6),
                mouse.Field("damage").AsNumber(0, 1e6),
                mouse.Field("pxPerHit").AsNumber(1, 1e6),
                FocusRules.Read(root.Field("focus")),
                ResourceRules.Read(root.Field("resources")),
                crit.Field("chance").AsNumber(0, 1),
                crit.Field("multiplier").AsNumber(1, 1000),
                ClassRules.Read(root.Field("classes")),
                OfflineRules.Read(root.Field("offline")),
                HeroRules.Read(root.Field("hero")),
                StrikeUpgradeRules.Read(root.Field("strikeUpgrade")),
                ConditionRules.Read(root.Field("conditions")),
                CurseRules.Read(root.Field("curses")),
                DotRules.Read(root.Field("dots")),
                MinionRules.Read(root.Field("skeletons")),
                MinionRules.Read(root.Field("beasts")),
                PathRules.Read(root.Field("paths")),
                EncounterRules.Read(root.Field("encounters")))
            {
                Attack = AttackRules.Read(root.Field("attack")),
                Mount = MountRules.Read(root.Field("mount")),
                Facets = FacetRules.Read(root.Field("facets")),
                Fracture = FractureRules.Read(root.Field("fracture")),
                Defense = DefenseRules.Read(root.Field("defense")),
                Elements = ElementRules.Read(root.Field("elements")),
                Tempo = TempoRules.Read(root.Field("tempo")),
                BasePierce = root.HasField("pierce") ? root.Field("pierce").Field("baseShare").AsNumber(0, 1) : 0,
                Pierce = root.HasField("pierce") ? PierceRules.Read(root.Field("pierce")) : new PierceRules()
            };
        }
    }
}
