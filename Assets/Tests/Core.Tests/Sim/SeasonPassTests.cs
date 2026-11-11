using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class SeasonPassTests
    {
        private static SeasonPassRules Rules => TestWorld.Real.SeasonPass;

        [Test]
        public void SeasonsStartTogetherWithClanWarSeasonsAndGiveOnlyPassItems()
        {
            var warSeasonWeeks = ContentNode.Root(TestData.ReadGameDataFile("clan_wars.json"), "clan_wars.json").Field("seasonWeeks").AsInt();
            Assert.That(Rules.SeasonWeeks % warSeasonWeeks, Is.EqualTo(0));
            Assert.That(Rules.FirstWeek % Rules.SeasonWeeks, Is.EqualTo(0));
            Assert.That(Rules.SeasonOfWeek(Rules.FirstWeek - 1), Is.EqualTo(-1));
            Assert.That(Rules.SeasonOfWeek(Rules.FirstWeek + Rules.SeasonWeeks - 1), Is.EqualTo(0));
            Assert.That(Rules.SeasonOfWeek(Rules.FirstWeek + Rules.SeasonWeeks), Is.EqualTo(1));
            var season = Rules.Seasons[0];
            Assert.That(season.Free.Concat(season.Premium).Where(r => r.Cosmetic.Length > 0).All(r => TestWorld.Real.Cosmetics.TryGet(r.Cosmetic, out var item) && item.Source == "pass"), Is.True);
            Assert.That(season.Free.Sum(r => r.Crystals), Is.EqualTo(200));
            Assert.That(TestWorld.Real.Cosmetics.All.Count(c => c.ForSale), Is.EqualTo(94), "pass items are not sold");
        }

        [Test]
        public void BrokenSeasonsAreRejected()
        {
            var json = TestData.ReadGameDataFile(SeasonPassRules.FileName);
            var cosmetics = TestWorld.Real.Cosmetics;
            Assert.Throws<ContentException>(() => SeasonPassRules.Load(json.Replace("\"firstWeek\": 2968", "\"firstWeek\": 2970"), cosmetics));
            Assert.Throws<ContentException>(() => SeasonPassRules.Load(json.Replace("\"cosmetic\": \"pass1_frame\"", "\"cosmetic\": \"skin_duelist\""), cosmetics));
            Assert.Throws<ContentException>(() => SeasonPassRules.Load(json.Replace("\"cosmetic\": \"pass1_font\"", "\"cosmetic\": \"pass1_frame\""), cosmetics));
            Assert.Throws<ContentException>(() => SeasonPassRules.Load(json.Replace("{ \"level\": 10, \"crystals\": 50 }", "{ \"level\": 10, \"crystals\": 50, \"cosmetic\": \"pass1_avatar_gold\" }"), cosmetics));
        }

        [Test]
        public void PlayMinutesAndClaimedQuestsBecomeUnsentPoints()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.SetLocalDay(20_000);
            loop.Tick();
            var pass = loop.SeasonPass;
            Assert.That(pass.State.UnsentPoints, Is.EqualTo(0));
            loop.State.TotalInput.PlaySeconds += 600;
            loop.Tick();
            Assert.That(pass.State.UnsentPoints, Is.EqualTo(10 * Rules.PointsPerActiveMinute));

            var daily = loop.Quests.State.Daily[0];
            daily.Progress = daily.Target;
            Assert.That(loop.ClaimQuest(false, 0), Is.EqualTo(QuestResult.Done));
            var weekly = loop.Quests.State.Weekly[0];
            weekly.Progress = weekly.Target;
            Assert.That(loop.ClaimQuest(true, 0), Is.EqualTo(QuestResult.Done));
            var expected = 10 * Rules.PointsPerActiveMinute + loop.Quests.RewardOf(false, daily.Kind).PassPoints + loop.Quests.Rules.WeeklyReward.PassPoints;
            Assert.That(loop.Quests.Rules.WeeklyReward.PassPoints, Is.GreaterThan(0));
            Assert.That(pass.State.UnsentPoints, Is.EqualTo(expected));

            pass.Sent(expected - 5);
            Assert.That(pass.State.UnsentPoints, Is.EqualTo(5), "points earned while the request was in flight stay");
            pass.Sent(100);
            Assert.That(pass.State.UnsentPoints, Is.EqualTo(0));
        }

        [Test]
        public void TheServerLevelIsKeptInTheSaveAndShownByLevel()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var pass = loop.SeasonPass;
            Assert.That(SeasonPassView.Lines(pass, loop.Cosmetics.Catalog), Is.Empty, "no season before the server answers");

            pass.Apply(0, Rules.PointsPerLevel * 10 + 7, false);
            Assert.That(pass.Level, Is.EqualTo(10));
            Assert.That(pass.PointsInLevel, Is.EqualTo(7));
            pass.State.UnsentPoints = 42;
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.SeasonPass.Points, Is.EqualTo(Rules.PointsPerLevel * 10 + 7));
            Assert.That(copy.SeasonPass.UnsentPoints, Is.EqualTo(42));
            Assert.That(copy.SeasonPass.Season, Is.EqualTo(0));

            var lines = SeasonPassView.Lines(pass, loop.Cosmetics.Catalog);
            Assert.That(SeasonPassView.Title(pass), Is.EqualTo("Сезон первых углей"));
            var five = lines.Single(l => l.Level == 5);
            Assert.That(five.Free!.Got, Is.True);
            Assert.That(five.Free.Name, Is.EqualTo("Угольная кайма"));
            var ten = lines.Single(l => l.Level == 10);
            Assert.That(ten.Premium!.Locked, Is.True);
            Assert.That(ten.Premium.Got, Is.False);
            Assert.That(lines.Single(l => l.Level == 20).Reached, Is.False);

            pass.Apply(0, Rules.MaxPoints * 2, true);
            Assert.That(pass.Complete, Is.True);
            Assert.That(SeasonPassView.Lines(pass, loop.Cosmetics.Catalog).All(l => l.Premium == null || l.Premium.Got), Is.True);
        }
    }
}
