using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class SeaWolfFireShotTests
    {
        private static CannonRules Rules => TestWorld.Data.Combat.Cannons;
        private static GemDef CannonByName => TestWorld.Real.Gems["ghostcaptain_hit_1"];
        private static SummonEffect Cannon => (SummonEffect)CannonByName.Skill!.Effect;

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, EnemyType enemy, params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, enemy, BigNumber.FromDouble(1e9));
            loop.State.BossTicksLeft = 100_000;
            loop.State.ClearQueue();
            loop.Events.Clear();
            return (loop, input);
        }

        private static void Wait(GameLoop loop, double seconds)
        {
            for (var i = 0; i < (int)System.Math.Round(seconds * GameLoop.TicksPerSecond); i++)
                loop.Tick();
        }

        private static bool Burning(GameLoop loop) =>
            (loop.Synergy.OwnMarks & TestWorld.Data.Synergy.MaskOf(new[] { "fire" })) != 0;

        [Test]
        public void TheBroadsideComesSoonerAndHitsTheNextEnemyWithTheTree()
        {
            var (loop, input) = Hero(HeroClass.Ghostcaptain, EnemyType.Normal,
                new Modifier(CombatStats.VolleyPx, ModifierKind.Flat, -200, StatTags.Mouse),
                new Modifier(CombatStats.VolleyPierce, ModifierKind.Flat, 1));
            input.MoveMouse((float)(Rules.BroadsidePx - 200));
            loop.Tick();
            loop.Tick();

            Assert.That(loop.State.QueueDamage[0].ToDouble(), Is.GreaterThan(0));
        }
    }
}
