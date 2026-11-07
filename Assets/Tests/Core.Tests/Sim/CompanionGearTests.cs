using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionGearTests
    {
        private static GameLoop Camp(bool forge = true)
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            loop.SetLocalDay(100);
            if (forge)
                loop.State.Camp.Tier = 1;
            loop.State.Drops.Shards = 100_000;
            loop.Camp.Add("bone_hills", 100);
            return loop;
        }

        [Test]
        public void TheForgeMakesGearFromShardsAndBones()
        {
            var loop = Camp(false);
            Assert.That(loop.CompanionGear.Craft(CompanionGearSlot.Collar, 1, "green_hills"), Is.EqualTo(GearResult.NoForge));
            loop.State.Camp.Tier = 1;
            Assert.That(loop.CompanionGear.Craft(CompanionGearSlot.Collar, 7, "green_hills"), Is.EqualTo(GearResult.BadRarity));
            Assert.That(loop.CompanionGear.Craft(CompanionGearSlot.Collar, 3, "green_hills"), Is.EqualTo(GearResult.Done));
            Assert.That(loop.State.Drops.Shards, Is.EqualTo(100_000 - 120));
            Assert.That(loop.Camp.Count("bone_hills"), Is.EqualTo(94));
            var item = loop.CompanionGear.Items[0];
            Assert.That(item.Affixes.Count, Is.EqualTo(3));
            Assert.That(item.Affixes.TrueForAll(affix => affix.Value > 0), Is.True);
        }

        [Test]
        public void SlotsOpenWithTheYoungsterAndStars()
        {
            var loop = Camp();
            loop.CompanionGear.Craft(CompanionGearSlot.Collar, 1, "green_hills");
            loop.CompanionGear.Craft(CompanionGearSlot.Bag, 1, "green_hills");
            var collar = loop.CompanionGear.Items[0].Id;
            var bag = loop.CompanionGear.Items[1].Id;
            Assert.That(loop.CompanionGear.Equip("camp_dog", collar), Is.EqualTo(GearResult.SlotLocked));
            loop.Companions.Progress("camp_dog").Stage = 1;
            Assert.That(loop.CompanionGear.Equip("camp_dog", collar), Is.EqualTo(GearResult.Done));
            Assert.That(loop.CompanionGear.Equip("camp_dog", bag), Is.EqualTo(GearResult.SlotLocked));
            loop.Companions.Progress("camp_crow").Stage = 1;
            Assert.That(loop.CompanionGear.Equip("camp_crow", collar), Is.EqualTo(GearResult.Done), "gear moves freely between companions");
            Assert.That(loop.CompanionGear.Worn("camp_dog", CompanionGearSlot.Collar), Is.Null);
        }

        [Test]
        public void AffixesAndSetsWork()
        {
            var loop = Camp();
            var dog = loop.Companions.Progress("camp_dog");
            dog.Stage = 3;
            dog.Stars = 6;
            loop.CompanionGear.Craft(CompanionGearSlot.Collar, 1, "green_hills", "trail");
            loop.CompanionGear.Craft(CompanionGearSlot.Bag, 1, "green_hills", "trail");
            loop.CompanionGear.Craft(CompanionGearSlot.Amulet, 1, "green_hills", "trail");
            var before = loop.Companions.TravelMultiplier("camp_dog", TravelStat.Items);
            foreach (var item in loop.CompanionGear.Items)
                loop.CompanionGear.Equip("camp_dog", item.Id);
            Assert.That(loop.CompanionGear.Pieces("camp_dog", "trail"), Is.EqualTo(3));
            Assert.That(loop.Companions.TravelMultiplier("camp_dog", TravelStat.Items), Is.GreaterThanOrEqualTo(before * 1.1 - 1e-9));
            Assert.That(loop.CompanionGear.HasPower("camp_dog", CompanionSetPower.InstantTrip), Is.True);
            loop.Expeditions.Send("camp_dog", "green_hills", 8);
            Assert.That(loop.CompanionGear.InstantReturn("camp_dog"), Is.EqualTo(GearResult.Done));
            Assert.That(loop.Expeditions.Busy("camp_dog"), Is.False);
            Assert.That(loop.State.Expeditions.Reports.Count, Is.EqualTo(1));
            loop.Expeditions.Send("camp_dog", "green_hills", 8);
            Assert.That(loop.CompanionGear.InstantReturn("camp_dog"), Is.EqualTo(GearResult.Used), "once a day");
        }

        [Test]
        public void GearSurvivesTheSave()
        {
            var loop = Camp();
            loop.Companions.Progress("camp_dog").Stage = 1;
            loop.CompanionGear.Craft(CompanionGearSlot.Collar, 4, "green_hills", "home");
            loop.CompanionGear.Equip("camp_dog", loop.CompanionGear.Items[0].Id);
            var copy = Game.Core.Save.GameStateJson.Read(new ContentNode(Game.Core.Save.GameStateJson.Write(loop.State), "save", string.Empty),
                TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            var item = copy.Companions.Gear[0];
            Assert.That(item.Owner, Is.EqualTo("camp_dog"));
            Assert.That(item.Set, Is.EqualTo("home"));
            Assert.That(item.Rarity, Is.EqualTo(4));
            Assert.That(item.Affixes.Count, Is.EqualTo(loop.CompanionGear.Items[0].Affixes.Count));
            Assert.That(copy.Companions.NextGearId, Is.EqualTo(2));
        }
    }
}
