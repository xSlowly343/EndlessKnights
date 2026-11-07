using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public enum UnlockKind
    {
        Drop,
        Guild,
        Clan,
        Hideout,
        Prestige,
        Achievements,
        Server,
        ClassLevel
    }

    public sealed class CompanionUnlock
    {
        public UnlockKind Kind { get; private set; }
        public string Enemy { get; private set; } = string.Empty;
        public double Chance { get; private set; }
        public string Guild { get; private set; } = string.Empty;
        public int Target { get; private set; }
        public string Class { get; private set; } = string.Empty;

        private static readonly Dictionary<string, UnlockKind> Kinds = new Dictionary<string, UnlockKind>(StringComparer.Ordinal)
        {
            ["drop"] = UnlockKind.Drop,
            ["guild"] = UnlockKind.Guild,
            ["clan"] = UnlockKind.Clan,
            ["hideout"] = UnlockKind.Hideout,
            ["prestige"] = UnlockKind.Prestige,
            ["achievements"] = UnlockKind.Achievements,
            ["server"] = UnlockKind.Server,
            ["classLevel"] = UnlockKind.ClassLevel
        };

        internal static CompanionUnlock Load(ContentNode node)
        {
            var name = node.Field("kind").AsString();
            if (!Kinds.TryGetValue(name, out var kind))
                throw node.Field("kind").Error($"unknown unlock '{name}'");
            var unlock = new CompanionUnlock { Kind = kind };
            switch (kind)
            {
                case UnlockKind.Drop:
                    unlock.Enemy = node.Field("enemy").AsString();
                    unlock.Chance = node.Field("chance").AsNumber(0, 1);
                    break;
                case UnlockKind.Guild:
                    unlock.Guild = node.Field("guild").AsString();
                    unlock.Target = node.Field("level").AsInt(1, 1000);
                    break;
                case UnlockKind.Server:
                    break;
                case UnlockKind.ClassLevel:
                    unlock.Class = node.Field("class").AsString();
                    unlock.Target = node.Field("level").AsInt(1, 100000);
                    break;
                default:
                    unlock.Target = node.Field("target").AsInt(1, 1_000_000);
                    break;
            }
            return unlock;
        }
    }

    public sealed class CompanionCoat
    {
        public CompanionCoat(string id, double chance, double traitBonus, int star)
        {
            Id = id;
            Chance = chance;
            TraitBonus = traitBonus;
            Star = star;
        }

        public string Id { get; }
        public double Chance { get; }
        public double TraitBonus { get; }
        public int Star { get; }

        internal static IReadOnlyList<CompanionCoat> LoadAll(ContentNode node)
        {
            var coats = new List<CompanionCoat>();
            foreach (var item in node.Items())
                coats.Add(new CompanionCoat(item.Field("id").AsString(), item.Field("chance").AsNumber(0, 1), item.Field("traitBonus").AsNumber(0, 10), item.HasField("star") ? item.Field("star").AsInt(0, 10) : 0));
            return coats;
        }
    }
}
