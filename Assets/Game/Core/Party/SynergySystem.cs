using System.Collections.Generic;
using Game.Core.Content.Synergy;
using Game.Core.Sim;
using Game.Core.Stats;

namespace Game.Core.Party
{
    public sealed class SynergySystem
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly ModifierLayer _layer;
        private readonly List<Modifier> _buffer = new List<Modifier>(32);
        private readonly HeroClass[] _allyClass = new HeroClass[PartyRules.MaxSlots];
        private readonly bool[] _allyPresent = new bool[PartyRules.MaxSlots];
        private readonly bool[] _allyFighting = new bool[PartyRules.MaxSlots];
        private SynergyRules _rules;
        private int _roles;
        private double _effect = double.NaN;

        public SynergySystem(SynergyRules rules, GameState state, StatSheet hero)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _layer = new ModifierLayer(hero, ModifierPool.Party);
        }

        public IReadOnlyList<Modifier> Modifiers => _layer.Applied;

        public SynergyRules Rules => _rules;
        public int AllyRoles => _roles;

        public bool RoleActive(int index) => (_roles & (1 << index)) != 0;

        public int OwnRole => _rules.RoleMaskOf(_state.Class);

        public int AllyRoleOf(int slot) => slot >= 0 && slot < PartyRules.MaxSlots && _allyPresent[slot] ? _rules.RoleMaskOf(_allyClass[slot]) : 0;

        public int PartyRoles
        {
            get
            {
                var roles = OwnRole;
                for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
                    roles |= AllyRoleOf(slot);
                return roles;
            }
        }

        public void UseRules(SynergyRules rules)
        {
            _rules = rules;
            _roles = 0;
            Rebuild();
        }

        public bool OwnFighting { get; set; } = true;

        public void SetAlly(int slot, bool present, HeroClass heroClass, bool fighting = true)
        {
            _allyPresent[slot] = present;
            _allyFighting[slot] = present && fighting;
            _allyClass[slot] = heroClass;
        }

        public void ClearAllies()
        {
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
                SetAlly(slot, false, default);
            OwnFighting = true;
        }

        public void Tick()
        {
            var roles = OwnFighting ? FightingAllyRoles() : 0;
            var effect = RoleBuffEffect;
            if (roles == _roles && effect == _effect)
                return;
            _roles = roles;
            _effect = effect;
            Rebuild();
        }

        private int FightingAllyRoles()
        {
            var roles = 0;
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                if (_allyFighting[slot])
                    roles |= _rules.RoleMaskOf(_allyClass[slot]);
            }
            return roles;
        }

        private void Rebuild()
        {
            _buffer.Clear();
            var effect = RoleBuffEffect;
            for (var i = 0; i < _rules.RoleBuffs.Count; i++)
            {
                if (RoleActive(i))
                    Add(_rules.RoleBuffs[i], effect);
            }
            _layer.Set(_buffer);
        }

        private double RoleBuffEffect => System.Math.Max(0, _hero.Value(CombatStats.RoleBuffEffect, 1));

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
