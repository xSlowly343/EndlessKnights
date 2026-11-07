using Game.Core.Content;
using Game.Core.Content.Rift;
using Game.Core.Input;
using Game.Core.Mail;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class TournamentTests
    {
        private static TournamentRules Rules => TestWorld.Real.Tournaments;

        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        [Test]
        public void RewardsGrowWithThePlaceAndTheBand()
        {
            Assert.That(Rules.BandOf(1), Is.EqualTo(0));
            Assert.That(Rules.BandOf(999), Is.EqualTo(0));
            Assert.That(Rules.BandOf(1000), Is.EqualTo(1));
            Assert.That(Rules.BandOf(80000), Is.EqualTo(Rules.Bands.Count - 1));
            var first = Rules.RewardFor(1, 0);
            Assert.That(first.Ingots, Is.EqualTo(120));
            Assert.That(first.Essence, Is.EqualTo(60));
            Assert.That(Rules.RewardFor(2, 0).Ingots, Is.LessThan(first.Ingots));
            Assert.That(Rules.RewardFor(Rules.GroupSize, 0).Ingots, Is.GreaterThan(0), "every place in the group is paid");
            Assert.That(Rules.RewardFor(Rules.GroupSize + 1, 0).IsEmpty, Is.True);
            Assert.That(Rules.RewardFor(1, 1).Ingots, Is.EqualTo(180));
            Assert.That(Rules.RewardFor(0, 0).IsEmpty, Is.True);
        }

        [Test]
        public void BrokenTournamentRulesAreRejected()
        {
            var json = TestData.ReadGameDataFile(TournamentRules.FileName);
            Assert.That(TournamentRules.Load(json).Formats.Count, Is.EqualTo(3));
            Assert.Throws<ContentException>(() => TournamentRules.Load(json.Replace("\"id\": \"kills\"", "\"id\": \"damage\"")));
            Assert.Throws<ContentException>(() => TournamentRules.Load(json.Replace("\"id\": \"kills\"", "\"id\": \"bosses\"")));
            Assert.Throws<ContentException>(() => TournamentRules.Load(json.Replace("\"startDays\": [0, 3]", "\"startDays\": [3, 0]")));
            Assert.Throws<ContentException>(() => TournamentRules.Load(json.Replace("{ \"from\": 1, \"scale\": 1 }", "{ \"from\": 5, \"scale\": 1 }")));
            Assert.Throws<ContentException>(() => TournamentRules.Load(json.Replace("{ \"upTo\": 50, \"ingots\": 15, \"essence\": 8 }", "{ \"upTo\": 60, \"ingots\": 15, \"essence\": 8 }")));
        }

        [Test]
        public void ABossHuntCountsOnlyBossesAfterJoining()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.State.BossKills = 40;
            loop.State.Kills = 900;

            Assert.That(loop.Tournament.Join(7, "bosses"), Is.EqualTo(TournamentJoin.Joined));
            Assert.That(loop.Tournament.Join(7, "bosses"), Is.EqualTo(TournamentJoin.AlreadyJoined));
            Assert.That(loop.Tournament.State.Best, Is.EqualTo(0));
            loop.State.BossKills = 43;
            loop.Tick();
            Assert.That(loop.Tournament.State.Best, Is.EqualTo(3));
            Assert.That(loop.Tournament.State.Unsent, Is.True);

            loop.Tournament.Sent(7, 2);
            Assert.That(loop.Tournament.State.Unsent, Is.True, "an older score does not clear a newer one");
            loop.Tournament.Sent(7, 3);
            Assert.That(loop.Tournament.State.Unsent, Is.False);
        }

        [Test]
        public void TheAscentNeedsAPrestigeAndKeepsTheBestStage()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.State.HighestStage = 5;
            Assert.That(loop.Tournament.CanJoin("climb"), Is.False);
            Assert.That(loop.Tournament.Join(3, "climb"), Is.EqualTo(TournamentJoin.PrestigeNotReady));
            Assert.That(loop.Tournament.State.Joined, Is.False);

            loop.State.HighestStage = TestWorld.Data.Prestige.MinStage + 20;
            var prestiges = loop.State.Prestiges;
            Assert.That(loop.Tournament.CanJoin("climb"), Is.True);
            Assert.That(loop.Tournament.Join(3, "climb"), Is.EqualTo(TournamentJoin.Joined));
            Assert.That(loop.State.Prestiges, Is.EqualTo(prestiges + 1));
            var start = loop.Tournament.State.Best;
            Assert.That(start, Is.LessThan(TestWorld.Data.Prestige.MinStage));

            loop.State.HighestStage = 300;
            loop.Tick();
            loop.State.HighestStage = 12;
            loop.Tick();
            Assert.That(loop.Tournament.State.Best, Is.EqualTo(300), "a second prestige does not lower the score");
            Assert.That(loop.Tournament.Join(4, "unknown"), Is.EqualTo(TournamentJoin.UnknownFormat));
        }

        [Test]
        public void ANewTournamentClearsTheOldScoreAndTheSaveKeepsIt()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.Tournament.Join(9, "kills");
            loop.State.Kills += 25;
            loop.Tick();

            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Tournament.Index, Is.EqualTo(9));
            Assert.That(copy.Tournament.Format, Is.EqualTo("kills"));
            Assert.That(copy.Tournament.Best, Is.EqualTo(25));
            Assert.That(copy.Tournament.Unsent, Is.True);

            loop.Tournament.Follow(9);
            Assert.That(loop.Tournament.State.Joined, Is.True);
            loop.Tournament.Follow(10);
            Assert.That(loop.Tournament.State.Joined, Is.False);
            loop.State.Kills += 25;
            loop.Tick();
            Assert.That(loop.Tournament.State.Best, Is.EqualTo(0));

            var empty = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(empty.Tournament.Joined, Is.False);
            Assert.That(empty.Tournament.Format, Is.Empty);
        }

        [Test]
        public void TheViewNamesTheFormatShowsRewardsAndTheLetterPays()
        {
            var bosses = Rules.Format("bosses")!;
            Assert.That(TournamentView.FormatName(Rules, "bosses"), Is.EqualTo("Охота на боссов"));
            Assert.That(TournamentView.Score(bosses, 12), Is.EqualTo("боссов: 12"));
            Assert.That(TournamentView.JoinButton(Rules.Format("climb")!, true, true), Is.EqualTo("Престиж и вступить"));
            Assert.That(TournamentView.JoinButton(bosses, true, false), Is.EqualTo("Вступить уже нельзя — ждите следующий"));
            var rows = TournamentView.Rewards(Rules, 0);
            Assert.That(rows.Count, Is.EqualTo(Rules.Rewards.Count));
            Assert.That(rows[0].Places, Is.EqualTo("1 место"));
            Assert.That(rows[0].Reward, Is.EqualTo("Слитки Разлома ×120 · Эссенция Разлома ×60"));
            Assert.That(rows[1].Places, Is.EqualTo("2–3 место"));

            var letter = new MailLetter("tournament-6001", MailLetter.TournamentKind, 1, 2, 37, 340, "bosses", new RiftReward(0, 80, 0, 40), false);
            Assert.That(MailText.Title(letter), Is.EqualTo("Итоги турнира"));
            Assert.That(MailText.Body(letter, null, Rules), Is.EqualTo("«Охота на боссов»: 2 место из 37, счёт 340"));
            var state = new GameState();
            var box = new MailBox();
            box.Replace(new[] { letter });
            Assert.That(box.Take(letter, state), Is.True);
            Assert.That(state.Drops.Ingots, Is.EqualTo(80));
            Assert.That(state.Drops.Essence, Is.EqualTo(40));
        }
    }
}
