using System.Linq;
using Game.Core.Content.Items;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CombatSetLineTests
    {
        private static readonly string[] Lines = { "windmill", "hundred_strikes", "ringing_click", "colossus_hunters", "legion", "rift_depths" };

        private static ItemCatalog Items => TestWorld.Data.Items;

        private static GameLoop Runedancer() =>
            TestWorld.Loop(state: new GameState(7) { Class = HeroClass.Runedancer });

        [Test]
        public void SixNewCombatLinesHaveASetInEveryRarityWithFivePieces()
        {
            foreach (var line in Lines)
            {
                var sets = Items.Sets.Where(s => s.Theme == line).ToList();
                Assert.That(sets.Select(s => s.Rarity), Is.EquivalentTo(new[] { ItemRarity.Rare, ItemRarity.Epic, ItemRarity.Legendary, ItemRarity.Mythic, ItemRarity.Ancient, ItemRarity.Rift }), line);
                foreach (var set in sets)
                {
                    Assert.That(set.Slots, Is.EquivalentTo(new[] { ItemSlot.Weapon, ItemSlot.Helmet, ItemSlot.Chest, ItemSlot.Gloves, ItemSlot.Boots }), set.Id);
                    Assert.That(set.WeaponTypes, Is.Not.Empty, set.Id);
                    Assert.That(set.Bonus, Is.Not.Empty, set.Id);
                    Assert.That(set.Peaceful || set.Jewelry || set.Event != null, Is.False, set.Id);
                }
                var rare = sets.Single(s => s.Rarity == ItemRarity.Rare).Bonus[0].Value;
                var rift = sets.Single(s => s.Rarity == ItemRarity.Rift).Bonus[0].Value;
                Assert.That(rift / rare, Is.EqualTo(System.Math.Pow(1.5, 5)).Within(1e-2), line);
            }
            Assert.That(Items.Sets.Single(s => s.Id == "rift_depths_rift").WeaponTypes.Count, Is.EqualTo(TestWorld.Data.Weapons.Types.Count));
        }

        [Test]
        public void RiftDepthsWorksOnlyInTheRiftAndDungeons()
        {
            var loop = Runedancer();
            var set = Items.Sets.Single(s => s.Id == "rift_depths_rare");
            foreach (var piece in set.Pieces)
                loop.State.SetCollection.Add(SetDef.PieceKey(set.Id, piece));
            loop.Gear.Rebuild();
            loop.Tick();
            var outside = loop.Hero.Value(CombatStats.Damage, 100);

            loop.State.Rift.Inside = true;
            loop.Tick();

            Assert.That(loop.Hero.Value(CombatStats.Damage, 100) - outside, Is.EqualTo(10).Within(1e-9));
            Assert.That(loop.State.ConditionShares[(int)ModifierCondition.InRift], Is.Zero, "offline never counts the rift share");
        }

        [Test]
        public void ColossusHuntersHitBossesAndGuardiansHarder()
        {
            var loop = Runedancer();
            var before = loop.Hero.Value(CombatStats.DamageVsGuardian, 1);
            var set = Items.Sets.Single(s => s.Id == "colossus_hunters_rare");
            foreach (var piece in set.Pieces)
                loop.State.SetCollection.Add(SetDef.PieceKey(set.Id, piece));
            loop.Gear.Rebuild();

            Assert.That(loop.Hero.Value(CombatStats.DamageVsGuardian, 1) - before, Is.EqualTo(0.06).Within(1e-9));
            Assert.That(loop.Hero.Value(CombatStats.DamageVsBoss, 1), Is.GreaterThan(1));
        }
    }
}
