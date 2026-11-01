using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class ShardcallerMechanics
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private ShardcallerRules _rules;

        public ShardcallerMechanics(ShardcallerRules rules, GameState state, StatSheet hero, CombatSystem combat)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _combat = combat;
        }

        public void UseRules(ShardcallerRules rules) => _rules = rules;

        private ShardcallerState S => _state.Shardcaller;
        private bool On => _state.Is(HeroClass.Shardcaller);
        private bool Flag(StatId stat) => _hero.Value(stat, 0) >= 1;
        private double Positive(StatId stat) => System.Math.Max(0, _hero.Value(stat, 0));

        public bool Shards => On && ShardPower > 0;
        public bool Garden => On && !Shards && GardenPower > 0;
        public bool Shell => On && ShellPower > 0;
        private bool Crystals => Shards || Garden;

        public double ShardPower => Positive(ShardcallerStats.ShardPower);
        public double GardenPower => Positive(ShardcallerStats.GardenPower);
        public double ShellPower => Positive(ShardcallerStats.ShellPower);

        public int StrikesPerCrystal => Shards
            ? System.Math.Max(_rules.ShardEveryMin, (int)System.Math.Round(_rules.ShardEvery + _hero.Value(ShardcallerStats.ShardEvery, 0)))
            : System.Math.Max(_rules.GardenEveryMin, (int)System.Math.Round(_rules.GardenEvery + _hero.Value(ShardcallerStats.GardenEvery, 0)));

        public int CrystalMax => Shards
            ? System.Math.Max(1, (int)System.Math.Round(_rules.ShardMax + _hero.Value(ShardcallerStats.ShardMax, 0)))
            : System.Math.Max(1, (int)System.Math.Round(_rules.GardenMax + _hero.Value(ShardcallerStats.GardenMax, 0)));

        public int SizeMax => System.Math.Max(1, (int)System.Math.Round(_rules.GardenSizeMax + _hero.Value(ShardcallerStats.GardenSize, 0)));

        public int GrowTicks => Ticks(System.Math.Max(_rules.GardenGrowMinSeconds, _rules.GardenGrowSeconds + _hero.Value(ShardcallerStats.GardenGrow, 0)));

        public double ShardArmor => System.Math.Max(0, _rules.ShardArmor + _hero.Value(ShardcallerStats.ShardArmor, 0));

        public double GardenArmor => System.Math.Max(0, _rules.GardenArmor + _hero.Value(ShardcallerStats.GardenArmor, 0));

        public double ShellPenalty => System.Math.Min(0.9, System.Math.Max(0, _rules.ShellPenalty + _hero.Value(ShardcallerStats.ShellPenalty, 0)));

        public int ShellEveryTicks => Ticks(System.Math.Max(_rules.ShellEveryMinSeconds, _rules.ShellEverySeconds + _hero.Value(ShardcallerStats.ShellEvery, 0)));

        public int CrackTicks => System.Math.Max(1, Ticks(_rules.ShellCrackSeconds + _hero.Value(ShardcallerStats.ShellCrack, 0)));

        public double CrackMultiplier => System.Math.Max(1, ShellPower) * (S.Shatter ? _rules.ShellShatterMultiplier : 1);

        public double DamageTaken()
        {
            if (!On)
                return 1;
            var multiplier = S.PrisonTicks > 0 ? _rules.PrisonMultiplier : 1;
            if (Shell)
                multiplier *= S.Cracked ? CrackMultiplier : 1 - ShellPenalty;
            return multiplier;
        }

        public void AfterStrike(bool crit)
        {
            if (!On)
                return;
            if (Shell && crit && !S.Cracked && Flag(ShardcallerStats.ShellCritHaste))
                S.ShellTicks += Ticks(_rules.ShellCritHasteSeconds);
            if (!Crystals || ++S.StrikeProgress < StrikesPerCrystal)
                return;
            S.StrikeProgress = 0;
            AddCrystals(1);
        }

        public void OnSkillCast(GemDef skill)
        {
            if (!On || !(skill.Skill?.Effect is StrikeEffect strike))
                return;
            if (strike.Resonance > 0)
                _state.PendingResource += strike.Resonance;
            if (strike.PerCrystal > 0 && S.Crystals > 0)
                TraitHit(strike.PerCrystal * S.Crystals);
            if (strike.Crystals > 0)
                AddCrystals(strike.Crystals);
            if (strike.Grow > 0)
                Grow(strike.Grow);
            if (strike.Shatter > 0)
                Detonate(strike.Shatter);
            if (strike.Crack && Shell)
                Crack();
            if (strike.PrisonSeconds > 0)
                S.PrisonTicks = System.Math.Max(S.PrisonTicks, Ticks(strike.PrisonSeconds));
            if (strike.HeartSeconds > 0)
                S.HeartTicks = System.Math.Max(S.HeartTicks, Ticks(strike.HeartSeconds));
        }

        public void Tick()
        {
            if (!On)
                return;
            if (S.SpawnPending)
                OnNewEnemy();
            if (S.PrisonTicks > 0)
                S.PrisonTicks--;
            if (S.HeartTicks > 0)
                S.HeartTicks--;
            TickGarden();
            TickShell();
            if (!Crystals)
            {
                S.Crystals = 0;
                S.Size = 0;
            }
            _state.TraitPartyDamage = PartyDamage();
        }

        public double PartyDamage()
        {
            var damage = S.HeartTicks > 0 ? _rules.HeartPartyDamage : 0;
            if (Shards)
                damage += S.Crystals * ShardArmor;
            else if (Garden)
                damage += S.Crystals * S.Size * GardenArmor;
            if (Shell && S.Cracked)
                damage += (_rules.ShellPartyDamage + Positive(ShardcallerStats.ShellPartyDamage)) * (Flag(ShardcallerStats.ShellPartyDouble) ? 2 : 1);
            return damage;
        }

        private void OnNewEnemy()
        {
            S.SpawnPending = false;
            var lost = S.LostCrystals;
            var size = S.LostSize;
            S.LostCrystals = 0;
            S.LostSize = 0;
            if (Shards && lost > 0 && Flag(ShardcallerStats.ShardOnKill))
                TraitHit(ShardPower * lost * _rules.ShardOnKillShare);
            if (!Garden)
                return;
            if (lost > 0 && Flag(ShardcallerStats.GardenTransplant))
            {
                S.Crystals = System.Math.Min(CrystalMax, lost);
                S.Size = System.Math.Max(1, size);
            }
            else if (Flag(ShardcallerStats.GardenSeed))
            {
                S.Crystals = 1;
                S.Size = 1;
            }
        }

        private void AddCrystals(int count)
        {
            if (!Crystals || !_state.EnemyAlive)
                return;
            S.Crystals = System.Math.Min(CrystalMax, S.Crystals + count);
            if (Garden && S.Size <= 0)
                S.Size = 1;
            if (Shards && S.Crystals >= CrystalMax && Flag(ShardcallerStats.ShardAvalanche))
                Detonate(1);
        }

        private void Detonate(double scale)
        {
            if (S.Crystals <= 0)
                return;
            var perCrystal = Shards ? ShardPower : Garden ? GardenPower * System.Math.Max(1, S.Size) : 0;
            if (perCrystal <= 0)
                return;
            var multiplier = perCrystal * S.Crystals * scale;
            S.Detonations++;
            TraitHit(multiplier);
            if (Shards && Flag(ShardcallerStats.ShardQueue))
                Queue(multiplier);
            S.Crystals = Shards && Flag(ShardcallerStats.ShardEcho) ? (int)(S.Crystals * _rules.ShardEchoKeep) : 0;
            if (S.Crystals == 0)
                S.Size = 0;
        }

        private void TickGarden()
        {
            if (!Garden || S.Crystals <= 0)
            {
                S.GrowTicks = 0;
                return;
            }
            if (++S.GrowTicks < GrowTicks)
                return;
            S.GrowTicks = 0;
            Grow(1);
        }

        private void Grow(int steps)
        {
            if (!Garden || S.Crystals <= 0)
                return;
            S.Size = System.Math.Min(SizeMax, S.Size + steps);
            Pulse(1);
            if (S.Size < SizeMax || !Flag(ShardcallerStats.GardenBloom))
                return;
            S.Blooms++;
            Pulse(_rules.GardenBloomMultiplier);
            S.Size = 1;
        }

        private void Pulse(double scale)
        {
            var multiplier = GardenPower * S.Crystals * S.Size * scale;
            S.Pulses++;
            TraitHit(multiplier);
            if (Flag(ShardcallerStats.GardenQueue))
                Queue(multiplier * _rules.GardenQueueShare);
        }

        private void TickShell()
        {
            if (!Shell)
            {
                S.ShellTicks = 0;
                S.CrackTicks = 0;
                return;
            }
            if (S.CrackTicks > 0)
            {
                S.CrackTicks--;
                return;
            }
            if (++S.ShellTicks >= ShellEveryTicks)
                Crack();
        }

        private void Crack()
        {
            S.ShellTicks = 0;
            S.CrackTicks = CrackTicks;
            S.Cracks++;
            S.Shatter = Flag(ShardcallerStats.ShellShatter) && S.Cracks % _rules.ShellShatterEvery == 0;
            _state.PendingResource += Positive(ShardcallerStats.ShellResonance);
        }

        private void Queue(double multiplier)
        {
            if (multiplier <= 0)
                return;
            _state.AddToQueue(_combat.SkillDamage(_combat.BaseKeyDamage, multiplier * _state.FocusMultiplier, StatTags.Keyboard));
        }

        private void TraitHit(double multiplier)
        {
            if (multiplier <= 0 || !_state.EnemyAlive)
                return;
            var supporting = _combat.Supporting;
            _combat.Supporting = null;
            _combat.SkillHit(_combat.BaseKeyDamage, multiplier * _state.FocusMultiplier, StatTags.Keyboard);
            _combat.Supporting = supporting;
        }

        private static int Ticks(double seconds) => (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
    }
}
