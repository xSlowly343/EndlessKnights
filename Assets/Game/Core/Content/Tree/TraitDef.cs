using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Tree
{
    public sealed class TraitForkOption
    {
        public TraitForkOption(string id, string nameRu, string nameEn, IReadOnlyList<Modifier> modifiers)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Modifiers = modifiers;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
        public string TextRu { get; set; } = string.Empty;
        public string TextEn { get; set; } = string.Empty;
        public Game.Core.Party.PartyGiveMode? PartyMode { get; set; }
    }

    public sealed class TraitFork
    {
        public const int Options = 2;
        public const int PartyModeLevel = 75;

        public TraitFork(int heroLevel, IReadOnlyList<TraitForkOption> choices)
        {
            HeroLevel = heroLevel;
            Choices = choices;
        }

        public int HeroLevel { get; }
        public IReadOnlyList<TraitForkOption> Choices { get; }
    }

    public sealed class TraitDef
    {
        public const int MaxLevel = 5;

        public TraitDef(IReadOnlyList<IReadOnlyList<Modifier>> levels, IReadOnlyList<Modifier> party, IReadOnlyList<TraitFork> forks)
        {
            Levels = levels;
            Party = party;
            Forks = forks;
        }

        public IReadOnlyList<IReadOnlyList<Modifier>> Levels { get; }
        public IReadOnlyList<Modifier> Party { get; }
        public IReadOnlyList<TraitFork> Forks { get; }
        public string PartyTextRu { get; private set; } = string.Empty;
        public string PartyTextEn { get; private set; } = string.Empty;

        public bool TryFind(string optionId, out TraitFork fork, out TraitForkOption option)
        {
            foreach (var candidate in Forks)
            {
                foreach (var choice in candidate.Choices)
                {
                    if (string.Equals(choice.Id, optionId, StringComparison.Ordinal))
                    {
                        fork = candidate;
                        option = choice;
                        return true;
                    }
                }
            }
            fork = null!;
            option = null!;
            return false;
        }

        public static TraitDef Read(ContentNode node, HashSet<string> optionIds)
        {
            var levels = new List<IReadOnlyList<Modifier>>();
            foreach (var level in node.Field("levels").Items())
                levels.Add(ModifierReader.ReadList(level.Field("modifiers")));
            if (levels.Count != MaxLevel)
                throw node.Field("levels").Error($"a trait has exactly {MaxLevel} levels, got {levels.Count}");
            var party = node.HasField("party") ? ModifierReader.ReadList(node.Field("party")) : Array.Empty<Modifier>();
            var partyText = node.HasField("partyText") ? node.Field("partyText") : (ContentNode?)null;
            return new TraitDef(levels, party, ReadForks(node.Field("forks"), optionIds))
            {
                PartyTextRu = partyText?.Field("ru").AsString() ?? string.Empty,
                PartyTextEn = partyText?.Field("en").AsString() ?? string.Empty
            };
        }

        private static List<TraitFork> ReadForks(ContentNode node, HashSet<string> optionIds)
        {
            var forks = new List<TraitFork>();
            foreach (var item in node.Items())
            {
                var heroLevel = item.Field("level").AsInt(1, 1000);
                if (forks.Count > 0 && heroLevel <= forks[forks.Count - 1].HeroLevel)
                    throw item.Field("level").Error("fork levels must go up");
                var choices = new List<TraitForkOption>();
                foreach (var option in item.Field("options").Items())
                {
                    var id = option.Field("id").AsString();
                    if (!optionIds.Add(id))
                        throw option.Field("id").Error($"fork option id '{id}' is used twice");
                    if (option.HasField("partyMode") && heroLevel != TraitFork.PartyModeLevel)
                        throw option.Field("partyMode").Error($"the party mode changes only at the fork of level {TraitFork.PartyModeLevel}");
                    var name = option.Field("name");
                    var text = option.HasField("text") ? option.Field("text") : (ContentNode?)null;
                    choices.Add(new TraitForkOption(id, name.Field("ru").AsString(), name.Field("en").AsString(), ModifierReader.ReadList(option.Field("modifiers")))
                    {
                        TextRu = text?.Field("ru").AsString() ?? string.Empty,
                        TextEn = text?.Field("en").AsString() ?? string.Empty,
                        PartyMode = option.HasField("partyMode") ? Game.Core.Party.CoopRules.ReadMode(option.Field("partyMode")) : (Game.Core.Party.PartyGiveMode?)null
                    });
                }
                if (choices.Count != TraitFork.Options)
                    throw item.Field("options").Error($"a fork offers exactly {TraitFork.Options} options, got {choices.Count}");
                forks.Add(new TraitFork(heroLevel, choices));
            }
            return forks;
        }
    }
}
