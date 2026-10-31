using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Gems
{
    public sealed class GemDef
    {
        private readonly IReadOnlyList<SkillDef>? _variants;
        private readonly IReadOnlyList<double>? _branchDamage;

        public GemDef(string id, GemKind kind, GemColor color, string nameRu, string nameEn, StatTags tags,
            SkillDef? skill = null, TriggerDef? trigger = null, string? supportEffect = null, IReadOnlyDictionary<string, double>? requires = null,
            string? ascendancy = null, bool locked = false, SupportDef? support = null, SkillType? type = null,
            IReadOnlyList<SkillBranchTier>? branches = null, StatTags supports = StatTags.None, double damageMultiplier = 1, double manaMultiplier = 1,
            IReadOnlyList<SkillDef>? variants = null, IReadOnlyList<double>? branchDamage = null, int unlockLevel = 1)
        {
            UnlockLevel = unlockLevel;
            _variants = variants;
            _branchDamage = branchDamage;
            Type = type ?? TypeOf(kind, skill);
            Branches = branches ?? System.Array.Empty<SkillBranchTier>();
            Supports = supports;
            DamageMultiplier = damageMultiplier;
            ManaMultiplier = manaMultiplier;
            Support = support;
            Requires = requires ?? new Dictionary<string, double>();
            Id = id;
            Kind = kind;
            Color = color;
            NameRu = nameRu;
            NameEn = nameEn;
            Tags = tags;
            Skill = skill;
            Trigger = trigger;
            SupportEffect = supportEffect;
            NetId = NetIdOf(id);
            PowerStat = StatId.ForSkill(Sim.CombatStats.SkillPower, id);
            DurationStat = StatId.ForSkill(Sim.CombatStats.SkillDurationSeconds, id);
            Ascendancy = ascendancy;
            Locked = locked;
        }

        public static SkillType TypeOf(GemKind kind, SkillDef? skill) => kind switch
        {
            GemKind.Support => SkillType.Support,
            GemKind.Trigger => SkillType.Trigger,
            _ => skill?.Effect is AuraEffect ? SkillType.Aura : SkillType.Active
        };

        public static int NetIdOf(string id)
        {
            var hash = 2166136261u;
            foreach (var symbol in id)
            {
                hash ^= symbol;
                hash *= 16777619u;
            }
            return unchecked((int)hash);
        }

        public string Id { get; }
        public int NetId { get; }
        public StatId PowerStat { get; }
        public StatId DurationStat { get; }
        public GemKind Kind { get; }
        public GemColor Color { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public StatTags Tags { get; }
        public SkillDef? Skill { get; }
        public TriggerDef? Trigger { get; }
        public string? SupportEffect { get; }
        public SupportDef? Support { get; }
        public IReadOnlyDictionary<string, double> Requires { get; }
        public string? Ascendancy { get; }
        public bool Locked { get; }
        public SkillType Type { get; }
        public IReadOnlyList<SkillBranchTier> Branches { get; }
        public StatTags Supports { get; }
        public double DamageMultiplier { get; }
        public double ManaMultiplier { get; }
        public int UnlockLevel { get; }

        public SkillDef? VariantOf(int combo) => _variants == null || combo == SkillBranches.Plain ? null : _variants[combo];

        public double BranchDamageOf(int combo) => _branchDamage == null ? 1 : _branchDamage[combo];

        public bool CanSupport(GemDef skill) => Type == SkillType.Support && (skill.Type == SkillType.Active || skill.Type == SkillType.Aura) && (skill.Tags & Supports) == Supports;
    }
}
