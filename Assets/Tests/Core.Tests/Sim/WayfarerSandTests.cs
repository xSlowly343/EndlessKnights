using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class WayfarerSandTests
    {
        private static BlindRules Rules => TestWorld.Data.Combat.Classes.Sandwalker.Blind;

        private static int Mark(string name) => TestWorld.Data.Synergy.MaskOf(new[] { name });

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
            loop.Events.Clear();
            return (loop, input);
        }

        private static void Hits(GameLoop loop, FakeInputSource input, int keys)
        {
            for (var i = 0; i < keys; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
            }
        }

        private static void Dune(GameLoop loop) => loop.Combat.Sandwalker.OnSkillCast(TestWorld.Data.Gems["sandwalker_queue_2"]);

        [Test]
        public void HitsNoLongerThrowSandButTheDuneBlindsTheEnemyForAllies()
        {
            var (loop, input) = Hero(HeroClass.Sandwalker);
            Hits(loop, input, 30);
            Assert.That(loop.State.BlindTicks, Is.Zero);

            Dune(loop);
            loop.Tick();
            Assert.That(loop.State.BlindTicks, Is.GreaterThan(0));
            Assert.That(loop.Synergy.OwnMarks & Mark("blind"), Is.Not.Zero);

            for (var i = 0; i < 6 * GameLoop.TicksPerSecond; i++)
                loop.Tick();

            Assert.That(loop.State.BlindTicks, Is.Zero);
            Assert.That(loop.Synergy.OwnMarks & Mark("blind"), Is.Zero);
        }

        [Test]
        public void TheBlindedEnemyTakesMoreFromTheSandwalkerAndTreeLinesLengthenIt()
        {
            var (loop, _) = Hero(HeroClass.Sandwalker,
                new Modifier(CombatStats.BlindSeconds, ModifierKind.Flat, 2),
                new Modifier(CombatStats.DamageVsBlinded, ModifierKind.Increased, 0.1));
            var plain = loop.Combat.ConditionMultiplier(StatTags.Keyboard);

            loop.Combat.Sandwalker.Blind(Rules.Seconds);

            Assert.That(loop.State.BlindTicks, Is.EqualTo((int)((Rules.Seconds + 2) * GameLoop.TicksPerSecond)).Within(1));
            Assert.That(loop.Combat.Sandwalker.DamageTaken(), Is.EqualTo(1 + Rules.DamageTaken).Within(1e-9));
            Assert.That(loop.Combat.ConditionMultiplier(StatTags.Keyboard) / plain, Is.EqualTo(1.1).Within(1e-9));
        }

        [Test]
        public void CompanionLinesAddDamagePerMarkRaiseTheLimitAndCountOwnBlind()
        {
            var (loop, _) = Hero(HeroClass.Sandwalker,
                new Modifier(CombatStats.CompanionPerMark, ModifierKind.Flat, 0.01),
                new Modifier(CombatStats.CompanionLimit, ModifierKind.Flat, 1),
                new Modifier(CombatStats.CompanionOwnMark, ModifierKind.Flat, 1));
            loop.Synergy.SetAlly(0, true, HeroClass.Whisperer, Mark("curse") | Mark("time") | Mark("aura"));
            loop.Synergy.SetAlly(1, true, HeroClass.Alchemist, Mark("poison") | Mark("fire"));
            loop.Tick();
            Assert.That(loop.Synergy.PerMarkCount, Is.EqualTo(5));

            Dune(loop);
            loop.Tick();

            Assert.That(loop.Synergy.PerMarkCount, Is.EqualTo(6));
            Assert.That(loop.State.CompanionMarks, Is.EqualTo(6));
        }

        [Test]
        public void WithoutTheTreeTheCompanionStopsAtFiveMarksAndIgnoresOwnBlind()
        {
            var (loop, _) = Hero(HeroClass.Sandwalker);
            loop.Synergy.SetAlly(0, true, HeroClass.Whisperer, Mark("curse") | Mark("time") | Mark("aura"));
            loop.Synergy.SetAlly(1, true, HeroClass.Alchemist, Mark("poison") | Mark("fire") | Mark("summon"));
            Dune(loop);
            loop.Tick();

            Assert.That(loop.Synergy.PerMarkCount, Is.EqualTo(5));
        }

        [Test]
        public void OtherClassesThrowNoSand()
        {
            var (loop, input) = Hero(HeroClass.Runedancer);
            Hits(loop, input, 20);

            Dune(loop);
            loop.Combat.Sandwalker.Blind(Rules.Seconds);

            Assert.That(loop.State.BlindTicks, Is.Zero);
            Assert.That(loop.Combat.Sandwalker.DamageTaken(), Is.EqualTo(1));
        }
    }
}
