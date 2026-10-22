using System;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class SkillSystem : IKeyHitListener
    {
        private readonly GameState _state;
        private readonly EventQueue _events;
        private readonly EnergySystem _energy;
        private readonly ClassMechanics _class;
        private readonly StatSheet _hero;

        public SkillSystem(GameState state, EventQueue events, EnergySystem energy, ClassMechanics classMechanics, StatSheet hero)
        {
            _state = state;
            _events = events;
            _energy = energy;
            _class = classMechanics;
            _hero = hero;
            foreach (var slot in _state.Skills)
            {
                if (slot.Active)
                    ApplyAura(slot);
            }
        }

        public int Equip(GemDef skill, GemDef? trigger = null)
        {
            if (skill.Kind != GemKind.Skill || skill.Skill == null)
                throw new ArgumentException($"{skill.Id} is not a skill gem.", nameof(skill));
            if (trigger != null && (trigger.Kind != GemKind.Trigger || trigger.Trigger == null))
                throw new ArgumentException($"{trigger.Id} is not a trigger gem.", nameof(trigger));
            _state.Skills.Add(new SkillSlot(skill, trigger));
            return _state.Skills.Count - 1;
        }

        public void UseGems(GemCatalog gems)
        {
            foreach (var slot in _state.Skills)
            {
                RemoveAura(slot);
                if (gems.TryGet(slot.Skill.Id, out var skill) && skill.Skill != null)
                    slot.Skill = skill;
                if (slot.Trigger != null && gems.TryGet(slot.Trigger.Id, out var trigger) && trigger.Trigger != null)
                    slot.Trigger = trigger;
                if (slot.Active)
                    ApplyAura(slot);
            }
        }

        public CastResult TryCast(int index, bool triggered = false)
        {
            if (index < 0 || index >= _state.Skills.Count)
                return CastResult.NoSkill;
            var slot = _state.Skills[index];
            var skill = slot.Def;
            if (skill.IsPassive)
                return CastResult.Passive;
            if (!slot.Ready)
                return CastResult.OnCooldown;
            if (skill.NoteCost > 0 && _state.Notes < skill.NoteCost)
                return CastResult.NotEnoughNotes;
            if (skill.EnergyCost > 0 && !_energy.TrySpend(skill.EnergyCost))
                return CastResult.NotEnoughEnergy;
            if (skill.NoteCost > 0)
                _class.SpendNotes(skill.NoteCost);

            var cooldown = skill.CooldownSeconds * (triggered && slot.Trigger != null ? slot.Trigger.Trigger!.CooldownMultiplier : 1);
            slot.CooldownTicks = Ticks(cooldown);
            slot.ActiveTicks = Ticks(skill.DurationSeconds);
            if (skill.Effect is ChainHitsEffect chain)
                slot.HitsLeft = chain.Hits;
            ApplyAura(slot);
            _events.Add(GameEvent.ForSkill(GameEventKind.SkillCast, _state.Tick, slot.Skill));
            return CastResult.Cast;
        }

        public void OnKeyHit(BigNumber damage)
        {
            foreach (var slot in _state.Skills)
            {
                if (slot.HitsLeft <= 0 || !(slot.Def.Effect is ChainHitsEffect chain))
                    continue;
                slot.HitsLeft--;
                _state.ChainDamage += damage * chain.NextEnemyShare;
            }
        }

        public void Tick()
        {
            for (var index = 0; index < _state.Skills.Count; index++)
            {
                var slot = _state.Skills[index];
                if (slot.CooldownTicks > 0)
                    slot.CooldownTicks--;
                if (slot.ActiveTicks > 0)
                {
                    slot.ActiveTicks--;
                    if (slot.ActiveTicks == 0)
                        RemoveAura(slot);
                }
                if (slot.Def.NoteCost > 0 && _state.Notes >= slot.Def.NoteCost)
                    TryCast(index);
            }
        }

        private void ApplyAura(SkillSlot slot)
        {
            if (slot.AuraApplied || !(slot.Def.Effect is AuraEffect aura))
                return;
            for (var i = 0; i < aura.Modifiers.Count; i++)
                _hero.Add(aura.Modifiers[i]);
            slot.AuraApplied = true;
            _events.Add(GameEvent.ForSkill(GameEventKind.AuraChanged, _state.Tick, slot.Skill));
        }

        private void RemoveAura(SkillSlot slot)
        {
            if (!slot.AuraApplied || !(slot.Def.Effect is AuraEffect aura))
                return;
            for (var i = 0; i < aura.Modifiers.Count; i++)
                _hero.Remove(aura.Modifiers[i]);
            slot.AuraApplied = false;
            _events.Add(GameEvent.ForSkill(GameEventKind.AuraChanged, _state.Tick, slot.Skill));
        }

        private static int Ticks(double seconds) => (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
    }
}
