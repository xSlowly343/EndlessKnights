using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content.Tree;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public sealed class PathTraitOption
    {
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public string Text { get; internal set; } = string.Empty;
        public bool Picked { get; internal set; }
        public bool CanPick { get; internal set; }
    }

    public sealed class PathTraitSlot
    {
        public int HeroLevel { get; internal set; }
        public bool Open { get; internal set; }
        public string Title { get; internal set; } = string.Empty;
        public List<PathTraitOption> Options { get; } = new List<PathTraitOption>();
    }

    public sealed class PathTraitCard
    {
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public string Skill { get; internal set; } = string.Empty;
        public string Mechanic { get; internal set; } = string.Empty;
        public int Level { get; internal set; }
        public int ShownLevel { get; internal set; }
        public List<string> Stats { get; } = new List<string>();
        public string Rule { get; internal set; } = string.Empty;
        public bool RuleShown { get; internal set; }
        public string Party { get; internal set; } = string.Empty;
        public string Mode { get; internal set; } = string.Empty;
        public bool Chosen { get; internal set; }
        public List<PathTraitSlot> Slots { get; } = new List<PathTraitSlot>();
    }

    public static class PathTraitView
    {
        public static PathTraitCard Build(GameLoop loop, SubclassDef path, int shownLevel)
        {
            var catalog = loop.Data.Ascendancy;
            var state = loop.State.Ascendancy;
            var trait = path.Trait;
            var chosen = state.Subclass == path.Id;
            var card = new PathTraitCard
            {
                Id = path.Id,
                Name = SubclassView.Name(path),
                Skill = SubclassView.PathSkill(loop.Data.Gems, path) ?? string.Empty,
                Mechanic = SubclassView.Blurb(path),
                Level = chosen ? state.TraitLevel(catalog) : 0,
                ShownLevel = System.Math.Max(1, System.Math.Min(TraitDef.MaxLevel, shownLevel)),
                Chosen = chosen,
                Mode = ModeText(state.Subclass == path.Id ? state.PartyMode(catalog) : path.PartyMode)
            };
            if (trait == null)
                return card;
            foreach (var line in Totals(trait, card.ShownLevel))
                card.Stats.Add(ModifierText.Describe(line));
            card.Rule = Texts.Pick(trait.RuleRu, trait.RuleEn);
            card.RuleShown = card.ShownLevel == TraitDef.MaxLevel;
            card.Party = Texts.Pick(trait.PartyTextRu, trait.PartyTextEn);
            foreach (var fork in trait.Forks)
                card.Slots.Add(Slot(fork, state, chosen, loop.State.HeroLevel));
            return card;
        }

        public static List<Modifier> Totals(TraitDef trait, int level)
        {
            var totals = new List<Modifier>();
            for (var i = 0; i < level && i < trait.Levels.Count; i++)
            {
                foreach (var modifier in trait.Levels[i])
                {
                    var at = IndexOf(totals, modifier);
                    if (at < 0)
                        totals.Add(modifier);
                    else
                        totals[at] = totals[at].WithValue(Combine(totals[at], modifier));
                }
            }
            return totals;
        }

        public static string ModeText(PartyGiveMode mode) => mode switch
        {
            PartyGiveMode.Support => Texts.Get("subclass.mode.support"),
            PartyGiveMode.Solo => Texts.Get("subclass.mode.solo"),
            _ => Texts.Get("subclass.mode.together")
        };

        private static PathTraitSlot Slot(TraitFork fork, AscendancyState state, bool chosen, int heroLevel)
        {
            var slot = new PathTraitSlot { HeroLevel = fork.HeroLevel, Open = heroLevel >= fork.HeroLevel, Title = Texts.Format("subclass.fork", fork.HeroLevel) };
            var taken = false;
            foreach (var choice in fork.Choices)
                taken |= state.Picked(choice.Id);
            foreach (var choice in fork.Choices)
            {
                slot.Options.Add(new PathTraitOption
                {
                    Id = choice.Id,
                    Name = Texts.Pick(choice.NameRu, choice.NameEn),
                    Text = Texts.Pick(choice.TextRu, choice.TextEn),
                    Picked = state.Picked(choice.Id),
                    CanPick = chosen && slot.Open && !taken
                });
            }
            return slot;
        }

        private static int IndexOf(List<Modifier> totals, Modifier modifier)
        {
            for (var i = 0; i < totals.Count; i++)
            {
                if (totals[i].WithValue(0) == modifier.WithValue(0))
                    return i;
            }
            return -1;
        }

        private static double Combine(Modifier total, Modifier next) =>
            total.Kind == ModifierKind.More ? (1 + total.Value) * (1 + next.Value) - 1 : total.Value + next.Value;
    }
}
