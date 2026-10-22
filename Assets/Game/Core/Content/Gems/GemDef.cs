using Game.Core.Stats;

namespace Game.Core.Content.Gems
{
    public sealed class GemDef
    {
        public GemDef(string id, GemKind kind, GemColor color, string nameRu, string nameEn, StatTags tags,
            SkillDef? skill = null, TriggerDef? trigger = null, string? supportEffect = null)
        {
            Id = id;
            Kind = kind;
            Color = color;
            NameRu = nameRu;
            NameEn = nameEn;
            Tags = tags;
            Skill = skill;
            Trigger = trigger;
            SupportEffect = supportEffect;
        }

        public string Id { get; }
        public GemKind Kind { get; }
        public GemColor Color { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public StatTags Tags { get; }
        public SkillDef? Skill { get; }
        public TriggerDef? Trigger { get; }
        public string? SupportEffect { get; }
    }
}
