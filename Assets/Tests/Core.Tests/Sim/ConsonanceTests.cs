using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ConsonanceTests
    {
        private static ItemCatalog Items => TestWorld.Data.Items;

        private static GameLoop Runedancer() =>
            TestWorld.Loop(null, new GameState(7) { Class = HeroClass.Runedancer });

        private static void Collect(GameLoop loop, string theme, ItemRarity rarity)
        {
            var set = Items.SetOf(theme, rarity)!;
            foreach (var piece in set.Pieces)
                loop.State.SetCollection.Add(SetDef.PieceKey(set.Id, piece));
            loop.Gear.Rebuild();
        }

        [Test]
        public void EveryPairNamesAnArmourAndAJewelryLineInAllRarities()
        {
            Assert.That(Items.Consonances.Count, Is.EqualTo(23));
            foreach (var consonance in Items.Consonances)
            {
                for (var rarity = ItemRarity.Rare; rarity <= ItemRarity.Rift; rarity++)
                {
                    Assert.That(Items.SetOf(consonance.Armor, rarity)?.Jewelry, Is.False, consonance.Armor + rarity);
                    Assert.That(Items.SetOf(consonance.Jewelry, rarity)?.Jewelry, Is.True, consonance.Jewelry + rarity);
                }
            }
        }

        [Test]
        public void APairOfTheSameRarityOpensTheConsonance()
        {
            var loop = Runedancer();
            Collect(loop, "red", ItemRarity.Rare);
            var before = loop.Hero.Value(CombatStats.RedGemEffect, 1);
            Collect(loop, "three_fires", ItemRarity.Epic);
            Assert.That(loop.Gear.Consonant, Is.Empty, "different rarities do not sound together");

            Collect(loop, "red", ItemRarity.Epic);
            Assert.That(loop.Gear.Consonant.Count, Is.EqualTo(1));
            var withEpicPair = loop.Hero.Value(CombatStats.RedGemEffect, 1);
            Collect(loop, "three_fires", ItemRarity.Rare);

            Assert.That(loop.Gear.Consonant.Count, Is.EqualTo(2));
            Assert.That(loop.Hero.Value(CombatStats.RedGemEffect, 1) - withEpicPair, Is.EqualTo(0.03 + Items.SetOf("three_fires", ItemRarity.Rare)!.Bonus[0].Value).Within(1e-9));
            Assert.That(withEpicPair, Is.GreaterThan(before));
        }

        [Test]
        public void TheNightRestPairCountsTheWholeOfflineAsNight()
        {
            var loop = Runedancer();
            loop.Tick();
            Assert.That(loop.State.ConditionShares[(int)ModifierCondition.Night], Is.EqualTo(0.5));
            Collect(loop, "clock_shift", ItemRarity.Rare);
            Collect(loop, "night_rest", ItemRarity.Rare);
            loop.Tick();
            Assert.That(loop.State.ConditionShares[(int)ModifierCondition.Night], Is.EqualTo(1));
            Assert.That(loop.State.ConditionShares[(int)ModifierCondition.Day], Is.Zero);
        }

        [Test]
        public void TheGoldVeinPairAddsAFourthStakeThatPaysDamageAndGold()
        {
            var loop = Runedancer();
            loop.State.StartedUnixMs = 99;
            Collect(loop, "fate_stake", ItemRarity.Rare);
            Collect(loop, "gold_vein", ItemRarity.Rare);
            Assert.That(loop.State.FateOptions, Is.EqualTo(4));
            var day = Enumerable.Range(0, 400).First(d => GameLoop.FateOf(99, d, 4) == 3);

            loop.SetLocalDay(day);
            loop.Tick();

            var on = loop.Hero.Conditions;
            Assert.That(on & ModifierConditions.Bit(ModifierCondition.FateDamage), Is.Not.Zero);
            Assert.That(on & ModifierConditions.Bit(ModifierCondition.FateGold), Is.Not.Zero);
        }

        [Test]
        public void TheMasterSignetRaisesThePassivesOfItsPeacefulPair()
        {
            var loop = Runedancer();
            Collect(loop, "forge_apprentice", ItemRarity.Rare);
            var perk = Items.SetOf("forge_apprentice", ItemRarity.Rare)!.Perks[0];
            var alone = loop.State.Perk(perk.Perk);
            Collect(loop, "master_signet", ItemRarity.Rare);
            Assert.That(loop.State.Perk(perk.Perk) - alone, Is.EqualTo(perk.Value * 0.03).Within(1e-9));
        }

        [Test]
        public void TheScholarPairDoublesGemExperienceDuringTheSecondWind()
        {
            var loop = Runedancer();
            Collect(loop, "echo_legacy", ItemRarity.Rare);
            Collect(loop, "scholar_gem", ItemRarity.Rare);
            loop.State.RecordStage = 100;
            loop.State.Stage = 100;
            loop.Tick();
            var atRecord = loop.Hero.Value(CombatStats.GemExperience, 1);
            loop.State.Stage = 10;
            loop.Tick();
            Assert.That(loop.Hero.Value(CombatStats.GemExperience, 1) - atRecord, Is.EqualTo(1).Within(1e-9));
        }
    }
}
