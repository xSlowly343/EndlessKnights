using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Gems
{
    public sealed class GemDef
    {
        private readonly IReadOnlyList<SkillDef>? _variants;
        private readonly IReadOnlyList<double>? _branchDamage;

        public GemDef(string id, GemKind kind, GemColor color, string nameRu, string nameEn, StatTags tags,
            SkillDef? skill = null, string? supportEffect = null,
            string? ascendancy = null, bool locked = false, SupportDef? support = null, SkillType? type = null,
            IReadOnlyList<SkillBranchTier>? branches = null, SkillMechanic fits = SkillMechanic.None, double damageMultiplier = 1, double manaMultiplier = 1,
            IReadOnlyList<SkillDef>? variants = null, IReadOnlyList<double>? branchDamage = null, int unlockLevel = 1,
            int socket = 0, string? forSkill = null)
        {
            UnlockLevel = unlockLevel;
            Socket = socket;
            ForSkill = forSkill;
            _variants = variants;
            _branchDamage = branchDamage;
            Type = type ?? TypeOf(kind, skill);
            Branches = branches ?? System.Array.Empty<SkillBranchTier>();
            Fits = fits;
            Mechanics = SkillMechanics.Of(skill, tags);
            DamageMultiplier = damageMultiplier;
            ManaMultiplier = manaMultiplier;
            Support = support;
            Id = id;
            Kind = kind;
            Color = color;
            NameRu = nameRu;
            NameEn = nameEn;
            Tags = tags;
            Skill = skill;
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
        public string? SupportEffect { get; }
        public SupportDef? Support { get; }
        public string? Ascendancy { get; }
        public bool Locked { get; }
        public SkillType Type { get; }
        public IReadOnlyList<SkillBranchTier> Branches { get; }
        public SkillMechanic Fits { get; }
        public SkillMechanic Mechanics { get; }
        public double DamageMultiplier { get; }
        public double ManaMultiplier { get; }
        public int UnlockLevel { get; }
        public int Socket { get; }
        public string? ForSkill { get; }
        public string? ForPath { get; internal set; }

        public SkillDef? VariantOf(int combo) => _variants == null || combo == SkillBranches.Plain ? null : _variants[combo];

        public double BranchDamageOf(int combo) => _branchDamage == null ? 1 : _branchDamage[combo];

        public bool CanSupport(GemDef skill) => Type == SkillType.Support && (skill.Type == SkillType.Active || skill.Type == SkillType.Aura)
            && (ForSkill != null ? ForSkill == skill.Id : (skill.Mechanics & Fits) != 0);
    }
}
