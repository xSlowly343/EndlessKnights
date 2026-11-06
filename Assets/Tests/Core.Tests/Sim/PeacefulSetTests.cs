using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class PeacefulSetTests
    {
        private static ItemCatalog Items => TestWorld.Data.Items;

        private static void Collect(GameLoop loop, string setId)
        {
            Assert.That(Items.TryGetSet(setId, out var set), Is.True, setId);
            foreach (var piece in set.Pieces)
                loop.Gear.NoteFound(set, piece.Slot, piece.SecondRing);
        }

        [Test]
        public void ThirtySixPeacefulSetsCarryOnlyPassivesForPeacefulWork()
        {
            var peaceful = Items.Sets.Where(s => s.Peaceful).ToList();
            Assert.That(peaceful.Count, Is.EqualTo(36));
            Assert.That(peaceful.GroupBy(s => s.Rarity).All(g => g.Count() == 6), Is.True);
            foreach (var set in peaceful)
            {
                Assert.That(set.Bonus, Is.Empty, set.Id + " gives no battle power");
                Assert.That(set.Pieces.All(p => set.PieceLines(p.Slot, p.SecondRing).Count == 0), Is.True, set.Id + " pieces carry no set lines");
                Assert.That(set.WeaponTypes.Count, Is.EqualTo(18), set.Id);
            }
        }

        [Test]
        public void ACollectedPeacefulSetWorksWithoutWearingAnything()
        {
            var loop = TestWorld.Loop();
            Assert.That(loop.State.Perk(MuseumPerk.BiteFaster), Is.Zero);
            Collect(loop, "fishing_guild_rare");
            Assert.That(loop.State.Perk(MuseumPerk.BiteFaster), Is.EqualTo(0.05).Within(1e-12));
            Assert.That(loop.State.Perk(MuseumPerk.HookWindow), Is.EqualTo(0.5).Within(1e-12));
            Collect(loop, "fishing_guild_epic");
            Assert.That(loop.State.Perk(MuseumPerk.BiteFaster), Is.EqualTo(0.05 + 0.075).Within(1e-12), "sets of one line add up");
            Assert.That(loop.State.Gear.Worn.All(w => w == null), Is.True);
        }

        [Test]
        public void PeacefulPassivesReachEveryActivity()
        {
            var loop = TestWorld.Loop();
            var hammers = loop.Forge.HammersNeeded;
            var zone = loop.Forge.ZoneTo - loop.Forge.ZoneFrom;
            var reputation = TestWorld.Data.Town.Reputation;
            Collect(loop, "forge_apprentice_rift");
            Collect(loop, "pot_master_rift");
            Collect(loop, "wanderers_rift");
            Assert.That(loop.Forge.HammersNeeded, Is.LessThan(hammers));
            Assert.That(loop.Forge.ZoneTo - loop.Forge.ZoneFrom, Is.GreaterThan(zone));
            Assert.That(loop.Forge.SmeltYield(ItemRarity.Rare), Is.GreaterThan(TestWorld.Data.Forge.SmeltBase + TestWorld.Data.Forge.SmeltPerRarity * (int)ItemRarity.Rare));
            var dish = TestWorld.Data.Cooking.Dish("fish_soup")!;
            Assert.That(loop.Cooking.ChopsOf(dish), Is.LessThan(dish.Chops));
            Assert.That(loop.Cooking.SecondsOf(dish), Is.GreaterThan(dish.Seconds));
            loop.Expeditions.Send("camp_dog", "green_hills", 4);
            Assert.That(loop.State.Expeditions.Active[0].Seconds, Is.LessThan(4 * ExpeditionSystem.SecondsPerHour), "trips are shorter");
            loop.Expeditions.Advance(loop.State.Expeditions.Active[0].Seconds / 2);
            Assert.That(loop.State.Expeditions.Active[0].LetterSent, Is.True, "the letter still comes half way through the shorter trip");
            Assert.That(reputation, Is.GreaterThan(0));
        }

        [TestCase(@"""perk"": ""hookWindow"", ""value"": 0.5}", @"""perk"": ""luck"", ""value"": 0.5}", "unknown peaceful perk 'luck'")]
        public void BrokenPeacefulSetIsReported(string from, string to, string message)
        {
            var json = Content.TestData.ReadGameDataFile(ItemCatalog.FileName).Replace(from, to);
            Assert.That(json, Is.Not.EqualTo(Content.TestData.ReadGameDataFile(ItemCatalog.FileName)));
            var error = Assert.Throws<ContentException>(() => ItemCatalog.Load(json))!;
            Assert.That(error.Message, Does.Contain(message));
        }
    }
}
