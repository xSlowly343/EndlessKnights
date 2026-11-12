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
    public class CampTests
    {
        [Test]
        public void TiersOpenWithTheRecordAndTheirOwnConditions()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.Tick();
            Assert.That(loop.Camp.Tier.Id, Is.EqualTo("camp_site"));
            Assert.That(loop.Camp.Slots("garden_bed"), Is.EqualTo(4));
            Assert.That(loop.Camp.Slots("forge"), Is.Zero);

            loop.State.RecordStage = 1000;
            loop.Tick();
            Assert.That(loop.Camp.Tier.Id, Is.EqualTo("camp_site"), "the record alone is not enough, the bonfire has to grow");

            loop.State.Camp.Levels["bonfire"] = 2;
            loop.Tick();
            Assert.That(loop.Camp.Tier.Id, Is.EqualTo("camp"));
            Assert.That(loop.Events.CountOf(GameEventKind.CampTierReached), Is.EqualTo(1));
            Assert.That(loop.Camp.Slots("garden_bed"), Is.EqualTo(8));

            loop.State.RecordStage = 10;
            loop.Tick();
            Assert.That(loop.Camp.Tier.Id, Is.EqualTo("camp"), "a tier once reached stays after a prestige");

            loop.State.RecordStage = 5000;
            loop.Tick();
            Assert.That(loop.Camp.Tier.Id, Is.EqualTo("camp"), "the outpost waits for a tier 6 dungeon");
            loop.State.Dungeons.BestTier = 6;
            loop.Tick();
            Assert.That(loop.Camp.Tier.Id, Is.EqualTo("outpost"));
            loop.State.Chronicle.Completed.Add("only_mouse");
            loop.Tick();
            Assert.That(loop.Camp.NextTier, Is.Null);
        }

        [Test]
        public void CraftBuildingsWaitForTheirGuildInTheEdge()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.State.Camp.Tier = 1;
            loop.Tick();
            Assert.That(loop.Camp.Open("garden_bed"), Is.True, "the herbalist tent at the Gates opens the garden from the start");
            Assert.That(loop.Camp.Open("cauldron"), Is.True);
            Assert.That(loop.Camp.WaitsForGuild("forge"), Is.True);
            Assert.That(loop.Camp.Open("forge"), Is.False);
            loop.Camp.SetGuilds(new[] { "smiths" });
            Assert.That(loop.Camp.Open("forge"), Is.True);

            var street = CampView.Street(loop.Camp);
            Assert.That(street.Single(l => l.Building == "pier").WaitsForGuild, Is.True);
            Assert.That(street.Single(l => l.Building == "chef_table").OpensAtTier, Is.EqualTo("Застава"));
            Assert.That(CampView.TierName(loop.Camp), Is.EqualTo("Лагерь"));
        }

        [Test]
        public void TheBagHoldsResourcesAndIsSaved()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.Camp.Add("herb_mint", 5);
            Assert.That(loop.Camp.Take("herb_mint", 6), Is.False);
            Assert.That(loop.Camp.Take("herb_mint", 2), Is.True);
            Assert.That(loop.Camp.Count("herb_mint"), Is.EqualTo(3));
            loop.Camp.SetGuilds(new[] { "fishers" });
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Camp.Bag["herb_mint"], Is.EqualTo(3));
            Assert.That(copy.Camp.Guilds, Does.Contain("fishers"));
            Assert.That(loop.Camp.Take("herb_mint", 3), Is.True);
            Assert.That(loop.State.Camp.Bag.ContainsKey("herb_mint"), Is.False);
        }

        [Test]
        public void BrokenTiersAreReported()
        {
            var json = TestData.ReadGameDataFile(CampRules.FileName);
            Assert.Throws<ContentException>(() => CampRules.Load(json.Replace("\"fromRecord\": 2500", "\"fromRecord\": 40")));
            Assert.Throws<ContentException>(() => CampRules.Load(json.Replace("\"garden_bed\": 4,", "\"greenhouse\": 4,")));
        }
    }
}
