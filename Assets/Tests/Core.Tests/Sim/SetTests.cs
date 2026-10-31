using System.Collections.Generic;
using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class SetTests
    {
        private static ItemCatalog Items => TestWorld.Data.Items;

        private static SetDef Set(string id)
        {
            Assert.That(Items.TryGetSet(id, out var set), Is.True, id);
            return set;
        }

        private static GameLoop Viking() =>
            TestWorld.Loop(state: new GameState(7) { Class = HeroClass.Viking });

        [Test]
        public void ThirtySixSetsSixThemesInEachOfSixRarities()
        {
            Assert.That(Items.Sets.Count, Is.EqualTo(36));
            var rarities = new[] { ItemRarity.Rare, ItemRarity.Epic, ItemRarity.Legendary, ItemRarity.Mythic, ItemRarity.Ancient, ItemRarity.Rift };
            foreach (var rarity in rarities)
                Assert.That(Items.Sets.Where(s => s.Rarity == rarity).Select(s => s.Theme), Is.EquivalentTo(new[] { "red", "green", "blue", "onslaught", "mainstay", "decay" }), rarity.ToString());
            foreach (var set in Items.Sets)
            {
                Assert.That(set.Slots, Is.EquivalentTo(new[] { ItemSlot.Weapon, ItemSlot.Helmet, ItemSlot.Chest, ItemSlot.Gloves, ItemSlot.Boots }), set.Id);
                Assert.That(set.WeaponTypes, Is.Not.Empty, set.Id);
                Assert.That(set.Bonus, Is.Not.Empty, set.Id);
            }
            Assert.That(Set("jarl_forge").Color, Is.EqualTo("red"));
            Assert.That(Set("jarl_forge").Rarity, Is.EqualTo(ItemRarity.Rare));
            Assert.That(Set("onslaught").Rarity, Is.EqualTo(ItemRarity.Rare));
        }

        [Test]
        public void EveryRarityStepMakesTheSetHalfAgainStronger()
        {
            var rare = Set("jarl_forge").Bonus[0].Value;
            var epic = Set("mountain_hammer").Bonus[0].Value;
            var rift = Set("underside_forge").Bonus[0].Value;

            Assert.That(epic / rare, Is.EqualTo(1.5).Within(1e-3));
            Assert.That(rift / rare, Is.EqualTo(System.Math.Pow(1.5, 5)).Within(1e-2));
        }

        [Test]
        public void DropsPickASetOfTheirOwnRarity()
        {
            var rng = new Rng(5);
            for (var i = 0; i < 50; i++)
            {
                Assert.That(Items.PickSet(ItemSlot.Helmet, ItemRarity.Mythic, rng)!.Rarity, Is.EqualTo(ItemRarity.Mythic));
                Assert.That(Items.PickSet(ItemSlot.Helmet, ItemRarity.Rare, rng)!.Rarity, Is.EqualTo(ItemRarity.Rare));
            }
            Assert.That(Items.PickSet(ItemSlot.Helmet, ItemRarity.Uncommon, rng), Is.Null);
        }

        [Test]
        public void CraftingGivesAPieceThatIsNotYetInTheCollectionAndTheCostGrows()
        {
            var loop = Viking();
            loop.State.Drops.Ingots = 1000;
            var set = Set("jarl_forge");
            var costs = new List<double>();
            var slots = new HashSet<ItemSlot>();

            for (var i = 0; i < 5; i++)
            {
                costs.Add(loop.Drops.CraftCost(set));
                var gearBefore = loop.State.Drops.Gear.Count;
                Assert.That(loop.CraftSetPiece("jarl_forge"), Is.EqualTo(CraftResult.Crafted));
                slots.Add(loop.State.Drops.Gear.Count > gearBefore ? loop.State.Drops.Gear[loop.State.Drops.Gear.Count - 1].Slot : ItemSlot.Weapon);
            }

            Assert.That(costs, Is.EqualTo(new[] { 10.0, 15, 20, 30, 40 }));
            Assert.That(slots.Count, Is.EqualTo(5));
            Assert.That(loop.State.Weapons.Bag.Last().Set, Is.SameAs(set));
            Assert.That(loop.State.Drops.Ingots, Is.EqualTo(1000 - 115));
            Assert.That(loop.Gear.Collected(set), Is.True);
            Assert.That(loop.Drops.CraftCost(set), Is.EqualTo(10));
        }

        [Test]
        public void ACollectedSetCraftsThePieceThePlayerChooses()
        {
            var loop = Viking();
            loop.State.Drops.Ingots = 100;
            foreach (var slot in Set("onslaught").Slots)
                loop.State.SetCollection.Add(SetDef.PieceKey("onslaught", slot));

            Assert.That(loop.CraftSetPiece("onslaught"), Is.EqualTo(CraftResult.NeedsSlot));
            Assert.That(loop.CraftSetPiece("onslaught", ItemSlot.Ring), Is.EqualTo(CraftResult.WrongSlot));
            Assert.That(loop.CraftSetPiece("onslaught", ItemSlot.Gloves), Is.EqualTo(CraftResult.Crafted));

            var piece = loop.State.Drops.Gear.Last();
            Assert.That(piece.Slot, Is.EqualTo(ItemSlot.Gloves));
            Assert.That(piece.Set!.Id, Is.EqualTo("onslaught"));
            Assert.That(piece.Rarity, Is.EqualTo(ItemRarity.Rare));
            Assert.That(loop.State.Drops.Ingots, Is.EqualTo(90));
        }

        [Test]
        public void CraftingNeedsIngotsAndAKnownSet()
        {
            var loop = Viking();
            loop.State.Drops.Ingots = 5;

            Assert.That(loop.CraftSetPiece("no_such_set"), Is.EqualTo(CraftResult.UnknownSet));
            Assert.That(loop.CraftSetPiece("jarl_forge"), Is.EqualTo(CraftResult.NotEnoughIngots));
            Assert.That(loop.State.Drops.Ingots, Is.EqualTo(5));
        }

        [Test]
        public void WornSetPieceAddsItsSetLine()
        {
            var loop = Viking();
            var piece = Items.RollSetPiece(Set("jarl_forge"), ItemSlot.Chest, null, ItemRarity.Rare, 1, new Rng(1));
            Assert.That(piece.Base!.Color, Is.EqualTo("red"));
            loop.State.Drops.Gear.Add(piece);
            var before = loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard);

            loop.Gear.Equip(piece);

            var expected = new List<Modifier>();
            piece.AddModifiers(expected);
            Assert.That(expected, Does.Contain(Set("jarl_forge").PieceLines(ItemSlot.Chest)[0]));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.GreaterThan(before));
        }

        [Test]
        public void CollectedSetGivesItsBonusForeverEvenUnworn()
        {
            var loop = Viking();
            var set = Set("jarl_forge");
            var before = loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard);
            var rng = new Rng(3);

            var armour = set.Slots.Where(slot => slot != ItemSlot.Weapon).ToList();
            foreach (var slot in armour)
                Assert.That(loop.Gear.NoteFound(Items.RollSetPiece(set, slot, null, ItemRarity.Rare, 1, rng)), Is.True);
            Assert.That(loop.Gear.Collected(set), Is.False);
            Assert.That(loop.Gear.CollectedPieces(set), Is.EqualTo(4));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(before).Within(1e-9));

            Assert.That(loop.Gear.NoteFound(set, ItemSlot.Weapon), Is.True);

            Assert.That(loop.Gear.Collected(set), Is.True);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(before * 1.08).Within(1e-9));
            Assert.That(loop.Gear.NoteFound(Items.RollSetPiece(set, armour[0], null, ItemRarity.Rare, 1, rng)), Is.False);
        }

        [Test]
        public void GuardiansDropSetPiecesThatEnterTheCollection()
        {
            var loop = Viking();
            var sets = 0;

            for (var i = 0; i < 400; i++)
            {
                loop.Events.Clear();
                loop.Drops.OnBossDefeated(50, EnemyType.Guardian, 50, raisesRecord: true);
                loop.Drops.Flush();
                for (var e = 0; e < loop.Events.Count; e++)
                {
                    var drop = loop.Events[e].Drop;
                    if (drop?.Gear?.Set != null)
                        sets++;
                }
                loop.State.Drops.Gear.Clear();
                loop.State.Weapons.Bag.Clear();
            }

            Assert.That(sets, Is.GreaterThan(0));
            Assert.That(loop.State.SetCollection, Is.Not.Empty);
        }

        [Test]
        public void DroppedArmourIsARealItem()
        {
            var loop = Viking();
            loop.Events.Clear();
            loop.Drops.OnBossDefeated(3, EnemyType.Boss, 3, raisesRecord: true);
            loop.Drops.Flush();

            var helmet = loop.State.Drops.Gear.Single(item => item.Slot == ItemSlot.Helmet);
            Assert.That(helmet.Wearable, Is.True);
            Assert.That(helmet.Base!.Color, Is.EqualTo("red"));
            Assert.That(helmet.Affixes.Count, Is.EqualTo(Items.RuleOf(helmet.Rarity).Affixes));
            Assert.That(loop.Gear.Equip(helmet), Is.True);
        }

        [Test]
        public void StageFiftyGuaranteeFillsAnEmptyGearSlot()
        {
            var loop = Viking();
            loop.Weapons.Equip(WeaponHand.Left, loop.Weapons.Give(TestWorld.Data.Weapons.StarterFor(HeroClass.Ronin)));
            var rng = new Rng(9);
            foreach (var slot in new[] { GearSlot.Helmet, GearSlot.Chest, GearSlot.Gloves })
            {
                var item = Items.Roll((ItemSlot)((int)slot + 1), "red", ItemRarity.Rare, 1, rng);
                loop.State.Drops.Gear.Add(item);
                loop.Gear.Equip(slot, item);
            }
            loop.Events.Clear();

            loop.Drops.OnBossDefeated(50, EnemyType.Guardian, 50, raisesRecord: true);
            loop.Drops.Flush();

            var guarantee = Enumerable.Range(0, loop.Events.Count).Select(i => loop.Events[i].Drop).Single(d => d != null && d.Source == DropSource.Guarantee)!;
            Assert.That(guarantee.Slot, Is.EqualTo(ItemSlot.Boots));
        }

        [Test]
        public void SetPiecesAndTheCollectionSurviveSaveAndLoad()
        {
            var loop = Viking();
            var piece = Items.RollSetPiece(Set("onslaught"), ItemSlot.Gloves, "blue", ItemRarity.Rare, 2, new Rng(4));
            loop.State.Drops.Gear.Add(piece);
            loop.Gear.NoteFound(piece);
            loop.Gear.Equip(piece);

            var loaded = TestWorld.Loop(state: SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State);

            Assert.That(loaded.Gear.In(GearSlot.Gloves)!.Set!.Id, Is.EqualTo("onslaught"));
            Assert.That(loaded.State.SetCollection, Does.Contain(SetDef.PieceKey("onslaught", ItemSlot.Gloves)));
        }
    }
}
