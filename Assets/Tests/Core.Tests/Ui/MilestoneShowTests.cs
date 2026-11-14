using System;
using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Sim.Rift;
using Game.Core.Tests.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class MilestoneShowTests
    {
        private static readonly long InsideRift = new DateTimeOffset(2026, 10, 27, 13, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        private static MilestoneCue Cue(MilestoneKind kind, long value = 0) => new MilestoneCue(kind, 0, value);

        private static GameLoop WithAllArtifacts()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            foreach (var artifact in TestWorld.Data.Artifacts.All)
                loop.State.Artifacts[artifact.Id] = 1;
            loop.State.Echo = BigNumber.FromDouble(1e300);
            loop.Artifacts.Rebuild();
            loop.Tick();
            loop.Events.Clear();
            return loop;
        }

        private static GameEvent Last(GameLoop loop, GameEventKind kind)
        {
            for (var i = loop.Events.Count - 1; i >= 0; i--)
            {
                if (loop.Events[i].Kind == kind)
                    return loop.Events[i];
            }
            Assert.Fail("no " + kind + " event");
            return default;
        }

        [Test]
        public void NoMilestoneCoversTheFightLongerThanASecond()
        {
            foreach (MilestoneKind kind in Enum.GetValues(typeof(MilestoneKind)))
                Assert.That(MilestoneShow.SecondsOf(kind), Is.InRange(0.5, 1.0), kind.ToString());
        }

        [Test]
        public void SizesFollowTheAgreedList()
        {
            Assert.That(MilestoneShow.SizeOf(MilestoneKind.Prestige), Is.EqualTo(MilestoneSize.Large));
            var small = new[] { MilestoneKind.Achievement, MilestoneKind.Collection, MilestoneKind.CampTier, MilestoneKind.Quest, MilestoneKind.Trial };
            foreach (MilestoneKind kind in Enum.GetValues(typeof(MilestoneKind)))
            {
                if (kind == MilestoneKind.Prestige)
                    continue;
                Assert.That(MilestoneShow.SizeOf(kind), Is.EqualTo(small.Contains(kind) ? MilestoneSize.Small : MilestoneSize.Medium), kind.ToString());
            }
        }

        [Test]
        public void MilestonesPlayOneAtATimeInOrderWithAGap()
        {
            var show = new MilestoneShow();
            show.Offer(Cue(MilestoneKind.Achievement));
            show.Offer(Cue(MilestoneKind.LevelUp, 7));
            show.Offer(Cue(MilestoneKind.Quest));

            Assert.That(show.TryTake(10, out var first), Is.True);
            Assert.That(first.Kind, Is.EqualTo(MilestoneKind.Achievement));
            Assert.That(show.TryTake(10, out _), Is.False);
            var free = 10 + first.Seconds + MilestoneShow.GapSeconds;
            Assert.That(show.TryTake(free - 0.01, out _), Is.False);
            Assert.That(show.Busy(free - 0.01), Is.True);

            Assert.That(show.TryTake(free, out var second), Is.True);
            Assert.That(second.Kind, Is.EqualTo(MilestoneKind.LevelUp));
            Assert.That(second.Value, Is.EqualTo(7));
            free += second.Seconds + MilestoneShow.GapSeconds + 1e-9;
            Assert.That(show.TryTake(free, out var third), Is.True);
            Assert.That(third.Kind, Is.EqualTo(MilestoneKind.Quest));
            Assert.That(show.Waiting, Is.EqualTo(0));
            Assert.That(show.TryTake(free + 100, out _), Is.False);
        }

        [Test]
        public void RepeatsOfTheSameMilestoneMergeAndKeepTheLatestValueAndTheirPlace()
        {
            var show = new MilestoneShow();
            show.Offer(Cue(MilestoneKind.LevelUp, 5));
            show.Offer(Cue(MilestoneKind.GateCleared));
            show.Offer(Cue(MilestoneKind.LevelUp, 6));
            show.Offer(new MilestoneCue(MilestoneKind.LevelUp, 0, 9));

            Assert.That(show.Waiting, Is.EqualTo(2));
            Assert.That(show.TryTake(0, out var cue), Is.True);
            Assert.That(cue.Kind, Is.EqualTo(MilestoneKind.LevelUp));
            Assert.That(cue.Value, Is.EqualTo(9));
        }

        [Test]
        public void PrestigeSkipsTheQueueAndHoldsTheRestUntilItEnds()
        {
            var show = new MilestoneShow();
            show.Offer(Cue(MilestoneKind.LevelUp));
            show.Offer(Cue(MilestoneKind.Achievement));
            Assert.That(show.TryTake(0, out _), Is.True);

            show.Offer(Cue(MilestoneKind.Prestige));
            Assert.That(show.TryTake(0.1, out var prestige), Is.True);
            Assert.That(prestige.Kind, Is.EqualTo(MilestoneKind.Prestige));
            Assert.That(prestige.Size, Is.EqualTo(MilestoneSize.Large));

            var free = 0.1 + prestige.Seconds + MilestoneShow.GapSeconds;
            Assert.That(show.TryTake(free - 0.01, out _), Is.False);
            Assert.That(show.TryTake(free, out var next), Is.True);
            Assert.That(next.Kind, Is.EqualTo(MilestoneKind.Achievement));
        }

        [Test]
        public void ClearDropsEverythingWaiting()
        {
            var show = new MilestoneShow();
            show.Offer(Cue(MilestoneKind.LevelUp));
            show.Offer(Cue(MilestoneKind.Trial));
            show.Clear();
            Assert.That(show.Waiting, Is.EqualTo(0));
            Assert.That(show.TryTake(0, out _), Is.False);
        }

        [Test]
        public void CoreEventsMapToMilestonesAndOtherEventsAreIgnored()
        {
            var show = new MilestoneShow();
            Assert.That(show.Offer(GameEvent.ForStage(GameEventKind.EnemyKilled, 0, 3)), Is.False);
            Assert.That(show.Offer(GameEvent.ForStage(GameEventKind.CompanionEvolved, 0, 3)), Is.False);
            Assert.That(show.Offer(GameEvent.ForStage(GameEventKind.BestiaryEntryOpened, 0, 3)), Is.False);
            Assert.That(show.Waiting, Is.EqualTo(0));

            var pairs = new[]
            {
                (GameEventKind.LevelUp, MilestoneKind.LevelUp),
                (GameEventKind.Prestige, MilestoneKind.Prestige),
                (GameEventKind.ArtifactUnlocked, MilestoneKind.ArtifactUnlocked),
                (GameEventKind.ArtifactAwakened, MilestoneKind.ArtifactAwakened),
                (GameEventKind.ArtifactEnchanted, MilestoneKind.ArtifactEnchanted),
                (GameEventKind.RiftFeatureOpened, MilestoneKind.RiftStep),
                (GameEventKind.FeatureOpened, MilestoneKind.FeatureOpened),
                (GameEventKind.RiftGiftOffered, MilestoneKind.GiftOffered),
                (GameEventKind.RiftGiftChosen, MilestoneKind.GiftChosen),
                (GameEventKind.GateCleared, MilestoneKind.GateCleared),
                (GameEventKind.AchievementUnlocked, MilestoneKind.Achievement),
                (GameEventKind.CollectionCompleted, MilestoneKind.Collection),
                (GameEventKind.CampTierReached, MilestoneKind.CampTier),
                (GameEventKind.QuestClaimed, MilestoneKind.Quest),
                (GameEventKind.TrialCompleted, MilestoneKind.Trial)
            };
            Assert.That(pairs.Length, Is.EqualTo(Enum.GetValues(typeof(MilestoneKind)).Length));
            foreach (var (from, to) in pairs)
            {
                Assert.That(MilestoneShow.TryRead(GameEvent.ForStage(from, 0, 2), out var cue), Is.True, from.ToString());
                Assert.That(cue.Kind, Is.EqualTo(to));
            }
        }

        [Test]
        public void ArtifactMilestonesCarryTheGroupAsAVariant()
        {
            for (var group = 1; group <= MilestoneShow.ArtifactGroups; group++)
            {
                MilestoneShow.TryRead(GameEvent.ForStage(GameEventKind.ArtifactUnlocked, 0, group), out var cue);
                Assert.That(cue.Variant, Is.EqualTo(group - 1));
            }
            MilestoneShow.TryRead(GameEvent.ForStage(GameEventKind.ArtifactEnchanted, 0, 99), out var beyond);
            Assert.That(beyond.Variant, Is.EqualTo(MilestoneShow.ArtifactGroups - 1));
            MilestoneShow.TryRead(GameEvent.WithAmount(GameEventKind.ArtifactAwakened, 0, BigNumber.FromDouble(3), 2), out var awakened);
            Assert.That(awakened.Variant, Is.EqualTo(1));
            Assert.That(awakened.Value, Is.EqualTo(3));
        }

        [Test]
        public void TheNewKindsStayAtTheEndOfTheEventList()
        {
            var kinds = (GameEventKind[])Enum.GetValues(typeof(GameEventKind));
            var tail = new[] { GameEventKind.RiftGiftOffered, GameEventKind.ArtifactUnlocked, GameEventKind.ArtifactAwakened, GameEventKind.ArtifactEnchanted, GameEventKind.RiftGiftChosen, GameEventKind.QuestClaimed };
            Assert.That(kinds.Skip(kinds.Length - tail.Length), Is.EqualTo(tail));
        }

        [Test]
        public void UnlockingAnArtifactAnnouncesItsGroupWithoutTouchingTheRoll()
        {
            var loop = TestWorld.Loop(state: new GameState(5));
            var silent = TestWorld.Loop(state: new GameState(5));
            loop.State.Echo = BigNumber.FromDouble(1e12);
            silent.State.Echo = BigNumber.FromDouble(1e12);
            var quiet = new ArtifactSystem(TestWorld.Data.Artifacts, TestWorld.Data.Rift, silent.State, new Game.Core.Stats.StatSheet());
            loop.Events.Clear();

            for (var i = 0; i < 3; i++)
            {
                var artifact = loop.Artifacts.TryUnlock()!;
                Assert.That(quiet.TryUnlock()!.Id, Is.EqualTo(artifact.Id));
                Assert.That(Last(loop, GameEventKind.ArtifactUnlocked).Stage, Is.EqualTo(artifact.Group));
            }
            Assert.That(loop.Events.CountOf(GameEventKind.ArtifactUnlocked), Is.EqualTo(3));

            loop.State.Echo = BigNumber.Zero;
            Assert.That(loop.Artifacts.TryUnlock(), Is.Null);
            Assert.That(loop.Events.CountOf(GameEventKind.ArtifactUnlocked), Is.EqualTo(3));
        }

        [Test]
        public void EnchantingAndEachAwakeningLevelAreAnnounced()
        {
            var loop = WithAllArtifacts();
            Assert.That(TestWorld.Data.Artifacts.TryGet("typewriter", out var typewriter), Is.True);
            Assert.That(loop.Artifacts.Enchant("typewriter"), Is.EqualTo(EnchantResult.Done));
            Assert.That(Last(loop, GameEventKind.ArtifactEnchanted).Stage, Is.EqualTo(typewriter.Group));
            Assert.That(loop.Artifacts.Enchant("typewriter"), Is.EqualTo(EnchantResult.AlreadyEnchanted));
            Assert.That(loop.Events.CountOf(GameEventKind.ArtifactEnchanted), Is.EqualTo(1));

            loop.Tick();
            Assert.That(loop.Events.CountOf(GameEventKind.ArtifactAwakened), Is.EqualTo(0));
            loop.State.TotalInput.Keys += (ulong)TestWorld.Data.Artifacts.Awakening.Needed(AwakeningInput.Keys, 2);
            loop.Tick();
            var awakened = Last(loop, GameEventKind.ArtifactAwakened);
            Assert.That(awakened.Stage, Is.EqualTo(typewriter.Group));
            Assert.That(awakened.Amount.ToDouble(), Is.EqualTo(2));
            Assert.That(loop.Events.CountOf(GameEventKind.ArtifactAwakened), Is.EqualTo(1));

            loop.State.TotalInput.Keys += 1;
            loop.Tick();
            Assert.That(loop.Events.CountOf(GameEventKind.ArtifactAwakened), Is.EqualTo(1), "no new level, no new event");
        }

        [Test]
        public void ChoosingARiftGiftIsAnnounced()
        {
            var loop = TestWorld.Loop();
            Assert.That(loop.Rift.Enter(InsideRift), Is.EqualTo(RiftEntry.Entered));
            loop.Tick();
            Assert.That(loop.Events.CountOf(GameEventKind.RiftGiftOffered), Is.EqualTo(1));

            Assert.That(loop.RiftGifts.Choose("nothing"), Is.EqualTo(RiftGiftResult.NotOffered));
            Assert.That(loop.Events.CountOf(GameEventKind.RiftGiftChosen), Is.EqualTo(0));
            Assert.That(loop.RiftGifts.Choose(loop.RiftGifts.Offer[0]), Is.EqualTo(RiftGiftResult.Taken));
            Assert.That(Last(loop, GameEventKind.RiftGiftChosen).Stage, Is.EqualTo(1));
        }

        [Test]
        public void AQuestIsCelebratedWhenItsRewardIsClaimed()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.SetLocalDay(20_000);
            loop.Tick();
            var daily = loop.Quests.State.Daily[0];
            Assert.That(loop.ClaimQuest(false, 0), Is.EqualTo(QuestResult.NotFinished));
            daily.Progress = daily.Target;
            Assert.That(loop.Events.CountOf(GameEventKind.QuestClaimed), Is.EqualTo(0), "being done is not enough");

            Assert.That(loop.ClaimQuest(false, 0), Is.EqualTo(QuestResult.Done));
            Assert.That(Last(loop, GameEventKind.QuestClaimed).Stage, Is.EqualTo(0));
            var weekly = loop.Quests.State.Weekly[0];
            weekly.Progress = weekly.Target;
            Assert.That(loop.ClaimQuest(true, 0), Is.EqualTo(QuestResult.Done));
            Assert.That(Last(loop, GameEventKind.QuestClaimed).Stage, Is.EqualTo(1));
            Assert.That(loop.ClaimQuest(true, 0), Is.EqualTo(QuestResult.AlreadyClaimed));
            Assert.That(loop.Events.CountOf(GameEventKind.QuestClaimed), Is.EqualTo(2));
        }

        [Test]
        public void OfferingAndTakingDoNotAllocate()
        {
            var show = new MilestoneShow();
            var events = new[]
            {
                GameEvent.ForStage(GameEventKind.LevelUp, 0, 4),
                GameEvent.ForStage(GameEventKind.AchievementUnlocked, 0, 1),
                GameEvent.WithAmount(GameEventKind.ArtifactAwakened, 0, BigNumber.FromDouble(2), 3),
                GameEvent.ForStage(GameEventKind.Prestige, 0, 900),
                GameEvent.ForStage(GameEventKind.EnemyKilled, 0, 1)
            };
            show.Offer(events[0]);
            show.TryTake(0, out _);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 400; i++)
            {
                show.Offer(events[i % events.Length]);
                show.TryTake(i * 0.4, out _);
            }
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
