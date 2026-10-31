using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class PathKeystoneTests
    {
        private static double KeyDamage => TestWorld.Data.Combat.KeyDamage;

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            loop.State.HitsOnEnemy = TestWorld.Data.Combat.Conditions.FirstHits;
            loop.Events.Clear();
            return (loop, input);
        }

        private static double Damage(GameLoop loop, DamageSource source)
        {
            var total = 0.0;
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage && loop.Events[i].Source == source)
                    total += loop.Events[i].Amount.ToDouble();
            }
            return total;
        }

        private static int Hits(GameLoop loop, DamageSource source)
        {
            var count = 0;
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage && loop.Events[i].Source == source)
                    count++;
            }
            return count;
        }

        private static double KeyHit(GameLoop loop, FakeInputSource input)
        {
            loop.Events.Clear();
            input.PressKeys(1);
            loop.Tick();
            return Damage(loop, DamageSource.Keyboard);
        }

        private static void Type(GameLoop loop, FakeInputSource input, int keys)
        {
            for (var i = 0; i < keys; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
            }
        }

        private static void Wait(GameLoop loop, double seconds)
        {
            for (var i = 0; i < (int)System.Math.Round(seconds * GameLoop.TicksPerSecond); i++)
                loop.Tick();
        }

        [Test]
        public void FullResourceMakesKeyHitsStronger()
        {
            var perResource = new Modifier(CombatStats.DamagePerResource, ModifierKind.Flat, 0.8, StatTags.Keyboard);
            var (plain, plainInput) = Hero(HeroClass.Viking);
            var (empty, emptyInput) = Hero(HeroClass.Viking, perResource);
            var (full, fullInput) = Hero(HeroClass.Viking, perResource);
            full.State.ResourceFill = 1;
            full.State.Resource = 1000;

            var baseline = KeyHit(plain, plainInput);

            Assert.That(KeyHit(empty, emptyInput) / baseline, Is.EqualTo(1).Within(0.05));
            Assert.That(KeyHit(full, fullInput) / baseline, Is.EqualTo(1.8).Within(0.05));
        }

        [Test]
        public void ResourceFillFollowsTheBar()
        {
            var (loop, _) = Hero(HeroClass.Viking);
            loop.State.Resource = 50;

            loop.Tick();

            Assert.That(loop.State.ResourceFill, Is.EqualTo(loop.State.Resource / loop.Resource.Max).Within(1e-9));
        }

        [Test]
        public void FasterDecayBurnsRageTwiceAsFast()
        {
            var rules = TestWorld.Data.Combat.Resources.For("viking");
            var (plain, _) = Hero(HeroClass.Viking);
            var (fast, _) = Hero(HeroClass.Viking, new Modifier(CombatStats.ResourceDecay, ModifierKind.More, 1));
            foreach (var loop in new[] { plain, fast })
            {
                loop.State.Resource = 100;
                loop.State.KeylessTicks = 1000;
                loop.State.MouseIdleTicks = 1000;
            }

            Wait(plain, 1);
            Wait(fast, 1);

            Assert.That(100 - plain.State.Resource, Is.EqualTo(rules.DecayPerSecond).Within(1e-6));
            Assert.That(100 - fast.State.Resource, Is.EqualTo(rules.DecayPerSecond * 2).Within(1e-6));
        }

        [Test]
        public void KeysCanFillTheQuiver()
        {
            var (loop, input) = Hero(HeroClass.Amazon, new Modifier(CombatStats.ResourcePerKey, ModifierKind.Flat, 0.5));
            var bySelf = TestWorld.Data.Combat.Resources.For("amazon").PerSecond * GameLoop.TickSeconds;

            Type(loop, input, 10);

            Assert.That(loop.State.Resource, Is.EqualTo(5 + bySelf * 20).Within(1e-9));
        }

        [Test]
        public void KeysBuildPressureAndCanOverheat()
        {
            var (loop, input) = Hero(HeroClass.Moonshiner, new Modifier(CombatStats.ResourcePerKey, ModifierKind.Flat, 1));
            var decay = TestWorld.Data.Combat.Resources.For("moonshiner").InverseDecayPerSecond * GameLoop.TickSeconds;

            input.PressKeys(1);
            loop.Tick();
            Assert.That(loop.State.Resource, Is.EqualTo(1).Within(1e-9));

            loop.State.Resource = 99.5;
            input.PressKeys(1);
            loop.Tick();
            Assert.That(loop.Resource.Overheated, Is.True);
            Assert.That(decay, Is.GreaterThan(0));
        }

        [Test]
        public void PiercingKeyHitsWaitForTheNextEnemy()
        {
            var (loop, input) = Hero(HeroClass.Viking, new Modifier(CombatStats.KeyHitQueueShare, ModifierKind.Flat, 0.35));

            var hit = KeyHit(loop, input);

            Assert.That(loop.State.QueueDamage[0].ToDouble(), Is.EqualTo(hit * 0.35).Within(hit * 1e-9));
            Assert.That(loop.State.QueueDamage[1].ToDouble(), Is.EqualTo(0));
        }

        [Test]
        public void EveryTenthKeyHitCleavesTheWholeQueue()
        {
            var (loop, input) = Hero(HeroClass.Viking,
                new Modifier(CombatStats.QueueStrikeEvery, ModifierKind.Flat, 10),
                new Modifier(CombatStats.QueueStrikeMultiplier, ModifierKind.Flat, 3));

            Type(loop, input, 9);
            Assert.That(loop.State.QueueDamage[0].ToDouble(), Is.EqualTo(0));

            input.PressKeys(1);
            loop.Tick();
            var expected = loop.Combat.SkillDamage(KeyDamage, loop.State.FocusMultiplier * 3, StatTags.Keyboard).ToDouble();
            for (var i = 0; i < GameState.QueueDepth; i++)
                Assert.That(loop.State.QueueDamage[i].ToDouble(), Is.EqualTo(expected).Within(expected * 0.05), $"slot {i}");
        }

        [Test]
        public void ShadowRepeatsEveryFifthKeyHit()
        {
            var (loop, input) = Hero(HeroClass.Viking,
                new Modifier(CombatStats.CloneEvery, ModifierKind.Flat, 5),
                new Modifier(CombatStats.CloneMultiplier, ModifierKind.Flat, 1));

            Type(loop, input, 10);

            Assert.That(Hits(loop, DamageSource.Keyboard), Is.EqualTo(10));
            Assert.That(Hits(loop, DamageSource.Skill), Is.EqualTo(2));
            Assert.That(Damage(loop, DamageSource.Skill), Is.EqualTo(Damage(loop, DamageSource.Keyboard) / 5).Within(Damage(loop, DamageSource.Keyboard) * 0.01));
        }

        [Test]
        public void SeriesStrikeAlwaysCritsWithTheKeystone()
        {
            var every = TestWorld.Data.Combat.Classes.ComboStrikeEvery;
            var (loop, input) = Hero(HeroClass.Ronin, new Modifier(CombatStats.ComboStrikeCrit, ModifierKind.Flat, 1));
            var (plain, plainInput) = Hero(HeroClass.Ronin);

            Type(loop, input, every);
            Type(plain, plainInput, every);

            Assert.That(CritHits(loop), Is.EqualTo(1));
            Assert.That(CritHits(plain), Is.EqualTo(0));
        }

        private static int CritHits(GameLoop loop)
        {
            var count = 0;
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage && (loop.Events[i].Flags & HitFlags.Crit) != 0)
                    count++;
            }
            return count;
        }

        [Test]
        public void LongSeriesAddsDamageUpToTheCap()
        {
            var perCombo = new Modifier(CombatStats.DamagePerCombo, ModifierKind.Flat, 0.005, StatTags.Keyboard);
            var cap = new Modifier(CombatStats.DamagePerComboMax, ModifierKind.Flat, 1, StatTags.Keyboard);
            var (plain, plainInput) = Hero(HeroClass.Viking);
            var (mid, midInput) = Hero(HeroClass.Viking, perCombo, cap);
            var (long_, longInput) = Hero(HeroClass.Viking, perCombo, cap);
            mid.State.Combo = 100;
            long_.State.Combo = 1000;

            var baseline = KeyHit(plain, plainInput);

            Assert.That(KeyHit(mid, midInput) / baseline, Is.EqualTo(1.5).Within(0.01));
            Assert.That(KeyHit(long_, longInput) / baseline, Is.EqualTo(2).Within(0.01));
        }

        [Test]
        public void EveryFourthCastIsASuperCast()
        {
            var (loop, _) = Hero(HeroClass.Viking,
                new Modifier(CombatStats.SkillSuperEvery, ModifierKind.Flat, 4),
                new Modifier(CombatStats.SkillSuperMultiplier, ModifierKind.Flat, 4));
            var slot = loop.Skills.Equip(TestWorld.Data.Gems["through_stride"]);
            var powers = new double[8];

            for (var i = 0; i < powers.Length; i++)
            {
                loop.State.Resource = 100;
                loop.State.Skills[slot].CooldownTicks = 0;
                loop.Skills.TryCast(slot);
                powers[i] = loop.State.Skills[slot].CastPower;
            }

            Assert.That(powers, Is.EqualTo(new double[] { 1, 1, 1, 5, 1, 1, 1, 5 }));
        }

        [Test]
        public void CompanionStrikesEvenWithoutInput()
        {
            var (loop, _) = Hero(HeroClass.Amazon,
                new Modifier(CombatStats.CompanionHitsPerSecond, ModifierKind.Flat, 1.2),
                new Modifier(CombatStats.CompanionMultiplier, ModifierKind.Flat, 0.5));

            Wait(loop, 10);

            Assert.That(loop.Paths.CompanionHits, Is.EqualTo(12));
        }

        [Test]
        public void BalancedKeysAndMouseHitHarder()
        {
            var balance = new Modifier(CombatStats.DamageAtBalance, ModifierKind.Flat, 0.6);
            var (even, evenInput) = Hero(HeroClass.Viking, balance);
            var (keys, keysInput) = Hero(HeroClass.Viking, balance);
            var pxPerHit = TestWorld.Data.Combat.MousePxPerHit;

            for (var i = 0; i < 200; i++)
            {
                evenInput.PressKeys(1);
                evenInput.MoveMouse((float)pxPerHit);
                keysInput.PressKeys(1);
                even.Tick();
                keys.Tick();
                even.Tick();
                keys.Tick();
            }

            Assert.That(even.Combat.Balance(), Is.GreaterThan(0.95));
            Assert.That(keys.Combat.Balance(), Is.EqualTo(0));
            Assert.That(even.Combat.ConditionMultiplier(StatTags.Keyboard), Is.GreaterThan(1.57));
            Assert.That(keys.Combat.ConditionMultiplier(StatTags.Keyboard), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void LongFightWithOneEnemyRampsUpToTheCap()
        {
            var (loop, _) = Hero(HeroClass.Stormweaver,
                new Modifier(CombatStats.RampPerSecond, ModifierKind.Flat, 0.05),
                new Modifier(CombatStats.RampMax, ModifierKind.Flat, 1));

            Assert.That(loop.Combat.ConditionMultiplier(StatTags.Keyboard), Is.EqualTo(1).Within(1e-9));
            Wait(loop, 10);
            Assert.That(loop.Combat.ConditionMultiplier(StatTags.Keyboard), Is.EqualTo(1.5).Within(1e-9));
            Wait(loop, 30);
            Assert.That(loop.Combat.ConditionMultiplier(StatTags.Keyboard), Is.EqualTo(2).Within(1e-9));
        }

        [Test]
        public void OverloadAlsoHitsTheWholeQueue()
        {
            var (loop, _) = Hero(HeroClass.Stormweaver, new Modifier(CombatStats.OverloadQueueShare, ModifierKind.Flat, 1));
            loop.State.Resource = 1000;

            loop.Tick();

            Assert.That(loop.Overload.Discharges, Is.EqualTo(1));
            for (var i = 0; i < GameState.QueueDepth; i++)
                Assert.That(loop.State.QueueDamage[i].Sign, Is.GreaterThan(0), $"slot {i}");
        }

        [Test]
        public void IdleHeroStrikesOnlyWithoutInput()
        {
            var (loop, input) = Hero(HeroClass.Nomad, new Modifier(CombatStats.IdleHitsPerSecond, ModifierKind.Flat, 2));

            Wait(loop, 5);
            Assert.That(loop.Paths.IdleHits, Is.EqualTo(10));

            for (var i = 0; i < 50; i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }
            Assert.That(loop.Paths.IdleHits, Is.EqualTo(10));
        }
    }
}
