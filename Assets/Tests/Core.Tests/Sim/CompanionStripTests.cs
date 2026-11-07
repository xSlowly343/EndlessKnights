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
    }
}
