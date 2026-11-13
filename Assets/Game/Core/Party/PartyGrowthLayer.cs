using System.Collections.Generic;
using Game.Core.Sim;
using Game.Core.Stats;

namespace Game.Core.Party
{
    public sealed class PartyGrowthLayer
    {
        private const long DayMs = 86_400_000;

        private readonly ModifierLayer _layer;
        private readonly List<Modifier> _sources = new List<Modifier>();
        private readonly List<Modifier> _applied = new List<Modifier>();
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
            if (Share > 0)
            {
                foreach (var source in _sources)
                    _applied.Add(source.WithValue(source.Value * Share));
            }
            DamageMultiplier = CapDamage();
            Peaceful = CapPeaceful();
            _layer.Set(_applied);
        }

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
