using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Gems
{
    public sealed class GemDef
    {
        public GemDef(string id, GemKind kind, GemColor color, string nameRu, string nameEn, StatTags tags,
            SkillDef? skill = null, TriggerDef? trigger = null, string? supportEffect = null, IReadOnlyDictionary<string, double>? requires = null,
            string? ascendancy = null, bool locked = false)
        {
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
            Ascendancy = ascendancy;
            Locked = locked;
        }

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
        public GemKind Kind { get; }
        public GemColor Color { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public StatTags Tags { get; }
        public SkillDef? Skill { get; }
        public TriggerDef? Trigger { get; }
        public string? SupportEffect { get; }
        public IReadOnlyDictionary<string, double> Requires { get; }
        public string? Ascendancy { get; }
        public bool Locked { get; }
    }
}
