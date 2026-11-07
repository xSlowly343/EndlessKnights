using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionWorkTests
    {
        private const double Hour = ExpeditionSystem.SecondsPerHour;

        private static GameLoop Camp(long day = 100)
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            loop.SetLocalDay(day);
            return loop;
        }

        private static GameLoop CampWith(System.Func<BoardTask, bool> wanted)
        {
            for (long day = 100; day < 400; day++)
            {
                var loop = Camp(day);
                if (loop.CompanionWork.Board.Any(id => wanted(loop.Companions.Rules.Work.Task(id)!)))
                    return loop;
            }
            throw new System.InvalidOperationException("no such task in 300 days");
        }

        [Test]
        public void ThreeTasksADayThatChangeWithTheDay()
        {
            var loop = Camp();
            Assert.That(loop.CompanionWork.Board.Count, Is.EqualTo(3));
            Assert.That(loop.CompanionWork.Board.Distinct().Count(), Is.EqualTo(3));
            var today = loop.CompanionWork.Board.ToList();
            loop.SetLocalDay(101);
            var tomorrow = loop.CompanionWork.Board.ToList();
            loop.SetLocalDay(102);
            Assert.That(today.SequenceEqual(tomorrow) && tomorrow.SequenceEqual(loop.CompanionWork.Board), Is.False);
        }

        [Test]
        public void OneFreeRerollADay()
        {
            var loop = Camp();
            var first = loop.CompanionWork.Board[0];
            Assert.That(loop.CompanionWork.Reroll(0), Is.EqualTo(WorkResult.Done));
            Assert.That(loop.CompanionWork.Board[0], Is.Not.EqualTo(first));
            Assert.That(loop.CompanionWork.Reroll(1), Is.EqualTo(WorkResult.NoRerolls));
        }

        [Test]
        public void BringingItemsPaysTheCompanion()
        {
            var loop = CampWith(task => task.Kind == BoardTaskKind.Bring);
            var task = loop.CompanionWork.Board.Select(id => loop.Companions.Rules.Work.Task(id)!).First(t => t.Kind == BoardTaskKind.Bring);
            Assert.That(loop.CompanionWork.HandIn(task.Id, "camp_dog"), Is.EqualTo(WorkResult.NoItems));
            loop.Camp.Add(task.Item, task.Amount);
            var souls = loop.State.Companions.Souls;
            Assert.That(loop.CompanionWork.HandIn(task.Id, "camp_dog"), Is.EqualTo(WorkResult.Done));
            Assert.That(loop.Companions.Progress("camp_dog").Experience + loop.Companions.LevelOf("camp_dog") * 1000, Is.GreaterThan(1000));
            Assert.That(loop.State.Companions.Souls, Is.EqualTo(souls + task.Souls));
            Assert.That(loop.CompanionWork.HandIn(task.Id, "camp_dog"), Is.EqualTo(WorkResult.Finished));
        }

        [Test]
        public void ATripTaskIsDoneByTheTripItself()
        {
            var loop = CampWith(task => task.Kind == BoardTaskKind.Trip && task.Trait.Length == 0);
            var task = loop.CompanionWork.Board.Select(id => loop.Companions.Rules.Work.Task(id)!).First(t => t.Kind == BoardTaskKind.Trip && t.Trait.Length == 0);
            loop.State.RecordStage = 100_000;
            Assert.That(loop.Expeditions.Send("camp_dog", task.Biome, 4), Is.EqualTo(ExpeditionResult.Done));
            loop.Expeditions.Advance(4 * Hour);
            Assert.That(loop.State.Companions.BoardDone, Does.Contain(task.Id));
        }

        [Test]
        public void HelpingAResidentTakesFourHoursAndPaysBondAndReputation()
        {
            var loop = Camp();
            Assert.That(loop.CompanionWork.Help("camp_dog", "gardeners"), Is.EqualTo(WorkResult.Done));
            Assert.That(loop.Expeditions.Send("camp_dog", "green_hills", 4), Is.EqualTo(ExpeditionResult.CompanionBusy));
            Assert.That(loop.CompanionWork.Help("camp_crow", "gardeners"), Is.EqualTo(WorkResult.Busy), "one helper at a time");
            loop.Companions.Advance(4 * Hour);
            Assert.That(loop.Companions.Progress("camp_dog").Bond, Is.EqualTo(5).Within(1e-9));
            Assert.That(loop.State.Town.Reputation["gardeners"], Is.EqualTo(20));
            Assert.That(loop.CompanionWork.Helping("camp_dog"), Is.False);
        }

        [Test]
        public void TheBurrowGoesOnceAWeekAsDeepAsThePartyIsStrong()
        {
            var loop = Camp();
            var dog = loop.Companions.Progress("camp_dog");
            dog.Level = 30;
            dog.Stage = 2;
            var power = loop.CompanionWork.Power("camp_dog") + loop.CompanionWork.Power("camp_crow");
            Assert.That(power, Is.InRange(20, 59.9));
            Assert.That(loop.CompanionWork.Burrow(new[] { "camp_dog", "camp_crow" }, out var run), Is.EqualTo(WorkResult.Done));
            Assert.That(run.Power, Is.EqualTo(power).Within(1e-9));
            Assert.That(run.Floors, Is.EqualTo(1));
            Assert.That(loop.State.Companions.Souls, Is.EqualTo(2));
            Assert.That(loop.CompanionWork.Burrow(new[] { "camp_dog" }, out _), Is.EqualTo(WorkResult.TooSoon));
            loop.SetLocalDay(107);
            Assert.That(loop.CompanionWork.BurrowReady, Is.True);
            Assert.That(loop.CompanionWork.Burrow(new[] { "camp_dog", "camp_dog" }, out _), Is.EqualTo(WorkResult.BadParty));
        }
    }
}
