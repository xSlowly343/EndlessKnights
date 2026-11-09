using System.Collections.Generic;
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
    public class GuildTests
    {
        private static GuildRules Rules => TestWorld.Real.Guilds;

        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        [Test]
        public void GuildLevelsGoFromOneToTenByReputation()
        {
            Assert.That(Rules.MaxLevel, Is.EqualTo(10));
            Assert.That(Rules.LevelAt(0), Is.EqualTo(1));
            Assert.That(Rules.LevelAt(Rules.Levels[1] - 1), Is.EqualTo(1));
            Assert.That(Rules.LevelAt(Rules.Levels[1]), Is.EqualTo(2));
            Assert.That(Rules.LevelAt(1_000_000), Is.EqualTo(10));
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.State.Town.Reputation["fishers"] = Rules.Levels[3];
            Assert.That(loop.Guilds.Level("fishers"), Is.EqualTo(4));
            Assert.That(loop.Guilds.Level("cooks"), Is.EqualTo(1));
        }

        [Test]
        public void EveryCampBuildingGuildIsBuiltByTheServerAndBrokenRulesAreRejected()
        {
            foreach (var building in TestWorld.Real.Camp.Buildings)
            {
                if (building.Guild.Length > 0)
                    Assert.That(Rules.BuildOrder, Does.Contain(building.Guild), building.Id);
            }
            var json = TestData.ReadGameDataFile(GuildRules.FileName);
            var biomes = TestWorld.Real.Stages.Biomes;
            var characters = new List<string>();
            foreach (var character in TestWorld.Real.Characters.All)
                characters.Add(character.Id);
            Assert.That(GuildRules.Load(json, biomes, characters).Guilds.Count, Is.EqualTo(7));
            Assert.Throws<ContentException>(() => GuildRules.Load(json.Replace("\"master\": \"dowser\"", "\"master\": \"huscarl\""), biomes, characters));
            Assert.Throws<ContentException>(() => GuildRules.Load(json.Replace("\"biome\": \"ice_peak\"", "\"biome\": \"moon\""), biomes, characters));
            Assert.Throws<ContentException>(() => GuildRules.Load(json.Replace("\"order\": [\"fishers\", ", "\"order\": ["), biomes, characters));
            Assert.Throws<ContentException>(() => GuildRules.Load(json.Replace("\"levels\": [0, 30,", "\"levels\": [5, 30,"), biomes, characters));
        }

        [Test]
        public void DistrictsOpenWithClearedBiomesAndTheObservatoryWithTheSkySquare()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var guilds = loop.Guilds;
            var length = TestWorld.Real.Stages.BiomeLength;
            loop.State.RecordStage = 1;
            var lines = GuildView.Districts(guilds);
            Assert.That(lines[0].Name, Is.EqualTo("Ворота"));
            Assert.That(lines[0].Open, Is.True);
            Assert.That(lines.FindAll(line => line.Open).Count, Is.EqualTo(1));
            Assert.That(guilds.ObservatoryOpen, Is.False);
            Assert.That(lines[0].Guilds.Count, Is.EqualTo(2));
            Assert.That(lines[0].Guilds[0].Built, Is.True, "the herbalist's tent stands from the start");
            Assert.That(lines[0].Guilds[0].Master, Is.EqualTo("Herbwitch"));

            loop.State.RecordStage = length + 1;
            Assert.That(GuildView.Districts(guilds).FindAll(line => line.Open).Count, Is.EqualTo(2));
            loop.State.RecordStage = TestWorld.Real.Stages.OpensAt(4) + length - 1;
            Assert.That(guilds.ObservatoryOpen, Is.False);
            loop.State.RecordStage = TestWorld.Real.Stages.OpensAt(4) + length;
            Assert.That(guilds.ObservatoryOpen, Is.True);
            Assert.That(GuildView.Districts(guilds).Find(line => line.Id == "rift_crack")!.Open, Is.False);
            loop.State.RecordStage = TestWorld.Real.Stages.HeartFromStage;
            Assert.That(GuildView.Districts(guilds).Find(line => line.Id == "rift_crack")!.Open, Is.True);

            var harbor = GuildView.Districts(guilds).Find(line => line.Id == "ice_harbor")!;
            Assert.That(harbor.Guilds[0].Built, Is.False);
            Assert.That(harbor.Guilds[0].Status, Is.EqualTo("Здание строит весь сервер"));
            loop.Camp.SetGuilds(new[] { "fishers" });
            Assert.That(GuildView.Districts(guilds).Find(line => line.Id == "ice_harbor")!.Guilds[0].Status, Is.EqualTo("уровень 1 из 10"));
        }

        [Test]
        public void PeacefulActionsInTheCampAreCountedSentAndSaved()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            input.PressKeys(5);
            input.ClickLeft(2);
            loop.Tick();
            Assert.That(loop.Guilds.UnsentActions, Is.EqualTo(7));

            Assert.That(loop.Guilds.TakeActions(4), Is.EqualTo(4));
            Assert.That(loop.Guilds.UnsentActions, Is.EqualTo(3));
            loop.Guilds.ReturnActions(4);
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Peaceful.UnsentActions, Is.EqualTo(7));
        }

        [Test]
        public void TheForecastIsTheSameAllDayAndOnlyNamesOpenFish()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.State.RecordStage = 1;
            var closed = loop.Guilds.Forecast(20400);
            Assert.That(closed.Fish, Is.Empty, "no biome cleared, no fish to forecast");
            Assert.That(closed.Plant, Is.Not.Empty);

            loop.State.RecordStage = 6 * TestWorld.Real.Stages.BiomeLength;
            var today = loop.Guilds.Forecast(20400);
            Assert.That(loop.Guilds.Forecast(20400).Fish, Is.EqualTo(today.Fish));
            Assert.That(TestWorld.Real.Fishing.Fish(today.Fish), Is.Not.Null);
            var seen = new HashSet<string>();
            for (var day = 20400; day < 20430; day++)
                seen.Add(loop.Guilds.Forecast(day).Fish);
            Assert.That(seen.Count, Is.GreaterThan(3), "the forecast changes from day to day");
            Assert.That(GuildView.Forecast(today, TestWorld.Real.Fishing, TestWorld.Real.Garden), Does.StartWith("Обсерватория: сегодня клюёт "));
            Assert.That(GuildView.Project(Rules, "smiths", 3200, 50000), Is.EqualTo("Сервер строит «Гильдия кузнецов»: 3200 из 50000 мирных действий"));
        }
    }
}
