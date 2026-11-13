using System;
using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Content.Rift;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Ui;
using Game.Core.Text;
using NUnit.Framework;

namespace Game.Core.Tests.Sim.Rift
{
    public class RiftWorldTests
    {
        private static readonly long Inside = new DateTimeOffset(2026, 10, 27, 13, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        private static RiftRules Rules => TestWorld.Data.Rift;

        private static GameLoop InRift(long homeStage = 300)
        {
            var loop = TestWorld.Loop();
            loop.State.Stage = homeStage;
            loop.State.HighestStage = homeStage;
            loop.State.RecordStage = homeStage;
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Zero);
            loop.Stages.EnsureEnemy();
            loop.State.Echo = BigNumber.FromDouble(1e9);
            loop.Artifacts.TryUnlock();
            Assert.That(loop.Rift.Enter(Inside), Is.EqualTo(Game.Core.Sim.Rift.RiftEntry.Entered));
            loop.Events.Clear();
            return loop;
        }

        [Test]
        public void TheLadderOpensEveryFeatureByRiftStages()
        {
            Assert.That(Rules.StageOf(Feature.Artifacts), Is.EqualTo(10));
            Assert.That(Rules.StageOf(Feature.Dungeons), Is.EqualTo(25));
            Assert.That(Rules.Ladder.Count, Is.EqualTo(UnlockRules.FeatureCount));

            var loop = InRift();
            foreach (var feature in Rules.Ladder.Keys)
                Assert.That(loop.Unlocks.IsOpen(feature), Is.False, feature.ToString());
            loop.State.HighestStage = 100_000;
            foreach (var feature in Rules.Ladder.Keys)
                Assert.That(loop.Unlocks.IsOpen(feature), Is.True, feature.ToString());
        }

        [Test]
        public void HomeKeepsItsOwnUnlockRules()
        {
            var loop = TestWorld.RealLoop();
            loop.State.Stage = 300;
            loop.State.HighestStage = 300;
            loop.State.RecordStage = 300;
            loop.Tick();
            Assert.That(loop.Unlocks.IsOpen(Feature.Dungeons), Is.True, "home: stage 150 by unlocks.json");
            Assert.That(loop.Unlocks.IsOpen(Feature.SetCraft), Is.False);
            Assert.That(loop.Rift.Enter(Inside), Is.EqualTo(Game.Core.Sim.Rift.RiftEntry.Entered));
            Assert.That(loop.Unlocks.IsOpen(Feature.Dungeons), Is.False, "the Rift starts from its first stage");
            loop.Tick();
            loop.Rift.Leave();
            Assert.That(loop.Unlocks.IsOpen(Feature.Dungeons), Is.True);
            Assert.That(loop.Unlocks.IsOpen(Feature.SetCraft), Is.False);
            Assert.That(loop.State.OpenedFeatures, Does.Not.Contain(Feature.SetCraft));
        }

        [Test]
        public void ArtifactsOpenInsideOnlyByRiftStagesWhateverTheHomeRecord()
        {
            var loop = InRift();
            loop.State.Rift.Shards = BigNumber.FromDouble(1e9);
            Assert.That(loop.Unlocks.IsOpen(Feature.Artifacts), Is.False);
            Assert.That(loop.Artifacts.Open, Is.False);
            Assert.That(loop.Artifacts.TryUnlock(), Is.Null);

            loop.State.HighestStage = Rules.StageOf(Feature.Artifacts);
            Assert.That(loop.Unlocks.IsOpen(Feature.Artifacts), Is.True);
            Assert.That(loop.Artifacts.Open, Is.True);
            Assert.That(loop.Artifacts.TryUnlock(), Is.Not.Null);
        }

        [Test]
        public void DungeonKeysDropInsideOnlyFromTheLadderStage()
        {
            var loop = InRift();
            var stage = Rules.StageOf(Feature.Dungeons);
            for (var i = 0; i < 40; i++)
            {
                loop.Drops.OnBossDefeated(stage, EnemyType.Guardian, 300, raisesRecord: false);
                loop.Drops.Flush();
            }
            Assert.That(loop.Dungeons.Keys.Count, Is.Zero, "the Rift has not reached the ladder yet");

            loop.State.HighestStage = stage;
            for (var i = 0; i < 40; i++)
            {
                loop.Drops.OnBossDefeated(stage, EnemyType.Guardian, 300, raisesRecord: false);
                loop.Drops.Flush();
            }
            Assert.That(loop.Dungeons.Keys.Count, Is.GreaterThan(0));
            Assert.That(loop.Dungeons.Keys.All(k => k.ForRift), Is.True);
        }

        [Test]
        public void EachStepOfTheLadderIsAnnouncedOncePerWindowAndSaved()
        {
            var loop = InRift();
            loop.State.HighestStage = Rules.StageOf(Feature.Dungeons);
            loop.Tick();
            Assert.That(loop.Events.CountOf(GameEventKind.RiftFeatureOpened), Is.EqualTo(2));
            loop.Events.Clear();
            loop.Tick();
            Assert.That(loop.Events.CountOf(GameEventKind.RiftFeatureOpened), Is.Zero);
            Assert.That(loop.State.Rift.Opened, Is.EquivalentTo(new[] { "artifacts", "dungeons" }));

            var clock = ClockStamp.First(new ClockReading(Inside, 1000, 1));
            var restored = SaveCodec.Decode(SaveCodec.Encode(loop.State, clock), TestWorld.Data).State;
            Assert.That(restored.Rift.Opened, Is.EquivalentTo(new[] { "artifacts", "dungeons" }));
        }

        [Test]
        public void TheRiftIsItsOwnWorldWithoutPeacefulPlaces()
        {
            var loop = TestWorld.Loop();
            loop.State.Stage = 300;
            loop.State.HighestStage = 300;
            loop.State.RecordStage = 300;
            loop.State.Peaceful.Mode = PeacefulMode.Camp;
            Assert.That(loop.Rift.Enter(Inside), Is.EqualTo(Game.Core.Sim.Rift.RiftEntry.Entered));

            Assert.That(loop.State.Peaceful.Mode, Is.EqualTo(PeacefulMode.Battle));
            Assert.That(loop.Peaceful.Enter(PeacefulPlace.Town), Is.Not.EqualTo(PeacefulResult.Done));
            Assert.That(loop.Peaceful.Enter(PeacefulPlace.Hideout), Is.Not.EqualTo(PeacefulResult.Done));
        }

        [Test]
        public void TheRiftHeroStartsFromNothingAndHomeComesBackWhole()
        {
            var loop = TestWorld.Loop();
            loop.Book.GrantStartingGems();
            loop.State.Stage = 300;
            loop.State.HighestStage = 300;
            loop.State.RecordStage = 300;
            loop.State.HeroLevel = 40;
            loop.State.TreePointsEarned = 30;
            loop.State.Gold = BigNumber.FromDouble(1e9);
            Assert.That(TestWorld.Data.Items.TryGetBase("mail_coif", out var coif), Is.True);
            var helmet = new GearItem(coif, ItemRarity.Rare, 3, Array.Empty<GearAffix>());
            loop.State.Drops.Gear.Add(helmet);
            Assert.That(loop.Gear.Equip(helmet), Is.True);
            var homeGems = loop.State.OwnedGems.Count;
            var homeWeapon = loop.State.Weapons.Held;

            Assert.That(loop.Rift.Enter(Inside), Is.EqualTo(Game.Core.Sim.Rift.RiftEntry.Entered));
            Assert.That(loop.State.HeroLevel, Is.EqualTo(1));
            Assert.That(loop.State.TreePointsEarned, Is.EqualTo(1), "one point for level 1, nothing from home");
            Assert.That(loop.State.Gold.IsZero, Is.True);
            Assert.That(loop.State.Gear.Worn.Where(item => item != null).All(item => item!.Set?.Id == loop.State.Rift.WindowSet), Is.True, "only the window set");
            Assert.That(loop.State.Class, Is.EqualTo(loop.State.Rift.OtherHero!.Class), "the class stays");
            Assert.That(loop.State.OwnedGems.Count, Is.LessThanOrEqualTo(homeGems), "only the starting gems");
            Assert.That(loop.State.Weapons.Held, Is.Not.Null, "a starter weapon");
            Assert.That(loop.State.Weapons.Held, Is.Not.SameAs(homeWeapon));

            loop.State.HeroLevel = 7;
            loop.State.Gold = BigNumber.FromDouble(500);
            loop.Rift.Leave();
            Assert.That(loop.State.HeroLevel, Is.EqualTo(40));
            Assert.That(loop.State.Gold.ToDouble(), Is.EqualTo(1e9));
            Assert.That(loop.State.Gear.Worn, Has.Some.Not.Null);
            Assert.That(loop.State.Weapons.Held!.Id, Is.EqualTo(homeWeapon!.Id));

            Assert.That(loop.Rift.Enter(Inside), Is.EqualTo(Game.Core.Sim.Rift.RiftEntry.Entered));
            Assert.That(loop.State.HeroLevel, Is.EqualTo(7), "the same window keeps the Rift hero");
            Assert.That(loop.State.Gold.ToDouble(), Is.EqualTo(500));
        }

        [Test]
        public void TheRiftHeroBurnsWhenTheWindowEndsAndBothHeroesAreSaved()
        {
            var loop = InRift();
            loop.State.HeroLevel = 9;
            var clock = ClockStamp.First(new ClockReading(Inside, 1000, 1));
            var restored = SaveCodec.Decode(SaveCodec.Encode(loop.State, clock), TestWorld.Data).State;
            Assert.That(restored.HeroLevel, Is.EqualTo(9));
            Assert.That(restored.Rift.OtherHero, Is.Not.Null);
            Assert.That(restored.Rift.OtherHero!.Gold.ToDouble(), Is.EqualTo(loop.State.Rift.OtherHero!.Gold.ToDouble()));

            var end = loop.State.Rift.WindowEndUnixMs;
            Assert.That(loop.Rift.CloseIfEnded(end), Is.True);
            Assert.That(loop.State.Rift.OtherHero, Is.Null);
            Assert.That(loop.State.HeroLevel, Is.Not.EqualTo(9));
        }

        [Test]
        public void PeacefulAndClanBonusesStayHome()
        {
            var loop = TestWorld.Loop();
            loop.State.CampPerks[0] = 0.5;
            loop.State.SetPerks[0] = 0.25;
            Assert.That(loop.State.Perk((MuseumPerk)0), Is.EqualTo(0.75));
            Assert.That(loop.Rift.Enter(Inside), Is.EqualTo(Game.Core.Sim.Rift.RiftEntry.Entered));
            Assert.That(loop.State.Perk((MuseumPerk)0), Is.EqualTo(loop.State.SetPerks[0]), "only what the Rift hero wears");
        }

        [Test]
        public void AGiftIsOfferedAtTheStartAndEveryThousandStages()
        {
            var loop = InRift();
            loop.Tick();
            var gifts = loop.RiftGifts;
            Assert.That(gifts.Choosing, Is.True);
            Assert.That(gifts.Offer.Count, Is.EqualTo(Rules.Gifts.Offer));
            Assert.That(gifts.Offer.Distinct().Count(), Is.EqualTo(Rules.Gifts.Offer));
            Assert.That(loop.Events.CountOf(GameEventKind.RiftGiftOffered), Is.EqualTo(1));
            Assert.That(gifts.Choose("not_a_gift"), Is.EqualTo(Game.Core.Sim.Rift.RiftGiftResult.NotOffered));

            Assert.That(gifts.Choose("keen_edge") == Game.Core.Sim.Rift.RiftGiftResult.Taken || gifts.Choose(gifts.Offer[0]) == Game.Core.Sim.Rift.RiftGiftResult.Taken, Is.True);
            Assert.That(gifts.Taken.Count, Is.EqualTo(1));
            loop.Tick();
            Assert.That(gifts.Choosing, Is.False, "the next one waits for stage 1000");

            loop.State.HighestStage = Rules.Gifts.Every;
            loop.Tick();
            Assert.That(gifts.Choosing, Is.True);
            Assert.That(gifts.Offer, Has.None.EqualTo(gifts.Taken[0]), "a taken gift is not offered again");
        }

        [Test]
        public void GiftsWorkOnlyInsideAndAreSaved()
        {
            var loop = InRift();
            loop.Tick();
            var before = loop.Hero.Value(CombatStats.CritChance, 0);
            loop.State.Rift.GiftOffer.Clear();
            loop.State.Rift.GiftOffer.Add("sharp_eye");
            loop.RiftGifts.Choose("sharp_eye");
            Assert.That(loop.Hero.Value(CombatStats.CritChance, 0), Is.EqualTo(before + 0.05).Within(1e-9));

            var clock = ClockStamp.First(new ClockReading(Inside, 1000, 1));
            var restored = SaveCodec.Decode(SaveCodec.Encode(loop.State, clock), TestWorld.Data).State;
            Assert.That(restored.Rift.Gifts, Is.EqualTo(new[] { "sharp_eye" }));

            loop.Rift.Leave();
            loop.Tick();
            Assert.That(loop.Hero.Value(CombatStats.CritChance, 0), Is.LessThan(before + 0.05 - 1e-9));
        }

        [Test]
        public void EachWindowDressesTheRiftHeroInItsOwnSet()
        {
            var loop = InRift();
            var setId = loop.State.Rift.WindowSet;
            Assert.That(setId, Is.Not.Empty);
            Assert.That(TestWorld.Data.Items.TryGetSet(setId, out var set), Is.True);
            Assert.That(set.Rarity, Is.EqualTo(Rules.WindowSet.Rarity));
            Assert.That(loop.State.Gear.Worn.Count(item => item != null && item.Set?.Id == setId), Is.GreaterThan(0));

            var picks = Enumerable.Range(0, 20).Select(week => Game.Core.Sim.Rift.RiftWindowSet.For(TestWorld.Data.Items, Rules, 1_000_000 + week * 7 * 86400)?.Id).Distinct().Count();
            Assert.That(picks, Is.GreaterThan(1), "windows differ");
        }

        [Test]
        public void TwelveRiftKeysOpenEveryModifierTwice()
        {
            var keys = Rules.WindowSet.Keys;
            Assert.That(keys.Count, Is.EqualTo(Rules.Modifiers.Count));
            Assert.That(keys.Values.All(sets => sets.Count == 2), Is.True);
            var riftOnly = TestWorld.Data.Items.Sets.Where(set => set.RiftOnly).ToList();
            Assert.That(riftOnly.Count, Is.EqualTo(12));
            Assert.That(riftOnly.All(set => set.Rarity == Rules.WindowSet.Rarity), Is.True);
            Assert.That(riftOnly.All(set => set.Pieces.Count == 4), Is.True, "four armour pieces, no weapon");
            Assert.That(riftOnly.SelectMany(set => set.Bonus).All(line => line.When != ModifierCondition.Always), Is.True, "every bonus line is conditional");
            Assert.That(Rules.WindowSet.SoftenShare, Is.EqualTo(0.5));
        }

        [Test]
        public void TheWindowKeyIsPickedLikeOnTheServer()
        {
            Assert.That(Rules.KeyedModifier(1761570000)!.Id, Is.EqualTo("eclipse"));
            Assert.That(Rules.KeyedModifier(1761915600)!.Id, Is.EqualTo("regeneration"));
            Assert.That(Rules.KeyedModifier(1762174800)!.Id, Is.EqualTo("silence"));
            Assert.That(Rules.KeyedModifier(1762520400)!.Id, Is.EqualTo("colossus_shield"));
            Assert.That(Rules.KeyedModifier(1762779600)!.Id, Is.EqualTo("colossus_shield"));
        }

        [Test]
        public void TheWindowSetIsAKeyToOneOfTheWindowModifiers()
        {
            var loop = InRift();
            Assert.That(TestWorld.Data.Items.TryGetSet(loop.State.Rift.WindowSet, out var set), Is.True);
            Assert.That(set.RiftOnly, Is.True);
            var keyed = loop.Rift.KeyedModifier;
            Assert.That(keyed, Is.Not.Null);
            Assert.That(loop.Rift.WindowModifiers, Has.Member(keyed));
            Assert.That(Rules.WindowSet.KeysOf(keyed!.Id), Has.Member(set.Id));
            Assert.That(loop.State.Gear.Worn.Count(item => item?.Set == set), Is.EqualTo(4));
            Assert.That(loop.Gear.Collected(set), Is.True, "the set goes into the Rift hero's collection");
            Assert.That(RiftLoadingText.Lines(loop), Has.Some.Contains(Texts.Get("rift.key." + set.Id)));

            var week = Enumerable.Range(0, 30).Select(i => Game.Core.Sim.Rift.RiftWindowSet.For(TestWorld.Data.Items, Rules, 1_761_570_000L + i * 3 * 86400)!.Id).Distinct().Count();
            Assert.That(week, Is.GreaterThan(4), "windows hand out different keys");
        }

        [Test]
        public void RiftSetsNeverDropNorCraftAndNeverReachTheHomeCollection()
        {
            var items = TestWorld.Data.Items;
            var rng = new Rng(7);
            foreach (var slot in new[] { ItemSlot.Helmet, ItemSlot.Chest, ItemSlot.Gloves, ItemSlot.Boots })
            {
                for (var i = 0; i < 300; i++)
                    Assert.That(items.PickSet(slot, ItemRarity.Rift, rng)?.RiftOnly ?? false, Is.False);
            }
            var key = items.Sets.First(set => set.RiftOnly);
            Assert.That(items.SetOf(key.Theme!, key.Rarity), Is.Null);
            var home = TestWorld.Loop();
            Assert.That(home.Drops.CraftSetPiece(key.Id), Is.EqualTo(CraftResult.NotCraftable));

            var loop = InRift();
            Assert.That(loop.Drops.CraftSetPiece(loop.State.Rift.WindowSet), Is.EqualTo(CraftResult.NotCraftable));
            loop.Rift.Leave();
            Assert.That(loop.State.SetCollection.Any(piece => piece.StartsWith("rift_key_", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public void TheHomeCollectionAndForgeMasteryStayHome()
        {
            var loop = TestWorld.Loop();
            var home = TestWorld.Data.Items.Sets.First(set => set.Ordinary && !set.Peaceful && set.Rarity == ItemRarity.Rift && !set.Jewelry);
            foreach (var piece in home.Pieces)
                loop.State.SetCollection.Add(SetDef.PieceKey(home.Id, piece));
            loop.State.Drops.CraftSpent = 1_000_000;
            loop.Gear.Rebuild();
            Assert.That(loop.Gear.Collected(home), Is.True);
            var homeMastery = loop.Gear.MasteryLevel;
            Assert.That(homeMastery, Is.GreaterThan(TestWorld.Data.Items.MasteryLevel(0)));

            Assert.That(loop.Rift.Enter(Inside), Is.EqualTo(Game.Core.Sim.Rift.RiftEntry.Entered));
            Assert.That(loop.Gear.Collected(home), Is.False, "home sets do not work in the Rift");
            Assert.That(loop.Gear.MasteryLevel, Is.EqualTo(TestWorld.Data.Items.MasteryLevel(0)), "the Rift forge has its own mastery");
            Assert.That(loop.State.SetCollection.All(piece => piece.StartsWith(loop.State.Rift.WindowSet, StringComparison.Ordinal)), Is.True);

            loop.Rift.Leave();
            Assert.That(loop.Gear.Collected(home), Is.True);
            Assert.That(loop.Gear.MasteryLevel, Is.EqualTo(homeMastery));
            var clock = ClockStamp.First(new ClockReading(Inside, 1000, 1));
            Assert.That(loop.Rift.Enter(Inside), Is.EqualTo(Game.Core.Sim.Rift.RiftEntry.Entered));
            var restored = SaveCodec.Decode(SaveCodec.Encode(loop.State, clock), TestWorld.Data).State;
            Assert.That(restored.Rift.OtherHero!.SetCollection.Count, Is.EqualTo(home.Pieces.Count));
            Assert.That(restored.Rift.OtherHero!.Drops.CraftSpent, Is.EqualTo(1_000_000));
        }

        [Test]
        public void TheKeySoftensItsModifierByHalf()
        {
            var loop = InRift();
            var keyed = loop.Rift.KeyedModifier!;
            var index = loop.Rift.WindowModifiers.ToList().IndexOf(keyed);
            Assume.That(loop.Rift.ModifierActive(index), Is.True);
            Assert.That(loop.Rift.KeyHeld, Is.True);
            foreach (var modifier in keyed.Modifiers)
                Assert.That(loop.Rift.Modifiers, Has.Member(modifier.WithValue(modifier.Value * 0.5)));

            loop.State.SetCollection.Clear();
            loop.Rift.Refresh();
            Assert.That(loop.Rift.KeyHeld, Is.False);
            foreach (var modifier in keyed.Modifiers)
                Assert.That(loop.Rift.Modifiers, Has.Member(modifier));
        }

        [Test]
        public void SoloAndShieldedBossAreConditions()
        {
            var loop = TestWorld.Loop();
            loop.Tick();
            Assert.That((loop.Hero.Conditions & ModifierConditions.Bit(ModifierCondition.Solo)) != 0, Is.True);
            loop.State.PartyAllies = 1;
            loop.Tick();
            Assert.That((loop.Hero.Conditions & ModifierConditions.Bit(ModifierCondition.Solo)) != 0, Is.False);

            loop.State.PartyAllies = 0;
            loop.State.EnemyHp = BigNumber.FromDouble(150);
            loop.State.EnemyShieldFloor = BigNumber.FromDouble(100);
            Assert.That((loop.Conditions.Current() & ModifierConditions.Bit(ModifierCondition.BossShielded)) != 0, Is.True);
            loop.State.EnemyHp = BigNumber.FromDouble(90);
            Assert.That((loop.Conditions.Current() & ModifierConditions.Bit(ModifierCondition.BossShielded)) != 0, Is.False);
        }

        [Test]
        public void TheRiftKeepsTheBiomesButShufflesTheirEnemies()
        {
            var enemies = TestWorld.Data.Enemies;
            var a = enemies.Shuffled(11);
            var b = enemies.Shuffled(12);
            var normals = enemies.All.Where(enemy => enemy.Type == EnemyType.Normal).Select(enemy => enemy.Id).ToHashSet();
            var changed = false;
            foreach (var biome in TestWorld.Data.Stages.Biomes)
            {
                Assert.That(a.Normals(biome).Count, Is.EqualTo(enemies.Normals(biome).Count), biome);
                Assert.That(a.Normals(biome).All(enemy => normals.Contains(enemy.Id)), Is.True, "only existing enemies");
                Assert.That(a.Boss(biome), Is.SameAs(enemies.Boss(biome)), "bosses stay");
                changed |= !a.Normals(biome).Select(e => e.Id).SequenceEqual(b.Normals(biome).Select(e => e.Id));
            }
            Assert.That(changed, Is.True);
            Assert.That(enemies.Shuffled(11).Normals(TestWorld.Data.Stages.Biomes[0]).Select(e => e.Id), Is.EqualTo(a.Normals(TestWorld.Data.Stages.Biomes[0]).Select(e => e.Id)), "the same window, the same enemies");
        }

        [Test]
        public void AnUnknownOrRepeatedLadderStepIsRefused()
        {
            Assert.Throws<ContentException>(() => RiftRulesWith("[{ \"feature\": \"wings\", \"stage\": 5 }]"));
            Assert.Throws<ContentException>(() => RiftRulesWith("[{ \"feature\": \"artifacts\", \"stage\": 5 }, { \"feature\": \"artifacts\", \"stage\": 9 }]"));
            Assert.Throws<ContentException>(() => RiftRulesWith("[{ \"feature\": \"artifacts\", \"stage\": 5 }]"), "every feature must be on the ladder");
        }

        private static RiftRules RiftRulesWith(string ladder)
        {
            var text = Game.Core.Tests.Content.TestData.ReadGameDataFile(RiftRules.FileName);
            var start = text.IndexOf("\"ladder\"", StringComparison.Ordinal);
            var end = text.IndexOf(']', start) + 1;
            return RiftRules.Load(text.Substring(0, start) + "\"ladder\": " + ladder + text.Substring(end));
        }
    }
}
