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
                    Assert.That(Rules.Building(building.Guild), Is.Not.Null, building.Id);
            }
            var json = TestData.ReadGameDataFile(GuildRules.FileName);
            var biomes = TestWorld.Real.Stages.Biomes;
            var characters = new List<string>();
            foreach (var character in TestWorld.Real.Characters.All)
                characters.Add(character.Id);
            Assert.That(GuildRules.Load(json, biomes, characters).Guilds.Count, Is.EqualTo(7));
            Assert.Throws<ContentException>(() => GuildRules.Load(json.Replace("\"master\": \"dowser\"", "\"master\": \"huscarl\""), biomes, characters));
            Assert.Throws<ContentException>(() => GuildRules.Load(json.Replace("\"biome\": \"ice_peak\"", "\"biome\": \"moon\""), biomes, characters));
            Assert.Throws<ContentException>(() => GuildRules.Load(json.Replace("\"id\": \"fishers\",\n        \"guild\": \"fishers\",", "\"id\": \"fishers\","), biomes, characters), "a guild without a hall");
            Assert.Throws<ContentException>(() => GuildRules.Load(json.Replace("\"levels\": [0, 30,", "\"levels\": [5, 30,"), biomes, characters));
            Assert.Throws<ContentException>(() => GuildRules.Load(json.Replace("\"id\": \"finds\"", "\"id\": \"gold\""), biomes, characters), "a resource the game does not count");
            Assert.Throws<ContentException>(() => GuildRules.Load(json.Replace("\"perPlayerDay\": { \"labour\": 800, \"wood\": 30 }", "\"perPlayerDay\": { }"), biomes, characters), "a level without resources");
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
            Assert.That(lines[0].Guilds[0].Built, Is.False, "the herbwitch's house waits for the town to build it");
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
                candidates.Add(new TownCandidate("gardeners:2", 4));
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
            Assert.That(lines.Count, Is.EqualTo(3));
            Assert.That(lines[0].Text, Is.EqualTo("Пристань рыбаков — голосов: 12"));
            Assert.That(lines[1].Yours, Is.True);
            Assert.That(lines[1].Text, Is.EqualTo("Кузня — голосов: 30 (ваш голос)"));
            Assert.That(lines[2].Text, Is.EqualTo("Дом травницы, уровень 2 — голосов: 4"));
            Assert.That(lines[2].Level, Is.EqualTo(2));
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
            Assert.That(GuildView.Sign(Rules, gather, 0), Is.EqualTo("Строим: Кузня"));
            Assert.That(GuildView.QuestTitle(Rules, gather), Is.EqualTo("Задание города: собрать ресурсы на «Кузня»"));
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
            Assert.That(GuildView.Sign(Rules, opening, 0), Is.EqualTo("Кузня: откроется через 5 ч 1 мин"));
            var done = Board(TownBoardPhase.Done, string.Empty, string.Empty, 1_000_000, 0, 0);
            Assert.That(GuildView.Sign(Rules, done, 0), Is.EqualTo("Кромка отстроена целиком: все здания и их уровни готовы"));
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
        public void SitesOpenAsTheHeroClearsTheWayAndGatherWhileTheHeroFights()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var sites = loop.Guilds.Rules.Sites;
            Assert.That(sites.Count, Is.GreaterThanOrEqualTo(8));
            loop.State.RecordStage = sites[0].FromStage - 1;
            loop.Tick();
            Assert.That(loop.Guilds.NextSite, Is.SameAs(sites[0]));
            var lines = GuildView.Sites(loop.Guilds);
            Assert.That(lines.Count, Is.EqualTo(sites.Count));
            Assert.That(lines[0].Open, Is.False);
            Assert.That(lines[0].Text, Is.EqualTo($"Лесосека («Древесина»): откроется на этапе {sites[0].FromStage}"));
            loop.State.Kills += 1000;
            loop.Tick();
            Assert.That(loop.Guilds.Unsent(TownResource.Wood), Is.Zero, "a closed site gathers nothing");

            loop.Events.Clear();
            loop.State.RecordStage = sites[1].FromStage;
            loop.Tick();
            Assert.That(loop.Events.CountOf(GameEventKind.TownSiteOpened), Is.EqualTo(2));
            Assert.That(GuildView.SiteOpened(Rules, 0), Is.EqualTo("Путь расчищен: у Кромки заработал промысел «Лесосека» — добывает «Древесина»"));
            Assert.That(GuildView.Sites(loop.Guilds)[0].Text, Is.EqualTo("Лесосека: добывает «Древесина», пока вы сражаетесь"));
            Assert.That(loop.Guilds.NextSite, Is.SameAs(sites[2]));

            var herbs = loop.Guilds.Unsent(TownResource.Herbs);
            var wood = loop.Guilds.Unsent(TownResource.Wood);
            loop.State.Kills += 1000;
            loop.Tick();
            Assert.That(loop.Guilds.Unsent(TownResource.Wood) - wood, Is.EqualTo(1000 * sites[0].PerKill).Within(1));
            Assert.That(loop.Guilds.Unsent(TownResource.Herbs) - herbs, Is.EqualTo(1000 * sites[1].PerKill).Within(1), "the meadow adds to the herbs of the garden");
            Assert.That(loop.Guilds.Unsent(TownResource.Stone), Is.Zero);

            loop.Events.Clear();
            loop.Tick();
            Assert.That(loop.Events.CountOf(GameEventKind.TownSiteOpened), Is.Zero, "a site opens once");
            var saved = TestWorld.Loop(new FakeInputSource(), new GameState(1) { RecordStage = sites[3].FromStage, Kills = 5000 });
            saved.Tick();
            saved.Tick();
            Assert.That(saved.Events.CountOf(GameEventKind.TownSiteOpened), Is.Zero, "sites of a loaded save do not open again");
            Assert.That(saved.Guilds.Unsent(TownResource.Wood), Is.Zero, "kills made before the sites existed are not gathered");
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Peaceful.SiteKills, Is.EqualTo(loop.State.Peaceful.SiteKills), "the save remembers how many kills the sites have counted");
        }

        [Test]
        public void SitesGatherWithoutAllocations()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.State.RecordStage = Rules.Sites[Rules.Sites.Count - 1].FromStage;
            loop.Tick();
            loop.State.Kills += 100_000;
            loop.Guilds.Tick(loop.Events.Count);
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
            {
                loop.State.Kills += 37;
                loop.Guilds.Tick(loop.Events.Count);
            }
            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }

        [Test]
        public void WhatTheHallsMakeCountsForTheTown()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.Tick();
            loop.Events.Clear();
            var made = new[]
            {
                (GameEventKind.FishCaught, TownResource.Fish), (GameEventKind.DishCooked, TownResource.Dishes), (GameEventKind.PotionBrewed, TownResource.Potions),
                (GameEventKind.ItemSmelted, TownResource.Metalwork), (GameEventKind.ItemTempered, TownResource.Metalwork), (GameEventKind.FindDug, TownResource.Relics),
                (GameEventKind.TinctureDistilled, TownResource.Tinctures)
            };
            foreach (var (kind, _) in made)
                loop.Events.Add(GameEvent.ForStage(kind, loop.State.Tick, 0));
            loop.Events.Add(GameEvent.ForStage(GameEventKind.FishBite, loop.State.Tick, 0));
            loop.Guilds.Tick(0);
            Assert.That(loop.Guilds.Unsent(TownResource.Metalwork), Is.EqualTo(2));
            foreach (var (_, resource) in made)
                Assert.That(loop.Guilds.Unsent(resource), Is.GreaterThan(0), resource);
            Assert.That(loop.State.Peaceful.UnsentGains.Count, Is.EqualTo(6));
            foreach (var resource in Rules.Resources)
            {
                if (resource.Kind == TownResourceKind.Craft)
                    Assert.That(resource.Guild, Is.Not.Empty, resource.Id);
                Assert.That(resource.PointsPerUnit * resource.DailyCap, Is.EqualTo(TownResource.PointsPerDay).Within(1e-9), "a full day of any resource is worth the same");
            }
        }

        [Test]
        public void TheBoardShowsYourPartTheRewardTiersAndTheBestBuilders()
        {
            var tiers = Rules.Rewards;
            Assert.That(tiers.Count, Is.EqualTo(8), "twice as many steps as before");
            for (var i = 1; i < tiers.Count; i++)
                Assert.That(tiers[i].Shards, Is.GreaterThan(tiers[i - 1].Shards), tiers[i].Id);
            Assert.That(Rules.StageShards, Is.GreaterThan(0));
            Assert.That(Rules.TierAt(tiers[0].Points - 1), Is.Null);
            Assert.That(Rules.TierAt(tiers[1].Points), Is.SameAs(tiers[1]));
            var last = tiers[tiers.Count - 1];
            Assert.That(Rules.TierAfter(last.Points), Is.Null);
            var board = Board(TownBoardPhase.Gather, "smiths", string.Empty, 0, 0, 0);
            Assert.That(board.Top, Is.Empty);
            Assert.That(GuildView.Reward(Rules, board), Is.EqualTo($"Ваш вклад: 0 очков. До ступени «Подсобник» ещё {tiers[0].Points}"));
            board.WithBuilders(tiers[1].Points + 500, 2, new[] { new TownBuilder(1, "Mira", 90000), new TownBuilder(2, "You", tiers[1].Points + 500) });
            Assert.That(GuildView.Reward(Rules, board), Is.EqualTo($"Ваш вклад: {tiers[1].Points + 500} очков — «Подносчик». До ступени «Плотник» ещё {tiers[2].Points - tiers[1].Points - 500}"));
            board.WithBuilders(last.Points, 1, board.Top);
            Assert.That(GuildView.Reward(Rules, board), Is.EqualTo($"Ваш вклад: {last.Points} очков — «Градостроитель», высшая ступень"));
            Assert.That(GuildView.RewardOf(tiers[0]), Is.EqualTo($"«Подсобник» — от {tiers[0].Points} очков: Осколков {tiers[0].Shards}"));
            Assert.That(GuildView.RewardOf(last), Is.EqualTo($"«Градостроитель» — от {last.Points} очков: Осколков {last.Shards}, Знаков дружбы {last.Friendship}"));
            var top = GuildView.Top(board);
            Assert.That(top[0].Text, Is.EqualTo("1. Mira — 90000 (вы)"));
            Assert.That(top[1].Yours, Is.False);
            Assert.That(GuildView.Reward(Rules, Board(TownBoardPhase.Done, string.Empty, string.Empty, 0, 0, 0)), Is.Empty);
            Assert.That(GuildView.KindName(TownResourceKind.Raw), Is.EqualTo("Сырьё промыслов"));

            var thanks = new Game.Core.Mail.MailLetter("town-build-1", Game.Core.Mail.MailLetter.TownKind, 0, 3, 0, 15500, "smiths", new Game.Core.Content.Rift.RiftReward(40, 0, 2), false);
            Assert.That(MailText.Title(thanks), Is.EqualTo("Кромка благодарит"));
            Assert.That(MailText.Body(thanks, guilds: Rules), Is.EqualTo("Построено: «Кузня». Ваш вклад — 15500 очков, место 3 среди строителей"));
            var upgrade = new Game.Core.Mail.MailLetter("town-build-9", Game.Core.Mail.MailLetter.TownKind, 0, 0, 0, 2000, "palisade:3", new Game.Core.Content.Rift.RiftReward(3, 0, 0), false);
            Assert.That(MailText.Body(upgrade, guilds: Rules), Is.EqualTo("Построено: «Частокол, уровень 3». Ваш вклад — 2000 очков"));
        }

        [Test]
        public void BrokenSitesResourcesAndRewardsAreRejected()
        {
            var json = TestData.ReadGameDataFile(GuildRules.FileName);
            var biomes = TestWorld.Real.Stages.Biomes;
            var characters = new List<string>();
            foreach (var character in TestWorld.Real.Characters.All)
                characters.Add(character.Id);
            void Rejected(string from, string to, string why)
            {
                Assert.That(json, Does.Contain(from), why);
                Assert.Throws<ContentException>(() => GuildRules.Load(json.Replace(from, to), biomes, characters), why);
            }

            Rejected("\"resource\": \"wood\", \"fromStage\": 5000", "\"resource\": \"fish\", \"fromStage\": 5000", "a site cannot gather what a hall makes");
            Rejected("\"resource\": \"stone\", \"fromStage\": 7500", "\"resource\": \"stone\", \"fromStage\": 2000", "sites open in order");
            Rejected("\"resource\": \"riftdust\"", "\"resource\": \"wood\"", "raw material nobody gathers");
            Rejected("\"id\": \"salt\", \"kind\": \"raw\"", "\"id\": \"salt\", \"kind\": \"gift\"", "an unknown kind");
            Rejected("\"guild\": \"cooks\", \"name\"", "\"guild\": \"bakers\", \"name\"", "an unknown guild");
            Rejected("\"points\": 12000", "\"points\": 5000", "tiers grow");
            Rejected("\"finds\": 4, \"fish\": 2", "\"finds\": 4, \"dishes\": 2", "a hall is not built from its own goods");
            Rejected("{ \"afterBuilt\": 25,", "{ \"afterBuilt\": 400,", "a level that waits for more than the town can hold");
            Rejected("{ \"afterBuilt\": 6, \"perPlayerDay\": { \"labour\": 1200, \"stone\": 30", "{ \"afterBuilt\": 6, \"perPlayerDay\": { \"potato\": 1200, \"stone\": 30", "an unknown resource");
            Rejected("\"built\": 3 }", "\"built\": 1 }", "the looks of the town grow");
            Rejected("\"built\": 40 }", "\"built\": 99 }", "a look the town cannot reach");
            Rejected("\"master\": \"windshot\"", "\"master\": \"nobody\"", "a master who is not a character");
            Rejected("\"id\": \"well\",", "\"id\": \"well:2\",", "a building id cannot look like a level");
        }

        [Test]
        public void TheTownStartsFromATentAndTheHerbwitchAndTheAlchemistAreBuiltToo()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            Assert.That(loop.Guilds.Built("gardeners"), Is.False, "the herbwitch's house is built by the town");
            Assert.That(loop.Guilds.Built("alchemists"), Is.False, "the alchemist's shop is built by the town");
            foreach (var guild in Rules.Guilds)
                Assert.That(Rules.Building(guild.Id)!.Guild, Is.EqualTo(guild.Id), guild.Id);
            var first = new List<string>();
            foreach (var building in Rules.Buildings)
            {
                if (building.Levels[0].AfterBuilt == 0)
                {
                    first.Add(building.Id);
                    Assert.That(building.Levels[0].Needs.ContainsKey(TownResource.Wood), Is.True, building.Id + " is built from wood");
                }
                for (var level = 1; level < building.MaxLevel; level++)
                {
                    var fresh = false;
                    foreach (var need in building.Levels[level].Needs)
                        fresh |= !building.Levels[level - 1].Needs.ContainsKey(need.Key) && need.Key != TownResource.Labour;
                    Assert.That(fresh, Is.True, $"level {level + 1} of {building.Id} asks for something new");
                    Assert.That(building.Levels[level].AfterBuilt, Is.GreaterThan(0), "upgrades come after the first buildings");
                }
            }
            Assert.That(first, Does.Contain("gardeners").And.Contain("alchemists"));
            Assert.That(first.Count, Is.GreaterThan(2));
            Assert.That(Rules.Buildings.Count, Is.GreaterThan(Rules.Guilds.Count), "the town has more than guild halls");
            Assert.That(Rules.ProjectCount, Is.EqualTo(Rules.Stages[Rules.Stages.Count - 1].Built));
            Assert.That(Rules.Stages.Count, Is.GreaterThanOrEqualTo(8));
            for (var i = 0; i < 4; i++)
                Assert.That(Rules.Sites[i].FromStage, Is.GreaterThanOrEqualTo(5000), Rules.Sites[i].Id);
        }

        [Test]
        public void ProjectsAreBuildingsAndTheirLevels()
        {
            Assert.That(GuildRules.ProjectId("smiths", 1), Is.EqualTo("smiths"));
            Assert.That(GuildRules.ProjectId("smiths", 3), Is.EqualTo("smiths:3"));
            Assert.That(Rules.ProjectOf("smiths:3", out var level)!.Id, Is.EqualTo("smiths"));
            Assert.That(level, Is.EqualTo(3));
            Assert.That(Rules.ProjectOf("well", out level)!.Id, Is.EqualTo("well"));
            Assert.That(level, Is.EqualTo(1));
            Assert.That(Rules.ProjectOf("smiths:1", out _), Is.Null);
            Assert.That(Rules.ProjectOf("smiths:9", out _), Is.Null);
            Assert.That(Rules.ProjectOf("smiths:x", out _), Is.Null);
            Assert.That(Rules.ProjectOf("bakery", out _), Is.Null);
            var built = new HashSet<string> { "smiths", "smiths:2", "well", "palisade:2" };
            Assert.That(Rules.LevelOf(Rules.Building("smiths")!, built), Is.EqualTo(2));
            Assert.That(Rules.LevelOf(Rules.Building("palisade")!, built), Is.Zero, "a level without the one below does not count");
            Assert.That(Rules.CountProjects(new[] { "smiths", "smiths:2", "well", "bakery" }), Is.EqualTo(3));

            var loop = TestWorld.Loop(new FakeInputSource());
            loop.Camp.SetGuilds(built);
            Assert.That(loop.Guilds.HallLevel("smiths"), Is.EqualTo(2));
            var smiths = GuildView.Districts(loop.Guilds).Find(line => line.Id == "smith_row")!.Guilds[0];
            Assert.That(smiths.HallLevel, Is.EqualTo(2));
            Assert.That(smiths.HallMaxLevel, Is.EqualTo(3));
            var buildings = GuildView.Buildings(Rules, built);
            Assert.That(buildings.Count, Is.EqualTo(Rules.Buildings.Count));
            Assert.That(buildings.Find(line => line.Id == "smiths")!.Text, Is.EqualTo("Кузня: уровень 2 из 3"));
            Assert.That(buildings.Find(line => line.Id == "market")!.Text, Is.EqualTo("Рынок: ещё не построено"));
        }

        [Test]
        public void TheTownChangesItsLookAsItGrows()
        {
            var tent = GuildView.Stage(Rules, 0);
            Assert.That(tent.Name, Is.EqualTo("Палатка"));
            Assert.That(tent.Index, Is.Zero);
            Assert.That(tent.Text, Is.EqualTo("Кромка — Палатка: построено 0. До вида «Стоянка» ещё 1"));
            var hamlet = GuildView.Stage(Rules, 4);
            Assert.That(hamlet.Name, Is.EqualTo("Посёлок"));
            Assert.That(hamlet.Text, Is.EqualTo("Кромка — Посёлок: построено 4. До вида «Слобода» ещё 2"));
            var capital = GuildView.Stage(Rules, Rules.ProjectCount);
            Assert.That(capital.Index, Is.EqualTo(Rules.Stages.Count - 1));
            Assert.That(capital.Text, Is.EqualTo($"Кромка — Столица Кромки: построено {Rules.ProjectCount}, город отстроен"));
            var board = new TownBoard(TownBoardPhase.Vote, 5, 0, 0, 0, string.Empty, string.Empty, new[] { "gardeners", "well", "gardeners:2", "bakery" }, new TownCandidate[0], new TownStock[0]);
            Assert.That(board.BuiltCount(Rules), Is.EqualTo(3));
        }

        [Test]
        public void TheMasterOfTheBuildingStandsAtTheBoardWhileItIsBuilt()
        {
            var smiths = Rules.Building("smiths")!;
            var gather = Board(TownBoardPhase.Gather, "smiths:2", string.Empty, 0, 0, 0);
            var said = new HashSet<string>();
            for (var day = 0; day < 30; day++)
            {
                var line = GuildView.MasterSays(Rules, gather, 20400 + day);
                Assert.That(line, Does.StartWith("Golemancer у доски: «"));
                said.Add(line);
            }
            Assert.That(said.Count, Is.EqualTo(smiths.Lines.Count), "every line is heard over the days");
            Assert.That(GuildView.MasterSays(Rules, gather, 7), Is.EqualTo(GuildView.MasterSays(Rules, gather, 7)), "one line a day");
            Assert.That(GuildView.MasterSays(Rules, Board(TownBoardPhase.Opening, "well", string.Empty, 0, 0, 0), 1), Does.StartWith("Dreamcatcher у доски: «"));
            Assert.That(GuildView.MasterSays(Rules, Board(TownBoardPhase.Vote, string.Empty, string.Empty, 0, 0, 0), 1), Is.Empty);
            Assert.That(GuildView.MasterSays(Rules, Board(TownBoardPhase.Gather, "bakery", string.Empty, 0, 0, 0), 1), Is.Empty);
        }

        [Test]
        public void GuildLevelsAndHallLevelsOpenPerksForTheirCraft()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.Tick();
            Assert.That(loop.State.GuildPerks, Is.All.EqualTo(0), "no hall, no perks");
            loop.State.Town.Reputation["gardeners"] = Rules.Levels[6];
            loop.Guilds.Refresh();
            Assert.That(loop.State.Perk(MuseumPerk.GrowFaster), Is.Zero, "reputation alone does nothing while the house is not built");
            loop.Camp.SetGuilds(new[] { "gardeners" });
            loop.Guilds.Refresh();
            Assert.That(loop.State.GuildPerks[(int)MuseumPerk.GrowFaster], Is.EqualTo(0.05).Within(1e-9));
            Assert.That(loop.State.GuildPerks[(int)MuseumPerk.SeedBack], Is.EqualTo(0.04).Within(1e-9));
            Assert.That(loop.State.GuildPerks[(int)MuseumPerk.MutationChance], Is.EqualTo(0.02).Within(1e-9));
            loop.Camp.SetGuilds(new[] { "gardeners", "gardeners:2" });
            for (var i = 0; i < 10; i++)
                loop.Tick();
            Assert.That(loop.State.GuildPerks[(int)MuseumPerk.MutationChance], Is.EqualTo(0.03).Within(1e-9), "the hall level adds its own perk, refreshed by the tick");
            var lines = GuildView.Perks(loop.Guilds, "gardeners");
            Assert.That(lines.Count, Is.EqualTo(6));
            Assert.That(lines[0].Text, Is.EqualTo("Гильдия, уровень 3: Растения растут на 5% быстрее"));
            Assert.That(lines[0].Open, Is.True);
            Assert.That(lines[3].Open, Is.False, "level 10 is not reached");
            Assert.That(lines[4].Text, Is.EqualTo("Здание, уровень 2: Шанс мутации +1%"));
            Assert.That(lines[4].Open, Is.True);
            Assert.That(lines[5].Open, Is.False);
            foreach (var guild in Rules.Guilds)
            {
                Assert.That(guild.Perks.Count, Is.GreaterThan(0), guild.Id);
                foreach (var line in GuildView.Perks(loop.Guilds, guild.Id))
                    Assert.That(line.Text, Does.Not.Contain("perk."), guild.Id);
            }
        }

        [Test]
        public void TheObservatoryForecastMakesTheFishAndTheHerbOfTheDayBetter()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.Tick();
            loop.State.Town.Day = 20400;
            loop.Guilds.Refresh();
            Assert.That(loop.State.Forecast.Fish, Is.Empty, "the observatory is not open yet");
            Assert.That(loop.State.Forecast.WeightOf("anything"), Is.EqualTo(1));
            loop.State.RecordStage = TestWorld.Real.Stages.HeartFromStage;
            loop.Guilds.Refresh();
            Assert.That(loop.Guilds.ObservatoryOpen, Is.True);
            var forecast = loop.State.Forecast;
            var today = loop.Guilds.Forecast(20400);
            Assert.That(forecast.Fish, Is.EqualTo(today.Fish));
            Assert.That(forecast.Plant, Is.EqualTo(today.Plant));
            Assert.That(forecast.WeightOf(today.Fish), Is.EqualTo(Rules.Observatory.FishWeight));
            Assert.That(forecast.WeightOf("other"), Is.EqualTo(1));
            Assert.That(forecast.GrowOf(today.Plant), Is.EqualTo(Rules.Observatory.PlantGrow));
            Assert.That(forecast.MutationOf("other", today.Plant), Is.EqualTo(Rules.Observatory.PlantMutation));
            Assert.That(forecast.MutationOf("other", "other"), Is.Zero);
            Assert.That(GuildView.ForecastBonus(Rules), Is.EqualTo("Рыба дня клюёт в 3 раза чаще, трава дня растёт на 25% быстрее и мутирует с шансом +5%"));

            double Grown(bool observatory)
            {
                var garden = TestWorld.Loop(new FakeInputSource());
                garden.Tick();
                garden.State.Town.Day = 20400;
                if (observatory)
                    garden.State.RecordStage = TestWorld.Real.Stages.HeartFromStage;
                garden.Guilds.Refresh();
                garden.Camp.Add(TestWorld.Real.Garden.Plant(today.Plant)!.Seed, 1);
                Assert.That(garden.Garden.Plant(0, today.Plant), Is.EqualTo(GardenResult.Done));
                garden.Garden.Advance(10);
                return garden.Garden.State.Beds[0].Grown;
            }

            Assert.That(Grown(true), Is.EqualTo(Grown(false) * (1 + Rules.Observatory.PlantGrow)).Within(1e-6));
            loop.State.Town.Day = 20401;
            loop.Guilds.Refresh();
            Assert.That(loop.State.Forecast.Plant, Is.EqualTo(loop.Guilds.Forecast(20401).Plant), "a new day, a new forecast");
        }

        [Test]
        public void BrokenGuildPerksAreRejected()
        {
            var json = TestData.ReadGameDataFile(GuildRules.FileName);
            var biomes = TestWorld.Real.Stages.Biomes;
            var characters = new List<string>();
            foreach (var character in TestWorld.Real.Characters.All)
                characters.Add(character.Id);
            void Rejected(string from, string to, string why)
            {
                Assert.That(json, Does.Contain(from), why);
                Assert.Throws<ContentException>(() => GuildRules.Load(json.Replace(from, to), biomes, characters), why);
            }

            Rejected("{ \"level\": 3, \"perk\": \"growFaster\"", "{ \"level\": 3, \"hall\": 2, \"perk\": \"growFaster\"", "a perk opens one way");
            Rejected("{ \"level\": 3, \"perk\": \"growFaster\"", "{ \"level\": 3, \"perk\": \"flyFaster\"", "an unknown perk");
            Rejected("{ \"level\": 3, \"perk\": \"growFaster\", \"value\": 0.05 }", "{ \"level\": 3, \"perk\": \"growFaster\", \"value\": 0.3 }", "too much of one perk");
            Rejected("{ \"hall\": 3, \"perk\": \"seedBack\"", "{ \"hall\": 5, \"perk\": \"seedBack\"", "a hall level that does not exist");
            Rejected("{ \"level\": 3, \"perk\": \"growFaster\"", "{ \"level\": 11, \"perk\": \"growFaster\"", "a guild level that does not exist");
            Rejected("\"fishWeight\": 3", "\"fishWeight\": 0.5", "the fish of the day is not rarer");
        }

        [Test]
        public void TheMasterGivesATrialFightThatLeavesTheHeroAlone()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            loop.Tick();
            Assert.That(loop.StartMasterTrial("smiths"), Is.EqualTo(MasterTrialResult.NotInTown));
            loop.Peaceful.Enter(PeacefulPlace.Town);
            loop.Tick();
            Assert.That(loop.StartMasterTrial("smiths"), Is.EqualTo(MasterTrialResult.NotBuilt));
            Assert.That(loop.StartMasterTrial("bakery"), Is.EqualTo(MasterTrialResult.NoMaster));
            loop.Camp.SetGuilds(new[] { "smiths" });
            var heroClass = loop.State.Class;
            var kills = loop.State.Kills;
            var actions = loop.Guilds.UnsentActions;
            Assert.That(loop.StartMasterTrial("smiths"), Is.EqualTo(MasterTrialResult.Started));
            Assert.That(loop.StartMasterTrial("smiths"), Is.EqualTo(MasterTrialResult.Busy));
            var trial = loop.MasterTrial;
            Assert.That(trial.Master, Is.EqualTo(HeroClass.Golemancer));
            Assert.That(trial.SecondsLeft, Is.EqualTo(Rules.Trial.Seconds).Within(1e-9));
            Assert.That(GuildView.TrialOffer(Rules, "smiths"), Is.EqualTo("Golemancer предлагает пробный бой своим классом на 2 мин: ваши клавиши и мышь бьют за него, герой и сейв не меняются"));
            for (var i = 0; i < 100; i++)
            {
                input.PressKeys(4);
                loop.Tick();
            }
            Assert.That(trial.Damage.Sign, Is.GreaterThan(0), "your keys fight for the master's class");
            Assert.That(GuildView.TrialStatus(trial), Does.StartWith("Пробный бой за Golemancer: осталось 1 мин"));
            Assert.That(loop.State.Kills, Is.EqualTo(kills), "the hero does not fight");
            Assert.That(loop.State.Class, Is.EqualTo(heroClass));
            Assert.That(loop.Guilds.UnsentActions, Is.EqualTo(actions), "the keys go to the trial, not to the town work");
            loop.Events.Clear();
            for (var i = 0; i < Rules.Trial.Seconds * GameLoop.TicksPerSecond; i++)
                loop.Tick();
            Assert.That(trial.Active, Is.False);
            Assert.That(loop.Events.CountOf(GameEventKind.MasterTrialEnded), Is.EqualTo(1));
            Assert.That(GuildView.TrialEnded(3), Is.EqualTo("Пробный бой окончен: убито 3"));
            input.PressKeys(5);
            loop.Tick();
            Assert.That(loop.Guilds.UnsentActions, Is.GreaterThan(actions), "after the trial the keys work for the town again");

            Assert.That(loop.StartMasterTrial("smiths"), Is.EqualTo(MasterTrialResult.Started));
            loop.Events.Clear();
            loop.Peaceful.Leave();
            loop.Tick();
            Assert.That(trial.Active, Is.False, "leaving the town ends the trial");
            Assert.That(loop.Events.CountOf(GameEventKind.MasterTrialEnded), Is.EqualTo(1));
            Assert.That(GuildView.TrialResult(MasterTrialResult.NotBuilt), Is.EqualTo("Мастер ещё ждёт своего здания"));
        }

        [Test]
        public void TheWeeklyRebuildIsShownOnTheBoard()
        {
            Assert.That(Rules.Decorations.Count, Is.GreaterThanOrEqualTo(4));
            Assert.That(Rules.WeeklyMinLabour, Is.GreaterThan(0));
            foreach (var decor in Rules.Decorations)
                Assert.That(TestWorld.Real.Cosmetics.TryGet(decor.Frame, out _), Is.True, decor.Frame);
            var gathering = new TownWeek(1, 60000, 12000, false, 1000, 3000, false, "lanterns", new string[0]);
            Assert.That(GuildView.Week(Rules, gathering), Is.EqualTo("Отстройка недели: 12000 из 60000 мирных действий, ваш вклад 1000 (для рамки нужно 3000). Награда городу — «Фонари на столбах»"));
            var enough = new TownWeek(1, 60000, 12000, false, 3000, 3000, false, "lanterns", new string[0]);
            Assert.That(GuildView.Week(Rules, enough), Does.Contain("рамка будет ваша"));
            var done = new TownWeek(1, 60000, 61000, true, 3000, 3000, true, "banners", new[] { "lanterns", "banners" });
            Assert.That(GuildView.Week(Rules, done), Is.EqualTo("Отстройка недели завершена: «Флаги над Воротами» украшают Кромку, рамка — ваша"));
            Assert.That(GuildView.Decorations(Rules, done), Is.EqualTo(new[] { "Фонари на столбах", "Флаги над Воротами" }));
            Assert.That(GuildView.Week(Rules, TownWeek.None), Is.Empty);
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
    
        [Test]
        public void TheBoardNamesTheRolesOfTheRoundAndHidesNamesWithoutConsent()
        {
            Texts.UseLanguage("ru");
            var board = new TownBoard(TownBoardPhase.Gather, 2, 0, 0, 0, string.Empty, string.Empty, System.Array.Empty<string>(), System.Array.Empty<TownCandidate>(), System.Array.Empty<TownStock>())
                .WithRoles(new[] { new TownRole("feeder", "Cid", 4), new TownRole("logger", string.Empty, 10) });
            var lines = GuildView.Roles(board);
            Assert.That(lines[0], Is.EqualTo(Texts.Format("town.role.feeder", "Cid", 4)));
            Assert.That(lines[1], Is.EqualTo(Texts.Format("town.role.logger", Texts.Get("town.top.hidden"), 10)));
            Assert.That(PartyChronicleText.Record("supply", 0, "Ann", string.Empty, 0), Does.Contain("Ann").And.Contain("припас"));
        }
}
}
