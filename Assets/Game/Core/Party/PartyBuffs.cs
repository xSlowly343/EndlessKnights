using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Sim;
using Game.Core.Stats;

namespace Game.Core.Party
{
    public sealed class PartyBuffs
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly ModifierLayer _layer;
        private readonly List<Modifier> _buffer = new List<Modifier>(64);
        private readonly AllyOffer[] _allies = new AllyOffer[PartyRules.MaxSlots];
        private readonly double[] _applied = new double[PartyBuffRules.MaxStats];
        private readonly CoopGiver _giver = new CoopGiver();
        private readonly double[] _coopApplied = new double[CoopRules.MaxStats];
        private readonly int[] _giverClass = new int[PartyRules.MaxSlots + 1];
        private readonly double[] _giverValue = new double[PartyRules.MaxSlots + 1];
        private readonly int[] _enemyEffectIds;
        private int _enemyResendTicks;
        private double _soloTaken;
        private double _soloSlow;
        private PartyBuffRules _rules;
        private bool _dirty;
        private double _traitDamage;
        private double _traitCritChance;
        private double _traitGold;
        private double _traitCritMultiplier;
        private double _traitCooldown;

        public PartyBuffs(PartyBuffRules rules, GameState state, StatSheet hero)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _layer = new ModifierLayer(hero);
            for (var i = 0; i < _allies.Length; i++)
                _allies[i] = new AllyOffer();
            var classes = System.Enum.GetValues(typeof(HeroClass));
            _enemyEffectIds = new int[classes.Length];
            foreach (HeroClass heroClass in classes)
                _enemyEffectIds[(int)heroClass] = GemDef.NetIdOf("coop_enemy_" + heroClass);
        }

        public CoopGiver Giver => _giver;

        public double CoopOffered(int index) => _giver.Allies[index];

        public double CoopApplied(int index) => _coopApplied[index];

        public int EnemyEffectId(HeroClass heroClass) => _enemyEffectIds[(int)heroClass];

        public IReadOnlyList<Modifier> Modifiers => _layer.Applied;

        public static bool Shared(GemDef gem) => gem.Skill?.Effect is AuraEffect { Party: true } || SkillSystem.StacksOf(gem) != null;

        public PartyBuffRules Rules => _rules;

        public AllyOffer Ally(int slot) => _allies[slot];

        public double OwnValue(int index) => _rules.Stats[index].ValueOf(_hero) + TraitBonus(index);

        public double OfferedValue(int index) => OwnValue(index) * PartyRole.Giving(_hero);

        private double TraitBonus(int index)
        {
            var from = _rules.Stats[index].From;
            if (from == CombatStats.PartyDamage)
                return _state.TraitPartyDamage;
            if (from == CombatStats.PartyGold)
                return _state.TraitPartyGold;
            if (from == CombatStats.PartyCritMultiplier)
                return _state.TraitPartyCritMultiplier;
            if (from == CombatStats.PartySkillCooldown)
                return _state.TraitPartyCooldown;
            return from == CombatStats.PartyCritChance ? _state.TraitPartyCritChance : 0;
        }

        public double AppliedValue(int index) => _applied[index];

        public int AppliedModifiers => _layer.Applied.Count;

        public void UseRules(PartyBuffRules rules)
        {
            _rules = rules;
            Rebuild();
        }

        public void MarkChanged() => _dirty = true;

        public void ClearAllies()
        {
            for (var i = 0; i < _allies.Length; i++)
            {
                if (!_allies[i].Present)
                    continue;
                _allies[i].Clear();
                _dirty = true;
            }
        }

        public void ClearAlly(int slot)
        {
            if (!_allies[slot].Present)
                return;
            _allies[slot].Clear();
            _dirty = true;
        }

        public void Tick()
        {
            var members = 1;
            for (var slot = 0; slot < _allies.Length; slot++)
            {
                var ally = _allies[slot];
                if (!ally.Present)
                    continue;
                members++;
                for (var i = 0; i < ally.Auras.Length; i++)
                    TickAura(ally.Auras[i]);
            }
            _state.PartyMembers = members;
            TickCoop(members);
            if (_state.TraitPartyDamage != _traitDamage || _state.TraitPartyCritChance != _traitCritChance || _state.TraitPartyGold != _traitGold
                || _state.TraitPartyCritMultiplier != _traitCritMultiplier || _state.TraitPartyCooldown != _traitCooldown)
            {
                _traitDamage = _state.TraitPartyDamage;
                _traitCritChance = _state.TraitPartyCritChance;
                _traitGold = _state.TraitPartyGold;
                _traitCritMultiplier = _state.TraitPartyCritMultiplier;
                _traitCooldown = _state.TraitPartyCooldown;
                _dirty = true;
            }
            if (_dirty)
                Rebuild();
        }

        public void Rebuild()
        {
            _dirty = false;
            _buffer.Clear();
            AddStats();
            AddCoop();
            AddAllyAuras();
            _layer.Set(_buffer);
        }

        private void TickCoop(int members)
        {
            var coop = _rules.Coop;
            var mode = _state.PartyMode;
            _giver.Tick(_hero, _state, coop, coop.ModeSelf(mode), coop.ModePower(mode) * PartyRole.Giving(_hero));
            var inParty = _state.SharedEnemy && members > 1;
            ShareEnemy(inParty, coop);
            var soloTaken = inParty ? 0 : _giver.EnemyDamageTaken;
            var soloSlow = inParty ? 0 : System.Math.Min(0.9, _giver.EnemyTimerSlow);
            if (soloTaken != _soloTaken || soloSlow != _soloSlow)
            {
                _soloTaken = soloTaken;
                _soloSlow = soloSlow;
                _dirty = true;
            }
            for (var i = 0; i < coop.Stats.Count; i++)
            {
                if (System.Math.Abs(Combined(i, coop.Stats[i].Cap) - _coopApplied[i]) > 1e-12)
                {
                    _dirty = true;
                    return;
                }
            }
        }

        private void ShareEnemy(bool inParty, CoopRules coop)
        {
            var active = _giver.EnemyDamageTaken > 0 || _giver.EnemyTimerSlow > 0;
            if (!inParty || !active)
            {
                _enemyResendTicks = 0;
                return;
            }
            if (_enemyResendTicks-- > 0)
                return;
            _enemyResendTicks = System.Math.Max(1, (int)System.Math.Round(coop.EnemyResendSeconds * GameLoop.TicksPerSecond)) - 1;
            _state.TraitEnemyEffects.Add(EnemyEffectId(_state.Class), _giver.EnemyDamageTaken, coop.EnemySeconds, System.Math.Min(1, _giver.EnemyTimerSlow));
        }

        private double Combined(int index, double cap)
        {
            var receiving = PartyRole.Receiving(_hero);
            var givers = 0;
            givers = AddGiver(givers, (int)_state.Class, _giver.Self[index]);
            for (var slot = 0; slot < _allies.Length; slot++)
            {
                var ally = _allies[slot];
                if (ally.Present)
                    givers = AddGiver(givers, ally.HeroClass, ally.Coop[index] * receiving);
            }
            var total = 0.0;
            for (var i = 0; i < givers; i++)
                total += _giverValue[i];
            return System.Math.Min(cap, total);
        }

        private int AddGiver(int givers, int heroClass, double value)
        {
            if (!(value > 0))
                return givers;
            for (var i = 0; i < givers; i++)
            {
                if (_giverClass[i] != heroClass)
                    continue;
                _giverValue[i] = System.Math.Max(_giverValue[i], value);
                return givers;
            }
            _giverClass[givers] = heroClass;
            _giverValue[givers] = value;
            return givers + 1;
        }

        private void AddCoop()
        {
            var stats = _rules.Coop.Stats;
            System.Array.Clear(_coopApplied, 0, _coopApplied.Length);
            for (var i = 0; i < stats.Count; i++)
            {
                var value = Combined(i, stats[i].Cap);
                _coopApplied[i] = value;
                if (value > 0)
                    _buffer.Add(new Modifier(stats[i].Stat, stats[i].Kind, stats[i].Negate ? -value : value));
            }
            if (_soloTaken > 0)
                _buffer.Add(new Modifier(CombatStats.Damage, ModifierKind.More, _soloTaken));
            if (_soloSlow > 0)
                _buffer.Add(new Modifier(CombatStats.BossTimerSpeed, ModifierKind.More, -_soloSlow));
        }

        private void TickAura(AllyAura aura)
        {
            if (!aura.Active)
                return;
            aura.TicksLeft--;
            if (!aura.Active)
            {
                aura.Gem = null;
                _dirty = true;
                return;
            }
            var own = OwnStrength(aura.Gem!);
            if (own == aura.OwnStrength)
                return;
            aura.OwnStrength = own;
            _dirty = true;
        }

        private void AddStats()
        {
            var stats = _rules.Stats;
            for (var i = 0; i < _applied.Length; i++)
                _applied[i] = 0;
            var receiving = PartyRole.Receiving(_hero);
            for (var i = 0; i < stats.Count; i++)
            {
                var best = OwnValue(i) * _rules.SelfShare;
                for (var slot = 0; slot < _allies.Length; slot++)
                {
                    if (_allies[slot].Present)
                        best = System.Math.Max(best, _allies[slot].Stats[i] * receiving);
                }
                _applied[i] = best;
                if (best > 0)
                    _buffer.Add(new Modifier(stats[i].To, stats[i].Kind, stats[i].Negate ? -best : best));
            }
        }

        private void AddAllyAuras()
        {
            var receiving = PartyRole.Receiving(_hero);
            for (var slot = 0; slot < _allies.Length; slot++)
            {
                var ally = _allies[slot];
                if (!ally.Present)
                    continue;
                for (var i = 0; i < ally.Auras.Length; i++)
                {
                    var aura = ally.Auras[i];
                    if (!aura.Active)
                        continue;
                    aura.OwnStrength = OwnStrength(aura.Gem!);
                    var stacks = SkillSystem.StacksOf(aura.Gem!);
                    var strength = (stacks != null ? System.Math.Min(stacks.MaxTotal, aura.Strength) : aura.Strength) * receiving;
                    aura.Suppressed = aura.OwnStrength > 0 && aura.OwnStrength >= strength;
                    if (aura.Suppressed || !Strongest(slot, aura))
                        continue;
                    var above = strength - aura.OwnStrength;
                    if (aura.Gem!.Skill?.Effect is AuraEffect effect)
                    {
                        for (var m = 0; m < effect.Modifiers.Count; m++)
                        {
                            var modifier = effect.Modifiers[m];
                            _buffer.Add(modifier.WithValue(modifier.Value * above));
                        }
                    }
                    else if (stacks != null)
                    {
                        _buffer.Add(new Modifier(stacks.PerStack.Stat, stacks.PerStack.Kind, above, stacks.PerStack.Tags));
                    }
                }
            }
        }

        public const int NoAlly = -1;

        public double Receiving => PartyRole.Receiving(_hero);

        public int StrongerAlly(int slot, AllyAura aura)
        {
            for (var other = 0; other < _allies.Length; other++)
            {
                if (other == slot || !_allies[other].Present)
                    continue;
                var auras = _allies[other].Auras;
                for (var i = 0; i < auras.Length; i++)
                {
                    if (!auras[i].Active || auras[i].Gem!.NetId != aura.Gem!.NetId)
                        continue;
                    if (auras[i].Strength > aura.Strength || (auras[i].Strength == aura.Strength && other < slot))
                        return other;
                }
            }
            return NoAlly;
        }

        private bool Strongest(int slot, AllyAura aura) => StrongerAlly(slot, aura) == NoAlly;

        private double OwnStrength(GemDef gem)
        {
            var skills = _state.Skills;
            for (var i = 0; i < skills.Count; i++)
            {
                var slot = skills[i];
                if (slot.Skill.NetId != gem.NetId)
                    continue;
                if (slot.AuraApplied)
                    return slot.AuraStrength;
                if (slot.Stacks > 0 && SkillSystem.StacksOf(gem) is { } stacks)
                    return System.Math.Min(stacks.MaxTotal, stacks.PerStack.Value * slot.Stacks);
            }
            return 0;
        }
    }
}
