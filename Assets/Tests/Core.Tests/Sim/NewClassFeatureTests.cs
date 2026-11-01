using System.Linq;
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
        public void TheFourClassesAreOfferedToPlayersWithAWeaponAStartAndThreePaths()
        {
            Assert.That(HeroClassNames.InDevelopment, Is.Empty);
            foreach (var heroClass in new[] { HeroClass.Mothcaller, HeroClass.Cartomancer, HeroClass.Herbwitch, HeroClass.Golemancer })
            {
                var name = HeroClassNames.Of(heroClass);
                Assert.That(HeroClassNames.ByName[name], Is.EqualTo(heroClass));
                Assert.That(TestWorld.Data.ClassTreeOf(name), Is.Not.Null, name);
                Assert.That(TestWorld.Data.Weapons.Types.Count(type => type.HeroClass == heroClass), Is.EqualTo(1), name);
                Assert.That(TestWorld.Data.Ascendancy.SubclassesOf(name).Count, Is.EqualTo(3), name);
                Assert.That(TestWorld.Data.SkillBook.StartingGems(name), Is.Not.Empty, name);
            }
        }

        [Test]
        public void MothcallerBondsHoldAurasLongerAboveThirtyThread()
        {
            var mothcaller = Hero(HeroClass.Mothcaller, new FakeInputSource());
            mothcaller.State.Resource = Rules.Bonds.ResourceAbove - 5;
            mothcaller.Tick();

            Assert.That(mothcaller.State.BondsUp, Is.False);
            Assert.That(mothcaller.Hero.Value(CombatStats.AuraDuration, 1, StatTags.Aura), Is.EqualTo(1).Within(1e-9));

            mothcaller.State.Resource = Rules.Bonds.ResourceAbove + 20;
            mothcaller.Tick();

            Assert.That(mothcaller.State.BondsUp, Is.True);
            Assert.That(mothcaller.Hero.Value(CombatStats.AuraDuration, 1, StatTags.Aura), Is.EqualTo(1 + Rules.Bonds.AuraDuration).Within(1e-9));
            Assert.That(mothcaller.Synergy.OwnMarks & Mark("bond"), Is.Not.Zero);

            mothcaller.State.Resource = 0;
            mothcaller.Tick();

            Assert.That(mothcaller.Hero.Value(CombatStats.AuraDuration, 1, StatTags.Aura), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void MouseMovementSpinsThread()
        {
            var input = new FakeInputSource();
            var mothcaller = Hero(HeroClass.Mothcaller, input);
            var pxPerPoint = TestWorld.Data.Combat.Resources.For("mothcaller").MousePxPerPoint;
            input.MoveMouse((float)(2 * pxPerPoint));
            mothcaller.Tick();

            Assert.That(mothcaller.State.Resource, Is.EqualTo(2).Within(1e-6));
        }

        [Test]
        public void OtherClassesHaveNoBonds()
        {
            var huscarl = Hero(HeroClass.Runedancer, new FakeInputSource());
            huscarl.State.Resource = 80;
            huscarl.Tick();

            Assert.That(huscarl.State.BondsUp, Is.False);
        }

        [Test]
        public void CartomancerKeyHitsLayColdThatRaisesDamageAndFades()
        {
            var input = new FakeInputSource();
            var cartomancer = Hero(HeroClass.Cartomancer, input);
            for (var i = 0; i < 5; i++)
            {
                input.PressKeys(5);
                cartomancer.Tick();
            }

            var cold = cartomancer.State.Cold;
            Assert.That(cold, Is.GreaterThan(10).And.LessThanOrEqualTo(25));
            Assert.That(cartomancer.Combat.Class.ColdShare, Is.EqualTo(Rules.Cold.DamageTaken * cold / Rules.Cold.Max).Within(1e-9));
            Assert.That(cartomancer.Synergy.OwnMarks & Mark("cold"), Is.Not.Zero);

            var solo = cartomancer.Combat.ConditionMultiplier(StatTags.Keyboard);
            cartomancer.State.SharedEnemy = true;
            Assert.That(solo / cartomancer.Combat.ConditionMultiplier(StatTags.Keyboard), Is.EqualTo(1 + cartomancer.Combat.Class.ColdShare).Within(1e-9));
            cartomancer.State.SharedEnemy = false;

            for (var i = 0; i < 10; i++)
                cartomancer.Tick();
            Assert.That(cartomancer.State.Cold, Is.LessThan(cold));

            cartomancer.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            Assert.That(cartomancer.State.Cold, Is.Zero);
        }

        [Test]
        public void ColdCapsAtItsMaximum()
        {
            var input = new FakeInputSource();
            var cartomancer = Hero(HeroClass.Cartomancer, input);
            cartomancer.State.Cold = Rules.Cold.Max;
            input.PressKeys(5);
            cartomancer.Tick();

            Assert.That(cartomancer.State.Cold, Is.LessThanOrEqualTo(Rules.Cold.Max));
            Assert.That(cartomancer.Combat.Class.ColdShare, Is.LessThanOrEqualTo(Rules.Cold.DamageTaken + 1e-9));
        }

        [Test]
        public void OtherClassesLayNoCold()
        {
            var input = new FakeInputSource();
            var huscarl = Hero(HeroClass.Runedancer, input);
            input.PressKeys(5);
            huscarl.Tick();

            Assert.That(huscarl.State.Cold, Is.Zero);
            Assert.That(huscarl.Combat.Class.ColdShare, Is.Zero);
        }

        [Test]
        public void HerbwitchMouseMovementPutsBloodThatBleedsInAPause()
        {
            var input = new FakeInputSource();
            var herbwitch = Hero(HeroClass.Herbwitch, input);
            input.MoveMouse(1000);
            herbwitch.Tick();

            Assert.That(herbwitch.State.BloodStacks, Is.EqualTo(Rules.Blood.PerThousandPx).Within(1e-6));

            var before = herbwitch.State.TotalDamage;
            for (var i = 0; i < GameLoop.TicksPerSecond; i++)
                herbwitch.Tick();

            Assert.That(herbwitch.Features.BloodTicks, Is.EqualTo(1));
            Assert.That(herbwitch.Synergy.OwnMarks & Mark("blood"), Is.Not.Zero);
            Assert.That((herbwitch.State.TotalDamage - before).Sign, Is.GreaterThan(0));
        }

        [Test]
        public void BloodCapsAndOtherClassesPutNone()
        {
            var input = new FakeInputSource();
            var herbwitch = Hero(HeroClass.Herbwitch, input);
            for (var i = 0; i < 30; i++)
            {
                input.MoveMouse(1000);
                herbwitch.Tick();
            }
            Assert.That(herbwitch.State.BloodStacks, Is.EqualTo(Rules.Blood.Max).Within(1e-9));

            var huscarlInput = new FakeInputSource();
            var huscarl = Hero(HeroClass.Runedancer, huscarlInput);
            huscarlInput.MoveMouse(1000);
            huscarl.Tick();
            Assert.That(huscarl.State.BloodStacks, Is.Zero);
        }

        [Test]
        public void GolemancerRaisesOneGolemOnFullClayThatStunsWithEveryHit()
        {
            var golemancer = Hero(HeroClass.Golemancer, new FakeInputSource());
            golemancer.State.Resource = 1000;
            golemancer.Tick();

            Assert.That(golemancer.Features.GolemsRaised, Is.EqualTo(1));
            Assert.That(golemancer.Skills.Minions.Standing(MinionKind.Golem), Is.EqualTo(1));
            Assert.That(golemancer.State.Resource, Is.LessThan(1));
            Assert.That(golemancer.Synergy.OwnMarks & Mark("summon"), Is.Not.Zero);

            golemancer.State.Resource = 1000;
            for (var i = 0; i < 2 * GameLoop.TicksPerSecond; i++)
                golemancer.Tick();

            Assert.That(golemancer.Features.GolemsRaised, Is.EqualTo(1), "only one golem at a time");
            Assert.That(golemancer.Skills.Minions.GolemHits, Is.EqualTo(1));
            Assert.That(golemancer.State.StunTicks, Is.GreaterThan(0));
        }

        [Test]
        public void TheGolemFallsAndRisesAgainOnFullClay()
        {
            var golemancer = Hero(HeroClass.Golemancer, new FakeInputSource());
            golemancer.State.Resource = 1000;
            golemancer.Tick();
            for (var i = 0; i < Rules.Golem.Summon.Seconds * GameLoop.TicksPerSecond + 1; i++)
                golemancer.Tick();

            Assert.That(golemancer.Skills.Minions.Standing(MinionKind.Golem), Is.Zero);

            golemancer.State.Resource = 1000;
            golemancer.Tick();
            Assert.That(golemancer.Features.GolemsRaised, Is.EqualTo(2));
        }

        [Test]
        public void OtherClassesRaiseNoGolem()
        {
            var huscarl = Hero(HeroClass.Runedancer, new FakeInputSource());
            huscarl.State.Resource = 1000;
            huscarl.Tick();

            Assert.That(huscarl.Features.GolemsRaised, Is.Zero);
        }

        [Test]
        public void TheClassAttributeStrengthensEachNewFeature()
        {
            var bonds = Hero(HeroClass.Mothcaller, new FakeInputSource(), 20);
            bonds.State.Resource = 80;
            bonds.Tick();
            Assert.That(bonds.Features.BondShare, Is.EqualTo(Rules.Bonds.AuraDuration * (1 + 20 * PerPoint(bonds))).Within(1e-9));

            var plainCold = Hero(HeroClass.Cartomancer, new FakeInputSource());
            var strongCold = Hero(HeroClass.Cartomancer, new FakeInputSource(), 20);
            plainCold.State.Cold = strongCold.State.Cold = 50;
            Assert.That(strongCold.Combat.Class.ColdShare / plainCold.Combat.Class.ColdShare, Is.EqualTo(1 + 20 * PerPoint(strongCold)).Within(1e-9));

            var plainBlood = Hero(HeroClass.Herbwitch, new FakeInputSource());
            var strongBlood = Hero(HeroClass.Herbwitch, new FakeInputSource(), 20);
            plainBlood.State.BloodStacks = strongBlood.State.BloodStacks = 10;
            Assert.That(strongBlood.Features.BloodHitsPerSecond / plainBlood.Features.BloodHitsPerSecond, Is.EqualTo(1 + 20 * PerPoint(strongBlood)).Within(1e-9));

            Assert.That(GolemDamage(20) / GolemDamage(0), Is.EqualTo(1 + 20 * PerPoint(Hero(HeroClass.Golemancer, new FakeInputSource()))).Within(1e-6));
        }

        [Test]
        public void ANoviceHasNoneOfTheNewFeatures()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Golemancer, ClassChosen = false });
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
            var golemancer = Hero(HeroClass.Golemancer, new FakeInputSource(), attribute);
            golemancer.State.Resource = 1000;
            golemancer.Tick();
            var before = golemancer.State.TotalDamage;
            for (var i = 0; i < 4 * GameLoop.TicksPerSecond; i++)
                golemancer.Tick();
            Assert.That(golemancer.Skills.Minions.GolemHits, Is.EqualTo(2));
            return (golemancer.State.TotalDamage - before).ToDouble();
        }
    }
}
