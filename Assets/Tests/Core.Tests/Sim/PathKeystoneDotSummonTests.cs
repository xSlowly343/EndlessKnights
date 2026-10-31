using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class PathKeystoneDotSummonTests
    {
        private static PathRules Rules => TestWorld.Data.Combat.Paths;

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

        private static readonly Modifier Overwhelming = new Modifier(CombatStats.Damage, ModifierKind.More, 1e60, StatTags.Keyboard);

        private static double SkillDamage(GameLoop loop)
        {
            var total = 0.0;
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage && loop.Events[i].Source == DamageSource.Skill)
                    total += loop.Events[i].Amount.ToDouble();
            }
            return total;
        }

        private static void Wait(GameLoop loop, double seconds)
        {
            for (var i = 0; i < (int)System.Math.Round(seconds * GameLoop.TicksPerSecond); i++)
                loop.Tick();
        }

        private static void Kill(GameLoop loop, FakeInputSource input)
        {
            var kills = loop.State.Kills;
            input.PressKeys(1);
            loop.Tick();
            Assert.That(loop.State.Kills, Is.EqualTo(kills + 1));
        }

        [Test]
        public void KeyHitsApplyPoisonAndBurning()
        {
            var (loop, input) = Hero(HeroClass.Moonshiner,
                new Modifier(CombatStats.KeyHitPoison, ModifierKind.Flat, 0.15),
                new Modifier(CombatStats.KeyHitBurn, ModifierKind.Flat, 0.1));

            input.PressKeys(1);
            loop.Tick();

            Assert.That(loop.State.PoisonStacks, Is.EqualTo(0.15).Within(1e-9));
            Assert.That(loop.State.BurnStacks, Is.EqualTo(0.1).Within(1e-9));
        }

        [Test]
        public void BurnDamageMakesOnlyBurningStronger()
        {
            var (plain, _) = Hero(HeroClass.Viking);
            var (hot, _) = Hero(HeroClass.Viking, new Modifier(CombatStats.BurnDamage, ModifierKind.More, 1));
            plain.State.BurnStacks = 10;
            hot.State.BurnStacks = 10;

            Wait(plain, 1);
            Wait(hot, 1);

            Assert.That(SkillDamage(hot) / SkillDamage(plain), Is.EqualTo(2).Within(1e-6));
        }

        [Test]
        public void StacksAtTheCapBlowUp()
        {
            var (loop, _) = Hero(HeroClass.PlagueDoctor, new Modifier(CombatStats.DetonateAtMax, ModifierKind.Flat, 6));
            loop.State.PoisonStacks = loop.Skills.Dots.PoisonMax;

            loop.Tick();

            Assert.That(loop.Skills.Dots.Bursts, Is.EqualTo(1));
            Assert.That(loop.State.PoisonStacks, Is.EqualTo(0));
            Assert.That(SkillDamage(loop), Is.GreaterThan(0));
        }

        [Test]
        public void DotAlsoHitsTheWholeQueue()
        {
            var (loop, _) = Hero(HeroClass.Moonshiner, new Modifier(CombatStats.DotQueueShare, ModifierKind.Flat, 0.5));
            loop.State.BurnStacks = 10;

            Wait(loop, 1);

            for (var i = 0; i < GameState.QueueDepth; i++)
                Assert.That(loop.State.QueueDamage[i].Sign, Is.GreaterThan(0), $"slot {i}");
        }

        [Test]
        public void PoisonSpreadsToTheNextEnemy()
        {
            var (loop, input) = Hero(HeroClass.PlagueDoctor, Overwhelming, new Modifier(CombatStats.DotSpread, ModifierKind.Flat, 0.6));
            var (plain, plainInput) = Hero(HeroClass.PlagueDoctor, Overwhelming);
            loop.State.PoisonStacks = 20;
            plain.State.PoisonStacks = 20;

            Kill(loop, input);
            Kill(plain, plainInput);

            Assert.That(loop.State.PoisonStacks, Is.EqualTo(12).Within(0.2), "60% of 20 stacks move on, the killing key hits add vapours");
            Assert.That(plain.State.PoisonStacks, Is.EqualTo(0).Within(0.2));
        }

        [Test]
        public void MeteorFallsInsteadOfOverheating()
        {
            var (loop, input) = Hero(HeroClass.Moonshiner,
                new Modifier(CombatStats.OverheatDisabled, ModifierKind.Flat, 1),
                new Modifier(CombatStats.OverheatBurst, ModifierKind.Flat, 12),
                new Modifier(CombatStats.ResourcePerKey, ModifierKind.Flat, 200));

            input.PressKeys(1);
            loop.Tick();

            Assert.That(loop.Overload.Bursts, Is.EqualTo(1));
            Assert.That(loop.Resource.Overheated, Is.False);
            Assert.That(loop.State.Resource, Is.EqualTo(0));
            Assert.That(SkillDamage(loop), Is.GreaterThan(0));
        }

        [Test]
        public void FullPowderDischargesOnlyWithTheKeystone()
        {
            var (loop, _) = Hero(HeroClass.Captain, new Modifier(CombatStats.OverloadAtMax, ModifierKind.Flat, 8));
            var (plain, _) = Hero(HeroClass.Captain);
            loop.State.Resource = loop.Resource.Max;
            plain.State.Resource = plain.Resource.Max;

            loop.Tick();
            plain.Tick();

            Assert.That(loop.Overload.Discharges, Is.EqualTo(1));
            Assert.That(loop.State.Resource, Is.EqualTo(0).Within(1));
            Assert.That(plain.Overload.Discharges, Is.EqualTo(0));
        }

        [Test]
        public void SlainEnemiesRiseAsSkeletons()
        {
            var (loop, input) = Hero(HeroClass.Gravedigger, Overwhelming, new Modifier(CombatStats.SkeletonOnKill, ModifierKind.Flat, 1));

            Kill(loop, input);
            loop.Tick();

            Assert.That(loop.Paths.RaisedFromKills, Is.EqualTo(1));
            Assert.That(loop.Skills.Minions.Standing(MinionKind.Skeleton), Is.EqualTo(1));
        }

        [Test]
        public void SlainEnemiesJoinThePack()
        {
            var (loop, input) = Hero(HeroClass.Gravedigger, Overwhelming, new Modifier(CombatStats.BeastOnKill, ModifierKind.Flat, 1));

            Kill(loop, input);

            Assert.That(loop.Skills.Minions.Standing(MinionKind.Beast), Is.EqualTo(1));
        }

        [Test]
        public void DemonFormLeavesNoRoomForSkeletons()
        {
            var (loop, _) = Hero(HeroClass.Gravedigger, new Modifier(CombatStats.MinionCount, ModifierKind.Flat, -99));

            loop.Skills.Minions.Place(Rules.SkeletonOnKill, StatTags.Skill);

            Assert.That(loop.Skills.Minions.Standing(MinionKind.Skeleton), Is.EqualTo(0));
        }

        [Test]
        public void SummonsAlsoHitTheQueue()
        {
            var (loop, _) = Hero(HeroClass.Captain, new Modifier(CombatStats.MinionQueueShare, ModifierKind.Flat, 0.4));
            loop.Skills.Minions.Place(Rules.SkeletonOnKill, StatTags.Skill);

            Wait(loop, 1);

            for (var i = 0; i < GameState.QueueDepth; i++)
                Assert.That(loop.State.QueueDamage[i].Sign, Is.GreaterThan(0), $"slot {i}");
        }
    }
}
