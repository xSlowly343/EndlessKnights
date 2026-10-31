using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class WayfarerSandTests
    {
        private static BlindRules Rules => TestWorld.Data.Combat.Classes.Sand;

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

        private static int BlindAt(GameLoop loop, FakeInputSource input, int keys)
        {
            for (var i = 1; i <= keys; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
                if (loop.State.BlindTicks > 0)
                    return i;
            }
            return 0;
        }

        [Test]
        public void EveryTenthKeyThrowsSandAndBlindsTheEnemyForAllies()
        {
            Assert.That(Rules.Every, Is.EqualTo(10));
            var (loop, input) = Hero(HeroClass.Nomad);

            Assert.That(BlindAt(loop, input, Rules.Every), Is.EqualTo(Rules.Every));
            loop.Tick();
            Assert.That(loop.Synergy.OwnMarks & Mark("blind"), Is.Not.Zero);

            for (var i = 0; i < Rules.Seconds * GameLoop.TicksPerSecond + 1; i++)
                loop.Tick();

            Assert.That(loop.State.BlindTicks, Is.Zero);
            Assert.That(loop.Synergy.OwnMarks & Mark("blind"), Is.Zero);
        }

        [Test]
        public void TreeLinesThrowSandMoreOftenForLongerAndHitTheBlindedHarder()
        {
            var (loop, input) = Hero(HeroClass.Nomad,
                new Modifier(CombatStats.BlindEvery, ModifierKind.Flat, -2),
                new Modifier(CombatStats.BlindSeconds, ModifierKind.Flat, 2),
                new Modifier(CombatStats.DamageVsBlinded, ModifierKind.Increased, 0.1));
            var plain = loop.Combat.ConditionMultiplier(StatTags.Keyboard);

            Assert.That(BlindAt(loop, input, Rules.Every), Is.EqualTo(Rules.Every - 2));
            Assert.That(loop.State.BlindTicks, Is.EqualTo((int)((Rules.Seconds + 2) * GameLoop.TicksPerSecond) - 1).Within(1));
            Assert.That(loop.Combat.ConditionMultiplier(StatTags.Keyboard) / plain, Is.EqualTo(1.1).Within(1e-9));

            var (fast, fastInput) = Hero(HeroClass.Nomad, new Modifier(CombatStats.BlindEvery, ModifierKind.Flat, -100));
            Assert.That(BlindAt(fast, fastInput, Rules.Every), Is.EqualTo(Rules.EveryMin));
        }

        [Test]
        public void CompanionLinesAddDamagePerMarkRaiseTheLimitAndCountOwnBlind()
        {
            var (loop, input) = Hero(HeroClass.Nomad,
                new Modifier(CombatStats.CompanionPerMark, ModifierKind.Flat, 0.01),
                new Modifier(CombatStats.CompanionLimit, ModifierKind.Flat, 1),
                new Modifier(CombatStats.CompanionOwnMark, ModifierKind.Flat, 1));
            loop.Synergy.SetAlly(0, true, HeroClass.Voodoo, Mark("curse") | Mark("time") | Mark("aura"));
            loop.Synergy.SetAlly(1, true, HeroClass.PlagueDoctor, Mark("poison") | Mark("fire"));
            loop.Tick();
            Assert.That(loop.Synergy.PerMarkCount, Is.EqualTo(5));

            BlindAt(loop, input, Rules.Every);
            loop.Tick();

            Assert.That(loop.Synergy.PerMarkCount, Is.EqualTo(6));
            Assert.That(loop.State.CompanionMarks, Is.EqualTo(6));
        }

        [Test]
        public void WithoutTheTreeTheCompanionStopsAtFiveMarksAndIgnoresOwnBlind()
        {
            var (loop, input) = Hero(HeroClass.Nomad);
            loop.Synergy.SetAlly(0, true, HeroClass.Voodoo, Mark("curse") | Mark("time") | Mark("aura"));
            loop.Synergy.SetAlly(1, true, HeroClass.PlagueDoctor, Mark("poison") | Mark("fire") | Mark("summon"));
            BlindAt(loop, input, Rules.Every);
            loop.Tick();

            Assert.That(loop.Synergy.PerMarkCount, Is.EqualTo(5));
        }

        [Test]
        public void OtherClassesThrowNoSand()
        {
            var (loop, input) = Hero(HeroClass.Viking);

            Assert.That(BlindAt(loop, input, Rules.Every * 2), Is.Zero);
            Assert.That(loop.State.KeysTowardBlind, Is.Zero);
        }

        [Test]
        public void TheCountTowardTheSandIsSaved()
        {
            var (loop, input) = Hero(HeroClass.Nomad);
            BlindAt(loop, input, 7);

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State;

            Assert.That(loaded.KeysTowardBlind, Is.EqualTo(7));
        }
    }
}
