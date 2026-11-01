using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class GhostcaptainCannonTests
    {
        private static CannonRules Rules => TestWorld.Data.Combat.Cannons;
        private static GemDef CannonByName => TestWorld.Real.Gems["ghostcaptain_hit_1"];
        private static SummonEffect Cannon => (SummonEffect)CannonByName.Skill!.Effect;

        private static (GameLoop Loop, FakeInputSource Input) Ghostcaptain(params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Ghostcaptain }, hero);
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
            Assert.That(TestWorld.Real.SkillBook.StartingGems("ghostcaptain"), Has.Some.Property("Id").EqualTo("ghostcaptain_hit_1"));
            Assert.That(CannonByName.Skill!.Weapon, Is.EqualTo("pistol"));
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
            var broadside = DamageFromMouse(HeroClass.Ghostcaptain, Rules.BroadsidePx) - DamageFromMouse(HeroClass.Clockwright, Rules.BroadsidePx);

            Assert.That(broadside, Is.EqualTo(TestWorld.Data.Combat.KeyDamage * Rules.BroadsideBalls).Within(1e-6));
        }

        [Test]
        public void NoBroadsideBeforeThreeThousandPixels()
        {
            var px = Rules.BroadsidePx * 0.9;

            Assert.That(DamageFromMouse(HeroClass.Ghostcaptain, px), Is.EqualTo(DamageFromMouse(HeroClass.Clockwright, px)).Within(1e-6));
        }

        [Test]
        public void PowderComesFromKeys()
        {
            var (loop, input) = Ghostcaptain();

            for (var i = 0; i < 3; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
                loop.Tick();
            }

            Assert.That(loop.State.Resource, Is.EqualTo(1).Within(1e-3));
        }
    }
}
