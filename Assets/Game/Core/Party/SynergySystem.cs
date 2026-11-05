using System.Collections.Generic;
using Game.Core.Content.Synergy;
using Game.Core.Sim;
using Game.Core.Stats;

namespace Game.Core.Party
{
    public sealed class SynergySystem
    {
        private const int Players = PartyRules.MaxSlots + 1;

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly ModifierLayer _layer;
        private readonly List<Modifier> _buffer = new List<Modifier>(64);
        private readonly int[] _allyMarks = new int[PartyRules.MaxSlots];
        private readonly HeroClass[] _allyClass = new HeroClass[PartyRules.MaxSlots];
        private readonly bool[] _allyPresent = new bool[PartyRules.MaxSlots];
        private readonly bool[] _allyFighting = new bool[PartyRules.MaxSlots];
        private readonly int[] _playerMarks = new int[Players];
        private readonly HeroClass[] _playerClass = new HeroClass[Players];
        private SynergyRules _rules;
        private ulong _constellations;
        private double _constellationEffect = double.NaN;

        public SynergySystem(SynergyRules rules, GameState state, StatSheet hero)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _layer = new ModifierLayer(hero);
        }

        public IReadOnlyList<Modifier> Modifiers => _layer.Applied;

        public SynergyRules Rules => _rules;
        public int OwnMarks { get; private set; }
        public int AllyMarks { get; private set; }

        public int AllyMarksOf(int slot) => slot >= 0 && slot < PartyRules.MaxSlots ? _allyMarks[slot] : 0;

        public bool ConstellationActive(int index) => (_constellations & (1UL << index)) != 0;
        public uint ConstellationMask => (uint)_constellations;

        public void UseRules(SynergyRules rules)
        {
            _rules = rules;
            _constellations = 0;
            Rebuild();
        }

        public bool OwnFighting { get; set; } = true;

        public void SetAlly(int slot, bool present, HeroClass heroClass, int marks, bool fighting = true)
        {
            _allyPresent[slot] = present;
            _allyFighting[slot] = present && fighting;
            _allyClass[slot] = heroClass;
            _allyMarks[slot] = present ? marks : 0;
        }

        public void ClearAllies()
        {
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
                SetAlly(slot, false, default, 0);
            OwnFighting = true;
        }

        public void Tick()
        {
            var players = CollectPlayers();
            var constellations = ActiveConstellations(AllFighting() ? players : 0);
            var effect = ConstellationEffect;
            if (constellations == _constellations && effect == _constellationEffect)
                return;
            _constellations = constellations;
            _constellationEffect = effect;
            Rebuild();
        }

        private bool AllFighting()
        {
            if (!OwnFighting)
                return false;
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                if (_allyPresent[slot] && !_allyFighting[slot])
                    return false;
            }
            return true;
        }

        private int CollectPlayers()
        {
            OwnMarks = OwnSkillMarks();
            _playerMarks[0] = OwnMarks;
            _playerClass[0] = _state.Class;
            var players = 1;
            var allies = 0;
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                if (!_allyPresent[slot])
                    continue;
                _playerMarks[players] = _allyMarks[slot];
                _playerClass[players] = _allyClass[slot];
                allies |= _allyMarks[slot];
                players++;
            }
            AllyMarks = allies;
            return players;
        }

        private int OwnSkillMarks()
        {
            var marks = StateMarks();
            var skills = _state.Skills;
            for (var i = 0; i < skills.Count; i++)
            {
                var gemMarks = _rules.MaskOf(skills[i].Skill);
                if (gemMarks == 0)
                    continue;
                if (skills[i].Active)
                    marks |= gemMarks;
                else if (skills[i].LastCastTick >= 0)
                    marks |= Lingering(gemMarks, _state.Tick - skills[i].LastCastTick);
            }
            return marks;
        }

        private int Lingering(int gemMarks, long ticksSinceCast)
        {
            var marks = 0;
            for (var mark = 0; gemMarks >> mark != 0; mark++)
            {
                if ((gemMarks & (1 << mark)) != 0 && ticksSinceCast < _rules.MarkTicks(mark))
                    marks |= 1 << mark;
            }
            return marks;
        }

        private int StateMarks()
        {
            var sources = _rules.StateMarks;
            var marks = 0;
            if (_state.EnemyAlive && _state.PoisonStacks > 0)
                marks |= sources.Poison;
            if (_state.EnemyAlive && _state.BurnStacks > 0)
                marks |= sources.Burn;
            if (sources.Minions != 0 && MinionsOut())
                marks |= sources.Minions;
            if (_state.EnemyAlive && _state.StunTicks > 0)
                marks |= sources.Stun;
            if (_state.EnemyAlive && _state.HunterMarkTicks > 0)
                marks |= sources.HunterMark;
            if (_state.EnemyAlive && _state.BlindTicks > 0)
                marks |= sources.Blind;
            if (_state.EnemyAlive && _state.Soulbinder.ColdTicks > 0)
                marks |= sources.Cold;
            if (_state.EnemyAlive && _state.Herbwitch.Brew > 0)
                marks |= sources.Blood;
            if (_state.Boons.PollenTicks > 0 && _state.Boons.Pollen > 0)
                marks |= sources.Bond;
            return marks;
        }

        private bool MinionsOut()
        {
            var minions = _state.Minions;
            for (var i = 0; i < minions.Length; i++)
            {
                if (minions[i].Standing)
                    return true;
            }
            return false;
        }

        private ulong ActiveConstellations(int players)
        {
            if (players < 2)
                return 0;
            var active = 0UL;
            var constellations = _rules.Constellations;
            for (var i = 0; i < constellations.Count && i < 64; i++)
            {
                if (Assign(constellations[i], 0, 0, players))
                    active |= 1UL << i;
            }
            return active;
        }

        private bool Assign(SynergyConstellation constellation, int requirement, int used, int players)
        {
            if (requirement == constellation.Members)
                return true;
            for (var player = 0; player < players; player++)
            {
                if ((used & (1 << player)) != 0 || !Fits(constellation, requirement, _playerClass[player]))
                    continue;
                if (Assign(constellation, requirement + 1, used | (1 << player), players))
                    return true;
            }
            return false;
        }

        private bool Fits(SynergyConstellation constellation, int requirement, HeroClass heroClass)
        {
            if (requirement < constellation.Classes.Count)
                return constellation.Classes[requirement] == heroClass;
            return (constellation.RoleGroups[requirement - constellation.Classes.Count] & _rules.RoleMaskOf(heroClass)) != 0;
        }

        private void Rebuild()
        {
            _buffer.Clear();
            var effect = ConstellationEffect;
            for (var i = 0; i < _rules.Constellations.Count && i < 64; i++)
            {
                if (ConstellationActive(i))
                    Add(_rules.Constellations[i].Modifiers, effect);
            }
            _layer.Set(_buffer);
        }

        private double ConstellationEffect => System.Math.Max(0, _hero.Value(CombatStats.ConstellationEffect, 1));

        private void Add(IReadOnlyList<Modifier> modifiers, double times)
        {
            for (var i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                _buffer.Add(times == 1 ? modifier : modifier.WithValue(modifier.Value * times));
            }
        }
    }
}
