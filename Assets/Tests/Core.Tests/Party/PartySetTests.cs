using System.Collections.Generic;
using System.Linq;
using Game.Core.Content.Items;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartySetTests
    {
        private const long Day = 86_400_000;
        private const long Now = 1_763_000_000_000;

        private static ItemCatalog Items => TestWorld.Data.Items;

        private static SetDef Set(string line, string kind, ItemRarity rarity) => Items.Sets.Single(s => s.Id == PartySetReader.IdOf(line, kind, rarity));

        private static GameLoop Member()
        {
            var loop = TestWorld.Loop();
            loop.SetPartyStanding(true, Now - 10 * Day, 4, Now);
            return loop;
        }

        [Test]
        public void TwelveLinesOfArmourAndJewelryWithFourPiecesEach()
        {
            var party = Items.Sets.Where(s => s.Party).ToList();
            Assert.That(party.Count, Is.EqualTo(11 * 8 * 2 + 3 * 2));
            Assert.That(party.Select(s => s.PartyLine).Distinct().Count(), Is.EqualTo(12));
            foreach (var set in party)
            {
                Assert.That(set.Pieces.Count, Is.EqualTo(4), set.Id);
                Assert.That(set.Ordinary, Is.False, "party sets do not drop or craft like personal ones");
                Assert.That(set.Bonus, Is.Empty, "no personal bonus");
            }
            var jewelry = Set("common_pot", "jewelry", ItemRarity.Rare);
            Assert.That(jewelry.HasSecondRing && jewelry.Has(ItemSlot.Amulet) && jewelry.Has(ItemSlot.Weapon), Is.True);
            Assert.That(jewelry.AllowsType("axe"), Is.True, "the weapon piece fits any class");
            Assert.That(Set("common_pot", "armor", ItemRarity.Rare).Has(ItemSlot.Boots), Is.True);
            Assert.That(Items.Sets.Any(s => s.Id == PartySetReader.IdOf("primal_flame", "armor", ItemRarity.Mythic)), Is.False, "the primal flame starts from rift");
        }

        [Test]
        public void RarerSetsHaveMoreLinesAndMultipliers()
        {
            Assert.That(Set("eternal_hunters", "armor", ItemRarity.Rare).PartyBonus.Count, Is.EqualTo(1));
            Assert.That(Set("eternal_hunters", "armor", ItemRarity.Epic).PartyBonus.Count, Is.EqualTo(2));
            var mythic = Set("eternal_hunters", "armor", ItemRarity.Mythic).PartyBonus;
            Assert.That(mythic.Count, Is.EqualTo(3));
            Assert.That(mythic.Count(m => m.Kind == ModifierKind.More), Is.EqualTo(1));
            Assert.That(Set("eternal_hunters", "armor", ItemRarity.Rift).PartyBonus.Count, Is.EqualTo(4));
            var astral = Set("eternal_hunters", "armor", ItemRarity.Astral).PartyBonus;
            Assert.That(astral.Count(m => m.Kind == ModifierKind.More), Is.EqualTo(2));
            Assert.That(Set("eternal_hunters", "armor", ItemRarity.Primordial).PartyBonus.Count, Is.EqualTo(6));
            Assert.That(Set("eternal_hunters", "armor", ItemRarity.Primordial).PartyBonus[0].Value, Is.EqualTo(0.28));
        }

        [Test]
        public void CollectedPartySetsGiveNoPersonalBonusButAreListedForTheParty()
        {
            var loop = TestWorld.Loop();
            var before = loop.Hero.Value(CombatStats.Damage, 1);
            var set = Set("standard_bearers", "armor", ItemRarity.Mythic);
            foreach (var piece in set.Pieces)
                loop.State.SetCollection.Add(SetDef.PieceKey(set.Id, piece));
            loop.Gear.Rebuild();
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(before).Within(1e-9));
            Assert.That(loop.CollectedPartySets(), Is.EqualTo(new[] { set.Id }));
        }

        [Test]
        public void EverySetCollectedByAMemberBuffsThePartyAndCopiesStack()
        {
            var loop = Member();
            var damage = loop.Hero.Value(CombatStats.Damage, 1);
            var id = PartySetReader.IdOf("standard_bearers", "armor", ItemRarity.Mythic);
            loop.SetPartySets(new Dictionary<string, int> { [id] = 1 });
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1) / damage, Is.EqualTo(1.14 * 1.03).Within(1e-9));
            loop.SetPartySets(new Dictionary<string, int> { [id] = 3 });
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1) / damage, Is.EqualTo((1 + 0.14 * 3) * System.Math.Pow(1.03, 3)).Within(1e-9), "three members, three copies");
            var colossus = PartySetReader.IdOf("eternal_hunters", "armor", ItemRarity.Rare);
            loop.SetPartySets(new Dictionary<string, int> { [colossus] = 2 });
            Assert.That(loop.PartyGrowth.ColossusPower, Is.EqualTo(1.1).Within(1e-12));
            loop.SetPartyStanding(false, 0, 0, Now);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(damage).Within(1e-9));
        }

        [Test]
        public void ThePreviewAndWindowShowPartySets()
        {
            Game.Core.Text.Texts.UseLanguage("ru");
            var loop = Member();
            var id = PartySetReader.IdOf("standard_bearers", "armor", ItemRarity.Rare);
            var gain = PartyGrowthPreview.Of(loop.PartyGrowth, PartyUpgrade.PartySet(id));
            Assert.That(gain.After.Damage, Is.EqualTo(1.05).Within(1e-9));
            loop.SetPartySets(new Dictionary<string, int> { [id] = 2 });
            var lines = Game.Core.Ui.PartyGrowthView.Window(loop.PartyGrowth);
            Assert.That(lines.Any(line => line.Label == "Наборы отряда" && line.Value == "×1.1"), Is.True);
        }

        [Test]
        public void ServerDropsAndCraftsBecomeRealPiecesAndFillTheCollection()
        {
            var loop = TestWorld.Loop();
            var set = Set("common_pot", "jewelry", ItemRarity.Epic);
            var room = loop.BagRoom;
            Assert.That(loop.GivePartySetPiece(set.Id, "amulet"), Is.True);
            Assert.That(loop.GivePartySetPiece(set.Id, "ring"), Is.True);
            Assert.That(loop.GivePartySetPiece(set.Id, "ring_right"), Is.True);
            Assert.That(loop.BagRoom, Is.EqualTo(room - 3));
            Assert.That(loop.CollectedPartySets(), Is.Empty);
            Assert.That(loop.GivePartySetPiece(set.Id, "weapon"), Is.True, "the weapon piece fits any class");
            Assert.That(loop.CollectedPartySets(), Is.EqualTo(new[] { set.Id }));
            Assert.That(loop.GivePartySetPiece(set.Id, "boots"), Is.False, "a jewelry set has no boots");
            Assert.That(loop.GivePartySetPiece("dragon", "ring"), Is.False);
        }

        [Test]
        public void DropSourcesComeFromTheData()
        {
            var drops = Items.PartySetDrops;
            Assert.That(drops.Keys, Is.SupersetOf(new[] { "battle", "orders", "mascot", "trials", "colossus", "banner", "extreme", "dungeons", "rift" }));
            Assert.That(drops["dungeons"].Chance, Is.EqualTo(0.01));
            Assert.That(drops["colossus"].Roll(50, 0), Is.EqualTo(ItemRarity.Rift));
            Assert.That(drops["colossus"].Roll(50, 0.9999), Is.EqualTo(ItemRarity.Primordial));
            Assert.That(drops["colossus"].Roll(5, 0), Is.EqualTo(ItemRarity.Legendary));
            Assert.That(Set("peace_guild", "armor", ItemRarity.Rare).PartySource, Is.EqualTo("orders"), "peaceful lines drop from orders");
            Assert.That(Set("deep_key", "armor", ItemRarity.Rare).PartySource, Is.EqualTo("dungeons"));
        }
    }
}
