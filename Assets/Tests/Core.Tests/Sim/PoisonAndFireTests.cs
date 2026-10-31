using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class PoisonAndFireTests
    {
        private static DotRules Rules => TestWorld.Data.Combat.Dots;
        private static double KeyDamage => TestWorld.Data.Combat.KeyDamage;
        private static GemDef Vial => TestWorld.Real.Gems["vial_from_belt"];
        private static GemDef Bottle => TestWorld.Real.Gems["fuse_bottle"];

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
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e6));
            loop.Events.Clear();
            return (loop, input);
        }

        private static int Equip(GameLoop loop, GemDef gem)
        {
            TestWorld.ArmFor(loop, gem);
            return loop.Skills.Equip(gem);
        }

        private static void Wait(GameLoop loop, double seconds)
        {
            for (var i = 0; i < (int)System.Math.Round(seconds * GameLoop.TicksPerSecond); i++)
                loop.Tick();
        }

        private static double Dealt(GameLoop loop, BigNumber hp) => (hp - loop.State.EnemyHp).ToDouble();

        [Test]
        public void EveryTenthKeyHitOfAVenomistAddsAPoisonStack()
        {
            var (venomist, venomistKeys) = Hero(HeroClass.PlagueDoctor);
            var (huscarl, huscarlKeys) = Hero(HeroClass.Viking);

            for (var tick = 0; tick < 10; tick++)
            {
                venomistKeys.PressKeys(3);
                huscarlKeys.PressKeys(3);
                venomist.Tick();
                huscarl.Tick();
            }

            Assert.That(Rules.VapourKeyHits, Is.EqualTo(10));
            Assert.That(venomist.State.KeyHits, Is.GreaterThan(10));
            Assert.That(venomist.State.PoisonStacks, Is.EqualTo(venomist.State.KeyHits / Rules.VapourKeyHits).Within(0.1), "a stack of poisonous vapours per ten counted key hits");
            Assert.That(huscarl.State.PoisonStacks, Is.Zero, "only the Venomist breathes vapours");
        }

        [Test]
        public void PoisonousVapoursStopAtThePoisonCap()
        {
            var (loop, input) = Hero(HeroClass.PlagueDoctor);

            for (var tick = 0; tick < 3000; tick++)
            {
                input.PressKeys(3);
                loop.Tick();
            }

            Assert.That(loop.State.PoisonStacks, Is.EqualTo(loop.Skills.Dots.PoisonMax).Within(1e-9));
        }

        [Test]
        public void VialAddsThreePoisonStacks()
        {
            var (loop, _) = Hero(HeroClass.PlagueDoctor);
            loop.State.Resource = 100;

            Assert.That(loop.Skills.TryCast(Equip(loop, Vial)), Is.EqualTo(CastResult.Cast));

            Assert.That(loop.State.PoisonStacks, Is.EqualTo(3));
        }

        [Test]
        public void PoisonTicksOncePerSecondEvenWithoutInput()
        {
            var (loop, _) = Hero(HeroClass.PlagueDoctor);
            loop.State.PoisonStacks = 10;
            var hp = loop.State.EnemyHp;

            Wait(loop, 3);

            Assert.That(Dealt(loop, hp), Is.EqualTo(KeyDamage * 10 * Rules.PoisonHitsPerStack * 3).Within(1e-6));
        }

        [Test]
        public void PoisonStacksStopAtTheLimit()
        {
            var (loop, _) = Hero(HeroClass.PlagueDoctor);
            loop.State.PoisonStacks = Rules.PoisonMax - 1;

            loop.Skills.Dots.Apply(new AddStacksEffect(DotKind.Poison, 3, 0, 0));

            Assert.That(loop.State.PoisonStacks, Is.EqualTo(Rules.PoisonMax));
        }

        [Test]
        public void PoisonStacksStatRaisesTheLimit()
        {
            var (loop, _) = Hero(HeroClass.PlagueDoctor, new Modifier(CombatStats.PoisonStacks, ModifierKind.Flat, 2));
            loop.State.PoisonStacks = Rules.PoisonMax;

            loop.Skills.Dots.Apply(new AddStacksEffect(DotKind.Poison, 3, 0, 0));

            Assert.That(loop.State.PoisonStacks, Is.EqualTo(Rules.PoisonMax + 2));
        }

        [Test]
        public void StirringWithMouseCirclesAddsStacksToTheNextVial()
        {
            var (loop, input) = Hero(HeroClass.PlagueDoctor);
            for (var i = 0; i < Rules.StirMax + 3; i++)
            {
                input.Circle(1);
                loop.Tick();
            }

            loop.Skills.Dots.Apply(new AddStacksEffect(DotKind.Poison, 3, 0, 0));

            Assert.That(loop.State.PoisonStacks, Is.EqualTo(3 + Rules.StirMax));
            Assert.That(loop.Skills.Dots.Stir, Is.EqualTo(0));
        }

        [Test]
        public void StacksLeaveWithTheEnemy()
        {
            var (loop, _) = Hero(HeroClass.PlagueDoctor);
            loop.State.PoisonStacks = 10;
            loop.State.BurnStacks = 5;

            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e6));

            Assert.That(loop.State.PoisonStacks, Is.EqualTo(0));
            Assert.That(loop.State.BurnStacks, Is.EqualTo(0));
        }

        [Test]
        public void DotDamageAndDotTaggedDamageMakeTicksStronger()
        {
            var (loop, _) = Hero(HeroClass.PlagueDoctor,
                new Modifier(CombatStats.DotDamage, ModifierKind.Increased, 0.5),
                new Modifier(CombatStats.Damage, ModifierKind.More, 1, StatTags.DoT));
            loop.State.PoisonStacks = 10;
            var hp = loop.State.EnemyHp;

            Wait(loop, 1);

            Assert.That(Dealt(loop, hp), Is.EqualTo(KeyDamage * 10 * Rules.PoisonHitsPerStack * 1.5 * 2).Within(1e-6));
        }

        [Test]
        public void FuseBottleStrikesAndSetsTheEnemyOnFire()
        {
            var (loop, _) = Hero(HeroClass.Moonshiner);
            var hp = loop.State.EnemyHp;

            Assert.That(loop.Skills.TryCast(Equip(loop, Bottle)), Is.EqualTo(CastResult.Cast));

            Assert.That(loop.State.BurnStacks, Is.EqualTo(3));
            Assert.That(Dealt(loop, hp), Is.EqualTo(KeyDamage * 2).Within(1e-6));
        }

        [Test]
        public void MovingTheMouseFansTheFire()
        {
            var (still, _) = Hero(HeroClass.Moonshiner);
            var (fanned, input) = Hero(HeroClass.Moonshiner);
            still.State.BurnStacks = 10;
            fanned.State.BurnStacks = 10;
            var hp = still.State.EnemyHp;

            Wait(still, 1);
            for (var i = 0; i < GameLoop.TicksPerSecond; i++)
            {
                input.MoveMouse(1);
                fanned.Tick();
            }

            var burn = KeyDamage * 10 * Rules.BurnHitsPerStack;
            Assert.That(Dealt(still, hp), Is.EqualTo(burn).Within(1e-6));
            Assert.That(Dealt(fanned, hp), Is.EqualTo(burn * Rules.FanningMultiplier).Within(0.01));
        }

        [Test]
        public void PressureGrowsFromSkillsAndFallsByItself()
        {
            var (loop, _) = Hero(HeroClass.Moonshiner);
            var index = Equip(loop, Bottle);

            loop.Skills.TryCast(index);
            Assert.That(loop.State.Resource, Is.EqualTo(Bottle.Skill!.ResourceCost));

            Wait(loop, 1);
            Assert.That(loop.State.Resource, Is.EqualTo(Bottle.Skill.ResourceCost - 5).Within(1e-9));
        }

        [Test]
        public void FullPressureOverheatsAndBlocksSkills()
        {
            var (loop, _) = Hero(HeroClass.Moonshiner);
            var index = Equip(loop, Bottle);
            loop.State.Resource = 95;

            Assert.That(loop.Skills.TryCast(index), Is.EqualTo(CastResult.Cast));
            Assert.That(loop.Resource.Overheated, Is.True);

            Wait(loop, Bottle.Skill!.CooldownSeconds);
            Assert.That(loop.Skills.TryCast(index), Is.EqualTo(CastResult.NotEnoughResource));

            Wait(loop, 5 - Bottle.Skill.CooldownSeconds);
            Assert.That(loop.Resource.Overheated, Is.False);
            Assert.That(loop.Skills.TryCast(index), Is.EqualTo(CastResult.Cast));
        }

        [Test]
        public void OverheatDisabledLetsPressureStayFull()
        {
            var (loop, _) = Hero(HeroClass.Moonshiner, new Modifier(CombatStats.OverheatDisabled, ModifierKind.Flat, 1));
            var index = Equip(loop, Bottle);
            loop.State.Resource = 95;

            loop.Skills.TryCast(index);

            Assert.That(loop.Resource.Overheated, Is.False);
        }

        [Test]
        public void SureToBlowDetonatesTenSecondsOfStacks()
        {
            var (loop, _) = Hero(HeroClass.PlagueDoctor);
            loop.State.PoisonStacks = 10;
            var hp = loop.State.EnemyHp;

            loop.Skills.Dots.Detonate((DetonateEffect)TestWorld.Real.Gems["sure_to_blow"].Skill!.Effect);

            Assert.That(Dealt(loop, hp), Is.EqualTo(KeyDamage * 10 * Rules.PoisonHitsPerStack * 10).Within(1e-6));
            Assert.That(loop.State.PoisonStacks, Is.EqualTo(0));
        }

        [Test]
        public void ExhaleBurnsThreeStacksASecond()
        {
            var (loop, _) = Hero(HeroClass.Moonshiner);
            var exhale = (AddStacksEffect)TestWorld.Real.Gems["exhale"].Skill!.Effect;

            for (var i = 0; i < GameLoop.TicksPerSecond; i++)
                loop.Skills.Dots.Flow(exhale);

            Assert.That(loop.State.BurnStacks, Is.EqualTo(3).Within(1e-9));
        }

        [Test]
        public void DotPathSkillsAreOpen()
        {
            foreach (var id in new[] { "sure_to_blow", "sky_stone", "exhale" })
                Assert.That(TestWorld.Real.Gems[id].Locked, Is.False, id);
        }
    }
}
