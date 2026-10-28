using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Content.Weapons;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class SetWeaponTests
    {
        private static SetDef Set(string id)
        {
            Assert.That(TestWorld.Data.Items.TryGetSet(id, out var set), Is.True, id);
            return set;
        }

        private static GameLoop Hero(HeroClass heroClass, ulong seed = 7) =>
            TestWorld.Loop(state: TestWorld.Classless(new GameState(seed) { Class = heroClass }));

        [Test]
        public void SetWeaponTypesFollowColourOrRole()
        {
            Assert.That(Set("jarl_forge").AllowsType("axe"), Is.True);
            Assert.That(Set("jarl_forge").AllowsType("short_bow"), Is.False);
            Assert.That(Set("onslaught").WeaponTypes, Is.EquivalentTo(new[] { "axe", "katana", "short_bow", "storm_wand", "scimitar" }));
            foreach (var set in TestWorld.Data.Items.Sets)
            {
                foreach (var type in set.WeaponTypes)
                    Assert.That(TestWorld.Data.Weapons.TryGetType(type, out _), Is.True, set.Id + ":" + type);
            }
        }

        [Test]
        public void CraftedSetWeaponTakesTheHeroTypeWhenItFits()
        {
            var viking = Hero(HeroClass.Viking);
            viking.State.Drops.Ingots = 1000;
            foreach (var slot in Set("jarl_forge").Slots.Where(s => s != ItemSlot.Weapon))
                viking.State.SetCollection.Add(SetDef.PieceKey("jarl_forge", slot));

            Assert.That(viking.CraftSetPiece("jarl_forge"), Is.EqualTo(CraftResult.Crafted));

            var weapon = viking.State.Weapons.Bag.Last();
            Assert.That(weapon.Type.Id, Is.EqualTo("axe"));
            Assert.That(weapon.Set!.Id, Is.EqualTo("jarl_forge"));
            Assert.That(weapon.Rarity, Is.EqualTo(ItemRarity.Rare));
            Assert.That(viking.Gear.Collected(Set("jarl_forge")), Is.True);
        }

        [Test]
        public void ForeignHeroGetsAFittingTypeAndCollectedSetsLetThePlayerChoose()
        {
            var amazon = Hero(HeroClass.Amazon);
            amazon.State.Drops.Ingots = 1000;
            foreach (var slot in Set("jarl_forge").Slots)
                amazon.State.SetCollection.Add(SetDef.PieceKey("jarl_forge", slot));

            Assert.That(amazon.CraftSetPiece("jarl_forge", ItemSlot.Weapon, "short_bow"), Is.EqualTo(CraftResult.WrongSlot));
            Assert.That(amazon.State.Drops.Ingots, Is.EqualTo(1000));
            Assert.That(amazon.CraftSetPiece("jarl_forge", ItemSlot.Weapon, "katana"), Is.EqualTo(CraftResult.Crafted));
            Assert.That(amazon.State.Weapons.Bag.Last().Type.Id, Is.EqualTo("katana"));

            Assert.That(amazon.CraftSetPiece("jarl_forge", ItemSlot.Weapon), Is.EqualTo(CraftResult.Crafted));
            Assert.That(Set("jarl_forge").AllowsType(amazon.State.Weapons.Bag.Last().Type.Id), Is.True);
        }

        [Test]
        public void HeldSetWeaponAddsItsSetLine()
        {
            var loop = Hero(HeroClass.Viking);
            var set = Set("jarl_forge");
            var weapon = loop.Weapons.Give(TestWorld.Data.Weapons.Roll(TestWorld.Data.Weapons.StarterFor(HeroClass.Viking), ItemRarity.Rare, new Rng(2)).InSet(set));
            var before = loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard);

            loop.Weapons.Equip(WeaponHand.Left, weapon);

            Assert.That(loop.Weapons.Modifiers, Does.Contain(set.PieceLines(ItemSlot.Weapon)[0]));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.GreaterThan(before));
        }

        [Test]
        public void SetWeaponSurvivesSaveAndLoad()
        {
            var loop = Hero(HeroClass.Viking);
            var weapon = loop.Weapons.Give(TestWorld.Data.Weapons.Roll(TestWorld.Data.Weapons.StarterFor(HeroClass.Viking), ItemRarity.Epic, new Rng(2)).InSet(Set("mountain_hammer")));

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State;

            Assert.That(loaded.Weapons.Bag.Single(w => w.Set != null).Set!.Id, Is.EqualTo("mountain_hammer"));
        }

        [Test]
        public void GuardiansDropSetWeaponsThatEnterTheCollection()
        {
            var json = TestData.ReadGameDataFile(DropRules.FileName).Replace("\"sets\": { \"chance\": 0.02, \"boss\": 0.04, \"guardian\": 0.08 }", "\"sets\": { \"chance\": 0.02, \"boss\": 0.04, \"guardian\": 1 }");
            var loop = Hero(HeroClass.Viking);
            loop.Drops.UseData(DropRules.Load(json), TestWorld.Data.Stages, TestWorld.Data.Gems, TestWorld.Data.Items);
            var found = 0;
            for (var i = 0; i < 40; i++)
            {
                loop.Events.Clear();
                loop.Drops.OnBossDefeated(50, EnemyType.Guardian, 50, raisesRecord: true);
                loop.Drops.Flush();
                for (var e = 0; e < loop.Events.Count; e++)
                {
                    var weapon = loop.Events[e].Drop?.Weapon;
                    if (weapon?.Set == null)
                        continue;
                    found++;
                    Assert.That(weapon.Set.AllowsType(weapon.Type.Id), Is.True);
                    Assert.That(loop.State.SetCollection, Does.Contain(SetDef.PieceKey(weapon.Set.Id, ItemSlot.Weapon)));
                }
                loop.State.Weapons.Bag.Clear();
                loop.State.Drops.Gear.Clear();
            }

            Assert.That(found, Is.GreaterThan(0));
        }
    }
}
