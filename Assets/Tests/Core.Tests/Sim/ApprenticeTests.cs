using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ApprenticeTests
    {
        private static ApprenticeRules Rules => TestWorld.Real.Apprentices;

        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        private static GameLoop Refuge()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.State.RecordStage = 300;
            loop.State.Camp.Tier = 3;
            loop.Tick();
            loop.State.Town.Reputation["gardeners"] = TestWorld.Real.Guilds.Levels[Rules.MinGuildLevel - 1];
            return loop;
        }

        private static long Herbs(GameLoop loop)
        {
            long total = 0;
            foreach (var item in Rules.Of("garden_bed")!.Items)
                total += loop.Camp.Count(item);
            return total;
        }

        [Test]
        public void BrokenApprenticeRulesAreRejected()
        {
            var json = TestData.ReadGameDataFile(ApprenticeRules.FileName);
            var camp = TestWorld.Real.Camp;
            var alchemy = TestWorld.Real.Alchemy;
            Assert.That(ApprenticeRules.Load(json, camp, alchemy).Apprentices.Count, Is.EqualTo(4));
            Assert.Throws<ContentException>(() => ApprenticeRules.Load(json.Replace("\"potion\": \"might\"", "\"potion\": \"nectar\""), camp, alchemy));
            Assert.Throws<ContentException>(() => ApprenticeRules.Load(json.Replace("\"building\": \"pier\"", "\"building\": \"forge\""), camp, alchemy));
            Assert.Throws<ContentException>(() => ApprenticeRules.Load(json.Replace("\"levelHours\": [0, 10,", "\"levelHours\": [5, 10,"), camp, alchemy));
        }

        [Test]
        public void TheMasterSendsAnApprenticeOnlyWhenTheGuildTrustsYou()
        {
            var loop = Refuge();
            Assert.That(loop.Apprentices.Hire("pier"), Is.EqualTo(ApprenticeResult.NotOpen));
            loop.State.Town.Reputation["gardeners"] = 0;
            Assert.That(loop.Apprentices.Hire("garden_bed"), Is.EqualTo(ApprenticeResult.GuildTooLow));
            loop.State.Town.Reputation["gardeners"] = TestWorld.Real.Guilds.Levels[Rules.MinGuildLevel - 1];
            Assert.That(loop.Apprentices.Hire("garden_bed"), Is.EqualTo(ApprenticeResult.Done));
            Assert.That(loop.Apprentices.Hire("garden_bed"), Is.EqualTo(ApprenticeResult.AlreadyHired));
            Assert.That(loop.Apprentices.Hire("trophy_shelf"), Is.EqualTo(ApprenticeResult.NoApprentice));
        }

        [Test]
        public void InBattleTheApprenticeWorksAndTheHarvestWaitsForTheCamp()
        {
            var loop = Refuge();
            loop.Apprentices.Hire("garden_bed");
            var ticks = (int)(Rules.SecondsPerItem / GameLoop.TickSeconds) + 1;
            for (var i = 0; i < ticks; i++)
                loop.Tick();
            var apprentice = loop.Apprentices.At("garden_bed")!;
            Assert.That(loop.Apprentices.Pending(apprentice), Is.EqualTo(1));
            Assert.That(Herbs(loop), Is.Zero, "nothing lands in the bag during battle");
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Tick();
            Assert.That(Herbs(loop), Is.EqualTo(1));
            Assert.That(apprentice.Brought, Is.EqualTo(1));
        }

        [Test]
        public void OfflineWorkIsCappedAndGrowsLevelsAndATrait()
        {
            var loop = Refuge();
            loop.Apprentices.Hire("garden_bed");
            loop.Apprentices.AdvanceOffline(100 * 3600);
            var apprentice = loop.Apprentices.At("garden_bed")!;
            Assert.That(apprentice.Worked, Is.EqualTo(Rules.MaxOfflineSeconds));
            Assert.That(Herbs(loop), Is.GreaterThanOrEqualTo((long)(Rules.MaxOfflineSeconds / Rules.SecondsPerItem)));

            apprentice.Worked = Rules.LevelHours[Rules.TraitLevel - 1] * 3600;
            loop.Apprentices.Collect();
            Assert.That(loop.Apprentices.Level(apprentice), Is.EqualTo(Rules.TraitLevel));
            Assert.That(Rules.Trait(apprentice.Trait), Is.Not.Null, "a trait comes with the level");
            Assert.That(loop.Apprentices.Speed(apprentice), Is.GreaterThan(1 + Rules.SpeedPerLevel * (Rules.TraitLevel - 1) - 1e-9));
        }

        [Test]
        public void TheAlchemistsApprenticeBrewsPotionsAndTheSaveKeepsEveryone()
        {
            var loop = Refuge();
            loop.State.Town.Reputation["alchemists"] = TestWorld.Real.Guilds.Levels[Rules.MinGuildLevel - 1];
            Assert.That(loop.Apprentices.Hire("cauldron"), Is.EqualTo(ApprenticeResult.Done));
            loop.Apprentices.AdvanceOffline(Rules.SecondsPerItem * 2);
            Assert.That(loop.State.Alchemy.Potions.FindAll(p => p.Recipe == "might").Count, Is.GreaterThanOrEqualTo(2));

            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Apprentices.Count, Is.EqualTo(1));
            Assert.That(copy.Apprentices[0].Building, Is.EqualTo("cauldron"));
            Assert.That(copy.Apprentices[0].Worked, Is.EqualTo(Rules.SecondsPerItem * 2).Within(1e-6));

            var lines = ApprenticeView.Lines(loop.Apprentices, TestWorld.Real.Guilds);
            Assert.That(lines.Find(line => line.Building == "cauldron")!.Status, Does.StartWith("уровень 1 из 5"));
            Assert.That(lines.Find(line => line.Building == "pier")!.Status, Is.EqualTo("Постройки ещё нет в убежище"));
            loop.State.Town.Reputation["smiths"] = 0;
            loop.Camp.SetGuilds(new[] { "smiths" });
            Assert.That(ApprenticeView.Lines(loop.Apprentices, TestWorld.Real.Guilds).Find(line => line.Building == "forge")!.Status,
                Is.EqualTo("Мастер отпустит подмастерья с 2-го уровня: «Гильдия кузнецов»"));
        }
    }
}
