using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Sim.Rift;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim.Rift
{
    public class RiftForgeTests
    {
        private static readonly long Inside = new DateTimeOffset(2026, 10, 27, 13, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        private static Game.Core.Content.Rift.RiftForgeRules Rules => TestWorld.Data.Rift.Forge;

        private static GameLoop InRift()
        {
            var loop = TestWorld.Loop();
            Assert.That(loop.Rift.Enter(Inside), Is.EqualTo(RiftEntry.Entered));
            return loop;
        }

        private static ItemDrop RareDrop()
        {
            Assert.That(TestWorld.Data.Items.TryGetBase("mail_coif", out var coif), Is.True);
            return new ItemDrop(DropSource.Record, 10, null, new GearItem(coif, ItemRarity.Rare, 3, Array.Empty<GearAffix>()), true, 0);
        }

        [Test]
        public void BossesGuardiansAndDepthMilestonesGiveSparksOnlyInside()
        {
            var home = TestWorld.Loop();
            home.Rift.OnBossDefeated(EnemyType.Boss, 1);
            Assert.That(home.Rift.Sparks, Is.Zero, "no sparks at home");

            var loop = InRift();
            loop.State.Stage = 5;
            loop.Rift.OnBossDefeated(EnemyType.Boss, 4);
            Assert.That(loop.Rift.Sparks, Is.EqualTo(Rules.BossSparks));
            loop.Rift.OnBossDefeated(EnemyType.Guardian, 4);
            Assert.That(loop.Rift.Sparks, Is.EqualTo(Rules.BossSparks + Rules.GuardianSparks));

            var before = loop.Rift.Sparks;
            loop.State.Stage = Rules.MilestoneEvery * 2 + 1;
            loop.Rift.OnBossDefeated(EnemyType.Boss, loop.State.Stage - 1);
            Assert.That(loop.Rift.Sparks - before, Is.EqualTo(Rules.BossSparks + 2 * Rules.MilestoneSparks), "two depth milestones at once");
        }

        [Test]
        public void SalvageInsideGivesSparksInsteadOfOreForTheHomeBag()
        {
            var home = TestWorld.Loop();
            var bagBefore = home.State.Camp.Bag.Values.Sum();
            home.Drops.Salvaged!(RareDrop());
            Assert.That(home.State.Camp.Bag.Values.Sum(), Is.GreaterThan(bagBefore), "ore at home");

            var loop = InRift();
            var bag = loop.State.Camp.Bag.Values.Sum();
            loop.Drops.Salvaged!(RareDrop());
            Assert.That(loop.Rift.Sparks, Is.EqualTo(Rules.SalvageSparks(ItemRarity.Rare)));
            Assert.That(loop.State.Camp.Bag.Values.Sum(), Is.EqualTo(bag), "nothing reaches the home bag");
        }

        [Test]
        public void TheRiftForgeCraftsForSparksAndGrowsItsOwnMastery()
        {
            var loop = InRift();
            loop.State.Drops.Ingots = 100_000;
            loop.State.Drops.Essence = 100_000;
            loop.State.HighestStage = 1000;
            var set = TestWorld.Data.Items.Sets.First(s => s.Ordinary && s.Rarity == ItemRarity.Rare && !s.Jewelry && !s.Peaceful);
            Assert.That(loop.Drops.CraftsForSparks, Is.True);
            Assert.That(loop.Drops.CraftSetPiece(set.Id), Is.EqualTo(CraftResult.NotEnoughSparks), "home ingots do not pay inside");

            loop.State.Rift.Sparks = 1000;
            var cost = (long)System.Math.Ceiling(loop.Drops.CraftCost(set));
            Assert.That(loop.Drops.CraftSetPiece(set.Id), Is.EqualTo(CraftResult.Crafted));
            Assert.That(loop.State.Rift.Sparks, Is.EqualTo(1000 - cost));
            Assert.That(loop.State.Drops.Ingots, Is.EqualTo(100_000), "ingots untouched");
            Assert.That(loop.State.Drops.CraftSpent, Is.EqualTo(cost), "the Rift hero's own mastery grows");

            loop.Rift.Leave();
            Assert.That(loop.State.Drops.CraftSpent, Is.Zero, "home mastery did not move");
            Assert.That(loop.Drops.CraftsForSparks, Is.False);
        }

        [Test]
        public void SparksAreSavedAndBurnWithTheWindow()
        {
            var loop = InRift();
            loop.State.Rift.Sparks = 321;
            loop.State.Rift.ShopCredited = 100;
            var clock = ClockStamp.First(new ClockReading(Inside, 1000, 1));
            var restored = SaveCodec.Decode(SaveCodec.Encode(loop.State, clock), TestWorld.Data).State;
            Assert.That(restored.Rift.Sparks, Is.EqualTo(321));
            Assert.That(restored.Rift.ShopCredited, Is.EqualTo(100));

            Assert.That(loop.Rift.CloseIfEnded(loop.State.Rift.WindowEndUnixMs), Is.True);
            Assert.That(loop.State.Rift.Sparks, Is.Zero);
            Assert.That(loop.State.Rift.ShopCredited, Is.Zero);
        }

        [Test]
        public void ShopPurchasesAreCreditedOnceAndOnlyToTheirWindow()
        {
            var loop = InRift();
            var window = loop.State.Rift.WindowId;
            Assert.That(RiftShopView.Credit(loop, window, 130), Is.EqualTo(130));
            Assert.That(RiftShopView.Credit(loop, window, 130), Is.Zero, "the same purchases twice");
            Assert.That(RiftShopView.Credit(loop, window, 230), Is.EqualTo(100), "only the new purchase");
            Assert.That(RiftShopView.Credit(loop, window + 1, 999), Is.Zero, "another window's purchases");
            Assert.That(loop.Rift.Sparks, Is.EqualTo(230));
        }

        [Test]
        public void TheShopShowsItsOffersOnlyInsideTheRift()
        {
            var bought = new Dictionary<string, int> { [Rules.Shop[0].Id] = Rules.Shop[0].PerWindow };
            var home = TestWorld.Loop();
            Assert.That(RiftShopView.Open(home), Is.False);
            Assert.That(RiftShopView.Lines(home, bought, 100_000).All(line => !line.CanBuy), Is.True);

            var loop = InRift();
            var lines = RiftShopView.Lines(loop, bought, Rules.Shop[1].Price);
            Assert.That(lines.Count, Is.EqualTo(Rules.Shop.Count));
            Assert.That(lines[0].CanBuy, Is.False, "sold out for this window");
            Assert.That(lines[0].LeftThisWindow, Is.Zero);
            Assert.That(lines[1].CanBuy, Is.True);
            Assert.That(lines.All(line => !line.Name.StartsWith("rift.", StringComparison.Ordinal)), Is.True);
            Assert.That(RiftShopView.Sparks(loop), Does.Contain("0"));
        }
        [Test]
        public void TheRiftHeroKeepsItsOwnListOfFoundUniques()
        {
            var loop = TestWorld.Loop();
            loop.State.UniquesFound.Add("home_unique");
            Assert.That(loop.Rift.Enter(Inside), Is.EqualTo(RiftEntry.Entered));
            Assert.That(loop.State.UniquesFound, Is.Empty, "home finds do not open the Rift forge");
            loop.State.UniquesFound.Add("rift_unique");
            var clock = ClockStamp.First(new ClockReading(Inside, 1000, 1));
            var restored = SaveCodec.Decode(SaveCodec.Encode(loop.State, clock), TestWorld.Data).State;
            Assert.That(restored.UniquesFound, Is.EquivalentTo(new[] { "rift_unique" }));
            Assert.That(restored.Rift.OtherHero!.UniquesFound, Is.EquivalentTo(new[] { "home_unique" }));

            loop.Rift.Leave();
            Assert.That(loop.State.UniquesFound, Is.EquivalentTo(new[] { "home_unique" }), "a Rift find stays in the Rift");
        }
    }
}
