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
        private int[] _reactionTicks;
        private int[] _reactionCooldown;
        private ulong _responses;
        private ulong _weakResponses;
        private int _perMarkCount;
        private ulong _reactions;
        private ulong _constellations;
        private int _favouredMarks;
        private double _favouredBonus;

        public SynergySystem(SynergyRules rules, GameState state, StatSheet hero)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _layer = new ModifierLayer(hero);
            _reactionTicks = new int[rules.Reactions.Count];
            _reactionCooldown = new int[rules.Reactions.Count];
        }

        public IReadOnlyList<Modifier> Modifiers => _layer.Applied;

        public SynergyRules Rules => _rules;
        public int OwnMarks { get; private set; }
        public int AllyMarks { get; private set; }
        public int PerMarkCount => _perMarkCount;
        public int FavouredMarks => _favouredMarks;

        public int AllyMarksOf(int slot) => slot >= 0 && slot < PartyRules.MaxSlots ? _allyMarks[slot] : 0;

        public bool ResponseActive(int index) => (_responses & (1UL << index)) != 0;
        public bool ReactionActive(int index) => (_reactions & (1UL << index)) != 0;
        public uint ReactionMask => (uint)_reactions;
        public bool ConstellationActive(int index) => (_constellations & (1UL << index)) != 0;
        public double ReactionCooldownSeconds(int index) => _reactionCooldown[index] * GameLoop.TickSeconds;

        public void UseRules(SynergyRules rules)
        {
            _rules = rules;
            _reactionTicks = new int[rules.Reactions.Count];
            _reactionCooldown = new int[rules.Reactions.Count];
            _responses = 0;
            _reactions = 0;
            _constellations = 0;
            Rebuild();
        }

        public void SetFavoured(int marks, double bonus)
        {
            if (marks == _favouredMarks && bonus == _favouredBonus)
                return;
            _favouredMarks = marks;
            _favouredBonus = bonus;
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
            var responses = ActiveResponses(out var perMarkCount, out var weakResponses);
            var constellations = ActiveConstellations(AllFighting() ? players : 0, out var noCooldown);
            var reactions = TickReactions(players, noCooldown);
            if (responses == _responses && weakResponses == _weakResponses && perMarkCount == _perMarkCount && reactions == _reactions && constellations == _constellations)
                return;
            _responses = responses;
            _weakResponses = weakResponses;
            _perMarkCount = perMarkCount;
            _state.CompanionMarks = perMarkCount;
            _reactions = reactions;
            _constellations = constellations;
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
            if (_state.EvilEyeOnEnemy)
                marks |= sources.EvilEye;
            if (_state.EnemyAlive && _state.Cold > 0)
                marks |= sources.Cold;
            if (_state.EnemyAlive && _state.BloodStacks > 0)
                marks |= sources.Blood;
            if (_state.BondsUp)
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

        private ulong ActiveResponses(out int perMarkCount, out ulong weak)
        {
            perMarkCount = 0;
            weak = 0;
            var active = 0UL;
            var second = _hero.Value(_rules.SecondResponseStat, 0) >= 1;
            var responses = _rules.Responses;
            for (var i = 0; i < responses.Count && i < 64; i++)
            {
                var response = responses[i];
                if (!_state.Is(response.HeroClass) || (response.Second && !second))
                    continue;
                if (response.Mark == SynergyResponse.AnyMark)
                {
                    var count = System.Math.Min(response.MaxMarks + CompanionLimit(), BitCount(CompanionMarks()));
                    if (count == 0)
                        continue;
                    if (response.PerMark)
                        perMarkCount = count;
                }
                else if ((AllyMarks & (1 << response.Mark)) == 0)
                {
                    if ((AllyMarks & _rules.WeakMarksOf(response.Mark)) == 0)
                        continue;
                    weak |= 1UL << i;
                }
                active |= 1UL << i;
            }
            return active;
        }

        private int CompanionLimit() => System.Math.Max(0, (int)System.Math.Round(_hero.Value(CombatStats.CompanionLimit, 0)));

        private int CompanionMarks() =>
            _hero.Value(CombatStats.CompanionOwnMark, 0) >= 1 ? AllyMarks | (OwnMarks & _rules.StateMarks.Blind) : AllyMarks;

        private ulong ActiveConstellations(int players, out bool noCooldown)
        {
            noCooldown = false;
            if (players < 2)
                return 0;
            var marks = OwnMarks | AllyMarks;
            var active = 0UL;
            var constellations = _rules.Constellations;
            for (var i = 0; i < constellations.Count && i < 64; i++)
            {
                var constellation = constellations[i];
                if ((marks & constellation.Marks) != constellation.Marks || !Assign(constellation, 0, 0, players))
                    continue;
                active |= 1UL << i;
                noCooldown |= constellation.NoReactionCooldown;
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

        private ulong TickReactions(int players, bool noCooldown)
        {
            var active = 0UL;
            var reactions = _rules.Reactions;
            for (var i = 0; i < reactions.Count && i < 64; i++)
            {
                if (_reactionTicks[i] > 0)
                    _reactionTicks[i]--;
                if (_reactionCooldown[i] > 0)
                    _reactionCooldown[i]--;
                var reaction = reactions[i];
                if (_reactionTicks[i] == 0 && (_reactionCooldown[i] == 0 || noCooldown) && Unlocked(reaction) && Paired(reaction.First, reaction.Second, players))
                {
                    _reactionTicks[i] = Ticks(reaction.Seconds);
                    _reactionCooldown[i] = Ticks(reaction.CooldownSeconds);
                }
                if (_reactionTicks[i] > 0)
                    active |= 1UL << i;
            }
            return active;
        }

        private bool Unlocked(SynergyReaction reaction) => reaction.Unlock is not { } stat || _hero.Value(stat, 0) >= 1;

        private bool Paired(int first, int second, int players)
        {
            for (var a = 0; a < players; a++)
            {
                if ((_playerMarks[a] & (1 << first)) == 0)
                    continue;
                for (var b = 0; b < players; b++)
                {
                    if (b != a && (_playerMarks[b] & (1 << second)) != 0)
                        return true;
                }
            }
            return false;
        }

        private void Rebuild()
        {
            _buffer.Clear();
            for (var i = 0; i < _rules.Responses.Count && i < 64; i++)
            {
                if (ResponseActive(i))
                    Add(_rules.Responses[i].Modifiers, ResponseTimes(_rules.Responses[i]) * WeakShare(i));
            }
            for (var i = 0; i < _rules.Reactions.Count && i < 64; i++)
            {
                if (ReactionActive(i))
                    Add(_rules.Reactions[i].Modifiers, Favour((1 << _rules.Reactions[i].First) | (1 << _rules.Reactions[i].Second)));
            }
            for (var i = 0; i < _rules.Constellations.Count && i < 64; i++)
            {
                if (ConstellationActive(i))
                    Add(_rules.Constellations[i].Modifiers, 1);
            }
            _layer.Set(_buffer);
        }

        private double ResponseTimes(SynergyResponse response)
        {
            var times = response.PerMark ? _perMarkCount : 1;
            var marks = response.Mark == SynergyResponse.AnyMark ? AllyMarks : 1 << response.Mark;
            return times * Favour(marks);
        }

        private double WeakShare(int index) => (_weakResponses & (1UL << index)) != 0 ? _rules.WeakShareOf(_rules.Responses[index].Mark) : 1;

        private double Favour(int marks) => (marks & _favouredMarks) != 0 ? 1 + _favouredBonus : 1;

        private void Add(IReadOnlyList<Modifier> modifiers, double times)
        {
            for (var i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                _buffer.Add(times == 1 ? modifier : modifier.WithValue(modifier.Value * times));
            }
        }

        private static int BitCount(int mask)
        {
            var count = 0;
            for (; mask != 0; mask &= mask - 1)
                count++;
            return count;
        }

        private static int Ticks(double seconds) => (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
    }
}
