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
        private readonly CombatSystem _combat;
        private readonly StatSheet _hero;

        public SkillSystem(GameState state, EventQueue events, EnergySystem energy, CombatSystem combat, StatSheet hero)
        {
            _state = state;
            _events = events;
            _energy = energy;
            _combat = combat;
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
            var noteCost = NoteCost(skill);
            if (noteCost > 0 && _state.Notes < noteCost)
                return CastResult.NotEnoughNotes;
            if (skill.EnergyCost > 0 && !_energy.TrySpend(skill.EnergyCost))
                return CastResult.NotEnoughEnergy;
            if (noteCost > 0)
                _combat.Class.SpendNotes(noteCost);

            var cooldown = skill.CooldownSeconds * (triggered && slot.Trigger != null ? slot.Trigger.Trigger!.CooldownMultiplier : 1);
            slot.CooldownTicks = Ticks(cooldown);
            slot.ActiveTicks = Ticks(skill.DurationSeconds * (skill.Effect is AuraEffect ? _hero.Value(CombatStats.AuraDuration, 1, StatTags.Aura) : 1));
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

        public void Tick(in EffectiveInput input, long idleTicksBefore)
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
                if (slot.Def.NoteCost > 0 && _state.Notes >= NoteCost(slot.Def))
                    TryCast(index);
                if (input.HasInput && slot.Def.Effect is IdleStrikeEffect strike)
                    ReleaseIdleStrike(slot, strike, idleTicksBefore * GameLoop.TickSeconds);
            }
        }

        public double IdleStrikeCharge(int index)
        {
            if (index < 0 || index >= _state.Skills.Count || !(_state.Skills[index].Def.Effect is IdleStrikeEffect strike) || !_state.Skills[index].Ready)
                return 0;
            var idleSeconds = _state.IdleTicks * GameLoop.TickSeconds;
            return idleSeconds < MinIdleSeconds(strike) ? 0 : ChargeMultiplier(strike, idleSeconds);
        }

        private void ReleaseIdleStrike(SkillSlot slot, IdleStrikeEffect strike, double idleSeconds)
        {
            if (!slot.Ready || idleSeconds < MinIdleSeconds(strike))
                return;
            slot.CooldownTicks = Ticks(slot.Def.CooldownSeconds);
            _events.Add(GameEvent.ForSkill(GameEventKind.SkillCast, _state.Tick, slot.Skill));
            _combat.SkillHit(_combat.BaseKeyDamage, ChargeMultiplier(strike, idleSeconds), slot.Skill.Tags);
        }

        public void RefreshAuras()
        {
            foreach (var slot in _state.Skills)
            {
                RemoveAura(slot);
                if (slot.Active)
                    ApplyAura(slot);
            }
        }

        private int NoteCost(SkillDef skill) => skill.NoteCost <= 0 ? 0 : System.Math.Min(skill.NoteCost, _combat.Class.NotesPerMelody);

        private double MinIdleSeconds(IdleStrikeEffect strike) => System.Math.Max(0, strike.MinIdleSeconds + _hero.Value(CombatStats.IdleStartSeconds, 0, StatTags.Idle));

        private static double ChargeMultiplier(IdleStrikeEffect strike, double idleSeconds) =>
            strike.ChargePerSecond * System.Math.Min(idleSeconds, strike.MaxChargeSeconds);

        private void ApplyAura(SkillSlot slot)
        {
            if (slot.AuraApplied || !(slot.Def.Effect is AuraEffect aura))
                return;
            var strength = _hero.Value(CombatStats.AuraEffect, 1, StatTags.Aura);
            for (var i = 0; i < aura.Modifiers.Count; i++)
            {
                var modifier = aura.Modifiers[i];
                slot.AuraModifiers.Add(new Modifier(modifier.Stat, modifier.Kind, modifier.Value * strength, modifier.Tags));
            }
            _hero.AddRange(slot.AuraModifiers);
            slot.AuraApplied = true;
            _events.Add(GameEvent.ForSkill(GameEventKind.AuraChanged, _state.Tick, slot.Skill));
        }

        private void RemoveAura(SkillSlot slot)
        {
            if (!slot.AuraApplied)
                return;
            for (var i = 0; i < slot.AuraModifiers.Count; i++)
                _hero.Remove(slot.AuraModifiers[i]);
            slot.AuraModifiers.Clear();
            slot.AuraApplied = false;
            _events.Add(GameEvent.ForSkill(GameEventKind.AuraChanged, _state.Tick, slot.Skill));
        }

        private static int Ticks(double seconds) => (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
    }
}
