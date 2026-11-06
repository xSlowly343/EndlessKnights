using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class PeacefulModeTests
    {
        private static PeacefulRules Rules => TestWorld.Data.Peaceful;

        [Test]
        public void InTheCampColossiDoNotFightAndInputGoesToTheActivity()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            loop.Tick();
            Assert.That(loop.Peaceful.SetMode(PeacefulMode.Camp), Is.EqualTo(PeacefulResult.Done));
            var hp = loop.State.EnemyHp;
            var kills = loop.State.Kills;
            for (var i = 0; i < 300; i++)
            {
                input.PressKeys(3);
                input.ClickLeft();
                input.MoveMouse(400);
                loop.Tick();
            }
            Assert.That(loop.State.EnemyHp, Is.EqualTo(hp), "no damage in the camp");
            Assert.That(loop.State.Kills, Is.EqualTo(kills));
            var work = loop.Peaceful.Work(loop.Peaceful.Activity);
            Assert.That(work.Keys, Is.EqualTo(900));
            Assert.That(work.Clicks, Is.EqualTo(300));
            Assert.That(work.MouseMeters, Is.GreaterThan(0));
            Assert.That(loop.Peaceful.SetMode(PeacefulMode.Camp), Is.EqualTo(PeacefulResult.Same));

            loop.Peaceful.SetMode(PeacefulMode.Battle);
            for (var i = 0; i < 50; i++)
            {
                input.PressKeys(3);
                loop.Tick();
            }
            Assert.That(loop.State.EnemyHp, Is.Not.EqualTo(hp), "back in battle the hits land again");
        }

        [Test]
        public void TheWheelChangesTheActivityInTheCampOnly()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            var first = loop.Peaceful.Activity;
            input.ScrollWheel(1);
            loop.Tick();
            Assert.That(loop.Peaceful.Activity, Is.EqualTo(first), "the wheel does nothing in battle");
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            input.ScrollWheel(1);
            loop.Tick();
            Assert.That(loop.Peaceful.Activity, Is.EqualTo(Rules.Activities[1].Id));
            input.ScrollWheel(-2);
            loop.Tick();
            Assert.That(loop.Peaceful.Activity, Is.EqualTo(Rules.Activities[Rules.Activities.Count - 1].Id), "it wraps around");
            Assert.That(PeacefulView.Activities(loop.Peaceful).Single(l => l.On).Id, Is.EqualTo(loop.Peaceful.Activity));
        }

        [Test]
        public void APresetSwitchesTheModeAtItsBoundariesAndAManualSwitchHoldsUntilTheNext()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            Assert.That(loop.Peaceful.SetPreset("weekday_calls", true), Is.True);
            loop.Peaceful.ApplySchedule(2, 9 * 60);
            Assert.That(loop.Peaceful.InCamp, Is.False);
            loop.Peaceful.ApplySchedule(2, 10 * 60);
            Assert.That(loop.Peaceful.InCamp, Is.True, "Tuesday 10:00 is a call");
            loop.Peaceful.SetMode(PeacefulMode.Battle);
            loop.Peaceful.ApplySchedule(2, 10 * 60 + 30);
            Assert.That(loop.Peaceful.InCamp, Is.False, "a manual switch holds inside the same preset");
            loop.Peaceful.ApplySchedule(6, 10 * 60);
            Assert.That(loop.Peaceful.InCamp, Is.False, "not on Saturday");
            Assert.That(PeacefulView.Presets(loop.Peaceful).Single(l => l.Id == "weekday_calls").On, Is.True);
        }

        [Test]
        public void ATeaPauseIsSuggestedOnceAfterLongPlayAndAQuietBreak()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            var battleTicks = (int)(Rules.TeaBattleMinutes * 60 * GameLoop.TicksPerSecond);
            for (var i = 0; i < battleTicks; i++)
            {
                if (i % 5 == 0)
                    input.PressKeys(1);
                loop.Tick();
                loop.Events.Clear();
            }
            var quietTicks = (int)(Rules.TeaIdleMinutes * 60 * GameLoop.TicksPerSecond) + 2;
            var offers = 0;
            for (var i = 0; i < quietTicks * 2; i++)
            {
                loop.Tick();
                offers += loop.Events.CountOf(GameEventKind.TeaPauseSuggested);
                loop.Events.Clear();
            }
            Assert.That(offers, Is.EqualTo(1));

            var off = TestWorld.Loop(input);
            off.State.Peaceful.TeaPause = false;
            off.State.Peaceful.BattleSeconds = Rules.TeaBattleMinutes * 60;
            for (var i = 0; i < quietTicks; i++)
                off.Tick();
            Assert.That(off.Events.CountOf(GameEventKind.TeaPauseSuggested), Is.Zero, "the hint can be turned off");
        }

        [Test]
        public void OfflineInTheCampEarnsOnlyGoldAtAReducedRate()
        {
            var hero = new Game.Core.Stats.StatSheet();
            var offline = new OfflineCalculator(TestWorld.Data, hero);
            var dps = BigNumber.FromDouble(1e9);
            var battle = new GameState { Stage = 20, HighestStage = 20, RecentDps = dps };
            var camp = new GameState { Stage = 20, HighestStage = 20, RecentDps = dps };
            camp.Peaceful.Mode = PeacefulMode.Camp;
            var inBattle = offline.Simulate(battle, 600, dps, false);
            var inCamp = offline.Simulate(camp, 600, dps, false);
            Assert.That(inCamp.StageAfter, Is.EqualTo(20));
            Assert.That(inCamp.Kills, Is.Zero);
            Assert.That(inCamp.Gold.Sign, Is.GreaterThan(0));
            Assert.That(inCamp.Gold.ToDouble(), Is.LessThan(inBattle.Gold.ToDouble()));
        }

        [Test]
        public void TheCampCannotStartInsideARiftOrADungeonAndTheSaveKeepsIt()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.State.Events.FightInside = true;
            Assert.That(loop.Peaceful.SetMode(PeacefulMode.Camp), Is.EqualTo(PeacefulResult.Busy));
            loop.State.Events.FightInside = false;
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Peaceful.SetActivity("alchemy");
            loop.Peaceful.SetPreset("evening_battle", true);
            loop.Peaceful.Work("alchemy").Keys = 12;
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Peaceful.Mode, Is.EqualTo(PeacefulMode.Camp));
            Assert.That(copy.Peaceful.Activity, Is.EqualTo("alchemy"));
            Assert.That(copy.Peaceful.Presets, Does.Contain("evening_battle"));
            Assert.That(copy.Peaceful.Work["alchemy"].Keys, Is.EqualTo(12));
        }
    }
}
