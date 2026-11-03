using System.Collections.Generic;
using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Content.Weapons;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class AffixRerollTests
    {
        private static readonly ClockStamp Clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

        private static GameData Data => TestWorld.Real;
        private static ItemCatalog Items => Data.Items;

        private static GearAffix Affix(string id, int tier, double roll = 0)
        {
            Assert.That(Items.TryGetAffix(id, out var def), Is.True, id);
            return new GearAffix(def, tier, def.RollValue(tier, roll), null, roll);
        }

        private static GearItem Helmet(ItemRarity rarity = ItemRarity.Legendary)
        {
            Assert.That(Items.TryGetBase("mail_coif", out var gearBase), Is.True);
            return new GearItem(gearBase, rarity, 6, new[] { Affix("armor_damage", 4), Affix("armor_crit_chance", 3), Affix("helmet_resource_max", 2) });
        }

        private static GameLoop Loop(double dust, params GearItem[] bag)
        {
            var loop = TestWorld.Loop();
            loop.State.Drops.Dust = dust;
            loop.State.Drops.Gear.AddRange(bag);
            return loop;
        }

        private static void RerollUntilChanged(GameLoop loop, GearItem item, int? locked = null)
        {
            for (var i = 0; i < 50; i++)
            {
                loop.State.Drops.Dust = 1_000_000;
                Assert.That(loop.Reroll.Reroll(item, locked), Is.EqualTo(RerollResult.Offered));
                var offer = loop.Reroll.Offer!;
                if (Enumerable.Range(0, offer.Before.Count).Any(index => offer.Before[index].Value != offer.After[index].Value))
                    return;
                loop.Reroll.KeepOld();
            }
            Assert.Fail("fifty rerolls never changed a number");
        }

        [Test]
        public void TheCostIsTwiceTheSalvageDustTimesTheChoiceAndALockDoublesIt()
        {
            var rules = Data.Drops;

            Assert.That(rules.RerollCost(ItemRarity.Rare, false), Is.EqualTo(30));
            Assert.That(rules.RerollCost(ItemRarity.Legendary, false), Is.EqualTo(180));
            Assert.That(rules.RerollCost(ItemRarity.Legendary, true), Is.EqualTo(360));
            Assert.That(rules.RerollCost(ItemRarity.Rift, false), Is.EqualTo(3000));
        }

        [Test]
        public void ARerollChangesOnlyTheNumbersAndKeepsTiersAndAffixes()
        {
            var helmet = Helmet();
            var loop = Loop(0, helmet);

            RerollUntilChanged(loop, helmet);
            var offer = loop.Reroll.Offer!;

            Assert.That(offer.After.Select(a => a.Def.Id), Is.EqualTo(helmet.Affixes.Select(a => a.Def.Id)));
            Assert.That(offer.After.Select(a => a.Tier), Is.EqualTo(helmet.Affixes.Select(a => a.Tier)));
            foreach (var affix in offer.After)
            {
                Assert.That(affix.Value, Is.InRange(affix.Def.Lowest(affix.Tier), affix.Def.Highest(affix.Tier)), affix.Def.Id);
                Assert.That(affix.Value, Is.EqualTo(affix.Def.RollValue(affix.Tier, affix.Roll)).Within(1e-12));
            }
        }

        [Test]
        public void TheDustIsPaidUpFrontAndKeepingTheOldItemDoesNotReturnIt()
        {
            var helmet = Helmet();
            var loop = Loop(500, helmet);

            Assert.That(loop.Reroll.Reroll(helmet), Is.EqualTo(RerollResult.Offered));
            Assert.That(loop.State.Drops.Dust, Is.EqualTo(320).Within(1e-9));
            Assert.That(loop.Reroll.Reroll(helmet), Is.EqualTo(RerollResult.Pending), "one choice at a time");

            loop.Reroll.KeepOld();

            Assert.That(loop.Reroll.Offer, Is.Null);
            Assert.That(loop.State.Drops.Gear, Does.Contain(helmet));
            Assert.That(loop.State.Drops.Dust, Is.EqualTo(320).Within(1e-9));
        }

        [Test]
        public void TakingTheNewNumbersReplacesTheItemInTheBag()
        {
            var helmet = Helmet();
            var loop = Loop(0, helmet);
            RerollUntilChanged(loop, helmet);
            var after = loop.Reroll.Offer!.After;

            Assert.That(loop.Reroll.TakeNew(), Is.True);

            var fresh = loop.State.Drops.Gear.Single();
            Assert.That(fresh, Is.Not.SameAs(helmet));
            Assert.That(fresh.Affixes.Select(a => a.Value), Is.EqualTo(after.Select(a => a.Value)));
            Assert.That(fresh.Base, Is.SameAs(helmet.Base));
            Assert.That(fresh.Rarity, Is.EqualTo(helmet.Rarity));
        }

        [Test]
        public void TakingTheNewNumbersOnAWornItemChangesTheHero()
        {
            var helmet = new GearItem(Helmet().Base!, ItemRarity.Legendary, 6, new[] { Affix("armor_damage", 4, roll: 0) });
            var loop = Loop(0, helmet);
            Assert.That(loop.Gear.Equip(helmet), Is.True);
            var before = loop.Hero.Value(CombatStats.Damage, 100);
            RerollUntilChanged(loop, helmet);
            var damage = loop.Reroll.Offer!.After[0].Value;

            Assert.That(loop.Reroll.TakeNew(), Is.True);

            Assert.That(loop.Gear.In(GearSlot.Helmet)!.Affixes[0].Value, Is.EqualTo(damage));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100) / before, Is.EqualTo((1 + damage) / 1.19).Within(1e-9));
        }

        [Test]
        public void ALockedAffixKeepsItsNumber()
        {
            var helmet = Helmet();
            var loop = Loop(0, helmet);

            RerollUntilChanged(loop, helmet, locked: 0);

            Assert.That(loop.Reroll.Offer!.After[0], Is.SameAs(helmet.Affixes[0]));
            Assert.That(loop.Reroll.Offer!.Paid, Is.EqualTo(360));
        }

        [Test]
        public void ARerollIsRefusedWithoutDustForUniquesBadLocksAndFixedNumbers()
        {
            var helmet = Helmet();
            var unique = new GearItem(Items.Uniques.First(u => u.Slot != ItemSlot.Weapon), 6);
            Assert.That(Items.TryGetAffix("helmet_support_level", out var level), Is.True);
            var whole = new GearItem(helmet.Base!, ItemRarity.Legendary, 6, new[] { new GearAffix(level, 6, level.RollValue(6, 0)) });
            var stranger = Helmet();
            var loop = Loop(100, helmet, unique, whole);

            Assert.That(loop.Reroll.Reroll(helmet), Is.EqualTo(RerollResult.NotEnoughDust));
            loop.State.Drops.Dust = 10_000;
            Assert.That(loop.Reroll.Reroll(unique), Is.EqualTo(RerollResult.Unique));
            Assert.That(loop.Reroll.Reroll(whole), Is.EqualTo(RerollResult.NothingToRoll));
            Assert.That(loop.Reroll.Reroll(helmet, locked: 3), Is.EqualTo(RerollResult.BadLock));
            Assert.That(loop.Reroll.Reroll(stranger), Is.EqualTo(RerollResult.NotOwned));
            Assert.That(loop.Reroll.Offer, Is.Null);
            Assert.That(loop.State.Drops.Dust, Is.EqualTo(10_000));
        }

        [Test]
        public void AHeldWeaponRerollsAndTheNewNumbersCountAtOnce()
        {
            var loop = new GameLoop(Data, new FakeInputSource(), TestWorld.WithAllSkillsOpen(new GameState(3) { Class = HeroClass.Runedancer, RecordStage = 300 }), new StatSheet());
            var weapon = loop.Weapons.Give(Data.Weapons.Roll(Data.Weapons.StarterFor(HeroClass.Runedancer), ItemRarity.Legendary, 6, new[] { Affix("weapon_strike_damage", 4) }));
            Assert.That(loop.Weapons.Equip(weapon), Is.True);
            WeaponItem? fresh = null;
            for (var i = 0; i < 50 && fresh == null; i++)
            {
                loop.State.Drops.Dust = 1_000_000;
                Assert.That(loop.Reroll.Reroll(weapon), Is.EqualTo(RerollResult.Offered));
                if (loop.Reroll.Offer!.After[0].Value == weapon.Affixes[0].Value)
                {
                    loop.Reroll.KeepOld();
                    continue;
                }
                Assert.That(loop.Reroll.TakeNew(), Is.True);
                fresh = loop.Weapons.Held;
            }

            Assert.That(fresh, Is.Not.Null.And.Not.SameAs(weapon));
            Assert.That(fresh!.Affixes[0].Value, Is.Not.EqualTo(weapon.Affixes[0].Value));
            Assert.That(fresh.Id, Is.EqualTo(weapon.Id));
        }

        [Test]
        public void TheNewNumbersSurviveASave()
        {
            var helmet = Helmet();
            var loop = Loop(0, helmet);
            RerollUntilChanged(loop, helmet);
            loop.Reroll.TakeNew();
            var rolled = loop.State.Drops.Gear.Single().Affixes.Select(a => (a.Value, a.Roll)).ToList();

            var restored = SaveCodec.Decode(SaveCodec.Encode(loop.State, Clock), Data).State;

            Assert.That(restored.Drops.Gear.Single().Affixes.Select(a => (a.Value, a.Roll)), Is.EqualTo(rolled));
        }
    }
}
