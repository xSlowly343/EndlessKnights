using System.Collections.Generic;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public sealed class ExpeditionCompanionLine
    {
        public string Companion { get; internal set; } = string.Empty;
        public string Trait { get; internal set; } = string.Empty;
        public bool Busy { get; internal set; }
    }

    public sealed class ExpeditionLine
    {
        public int Index { get; internal set; }
        public string Companion { get; internal set; } = string.Empty;
        public string Partner { get; internal set; } = string.Empty;
        public string Biome { get; internal set; } = string.Empty;
        public double Progress { get; internal set; }
        public double SecondsLeft { get; internal set; }
        public string Letter { get; internal set; } = string.Empty;
        public bool Choice { get; internal set; }
    }

    public static class ExpeditionView
    {
        public static List<ExpeditionCompanionLine> Companions(ExpeditionSystem expeditions, bool russian = true)
        {
            var lines = new List<ExpeditionCompanionLine>();
            foreach (var companion in expeditions.Companions())
            {
                var trait = expeditions.TraitOf(companion);
                lines.Add(new ExpeditionCompanionLine
                {
                    Companion = companion,
                    Trait = trait == null ? string.Empty : russian ? trait.NameRu : trait.NameEn,
                    Busy = expeditions.Busy(companion)
                });
            }
            return lines;
        }

        public static List<ExpeditionLine> Active(ExpeditionSystem expeditions, bool russian = true)
        {
            var lines = new List<ExpeditionLine>();
            var active = expeditions.State.Active;
            var letters = expeditions.Rules.Letters;
            for (var i = 0; i < active.Count; i++)
            {
                var expedition = active[i];
                var letter = expedition.LetterSent && expedition.Letter < letters.Count ? letters[expedition.Letter] : (Ru: string.Empty, En: string.Empty);
                lines.Add(new ExpeditionLine
                {
                    Index = i,
                    Companion = expedition.Companion,
                    Partner = expedition.Partner,
                    Biome = expedition.Biome,
                    Progress = expedition.Seconds <= 0 ? 1 : System.Math.Min(1, expedition.Elapsed / expedition.Seconds),
                    SecondsLeft = System.Math.Max(0, expedition.Seconds - expedition.Elapsed),
                    Letter = russian ? letter.Ru : letter.En,
                    Choice = expedition.Choice
                });
            }
            return lines;
        }
    }
}
