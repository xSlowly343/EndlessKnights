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
        private PartyBuffRules _rules;
        private bool _dirty;

        public PartyBuffs(PartyBuffRules rules, GameState state, StatSheet hero)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _layer = new ModifierLayer(hero);
            for (var i = 0; i < _allies.Length; i++)
                _allies[i] = new AllyOffer();
        }

        public IReadOnlyList<Modifier> Modifiers => _layer.Applied;

        public PartyBuffRules Rules => _rules;

        public AllyOffer Ally(int slot) => _allies[slot];

        public double OwnValue(int index) => _rules.Stats[index].ValueOf(_hero);

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
            if (_dirty)
                Rebuild();
        }

        public void Rebuild()
        {
            _dirty = false;
            _buffer.Clear();
            AddStats();
            AddAllyAuras();
            _layer.Set(_buffer);
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
            var suppressed = OwnAuraActive(aura.Gem!);
            if (suppressed == aura.Suppressed)
                return;
            aura.Suppressed = suppressed;
            _dirty = true;
        }

        private void AddStats()
        {
            var stats = _rules.Stats;
            for (var i = 0; i < _applied.Length; i++)
                _applied[i] = 0;
            for (var i = 0; i < stats.Count; i++)
            {
                var best = stats[i].ValueOf(_hero) * _rules.SelfShare;
                for (var slot = 0; slot < _allies.Length; slot++)
                {
                    if (_allies[slot].Present)
                        best = System.Math.Max(best, _allies[slot].Stats[i]);
                }
                _applied[i] = best;
                if (best > 0)
                    _buffer.Add(new Modifier(stats[i].To, stats[i].Kind, best));
            }
        }

        private void AddAllyAuras()
        {
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
                    aura.Suppressed = OwnAuraActive(aura.Gem!);
                    if (aura.Suppressed || !Strongest(slot, aura) || !(aura.Gem!.Skill?.Effect is AuraEffect effect))
                        continue;
                    for (var m = 0; m < effect.Modifiers.Count; m++)
                    {
                        var modifier = effect.Modifiers[m];
                        _buffer.Add(new Modifier(modifier.Stat, modifier.Kind, modifier.Value * aura.Strength, modifier.Tags));
                    }
                }
            }
        }

        private bool Strongest(int slot, AllyAura aura)
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
                        return false;
                }
            }
            return true;
        }

        private bool OwnAuraActive(GemDef gem)
        {
            var skills = _state.Skills;
            for (var i = 0; i < skills.Count; i++)
            {
                if (skills[i].AuraApplied && skills[i].Skill.NetId == gem.NetId)
                    return true;
            }
            return false;
        }
    }
}
