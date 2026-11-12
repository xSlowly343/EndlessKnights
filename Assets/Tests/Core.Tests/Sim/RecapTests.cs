using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class RecapTests
    {
        private const long Monday = 20773;

        private static GameLoop Loop(GameState? state = null) => TestWorld.Loop(new FakeInputSource(), state ?? new GameState(1));

        private static void Play(GameLoop loop, ulong keys, ulong clicks, double meters, double seconds, long kills, long bosses, long stages)
        {
            var state = loop.State;
            state.TotalInput.Keys += keys;
            state.TotalInput.LeftClicks += clicks;
            state.TotalInput.MouseMeters += meters;
            state.TotalInput.PlaySeconds += seconds;
            state.Kills += kills;
            state.BossKills += bosses;
            state.RecordStage += stages;
        }

        [Test]
        public void TheWeekStartsOnMonday()
        {
            Assert.That(new System.DateTime(1970, 1, 1).AddDays(Monday).DayOfWeek, Is.EqualTo(System.DayOfWeek.Monday));
            Assert.That(ChronicleCatalog.WeekOf(Monday), Is.EqualTo(ChronicleCatalog.WeekOf(Monday + 6)));
            Assert.That(ChronicleCatalog.WeekOf(Monday - 1), Is.EqualTo(ChronicleCatalog.WeekOf(Monday) - 1));
        }

        [Test]
        public void ADayIsClosedWhenTheNextOneStartsAndIsShownOnce()
        {
            var loop = Loop();
            loop.SetLocalDay(Monday + 1);
            Assert.That(loop.Recap.Due, Is.EqualTo(RecapKind.None), "nothing to show on the very first day");

            Play(loop, 1200, 300, 450, 3600, 500, 20, 40);
            loop.SetLocalDay(Monday + 1);
            Assert.That(loop.Recap.Due, Is.EqualTo(RecapKind.None), "the same day goes on");
            loop.SetLocalDay(Monday + 2);

            Assert.That(loop.Recap.Due, Is.EqualTo(RecapKind.Day));
            var day = loop.Recap.LastDay!;
            Assert.That(day.Day, Is.EqualTo(Monday + 1));
            Assert.That(day.Keys, Is.EqualTo(1200));
            Assert.That(day.Clicks, Is.EqualTo(300));
            Assert.That(day.MouseMeters, Is.EqualTo(450));
            Assert.That(day.PlaySeconds, Is.EqualTo(3600));
            Assert.That(day.Kills, Is.EqualTo(500));
            Assert.That(day.Bosses, Is.EqualTo(20));
            Assert.That(day.Stages, Is.EqualTo(40));
            Assert.That(loop.Recap.Today.Keys, Is.Zero, "the new day counts from zero");

            loop.Recap.MarkShown(RecapKind.Day);
            Assert.That(loop.Recap.Due, Is.EqualTo(RecapKind.None));
        }

        [Test]
        public void TheFirstStartOfANewWeekShowsTheWeek()
        {
            var loop = Loop();
            loop.SetLocalDay(Monday);
            Play(loop, 100, 10, 5, 600, 30, 1, 3);
            loop.SetLocalDay(Monday + 3);
            loop.Recap.MarkShown(loop.Recap.Due);
            Play(loop, 400, 40, 20, 1200, 70, 2, 5);
            loop.SetLocalDay(Monday + 8);

            Assert.That(loop.Recap.Due, Is.EqualTo(RecapKind.Week), "Tuesday still counts when Monday was skipped");
            var week = loop.Recap.Week(ChronicleCatalog.WeekOf(Monday), out var played);
            Assert.That(played, Is.EqualTo(2));
            Assert.That(week.Keys, Is.EqualTo(500));
            Assert.That(week.Kills, Is.EqualTo(100));
            Assert.That(week.Stages, Is.EqualTo(8));

            loop.Recap.MarkShown(RecapKind.Week);
            Assert.That(loop.Recap.Due, Is.EqualTo(RecapKind.None), "the week covers its last day too");
        }

        [Test]
        public void AnEmptyDayLeavesNoRecordAndOnlyTwoWeeksAreKept()
        {
            var loop = Loop();
            loop.SetLocalDay(Monday);
            loop.SetLocalDay(Monday + 1);
            Assert.That(loop.Recap.State.Days, Is.Empty);

            for (var i = 0; i < RecapState.DaysKept + 5; i++)
            {
                Play(loop, 10, 1, 1, 60, 1, 0, 0);
                loop.SetLocalDay(Monday + 2 + i);
            }

            Assert.That(loop.Recap.State.Days.Count, Is.EqualTo(RecapState.DaysKept));
            Assert.That(loop.Recap.State.Days.Last().Day, Is.EqualTo(Monday + RecapState.DaysKept + 5));
        }

        [Test]
        public void TheClockGoingBackChangesNothing()
        {
            var loop = Loop();
            loop.SetLocalDay(Monday + 2);
            Play(loop, 10, 1, 1, 60, 1, 0, 0);

            loop.SetLocalDay(Monday);

            Assert.That(loop.Recap.State.Day, Is.EqualTo(Monday + 2));
            Assert.That(loop.Recap.State.Days, Is.Empty);
            Assert.That(loop.Recap.Today.Keys, Is.EqualTo(10));
        }

        [Test]
        public void TheRecapSurvivesTheSaveWithKillsOfTheRunningDay()
        {
            var loop = Loop();
            loop.SetLocalDay(Monday);
            Play(loop, 100, 10, 5, 600, 30, 1, 3);
            loop.SetLocalDay(Monday + 1);
            loop.Recap.MarkShown(RecapKind.Day);
            Play(loop, 50, 5, 2, 300, 12, 4, 1);

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, default), TestWorld.Data).State;
            var again = Loop(loaded);
            again.SetLocalDay(Monday + 1);

            Assert.That(again.Recap.State.ShownDay, Is.EqualTo(Monday));
            Assert.That(again.Recap.LastDay!.Kills, Is.EqualTo(30));
            Assert.That(again.Recap.Today.Keys, Is.EqualTo(50));
            Assert.That(again.Recap.Today.Kills, Is.EqualTo(12), "kills are counted per session, the save keeps today's share");
            Assert.That(again.Recap.Today.Bosses, Is.EqualTo(4));
            Play(again, 0, 0, 0, 0, 3, 0, 0);
            Assert.That(again.Recap.Today.Kills, Is.EqualTo(15));
        }

        [Test]
        public void TheCardHasSixTilesTheDateAndTheHero()
        {
            Texts.UseLanguage("ru");
            var loop = Loop();
            loop.SetLocalDay(Monday);
            Play(loop, 12345, 2345, 1250, 3 * 3600 + 12 * 60, 5432, 87, 120);
            loop.SetLocalDay(Monday + 1);

            var card = RecapView.Card(loop.Recap, RecapKind.Day, "Seeker")!;
            var lines = RecapView.Lines(card);

            Assert.That(card.Title, Is.EqualTo("Итог дня"));
            Assert.That(card.Period, Is.EqualTo(new System.DateTime(1970, 1, 1).AddDays(Monday).ToString("dd.MM.yyyy")));
            Assert.That(card.Tiles.Count, Is.EqualTo(RecapView.TileCount));
            Assert.That(card.Tiles[0].Value, Is.EqualTo("3 ч 12 мин"));
            Assert.That(card.Tiles[1].Value, Is.EqualTo("12 345"));
            Assert.That(card.Tiles[3].Value, Is.EqualTo("1.25 км"));
            Assert.That(card.Tiles[4].Value, Does.Contain("5 432").And.Contain("87"));
            Assert.That(card.Tiles[5].Value, Does.Contain("+120"));
            Assert.That(card.Footer, Does.Contain("Seeker").And.Contain("Rift Warden"));
            Assert.That(lines.Count, Is.LessThanOrEqualTo(7), "the report card has seven lines");
            Assert.That(RecapCard.WidthPx, Is.EqualTo(1200));
            Assert.That(RecapCard.HeightPx, Is.EqualTo(630));
        }

        [Test]
        public void TheWeekCardSaysHowManyDaysWerePlayedAndNothingIsShownWithoutPlay()
        {
            Texts.UseLanguage("ru");
            var loop = Loop();
            loop.SetLocalDay(Monday);
            Assert.That(RecapView.Card(loop.Recap, RecapKind.Day, string.Empty), Is.Null);
            Assert.That(RecapView.Card(loop.Recap, RecapKind.Week, string.Empty), Is.Null);
            Assert.That(RecapView.Card(loop.Recap, RecapKind.None, string.Empty), Is.Null);

            Play(loop, 100, 10, 5, 600, 30, 1, 3);
            loop.SetLocalDay(Monday + 2);
            Play(loop, 100, 10, 5, 600, 30, 1, 3);
            loop.SetLocalDay(Monday + 7);

            var card = RecapView.Card(loop.Recap, RecapKind.Week, string.Empty)!;
            Assert.That(card.Title, Is.EqualTo("Итог недели"));
            Assert.That(card.Footer, Does.Contain("Дней в игре: 2 из 7"));
            Assert.That(card.Tiles[1].Value, Is.EqualTo("200"));
            Assert.That(RecapView.Title(card), Does.Contain("–"));
            Assert.That(RecapView.Title(card).Length, Is.LessThanOrEqualTo(28), "the title plate is narrow");
            Assert.That(card.Period, Does.Contain(" – "));

            var today = RecapView.Card(loop.Recap, RecapKind.Day, string.Empty)!;
            Assert.That(today.Tiles[1].Value, Is.EqualTo("100"), "the day card shows the last closed day");
        }
    }
}
