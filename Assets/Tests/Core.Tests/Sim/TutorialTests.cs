using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class TutorialTests
    {
        private static (GameLoop Loop, FakeInputSource Input) NewPlayer(StatSheet? hero = null)
        {
            var input = new FakeInputSource();
            return (TestWorld.Loop(input, hero: hero), input);
        }

        private static void Type(GameLoop loop, FakeInputSource input, int ticks, int keysPerTick = 1)
        {
            for (var i = 0; i < ticks; i++)
            {
                input.PressKeys(keysPerTick);
                loop.Tick();
                loop.Events.Clear();
            }
        }

        [Test]
        public void RealDataFileLoadsAndFitsTheStrip()
        {
            var rules = TutorialRules.Load(TestData.ReadGameDataFile(TutorialRules.FileName));

            Assert.That(rules.Steps.Count, Is.GreaterThanOrEqualTo(5));
            Assert.That(rules.Steps[0].Show, Is.EqualTo(TutorialShow.Start));
            foreach (var step in rules.Steps)
                Assert.That(step.Text.Length, Is.LessThanOrEqualTo(TutorialRules.MaxTextLength));
        }

        [Test]
        public void HintsWaitForConsent()
        {
            var (loop, input) = NewPlayer();
            loop.Permissions = Game.Core.Settings.InputPermissions.None;
            for (var i = 0; i < 2000; i++)
                loop.Tick();

            Assert.That(loop.Tutorial.Current, Is.Null);
            Assert.That(loop.State.TutorialDone, Is.Empty);

            loop.Permissions = Game.Core.Settings.InputPermissions.All;
            loop.Tick();

            Assert.That(loop.Tutorial.Current!.Id, Is.EqualTo("type"));
        }

        [Test]
        public void FirstHintAsksToTypeAndGoesAwayAfterTyping()
        {
            var (loop, input) = NewPlayer();
            loop.Tick();

            Assert.That(loop.Tutorial.Current!.Id, Is.EqualTo("type"));

            Type(loop, input, 25);

            Assert.That(loop.State.TutorialDone, Does.Contain("type"));
        }

        [Test]
        public void HintsFollowTheGameAndEachShowsOnce()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1e6));
            var (loop, input) = NewPlayer(hero);

            Type(loop, input, 40);
            Assert.That(loop.Tutorial.Current!.Id, Is.EqualTo("mouse"));

            input.MoveMouse(5000);
            loop.Tick();
            Assert.That(loop.State.TutorialDone, Does.Contain("mouse"));
            loop.Tick();
            Assert.That(loop.Tutorial.Current!.Id, Is.EqualTo("skills"));
        }

        [Test]
        public void HintExpiresAfterItsTime()
        {
            var (loop, _) = NewPlayer();
            var step = TestWorld.Data.Tutorial.Steps[0];

            for (var i = 0; i <= step.MaxSeconds * GameLoop.TicksPerSecond; i++)
                loop.Tick();

            Assert.That(loop.State.TutorialDone, Does.Contain(step.Id));
        }

        [Test]
        public void DismissClosesTheCurrentHint()
        {
            var (loop, _) = NewPlayer();
            loop.Tick();

            loop.Tutorial.Dismiss();
            loop.Tick();

            Assert.That(loop.State.TutorialDone, Does.Contain("type"));
            Assert.That(loop.Tutorial.Current, Is.Null);
        }

        [Test]
        public void UpgradeHintAppearsWhenGoldIsEnough()
        {
            var (loop, input) = NewPlayer();
            foreach (var step in TestWorld.Data.Tutorial.Steps)
            {
                if (step.Id != "upgrade")
                    loop.State.TutorialDone.Add(step.Id);
            }
            loop.Tick();
            Assert.That(loop.Tutorial.Current, Is.Null);

            loop.State.Gold = BigNumber.FromDouble(1e12);
            loop.Tick();
            Assert.That(loop.Tutorial.Current!.Id, Is.EqualTo("upgrade"));

            loop.Upgrades.TryBuy(1);
            loop.Tick();
            Assert.That(loop.State.TutorialDone, Does.Contain("upgrade"));
        }

        [Test]
        public void OldSavesSkipTheTutorialAndNewOnesKeepProgress()
        {
            var clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));
            var old = SaveCodec.Decode(TestData.ReadSaveFixture("save_v9.json"), TestWorld.Data).State;
            Assert.That(old.TutorialSkipped, Is.True);

            var state = new GameState();
            state.TutorialDone.Add("type");
            state.TutorialDone.Add("boss");
            var loaded = SaveCodec.Decode(SaveCodec.Encode(state, clock), TestWorld.Data).State;

            Assert.That(loaded.TutorialSkipped, Is.False);
            Assert.That(loaded.TutorialDone, Is.EquivalentTo(new[] { "type", "boss" }));
        }

        [Test]
        public void SkippedTutorialShowsNothing()
        {
            var (loop, _) = NewPlayer();
            loop.State.TutorialSkipped = true;

            loop.Tick();

            Assert.That(loop.Tutorial.Current, Is.Null);
        }

        [Test]
        public void UnknownTriggerIsRejected()
        {
            const string json = @"{ ""version"": 1, ""steps"": [ { ""id"": ""a"", ""text"": ""x"", ""show"": { ""when"": ""never"" }, ""done"": { ""when"": ""keys"" }, ""maxSeconds"": 5 } ] }";

            var error = Assert.Throws<ContentException>(() => TutorialRules.Load(json));

            Assert.That(error!.Message, Does.Contain("never"));
        }
    }
}
