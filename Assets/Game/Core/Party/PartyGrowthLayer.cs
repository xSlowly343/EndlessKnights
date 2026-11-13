using System.Collections.Generic;
using Game.Core.Sim;
using Game.Core.Stats;

namespace Game.Core.Party
{
    public sealed class PartyGrowthLayer
    {
        private const long DayMs = 86_400_000;
        public static readonly StatId TrialSeals = new StatId("trialSeals");
        public static readonly StatId TrialPowerStat = new StatId("trialPower");
        public static readonly StatId TrialTries = new StatId("trialTries");
        public static readonly StatId TrialCarry = new StatId("trialCarry");
        public static readonly StatId ColossusDamage = new StatId("colossusDamage");
        public static readonly StatId ColossusHardship = new StatId("colossusHardship");
        public static readonly StatId ColossusHardshipOff = new StatId("colossusHardshipOff");
        public static readonly StatId ColossusNoHeal = new StatId("colossusNoHeal");
        public static readonly StatId ColossusTrophies = new StatId("colossusTrophies");
        public static readonly StatId ColossusLoot = new StatId("colossusLoot");
        public static readonly StatId ColossusTrophyChance = new StatId("colossusTrophyChance");
        public static readonly StatId SignsColossus = new StatId("signsColossus");
        public static readonly StatId ColossusTrophyFlat = new StatId("colossusTrophyFlat");

        private readonly ModifierLayer _layer;
        private readonly List<Modifier> _sources = new List<Modifier>();
        private readonly List<Modifier> _applied = new List<Modifier>();
        private readonly List<Modifier> _tree = new List<Modifier>();
        private readonly Dictionary<int, int> _treeLevels = new Dictionary<int, int>();
        private readonly List<string> _treeMajors = new List<string>();
        private readonly List<int> _trialBest = new List<int>();
        private readonly List<Modifier> _relics = new List<Modifier>();
        private readonly Dictionary<string, int> _relicLevels = new Dictionary<string, int>();
        private PartyGrowthRules _rules;
        private long _joinedUnixMs;
        private long _nowUnixMs;

        public PartyGrowthLayer(PartyGrowthRules rules, StatSheet hero)
        {
            _rules = rules;
            _layer = new ModifierLayer(hero, ModifierPool.PartyGrowth);
        }

        public bool InParty { get; private set; }
        public int Members { get; private set; }
        public double Share { get; private set; }
        public double DamageMultiplier { get; private set; } = 1;
        public double Peaceful { get; private set; }
        public bool Capped { get; private set; }
        public IReadOnlyList<Modifier> Applied => _applied;
        public IReadOnlyList<Modifier> Tree => _tree;
        public IReadOnlyDictionary<int, int> TreeLevels => _treeLevels;
        public IReadOnlyList<string> TreeMajors => _treeMajors;
        public IReadOnlyList<int> TrialBest => _trialBest;
        public double Seals { get; private set; }
        public double TrialPower { get; private set; } = 1;
        public double ColossusPower { get; private set; } = 1;

        public IReadOnlyList<Modifier> Relics => _relics;
        public IReadOnlyDictionary<string, int> RelicLevels => _relicLevels;

        public void SetRelics(IReadOnlyDictionary<string, int> levels)
        {
            _relicLevels.Clear();
            foreach (var pair in levels)
                _relicLevels[pair.Key] = pair.Value;
            Rebuild();
        }

        private void BuildRelics()
        {
            _relics.Clear();
            foreach (var relic in _rules.Colossus.Relics)
            {
                if (!relic.Works || !_relicLevels.TryGetValue(relic.Id, out var level) || level <= 0)
                    continue;
                _relics.Add(relic.At(System.Math.Min(level, _rules.Colossus.RelicMaxLevel)));
            }
        }

        public void SetTrials(IReadOnlyList<int> bestTiers)
        {
            _trialBest.Clear();
            for (var i = 0; i < bestTiers.Count; i++)
                _trialBest.Add(bestTiers[i]);
            Rebuild();
        }

        public void SetTree(IReadOnlyDictionary<int, int> levels, IReadOnlyList<string> majors)
        {
            _treeLevels.Clear();
            foreach (var pair in levels)
                _treeLevels[pair.Key] = pair.Value;
            _treeMajors.Clear();
            for (var i = 0; i < majors.Count; i++)
                _treeMajors.Add(majors[i]);
            Rebuild();
        }

        public void UseRules(PartyGrowthRules rules)
        {
            _rules = rules;
            Rebuild();
        }

        public void SetStanding(bool inParty, long joinedUnixMs, int members, long nowUnixMs)
        {
            InParty = inParty;
            _joinedUnixMs = joinedUnixMs;
            Members = inParty ? System.Math.Max(1, members) : 0;
            _nowUnixMs = nowUnixMs;
            Rebuild();
        }

        public void SetSources(IReadOnlyList<Modifier> sources)
        {
            _sources.Clear();
            for (var i = 0; i < sources.Count; i++)
                _sources.Add(sources[i]);
            Rebuild();
        }

        public bool Refresh(long nowUnixMs)
        {
            if (!InParty || ShareAt(nowUnixMs) == Share)
            {
                _nowUnixMs = nowUnixMs;
                return false;
            }
            _nowUnixMs = nowUnixMs;
            Rebuild();
            return true;
        }

        private double ShareAt(long nowUnixMs)
        {
            if (!InParty)
                return 0;
            if (_joinedUnixMs <= 0)
                return 1;
            return _rules.NewcomerShare((int)(System.Math.Max(0, nowUnixMs - _joinedUnixMs) / DayMs));
        }

        private void Rebuild()
        {
            _applied.Clear();
            Share = ShareAt(_nowUnixMs);
            Capped = false;
            PartyTreeBonus.Build(_rules.Tree, _treeLevels, _treeMajors, Members, _tree);
            BuildRelics();
            Seals = _rules.Trials.SealIncrease(_trialBest) * Scale(TrialSeals);
            TrialPower = InParty ? Scale(TrialPowerStat) : 1;
            ColossusPower = InParty ? Scale(ColossusDamage) : 1;
            if (Share > 0)
            {
                foreach (var source in _sources)
                    _applied.Add(source.WithValue(source.Value * Share));
                foreach (var line in _tree)
                {
                    if (!IsPartyOnlyLine(line))
                        _applied.Add(line.WithValue(line.Value * Share));
                }
                foreach (var line in _relics)
                {
                    if (!IsPartyOnlyLine(line))
                        _applied.Add(line.WithValue(line.Value * Share));
                }
                if (Seals > 0)
                    _applied.Add(new Modifier(CombatStats.Damage, ModifierKind.Increased, Seals * Share));
            }
            DamageMultiplier = CapDamage();
            Peaceful = CapPeaceful();
            _layer.Set(_applied);
        }

        private double Scale(StatId stat)
        {
            var increased = 0.0;
            var more = 1.0;
            Collect(_tree, stat, ref increased, ref more);
            Collect(_relics, stat, ref increased, ref more);
            return System.Math.Max(0, (1 + increased) * more);
        }

        private static void Collect(List<Modifier> lines, StatId stat, ref double increased, ref double more)
        {
            foreach (var line in lines)
            {
                if (line.Stat != stat)
                    continue;
                if (line.Kind == ModifierKind.Increased)
                    increased += line.Value;
                else if (line.Kind == ModifierKind.More)
                    more *= 1 + line.Value;
            }
        }

        private static bool IsPartyOnlyLine(Modifier line) =>
            line.Stat == TrialSeals || line.Stat == TrialPowerStat || line.Stat == TrialTries || line.Stat == TrialCarry || line.Stat == ColossusDamage
            || line.Stat == ColossusHardship || line.Stat == ColossusHardshipOff || line.Stat == ColossusNoHeal
            || line.Stat == ColossusTrophies || line.Stat == ColossusLoot || line.Stat == ColossusTrophyChance || line.Stat == SignsColossus
            || line.Stat == ColossusTrophyFlat;

        private double CapDamage()
        {
            var increased = 0.0;
            var more = 1.0;
            foreach (var modifier in _applied)
            {
                if (!IsPlain(modifier, CombatStats.Damage))
                    continue;
                if (modifier.Kind == ModifierKind.Increased)
                    increased += modifier.Value;
                else if (modifier.Kind == ModifierKind.More)
                    more *= 1 + modifier.Value;
            }
            var total = (1 + increased) * more;
            if (total <= _rules.DamageCap)
                return total;
            Capped = true;
            _applied.Add(new Modifier(CombatStats.Damage, ModifierKind.More, _rules.DamageCap / total - 1));
            return _rules.DamageCap;
        }

        private double CapPeaceful()
        {
            var peaceful = 0.0;
            foreach (var modifier in _applied)
            {
                if (IsPlain(modifier, CombatStats.PartyPeaceful) && modifier.Kind != ModifierKind.More)
                    peaceful += modifier.Value;
            }
            var cap = _rules.PeacefulCap - 1;
            if (peaceful <= cap)
                return peaceful;
            Capped = true;
            _applied.Add(new Modifier(CombatStats.PartyPeaceful, ModifierKind.Flat, cap - peaceful));
            return cap;
        }

        private static bool IsPlain(Modifier modifier, StatId stat) =>
            modifier.Stat == stat && modifier.Tags == StatTags.None && modifier.When == ModifierCondition.Always;
    }
}
