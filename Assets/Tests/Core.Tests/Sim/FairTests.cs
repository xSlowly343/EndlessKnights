using Game.Core.Content;
using Game.Core.Mail;
using Game.Core.Content.Rift;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class FairTests
    {
        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        [Test]
        public void FairRulesNameBothContestsAndRejectAnUnknownOne()
        {
            var json = TestData.ReadGameDataFile(FairRules.FileName);
            Assert.That(FairRules.Load(json).Contests.Count, Is.EqualTo(2));
            Assert.Throws<ContentException>(() => FairRules.Load(json.Replace("\"id\": \"best_garden\"", "\"id\": \"best_hat\"")));
        }

        [Test]
        public void TheGardenTallySendsOnlyWhatIsNewAndSurvivesTheSave()
        {
            var state = new GameState();
            state.Garden.Harvests = 12;
            state.Garden.Mutations = 2;
            var first = FairTally.Garden(state);
            Assert.That(first.Harvests, Is.EqualTo(12));
            Assert.That(first.Mutations, Is.EqualTo(2));
            FairTally.GardenSent(state, first);
            Assert.That(FairTally.Garden(state).Any, Is.False);
            state.Garden.Harvests = 15;

            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(state), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Garden.Mutations, Is.EqualTo(2));
            Assert.That(FairTally.Garden(copy).Harvests, Is.EqualTo(3));
            Assert.That(FairTally.Garden(copy).Mutations, Is.Zero);
        }

        [Test]
        public void AFishIsScoredAgainstTheLargestOfItsKind()
        {
            var fishing = TestWorld.Real.Fishing;
            Assert.That(FairTally.FishScore(fishing, "fish_hills_pike", 45), Is.EqualTo(50));
            Assert.That(FairTally.FishScore(fishing, "legend_hills_boar_catfish", 210), Is.EqualTo(100));
            Assert.That(FairTally.FishScore(fishing, "fish_moon", 10), Is.Zero);
        }

        [Test]
        public void AFairLetterNamesItsContest()
        {
            var letter = new MailLetter("fair-big_fish-3000", MailLetter.FairKind, 0, 1, 5, 98, FairRules.BigFish, new RiftReward(0, 0, 15), false);
            Assert.That(MailText.Title(letter), Is.EqualTo("Итоги ярмарки"));
            Assert.That(MailText.Body(letter, fair: TestWorld.Real.Fair), Is.EqualTo("«Самая большая рыба»: 1 место из 5, счёт 98"));
        }
    }
}
