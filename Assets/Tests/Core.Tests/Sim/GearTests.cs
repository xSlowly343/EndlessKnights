using Game.Core.Clock;
using Game.Core.Content.Items;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class GearTests
    {
        private static ItemCatalog Items => TestWorld.Data.Items;

        private static GearItem Item(string baseId, params (string Affix, int Tier)[] affixes)
        {
            Assert.That(Items.TryGetBase(baseId, out var gearBase), Is.True);
            var rolled = new System.Collections.Generic.List<GearAffix>();
            foreach (var (id, tier) in affixes)
            {
                Assert.That(Items.TryGetAffix(id, out var def), Is.True);
                rolled.Add(new GearAffix(def, tier, def.ValueAt(tier, Items.Tiers)));
            }
            return new GearItem(gearBase, ItemRarity.Rare, 6, rolled);
        }

        private static GameLoop Loop(params GearItem[] bag)
        {
            var loop = TestWorld.Loop();
            loop.State.Drops.Gear.AddRange(bag);
            return loop;
        }

        [Test]
        public void WornItemAddsItsImplicitAndAffixesToTheHero()
        {
            var helmet = Item("mail_coif", ("armor_damage", 6));
            var loop = Loop(helmet);
            var before = loop.Hero.Value(CombatStats.Damage, 100);

            Assert.That(loop.Gear.Equip(helmet), Is.True);

            Assert.That(loop.Hero.Value(CombatStats.Damage, 100) / before, Is.EqualTo(1.18).Within(1e-9));
            Assert.That(loop.Hero.Value(CombatStats.RedGemEffect, 1), Is.EqualTo(1.04).Within(1e-9));
            Assert.That(loop.Gear.In(GearSlot.Helmet), Is.SameAs(helmet));
            Assert.That(loop.State.Drops.Gear, Has.No.Member(helmet));

            Assert.That(loop.Gear.Unequip(GearSlot.Helmet), Is.True);
            Assert.That(loop.Hero.Value(CombatStats.RedGemEffect, 1), Is.EqualTo(1).Within(1e-9));
            Assert.That(loop.State.Drops.Gear, Does.Contain(helmet));
        }

        [Test]
        public void EquipSwapsWithTheWornItem()
        {
            var first = Item("mail_coif");
            var second = Item("leather_hood");
            var loop = Loop(first, second);

            loop.Gear.Equip(first);
            loop.Gear.Equip(second);

            Assert.That(loop.Gear.In(GearSlot.Helmet), Is.SameAs(second));
            Assert.That(loop.State.Drops.Gear, Does.Contain(first));
            Assert.That(loop.Hero.Value(CombatStats.RedGemEffect, 1), Is.EqualTo(1).Within(1e-9));
            Assert.That(loop.Hero.Value(CombatStats.GreenGemEffect, 1), Is.EqualTo(1.04).Within(1e-9));
        }

        [Test]
        public void TwoRingsFillBothRingSlots()
        {
            var first = Item("ruby_ring");
            var second = Item("ruby_ring");
            var loop = Loop(first, second);

            loop.Gear.Equip(first);
            loop.Gear.Equip(second);

            Assert.That(loop.Gear.In(GearSlot.RingLeft), Is.SameAs(first));
            Assert.That(loop.Gear.In(GearSlot.RingRight), Is.SameAs(second));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1, StatTags.Keyboard), Is.GreaterThanOrEqualTo(1.08 - 1e-9));
        }

        [Test]
        public void BlanksAndWrongSlotsCannotBeWorn()
        {
            var blank = new GearItem(ItemSlot.Helmet, ItemRarity.Rare, 1, "red");
            var boots = Item("plate_boots");
            var loop = Loop(blank, boots);

            Assert.That(loop.Gear.Equip(GearSlot.Helmet, blank), Is.False);
            Assert.That(loop.Gear.Equip(GearSlot.Helmet, boots), Is.False);
            Assert.That(loop.Gear.Equip(GearSlot.Boots, boots), Is.True);
        }

        [Test]
        public void ItemsFromTheBagCanOnlyBeWornOnce()
        {
            var helmet = Item("mail_coif");
            var loop = Loop();

            Assert.That(loop.Gear.Equip(helmet), Is.False);
        }

        [Test]
        public void WornAndBaggedItemsSurviveSaveAndLoad()
        {
            var helmet = Item("mail_coif", ("armor_damage", 4), ("armor_strength", 2));
            var ring = Item("gold_ring", ("jewel_gold", 6));
            var loop = Loop(helmet, ring);
            loop.Gear.Equip(helmet);

            var loaded = TestWorld.Loop(state: SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State);

            var worn = loaded.Gear.In(GearSlot.Helmet)!;
            Assert.That(worn.Base!.Id, Is.EqualTo("mail_coif"));
            Assert.That(worn.Affixes.Count, Is.EqualTo(2));
            Assert.That(worn.Affixes[0].Tier, Is.EqualTo(4));
            Assert.That(worn.Affixes[0].Value, Is.EqualTo(helmet.Affixes[0].Value).Within(1e-12));
            Assert.That(loaded.State.Drops.Gear.Count, Is.EqualTo(1));
            Assert.That(loaded.State.Drops.Gear[0].Base!.Id, Is.EqualTo("gold_ring"));
            Assert.That(loaded.Hero.Value(CombatStats.RedGemEffect, 1), Is.EqualTo(1.04).Within(1e-9));
        }

        [Test]
        public void RedGemEffectMakesRedSkillsStronger()
        {
            var helmet = Item("mail_coif");
            var loop = TestWorld.Loop(state: TestWorld.WithAllWeaponPlacesOpen(new GameState { Class = HeroClass.Viking }));
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            var gem = TestWorld.Data.Gems["iron_rim_bash"];
            TestWorld.ArmFor(loop, gem);
            var slot = loop.Skills.Equip(gem);
            loop.State.Drops.Gear.Add(helmet);
            loop.Gear.Equip(helmet);
            loop.State.Resource = 100;

            loop.Skills.TryCast(slot);

            Assert.That(loop.State.Skills[slot].CastPower, Is.EqualTo(1.04).Within(1e-9));
        }
    }
}
