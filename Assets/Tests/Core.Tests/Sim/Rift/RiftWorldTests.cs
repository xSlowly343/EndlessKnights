using System;
using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Rift;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim.Rift
{
    public class RiftWorldTests
    {
        private static readonly long Inside = new DateTimeOffset(2026, 10, 27, 13, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        private static RiftRules Rules => TestWorld.Data.Rift;

        private static GameLoop InRift(long homeStage = 300)
        {
            var loop = TestWorld.Loop();
            loop.State.Stage = homeStage;
            loop.State.HighestStage = homeStage;
            loop.State.RecordStage = homeStage;
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Zero);
            loop.Stages.EnsureEnemy();
            loop.State.Echo = BigNumber.FromDouble(1e9);
            loop.Artifacts.TryUnlock();
            Assert.That(loop.Rift.Enter(Inside), Is.EqualTo(Game.Core.Sim.Rift.RiftEntry.Entered));
            loop.Events.Clear();
            return loop;
        }

        [Test]
        public void TheLadderIsReadFromRiftJson()
        {
            Assert.That(Rules.StageOf(RiftFeature.Artifacts), Is.EqualTo(10));
            Assert.That(Rules.StageOf(RiftFeature.Dungeons), Is.EqualTo(25));
        }

        [Test]
        public void ArtifactsOpenInsideOnlyByRiftStagesWhateverTheHomeRecord()
        {
            var loop = InRift();
            loop.State.Rift.Shards = BigNumber.FromDouble(1e9);
            Assert.That(loop.Rift.IsOpen(RiftFeature.Artifacts), Is.False);
            Assert.That(loop.Artifacts.Open, Is.False);
            Assert.That(loop.Artifacts.TryUnlock(), Is.Null);

            loop.State.HighestStage = Rules.StageOf(RiftFeature.Artifacts);
            Assert.That(loop.Rift.IsOpen(RiftFeature.Artifacts), Is.True);
            Assert.That(loop.Artifacts.Open, Is.True);
            Assert.That(loop.Artifacts.TryUnlock(), Is.Not.Null);
        }

        [Test]
        public void DungeonKeysDropInsideOnlyFromTheLadderStage()
        {
            var loop = InRift();
            var stage = Rules.StageOf(RiftFeature.Dungeons);
            for (var i = 0; i < 40; i++)
            {
                loop.Drops.OnBossDefeated(stage, EnemyType.Guardian, 300, raisesRecord: false);
                loop.Drops.Flush();
            }
            Assert.That(loop.Dungeons.Keys.Count, Is.Zero, "the Rift has not reached the ladder yet");

            loop.State.HighestStage = stage;
            for (var i = 0; i < 40; i++)
            {
                loop.Drops.OnBossDefeated(stage, EnemyType.Guardian, 300, raisesRecord: false);
                loop.Drops.Flush();
            }
            Assert.That(loop.Dungeons.Keys.Count, Is.GreaterThan(0));
            Assert.That(loop.Dungeons.Keys.All(k => k.ForRift), Is.True);
        }

        [Test]
        public void EachStepOfTheLadderIsAnnouncedOncePerWindowAndSaved()
        {
            var loop = InRift();
            loop.State.HighestStage = Rules.StageOf(RiftFeature.Dungeons);
            loop.Tick();
            Assert.That(loop.Events.CountOf(GameEventKind.RiftFeatureOpened), Is.EqualTo(2));
            loop.Events.Clear();
            loop.Tick();
            Assert.That(loop.Events.CountOf(GameEventKind.RiftFeatureOpened), Is.Zero);
            Assert.That(loop.State.Rift.Opened, Is.EquivalentTo(new[] { "artifacts", "dungeons" }));

            var clock = ClockStamp.First(new ClockReading(Inside, 1000, 1));
            var restored = SaveCodec.Decode(SaveCodec.Encode(loop.State, clock), TestWorld.Data).State;
            Assert.That(restored.Rift.Opened, Is.EquivalentTo(new[] { "artifacts", "dungeons" }));
        }

        [Test]
        public void TheRiftIsItsOwnWorldWithoutPeacefulPlaces()
        {
            var loop = TestWorld.Loop();
            loop.State.Stage = 300;
            loop.State.HighestStage = 300;
            loop.State.RecordStage = 300;
            loop.State.Peaceful.Mode = PeacefulMode.Camp;
            Assert.That(loop.Rift.Enter(Inside), Is.EqualTo(Game.Core.Sim.Rift.RiftEntry.Entered));

            Assert.That(loop.State.Peaceful.Mode, Is.EqualTo(PeacefulMode.Battle));
            Assert.That(loop.Peaceful.Enter(PeacefulPlace.Town), Is.Not.EqualTo(PeacefulResult.Done));
            Assert.That(loop.Peaceful.Enter(PeacefulPlace.Hideout), Is.Not.EqualTo(PeacefulResult.Done));
        }

        [Test]
        public void AnUnknownOrRepeatedLadderStepIsRefused()
        {
            Assert.Throws<ContentException>(() => RiftRulesWith("[{ \"feature\": \"wings\", \"stage\": 5 }]"));
            Assert.Throws<ContentException>(() => RiftRulesWith("[{ \"feature\": \"artifacts\", \"stage\": 5 }, { \"feature\": \"artifacts\", \"stage\": 9 }]"));
        }

        private static RiftRules RiftRulesWith(string ladder)
        {
            var text = Game.Core.Tests.Content.TestData.ReadGameDataFile(RiftRules.FileName);
            var start = text.IndexOf("\"ladder\"", StringComparison.Ordinal);
            var end = text.IndexOf(']', start) + 1;
            return RiftRules.Load(text.Substring(0, start) + "\"ladder\": " + ladder + text.Substring(end));
        }
    }
}
