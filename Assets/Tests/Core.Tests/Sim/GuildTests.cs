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
            Assert.Throws<ContentException>(() => GuildRules.Load(json.Replace("\"id\": \"finds\"", "\"id\": \"gold\""), biomes, characters), "a resource the game does not count");
            Assert.Throws<ContentException>(() => GuildRules.Load(json.Replace("{ \"guild\": \"cooks\"", "{ \"guild\": \"gardeners\""), biomes, characters), "a hall on the board without resources");
            Assert.That(Rules.VoteDays, Is.EqualTo(14));
            Assert.That(Rules.Resources.Count, Is.EqualTo(TownResource.Counted.Count));
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
            Assert.That(harbor.Guilds[0].Status, Is.EqualTo("Ещё не построено: решает голосование у доски"));
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

            Assert.That(loop.Guilds.Take(TownResource.Labour, 4), Is.EqualTo(4));
            Assert.That(loop.Guilds.UnsentActions, Is.EqualTo(3));
            loop.Guilds.Return(TownResource.Labour, 4);
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
        }

        private static TownBoard Board(TownBoardPhase phase, string project, string vote, long serverNow, long voteEnds, long opensAt, params TownStock[] stocks)
        {
            var candidates = new List<TownCandidate>();
            if (phase == TownBoardPhase.Vote)
            {
                candidates.Add(new TownCandidate("fishers", 12));
                candidates.Add(new TownCandidate("smiths", 30));
            }
            return new TownBoard(phase, 1, voteEnds, opensAt, serverNow, project, vote, new string[0], candidates, stocks);
        }

        [Test]
        public void TheSignCountsDownTheVoteAndTheBoardListsTheCandidates()
        {
            const long hour = 3600_000;
            var board = Board(TownBoardPhase.Vote, string.Empty, "smiths", 1_000_000, 1_000_000 + 77 * hour, 0,
                new TownStock(TownResource.Labour, 800, 0, 120, 120, 5000));
            Assert.That(board.CanVote, Is.True);
            Assert.That(GuildView.Sign(Rules, board, 0), Is.EqualTo("Голосование: осталось 3 д 5 ч"));
            Assert.That(GuildView.Sign(Rules, board, 5 * 3600), Is.EqualTo("Голосование: осталось 3 д"));
            Assert.That(GuildView.Sign(Rules, board, 60 * 3600), Is.EqualTo("Голосование: осталось 17 ч"));
            Assert.That(board.SecondsLeft(1000 * 3600), Is.EqualTo(0));
            var lines = GuildView.Board(Rules, board);
            Assert.That(lines.Count, Is.EqualTo(2));
            Assert.That(lines[0].Text, Is.EqualTo("Гильдия рыбаков — голосов: 12"));
            Assert.That(lines[1].Yours, Is.True);
            Assert.That(lines[1].Text, Is.EqualTo("Гильдия кузнецов — голосов: 30 (ваш голос)"));
            Assert.That(GuildView.QuestTitle(Rules, board), Does.StartWith("Запас города"));
            Assert.That(GuildView.Quest(Rules, board)[0].Text, Is.EqualTo("Труд: собрано 800, ваш вклад 120"));
        }

        [Test]
        public void AfterTheVoteTheSignNamesTheHallItsResourcesAndThenTheCountdown()
        {
            var gather = Board(TownBoardPhase.Gather, "smiths", string.Empty, 1_000_000, 0, 0,
                new TownStock(TownResource.Labour, 3200, 50000, 300, 4800, 5000),
                new TownStock(TownResource.Herbs, 900, 400, 0, 0, 300),
                new TownStock(TownResource.Finds, 10, 0, 0, 0, 200));
            Assert.That(gather.CanVote, Is.False);
            Assert.That(gather.Gathered, Is.False);
            Assert.That(GuildView.Sign(Rules, gather, 0), Is.EqualTo("Строим: Гильдия кузнецов"));
            Assert.That(GuildView.QuestTitle(Rules, gather), Is.EqualTo("Задание города: собрать ресурсы на «Гильдия кузнецов»"));
            var quest = GuildView.Quest(Rules, gather);
            Assert.That(quest.Count, Is.EqualTo(2), "a resource the hall does not need is not listed");
            Assert.That(quest[0].Text, Is.EqualTo("Труд: 3200 из 50000, ваш вклад 300"));
            Assert.That(quest[0].LeftToday, Is.EqualTo(200));
            Assert.That(quest[0].Done, Is.False);
            Assert.That(quest[1].Text, Is.EqualTo("Травы: 400 из 400, ваш вклад 0"));
            Assert.That(quest[1].Done, Is.True);

            var opening = Board(TownBoardPhase.Opening, "smiths", string.Empty, 1_000_000, 0, 1_000_000 + 5 * 3600_000 + 60_000,
                new TownStock(TownResource.Labour, 50000, 50000, 300, 0, 5000));
            Assert.That(opening.Gathered, Is.True);
            Assert.That(GuildView.Sign(Rules, opening, 0), Is.EqualTo("Гильдия кузнецов: откроется через 5 ч 1 мин"));
            var done = Board(TownBoardPhase.Done, string.Empty, string.Empty, 1_000_000, 0, 0);
            Assert.That(GuildView.Sign(Rules, done, 0), Is.EqualTo("Все здания гильдий построены"));
            Assert.That(GuildView.Quest(Rules, done), Is.Empty);
            Assert.That(TownBoard.PhaseOf("vote"), Is.EqualTo(TownBoardPhase.Vote));
            Assert.That(TownBoard.PhaseOf("gather"), Is.EqualTo(TownBoardPhase.Gather));
            Assert.That(TownBoard.PhaseOf("build"), Is.EqualTo(TownBoardPhase.Opening));
            Assert.That(TownBoard.PhaseOf("done"), Is.EqualTo(TownBoardPhase.Done));
        }

        [Test]
        public void WhatThePlayerGathersForThemselvesIsCountedForTheTownAndSaved()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.Tick();
            var plant = TestWorld.Real.Garden.Plant("hushbell")!;
            loop.Garden.Plant(0, "hushbell");
            loop.Garden.Advance(plant.GrowSeconds);
            Assert.That(loop.Garden.Harvest(0), Is.EqualTo(GardenResult.Done));
            var herbs = loop.Camp.Count(plant.Herb);
            Assert.That(herbs, Is.GreaterThan(0));
            Assert.That(loop.Guilds.Unsent(TownResource.Herbs), Is.EqualTo(herbs), "the herbs stay in the bag and count for the town");

            loop.State.Peaceful.Gain(TownResource.Finds, 9);
            loop.State.Peaceful.UnsentActions = 700;
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Peaceful.UnsentGains[TownResource.Finds], Is.EqualTo(9));
            Assert.That(copy.Peaceful.UnsentGains[TownResource.Herbs], Is.EqualTo(herbs));

            var board = Board(TownBoardPhase.Gather, "smiths", string.Empty, 0, 0, 0,
                new TownStock(TownResource.Labour, 0, 50000, 0, 4500, 5000),
                new TownStock(TownResource.Finds, 0, 100, 0, 0, 200),
                new TownStock(TownResource.Herbs, 0, 100, 0, 300, 300));
            var sent = loop.Guilds.TakeFor(board);
            Assert.That(sent[TownResource.Labour], Is.EqualTo(500), "only what is left of the day goes out");
            Assert.That(sent[TownResource.Finds], Is.EqualTo(9));
            Assert.That(sent.ContainsKey(TownResource.Herbs), Is.False);
            Assert.That(loop.Guilds.Unsent(TownResource.Labour), Is.EqualTo(200));
            Assert.That(loop.Guilds.Unsent(TownResource.Finds), Is.EqualTo(0));
            loop.Guilds.Settle(sent, new Dictionary<string, long> { [TownResource.Labour] = 500, [TownResource.Finds] = 4 });
            Assert.That(loop.Guilds.Unsent(TownResource.Finds), Is.EqualTo(5), "what the server did not take comes back");
            Assert.That(loop.Guilds.Unsent(TownResource.Labour), Is.EqualTo(200));
            loop.Guilds.Return(loop.Guilds.TakeFor(board));
            Assert.That(loop.Guilds.Unsent(TownResource.Finds), Is.EqualTo(5), "a failed request returns everything");
            Assert.That(loop.Guilds.TakeFor(Board(TownBoardPhase.Done, string.Empty, string.Empty, 0, 0, 0)), Is.Empty);
        }

        [Test]
        public void ExpeditionFindsCountForTheTown()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.Tick();
            Assert.That(loop.Expeditions.Send("camp_dog", "green_hills", 4), Is.EqualTo(ExpeditionResult.Done));
            loop.Expeditions.FinishWithLoot(0, 1);
            var found = 0L;
            foreach (var item in TestWorld.Real.Expeditions.Biome("green_hills")!.Items)
                found += loop.Camp.Count(item.Item);
            Assert.That(found, Is.GreaterThan(0));
            Assert.That(loop.Guilds.Unsent(TownResource.Finds), Is.GreaterThanOrEqualTo(found));
        }
    }
}
