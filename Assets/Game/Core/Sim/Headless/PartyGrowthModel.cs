using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Party;
using Game.Core.Stats;

namespace Game.Core.Sim.Headless
{
    public readonly struct PartyGrowthDay
    {
        public PartyGrowthDay(int day, int partyLevel, int treePoints, int sealTiers, int colossusTier, int relicLevels, int mascotLevel, int crests,
            double uncapped, double damage, double peaceful, double colossusPower, bool capped, double keys, double boss)
        {
            Keys = keys;
            Boss = boss;
            Day = day;
            PartyLevel = partyLevel;
            TreePoints = treePoints;
            SealTiers = sealTiers;
            ColossusTier = colossusTier;
            RelicLevels = relicLevels;
            MascotLevel = mascotLevel;
            Crests = crests;
            Uncapped = uncapped;
            Damage = damage;
            Peaceful = peaceful;
            ColossusPower = colossusPower;
            Capped = capped;
        }

        public int Day { get; }
        public int PartyLevel { get; }
        public int TreePoints { get; }
        public int SealTiers { get; }
        public int ColossusTier { get; }
        public int RelicLevels { get; }
        public int MascotLevel { get; }
        public int Crests { get; }
        public double Uncapped { get; }
        public double Damage { get; }
        public double Peaceful { get; }
        public double ColossusPower { get; }
        public bool Capped { get; }
        public double Keys { get; }
        public double Boss { get; }
    }

    public sealed class PartyGrowthModel
    {
        private const long LongAgoMs = 30L * 86_400_000;
        private readonly GameData _data;
        private readonly PartyGrowthRules _rules;
        private readonly int _members;
        private readonly double _hoursPerDay;
        private readonly double _colossusPointsPerHour;
        private readonly Dictionary<int, int> _tree = new Dictionary<int, int>();
        private readonly List<string> _majors = new List<string>();
        private readonly int[] _trials;
        private readonly Dictionary<string, int> _relics = new Dictionary<string, int>();
        private readonly List<string> _crests = new List<string>();
        private double _experience;
        private double _signs;
        private int _colossusTier = 1;
        private double _colossusHp;
        private int _colossusBest;
        private double _trophies;
        private double _mascotExperience;

        public PartyGrowthModel(GameData data, int members, double hoursPerDay, double colossusPointsPerHour)
        {
            _data = data;
            _rules = data.Party.Growth;
            _members = System.Math.Max(1, members);
            _hoursPerDay = hoursPerDay;
            _colossusPointsPerHour = colossusPointsPerHour;
            _trials = new int[_rules.Trials.List.Count];
            _colossusHp = _rules.Colossus.Hp(1);
        }

        public double SignsPerMemberDay { get; set; } = 50;
        public double TrialTiersPerWeek { get; set; } = 3;
        public string Beast { get; set; } = "boar";

        public IReadOnlyList<PartyGrowthDay> Run(int days)
        {
            var result = new List<PartyGrowthDay>(days);
            for (var day = 1; day <= days; day++)
            {
                Advance(day);
                result.Add(Snapshot(day));
            }
            return result;
        }

        private int Level => _rules.LevelOf(_experience).Level;

        private void Advance(int day)
        {
            var minutes = _hoursPerDay * 60 * _members;
            _experience += minutes * _rules.PerActiveMinute + 40;
            _signs += SignsPerMemberDay * _members;
            var level = Level;
            if (_rules.Opened(PartyUnlockKind.Tree, level))
                BuyTree();
            if (_rules.Opened(PartyUnlockKind.Trials, level) && day % 7 == 0)
                RaiseTrials(day, level);
            if (_rules.Opened(PartyUnlockKind.Colossus, level))
                HitColossus(level);
            if (_rules.Opened(PartyUnlockKind.Mascot, level))
                _mascotExperience += minutes * _rules.Mascot.PerMinute + _members * _rules.Mascot.FeedPerDay * 5;
            PlaceCrests(level);
        }

        private void BuyTree()
        {
            var tree = _rules.Tree;
            var bought = true;
            while (bought)
            {
                bought = false;
                foreach (var node in tree.Nodes)
                {
                    if (tree.CanRaise(node.Number, _tree) != PartyTreeRefusal.None)
                        continue;
                    var price = tree.Price(node.Number, _tree);
                    if (price > _signs)
                        continue;
                    _signs -= price;
                    _tree[node.Number] = (_tree.TryGetValue(node.Number, out var rank) ? rank : 0) + 1;
                    bought = true;
                    break;
                }
            }
            var points = PartyTreeRules.Points(_tree);
            _majors.Clear();
            foreach (var slot in tree.Majors)
            {
                if (points >= slot.At)
                    _majors.Add(slot.Options[0].Id);
            }
        }

        private void RaiseTrials(int day, int level)
        {
            var week = day / 7;
            var ceiling = _rules.Trials.Ceiling(_rules, level);
            foreach (var trial in _rules.Trials.List)
            {
                if (_rules.Trials.OpenIn(trial, week))
                    _trials[trial.Index] = System.Math.Min(ceiling, _trials[trial.Index] + (int)TrialTiersPerWeek);
            }
        }

        private void HitColossus(int level)
        {
            var colossus = _rules.Colossus;
            var ceiling = colossus.Ceiling(_rules, level);
            var points = _colossusPointsPerHour * _hoursPerDay * _members * Layer().ColossusPoints;
            while (points > 0 && _colossusTier <= ceiling)
            {
                var hit = System.Math.Min(points, _colossusHp);
                _colossusHp -= hit;
                points -= hit;
                if (_colossusHp > 1e-9)
                    break;
                _trophies += colossus.Trophies(_colossusTier);
                _colossusBest = System.Math.Max(_colossusBest, _colossusTier);
                if (_colossusTier >= ceiling)
                    break;
                _colossusTier++;
                _colossusHp = colossus.Hp(_colossusTier);
            }
            BuyRelics();
        }

        private void BuyRelics()
        {
            var colossus = _rules.Colossus;
            var open = colossus.RelicsOpen(_colossusBest);
            var bought = true;
            while (bought)
            {
                bought = false;
                PartyColossusRelic? cheapest = null;
                var cheapestLevel = int.MaxValue;
                for (var i = 0; i < open; i++)
                {
                    var relic = colossus.Relics[i];
                    if (!relic.Works)
                        continue;
                    var level = _relics.TryGetValue(relic.Id, out var current) ? current : 0;
                    if (level < colossus.RelicMaxLevel && level < cheapestLevel)
                    {
                        cheapest = relic;
                        cheapestLevel = level;
                    }
                }
                if (cheapest == null)
                    return;
                var price = colossus.RelicPrice(cheapestLevel);
                if (price > _trophies)
                    return;
                _trophies -= price;
                _relics[cheapest.Id] = cheapestLevel + 1;
                bought = true;
            }
        }

        private void PlaceCrests(int level)
        {
            var banner = _rules.Banner;
            var slots = banner.Slots(_rules, level);
            var best = new Dictionary<string, int>();
            foreach (var trial in _rules.Trials.List)
                best[trial.Id] = _trials[trial.Index];
            var earned = new List<PartyCrest>();
            foreach (var crest in banner.Crests)
            {
                if (banner.Earned(crest, best, _colossusBest) && crest.Modifier.Stat == CombatStats.Damage && crest.Modifier.When == ModifierCondition.Always && crest.Modifier.Tags == StatTags.None)
                    earned.Add(crest);
            }
            earned.Sort((a, b) => b.Modifier.Value.CompareTo(a.Modifier.Value));
            _crests.Clear();
            for (var i = 0; i < earned.Count && i < slots; i++)
                _crests.Add(earned[i].Id);
        }

        private PartyGrowthLayer Layer()
        {
            var layer = new PartyGrowthLayer(_rules, new StatSheet()) { Items = _data.Items };
            layer.SetStanding(true, 1, _members, 1 + LongAgoMs);
            layer.SetTree(_tree, _majors);
            layer.SetTrials(_trials);
            layer.SetRelics(_relics);
            var mascotLevel = _rules.Opened(PartyUnlockKind.Mascot, Level) ? _rules.Mascot.LevelOf(_mascotExperience).Level : 0;
            layer.SetMascot(mascotLevel > 0 ? Beast : string.Empty, mascotLevel);
            layer.SetBanner(_crests);
            return layer;
        }

        private PartyGrowthDay Snapshot(int day)
        {
            var layer = Layer();
            var summary = PartyGrowthSummary.Of(layer);
            var relicLevels = 0;
            foreach (var level in _relics.Values)
                relicLevels += level;
            var seals = 0;
            foreach (var tier in _trials)
                seals += tier;
            return new PartyGrowthDay(day, Level, PartyTreeRules.Points(_tree), seals, _colossusTier, relicLevels, layer.MascotLevel, _crests.Count,
                layer.UncappedDamage, layer.DamageMultiplier, layer.Peaceful, layer.ColossusPower, layer.Capped, summary.Keys, summary.VsBoss);
        }
    }
}
