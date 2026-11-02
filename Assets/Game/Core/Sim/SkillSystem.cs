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
        private long _seenPoisonTicks;
        private long _seenWeakenedKills;
        private SkillSlot? _lastCast;
        private long _lastKeyHitTick = long.MinValue / 2;
        private long _keyGapTicks;
        private BigNumber _lastKeyHitDamage;

        public SkillSystem(GameState state, EventQueue events, ResourceSystem resource, CombatSystem combat, StatSheet hero, WeaponCatalog weapons)
        {
            _state = state;
            _events = events;
            _resource = resource;
            _combat = combat;
            _hero = hero;
            _weapons = weapons;
            Dots = new DotSystem(state, hero, combat);
            Minions = new MinionSystem(state, hero, combat, Dots);
            _seenKills = state.Kills;
            foreach (var slot in _state.Skills)
            {
                if (slot.Active)
                    ApplyAura(slot);
            }
        }

        public bool SharedEnemy { get; private set; }
        public GemLevels? Levels { get; set; }

        public double LevelPower(GemDef gem) => (Levels == null ? 1 : Levels.EffectMultiplier(_state.LevelOf(gem), LevelBonus)) * ColorPower(gem);

        private int LevelBonus => (int)System.Math.Floor(System.Math.Max(0, _hero.Value(CombatStats.GemLevelBonus, 0)) + 1e-9);

        public double ColorPower(GemDef gem) => gem.Color switch
        {
            GemColor.Red => System.Math.Max(0, _hero.Value(CombatStats.RedGemEffect, 1)),
            GemColor.Green => System.Math.Max(0, _hero.Value(CombatStats.GreenGemEffect, 1)),
            GemColor.Blue => System.Math.Max(0, _hero.Value(CombatStats.BlueGemEffect, 1)),
            _ => 1
        };
        public MinionSystem Minions { get; }
        public DotSystem Dots { get; }

        public void ShareEnemy(bool shared)
        {
            if (SharedEnemy == shared)
                return;
            SharedEnemy = shared;
            _state.SharedEnemy = shared;
        }

        public void UseWeapons(WeaponCatalog weapons)
        {
            _weapons = weapons;
            RefreshAuras();
        }

        public int Equip(GemDef skill, SkillSlotRole? role = null)
        {
            if (skill.Kind != GemKind.Skill || skill.Skill == null)
                throw new ArgumentException($"{skill.Id} is not a skill gem.", nameof(skill));
            var slot = new SkillSlot(skill)
            {
                EquipTick = _state.Tick,
                ReadyTick = _state.Tick,
                KeyHitsAtCast = _state.StrikeHits,
                Role = role ?? SkillLoadout.FreeRole(_state.Skills, skill) ?? SkillSlotRole.Auto
            };
            _state.ProgressOf(skill);
            ApplyBranches(slot);
            _state.Skills.Add(slot);
            KeepReserved(slot);
            return _state.Skills.Count - 1;
        }

        public void Unequip(int index)
        {
            var slot = _state.Skills[index];
            RemoveAura(slot);
            ClearStacks(slot);
            DropStanding(slot);
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
                DropStanding(slot);
                if (gems.TryGet(slot.Skill.Id, out var skill) && skill.Skill != null)
                    slot.Skill = skill;
                ApplyBranches(slot);
                if (slot.Active)
                    ApplyAura(slot);
            }
        }

        public void RefreshBranches()
        {
            foreach (var slot in _state.Skills)
            {
                var combo = ComboOf(slot.Skill);
                if (ReferenceEquals(slot.Variant, slot.Skill.VariantOf(combo)))
                    continue;
                RemoveAura(slot);
                ClearStacks(slot);
                DropStanding(slot);
                ApplyBranches(slot);
                if (slot.Active)
                    ApplyAura(slot);
            }
        }

        private void ApplyBranches(SkillSlot slot)
        {
            var combo = ComboOf(slot.Skill);
            slot.Variant = slot.Skill.VariantOf(combo);
            slot.BranchDamage = slot.Skill.BranchDamageOf(combo);
        }

        public int ComboOf(GemDef gem) =>
            _state.GemProgress.TryGetValue(gem.Id, out var progress) ? SkillBranches.ComboOf(gem.Branches, progress.Branches, progress.Level) : SkillBranches.Plain;

        public bool IsNative(int index) => index >= 0 && index < _state.Skills.Count && IsNative(_state.Skills[index]);

        public static StacksEffect? StacksOf(GemDef gem) => gem.Skill?.Effect switch
        {
            StacksEffect stacks => stacks,
            StrikeEffect strike => strike.Stacks,
            _ => null
        };

        public bool SharesStacks(int index, out StacksEffect stacks)
        {
            stacks = null!;
            if (index < 0 || index >= _state.Skills.Count)
                return false;
            var slot = _state.Skills[index];
            var found = StacksOf(slot.Skill);
            if (found == null || slot.Stacks <= 0 || !(found.Party || (IsNative(slot) && slot.Def.Native.PartyStacks)))
                return false;
            stacks = found;
            return true;
        }

        public CastResult TryCast(int index)
        {
            if (index < 0 || index >= _state.Skills.Count)
                return CastResult.NoSkill;
            if (_state.Chronicle.SkillsLocked)
                return CastResult.TrialForbids;
            var slot = _state.Skills[index];
            var skill = slot.Def;
            if (skill.IsPassive)
                return CastResult.Passive;
            if (_combat.Runedancer.Forbids(slot.Skill) || _combat.Lanternbearer.Forbids(slot.Skill))
                return CastResult.TraitCastsIt;
            var spare = !slot.Ready && slot.SpareCharges > 0;
            if (!slot.Ready && !spare)
                return CastResult.OnCooldown;
            var native = IsNative(slot) ? skill.Native : NativeTrait.None;
            var wasFull = _resource.IsFull;
            slot.WhenAtCast = WhenHolds(native);
            var beacon = _state.Boons.BeaconCharges > 0 && !(skill.Effect is AuraEffect);
            var spent = 0.0;
            var elixir = !beacon && _state.Boons.FreeSkills && !(skill.Effect is AuraEffect);
            if (!beacon && !elixir && !Pay(slot, native, slot.WhenAtCast, out spent))
                return CastResult.NotEnoughResource;

            var supportPower = SupportEffects.Power(slot, _state);
            var ownPower = OwnPower(slot);
            slot.CastPower = CastPower(slot) * SuperCast() * LevelPower(slot.Skill) * supportPower * ownPower * slot.BranchDamage;
            slot.AuraPower = supportPower * SupportEffects.AuraPower(slot) * ownPower * slot.BranchDamage;
            if (beacon)
                slot.CastPower *= 1 + _state.Boons.BeaconPower;
            if (skill.Role == SkillRole.Fast)
                _resource.Gain(_hero.Value(CombatStats.ResourcePerFastSkill, 0));
            if (!(skill.Effect is EchoEffect))
                _lastCast = slot;
            var cooldown = skill.CooldownSeconds * CooldownMultiplier() * SupportEffects.CooldownMultiplier(slot);
            if (beacon)
                TakeBeacon();
            else if (spare)
                slot.SpareCharges--;
            else
                slot.StartCooldown(Ticks(cooldown));
            slot.LastCastTick = _state.Tick;
            ArmTwin(slot);
            if (SupportEffects.Finish(slot) != null)
                slot.FinishHits = 0;
            if (skill.ArmOn != ArmOn.None)
            {
                slot.Primed = true;
                slot.PrimedSpent = spent;
                slot.PrimedWasFull = wasFull;
                slot.Progress = 0;
                _events.Add(GameEvent.ForSkill(GameEventKind.SkillCast, _state.Tick, slot.Skill));
                return CastResult.Cast;
            }
            var outer = _combat.Supporting;
            _combat.Supporting = slot;
            Release(slot, native, spent, wasFull, announce: true);
            _combat.Supporting = outer;
            return CastResult.Cast;
        }

        private void TakeBeacon()
        {
            if (--_state.Boons.BeaconCharges <= 0)
                _state.Boons.BeaconPower = 0;
        }

        private double CastPower(SkillSlot slot)
        {
            if (slot.Def.CooldownSeconds <= 0 || _state.Tick - slot.ReadyTick < Ticks(slot.Def.CooldownSeconds))
                return 1;
            var power = 1.0;
            foreach (var other in _state.Skills)
            {
                if (other.Def.Effect is ComposureEffect composure)
                    power = System.Math.Max(power, composure.Multiplier);
            }
            return power;
        }

        private double OwnPower(SkillSlot slot) => System.Math.Max(0, _hero.Value(slot.Skill.PowerStat, 1));

        private double SuperCast()
        {
            _state.SkillCasts++;
            var every = (long)System.Math.Round(_hero.Value(CombatStats.SkillSuperEvery, 0, StatTags.Skill));
            if (every < 1 || _state.SkillCasts % every != 0)
                return 1;
            return System.Math.Max(1, _hero.Value(CombatStats.SkillSuperMultiplier, 1, StatTags.Skill));
        }

        private void SkillHit(SkillSlot slot, double multiplier) =>
            _combat.SkillHit(_combat.BaseKeyDamage, multiplier * slot.CastPower, slot.Skill.Tags);

        private void Release(SkillSlot slot, NativeTrait native, double spent, bool wasFull, bool announce)
        {
            var skill = slot.Def;
            slot.ActiveTicks = Ticks(skill.DurationSeconds * DurationMultiplier(skill, native, wasFull, slot.WhenAtCast) * SupportEffects.DurationMultiplier(slot))
                + Ticks(System.Math.Max(0, spent - Cost(slot)) * native.ExtraSecondsPerPoint)
                + (skill.DurationSeconds > 0 ? Ticks(System.Math.Max(0, _hero.Value(slot.Skill.DurationStat, 0))) : 0);
            slot.ActiveTotalTicks = slot.ActiveTicks;
            slot.LingerTicks = 0;
            slot.WindPx = 0;
            slot.WindTicks = 0;
            slot.FlickTargets = 0;
            if (skill.Effect is ChainHitsEffect chain)
                slot.HitsLeft = chain.Hits;
            if (skill.Effect is InputHitsEffect hits && hits.MaxHits > 0)
                slot.HitsLeft = hits.MaxHits;
            if (skill.Effect is FlurryEffect flurry)
                slot.HitsLeft = FlurryHits(flurry, native);
            slot.HitCount = 0;
            slot.InputKinds = 0;
            slot.LastKeyTick = -1;
            slot.Progress = 0;
            slot.CircleProgress = 0;
            if (skill.Effect is KeyRepeatEffect limited && limited.Hits > 0)
                slot.HitsLeft = limited.Hits;
            ApplyAura(slot);
            if (native.HoldDecaySeconds > 0)
                _resource.HoldDecay(native.HoldDecaySeconds);
            if (native.ResourceOnCast > 0)
                _resource.Gain(native.ResourceOnCast);
            if (native.ComboOnCast > 0)
                _state.Combo += native.ComboOnCast;
            if (announce)
                _events.Add(GameEvent.ForSkill(GameEventKind.SkillCast, _state.Tick, slot.Skill));
            if (skill.Effect is StrikeEffect strike)
            {
                Strike(slot, strike, native, spent);
                if (native.RepeatAboveCombo > 0 && _state.Combo > native.RepeatAboveCombo)
                    Strike(slot, strike, native, 0);
            }
            _combat.Runedancer.OnSkillCast(slot.Skill);
            _combat.Duelist.OnSkillCast(slot.Skill);
            _combat.Windshot.OnSkillCast(slot.Skill);
            _combat.Shardcaller.OnSkillCast(slot.Skill);
            _combat.Sandwalker.OnSkillCast(slot.Skill);
            _combat.Minstrel.OnSkillCast(slot.Skill);
            _combat.Dreamcatcher.OnSkillCast(slot.Skill);
            _combat.Lanternbearer.OnSkillCast(slot.Skill);
            _combat.Whisperer.OnSkillCast(slot.Skill);
            _combat.Clockwright.OnSkillCast(slot.Skill);
            _combat.Alchemist.OnSkillCast(slot.Skill);
            _combat.Dowser.OnSkillCast(slot.Skill);
            _combat.Soulbinder.OnSkillCast(slot.Skill);
            _combat.Ghostcaptain.OnSkillCast(slot);
            _combat.Mothcaller.OnSkillCast(slot.Skill);
            _combat.Cartomancer.OnSkillCast(slot.Skill);
            _combat.Herbwitch.OnSkillCast(slot.Skill);
            _combat.Golemancer.OnSkillCast(slot.Skill);
            if (skill.Effect is ResourceGainEffect gain)
                _resource.Gain(gain.Amount);
            if (native.RestoresFocus)
                _state.FocusRefill = true;
            if (slot.WhenAtCast && native.WhenResource > 0)
                _resource.Gain(native.WhenResource);
            if (native.ResourceRelief > 0)
                _state.Resource = System.Math.Max(0, _state.Resource - native.ResourceRelief);
            if (skill.Effect is NextKillRewardEffect reward)
                ArmNextKillReward(reward);
            if (skill.Effect is EchoEffect echo)
                Repeat(echo);
        }

        private void TickBarrels()
        {
            if (_state.BarrelTicks <= 0 || --_state.BarrelTicks > 0)
                return;
            for (; _state.Barrels > 0; _state.Barrels--)
                _combat.SkillHit(_combat.BaseKeyDamage, _state.BarrelMultiplier, StatTags.Skill);
        }

        private bool HeldByResource(SkillSlot slot) =>
            slot.ActiveTicks == 1 && IsNative(slot) && slot.Def.Native.LastsAboveResource > 0 && _state.Resource > slot.Def.Native.LastsAboveResource;

        private void ArmNextKillReward(NextKillRewardEffect reward)
        {
            _state.NextKillReward = System.Math.Max(_state.NextKillReward, reward.Multiplier);
            _state.NextKillShardChance = reward.ShardChance;
            _state.NextKillShards = reward.Shards;
        }

        private void Repeat(EchoEffect echo)
        {
            var target = _lastCast;
            if (target == null || !_state.Skills.Contains(target))
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

        private void TickInputHits(SkillSlot slot, InputHitsEffect hits, in EffectiveInput input)
        {
            if (!slot.Active || (hits.MaxHits > 0 && slot.HitsLeft <= 0))
                return;
            var multiplier = 0.0;
            var hitCount = 0;
            if (hits.PxPerHit > 0)
            {
                slot.Progress += input.MousePx * SupportEffects.MouseDistance(slot);
                var steps = (int)System.Math.Floor(slot.Progress / hits.PxPerHit);
                slot.Progress -= steps * hits.PxPerHit;
                multiplier += hits.PxShare * steps;
                hitCount += steps;
            }
            if (multiplier > 0 && hits.QueueTargets > 0)
                QueueShare(_combat.SkillDamage(_combat.BaseKeyDamage, multiplier * slot.CastPower, slot.Skill.Tags), hits.QueueTargets);
            if (multiplier > 0)
                SkillHit(slot, multiplier);
            if (hitCount > 0 && IsNative(slot))
                _resource.Gain(slot.Def.Native.ResourceOnHit * hitCount);
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
            var px = input.MousePx * SupportEffects.MouseDistance(slot);
            buckets[index] += px;
            slot.Progress += px;
            var stacks = System.Math.Min(MaxStacks(distance.Stacks), (int)System.Math.Floor(System.Math.Max(0, slot.Progress) / distance.PxPerStack));
            SetStacks(slot, distance.Stacks, stacks);
        }

        private double Cost(SkillSlot slot) => slot.Def.ResourceCost * System.Math.Max(MinCostShare, _hero.Value(CombatStats.SkillCost, 1)) * SupportEffects.CostMultiplier(slot);

        private const double MinCostShare = 0.1;

        private bool Pay(SkillSlot slot, NativeTrait native, bool when, out double spent)
        {
            var skill = slot.Def;
            var cost = Cost(slot);
            spent = cost;
            if ((native.FreeAboveCombo > 0 && _state.Combo > native.FreeAboveCombo) || (when && native.WhenFree))
            {
                spent = 0;
                return true;
            }
            if (native.SpendsAll)
            {
                if (_state.Resource < cost)
                    return false;
                spent = _resource.SpendAll();
                return true;
            }
            if (cost > 0 && SupportEffects.DeferPerSecond(slot) > 0)
            {
                if (_state.Resource < cost)
                    return false;
                slot.DeferredCost += cost;
                slot.DeferredTotal = slot.DeferredCost;
                return true;
            }
            return cost <= 0 || _resource.TrySpend(cost);
        }

        private double DurationMultiplier(SkillDef skill, NativeTrait native, bool wasFull, bool when)
        {
            var multiplier = skill.Effect is AuraEffect ? _hero.Value(CombatStats.AuraDuration, 1, StatTags.Aura) : 1;
            if (wasFull && native.FullResourceDurationMultiplier > 0)
                multiplier *= native.FullResourceDurationMultiplier;
            if (native.DurationMultiplier > 0)
                multiplier *= native.DurationMultiplier;
            if (when && native.WhenDurationMultiplier > 0)
                multiplier *= native.WhenDurationMultiplier;
            return multiplier;
        }

        private void Strike(SkillSlot slot, StrikeEffect strike, NativeTrait native, double spent)
        {
            if (strike.CoinFlip && _state.Rng.NextDouble() < 0.5)
            {
                slot.StartCooldown(0);
                return;
            }
            var when = slot.WhenAtCast && native.When != null;
            if (Executes(strike, when ? native.WhenExecuteBelow : 0))
            {
                _combat.DirectHit(_state.EnemyHp);
                return;
            }
            var paused = strike.PauseSeconds <= 0 || _state.Tick - _lastKeyHitTick >= Ticks(strike.PauseSeconds);
            var power = slot.CastPower * StrikeBonus(strike, native, when) * (1 + native.PerMinion * Minions.Standing(MinionKind.Skeleton));
            if (strike.ConsumeMinion && !Minions.Consume(MinionKind.Skeleton, native.RespawnMinion))
                power = 0;
            var baseMultiplier = strike.PauseSeconds > 0 && paused ? strike.PauseMultiplier : LowHp(strike) ? strike.LowHpMultiplier : strike.Multiplier;
            var multiplier = power * (native.SpendsAll ? System.Math.Min(native.SpendAllMax, spent * native.SpendAllPerPoint) : baseMultiplier);
            var queueMultiplier = native.SpendsAll ? multiplier : power * strike.QueueMultiplier;
            var tags = slot.Skill.Tags;
            var targets = strike.QueueTargets + (native.ExtraTargetWhileFamily != null && AnyActive(native.ExtraTargetWhileFamily) ? 1 : 0) + (when ? native.WhenExtraTargets : 0);
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
            if (strike.BossTimerStopSeconds > 0 && _state.FightingBoss)
                _state.BossTimerStopTicks = System.Math.Max(_state.BossTimerStopTicks, Ticks(strike.BossTimerStopSeconds));
            var killsBefore = _state.Kills;
            if (strike.Arrow)
                multiplier *= System.Math.Max(0, _hero.Value(CombatStats.ArrowDamage, 1, tags));
            if (strike.LastKeyHit)
                _combat.DirectHit(_lastKeyHitDamage * multiplier);
            else
                _combat.SkillHit(_combat.BaseKeyDamage, multiplier, tags, paused ? strike.CritChance : 0);
            _resource.Gain(native.ResourceOnHit);
            if (strike.Stacks != null)
            {
                AddStacks(slot, strike.Stacks, 1);
                if (native.StackSeconds > 0)
                    slot.StackTicks = Ticks(native.StackSeconds);
            }
            if (native.ResourceOnCrit > 0 && _combat.LastHitCrit && !strike.LastKeyHit)
                _resource.Gain(native.ResourceOnCrit);
            var burn = native.SpendsAll && strike.BurnStacks > 0 ? System.Math.Min(native.SpendAllMax, spent * native.SpendAllPerPoint) : strike.BurnStacks;
            if (burn > 0)
                Dots.AddBurn(burn * slot.StackPower);
            if (_state.Kills == killsBefore)
                return;
            _resource.Gain(spent * strike.KillRefundShare + native.ResourceOnKill);
            if (native.RefreshFamily != null)
                RefreshActive(native.RefreshFamily);
        }

        private bool Executes(StrikeEffect strike, double nativeBelow)
        {
            var below = System.Math.Max(strike.ExecuteBelow, strike.ExecuteBelow > 0 ? nativeBelow : 0);
            return below > 0 && EnemyShareBelow(below);
        }

        private bool LowHp(StrikeEffect strike) => strike.LowHpBelow > 0 && EnemyShareBelow(strike.LowHpBelow);

        private bool EnemyShareBelow(double share) =>
            _state.EnemyAlive && _state.EnemyMaxHp.Sign > 0 && (_state.EnemyHp / _state.EnemyMaxHp).ToDouble() < share;

        private double StrikeBonus(StrikeEffect strike, NativeTrait native, bool when)
        {
            var bonus = strike.BossMultiplier > 0 && _state.FightingBoss ? strike.BossMultiplier : 1;
            return when && native.WhenMultiplier > 0 ? bonus * native.WhenMultiplier : bonus;
        }

        private bool WhenHolds(NativeTrait native) => native.When != null && Holds(native.When);

        private bool Holds(SkillCondition condition) =>
            (condition.ResourceAtLeast <= 0 || _state.Resource >= condition.ResourceAtLeast - 1e-9)
            && (condition.ResourceAtMost <= 0 || _state.Resource <= condition.ResourceAtMost + 1e-9)
            && (condition.ComboAbove <= 0 || _state.Combo > condition.ComboAbove)
            && (!condition.Boss || _state.FightingBoss)
            && (!condition.BossRetry || (_state.FightingBoss && _state.BossFailsInARow > 0))
            && (condition.BossLastSeconds <= 0 || (_state.FightingBoss && _state.BossTicksLeft > 0 && _state.BossTicksLeft <= Ticks(condition.BossLastSeconds)))
            && (condition.FamilyActive == null || AnyActive(condition.FamilyActive))
            && (!condition.MouseMoving || _state.MouseIdleTicks < Ticks(MouseMovingSeconds))
            && (!condition.Reading || _state.Reading)
            && (condition.DotStacksAtLeast <= 0 || _state.PoisonStacks + _state.BurnStacks >= condition.DotStacksAtLeast - 1e-9)
            && (condition.MinionsAtLeast <= 0 || StandingMinions >= condition.MinionsAtLeast)
            && (condition.AurasAtLeast <= 0 || ActiveAuras >= condition.AurasAtLeast);

        private const double MouseMovingSeconds = 2;

        private int StandingMinions
        {
            get
            {
                var count = 0;
                foreach (var minion in _state.Minions)
                {
                    if (minion.Standing)
                        count++;
                }
                return count;
            }
        }

        private int ActiveAuras
        {
            get
            {
                var count = 0;
                foreach (var slot in _state.Skills)
                {
                    if (slot.Active && slot.Def.Effect is AuraEffect)
                        count++;
                }
                return count;
            }
        }

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
            _keyGapTicks = _state.Tick - _lastKeyHitTick;
            _lastKeyHitTick = _state.Tick;
            _state.KeyStreak = _keyGapTicks > Ticks(_combat.Classes.KeyStreakGapSeconds) ? 1 : _state.KeyStreak + 1;
            _lastKeyHitDamage = damage;
            var outer = _combat.Supporting;
            foreach (var slot in _state.Skills)
            {
                _combat.Supporting = slot;
                if (slot.HitsLeft > 0 && slot.Def.Effect is ChainHitsEffect chain)
                {
                    slot.HitsLeft--;
                    _state.ChainDamage += damage * chain.NextEnemyShare;
                }
                if (slot.Active && !slot.Def.Reserves)
                    OnActiveKeyHit(slot, damage);
                SupportEffects.OnKeyHit(slot, _state);
                CountFinish(slot);
                CountKeyEcho(slot);
            }
            _combat.Supporting = outer;
        }

        private void CountKeyEcho(SkillSlot slot)
        {
            var echo = SupportEffects.KeyEcho(slot);
            if (echo == null || slot.LastCastTick < 0)
                return;
            if (++slot.EchoKeys < echo.EchoEveryKeys)
                return;
            slot.EchoKeys = 0;
            Repeat(slot, echo.EchoPower);
        }

        private void CountFinish(SkillSlot slot)
        {
            if (slot.FinishHits < 0 || slot.Active)
                return;
            var finish = SupportEffects.Finish(slot);
            if (finish == null)
            {
                slot.FinishHits = -1;
                return;
            }
            if (++slot.FinishHits < finish.FinishAfterHits)
                return;
            slot.FinishHits = -1;
            SkillHit(slot, finish.FinishMultiplier);
            _resource.Gain(finish.FinishResource);
        }

        public void OnMouseHit(BigNumber damage)
        {
            var outer = _combat.Supporting;
            foreach (var slot in _state.Skills)
            {
                _combat.Supporting = slot;
                if (slot.Active && !slot.Def.Reserves)
                    OnActiveHit(slot, damage, HitInput.Mouse);
            }
            _combat.Supporting = outer;
        }

        public void OnCrit()
        {
            foreach (var slot in _state.Skills)
            {
                if (slot.Def.IsPassive && slot.Def.Native.ResourceOnCrit > 0 && IsNative(slot))
                    _resource.Gain(slot.Def.Native.ResourceOnCrit);
            }
        }

        private void OnActiveKeyHit(SkillSlot slot, BigNumber damage)
        {
            switch (slot.Def.Effect)
            {
                case StacksEffect stacks when stacks.Trigger == StackTrigger.KeyHit:
                    AddStacks(slot, stacks, 1);
                    break;
                case StreakStrikeEffect streak:
                    StreakHit(slot, streak);
                    break;
            }
            OnActiveHit(slot, damage, HitInput.Keys);
            if (!IsNative(slot))
                return;
            _resource.Gain(slot.Def.Native.ResourcePerKeyHit);
            _state.Combo += slot.Def.Native.ComboPerKeyHit;
        }

        private void OnActiveHit(SkillSlot slot, BigNumber damage, HitInput input)
        {
            switch (slot.Def.Effect)
            {
                case AuraEffect raising when raising.SummonEveryKeys > 0 && input == HitInput.Keys:
                    if (++slot.HitCount % raising.SummonEveryKeys == 0)
                        Minions.Place(_combat.Paths.SkeletonOnKill, slot.Skill.Tags);
                    break;
                case KeyRepeatEffect repeat when Matches(RepeatInput(slot, repeat), input):
                    RepeatHit(slot, repeat, damage);
                    break;
            }
        }

        private HitInput RepeatInput(SkillSlot slot, KeyRepeatEffect repeat) =>
            IsNative(slot) && slot.Def.Native.RepeatInput != null ? slot.Def.Native.RepeatInput.Value : repeat.Input;

        private static bool Matches(HitInput wanted, HitInput actual) =>
            wanted == HitInput.Any || wanted == actual || (wanted == HitInput.Combo && actual == HitInput.Keys);

        private void QueueShare(BigNumber damage, int targets)
        {
            var count = targets <= 0 ? _state.QueueDamage.Length : System.Math.Min(targets, _state.QueueDamage.Length);
            for (var i = 0; i < count; i++)
                _state.QueueDamage[i] += damage;
        }

        private void RepeatHit(SkillSlot slot, KeyRepeatEffect repeat, BigNumber damage)
        {
            var every = IsNative(slot) && slot.Def.Native.RepeatEvery > 0 ? slot.Def.Native.RepeatEvery : repeat.Every;
            if (++slot.HitCount % every != 0)
                return;
            if (repeat.BurnStacks > 0)
                Dots.AddBurn(repeat.BurnStacks * slot.StackPower);
            var extra = damage * repeat.Multiplier;
            if (repeat.Multiplier > 0)
                _combat.DirectHit(extra);
            if (repeat.QueueTargets > 0)
                QueueShare(extra, repeat.QueueTargets);
            if (IsNative(slot))
                _resource.Gain(slot.Def.Native.ResourceOnHit);
            if (repeat.Hits <= 0 || --slot.HitsLeft > 0)
                return;
            slot.ActiveTicks = 0;
            End(slot);
        }

        private void StreakHit(SkillSlot slot, StreakStrikeEffect streak)
        {
            if (slot.LastKeyTick >= 0 && _state.Tick - slot.LastKeyTick > Ticks(streak.MaxGapSeconds))
                slot.HitCount = 0;
            slot.LastKeyTick = _state.Tick;
            if (++slot.HitCount % streak.Hits == 0)
                SkillHit(slot, streak.Multiplier);
        }

        private int FlurryHits(FlurryEffect flurry, NativeTrait native)
        {
            if (native.ExtraHitPerMousePx <= 0)
                return flurry.Hits;
            var extra = (int)System.Math.Min(int.MaxValue / 2, System.Math.Floor(_state.MouseWindow.Total / native.ExtraHitPerMousePx));
            return System.Math.Min(System.Math.Max(flurry.Hits, native.MaxHits), flurry.Hits + extra);
        }

        private void TickFlurry(SkillSlot slot, FlurryEffect flurry)
        {
            if (slot.HitsLeft <= 0)
                return;
            if (!_state.EnemyAlive)
                return;
            var killsBefore = _state.Kills;
            var native = IsNative(slot) ? slot.Def.Native : NativeTrait.None;
            var multiplier = flurry.Multiplier * (slot.WhenAtCast && native.WhenMultiplier > 0 ? native.WhenMultiplier : 1);
            var hits = System.Math.Min(slot.HitsLeft, flurry.HitsPerTick);
            for (var i = 0; i < hits; i++)
            {
                slot.HitsLeft--;
                FlurryQueueHit(slot, flurry, multiplier);
                SkillHit(slot, multiplier);
                _state.Combo += native.ComboPerHit;
                _resource.Gain(native.ResourceOnHit);
                if (_state.Kills == killsBefore)
                    continue;
                _resource.Gain(native.ResourceOnKill);
                if (!native.CarryOn)
                    slot.HitsLeft = 0;
                return;
            }
        }

        private void FlurryQueueHit(SkillSlot slot, FlurryEffect flurry, double multiplier)
        {
            if (flurry.QueueTargets <= 0)
                return;
            var damage = _combat.SkillDamage(_combat.BaseKeyDamage, multiplier * slot.CastPower, slot.Skill.Tags);
            for (var i = 0; i < flurry.QueueTargets; i++)
                _state.QueueDamage[i] += damage;
        }

        private void TickFlow(SkillSlot slot, FlowEffect flow)
        {
            if (_state.KeylessTicks > Ticks(flow.MaxGapSeconds))
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
            var poisonTicks = Dots.PoisonTicks - _seenPoisonTicks;
            _seenPoisonTicks = Dots.PoisonTicks;
            TickBarrels();
            var clickCut = Ticks(_hero.Value(CombatStats.CooldownPerClick, 0) * (input.LeftClicks + input.RightClicks + input.MiddleClicks));
            var weakenedKill = _state.WeakenedKills != _seenWeakenedKills;
            _seenWeakenedKills = _state.WeakenedKills;
            ExtendSongs(input);
            for (var index = 0; index < _state.Skills.Count; index++)
            {
                var slot = _state.Skills[index];
                _combat.Supporting = slot;
                if (slot.CooldownTicks > 0 && clickCut > 0)
                    slot.CooldownTicks = System.Math.Max(1, slot.CooldownTicks - clickCut);
                if (slot.CooldownTicks > 0 && --slot.CooldownTicks == 0)
                    slot.ReadyTick = _state.Tick;
                PayDeferred(slot);
                KeepReserved(slot);
                RefillCharge(slot);
                TickTwin(slot);
                TickTailwind(slot, input);
                if (slot.ActiveTicks > 0 && !Lingers(slot, input) && !HeldByResource(slot))
                {
                    slot.ActiveTicks--;
                    if (slot.ActiveTicks == 0)
                        End(slot);
                }
                if (slot.Def.Effect is StacksEffect stacks && stacks.Passive)
                    TickKillStacks(slot, stacks, kills);
                if (kills > 0 && slot.Def.IsPassive && slot.Def.Native.ResourceOnKill > 0 && IsNative(slot))
                    _resource.Gain(slot.Def.Native.ResourceOnKill * kills);
                if (slot.Stacks > 0 && slot.Def.Effect is StrikeEffect { Stacks: { } } && --slot.StackTicks <= 0)
                    ClearStacks(slot);
                if (weakenedKill && slot.CooldownTicks > 0 && slot.Def.Native.ResetWithinSeconds > 0 && IsNative(slot)
                    && _state.LastWeakenedKillSeconds <= slot.Def.Native.ResetWithinSeconds + 1e-9)
                {
                    slot.CooldownTicks = 0;
                    slot.ReadyTick = _state.Tick;
                }
                if (slot.Active && _state.FightingBoss && slot.Def.Native.ResourcePerSavedSecond > 0 && IsNative(slot))
                    _resource.Gain(slot.Def.Native.ResourcePerSavedSecond * GameLoop.TickSeconds * System.Math.Max(0, 1 - _state.BossTimerRate));
                if (poisonTicks > 0 && slot.Def.Native.ResourcePerPoisonTick > 0 && IsNative(slot))
                    _resource.Gain(slot.Def.Native.ResourcePerPoisonTick * poisonTicks);
                if (slot.Def.Effect is FlowEffect flow)
                    TickFlow(slot, flow);
                if (slot.Def.Effect is InputHitsEffect inputHits)
                    TickInputHits(slot, inputHits, input);
                if (slot.Def.Effect is DistanceStacksEffect distance)
                    TickDistanceStacks(slot, distance, input);
                if (slot.Def.Effect is BossHuntEffect hunt)
                    TickBossHunt(slot, hunt);
                if (slot.Def.Effect is FlurryEffect flurry)
                    TickFlurry(slot, flurry);
                if (slot.Def.Effect is PassiveEffect passive)
                    SetPassive(slot, passive.Modifiers, passive.When != null && !Holds(passive.When) ? 0 : passive.PerPartyMember ? _state.PartyMembers : 1);
                TickStanding(slot);
                if (slot.Def.Effect is BreakBonusEffect breakBonus)
                    TickBreakBonus(slot, breakBonus, input, idleTicksBefore);
                if (slot.Active && slot.Def.Effect is AuraEffect keyless && keyless.KeylessStrikeSeconds > 0)
                    TickKeylessStrike(slot, keyless);
                if (slot.Active && slot.Def.Effect is TrailEffect)
                    slot.Progress += input.MousePx * SupportEffects.MouseDistance(slot);
                if (input.HasInput && slot.Def.Effect is IdleStrikeEffect strike)
                    ReleaseIdleStrike(slot, strike, idleTicksBefore * GameLoop.TickSeconds);
            }
            _combat.Supporting = null;
            _state.ActiveAuras = CountAuras();
            ActiveInputBonuses();
        }

        private void ActiveInputBonuses()
        {
            var keys = 1.0;
            var mouse = 1.0;
            for (var i = 0; i < _state.Skills.Count; i++)
            {
                if (_state.Skills[i].Active)
                    SupportEffects.ActiveInputDamage(_state.Skills[i], ref keys, ref mouse);
            }
            _state.SupportKeyBonus = keys;
            _state.SupportMouseBonus = mouse;
            var cap = 1.0;
            for (var i = 0; i < _state.Skills.Count; i++)
                cap *= SupportEffects.StackCap(_state.Skills[i]);
            _state.SupportStackCap = cap;
        }

        private void TickTailwind(SkillSlot slot, in EffectiveInput input)
        {
            if (!slot.Active || input.MousePx <= 0)
                return;
            var wind = SupportEffects.Tailwind(slot);
            if (wind == null)
                return;
            slot.WindPx += input.MousePx;
            var seconds = (int)System.Math.Floor(slot.WindPx / wind.WindPxPerSecond + 1e-9);
            if (seconds <= 0)
                return;
            slot.WindPx -= seconds * wind.WindPxPerSecond;
            var room = System.Math.Max(0, (int)System.Math.Round(slot.ActiveTotalTicks * wind.WindMax) - slot.WindTicks);
            var added = System.Math.Min(room, seconds * GameLoop.TicksPerSecond);
            slot.WindTicks += added;
            slot.ActiveTicks += added;
        }

        private void RefillCharge(SkillSlot slot)
        {
            var extra = SupportEffects.ExtraCharges(slot);
            if (slot.SpareCharges >= extra)
            {
                slot.SpareCharges = System.Math.Min(slot.SpareCharges, extra);
                slot.ChargeTicks = 0;
                return;
            }
            if (!slot.Ready)
                return;
            if (slot.ChargeTicks <= 0)
                slot.ChargeTicks = System.Math.Max(1, Ticks(slot.Def.CooldownSeconds * CooldownMultiplier() * SupportEffects.CooldownMultiplier(slot)));
            if (--slot.ChargeTicks == 0)
                slot.SpareCharges++;
        }

        private void ArmTwin(SkillSlot slot)
        {
            var twins = SupportEffects.Twins(slot);
            if (twins == null || slot.Def.Effect is EchoEffect)
                return;
            slot.TwinTicks = System.Math.Max(1, Ticks(twins.TwinSeconds));
            slot.TwinPower = twins.TwinPower;
        }

        private void TickTwin(SkillSlot slot)
        {
            if (slot.TwinTicks <= 0 || --slot.TwinTicks > 0)
                return;
            Repeat(slot, slot.TwinPower);
        }

        private void Repeat(SkillSlot slot, double power)
        {
            if (slot.Def.ArmOn != ArmOn.None || slot.Def.IsPassive)
                return;
            var castPower = slot.CastPower;
            var auraPower = slot.AuraPower;
            slot.CastPower = castPower * power;
            slot.AuraPower = auraPower * power;
            if (slot.Def.Effect is AuraEffect)
                RemoveAura(slot);
            slot.StackPower = power;
            var outer = _combat.Supporting;
            _combat.Supporting = slot;
            Release(slot, IsNative(slot) ? slot.Def.Native : NativeTrait.None, 0, false, announce: false);
            _combat.Supporting = outer;
            slot.StackPower = 1;
            slot.CastPower = castPower;
            slot.AuraPower = auraPower;
        }

        private bool Lingers(SkillSlot slot, in EffectiveInput input)
        {
            var max = SupportEffects.LingerMax(slot);
            if (max <= 0 || slot.LingerTicks >= slot.ActiveTotalTicks * max || !SameInput(slot.Skill.Tags, input))
                return false;
            slot.LingerTicks++;
            return true;
        }

        private static bool SameInput(StatTags tags, in EffectiveInput input) =>
            ((tags & StatTags.Strike) != 0 && input.Hits > 0)
            || ((tags & StatTags.Mouse) != 0 && input.MousePx > 0)
            || ((tags & StatTags.Click) != 0 && input.ButtonPresses > 0);

        private void PayDeferred(SkillSlot slot)
        {
            if (slot.DeferredCost <= 0)
                return;
            var due = System.Math.Min(slot.DeferredCost, slot.DeferredTotal * SupportEffects.DeferPerSecond(slot) * GameLoop.TickSeconds);
            if (due <= 0)
                due = slot.DeferredCost;
            slot.DeferredCost -= due;
            _resource.TrySpend(System.Math.Min(due, _state.Resource));
        }

        private int CountAuras()
        {
            var count = 0;
            for (var i = 0; i < _state.Skills.Count; i++)
            {
                if (_state.Skills[i].AuraApplied && _state.Skills[i].Def.Effect is AuraEffect)
                    count++;
            }
            return count;
        }

        private static bool IsSong(SkillSlot slot) => slot.Active && slot.Def.Effect is AuraEffect && !slot.Def.Reserves;

        private void ExtendSongs(in EffectiveInput input)
        {
            var keyTicks = KeyExtension(input);
            if (keyTicks == 0)
                return;
            for (var i = 0; i < _state.Skills.Count; i++)
            {
                var slot = _state.Skills[i];
                if (!IsSong(slot))
                    continue;
                slot.ActiveTicks = System.Math.Max(slot.ActiveTicks, System.Math.Min(slot.ActiveTotalTicks, slot.ActiveTicks + keyTicks));
            }
        }

        private int KeyExtension(in EffectiveInput input)
        {
            var perKey = _hero.Value(CombatStats.AuraSecondsPerKey, 0, StatTags.Aura);
            if (perKey <= 0 || input.Hits <= 0)
                return 0;
            _state.AuraKeyCarry += input.Hits * perKey * GameLoop.TicksPerSecond;
            var ticks = (int)System.Math.Floor(_state.AuraKeyCarry + 1e-9);
            _state.AuraKeyCarry -= ticks;
            return ticks;
        }

        private void TickStanding(SkillSlot slot)
        {
            var native = IsNative(slot);
            var trait = slot.Def.Native;
            slot.NativeApplied = Toggle(slot.NativeApplied, trait.Modifiers, native && slot.Def.IsPassive && !slot.Def.Reserves);
            var when = native && trait.When != null && (slot.Def.IsPassive || slot.Active) && Holds(trait.When);
            slot.WhenApplied = Toggle(slot.WhenApplied, trait.WhenModifiers, when);
        }

        private bool Toggle(bool applied, System.Collections.Generic.IReadOnlyList<Modifier> modifiers, bool on)
        {
            if (applied == on || modifiers.Count == 0)
                return on && modifiers.Count > 0;
            for (var i = 0; i < modifiers.Count; i++)
            {
                if (on)
                    _hero.Add(modifiers[i]);
                else
                    _hero.Remove(modifiers[i]);
            }
            return on;
        }

        private void DropStanding(SkillSlot slot)
        {
            if (slot.NativeApplied)
                Toggle(true, slot.Def.Native.Modifiers, false);
            if (slot.WhenApplied)
                Toggle(true, slot.Def.Native.WhenModifiers, false);
            slot.StandingApplied = false;
            slot.NativeApplied = false;
            slot.WhenApplied = false;
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
            if (scale == slot.PassiveScale && (scale <= 0 || slot.AuraApplied))
                return;
            RemoveAura(slot);
            slot.PassiveScale = scale;
            if (scale <= 0)
                return;
            for (var i = 0; i < modifiers.Count; i++)
                slot.AuraModifiers.Add(modifiers[i].WithValue(modifiers[i].Value * scale));
            _hero.AddRange(slot.AuraModifiers);
            slot.AuraApplied = true;
        }

        private void End(SkillSlot slot)
        {
            RemoveAura(slot);
            if (slot.Def.Effect is AuraEffect aura && aura.FocusLockAfterSeconds > 0)
                _state.FocusLockTicks = Ticks(aura.FocusLockAfterSeconds);
            if (slot.Def.Effect is StacksEffect stacks && !stacks.Passive)
                ClearStacks(slot);
            if (IsNative(slot))
                _resource.Gain(slot.Def.Native.ResourceOnEnd);
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

        private const double RescaleTolerance = 1e-9;

        private void KeepReserved(SkillSlot slot)
        {
            if (!slot.Def.Reserves)
                return;
            if (IdleOff(slot))
            {
                RemoveAura(slot);
                slot.ActiveTicks = 0;
                return;
            }
            slot.ActiveTicks = Ticks(ReserveRefreshSeconds);
            slot.ActiveTotalTicks = slot.ActiveTicks;
            var power = ReservedPower(slot);
            if (!slot.AuraApplied)
                slot.AuraPower = power;
            else if (System.Math.Abs(power - slot.AuraPower) > RescaleTolerance * System.Math.Max(1e-12, slot.AuraPower))
                RescaleAura(slot, power);
            ApplyAura(slot);
        }

        private double ReservedPower(SkillSlot slot) =>
            SupportEffects.Power(slot, _state) * SupportEffects.AuraPower(slot) * OwnPower(slot) * slot.BranchDamage;

        private void RescaleAura(SkillSlot slot, double power)
        {
            for (var i = 0; i < slot.AuraModifiers.Count; i++)
                _hero.Remove(slot.AuraModifiers[i]);
            slot.AuraModifiers.Clear();
            slot.AuraPower = power;
            FillAuraModifiers(slot, slot.Def.Effect as AuraEffect);
            _hero.AddRange(slot.AuraModifiers);
        }

        private bool IdleOff(SkillSlot slot) =>
            slot.Def.Effect is AuraEffect aura && aura.IdleOffSeconds > 0 && !PauseProof(slot) && _state.IdleTicks > Ticks(aura.IdleOffSeconds);

        private bool PauseProof(SkillSlot slot) => IsNative(slot);

        public void CastFromButtons(in EffectiveInput input)
        {
            CastFromKeys(input.Hits);
            if (input.LeftClicks > 0)
                TryCast(SkillLoadout.IndexOf(_state.Skills, SkillSlotRole.Left));
            if (input.RightClicks > 0)
                TryCast(SkillLoadout.IndexOf(_state.Skills, SkillSlotRole.Right));
        }

        public void CastAutomatic()
        {
            for (var index = 0; index < _state.Skills.Count; index++)
            {
                var slot = _state.Skills[index];
                if (SkillLoadout.CastsByItself(slot.Role) && slot.Ready && !slot.Active && !slot.Primed && !slot.Def.IsPassive)
                    TryCast(index);
            }
        }

        private void CastFromKeys(double keys)
        {
            var every = (long)System.Math.Round(_hero.Value(CombatStats.KeyCastLeftEvery, 0, StatTags.Strike));
            if (every < 1 || keys <= 0)
                return;
            _state.AutoCastKeys += keys;
            if (_state.AutoCastKeys < every)
                return;
            _state.AutoCastKeys %= every;
            TryCast(SkillLoadout.IndexOf(_state.Skills, SkillSlotRole.Left));
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
            slot.StartCooldown(Ticks(slot.Def.CooldownSeconds * CooldownMultiplier()));
            slot.LastCastTick = _state.Tick;
            _events.Add(GameEvent.ForSkill(GameEventKind.SkillCast, _state.Tick, slot.Skill));
            _combat.SkillHit(_combat.BaseKeyDamage, ChargeMultiplier(strike, idleSeconds), slot.Skill.Tags);
            if (IsNative(slot))
                _resource.Gain(slot.Def.Native.ResourceOnCast);
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

        public void RefreshAuraStrength()
        {
            foreach (var slot in _state.Skills)
            {
                if (!slot.AuraApplied || !(slot.Def.Effect is AuraEffect))
                    continue;
                RemoveAura(slot);
                ApplyAura(slot);
            }
        }

        private bool IsNative(SkillSlot slot) =>
            slot.Def.Weapon != null && _weapons.TryGetType(slot.Def.Weapon, out var weapon) && _state.Is(weapon.HeroClass);

        private double CooldownMultiplier() => System.Math.Max(0.1, _hero.Value(CombatStats.SkillCooldown, 1) - ResourceCooldownCut()) * _combat.Clockwright.OwnCooldownMultiplier;

        private double ResourceCooldownCut()
        {
            var perPoint = _hero.Value(CombatStats.CooldownPerResource, 0, StatTags.Skill);
            if (perPoint <= 0)
                return 0;
            return System.Math.Min(System.Math.Max(0, _hero.Value(CombatStats.CooldownPerResourceMax, 0, StatTags.Skill)), perPoint * _state.Resource);
        }

        private double MinIdleSeconds(IdleStrikeEffect strike) => System.Math.Max(0, strike.MinIdleSeconds + _hero.Value(CombatStats.IdleStartSeconds, 0, StatTags.Idle));

        private static double ChargeMultiplier(IdleStrikeEffect strike, double idleSeconds) =>
            strike.ChargePerSecond * System.Math.Min(idleSeconds, strike.MaxChargeSeconds);

        private void ApplyAura(SkillSlot slot)
        {
            if (slot.AuraApplied)
                return;
            var aura = slot.Def.Effect as AuraEffect;
            if (!FillAuraModifiers(slot, aura))
                return;
            _hero.AddRange(slot.AuraModifiers);
            slot.AuraApplied = true;
            Reserve(slot, aura);
            if (aura == null)
                return;
            _events.Add(GameEvent.ForSkill(GameEventKind.AuraChanged, _state.Tick, slot.Skill));
            if (BoostsAuras(aura))
                RefreshOtherAuras(slot);
        }

        private bool FillAuraModifiers(SkillSlot slot, AuraEffect? aura)
        {
            var native = IsNative(slot) ? slot.Def.Native.Modifiers : Array.Empty<Modifier>();
            if (aura == null && native.Count == 0)
                return false;
            var strength = aura == null ? 1 : _hero.Value(CombatStats.AuraEffect, 1, StatTags.Aura);
            var when = slot.WhenAtCast && IsNative(slot) && slot.Def.Native.WhenMultiplier > 0 ? slot.Def.Native.WhenMultiplier : 1;
            for (var i = 0; aura != null && i < aura.Modifiers.Count; i++)
            {
                var modifier = aura.Modifiers[i];
                slot.AuraModifiers.Add(modifier.WithValue(modifier.Value * strength * when * slot.AuraPower * LevelPower(slot.Skill)));
            }
            for (var i = 0; i < native.Count; i++)
                slot.AuraModifiers.Add(native[i]);
            slot.AuraStrength = strength;
            return true;
        }

        private void Reserve(SkillSlot slot, AuraEffect? aura)
        {
            if (aura == null || !aura.Reserves)
                return;
            var reserve = System.Math.Max(0, aura.Reserve + _hero.Value(CombatStats.AuraReserve, 0, StatTags.Aura));
            if (_hero.Value(CombatStats.AuraReserveOnce, 0, StatTags.Aura) > 0)
            {
                _state.Resource = System.Math.Max(0, _state.Resource - reserve);
                return;
            }
            slot.ReservedApplied = reserve;
            _state.ReservedResource += slot.ReservedApplied;
            _resource.ClampToMax();
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

        private static int Ticks(double seconds) => (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
    }
}
