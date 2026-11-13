using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Settings;
using Game.Core.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionStripTests
    {
        private static (GameLoop Loop, FakeInputSource Input) World()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState(1));
            loop.Tick();
            loop.SetLocalDay(100);
            return (loop, input);
        }

        private static void Ticks(GameLoop loop, FakeInputSource input, double seconds, bool typing)
        {
            for (var i = 0; i < seconds / GameLoop.TickSeconds; i++)
            {
                if (typing)
                    input.PressKeys(1);
                loop.Tick();
            }
        }

        [Test]
        public void ItSitsAndSleepsWithoutInputAndHopsAndRunsInASeries()
        {
            var (loop, input) = World();
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            Ticks(loop, input, 61, false);
            Assert.That(loop.Strip.Pose, Is.EqualTo(StripPose.Sit));
            Ticks(loop, input, 240, false);
            Assert.That(loop.Strip.Pose, Is.EqualTo(StripPose.Sleep));
            Ticks(loop, input, 2, true);
            Assert.That(loop.Strip.Pose, Is.EqualTo(StripPose.Stand));
            Ticks(loop, input, 4, true);
            Assert.That(loop.Strip.Pose, Is.EqualTo(StripPose.Hop));
            Ticks(loop, input, 25, true);
            Assert.That(loop.Strip.Pose, Is.EqualTo(StripPose.Run));
            Ticks(loop, input, 3, false);
            Assert.That(loop.Strip.SeriesSeconds, Is.Zero, "a pause breaks the series");
        }

        [Test]
        public void OnATripItLeavesASignAndBringsTheBestFind()
        {
            var (loop, input) = World();
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Expeditions.Send("camp_dog", "green_hills", 4);
            loop.Tick();
            Assert.That(loop.Strip.Pose, Is.EqualTo(StripPose.Sign));
            Assert.That(loop.Strip.ReturnSeconds, Is.GreaterThan(3 * 3600));
            loop.Expeditions.Advance(4 * ExpeditionSystem.SecondsPerHour);
            loop.Tick();
            Assert.That(loop.Strip.Pose, Is.EqualTo(StripPose.Bring));
            Assert.That(loop.Strip.BringItem, Is.Not.Empty);
            Assert.That(loop.Strip.Click(), Is.True);
            Assert.That(loop.Strip.Click(), Is.False);
            loop.Tick();
            Assert.That(loop.Strip.Pose, Is.Not.EqualTo(StripPose.Bring));
        }

        [Test]
        public void TheSettingHidesItEverywhereOrAwayFromHome()
        {
            var (loop, _) = World();
            loop.Strip.Mode = StripMode.HomeOnly;
            loop.Tick();
            Assert.That(loop.Strip.Pose, Is.EqualTo(StripPose.Hidden));
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Tick();
            Assert.That(loop.Strip.Pose, Is.Not.EqualTo(StripPose.Hidden));
            loop.Strip.Mode = StripMode.Off;
            loop.Tick();
            Assert.That(loop.Strip.Pose, Is.EqualTo(StripPose.Hidden));
            var settings = new GameSettings();
            Assert.That(SettingsScreen.StripModeOf(settings), Is.EqualTo(StripMode.Always));
            SettingsScreen.Change(Game.Core.Ui.SettingId.CompanionStrip, settings, 1);
            Assert.That(SettingsScreen.StripModeOf(settings), Is.EqualTo(StripMode.HomeOnly));
            var copy = SettingsCodec.Decode(SettingsCodec.Encode(settings), new System.Collections.Generic.List<string>());
            Assert.That(copy.CompanionStrip, Is.EqualTo("homeOnly"));
        }

        [Test]
        public void ThePlayerPicksWhoIsShownAndItIsSaved()
        {
            var (loop, _) = World();
            Assert.That(loop.Strip.Companion, Is.EqualTo("camp_dog"));
            Assert.That(loop.Strip.Show("egg_griffin"), Is.EqualTo(CompanionResult.UnknownCompanion));
            Assert.That(loop.Strip.Show("camp_crow"), Is.EqualTo(CompanionResult.Done));
            var copy = Game.Core.Save.GameStateJson.Read(new Game.Core.Content.ContentNode(Game.Core.Save.GameStateJson.Write(loop.State), "save", string.Empty),
                TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Companions.Shown, Is.EqualTo("camp_crow"));
        }
        [Test]
        public void EveryCompanionHasItsOwnGestures()
        {
            var rules = TestWorld.Data.Companions;
            Assert.That(rules.Strip.Gestures, Is.True);
            var sets = new System.Collections.Generic.HashSet<string>();
            foreach (var companion in rules.Companions)
            {
                Assert.That(companion.Gestures.Count, Is.GreaterThanOrEqualTo(2), companion.Id);
                sets.Add(string.Join(",", System.Linq.Enumerable.Select(companion.Gestures, g => g.Id)));
            }
            Assert.That(sets.Count, Is.EqualTo(rules.Companions.Count), "no two companions share the same gestures");
        }

        [Test]
        public void AtRestItPlaysOneOfItsGesturesEveryTwentyToFortySeconds()
        {
            var (loop, input) = World();
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            var rules = TestWorld.Data.Companions.Strip;
            var own = TestWorld.Data.Companions.Companion(loop.Strip.Companion)!.Gestures;
            var started = new System.Collections.Generic.List<(double At, string Id)>();
            var playing = string.Empty;
            for (var tick = 1; tick <= 59 * 10; tick++)
            {
                if (tick % 30 == 0)
                    input.PressKeys(1);
                loop.Tick();
                Assert.That(loop.Strip.Pose, Is.EqualTo(StripPose.Stand).Or.EqualTo(StripPose.Sit));
                if (loop.Strip.Gesture.Length > 0 && playing.Length == 0)
                    started.Add((tick * GameLoop.TickSeconds, loop.Strip.Gesture));
                playing = loop.Strip.Gesture;
            }
            Assert.That(started.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(started[0].At, Is.InRange(rules.GestureMinSeconds, rules.GestureMaxSeconds + 0.2));
            Assert.That(started.TrueForAll(g => System.Linq.Enumerable.Any(own, o => o.Id == g.Id)), Is.True, "only its own gestures");
            for (var i = 1; i < started.Count; i++)
            {
                Assert.That(started[i].Id, Is.Not.EqualTo(started[i - 1].Id), "not the same gesture twice in a row");
                Assert.That(started[i].At - started[i - 1].At, Is.GreaterThanOrEqualTo(rules.GestureMinSeconds));
            }
        }

        [Test]
        public void AGestureStopsWhenThePetHasSomethingToDo()
        {
            var (loop, input) = World();
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            for (var i = 0; i < 45 * 10 && loop.Strip.Gesture.Length == 0; i++)
            {
                if (i % 30 == 0)
                    input.PressKeys(1);
                loop.Tick();
            }
            Assert.That(loop.Strip.Gesture, Is.Not.Empty);
            Assert.That(loop.Strip.GestureSecondsLeft, Is.GreaterThan(0));
            Ticks(loop, input, 6, true);
            Assert.That(loop.Strip.Pose, Is.EqualTo(StripPose.Hop));
            Assert.That(loop.Strip.Gesture, Is.Empty, "a series interrupts the gesture");
        }
    }
}
