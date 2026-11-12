using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class WhispererMechanics
    {
        public static readonly int KnotEffect = GemDef.NetIdOf("trait_whisper_knots");
        public static readonly int NameEffect = GemDef.NetIdOf("trait_whisper_name");
        public static readonly int NameTimeEffect = GemDef.NetIdOf("trait_whisper_name_time");
        public static readonly int CurseEffect = GemDef.NetIdOf("trait_whisper_curse");

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private WhispererRules _rules;
        private int _knotResendTicks;
        private int _nameResendTicks;
        private int _curseResendTicks;

        public WhispererMechanics(WhispererRules rules, GameState state, StatSheet hero, CombatSystem combat)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _combat = combat;
        }

        public void UseRules(WhispererRules rules) => _rules = rules;

        private WhispererState S => _state.Whisperer;
        private AllyBoons Boons => _state.Boons;
        private bool On => _state.Is(HeroClass.Whisperer);
        private bool Flag(StatId stat) => _hero.Value(stat, 0) >= 1;
        private double Positive(StatId stat) => System.Math.Max(0, _hero.Value(stat, 0));
        private bool InParty => _state.SharedEnemy && _state.PartyMembers > 1;

        public bool Knots => On && KnotPower > 0;
        public bool Name => On && NamePower > 0;
        public bool Vow => On && VowPower > 0;

        public double KnotPower => Positive(WhispererStats.KnotPower);
        public double NamePower => Positive(WhispererStats.NamePower);
        public double VowPower => Positive(WhispererStats.VowPower);

        public double CurseMultiplier => System.Math.Max(0, _hero.Value(CombatStats.CurseEffect, 1))
            * (Vow ? (_rules.VowCurse + Positive(WhispererStats.VowCurse)) * (1 + S.Grow) * (S.BreakTicks > 0 ? _rules.VowBreakCurse : 1) : 1);

        public int KnotHits
        {
            get
            {
                var hits = System.Math.Max(_rules.KnotHitsMin, (int)System.Math.Round(_rules.KnotHits + _hero.Value(WhispererStats.KnotHits, 0)));
                return Flag(WhispererStats.KnotBoss) && _state.FightingBoss ? System.Math.Max(_rules.KnotHitsMin, hits / 2) : hits;
            }
        }

        public double KnotScale => S.Knots >= WhispererState.MaxKnots ? (Flag(WhispererStats.KnotTriple) ? _rules.KnotTriple : _rules.KnotSeventh) : 1;

        public double KnotDamageTaken => !Knots ? 0 : (KnotPower * HarmKnots + Positive(WhispererStats.KnotParty) * S.Knots) * KnotScale * CurseMultiplier;

        public double KnotTimerSlow => Knots && S.Knots >= 2 ? System.Math.Min(1, _rules.KnotSlow * KnotScale) : 0;

        public double KnotCritMultiplier => Knots && S.Knots >= 4 ? _rules.KnotCritMultiplier * KnotScale : 0;

        public double KnotGold => Knots && S.Knots >= 6 ? _rules.KnotGold * KnotScale : 0;

        private int HarmKnots => (S.Knots >= 1 ? 1 : 0) + (S.Knots >= 3 ? 1 : 0) + (S.Knots >= 5 ? 1 : 0);

        public int NameHits => System.Math.Max(_rules.NameHitsMin, (int)System.Math.Round(_rules.NameHits + _hero.Value(WhispererStats.NameHits, 0)));

        public double NameBonus
        {
            get
            {
                if (!Name || !_state.FightingBoss)
                    return 0;
                var full = NamePower * CurseMultiplier;
                if (S.Named)
                    return full;
                return Flag(WhispererStats.NameProgress) ? full * 0.5 * System.Math.Min(1, S.BossHits / (double)NameHits) : 0;
            }
        }

        public int HushHits => System.Math.Max(_rules.HushHitsMin, (int)System.Math.Round(_rules.HushHits + _hero.Value(WhispererStats.HushHits, 0)));

        public double HushPower => VowPower * (1 + CurseShare);

        public double CurseShare => S.CurseTicks > 0 ? S.Curse : 0;

        public double StrikeMultiplier() => Vow && S.BreakTicks <= 0 ? 0 : 1;

        public double DamageTaken()
        {
            if (!On || _state.SharedEnemy)
                return 1;
            return (1 + KnotDamageTaken) * (1 + NameBonus) * (1 + CurseShare);
        }

        public void AfterStrike()
        {
            StrikeBoons();
            if (!On)
                return;
            if (Knots)
                AddKnotProgress(1);
            if (Name && _state.FightingBoss)
                AddBossHits(1);
            if (Vow)
                SilentStrike();
        }

        public void OnSkillCast(GemDef skill, SkillDef? variant = null)
        {
            if (!On || !((variant ?? skill.Skill)?.CastStrike is StrikeEffect strike))
                return;
            if (strike.Curse > 0)
                Curse(strike.Curse, strike.CurseSeconds);
            if (strike.CurseHit > 0 && (CurseShare > 0 || KnotDamageTaken > 0))
                TraitHit(strike.CurseHit * (CurseShare + KnotDamageTaken + NameBonus));
            if (strike.ExtendCurse > 0 && S.CurseTicks > 0)
                S.CurseTicks += Ticks(strike.ExtendCurse);
            if (strike.Whisper > 0)
                _state.PendingResource += strike.Whisper;
            if (strike.Knots > 0 && Knots)
                Tie(strike.Knots);
            if (strike.Seventh && Knots)
                Tie(WhispererState.MaxKnots - S.Knots);
            if (strike.NameHits > 0 && Name && _state.FightingBoss)
                AddBossHits(strike.NameHits);
            if (strike.Hush > 0 && Vow)
                SendHush(strike.Hush);
            if (strike.BossTimerStopSeconds > 0 && _state.FightingBoss && !_state.SharedEnemy)
                _state.BossTimerStopTicks = System.Math.Max(_state.BossTimerStopTicks, Ticks(strike.BossTimerStopSeconds));
        }

        public void Tick()
        {
            if (!On)
                return;
            if (S.SpawnPending)
                OnNewEnemy();
            if (_state.InputStreakTicks > 0 && S.CurseTicks > 0 && --S.CurseTicks == 0)
                S.Curse = 0;
            if (Vow)
                TickVow();
            _state.TraitShareCritMultiplier = KnotCritMultiplier;
            _state.TraitShareGold = KnotGold;
            _state.TraitTimerSlow = _state.SharedEnemy ? 0 : KnotTimerSlow;
            ShareKnots();
            ShareName();
            ShareCurse();
        }

        private void ReceiveHushHits(int count)
        {
            if (!Vow || count <= 0)
                return;
            S.HushHitsBack += count;
            if (S.CurseTicks > 0)
                S.CurseTicks += Ticks(_rules.HushExtendSeconds * count);
        }

        private void OnNewEnemy()
        {
            S.SpawnPending = false;
            if (!(Flag(WhispererStats.KnotCarry) && Knots))
                S.Knots = 0;
            S.KnotHits = 0;
            S.BossHits = 0;
            S.Named = false;
            S.Grow = 0;
            S.Curse = 0;
            S.CurseTicks = 0;
            _knotResendTicks = 0;
            _nameResendTicks = 0;
            _curseResendTicks = 0;
            if (Name && _state.FightingBoss && Flag(WhispererStats.NameMemory) && _state.Enemy != null && _state.Enemy == S.RememberedBoss)
                Speak();
        }

        private void AddKnotProgress(double hits)
        {
            if (!_state.EnemyAlive || S.Knots >= WhispererState.MaxKnots)
                return;
            S.KnotHits += hits;
            var need = KnotHits;
            while (S.KnotHits >= need && S.Knots < WhispererState.MaxKnots)
            {
                S.KnotHits -= need;
                Tie(1);
            }
        }

        private void Tie(int count)
        {
            if (count <= 0 || S.Knots >= WhispererState.MaxKnots)
                return;
            var tied = count * (Flag(WhispererStats.KnotDouble) ? 2 : 1);
            S.Knots = System.Math.Min(WhispererState.MaxKnots, S.Knots + tied);
            S.KnotsTied += tied;
            _knotResendTicks = 0;
            if (Flag(WhispererStats.KnotBlast))
                TraitHit(_rules.KnotBlast * count);
            if (Flag(WhispererStats.KnotWhisper))
                _state.PendingResource += _rules.KnotWhisper * count;
        }

        private void AddBossHits(int hits)
        {
            if (S.Named || !_state.EnemyAlive)
                return;
            S.BossHits += hits;
            TryName();
        }

        private void TryName()
        {
            if (S.Named)
                return;
            if (S.BossHits >= NameHits)
                Speak();
        }

        private void Speak()
        {
            S.Named = true;
            S.Names++;
            S.RememberedBoss = _state.Enemy;
            _nameResendTicks = 0;
            if (Flag(WhispererStats.NameBlast))
                TraitHit(_rules.NameBlast);
            if (!Flag(WhispererStats.NameTime))
                return;
            if (!_state.SharedEnemy)
                _state.BossTicksLeft += Ticks(_rules.NameTimeSeconds);
            else
                _state.TraitEnemyEffects.Add(NameTimeEffect, 0, _rules.NameTimeSeconds, 1);
        }

        private void SilentStrike()
        {
            if (Flag(WhispererStats.VowGrow))
                S.Grow = System.Math.Min(_rules.VowGrowMax, S.Grow + _rules.VowGrowPerHit);
            _state.PendingResource += _rules.VowWhisper;
            if (++S.SilentHits >= HushHits)
            {
                S.SilentHits = 0;
                SendHush(1);
            }
            if (!Flag(WhispererStats.VowQueue) || ++S.QueueHits < _rules.VowQueueHits)
                return;
            S.QueueHits = 0;
            _state.AddToQueue(_combat.SkillDamage(_combat.BaseKeyDamage, _rules.VowQueueMultiplier * VowPower * _state.FocusMultiplier, StatTags.Strike));
        }

        private void TickVow()
        {
            if (S.BreakTicks > 0)
                S.BreakTicks--;
            if (!Flag(WhispererStats.VowBreak) || ++S.BreakCooldownTicks < Ticks(_rules.VowBreakEverySeconds))
                return;
            S.BreakCooldownTicks = 0;
            S.BreakTicks = Ticks(_rules.VowBreakSeconds);
        }

        private void SendHush(int count)
        {
            S.HushesSent += count;
            var power = HushPower * (1 + Positive(WhispererStats.HushParty));
            Boons.Hush(count * (Flag(WhispererStats.HushAll) ? 2 : 1), power, _rules.HushMax);
        }

        private void Curse(double share, double seconds)
        {
            var strength = share * CurseMultiplier;
            S.Curse = System.Math.Max(CurseShare, strength);
            var ticks = Ticks((seconds > 0 ? seconds : _rules.CurseSeconds) + Positive(CombatStats.CurseSeconds));
            S.CurseTicks = System.Math.Max(S.CurseTicks, ticks);
            _curseResendTicks = 0;
        }

        private void ShareKnots()
        {
            if (!InParty || !Knots || S.Knots <= 0 || _knotResendTicks-- > 0)
                return;
            _knotResendTicks = GameLoop.TicksPerSecond - 1;
            _state.TraitEnemyEffects.Add(KnotEffect, KnotDamageTaken, _rules.KnotEffectSeconds, KnotTimerSlow);
        }

        private void ShareName()
        {
            var bonus = NameBonus;
            if (!InParty || !(bonus > 0) || _nameResendTicks-- > 0)
                return;
            _nameResendTicks = GameLoop.TicksPerSecond - 1;
            _state.TraitEnemyEffects.Add(NameEffect, bonus, _rules.NameEffectSeconds);
        }

        private void ShareCurse()
        {
            if (!InParty || !(CurseShare > 0) || _curseResendTicks-- > 0)
                return;
            _curseResendTicks = GameLoop.TicksPerSecond - 1;
            _state.TraitEnemyEffects.Add(CurseEffect, CurseShare, _rules.CurseEffectSeconds);
        }

        private void StrikeBoons()
        {
            if (Boons.Hushes <= 0)
                return;
            Boons.Hushes--;
            Boons.HushHits++;
            TraitHit(Boons.HushPower);
            ReceiveHushHits(1);
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
