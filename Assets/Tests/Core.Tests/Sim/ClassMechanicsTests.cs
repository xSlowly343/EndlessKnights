using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ClassMechanicsTests
    {
        private static ClassRules Rules => TestWorld.Data.Combat.Classes;

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass }, hero);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            loop.Events.Clear();
            return (loop, input);
        }

        private static void Keys(GameLoop loop, FakeInputSource input, int count)
        {
            for (var i = 0; i < count; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
            }
        }

        private static GameEvent LastHit(GameLoop loop)
        {
            for (var i = loop.Events.Count - 1; i >= 0; i--)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage)
                    return loop.Events[i];
            }
            throw new AssertionException("no damage events");
        }

        [Test]
        public void TypistComboGrowsWithEachKey()
        {
            var (loop, input) = Hero(HeroClass.Typist);

            Keys(loop, input, 7);

            Assert.That(loop.State.Combo, Is.EqualTo(7));
        }

        [Test]
        public void EveryNthKeyIsAComboStrike()
        {
            var (loop, input) = Hero(HeroClass.Typist);

            Keys(loop, input, Rules.ComboStrikeEvery - 1);
            var normal = LastHit(loop);
            Keys(loop, input, 1);
            var strike = LastHit(loop);

            Assert.That((normal.Flags & HitFlags.ComboStrike) == 0, Is.True);
            Assert.That((strike.Flags & HitFlags.ComboStrike) != 0, Is.True);
            var normalDamage = normal.Amount.ToDouble();
            Assert.That(strike.Amount.ToDouble() / normalDamage, Is.EqualTo(Rules.ComboStrikeMultiplier).Within(0.05));
        }

        [Test]
        public void ComboSurvivesShortPausesAndBreaksOnLongOnes()
        {
            var (loop, input) = Hero(HeroClass.Typist);
            var timeoutTicks = (int)System.Math.Round(Rules.ComboTimeoutSeconds * GameLoop.TicksPerSecond);
            Keys(loop, input, 10);

            for (var i = 0; i < timeoutTicks - 1; i++)
                loop.Tick();
            Assert.That(loop.State.Combo, Is.EqualTo(10));

            for (var i = 0; i < 2; i++)
                loop.Tick();
            Assert.That(loop.State.Combo, Is.EqualTo(0));
        }

        [Test]
        public void ClicksAndMouseDoNotKeepTheComboAlive()
        {
            var (loop, input) = Hero(HeroClass.Typist);
            var timeoutTicks = (int)System.Math.Round(Rules.ComboTimeoutSeconds * GameLoop.TicksPerSecond);
            Keys(loop, input, 5);

            for (var i = 0; i < timeoutTicks + 2; i++)
            {
                input.ClickLeft();
                input.MoveMouse(500);
                loop.Tick();
            }

            Assert.That(loop.State.Combo, Is.EqualTo(0));
        }

        [Test]
        public void BardHasNoCombo()
        {
            var (loop, input) = Hero(HeroClass.Bard);

            Keys(loop, input, Rules.ComboStrikeEvery);

            Assert.That(loop.State.Combo, Is.EqualTo(0));
            Assert.That((LastHit(loop).Flags & HitFlags.ComboStrike) == 0, Is.True);
        }

        [Test]
        public void BardClicksAreNotesUntilMelodyIsReady()
        {
            var (loop, input) = Hero(HeroClass.Bard);

            for (var i = 0; i < Rules.NotesPerMelody - 1; i++)
            {
                input.ClickLeft();
                loop.Tick();
                loop.Tick();
            }
            Assert.That(loop.State.Notes, Is.EqualTo(Rules.NotesPerMelody - 1));
            Assert.That(loop.Combat.Class.MelodyReady, Is.False);

            input.ClickLeft(3);
            loop.Tick();

            Assert.That(loop.State.Notes, Is.EqualTo(Rules.NotesPerMelody));
            Assert.That(loop.Combat.Class.MelodyReady, Is.True);
            Assert.That(loop.Events.CountOf(GameEventKind.MelodyReady), Is.EqualTo(1));
        }

        [Test]
        public void TakingTheMelodyEmptiesNotes()
        {
            var (loop, input) = Hero(HeroClass.Bard);
            input.ClickLeft(Rules.NotesPerMelody);
            loop.Tick();

            Assert.That(loop.Combat.Class.TryTakeMelody(), Is.True);
            Assert.That(loop.State.Notes, Is.EqualTo(0));
            Assert.That(loop.Combat.Class.TryTakeMelody(), Is.False);
        }

        [Test]
        public void TypistGetsNoNotesAndKeysGiveNoNotes()
        {
            var (typist, typistInput) = Hero(HeroClass.Typist);
            typistInput.ClickLeft(2);
            typist.Tick();

            var (bard, bardInput) = Hero(HeroClass.Bard);
            Keys(bard, bardInput, 5);
            bardInput.ClickRight(2);
            bard.Tick();

            Assert.That(typist.State.Notes, Is.EqualTo(0));
            Assert.That(bard.State.Notes, Is.EqualTo(0));
        }
    }
}
