using System.Collections.Generic;
using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Items;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class DropSystemTests
    {
        private static DropRules Rules => TestWorld.Data.Drops;
        private static int BiomeLength => TestWorld.Data.Stages.BiomeLength;

        private static GameLoop Viking(StatSheet? hero = null, ulong seed = 7) =>
            TestWorld.Loop(state: new GameState(seed) { Class = HeroClass.Viking }, hero: hero);

        private static List<ItemDrop> Defeat(GameLoop loop, long stage, long recordBefore, EnemyType type = EnemyType.Boss)
        {
            loop.Events.Clear();
            loop.Drops.OnBossDefeated(stage, type, recordBefore, raisesRecord: true);
            loop.Drops.Flush();
            return Items(loop);
        }

        private static List<ItemDrop> Items(GameLoop loop)
        {
            var items = new List<ItemDrop>();
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.ItemDropped)
                    items.Add(loop.Events[i].Drop!);
            }
            return items;
        }

        private static List<GemDef> Gems(GameLoop loop)
        {
            var gems = new List<GemDef>();
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.GemDropped)
                    gems.Add(loop.Events[i].Gem!);
            }
            return gems;
        }

        private static StatSheet With(StatId stat, ModifierKind kind, double value)
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(stat, kind, value));
            return hero;
        }

        [Test]
        public void FirstPassOfAStageGivesOneItem()
        {
            var loop = Viking();

            var items = Defeat(loop, 7, 7);

            Assert.That(items.Count, Is.EqualTo(1));
            Assert.That(items[0].Source, Is.EqualTo(DropSource.Record));
            Assert.That(items[0].Stage, Is.EqualTo(7));
        }

        [Test]
        public void PassingAStageAgainBelowTheDailyShareGivesNothing()
        {
            var loop = Viking();

            var items = Defeat(loop, 7, 100);

            Assert.That(items, Is.Empty);
        }

        [Test]
        public void RecordItemsNeverGoAboveRareEvenInTheLastBiome()
        {
            var loop = Viking();
            var stage = 6L * BiomeLength - 1;

            for (var i = 0; i < 500; i++)
            {
                foreach (var item in Defeat(loop, stage, stage).Where(item => item.Source == DropSource.Record))
                    Assert.That(item.Rarity, Is.LessThanOrEqualTo(ItemRarity.Rare));
            }
        }

        [Test]
        public void SpecialBossAddsAnUncommonOrBetterItemUnderTheBiomeCeiling()
        {
            var loop = Viking();
            var specials = 0;

            for (var i = 0; i < 300; i++)
            {
                var items = Defeat(loop, 30, 30);
                Assert.That(items.Count(item => item.Source == DropSource.Record), Is.EqualTo(1));
                foreach (var item in items.Where(item => item.Source == DropSource.SpecialBoss))
                {
                    specials++;
                    Assert.That(item.Rarity, Is.InRange(ItemRarity.Uncommon, ItemRarity.Rare));
                }
            }

            Assert.That(specials, Is.EqualTo(300));
        }

        [Test]
        public void GuardianGivesTwoRareOrBetterItems()
        {
            var loop = Viking();
            var stage = 2L * BiomeLength;

            for (var i = 0; i < 200; i++)
            {
                var guardian = Defeat(loop, stage, stage, EnemyType.Guardian).Where(item => item.Source == DropSource.Guardian).ToList();
                Assert.That(guardian.Count, Is.EqualTo(2));
                Assert.That(guardian.Select(item => item.Rarity), Has.All.InRange(ItemRarity.Rare, ItemRarity.Epic));
            }
        }

        [Test]
        public void RarityAboveTheBiomeCeilingMovesToTheCeiling()
        {
            var loop = Viking();
            var rng = new Rng(3);
            var table = new DropTable(1, ItemRarity.Simple, ItemRarity.Rift);
            var rolls = new int[DropRules.RarityCount];
            const int count = 40000;

            for (var i = 0; i < count; i++)
                rolls[(int)loop.Drops.RollRarity(table, 1, rng)]++;

            Assert.That(rolls.Skip((int)ItemRarity.Epic).Sum(), Is.Zero);
            Assert.That(rolls[(int)ItemRarity.Simple] / (double)count, Is.EqualTo(0.40).Within(0.015));
            Assert.That(rolls[(int)ItemRarity.Rare] / (double)count, Is.EqualTo(0.15).Within(0.01));
        }

        [Test]
        public void OnlyTheFirstFourRaritiesDropBeforeTheLimit()
        {
            Assert.That(Rules.CeilingAt(1), Is.EqualTo(ItemRarity.Rare));
            Assert.That(Rules.CeilingAt(300), Is.EqualTo(ItemRarity.Rare));
            Assert.That(Rules.CeilingAt(49999), Is.EqualTo(ItemRarity.Rare));
            Assert.That(Rules.CeilingAt(50000), Is.EqualTo(ItemRarity.Rift));
            Assert.That(Rules.CeilingAt(80000), Is.EqualTo(ItemRarity.Rift));
        }

        [Test]
        public void ItemRarityStatMakesBetterItemsMoreLikely()
        {
            var plain = Viking();
            var lucky = Viking(With(CombatStats.ItemFind, ModifierKind.Increased, 2));
            var table = new DropTable(1, ItemRarity.Simple, ItemRarity.Rift);
            var plainSimple = 0;
            var luckySimple = 0;
            var rngA = new Rng(5);
            var rngB = new Rng(5);

            for (var i = 0; i < 10000; i++)
            {
                if (plain.Drops.RollRarity(table, 60000, rngA) == ItemRarity.Simple)
                    plainSimple++;
                if (lucky.Drops.RollRarity(table, 60000, rngB) == ItemRarity.Simple)
                    luckySimple++;
            }

            Assert.That(luckySimple, Is.LessThan(plainSimple * 0.5));
        }

        [Test]
        public void DayOfBossesNeverGivesMoreThanTwentyDailyItemsOrAnyBelowEightyPercentOfTheRecord()
        {
            var loop = Viking(seed: 11);
            var rng = new Rng(42);
            const long record = 1000;

            for (var day = 1; day <= 3; day++)
            {
                loop.SetLocalDay(day);
                var daily = new List<ItemDrop>();
                for (var boss = 0; boss < 3000; boss++)
                {
                    var stage = 500 + rng.NextInt(0, (int)record - 500);
                    daily.AddRange(Defeat(loop, stage, record + 1).Where(item => item.Source == DropSource.Daily));
                }
                Assert.That(daily.Count, Is.EqualTo(Rules.Daily.Limit), "day " + day);
                Assert.That(daily.Select(item => item.Stage), Has.All.GreaterThanOrEqualTo(800), "day " + day);
                Assert.That(loop.Drops.DailyLeft, Is.Zero);
            }
        }

        [Test]
        public void DailyItemsDropWithTheConfiguredChance()
        {
            var loop = Viking(seed: 13);
            var drops = 0;
            const int bosses = 20000;

            for (var boss = 0; boss < bosses; boss++)
            {
                loop.State.Drops.DailyItems = 0;
                drops += Defeat(loop, 900, 1001).Count;
            }

            Assert.That(drops / (double)bosses, Is.EqualTo(Rules.Daily.Chance).Within(0.015));
        }

        [Test]
        public void ItemQuantityRaisesTheBossChanceButNotTheLimit()
        {
            var loop = Viking(With(CombatStats.ItemQuantity, ModifierKind.Increased, 3));
            loop.SetLocalDay(1);
            var drops = 0;

            for (var boss = 0; boss < Rules.Daily.Limit + 10; boss++)
                drops += Defeat(loop, 900, 1001).Count;

            Assert.That(drops, Is.EqualTo(Rules.Daily.Limit));
        }

        [Test]
        public void DailyLimitStatIsCappedByTheMaximum()
        {
            var loop = Viking(With(CombatStats.DailyItems, ModifierKind.Flat, 100));

            Assert.That(loop.Drops.DailyLimit, Is.EqualTo(Rules.Daily.MaxLimit));
        }

        [Test]
        public void NewDayResetsTheDailyLimit()
        {
            var loop = Viking();
            loop.SetLocalDay(10);
            loop.State.Drops.DailyItems = Rules.Daily.Limit;

            loop.SetLocalDay(10);
            Assert.That(loop.Drops.DailyLeft, Is.Zero);
            loop.SetLocalDay(11);
            Assert.That(loop.Drops.DailyLeft, Is.EqualTo(Rules.Daily.Limit));
        }

        [Test]
        public void StageThreeGivesAFineHelmetOfTheOwnColor()
        {
            var item = Defeat(Viking(), 3, 3).Single();

            Assert.That(item.Source, Is.EqualTo(DropSource.Guarantee));
            Assert.That(item.Gear!.Slot, Is.EqualTo(ItemSlot.Helmet));
            Assert.That(item.Rarity, Is.EqualTo(ItemRarity.Fine));
            Assert.That(item.Gear.Color, Is.EqualTo("red"));
        }

        [Test]
        public void StageTenGivesAnUncommonRingAndTheSpecialBossItem()
        {
            var items = Defeat(Viking(), 10, 10);

            var ring = items.Single(item => item.Source == DropSource.Guarantee);
            Assert.That(ring.Gear!.Slot, Is.EqualTo(ItemSlot.Ring));
            Assert.That(ring.Rarity, Is.EqualTo(ItemRarity.Uncommon));
            Assert.That(ring.Gear.Color, Is.EqualTo("red"));
            Assert.That(items.Count(item => item.Source == DropSource.SpecialBoss), Is.EqualTo(1));
        }

        [Test]
        public void StageTwentyGivesAnUncommonWeaponOfAnotherClassInTheOwnColor()
        {
            var loop = Viking();

            var weapon = Defeat(loop, 20, 20).Single(item => item.Source == DropSource.Guarantee).Weapon!;

            Assert.That(weapon.Rarity, Is.EqualTo(ItemRarity.Uncommon));
            Assert.That(weapon.Type.Id, Is.Not.EqualTo("axe"));
            Assert.That(weapon.Type.Color, Is.EqualTo("red"));
            Assert.That(weapon.Skills, Is.Not.Empty);
            Assert.That(loop.State.Weapons.Bag, Does.Contain(weapon));
        }

        [Test]
        public void FirstGuardianGivesARareWeaponOfTheOwnTypeForTheEmptyHand()
        {
            var loop = Viking();
            Assert.That(loop.State.Weapons.Left, Is.Null);

            var item = Defeat(loop, BiomeLength, BiomeLength, EnemyType.Guardian).Single(drop => drop.Source == DropSource.Guarantee);

            Assert.That(item.Weapon!.Type.Id, Is.EqualTo("axe"));
            Assert.That(item.Rarity, Is.EqualTo(ItemRarity.Rare));
        }

        [Test]
        public void FirstGuardianGivesRareGearWhenBothHandsHoldWeapons()
        {
            var loop = Viking();
            loop.Weapons.Equip(WeaponHand.Left, loop.Weapons.Give(TestWorld.Data.Weapons.StarterFor(HeroClass.Ronin)));

            var item = Defeat(loop, BiomeLength, BiomeLength, EnemyType.Guardian).Single(drop => drop.Source == DropSource.Guarantee);

            Assert.That(item.Gear, Is.Not.Null);
            Assert.That(item.Rarity, Is.EqualTo(ItemRarity.Rare));
        }

        [Test]
        public void WeaponTypesFavourTheOwnClass()
        {
            var loop = Viking(seed: 21);
            var own = 0;
            var weapons = 0;

            for (var i = 0; i < 4000; i++)
            {
                loop.State.Weapons.Bag.Clear();
                loop.State.Drops.Gear.Clear();
                foreach (var item in Defeat(loop, 7, 7).Where(item => item.Weapon != null))
                {
                    weapons++;
                    if (item.Weapon!.Type.Id == "axe")
                        own++;
                }
            }

            var types = TestWorld.Data.Weapons.Types.Count;
            Assert.That(weapons / 4000.0, Is.EqualTo(0.2).Within(0.03));
            Assert.That(own / (double)weapons, Is.EqualTo(3.0 / (types + 2)).Within(0.04));
        }

        [Test]
        public void WeaponBasesOpenByBiome()
        {
            var loop = Viking(seed: 23);

            for (var i = 0; i < 2000; i++)
            {
                loop.State.Weapons.Bag.Clear();
                loop.State.Drops.Gear.Clear();
                foreach (var item in Defeat(loop, 7, 7).Where(item => item.Weapon != null))
                    Assert.That(item.Weapon!.Base.FromBiome, Is.EqualTo(1));
            }
        }

        [Test]
        public void ArmorDropsInAllFourSlotsAndJewelryInTheRest()
        {
            var loop = Viking(seed: 29);
            var slots = new HashSet<ItemSlot>();

            for (var i = 0; i < 300; i++)
            {
                loop.State.Drops.Gear.Clear();
                loop.State.Weapons.Bag.Clear();
                foreach (var item in Defeat(loop, 7, 7))
                    slots.Add(item.Slot);
            }

            Assert.That(slots, Is.EquivalentTo(System.Enum.GetValues(typeof(ItemSlot))));
        }

        private static void WearMythicsEverywhere(GameLoop loop)
        {
            var items = TestWorld.Data.Items;
            var rng = new Game.Core.Math.Rng(5);
            foreach (GearSlot slot in System.Enum.GetValues(typeof(GearSlot)))
            {
                var itemSlot = slot switch
                {
                    GearSlot.Helmet => ItemSlot.Helmet,
                    GearSlot.Chest => ItemSlot.Chest,
                    GearSlot.Gloves => ItemSlot.Gloves,
                    GearSlot.Boots => ItemSlot.Boots,
                    GearSlot.Amulet => ItemSlot.Amulet,
                    _ => ItemSlot.Ring
                };
                var item = items.Roll(itemSlot, null, ItemRarity.Mythic, 1, rng);
                loop.State.Drops.Gear.Add(item);
                Assert.That(loop.Gear.Equip(slot, item), Is.True);
            }
        }

        [Test]
        public void AutoSalvageKeepsArmourBetterThanTheWornOne()
        {
            var loop = Viking(seed: 31);
            loop.State.Drops.SalvageUpTo = ItemRarity.Fine;
            var keptFine = 0;

            for (var i = 0; i < 200 && keptFine == 0; i++)
            {
                foreach (var item in Defeat(loop, 7, 7))
                {
                    if (item.Gear != null && item.Rarity <= ItemRarity.Fine && !item.Salvaged)
                        keptFine++;
                }
                loop.State.Drops.Gear.Clear();
            }

            Assert.That(keptFine, Is.GreaterThan(0));
        }

        [Test]
        public void AutoSalvageTurnsLowItemsIntoDust()
        {
            var loop = Viking(seed: 31);
            loop.State.Drops.SalvageUpTo = ItemRarity.Fine;
            WearMythicsEverywhere(loop);
            var kept = 0;

            for (var i = 0; i < 400; i++)
            {
                foreach (var item in Defeat(loop, 7, 7))
                {
                    if (item.Gear != null)
                        Assert.That(item.Salvaged, Is.EqualTo(item.Rarity <= ItemRarity.Fine), item.Rarity.ToString());
                    if (!item.Salvaged)
                        kept++;
                }
                loop.State.Drops.Gear.Clear();
                loop.State.Weapons.Bag.Clear();
            }

            Assert.That(loop.State.Drops.Dust, Is.GreaterThan(0));
            Assert.That(loop.State.Drops.SalvagedItems + kept, Is.EqualTo(loop.State.Drops.ItemsFound));
        }

        [Test]
        public void AutoSalvageKeepsAWeaponBetterThanAHeldOne()
        {
            var loop = Viking(seed: 37);
            loop.Weapons.Equip(WeaponHand.Left, loop.Weapons.Give(TestWorld.Data.Weapons.StarterFor(HeroClass.Ronin)));
            loop.State.Drops.SalvageUpTo = ItemRarity.Fine;
            var fineWeapons = 0;

            for (var i = 0; i < 2000 && fineWeapons < 5; i++)
            {
                foreach (var item in Defeat(loop, 7, 7).Where(item => item.Weapon != null && item.Rarity <= ItemRarity.Fine))
                {
                    Assert.That(item.Salvaged, Is.EqualTo(item.Rarity == ItemRarity.Simple));
                    if (item.Rarity == ItemRarity.Fine)
                        fineWeapons++;
                }
                loop.State.Weapons.Bag.Clear();
                loop.State.Drops.Gear.Clear();
            }

            Assert.That(fineWeapons, Is.EqualTo(5));
        }

        [Test]
        public void FullBagSalvagesNewItems()
        {
            var loop = Viking();
            while (loop.Drops.BagCount < Rules.BagSize)
                loop.State.Drops.Gear.Add(new GearItem(ItemSlot.Boots, ItemRarity.Simple, 1, "red"));

            var item = Defeat(loop, 7, 7).Single();

            Assert.That(item.Salvaged, Is.True);
            Assert.That(loop.Drops.BagCount, Is.EqualTo(Rules.BagSize));
            Assert.That(loop.State.Drops.Dust, Is.EqualTo(Rules.DustFor(item.Rarity)));
        }

        [Test]
        public void NoItemsInsideTheRift()
        {
            var loop = Viking();
            loop.Drops.OnBossDefeated(7, EnemyType.Boss, 7, raisesRecord: false);
            loop.Drops.Flush();

            Assert.That(Items(loop), Is.Empty);
        }

        [Test]
        public void OfflineGivesDailyItemsAtHalfChanceFromTheSameLimit()
        {
            var loop = Viking(seed: 41);
            loop.State.RecordStage = 1000;
            loop.SetLocalDay(1);
            loop.Events.Clear();

            loop.Drops.ApplyOffline(new OfflineResult(3600, 0, BigNumber.Zero, 700, 1000, 0, false, false, false));

            var items = Items(loop);
            Assert.That(items.Count, Is.InRange(1, Rules.Daily.Limit));
            Assert.That(items.Select(item => item.Source), Has.All.EqualTo(DropSource.Offline));
            Assert.That(items.Select(item => item.Stage), Has.All.GreaterThanOrEqualTo(800));
            Assert.That(loop.State.Drops.DailyItems, Is.EqualTo(items.Count));
        }

        [Test]
        public void SkillGemsComeFromTheCommonPoolAndFollowTheChance()
        {
            var loop = Viking(seed: 43);
            const int kills = 60000;

            for (var i = 0; i < kills; i++)
                loop.Drops.OnKill(EnemyType.Normal);
            loop.Drops.Flush();

            var gems = Gems(loop);
            var skills = gems.Where(gem => gem.Kind == GemKind.Skill).ToList();
            Assert.That(skills.Select(gem => gem.Skill!.Weapon), Has.All.Null);
            Assert.That(skills.Select(gem => gem.Ascendancy), Has.All.Null);
            Assert.That(skills.Select(gem => gem.Locked), Has.All.False);
            Assert.That(skills.Count, Is.EqualTo(kills / 300.0).Within(kills / 300.0 * 0.25));
        }

        [Test]
        public void BossesDropSkillGemsMoreOften()
        {
            var loop = Viking(seed: 47);
            const int bosses = 4000;

            for (var i = 0; i < bosses; i++)
                loop.Drops.OnKill(EnemyType.Boss);
            loop.Drops.Flush();

            Assert.That(Gems(loop).Count, Is.EqualTo(bosses / 10.0).Within(bosses / 10.0 * 0.2));
        }

        [Test]
        public void GemDropStatRaisesTheGemChance()
        {
            var loop = Viking(With(CombatStats.GemDrop, ModifierKind.Increased, 1), seed: 47);
            const int bosses = 4000;

            for (var i = 0; i < bosses; i++)
                loop.Drops.OnKill(EnemyType.Boss);
            loop.Drops.Flush();

            Assert.That(Gems(loop).Count, Is.EqualTo(bosses / 5.0).Within(bosses / 5.0 * 0.2));
        }

        [Test]
        public void RealFightGivesRecordItemsForEveryNewStageAndNoneOnTheSecondRun()
        {
            var input = new FakeInputSource();
            var hero = With(CombatStats.Damage, ModifierKind.More, 1e12);
            var loop = TestWorld.Loop(input, new GameState(3) { Class = HeroClass.Viking }, hero);
            var record = new List<ItemDrop>();

            for (var tick = 0; tick < 4000 && loop.State.Stage <= 2 * BiomeLength; tick++)
            {
                input.PressKeys(3);
                loop.Advance(GameLoop.TickSeconds);
                record.AddRange(Items(loop).Where(item => item.Source == DropSource.Record || item.Source == DropSource.Guarantee));
                loop.Events.Clear();
            }

            var passed = loop.State.RecordStage - 1;
            Assert.That(passed, Is.GreaterThan(2 * BiomeLength - 1));
            Assert.That(record.Count, Is.EqualTo(passed));
            Assert.That(record.Select(item => item.Stage).Distinct().Count(), Is.EqualTo(passed));
        }
    }
}
