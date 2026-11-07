using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public enum AbilityKind
    {
        Harvest,
        Dig,
        Bite,
        Forge,
        Tailwind,
        Fold,
        Rally,
        Gathering
    }

    public sealed class CompanionAbility
    {
        public CompanionAbility(string family, AbilityKind kind, string nameRu, string nameEn, double value, double seconds, string waits)
        {
            Family = family;
            Kind = kind;
            NameRu = nameRu;
            NameEn = nameEn;
            Value = value;
            Seconds = seconds;
            Waits = waits;
        }

        public string Family { get; }
        public AbilityKind Kind { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public double Value { get; }
        public double Seconds { get; }
        public string Waits { get; }
        public bool Works => Waits.Length == 0;
    }

    public sealed class CompanionAbilityRules
    {
        public IReadOnlyList<CompanionAbility> Abilities { get; private set; } = Array.Empty<CompanionAbility>();

        public CompanionAbility? Of(string family)
        {
            for (var i = 0; i < Abilities.Count; i++)
            {
                if (Abilities[i].Family == family)
                    return Abilities[i];
            }
            return null;
        }

        private static readonly Dictionary<string, AbilityKind> Kinds = new Dictionary<string, AbilityKind>(StringComparer.Ordinal)
        {
            ["harvest"] = AbilityKind.Harvest,
            ["dig"] = AbilityKind.Dig,
            ["bite"] = AbilityKind.Bite,
            ["forge"] = AbilityKind.Forge,
            ["tailwind"] = AbilityKind.Tailwind,
            ["fold"] = AbilityKind.Fold,
            ["rally"] = AbilityKind.Rally,
            ["gathering"] = AbilityKind.Gathering
        };

        internal static CompanionAbilityRules Load(ContentNode node)
        {
            var abilities = new List<CompanionAbility>();
            foreach (var item in node.Field("list").Items())
            {
                var kindName = item.Field("kind").AsString();
                if (!Kinds.TryGetValue(kindName, out var kind))
                    throw item.Field("kind").Error($"unknown ability '{kindName}'");
                var name = item.Field("name");
                abilities.Add(new CompanionAbility(item.Field("family").AsString(), kind, name.Field("ru").AsString(), name.Field("en").AsString(),
                    item.HasField("value") ? item.Field("value").AsNumber(0, 100) : 0,
                    item.HasField("seconds") ? item.Field("seconds").AsNumber(0, 86400) : 0,
                    item.HasField("waits") ? item.Field("waits").AsString() : string.Empty));
            }
            return new CompanionAbilityRules { Abilities = abilities };
        }
    }
}
