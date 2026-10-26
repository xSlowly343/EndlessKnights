using System;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Weapons;
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
        public const double ReserveRefreshSeconds = 10;

        private readonly ResourceSystem _resource;
        private readonly CombatSystem _combat;
        private readonly StatSheet _hero;
        private WeaponCatalog _weapons;
        private long _seenKills;
        private SkillSlot? _lastCast;

        public SkillSystem(GameState state, EventQueue events, ResourceSystem resource, CombatSystem combat, StatSheet hero, WeaponCatalog weapons)
        {
            _state = state;
            _events = events;
            _resource = resource;
            _combat = combat;
            _hero = hero;
            _weapons = weapons;
            Minions = new MinionSystem(state, hero, combat, resource);
            Dots = new DotSystem(state, hero, combat);
            _seenKills = state.Kills;
            foreach (var slot in _state.Skills)
            {
                if (slot.Active)
                    ApplyAura(slot);
            }
        }

        public bool SharedEnemy { get; private set; }
        public MinionSystem Minions { get; }
        public DotSystem Dots { get; }

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

        public void UseWeapons(WeaponCatalog weapons)
        {
            _weapons = weapons;
            RefreshAuras();
        }

        public int Equip(GemDef skill, GemDef? trigger = null)
        {
            if (skill.Kind != GemKind.Skill || skill.Skill == null)
                throw new ArgumentException($"{skill.Id} is not a skill gem.", nameof(skill));
            if (trigger != null && (trigger.Kind != GemKind.Trigger || trigger.Trigger == null))
                throw new ArgumentException($"{trigger.Id} is not a trigger gem.", nameof(trigger));
            var slot = new SkillSlot(skill, trigger) { EquipTick = _state.Tick, ReadyTick = _state.Tick };
            _state.Skills.Add(slot);
            KeepReserved(slot);
            return _state.Skills.Count - 1;
        }

        public void Unequip(int index)
        {
            var slot = _state.Skills[index];
            RemoveAura(slot);
            ClearStacks(slot);
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
                ClearStacks(slot);
                if (gems.TryGet(slot.Skill.Id, out var skill) && skill.Skill != null)
                    slot.Skill = skill;
                if (slot.Trigger != null && gems.TryGet(slot.Trigger.Id, out var trigger) && trigger.Trigger != null)
                    slot.Trigger = trigger;
                if (slot.Active)
                    ApplyAura(slot);
            }
        }

        public bool IsNative(int index) => index >= 0 && index < _state.Skills.Count && IsNative(_state.Skills[index]);

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
            var native = IsNative(slot) ? skill.Native : NativeTrait.None;
            var wasFull = _resource.IsFull;
            if (!Pay(skill, native, out var spent))
                return CastResult.NotEnoughResource;

            slot.CastPower = CastPower(slot);
            slot.AuraPower = 1;
            if (!(skill.Effect is EchoEffect))
                _lastCast = slot;
            var cooldown = skill.CooldownSeconds * CooldownMultiplier() * (triggered && slot.Trigger != null ? slot.Trigger.Trigger!.CooldownMultiplier : 1);
            slot.StartCooldown(Ticks(cooldown));
            slot.LastCastTick = _state.Tick;
            if (skill.ArmOn != ArmOn.None)
            {
                slot.Primed = true;
                slot.PrimedSpent = spent;
                slot.PrimedWasFull = wasFull;
                slot.Progress = 0;
                _events.Add(GameEvent.ForSkill(GameEventKind.SkillCast, _state.Tick, slot.Skill));
                return CastResult.Cast;
            }
            Release(slot, native, spent, wasFull, announce: true);
            return CastResult.Cast;
        }

        private double CastPower(SkillSlot slot)
        {
            if (slot.Def.CooldownSeconds <= 0 || _state.Tick - slot.ReadyTick < Ticks(slot.Def.CooldownSeconds))
                return 1;
            var power = 1.0;
            foreach (var other in _state.Skills)
            {
                if (other.Def.Effect is ComposureEffect composure && Armed(other))
                    power = System.Math.Max(power, composure.Multiplier);
            }
            return power;
        }

        private void SkillHit(SkillSlot slot, double multiplier) =>
            _combat.SkillHit(_combat.BaseKeyDamage, multiplier * slot.CastPower, slot.Skill.Tags);

        private void Release(SkillSlot slot, NativeTrait native, double spent, bool wasFull, bool announce)
        {
            var skill = slot.Def;
            if (skill.Effect is EnemyDebuffEffect)
            {
                PrepareCurse(slot);
                RemoveAura(slot);
            }
            slot.ActiveTicks = Ticks(skill.DurationSeconds * DurationMultiplier(skill, native, wasFull));
            slot.ActiveTotalTicks = slot.ActiveTicks;
            if (skill.Effect is ChainHitsEffect chain)
                slot.HitsLeft = chain.Hits;
            if (skill.Effect is SpecialKeysEffect special && special.Charges > 0)
                slot.HitsLeft = special.Charges;
            if (skill.Effect is InputHitsEffect hits && hits.MaxHits > 0)
                slot.HitsLeft = hits.MaxHits;
            slot.HitCount = 0;
            slot.LastKeyTick = -1;
            slot.Progress = 0;
            slot.CircleProgress = 0;
            ApplyAura(slot);
            if (native.HoldDecaySeconds > 0)
                _resource.HoldDecay(native.HoldDecaySeconds);
            if (announce)
                _events.Add(GameEvent.ForSkill(GameEventKind.SkillCast, _state.Tick, slot.Skill));
            if (skill.Effect is StrikeEffect strike)
                Strike(slot, strike, native, spent);
            if (skill.Effect is ResourceGainEffect gain)
                _resource.Gain(gain.Amount);
            if (skill.Effect is SummonEffect summon)
                Minions.Place(summon, slot.Skill.Tags);
            if (skill.Effect is AddStacksEffect stacks)
                AddStacks(slot, stacks);
            if (skill.Effect is DetonateEffect detonate)
                Dots.Detonate(detonate);
            if (skill.Effect is WeakenNextEffect weaken)
                WeakenNext(weaken);
            if (skill.Effect is NextKillRewardEffect reward)
                ArmNextKillReward(reward);
            if (skill.Effect is EchoEffect echo)
                Repeat(echo);
        }

        private void AddStacks(SkillSlot slot, AddStacksEffect stacks)
        {
            if (stacks.Multiplier > 0)
                SkillHit(slot, stacks.Multiplier);
            Dots.Apply(stacks);
        }

        private void WeakenNext(WeakenNextEffect weaken)
        {
            _state.WeakenedSpawns = System.Math.Max(_state.WeakenedSpawns, weaken.Enemies);
            _state.WeakenShare = System.Math.Max(_state.WeakenShare, weaken.HpShare);
        }

        private void ArmNextKillReward(NextKillRewardEffect reward)
        {
            _state.NextKillReward = System.Math.Max(_state.NextKillReward, reward.Multiplier);
            _state.NextKillShardChance = reward.ShardChance;
            _state.NextKillShards = reward.Shards;
        }

        private void Repeat(EchoEffect echo)
        {
            var target = _lastCast;
            if (target == null || !_state.Skills.Contains(target) || !Armed(target))
                return;
            if (target.Def.Effect is StrikeEffect strike)
            {
                var power = target.CastPower;
                target.CastPower = echo.Power;
                Strike(target, strike, NativeTrait.None, 0);
                target.CastPower = power;
                return;
            }
            if (target.Active || target.Primed)
            {
                target.ActiveTicks += Ticks(target.Def.DurationSeconds * echo.Power);
                return;
            }
            target.CastPower = echo.Power;
            target.AuraPower = echo.Power;
            Release(target, NativeTrait.None, 0, false, announce: false);
        }

        private void TickPrimed(SkillSlot slot, in EffectiveInput input)
        {
            if (!Armed(slot))
            {
                slot.Primed = false;
                return;
            }
            var released = slot.Def.ArmOn == ArmOn.Flick ? input.Flicks > 0 : (slot.Progress += input.CircleTurns) >= 1;
            if (!released)
                return;
            slot.Primed = false;
            Release(slot, IsNative(slot) ? slot.Def.Native : NativeTrait.None, slot.PrimedSpent, slot.PrimedWasFull, announce: false);
        }

        private void TickInputHits(SkillSlot slot, InputHitsEffect hits, in EffectiveInput input)
        {
            if (!slot.Active || (hits.MaxHits > 0 && slot.HitsLeft <= 0))
                return;
            var multiplier = hits.FlickMultiplier * TakeHits(slot, hits, input.Flicks)
                + hits.ReversalMultiplier * TakeHits(slot, hits, input.Reversals);
            if (hits.PxPerHit > 0)
            {
                slot.Progress += input.MousePx;
                var steps = (int)System.Math.Floor(slot.Progress / hits.PxPerHit);
                slot.Progress -= steps * hits.PxPerHit;
                multiplier += hits.PxShare * steps;
            }
            if (hits.CircleMultiplier > 0)
            {
                slot.CircleProgress += input.CircleTurns;
                var turns = (int)System.Math.Floor(slot.CircleProgress + 1e-9);
                slot.CircleProgress -= turns;
                multiplier += hits.CircleMultiplier * turns;
            }
            if (multiplier > 0)
                SkillHit(slot, multiplier);
        }

        private static int TakeHits(SkillSlot slot, InputHitsEffect hits, int events)
        {
            if (events <= 0 || hits.MaxHits <= 0)
                return events;
            var taken = System.Math.Min(events, slot.HitsLeft);
            slot.HitsLeft -= taken;
            return taken;
        }

        private void TickDistanceStacks(SkillSlot slot, DistanceStacksEffect distance, in EffectiveInput input)
        {
            var buckets = slot.Buckets ??= new double[distance.WindowSeconds];
            var second = _state.Tick / GameLoop.TicksPerSecond;
            var index = (int)(second % buckets.Length);
            if (second != slot.BucketSecond)
            {
                slot.BucketSecond = second;
                slot.Progress -= buckets[index];
                buckets[index] = 0;
            }
            if (Armed(slot))
            {
                buckets[index] += input.MousePx;
                slot.Progress += input.MousePx;
            }
            var stacks = System.Math.Min(MaxStacks(distance.Stacks), (int)System.Math.Floor(System.Math.Max(0, slot.Progress) / distance.PxPerStack));
            SetStacks(slot, distance.Stacks, stacks);
        }

        private bool Pay(SkillDef skill, NativeTrait native, out double spent)
        {
            spent = skill.ResourceCost;
            if (native.SpendsAll)
            {
                if (_state.Resource < skill.ResourceCost)
                    return false;
                spent = _resource.SpendAll();
                return true;
            }
            return skill.ResourceCost <= 0 || _resource.TrySpend(skill.ResourceCost);
        }

        private double DurationMultiplier(SkillDef skill, NativeTrait native, bool wasFull)
        {
            var multiplier = skill.Effect is AuraEffect ? _hero.Value(CombatStats.AuraDuration, 1, StatTags.Aura) : 1;
            if (IsCurse(skill) && skill.DurationSeconds > 0)
                multiplier *= System.Math.Max(0, _hero.Value(CombatStats.CurseSeconds, skill.DurationSeconds)) / skill.DurationSeconds;
            if (wasFull && native.FullResourceDurationMultiplier > 0)
                multiplier *= native.FullResourceDurationMultiplier;
            return multiplier;
        }

        private void Strike(SkillSlot slot, StrikeEffect strike, NativeTrait native, double spent)
        {
            if (strike.CoinFlip && _state.Rng.NextDouble() < 0.5)
            {
                slot.StartCooldown(0);
                return;
            }
            if (Executes(strike))
            {
                _combat.DirectHit(_state.EnemyHp);
                return;
            }
            var power = slot.CastPower;
            var multiplier = power * (native.SpendsAll ? System.Math.Min(native.SpendAllMax, spent * native.SpendAllPerPoint) : strike.Multiplier);
            var queueMultiplier = native.SpendsAll ? multiplier : power * strike.QueueMultiplier;
            var tags = slot.Skill.Tags;
            var targets = strike.QueueTargets + (native.ExtraTargetWhileFamily != null && AnyActive(native.ExtraTargetWhileFamily) ? 1 : 0);
            if (strike.QueueTargets > 0 && !strike.SkipCurrent)
                targets += (int)System.Math.Max(0, System.Math.Round(_hero.Value(CombatStats.ChainTargets, 0, tags)));
            targets = System.Math.Min(targets, GameState.QueueDepth);
            if (targets > 0)
            {
                var damage = _combat.SkillDamage(_combat.BaseKeyDamage, queueMultiplier, tags);
                for (var i = 0; i < targets; i++)
                    _state.QueueDamage[i] += damage;
                if (native.WoundedSpawnResource > 0)
                    _state.WoundedSpawnResource = native.WoundedSpawnResource;
            }
            if (strike.SkipCurrent)
                return;
            var killsBefore = _state.Kills;
            if (strike.Arrow)
                multiplier *= System.Math.Max(0, _hero.Value(CombatStats.ArrowDamage, 1, tags));
            _combat.SkillHit(_combat.BaseKeyDamage, multiplier, tags, strike.CritChance);
            if (_state.Kills == killsBefore)
                return;
            _resource.Gain(spent * strike.KillRefundShare);
            if (native.RefreshFamily != null)
                RefreshActive(native.RefreshFamily);
        }

        private bool Executes(StrikeEffect strike) =>
            strike.ExecuteBelow > 0 && _state.EnemyAlive && _state.EnemyMaxHp.Sign > 0
            && (_state.EnemyHp / _state.EnemyMaxHp).ToDouble() < strike.ExecuteBelow;

        private bool AnyActive(string family)
        {
            foreach (var slot in _state.Skills)
            {
                if (slot.Active && slot.Def.Family == family)
                    return true;
            }
            return false;
        }

        private void RefreshActive(string family)
        {
            foreach (var slot in _state.Skills)
            {
                if (slot.Active && slot.Def.Family == family)
                    slot.ActiveTicks = System.Math.Max(slot.ActiveTicks, slot.ActiveTotalTicks);
            }
        }

        public void OnKeyHit(BigNumber damage)
        {
            foreach (var slot in _state.Skills)
            {
                if (slot.HitsLeft > 0 && slot.Def.Effect is ChainHitsEffect chain)
                {
                    slot.HitsLeft--;
                    _state.ChainDamage += damage * chain.NextEnemyShare;
                }
                if (slot.Active && !slot.Def.Reserves)
                    OnActiveKeyHit(slot, damage);
            }
        }

        private void OnActiveKeyHit(SkillSlot slot, BigNumber damage)
        {
            switch (slot.Def.Effect)
            {
                case StoreHitsEffect _:
                    slot.Stored += damage;
                    break;
                case QueueHitsEffect queue:
                    for (var i = 0; i < _state.QueueDamage.Length; i++)
                        _state.QueueDamage[i] += damage * queue.Share;
                    break;
                case StacksEffect stacks when stacks.Trigger == StackTrigger.KeyHit:
                    AddStacks(slot, stacks, 1);
                    break;
                case KeyRepeatEffect repeat:
                    if (++slot.HitCount % repeat.Every == 0)
                        _combat.DirectHit(damage * repeat.Multiplier);
                    break;
                case StreakStrikeEffect streak:
                    StreakHit(slot, streak);
                    break;
            }
            if (IsNative(slot))
                _resource.Gain(slot.Def.Native.ResourcePerKeyHit);
        }

        private void StreakHit(SkillSlot slot, StreakStrikeEffect streak)
        {
            if (slot.LastKeyTick >= 0 && _state.Tick - slot.LastKeyTick > Ticks(streak.MaxGapSeconds))
                slot.HitCount = 0;
            slot.LastKeyTick = _state.Tick;
            if (++slot.HitCount % streak.Hits == 0)
                SkillHit(slot, streak.Multiplier);
        }

        private void SpecialKeyHits(SkillSlot slot, SpecialKeysEffect special, in EffectiveInput input)
        {
            if (!Armed(slot))
                return;
            var presses = System.Math.Min(Presses(special.Key, input), (int)System.Math.Ceiling(input.Keys));
            if (special.Charges > 0)
            {
                presses = System.Math.Min(presses, slot.HitsLeft);
                slot.HitsLeft -= presses;
            }
            else if (!special.Passive && !slot.Active)
            {
                return;
            }
            if (presses > 0)
                SkillHit(slot, special.Bonus * presses);
        }

        private static int Presses(SpecialKey key, in EffectiveInput input)
        {
            switch (key)
            {
                case SpecialKey.Space:
                    return input.SpaceDowns;
                case SpecialKey.Backspace:
                    return input.BackspaceDowns;
                default:
                    return input.ComboDowns;
            }
        }

        private void TickFlow(SkillSlot slot, FlowEffect flow)
        {
            if (!Armed(slot) || _state.KeylessTicks > Ticks(flow.MaxGapSeconds))
            {
                slot.FlowStartTick = -1;
                RemoveAura(slot);
                return;
            }
            if (slot.FlowStartTick < 0)
            {
                if (_state.KeylessTicks == 0)
                    slot.FlowStartTick = _state.Tick;
                return;
            }
            if (slot.AuraApplied || _state.Tick - slot.FlowStartTick < Ticks(flow.AfterSeconds))
                return;
            for (var i = 0; i < flow.Modifiers.Count; i++)
                slot.AuraModifiers.Add(flow.Modifiers[i]);
            _hero.AddRange(slot.AuraModifiers);
            slot.AuraApplied = true;
        }

        public void Tick(in EffectiveInput input, long idleTicksBefore)
        {
            var kills = (int)System.Math.Min(int.MaxValue, System.Math.Max(0, _state.Kills - _seenKills));
            _seenKills = _state.Kills;
            if (kills > 0 && !SharedEnemy && !CursesFlow())
                EndDebuffs();
            CountCurseFlicks(input);
            for (var index = 0; index < _state.Skills.Count; index++)
            {
                var slot = _state.Skills[index];
                if (slot.CooldownTicks > 0 && --slot.CooldownTicks == 0)
                    slot.ReadyTick = _state.Tick;
                KeepReserved(slot);
                if (slot.ActiveTicks > 0)
                {
                    slot.ActiveTicks--;
                    if (slot.ActiveTicks == 0)
                        End(slot);
                }
                if (slot.Def.Effect is StacksEffect stacks && stacks.Passive)
                    TickKillStacks(slot, stacks, kills);
                if (slot.Def.Effect is SpecialKeysEffect special)
                    SpecialKeyHits(slot, special, input);
                if (slot.Def.Effect is FlowEffect flow)
                    TickFlow(slot, flow);
                if (slot.Primed)
                    TickPrimed(slot, input);
                if (slot.Def.Effect is InputHitsEffect inputHits)
                    TickInputHits(slot, inputHits, input);
                if (slot.Def.Effect is DistanceStacksEffect distance)
                    TickDistanceStacks(slot, distance, input);
                if (slot.Def.Effect is BossHuntEffect hunt)
                    TickBossHunt(slot, hunt);
                if (slot.Def.Effect is PassiveEffect passive)
                    SetPassive(slot, passive.Modifiers, passive.PerPartyMember ? _state.PartyMembers : 1);
                if (slot.Def.Effect is BreakBonusEffect breakBonus)
                    TickBreakBonus(slot, breakBonus, input, idleTicksBefore);
                if (slot.Active && slot.Def.Effect is AuraEffect keyless && keyless.KeylessStrikeSeconds > 0)
                    TickKeylessStrike(slot, keyless);
                if (slot.Active && slot.Def.Effect is TrailEffect)
                    slot.Progress += input.MousePx;
                if (input.HasInput && slot.Def.Effect is IdleStrikeEffect strike)
                    ReleaseIdleStrike(slot, strike, idleTicksBefore * GameLoop.TickSeconds);
                if (slot.Active && slot.Def.Effect is CurseFlowEffect flowing)
                    TickCurseFlow(flowing);
                if (slot.Active && slot.Def.Effect is AddStacksEffect stackFlow && stackFlow.PerSecond > 0)
                    Dots.Flow(stackFlow);
            }
        }

        public double DebuffStrength(GemDef gem)
        {
            if (!(gem.Skill?.Effect is EnemyDebuffEffect debuff))
                return 0;
            var strength = debuff.DamageTaken * System.Math.Max(0, _hero.Value(CombatStats.EnemyDebuffEffect, 1));
            return IsCurse(gem.Skill) ? strength * System.Math.Max(0, _hero.Value(CombatStats.CurseEffect, 1)) : strength;
        }

        public int ActiveCurses
        {
            get
            {
                var count = 0;
                foreach (var slot in _state.Skills)
                {
                    if (slot.Active && IsCurse(slot.Def))
                        count++;
                }
                return count;
            }
        }

        private int CurseLimit => _combat.Curses.MaxOnEnemy + (int)System.Math.Max(0, System.Math.Round(_hero.Value(CombatStats.CursesOnEnemy, 0)));

        private static bool IsCurse(SkillDef skill)
        {
            if (!(skill.Effect is EnemyDebuffEffect))
                return false;
            for (var i = 0; i < skill.Marks.Count; i++)
            {
                if (skill.Marks[i] == CurseMark)
                    return true;
            }
            return false;
        }

        private bool CursesFlow()
        {
            foreach (var slot in _state.Skills)
            {
                if (slot.Active && slot.Def.Effect is CurseFlowEffect)
                    return true;
            }
            return false;
        }

        private void EndDebuffs()
        {
            foreach (var slot in _state.Skills)
            {
                if (!slot.Active || !(slot.Def.Effect is EnemyDebuffEffect))
                    continue;
                slot.ActiveTicks = 0;
                End(slot);
            }
        }

        private void CountCurseFlicks(in EffectiveInput input)
        {
            if (_state.Class != HeroClass.Voodoo || input.Flicks <= 0)
                return;
            _state.CurseFlicks = System.Math.Min(_combat.Curses.FlicksToMax, _state.CurseFlicks + input.Flicks);
        }

        private void PrepareCurse(SkillSlot slot)
        {
            slot.CursePower = 1;
            if (!IsCurse(slot.Def))
                return;
            var rules = _combat.Curses;
            slot.CursePower = 1 + System.Math.Min(rules.FlickMax, _state.CurseFlicks * rules.FlickBonus) * System.Math.Max(0, _hero.Value(CombatStats.FlickCurseBonus, 1));
            _state.CurseFlicks = 0;
            if (slot.Active)
                return;
            while (ActiveCurses >= CurseLimit)
                EndOldestCurse();
        }

        private void EndOldestCurse()
        {
            SkillSlot? oldest = null;
            foreach (var slot in _state.Skills)
            {
                if (slot.Active && IsCurse(slot.Def) && (oldest == null || slot.LastCastTick < oldest.LastCastTick))
                    oldest = slot;
            }
            if (oldest == null)
                return;
            oldest.ActiveTicks = 0;
            End(oldest);
        }

        private void TickCurseFlow(CurseFlowEffect flow)
        {
            if (flow.HitsPerSecond <= 0 || _state.Tick % GameLoop.TicksPerSecond != 0)
                return;
            var curses = ActiveCurses;
            if (curses > 0)
                _combat.SkillHit(_combat.BaseKeyDamage, curses * flow.HitsPerSecond * System.Math.Max(0, _hero.Value(CombatStats.CurseDamage, 1)), StatTags.Skill | StatTags.DoT);
        }

        private void TickBossHunt(SkillSlot slot, BossHuntEffect hunt)
        {
            var bonus = !_state.FightingBoss ? 0
                : _state.BossTicksLeft > 0 && _state.BossTicksLeft <= Ticks(hunt.LastSeconds) ? hunt.LastBonus : hunt.Bonus;
            SetPassive(slot, hunt.Modifiers, bonus);
        }

        private void TickBreakBonus(SkillSlot slot, BreakBonusEffect bonus, in EffectiveInput input, long idleTicksBefore)
        {
            if (input.HasInput && idleTicksBefore >= Ticks(bonus.BreakSeconds))
                slot.FlowStartTick = _state.Tick;
            var on = slot.FlowStartTick >= 0 && _state.Tick - slot.FlowStartTick < Ticks(bonus.Seconds);
            SetPassive(slot, bonus.Modifiers, on ? 1 : 0);
        }

        private void TickKeylessStrike(SkillSlot slot, AuraEffect aura)
        {
            var every = Ticks(aura.KeylessStrikeSeconds);
            if (every > 0 && _state.KeylessTicks > 0 && _state.KeylessTicks % every == 0)
                SkillHit(slot, aura.KeylessStrikeMultiplier);
        }

        private void SetPassive(SkillSlot slot, System.Collections.Generic.IReadOnlyList<Modifier> modifiers, double scale)
        {
            if (!Armed(slot))
                scale = 0;
            if (scale == slot.PassiveScale && (scale <= 0 || slot.AuraApplied))
                return;
            RemoveAura(slot);
            slot.PassiveScale = scale;
            if (scale <= 0)
                return;
            for (var i = 0; i < modifiers.Count; i++)
                slot.AuraModifiers.Add(new Modifier(modifiers[i].Stat, modifiers[i].Kind, modifiers[i].Value * scale, modifiers[i].Tags));
            _hero.AddRange(slot.AuraModifiers);
            slot.AuraApplied = true;
        }

        private void End(SkillSlot slot)
        {
            RemoveAura(slot);
            if (slot.Def.Effect is AuraEffect aura && aura.FocusLockAfterSeconds > 0)
                _state.FocusLockTicks = Ticks(aura.FocusLockAfterSeconds);
            if (slot.Def.Effect is StoreHitsEffect store)
            {
                var stored = slot.Stored;
                slot.Stored = BigNumber.Zero;
                _combat.DirectHit(stored * store.ReleaseShare);
            }
            if (slot.Def.Effect is StacksEffect stacks && !stacks.Passive)
                ClearStacks(slot);
            if (slot.Def.Effect is TrailEffect trail)
            {
                var multiplier = slot.Progress / trail.PxPerMultiplier;
                slot.Progress = 0;
                if (multiplier > 0)
                    SkillHit(slot, multiplier);
            }
        }

        private void TickKillStacks(SkillSlot slot, StacksEffect stacks, int kills)
        {
            if (!Armed(slot))
            {
                ClearStacks(slot);
                return;
            }
            if (kills > 0)
            {
                AddStacks(slot, stacks, kills);
                return;
            }
            if (slot.Stacks == 0 || HoldsStacks(slot))
                return;
            slot.StackTicks--;
            if (slot.StackTicks <= 0)
                ClearStacks(slot);
        }

        private bool HoldsStacks(SkillSlot slot) =>
            slot.Def.Native.HoldsStacks && IsNative(slot) && _state.Resource > slot.Def.Native.HoldStacksAboveResource;

        private void AddStacks(SkillSlot slot, StacksEffect stacks, int count)
        {
            slot.StackTicks = Ticks(stacks.StackSeconds);
            SetStacks(slot, stacks, System.Math.Min(MaxStacks(stacks), slot.Stacks + count));
        }

        private void SetStacks(SkillSlot slot, StacksEffect stacks, int next)
        {
            if (next == slot.Stacks)
                return;
            if (slot.Stacks > 0)
                _hero.Remove(slot.StackModifier);
            slot.Stacks = next;
            if (next == 0)
                return;
            slot.StackModifier = stacks.For(next);
            _hero.Add(slot.StackModifier);
        }

        private static int MaxStacks(StacksEffect stacks) =>
            stacks.PerStack.Value <= 0 ? 0 : (int)System.Math.Ceiling(stacks.MaxTotal / stacks.PerStack.Value - 1e-9);

        private void ClearStacks(SkillSlot slot)
        {
            if (slot.Stacks > 0)
                _hero.Remove(slot.StackModifier);
            slot.Stacks = 0;
            slot.StackTicks = 0;
        }

        private void KeepReserved(SkillSlot slot)
        {
            if (!slot.Def.Reserves || !Armed(slot))
                return;
            slot.ActiveTicks = Ticks(ReserveRefreshSeconds);
            slot.ActiveTotalTicks = slot.ActiveTicks;
            ApplyAura(slot);
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
            slot.StartCooldown(Ticks(slot.Def.CooldownSeconds * CooldownMultiplier()));
            slot.LastCastTick = _state.Tick;
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
                if (Armed(slot))
                {
                    KeepReserved(slot);
                    continue;
                }
                ClearStacks(slot);
                if (!slot.Active)
                    continue;
                slot.ActiveTicks = 0;
                slot.HitsLeft = 0;
                slot.Stored = BigNumber.Zero;
                RemoveAura(slot);
            }
        }

        private bool Armed(SkillSlot slot) => slot.Def.Weapon == null || _state.Weapons.Holds(slot.Def.Weapon);

        private bool IsNative(SkillSlot slot) =>
            slot.Def.Weapon != null && _weapons.TryGetType(slot.Def.Weapon, out var weapon) && weapon.HeroClass == _state.Class;

        private double CooldownMultiplier() => System.Math.Max(0.1, _hero.Value(CombatStats.SkillCooldown, 1));

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
            var aura = slot.Def.Effect as AuraEffect;
            var native = IsNative(slot) ? slot.Def.Native.Modifiers : Array.Empty<Modifier>();
            if (aura == null && native.Count == 0)
                return;
            var strength = aura == null ? 1 : _hero.Value(CombatStats.AuraEffect, 1, StatTags.Aura);
            for (var i = 0; aura != null && i < aura.Modifiers.Count; i++)
            {
                var modifier = aura.Modifiers[i];
                slot.AuraModifiers.Add(new Modifier(modifier.Stat, modifier.Kind, modifier.Value * strength * slot.AuraPower, modifier.Tags));
            }
            for (var i = 0; i < native.Count; i++)
                slot.AuraModifiers.Add(native[i]);
            slot.AuraStrength = strength;
            _hero.AddRange(slot.AuraModifiers);
            slot.AuraApplied = true;
            Reserve(slot, aura);
            if (aura == null)
                return;
            _events.Add(GameEvent.ForSkill(GameEventKind.AuraChanged, _state.Tick, slot.Skill));
            if (BoostsAuras(aura))
                RefreshOtherAuras(slot);
        }

        private void Reserve(SkillSlot slot, AuraEffect? aura)
        {
            if (aura == null || !aura.Reserves)
                return;
            slot.ReservedApplied = aura.Reserve;
            _state.ReservedResource += aura.Reserve;
            _resource.ClampToMax();
        }

        private void ApplyDebuff(SkillSlot slot, EnemyDebuffEffect debuff)
        {
            if (SharedEnemy || !slot.Active)
                return;
            slot.AuraModifiers.Add(new Modifier(CombatStats.Damage, ModifierKind.More, DebuffStrength(slot.Skill) * slot.CursePower));
            if (debuff.BossTimerSlow > 0)
                slot.AuraModifiers.Add(new Modifier(CombatStats.BossTimerSpeed, ModifierKind.More, -debuff.BossTimerSlow));
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
            slot.PassiveScale = 0;
            _state.ReservedResource = System.Math.Max(0, _state.ReservedResource - slot.ReservedApplied);
            slot.ReservedApplied = 0;
            if (!(slot.Def.Effect is AuraEffect aura))
                return;
            _events.Add(GameEvent.ForSkill(GameEventKind.AuraChanged, _state.Tick, slot.Skill));
            if (BoostsAuras(aura))
                RefreshOtherAuras(slot);
        }

        private const string CurseMark = "curse";

        private static int Ticks(double seconds) => (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
    }
}
