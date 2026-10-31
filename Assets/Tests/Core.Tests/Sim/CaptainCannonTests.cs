using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CaptainCannonTests
    {
        private static CannonRules Rules => TestWorld.Data.Combat.Cannons;
        private static GemDef CannonByName => TestWorld.Real.Gems["cannon_by_name"];
        private static SummonEffect Cannon => (SummonEffect)CannonByName.Skill!.Effect;

        private static (GameLoop Loop, FakeInputSource Input) Captain(params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Captain }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e6));
            loop.Events.Clear();
            return (loop, input);
        }

        private static Modifier NoFire => new Modifier(CombatStats.CannonIgnite, ModifierKind.Flat, -Rules.IgniteStacks);

        private static void Wait(GameLoop loop, double seconds)
        {
            for (var i = 0; i < (int)System.Math.Round(seconds * GameLoop.TicksPerSecond); i++)
                loop.Tick();
        }

        private static void Place(GameLoop loop, int count)
        {
            for (var i = 0; i < count; i++)
                loop.Skills.Minions.Place(Cannon, CannonByName.Tags);
        }

        [Test]
        public void CannonByNameIsTheCaptainsStartingSkill()
        {
            Assert.That(TestWorld.Real.SkillBook.StartingGems("captain"), Has.Some.Property("Id").EqualTo("cannon_by_name"));
            Assert.That(CannonByName.Skill!.Weapon, Is.EqualTo("pistol"));
        }

        [Test]
        public void CastingPlacesACannon()
        {
            var (loop, _) = Captain();
            loop.State.Resource = 100;
            TestWorld.ArmFor(loop, CannonByName);
            var index = loop.Skills.Equip(CannonByName);

            Assert.That(loop.Skills.TryCast(index), Is.EqualTo(CastResult.Cast));

            Assert.That(loop.Skills.Minions.Standing(MinionKind.Cannon), Is.EqualTo(1));
        }

        [Test]
        public void CannonFiresOncePerSecondByItselfAndFeedsPowder()
        {
            var (loop, _) = Captain(NoFire);
            Place(loop, 1);
            var hp = loop.State.EnemyHp;

            Wait(loop, 3);

            Assert.That(loop.Skills.Minions.CannonShots, Is.EqualTo(3));
            Assert.That(loop.State.Resource, Is.EqualTo(3 * Rules.ResourcePerShot).Within(1e-9));
            var expected = TestWorld.Data.Combat.KeyDamage * Cannon.Multiplier * 3;
            Assert.That((hp - loop.State.EnemyHp).ToDouble(), Is.EqualTo(expected).Within(1e-6));
        }

        [Test]
        public void CannonStandsForItsSecondsThenLeaves()
        {
            var (loop, _) = Captain();
            Place(loop, 1);

            Wait(loop, Cannon.Seconds - 1);
            Assert.That(loop.Skills.Minions.Standing(MinionKind.Cannon), Is.EqualTo(1));
            Wait(loop, 1);

            Assert.That(loop.Skills.Minions.Standing(MinionKind.Cannon), Is.EqualTo(0));
            Assert.That(loop.Skills.Minions.CannonShots, Is.EqualTo((long)Cannon.Seconds));
        }

        [Test]
        public void OnlyThreeCannonsStandAndTheOldestMakesWay()
        {
            var (loop, _) = Captain();
            Place(loop, 1);
            Wait(loop, 5);
            Place(loop, Rules.Max);

            Assert.That(loop.Skills.Minions.Standing(MinionKind.Cannon), Is.EqualTo(Rules.Max));
            Wait(loop, Cannon.Seconds - 5);
            Assert.That(loop.Skills.Minions.Standing(MinionKind.Cannon), Is.EqualTo(Rules.Max));
        }

        [Test]
        public void CannonCountRaisesTheLimit()
        {
            var (loop, _) = Captain(new Modifier(CombatStats.CannonCount, ModifierKind.Flat, 2));

            Place(loop, Rules.Max + 3);

            Assert.That(loop.Skills.Minions.Standing(MinionKind.Cannon), Is.EqualTo(Rules.Max + 2));
        }

        [Test]
        public void MinionSpeedAndSecondsChangeTheCannon()
        {
            var (loop, _) = Captain(
                new Modifier(CombatStats.MinionSpeed, ModifierKind.Increased, 1),
                new Modifier(CombatStats.MinionSeconds, ModifierKind.More, -0.5));
            Place(loop, 1);

            Wait(loop, Cannon.Seconds);

            Assert.That(loop.Skills.Minions.Standing(MinionKind.Cannon), Is.EqualTo(0));
            Assert.That(loop.Skills.Minions.CannonShots, Is.EqualTo((long)Cannon.Seconds));
        }

        [Test]
        public void MinionDamageMakesCannonsHitHarder()
        {
            var (loop, _) = Captain(NoFire, new Modifier(CombatStats.Damage, ModifierKind.More, 0.5, StatTags.Minion));
            Place(loop, 1);
            var hp = loop.State.EnemyHp;

            Wait(loop, 1);

            var expected = TestWorld.Data.Combat.KeyDamage * Cannon.Multiplier * 1.5;
            Assert.That((hp - loop.State.EnemyHp).ToDouble(), Is.EqualTo(expected).Within(1e-6));
        }

        private static double DamageFromMouse(HeroClass heroClass, double px)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass }, hero);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e6));
            var hp = loop.State.EnemyHp;
            for (var i = 0; i < 30; i++)
            {
                input.MoveMouse((float)(px / 30));
                loop.Tick();
            }
            return (hp - loop.State.EnemyHp).ToDouble();
        }

        [Test]
        public void ShipFiresABroadsideForEveryThreeThousandPixels()
        {
            var broadside = DamageFromMouse(HeroClass.Captain, Rules.BroadsidePx) - DamageFromMouse(HeroClass.Clockmaker, Rules.BroadsidePx);

            Assert.That(broadside, Is.EqualTo(TestWorld.Data.Combat.KeyDamage * Rules.BroadsideBalls).Within(1e-6));
        }

        [Test]
        public void NoBroadsideBeforeThreeThousandPixels()
        {
            var px = Rules.BroadsidePx * 0.9;

            Assert.That(DamageFromMouse(HeroClass.Captain, px), Is.EqualTo(DamageFromMouse(HeroClass.Clockmaker, px)).Within(1e-6));
        }

        [Test]
        public void PowderComesFromKeys()
        {
            var (loop, input) = Captain();

            for (var i = 0; i < 3; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
                loop.Tick();
            }

            Assert.That(loop.State.Resource, Is.EqualTo(1).Within(1e-3));
        }

        [Test]
        public void OneMoreCannonAddsTwoCannonsForThirtySeconds()
        {
            var gem = TestWorld.Real.Gems["one_more_cannon"];

            Assert.That(gem.Locked, Is.False);
            var aura = (AuraEffect)gem.Skill!.Effect;
            Assert.That(aura.Modifiers[0].Stat, Is.EqualTo(CombatStats.CannonCount));
            Assert.That(aura.Modifiers[0].Value, Is.EqualTo(2));
            Assert.That(gem.Skill.DurationSeconds, Is.EqualTo(30));
        }
    }
}
