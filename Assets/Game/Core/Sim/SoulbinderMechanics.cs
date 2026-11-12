using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class SoulbinderMechanics : IKillRewardListener
    {
        public static readonly int ColdEffect = GemDef.NetIdOf("trait_soul_cold");

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private SoulbinderRules _rules;

        public SoulbinderMechanics(SoulbinderRules rules, GameState state, StatSheet hero, CombatSystem combat)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _combat = combat;
        }

        public void UseRules(SoulbinderRules rules) => _rules = rules;

        private SoulbinderState S => _state.Soulbinder;
        private AllyBoons Boons => _state.Boons;
        private bool On => _state.Is(HeroClass.Soulbinder);
        private bool Flag(StatId stat) => _hero.Value(stat, 0) >= 1;
        private double Positive(StatId stat) => System.Math.Max(0, _hero.Value(stat, 0));
        private bool InParty => _state.SharedEnemy && _state.PartyMembers > 1;

        public bool Sphere => On && SoulPower > 0;
        public bool Breath => On && BreathPower > 0;
        public bool Lend => On && LendPower > 0;

        public double SoulPower => Positive(SoulbinderStats.SoulPower);
        public double BreathPower => Positive(SoulbinderStats.BreathPower);
        public double LendPower => Positive(SoulbinderStats.LendPower);

        public int SphereSize
        {
            get
            {
                var size = _rules.SphereSize + (int)System.Math.Round(_hero.Value(SoulbinderStats.SoulMax, 0));
                if (Sphere && Flag(SoulbinderStats.SoulHungry))
                    size = 4;
                if (Breath && Flag(SoulbinderStats.BreathSmall))
                    size = 3;
                if (Breath && Flag(SoulbinderStats.BreathTwice))
                    size += 2;
                return System.Math.Max(1, System.Math.Min(_rules.SphereMax, size));
            }
        }

        public int LendHits => System.Math.Max(_rules.LendHitsMin, (int)System.Math.Round(_rules.LendHits + _hero.Value(SoulbinderStats.LendHits, 0)));

        public double LendSeconds => System.Math.Max(1, _rules.LendSeconds + _hero.Value(SoulbinderStats.LendSeconds, 0));

        public double ColdShare => Breath && S.ColdTicks > 0 ? _rules.ColdDamageTaken + Positive(SoulbinderStats.BreathParty) : 0;

        public double SpiritStrength
        {
            get
            {
                var total = 0.0;
                for (var i = 0; i < S.Souls; i++)
                    total += S.Strength[i];
                return total;
            }
        }

        public double DamageTaken()
        {
            if (!On || _state.SharedEnemy)
                return 1;
            return 1 + ColdShare;
        }

        public void AfterStrike()
        {
            StrikeBoons();
            if (!On)
                return;
            if (Breath && S.ColdTicks > 0)
                AddColdHits(_rules.ColdSoloShare);
            if (Lend && ++S.LendHits >= LendHits)
            {
                S.LendHits = 0;
                LendSoul();
            }
        }

        public void OnSkillCast(GemDef skill, SkillDef? variant = null)
        {
            if (!On || !((variant ?? skill.Skill)?.CastStrike is StrikeEffect strike))
                return;
            if (strike.Souls > 0 && (Sphere || Breath))
            {
                for (var i = 0; i < strike.Souls; i++)
                    TakeSoul(1, _state.EnemyMaxHp);
            }
            if (strike.SpiritsHit > 0 && S.Souls > 0)
                TraitHit(strike.SpiritsHit * SpiritStrength);
            if (strike.ReleaseAll && S.Souls > 0)
                ReleaseAll(1);
            if (strike.Lend && Lend)
                LendSoul();
            if (strike.Breathe && Breath && S.Souls > 0)
                ExhaleOldest();
        }

        public void Tick()
        {
            TickBoons();
            if (!On)
                return;
            if (Sphere)
                TickSpirits();
            if (S.ColdTicks > 0)
                S.ColdTicks--;
            ShareCold();
        }

        public void OnKillRewarded()
        {
            if (!On)
                return;
            if (Lend && Boons.LendTicks > 0)
                ReceiveSoulBack(1);
            if (!Sphere && !Breath)
                return;
            var strength = _state.EnemyType == EnemyType.Boss ? _rules.BossSoul : _state.EnemyType == EnemyType.Guardian ? _rules.GuardianSoul : 1;
            var count = Sphere && Flag(SoulbinderStats.SoulMini) && _state.FightingBoss ? 2 : 1;
            for (var i = 0; i < count; i++)
                TakeSoul(strength, _state.EnemyMaxHp);
        }

        private void ReceiveSoulBack(int count)
        {
            if (!Lend || count <= 0)
                return;
            S.SoulsBack += count;
            _state.PendingResource += _rules.SoulBackResource * count;
            if (Flag(SoulbinderStats.LendBack))
                S.LendHits += _rules.SoulBackHits * count;
        }

        private void TakeSoul(double strength, BigNumber sourceHp)
        {
            S.SoulsTaken++;
            if (S.Souls >= SphereSize)
            {
                if (Breath)
                    ExhaleOldest();
                else if (Sphere && Flag(SoulbinderStats.SoulPush))
                {
                    TraitHit(_rules.PushBlast * S.Strength[0] * SoulPower);
                    DropOldest();
                }
                else
                    return;
            }
            if (S.Souls >= SoulbinderState.Capacity)
                return;
            S.Strength[S.Souls] = strength;
            S.SourceHp[S.Souls] = sourceHp;
            S.Souls++;
        }

        private void DropOldest()
        {
            for (var i = 1; i < S.Souls; i++)
            {
                S.Strength[i - 1] = S.Strength[i];
                S.SourceHp[i - 1] = S.SourceHp[i];
            }
            S.Souls--;
        }

        private void ExhaleOldest()
        {
            if (S.Souls <= 0)
                return;
            var hp = S.SourceHp[0];
            var boss = S.Strength[0] > 1;
            DropOldest();
            S.Breaths++;
            var share = BreathPower * (boss && Flag(SoulbinderStats.BreathBoss) ? 2 : 1);
            var damage = hp * share;
            _combat.DirectHit(damage);
            if (Flag(SoulbinderStats.BreathTwice))
                _combat.DirectHit(damage * _rules.BreathTwice);
            if (Flag(SoulbinderStats.BreathQueue))
                _state.AddToQueue(damage);
            if (Flag(SoulbinderStats.BreathNoCold))
                return;
            S.ColdTicks = Ticks(_rules.ColdSeconds + _hero.Value(SoulbinderStats.BreathColdSeconds, 0));
            S.ColdResendTicks = 0;
        }

        private void AddColdHits(double hits)
        {
            S.ColdHits += hits;
            var need = System.Math.Max(1, _rules.ColdHits + (int)System.Math.Round(_hero.Value(SoulbinderStats.BreathColdHits, 0)));
            while (S.ColdHits >= need && S.Souls > 0)
            {
                S.ColdHits -= need;
                ExhaleOldest();
            }
            if (S.Souls == 0)
                S.ColdHits = System.Math.Min(S.ColdHits, need);
        }

        private void TickSpirits()
        {
            if (S.Souls > 0 && _state.EnemyAlive)
            {
                S.SpiritCarry += _rules.SpiritHitsPerSecond * (Flag(SoulbinderStats.SoulHungry) ? 2 : 1) * GameLoop.TickSeconds;
                while (S.SpiritCarry >= 1 - 1e-9)
                {
                    S.SpiritCarry -= 1;
                    SpiritHit(SpiritStrength * SoulPower * (1 + Positive(SoulbinderStats.SoulParty)));
                }
            }
            if (!Flag(SoulbinderStats.SoulRelease) || !_state.FightingBoss || ++S.ReleaseTicks < Ticks(_rules.ReleaseEverySeconds))
                return;
            S.ReleaseTicks = 0;
            ReleaseAll(_rules.ReleaseMultiplier);
        }

        private void SpiritHit(double multiplier)
        {
            TraitHit(multiplier);
            if (Flag(SoulbinderStats.SoulQueue))
                _state.AddToQueue(_combat.SkillDamage(_combat.BaseKeyDamage, multiplier * _rules.QueueShare * _state.FocusMultiplier, StatTags.Strike));
        }

        private void ReleaseAll(double multiplier)
        {
            TraitHit(SpiritStrength * System.Math.Max(SoulPower, BreathPower * 10) * multiplier);
        }

        private void LendSoul()
        {
            S.Lends++;
            var power = LendPower * (1 + Positive(SoulbinderStats.LendParty)) * (Flag(SoulbinderStats.LendAll) ? 0.6 : 1);
            var seconds = Flag(SoulbinderStats.LendAll) ? _rules.LendAllSeconds : LendSeconds;
            Boons.LendPower = power;
            Boons.LendTicks = System.Math.Max(Boons.LendTicks, Ticks(seconds * (Flag(SoulbinderStats.LendTwo) ? 2 : 1)));
        }

        private void ShareCold()
        {
            var share = ColdShare;
            if (!InParty || !(share > 0) || S.ColdResendTicks-- > 0)
                return;
            S.ColdResendTicks = GameLoop.TicksPerSecond - 1;
            _state.TraitEnemyEffects.Add(ColdEffect, share, _rules.ColdEffectSeconds);
        }

        private void TickBoons()
        {
            if (Boons.LendTicks > 0)
                Boons.LendTicks--;
        }

        private void StrikeBoons()
        {
            if (Boons.LendTicks <= 0 || !(Boons.LendPower > 0))
                return;
            Boons.LentHits++;
            TraitHit(Boons.LendPower);
        }

        private void TraitHit(double multiplier)
        {
            if (multiplier <= 0 || !_state.EnemyAlive)
                return;
            var supporting = _combat.Supporting;
            _combat.Supporting = null;
            _combat.SkillHit(_combat.BaseKeyDamage, multiplier * _state.FocusMultiplier, StatTags.Strike);
            _combat.Supporting = supporting;
        }

        private static int Ticks(double seconds) => (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
    }
}
