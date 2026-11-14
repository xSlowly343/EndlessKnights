using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Party
{
    public sealed class PartyCrest
    {
        internal PartyCrest(string id, string source, int milestone, Modifier modifier)
        {
            Id = id;
            Source = source;
            Milestone = milestone;
            Modifier = modifier;
        }

        public string Id { get; }
        public string Source { get; }
        public int Milestone { get; }
        public Modifier Modifier { get; }
        public bool FromColossus => Source == PartyBannerRules.ColossusSource;
    }

    public sealed class PartyBannerRules
    {
        public const string ColossusSource = "colossus";
        public static readonly PartyBannerRules None = new PartyBannerRules();

        private readonly List<PartyCrest> _crests = new List<PartyCrest>();

        public IReadOnlyList<PartyCrest> Crests => _crests;

        public PartyCrest? Crest(string id)
        {
            foreach (var crest in _crests)
            {
                if (crest.Id == id)
                    return crest;
            }
            return null;
        }

        public int Slots(PartyGrowthRules growth, int partyLevel) =>
            growth.Opened(PartyUnlockKind.Banner, partyLevel) ? 1 + growth.CountOpened(PartyUnlockKind.BannerSlot, partyLevel) : 0;

        public bool Earned(PartyCrest crest, IReadOnlyDictionary<string, int> trialBest, int colossusBest) =>
            crest.FromColossus ? colossusBest >= crest.Milestone : trialBest.TryGetValue(crest.Source, out var best) && best >= crest.Milestone;

        public static string IdOf(string source, int milestone) => source + "_" + milestone.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public static PartyBannerRules Read(ContentNode node, PartyTrialRules trials)
        {
            var rules = new PartyBannerRules();
            var milestones = node.Field("milestones");
            var values = Numbers(node.Field("values"));
            var peaceful = Numbers(node.Field("peaceful"));
            var trialSteps = Ints(milestones.Field("trial"));
            var colossusSteps = Ints(milestones.Field("colossus"));
            if (trialSteps.Count != values.Count || colossusSteps.Count != values.Count || peaceful.Count != values.Count)
                throw node.Field("values").Error("milestones, values and peaceful values must have the same length");
            var lines = node.Field("trials");
            foreach (var trial in trials.List)
            {
                if (!lines.HasField(trial.Id))
                    throw lines.Error($"trial '{trial.Id}' has no crest");
                var template = Template(lines.Field(trial.Id));
                for (var i = 0; i < trialSteps.Count; i++)
                    rules._crests.Add(new PartyCrest(IdOf(trial.Id, trialSteps[i]), trial.Id, trialSteps[i], Valued(template, values[i], peaceful[i])));
            }
            var colossus = Template(node.Field("colossus"));
            for (var i = 0; i < colossusSteps.Count; i++)
                rules._crests.Add(new PartyCrest(IdOf(ColossusSource, colossusSteps[i]), ColossusSource, colossusSteps[i], Valued(colossus, values[i], peaceful[i])));
            return rules;
        }

        private static Modifier Valued(Modifier template, double value, double peaceful) =>
            template.WithValue(template.Stat == Sim.CombatStats.PartyPeaceful ? peaceful : value);

        private static Modifier Template(ContentNode node)
        {
            var tags = node.HasField("tags") ? ModifierReader.ReadTags(node.Field("tags")) : StatTags.None;
            var when = node.HasField("when") ? ModifierReader.ReadCondition(node.Field("when")) : ModifierCondition.Always;
            return new Modifier(new StatId(node.Field("stat").AsString()), ModifierReader.ReadKind(node.Field("kind")), 0, tags, ModifierPool.General, when);
        }

        private static List<double> Numbers(ContentNode node)
        {
            var list = new List<double>();
            foreach (var item in node.Items())
                list.Add(item.AsNumber(0, 10));
            return list;
        }

        private static List<int> Ints(ContentNode node)
        {
            var list = new List<int>();
            foreach (var item in node.Items())
                list.Add(item.AsInt(1, 100_000));
            return list;
        }
    }
}
