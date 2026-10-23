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
        public const int LeftButtonSlot = 0;
        public const int RightButtonSlot = 1;

        private readonly ResourceSystem _resource;
        private readonly CombatSystem _combat;
        private readonly StatSheet _hero;

        public SkillSystem(GameState state, EventQueue events, ResourceSystem resource, CombatSystem combat, StatSheet hero)
        {
            _state = state;
            _events = events;
            _resource = resource;
            _combat = combat;
            _hero = hero;
            foreach (var slot in _state.Skills)
            {
                if (slot.Active)
                    ApplyAura(slot);
            }
        }

        public bool SharedEnemy { get; private set; }

        public void ShareEnemy(bool shared)
        {
            if (SharedEnemy == shared)
                return;
            SharedEnemy = shared;
            foreach (var slot in _state.Skills)
            {
                if (!(slot.Def.Effect is EnemyDebuffEffect))
                    continue;
                RemoveAura(slot);
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

        public void Unequip(int index)
        {
            RemoveAura(_state.Skills[index]);
            _state.Skills.RemoveAt(index);
        }

        public void Move(int from, int to)
        {
            var slot = _state.Skills[from];
            _state.Skills.RemoveAt(from);
            _state.Skills.Insert(to, slot);
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
            if (!Armed(slot))
                return CastResult.NoWeapon;
            if (!slot.Ready)
                return CastResult.OnCooldown;
            if (skill.ResourceCost > 0 && !_resource.TrySpend(skill.ResourceCost))
                return CastResult.NotEnoughResource;

            var cooldown = skill.CooldownSeconds * (triggered && slot.Trigger != null ? slot.Trigger.Trigger!.CooldownMultiplier : 1);
            slot.StartCooldown(Ticks(cooldown));
            slot.ActiveTicks = Ticks(skill.DurationSeconds * (skill.Effect is AuraEffect ? _hero.Value(CombatStats.AuraDuration, 1, StatTags.Aura) : 1));
            if (skill.Effect is ChainHitsEffect chain)
                slot.HitsLeft = chain.Hits;
            ApplyAura(slot);
            _events.Add(GameEvent.ForSkill(GameEventKind.SkillCast, _state.Tick, slot.Skill));
            if (skill.Effect is StrikeEffect strike)
                _combat.SkillHit(_combat.BaseKeyDamage, strike.Multiplier, slot.Skill.Tags);
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
                if (input.HasInput && slot.Def.Effect is IdleStrikeEffect strike)
                    ReleaseIdleStrike(slot, strike, idleTicksBefore * GameLoop.TickSeconds);
            }
        }

        public void CastFromButtons(in EffectiveInput input)
        {
            if (input.LeftClicks > 0)
                TryCast(LeftButtonSlot);
            if (input.HeavyClicks > 0)
                TryCast(RightButtonSlot);
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
            if (!slot.Ready || !Armed(slot) || idleSeconds < MinIdleSeconds(strike))
                return;
            slot.StartCooldown(Ticks(slot.Def.CooldownSeconds));
            _events.Add(GameEvent.ForSkill(GameEventKind.SkillCast, _state.Tick, slot.Skill));
            _combat.SkillHit(_combat.BaseKeyDamage, ChargeMultiplier(strike, idleSeconds), slot.Skill.Tags);
        }

        public void RefreshAuras()
        {
            foreach (var slot in _state.Skills)
            {
                RemoveAura(slot);
                if (slot.Active && Armed(slot))
                    ApplyAura(slot);
            }
        }

        public bool Armed(int index) => index >= 0 && index < _state.Skills.Count && Armed(_state.Skills[index]);

        public void RefreshArmed()
        {
            foreach (var slot in _state.Skills)
            {
                if (!slot.Active || Armed(slot))
                    continue;
                slot.ActiveTicks = 0;
                slot.HitsLeft = 0;
                RemoveAura(slot);
            }
        }

        private bool Armed(SkillSlot slot) => slot.Def.Weapon == null || _state.Weapons.Holds(slot.Def.Weapon);

        private double MinIdleSeconds(IdleStrikeEffect strike) => System.Math.Max(0, strike.MinIdleSeconds + _hero.Value(CombatStats.IdleStartSeconds, 0, StatTags.Idle));

        private static double ChargeMultiplier(IdleStrikeEffect strike, double idleSeconds) =>
            strike.ChargePerSecond * System.Math.Min(idleSeconds, strike.MaxChargeSeconds);

        private void ApplyAura(SkillSlot slot)
        {
            if (slot.AuraApplied)
                return;
            if (slot.Def.Effect is EnemyDebuffEffect debuff)
            {
                ApplyDebuff(slot, debuff);
                return;
            }
            if (!(slot.Def.Effect is AuraEffect aura))
                return;
            var strength = _hero.Value(CombatStats.AuraEffect, 1, StatTags.Aura);
            for (var i = 0; i < aura.Modifiers.Count; i++)
            {
                var modifier = aura.Modifiers[i];
                slot.AuraModifiers.Add(new Modifier(modifier.Stat, modifier.Kind, modifier.Value * strength, modifier.Tags));
            }
            slot.AuraStrength = strength;
            _hero.AddRange(slot.AuraModifiers);
            slot.AuraApplied = true;
            _events.Add(GameEvent.ForSkill(GameEventKind.AuraChanged, _state.Tick, slot.Skill));
            if (BoostsAuras(aura))
                RefreshOtherAuras(slot);
        }

        private void ApplyDebuff(SkillSlot slot, EnemyDebuffEffect debuff)
        {
            if (SharedEnemy || !slot.Active)
                return;
            slot.AuraModifiers.Add(new Modifier(CombatStats.Damage, ModifierKind.More, debuff.DamageTaken));
            slot.AuraStrength = 1;
            _hero.AddRange(slot.AuraModifiers);
            slot.AuraApplied = true;
        }

        private static bool BoostsAuras(AuraEffect aura)
        {
            for (var i = 0; i < aura.Modifiers.Count; i++)
            {
                if (aura.Modifiers[i].Stat.Equals(CombatStats.AuraEffect))
                    return true;
            }
            return false;
        }

        private void RefreshOtherAuras(SkillSlot booster)
        {
            foreach (var slot in _state.Skills)
            {
                if (ReferenceEquals(slot, booster) || !slot.AuraApplied || !(slot.Def.Effect is AuraEffect aura) || BoostsAuras(aura))
                    continue;
                RemoveAura(slot);
                ApplyAura(slot);
            }
        }

        private void RemoveAura(SkillSlot slot)
        {
            if (!slot.AuraApplied)
                return;
            for (var i = 0; i < slot.AuraModifiers.Count; i++)
                _hero.Remove(slot.AuraModifiers[i]);
            slot.AuraModifiers.Clear();
            slot.AuraApplied = false;
            if (slot.Def.Effect is EnemyDebuffEffect)
                return;
            _events.Add(GameEvent.ForSkill(GameEventKind.AuraChanged, _state.Tick, slot.Skill));
            if (slot.Def.Effect is AuraEffect aura && BoostsAuras(aura))
                RefreshOtherAuras(slot);
        }

        private static int Ticks(double seconds) => (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
    }
}
