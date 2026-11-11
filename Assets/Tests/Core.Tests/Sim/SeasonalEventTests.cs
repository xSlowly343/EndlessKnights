using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class SeasonalEventTests
    {
        private static EventRules Rules => TestWorld.Data.Events;
        private static long PumpkinDay => EventRules.DayOf(2026, 10, 31);

        private static StatSheet Strong()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1e12));
            return hero;
        }

        private static GameLoop InEvent(FakeInputSource input, StatSheet? hero = null)
        {
            var loop = TestWorld.Loop(input, hero: hero ?? Strong());
            loop.SetLocalDay(PumpkinDay);
            return loop;
        }

        private static void Type(GameLoop loop, FakeInputSource input, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Events.Clear();
            }
        }

        [Test]
        public void FourEventsAYearTwoWeeksEachAndTheShopStaysThreeDaysLonger()
        {
            Assert.That(Rules.Events.Count, Is.EqualTo(4));
            Assert.That(Rules.WindowDays, Is.EqualTo(14));
            Assert.That(Rules.WindowAt(PumpkinDay)!.Value.Id, Is.EqualTo("pumpkin_night_2026"));
            var window = Rules.WindowAt(PumpkinDay)!.Value;
            Assert.That(Rules.WindowAt(window.FirstDay - 1), Is.Null);
            Assert.That(Rules.WindowAt(window.EndDay), Is.Null);
            Assert.That(Rules.ShopWindowAt(window.EndDay + Rules.ShopGraceDays - 1)!.Value.Id, Is.EqualTo(window.Id));
            Assert.That(Rules.ShopWindowAt(window.EndDay + Rules.ShopGraceDays), Is.Null);
            Assert.That(Rules.WindowAt(EventRules.DayOf(2027, 1, 3))!.Value.Id, Is.EqualTo("long_night_2026"), "the New Year event crosses the year");
            Assert.That(Rules.WindowAt(EventRules.DayOf(2026, 8, 1)), Is.Null);
            Assert.That(Rules.Events.SelectMany(e => e.Shop).All(i => TestWorld.Data.Cosmetics.TryGet(i.Cosmetic, out var c) && c.Source == "event"), Is.True);
        }

        [Test]
        public void EveryFifthEnemyIsAGuestAndPaysATokenOnlyDuringTheEvent()
        {
            var input = new FakeInputSource();
            var outside = TestWorld.Loop(input, hero: Strong());
            outside.SetLocalDay(EventRules.DayOf(2026, 8, 1));
            Type(outside, input, 200);
            Assert.That(outside.State.Events.Tokens, Is.EqualTo(0));

            var loop = InEvent(input);
            Type(loop, input, 400);
            var events = loop.State.Events;
            Assert.That(events.Guests, Is.GreaterThan(0));
            Assert.That(events.Guests, Is.InRange(events.Spawns / Rules.GuestEvery - 2, events.Spawns / Rules.GuestEvery + 1));
            Assert.That(events.Tokens, Is.EqualTo(events.Guests * Rules.TokensPerGuest));
            Assert.That(events.UnsentTokens, Is.EqualTo(events.Tokens));
        }

        [Test]
        public void TheEventBossHasTheDefenseOfTheRecordStage()
        {
            var loop = InEvent(new FakeInputSource());
            loop.State.RecordStage = 20000;

            Assert.That(loop.SeasonalEvents.StartBossFight(), Is.EqualTo(EventFightResult.Started));

            var defense = TestWorld.Data.Combat.Defense.Defense(20000, loop.State.EnemyType != EnemyType.Normal);
            Assert.That(defense, Is.GreaterThan(0));
            Assert.That(loop.State.EnemyDefense, Is.EqualTo(defense));
            Assert.That(loop.State.EnemyResists, Is.Not.EqualTo(ElementRules.None));
        }

        [Test]
        public void TheEventBossIsAsThickAsTheRecordBossAtHome()
        {
            var loop = InEvent(new FakeInputSource());
            loop.State.RecordStage = 20000;
            var stages = TestWorld.Data.Stages;

            loop.SeasonalEvents.StartBossFight();

            Assert.That((stages.ForDepth(1, Rules.BossHpMultiplier, Rules.BossTimerSeconds).EnemyHp(20000) / stages.EnemyHp(20000)).ToDouble(), Is.EqualTo(1).Within(1e-9));
            Assert.That(loop.State.EnemyMaxHp.Log10(), Is.GreaterThan(stages.EnemyHp(20000).Log10() - 1));
        }

        [Test]
        public void ALongAbsenceInsideTheEventFightReturnsHomeAndGivesTheFightBack()
        {
            var loop = InEvent(new FakeInputSource());
            loop.State.RecordStage = 30;
            var home = loop.State.Stage;
            loop.SeasonalEvents.StartBossFight();
            Assert.That(loop.SeasonalEvents.FightsLeftToday, Is.Zero);

            Assert.That(loop.ReturnHomeIfAway(5), Is.False);
            Assert.That(loop.SeasonalEvents.FightInside, Is.True);
            Assert.That(loop.ReturnHomeIfAway(3600), Is.True);

            Assert.That(loop.SeasonalEvents.FightInside, Is.False);
            Assert.That(loop.State.Stage, Is.EqualTo(home));
            Assert.That(loop.SeasonalEvents.FightsLeftToday, Is.EqualTo(1));
        }

        [Test]
        public void TheEventBossIsFoughtOnceADayAndAWinPaysTokensAndAScore()
        {
            var input = new FakeInputSource();
            var loop = InEvent(input);
            loop.State.RecordStage = 30;
            var home = loop.State.Stage;
            Assert.That(loop.SeasonalEvents.StartBossFight(), Is.EqualTo(EventFightResult.Started));
            Assert.That(loop.State.Stage, Is.EqualTo(30));
            Assert.That(loop.SeasonalEvents.StartBossFight(), Is.EqualTo(EventFightResult.NoFightsLeft));
            Assert.That(loop.Dungeons.Enter(new DungeonKey(1, System.Array.Empty<string>())), Is.EqualTo(DungeonResult.AlreadyInside));
            for (var i = 0; i < 2000 && loop.SeasonalEvents.FightInside; i++)
                Type(loop, input, 1);
            Assert.That(loop.SeasonalEvents.FightInside, Is.False);
            Assert.That(loop.State.Stage, Is.EqualTo(home), "home progress comes back");
            Assert.That(loop.State.Events.BossWins, Is.EqualTo(1));
            Assert.That(loop.State.Events.UnsentScore, Is.GreaterThan(0));
            Assert.That(loop.State.Events.Tokens, Is.GreaterThanOrEqualTo(Rules.BossTokens));

            loop.SetLocalDay(PumpkinDay + 1);
            Assert.That(loop.SeasonalEvents.FightsLeftToday, Is.EqualTo(Rules.BossPerDay));
        }

        [Test]
        public void GoalsAreDoneOnceAndSentGoalsAreRemembered()
        {
            var loop = InEvent(new FakeInputSource());
            var events = loop.State.Events;
            events.Guests = 300;
            events.DailyQuests = 5;
            loop.Tick();
            Assert.That(loop.SeasonalEvents.GoalsToSend(), Is.EqualTo(new[] { "guests", "quests" }));
            loop.SeasonalEvents.Sent(events.WindowId, 0, new[] { "guests" }, 0);
            Assert.That(loop.SeasonalEvents.GoalsToSend(), Is.EqualTo(new[] { "quests" }));
            loop.SeasonalEvents.Sent("other_window", 0, new[] { "quests" }, 0);
            Assert.That(loop.SeasonalEvents.GoalsToSend(), Is.EqualTo(new[] { "quests" }), "a reply for another window changes nothing");

            var goals = EventView.Goals(loop.SeasonalEvents);
            Assert.That(goals.Count, Is.EqualTo(6));
            Assert.That(goals.Single(g => g.Id == "guests").Done, Is.True);
            Assert.That(EventView.Shop(loop.SeasonalEvents, loop.Cosmetics).Count, Is.EqualTo(5));
            Assert.That(EventView.Title(loop.SeasonalEvents), Is.EqualTo("Ночь Тыкв"));
        }

        [Test]
        public void ANewEventStartsFromZeroAndTheSaveKeepsProgress()
        {
            var loop = InEvent(new FakeInputSource());
            loop.State.Events.Guests = 42;
            loop.State.Events.UnsentTokens = 7;
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Events.Guests, Is.EqualTo(42));
            Assert.That(copy.Events.UnsentTokens, Is.EqualTo(7));
            Assert.That(copy.Events.WindowId, Is.EqualTo("pumpkin_night_2026"));

            loop.SetLocalDay(EventRules.DayOf(2026, 12, 31));
            Assert.That(loop.State.Events.WindowId, Is.EqualTo("long_night_2026"));
            Assert.That(loop.State.Events.Guests, Is.EqualTo(0));
        }
    }
}
