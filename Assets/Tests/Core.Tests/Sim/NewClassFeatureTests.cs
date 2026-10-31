using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Tree;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class NewClassFeatureTests
    {
        private static ClassRules Rules => TestWorld.Data.Combat.Classes;

        private static GameLoop Hero(HeroClass heroClass, FakeInputSource input, double attribute = 0)
        {
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass, ClassChosen = true });
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.RebuildTree();
            if (attribute > 0)
                loop.Hero.Add(new Modifier(ClassAttributeDef.Stat, ModifierKind.Flat, attribute));
            loop.RefreshClassAttribute();
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            loop.State.BossTicksLeft = 100_000;
            return loop;
        }

        private static int Mark(string name) => TestWorld.Data.Synergy.MaskOf(new[] { name });

        [Test]
        public void TheFourClassesAreKnownButNotYetOfferedToPlayers()
        {
            Assert.That(HeroClassNames.InDevelopment.Count, Is.EqualTo(4));
            foreach (var pair in HeroClassNames.InDevelopment)
            {
                Assert.That(HeroClassNames.Of(pair.Value), Is.EqualTo(pair.Key));
                Assert.That(HeroClassNames.ByName.ContainsKey(pair.Key), Is.False, pair.Key);
                Assert.That(TestWorld.Data.ClassTreeOf(pair.Key), Is.Not.Null, pair.Key);
            }
        }

        [Test]
        public void SpinnerBondsHoldAurasLongerAboveThirtyThread()
        {
            var spinner = Hero(HeroClass.Spinner, new FakeInputSource());
            spinner.State.Resource = Rules.Bonds.ResourceAbove - 5;
            spinner.Tick();

            Assert.That(spinner.State.BondsUp, Is.False);
            Assert.That(spinner.Hero.Value(CombatStats.AuraDuration, 1, StatTags.Aura), Is.EqualTo(1).Within(1e-9));

            spinner.State.Resource = Rules.Bonds.ResourceAbove + 20;
            spinner.Tick();

            Assert.That(spinner.State.BondsUp, Is.True);
            Assert.That(spinner.Hero.Value(CombatStats.AuraDuration, 1, StatTags.Aura), Is.EqualTo(1 + Rules.Bonds.AuraDuration).Within(1e-9));
            Assert.That(spinner.Synergy.OwnMarks & Mark("bond"), Is.Not.Zero);

            spinner.State.Resource = 0;
            spinner.Tick();

            Assert.That(spinner.Hero.Value(CombatStats.AuraDuration, 1, StatTags.Aura), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void CirclesSpinThread()
        {
            var input = new FakeInputSource();
            var spinner = Hero(HeroClass.Spinner, input);
            input.Circle(2);
            spinner.Tick();

            Assert.That(spinner.State.Resource, Is.EqualTo(2 * TestWorld.Data.Combat.Resources.For("spinner").PerCircle).Within(1e-9));
        }

        [Test]
        public void OtherClassesHaveNoBonds()
        {
            var huscarl = Hero(HeroClass.Viking, new FakeInputSource());
            huscarl.State.Resource = 80;
            huscarl.Tick();

            Assert.That(huscarl.State.BondsUp, Is.False);
        }

        [Test]
        public void RimecallerKeyHitsLayColdThatRaisesDamageAndFades()
        {
            var input = new FakeInputSource();
            var rimecaller = Hero(HeroClass.Rimecaller, input);
            for (var i = 0; i < 5; i++)
            {
                input.PressKeys(5);
                rimecaller.Tick();
            }

            var cold = rimecaller.State.Cold;
            Assert.That(cold, Is.GreaterThan(10).And.LessThanOrEqualTo(25));
            Assert.That(rimecaller.Combat.Class.ColdShare, Is.EqualTo(Rules.Cold.DamageTaken * cold / Rules.Cold.Max).Within(1e-9));
            Assert.That(rimecaller.Synergy.OwnMarks & Mark("cold"), Is.Not.Zero);

            var solo = rimecaller.Combat.ConditionMultiplier(StatTags.Keyboard);
            rimecaller.State.SharedEnemy = true;
            Assert.That(solo / rimecaller.Combat.ConditionMultiplier(StatTags.Keyboard), Is.EqualTo(1 + rimecaller.Combat.Class.ColdShare).Within(1e-9));
            rimecaller.State.SharedEnemy = false;

            for (var i = 0; i < 10; i++)
                rimecaller.Tick();
            Assert.That(rimecaller.State.Cold, Is.LessThan(cold));

            rimecaller.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            Assert.That(rimecaller.State.Cold, Is.Zero);
        }

        [Test]
        public void ColdCapsAtItsMaximum()
        {
            var input = new FakeInputSource();
            var rimecaller = Hero(HeroClass.Rimecaller, input);
            rimecaller.State.Cold = Rules.Cold.Max;
            input.PressKeys(5);
            rimecaller.Tick();

            Assert.That(rimecaller.State.Cold, Is.LessThanOrEqualTo(Rules.Cold.Max));
            Assert.That(rimecaller.Combat.Class.ColdShare, Is.LessThanOrEqualTo(Rules.Cold.DamageTaken + 1e-9));
        }

        [Test]
        public void OtherClassesLayNoCold()
        {
            var input = new FakeInputSource();
            var huscarl = Hero(HeroClass.Viking, input);
            input.PressKeys(5);
            huscarl.Tick();

            Assert.That(huscarl.State.Cold, Is.Zero);
            Assert.That(huscarl.Combat.Class.ColdShare, Is.Zero);
        }

        [Test]
        public void ThornmaidenFlicksPutBloodThatBleedsInAPause()
        {
            var input = new FakeInputSource();
            var thornmaiden = Hero(HeroClass.Thornmaiden, input);
            input.Flick();
            thornmaiden.Tick();

            Assert.That(thornmaiden.State.BloodStacks, Is.EqualTo(Rules.Blood.PerFlick).Within(1e-9));

            var before = thornmaiden.State.TotalDamage;
            for (var i = 0; i < GameLoop.TicksPerSecond; i++)
                thornmaiden.Tick();

            Assert.That(thornmaiden.Features.BloodTicks, Is.EqualTo(1));
            Assert.That(thornmaiden.Synergy.OwnMarks & Mark("blood"), Is.Not.Zero);
            Assert.That((thornmaiden.State.TotalDamage - before).Sign, Is.GreaterThan(0));
        }

        [Test]
        public void BloodCapsAndOtherClassesPutNone()
        {
            var input = new FakeInputSource();
            var thornmaiden = Hero(HeroClass.Thornmaiden, input);
            for (var i = 0; i < 30; i++)
            {
                input.Flick();
                thornmaiden.Tick();
            }
            Assert.That(thornmaiden.State.BloodStacks, Is.EqualTo(Rules.Blood.Max).Within(1e-9));

            var huscarlInput = new FakeInputSource();
            var huscarl = Hero(HeroClass.Viking, huscarlInput);
            huscarlInput.Flick();
            huscarl.Tick();
            Assert.That(huscarl.State.BloodStacks, Is.Zero);
        }

        [Test]
        public void ClayshaperRaisesOneGolemOnFullClayThatStunsWithEveryHit()
        {
            var clayshaper = Hero(HeroClass.Clayshaper, new FakeInputSource());
            clayshaper.State.Resource = 1000;
            clayshaper.Tick();

            Assert.That(clayshaper.Features.GolemsRaised, Is.EqualTo(1));
            Assert.That(clayshaper.Skills.Minions.Standing(MinionKind.Golem), Is.EqualTo(1));
            Assert.That(clayshaper.State.Resource, Is.LessThan(1));
            Assert.That(clayshaper.Synergy.OwnMarks & Mark("summon"), Is.Not.Zero);

            clayshaper.State.Resource = 1000;
            for (var i = 0; i < 2 * GameLoop.TicksPerSecond; i++)
                clayshaper.Tick();

            Assert.That(clayshaper.Features.GolemsRaised, Is.EqualTo(1), "only one golem at a time");
            Assert.That(clayshaper.Skills.Minions.GolemHits, Is.EqualTo(1));
            Assert.That(clayshaper.State.StunTicks, Is.GreaterThan(0));
        }

        [Test]
        public void TheGolemFallsAndRisesAgainOnFullClay()
        {
            var clayshaper = Hero(HeroClass.Clayshaper, new FakeInputSource());
            clayshaper.State.Resource = 1000;
            clayshaper.Tick();
            for (var i = 0; i < Rules.Golem.Summon.Seconds * GameLoop.TicksPerSecond + 1; i++)
                clayshaper.Tick();

            Assert.That(clayshaper.Skills.Minions.Standing(MinionKind.Golem), Is.Zero);

            clayshaper.State.Resource = 1000;
            clayshaper.Tick();
            Assert.That(clayshaper.Features.GolemsRaised, Is.EqualTo(2));
        }

        [Test]
        public void OtherClassesRaiseNoGolem()
        {
            var huscarl = Hero(HeroClass.Viking, new FakeInputSource());
            huscarl.State.Resource = 1000;
            huscarl.Tick();

            Assert.That(huscarl.Features.GolemsRaised, Is.Zero);
        }

        [Test]
        public void TheClassAttributeStrengthensEachNewFeature()
        {
            var bonds = Hero(HeroClass.Spinner, new FakeInputSource(), 20);
            bonds.State.Resource = 80;
            bonds.Tick();
            Assert.That(bonds.Features.BondShare, Is.EqualTo(Rules.Bonds.AuraDuration * (1 + 20 * PerPoint(bonds))).Within(1e-9));

            var plainCold = Hero(HeroClass.Rimecaller, new FakeInputSource());
            var strongCold = Hero(HeroClass.Rimecaller, new FakeInputSource(), 20);
            plainCold.State.Cold = strongCold.State.Cold = 50;
            Assert.That(strongCold.Combat.Class.ColdShare / plainCold.Combat.Class.ColdShare, Is.EqualTo(1 + 20 * PerPoint(strongCold)).Within(1e-9));

            var plainBlood = Hero(HeroClass.Thornmaiden, new FakeInputSource());
            var strongBlood = Hero(HeroClass.Thornmaiden, new FakeInputSource(), 20);
            plainBlood.State.BloodStacks = strongBlood.State.BloodStacks = 10;
            Assert.That(strongBlood.Features.BloodHitsPerSecond / plainBlood.Features.BloodHitsPerSecond, Is.EqualTo(1 + 20 * PerPoint(strongBlood)).Within(1e-9));

            Assert.That(GolemDamage(20) / GolemDamage(0), Is.EqualTo(1 + 20 * PerPoint(Hero(HeroClass.Clayshaper, new FakeInputSource()))).Within(1e-6));
        }

        [Test]
        public void ANoviceHasNoneOfTheNewFeatures()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Clayshaper, ClassChosen = false });
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            loop.State.Resource = 1000;
            input.Flick();
            input.PressKeys(5);
            loop.Tick();

            Assert.That(loop.Features.GolemsRaised, Is.Zero);
            Assert.That(loop.State.BloodStacks, Is.Zero);
            Assert.That(loop.State.Cold, Is.Zero);
            Assert.That(loop.State.BondsUp, Is.False);
        }

        private static double PerPoint(GameLoop loop) => loop.ClassAttributeDef.PerPoint[0].Value;

        private static double GolemDamage(double attribute)
        {
            var clayshaper = Hero(HeroClass.Clayshaper, new FakeInputSource(), attribute);
            clayshaper.State.Resource = 1000;
            clayshaper.Tick();
            var before = clayshaper.State.TotalDamage;
            for (var i = 0; i < 4 * GameLoop.TicksPerSecond; i++)
                clayshaper.Tick();
            Assert.That(clayshaper.Skills.Minions.GolemHits, Is.EqualTo(2));
            return (clayshaper.State.TotalDamage - before).ToDouble();
        }
    }
}
