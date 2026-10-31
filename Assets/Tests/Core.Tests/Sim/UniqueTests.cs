using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class UniqueTests
    {
        private static ItemCatalog Items => TestWorld.Data.Items;

        private static GearItem Unique(string id)
        {
            Assert.That(Items.TryGetUnique(id, out var unique), Is.True, id);
            return new GearItem(unique, 3);
        }

        private static GameLoop Wearing(params string[] uniques)
        {
            var loop = TestWorld.Loop(state: new GameState(4) { Class = HeroClass.Viking });
            foreach (var id in uniques)
            {
                var item = Unique(id);
                loop.State.Drops.Gear.Add(item);
                Assert.That(loop.Gear.Equip(item), Is.True, id);
            }
            return loop;
        }

        private static DropRules AlwaysUnique() =>
            DropRules.Load(TestData.ReadGameDataFile(DropRules.FileName).Replace("\"uniques\": { \"guardian\": 0.03 }", "\"uniques\": { \"guardian\": 1 }"));

        [Test]
        public void SixteenArmourAndJewelleryUniques()
        {
            Assert.That(Items.Uniques.Count, Is.EqualTo(16));
            Assert.That(Items.Uniques.Select(u => u.Id).Distinct().Count(), Is.EqualTo(16));
            Assert.That(Items.Uniques.Select(u => u.Slot).Distinct().Count(), Is.EqualTo(6));
            foreach (var unique in Items.Uniques)
            {
                Assert.That(unique.Slot, Is.Not.EqualTo(ItemSlot.Weapon), unique.Id);
                Assert.That(unique.Lines, Is.Not.Empty, unique.Id);
                Assert.That(unique.RuleRu, Is.Not.Empty, unique.Id);
                Assert.That(unique.RuleEn, Is.Not.Empty, unique.Id);
            }
        }

        [Test]
        public void UniqueHasFixedLinesAndNoRandomAffixes()
        {
            var plate = Unique("colossus_plate");

            Assert.That(plate.Affixes, Is.Empty);
            Assert.That(plate.Rarity, Is.EqualTo(ItemRarity.Legendary));
            Assert.That(plate.Slot, Is.EqualTo(ItemSlot.Chest));
            Assert.That(plate.Wearable, Is.True);
        }

        [Test]
        public void ColossusPlateHitsBossesHarderAndNormalEnemiesSofter()
        {
            var loop = Wearing("colossus_plate");
            var normal = loop.Combat.ConditionMultiplier(StatTags.None);
            loop.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.FromDouble(1e9));
            var boss = loop.Combat.ConditionMultiplier(StatTags.None);

            Assert.That(normal, Is.EqualTo(0.8).Within(1e-9));
            Assert.That(boss, Is.EqualTo(1.4).Within(1e-9));
        }

        [Test]
        public void UniquesSurviveSaveAndLoad()
        {
            var loop = Wearing("devourer_eye");
            loop.State.Drops.Gear.Add(Unique("silence_helm"));

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State;

            Assert.That(loaded.Gear.In(GearSlot.Amulet)!.Unique!.Id, Is.EqualTo("devourer_eye"));
            Assert.That(loaded.Drops.Gear.Single().Unique!.Id, Is.EqualTo("silence_helm"));
        }

        [Test]
        public void GuardiansDropUniquesOnlyPastTheLimitAndAutoSalvageKeepsThem()
        {
            var stages = TestWorld.Data.Stages;
            var loop = TestWorld.Loop(state: new GameState(11) { Class = HeroClass.Viking });
            loop.Drops.UseData(AlwaysUnique(), stages, TestWorld.Data.Gems, Items);
            loop.State.Drops.SalvageUpTo = ItemRarity.Legendary;

            var early = DropsFrom(loop, stages.BiomeLength);
            var late = DropsFrom(loop, 50000);

            Assert.That(early.Where(d => d.Gear != null).All(d => d.Gear!.Unique == null), Is.True);
            var uniques = late.Where(d => d.Gear?.Unique != null).ToList();
            Assert.That(uniques, Is.Not.Empty);
            Assert.That(uniques.All(d => !d.Salvaged), Is.True);
        }

        private static System.Collections.Generic.List<ItemDrop> DropsFrom(GameLoop loop, long stage)
        {
            var drops = new System.Collections.Generic.List<ItemDrop>();
            for (var i = 0; i < 30; i++)
            {
                loop.Events.Clear();
                loop.Drops.OnBossDefeated(stage, EnemyType.Guardian, stage, raisesRecord: true);
                loop.Drops.Flush();
                for (var e = 0; e < loop.Events.Count; e++)
                {
                    if (loop.Events[e].Kind == GameEventKind.ItemDropped && loop.Events[e].Drop!.Source == DropSource.Guardian)
                        drops.Add(loop.Events[e].Drop!);
                }
                loop.State.Drops.Gear.Clear();
                loop.State.Weapons.Bag.Clear();
            }
            return drops;
        }

        [Test]
        public void StenographerGlovesCastTheLeftSkillEveryTwoHundredKeys()
        {
            var plain = SkillTestHero.Of(HeroClass.Viking, "iron_rim_bash");
            var gloved = SkillTestHero.Of(HeroClass.Viking, "iron_rim_bash");
            gloved.Loop.Hero.Add(new Modifier(CombatStats.KeyCastLeftEvery, ModifierKind.Flat, 200));

            var plainCasts = Casts(plain);
            var glovedCasts = Casts(gloved);

            Assert.That(plainCasts, Is.Zero);
            Assert.That(glovedCasts, Is.GreaterThanOrEqualTo(1));
        }

        private static long Casts(SkillTestHero hero)
        {
            var before = hero.State.SkillCasts;
            for (var tick = 0; tick < 300; tick++)
            {
                hero.Input.PressKeys(1);
                hero.Loop.Tick();
            }
            return hero.State.SkillCasts - before;
        }

        [Test]
        public void WandererBootsTurnMouseDistanceIntoResource()
        {
            var plain = Resource(new StatSheet(), mousePx: 1000, keys: 0);
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.ResourcePerThousandPx, ModifierKind.Flat, 2));
            var booted = Resource(hero, mousePx: 1000, keys: 0);

            Assert.That(booted - plain, Is.EqualTo(2).Within(1e-6));
        }

        [Test]
        public void HelmOfSilenceBuildsResourceWhileIdle()
        {
            var plain = Resource(new StatSheet(), mousePx: 0, keys: 0);
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.ResourcePerIdleSecond, ModifierKind.Flat, 3));
            var helmed = Resource(hero, mousePx: 0, keys: 0);
            var busy = Resource(hero, mousePx: 0, keys: 1) - Resource(new StatSheet(), mousePx: 0, keys: 1);

            Assert.That(helmed - plain, Is.EqualTo(3 * GameLoop.TickSeconds).Within(1e-6));
            Assert.That(busy, Is.EqualTo(0).Within(1e-9));
        }

        private static double Resource(StatSheet hero, double mousePx, int keys)
        {
            var input = new FakeInputSource();
            var state = new GameState(2) { Class = HeroClass.Viking };
            var loop = TestWorld.Loop(input, state, hero);
            loop.State.Resource = 50;
            if (mousePx > 0)
                input.MoveMouse((float)mousePx);
            if (keys > 0)
                input.PressKeys(keys);
            loop.Tick();
            return loop.State.Resource;
        }

        [Test]
        public void RingOfTwoHandsStrengthensWeaponInnates()
        {
            var plain = Real();
            var ringed = Real();
            var ring = new GearItem(TestWorld.Real.Items.Uniques.Single(u => u.Id == "two_hands_ring"), 3);
            ringed.State.Drops.Gear.Add(ring);
            Assert.That(ringed.Gear.Equip(ring), Is.True);

            var plainValues = plain.Weapons.Modifiers.Select(m => m.Value).ToList();
            var ringedValues = ringed.Weapons.Modifiers.Select(m => m.Value).ToList();
            Assert.That(plainValues, Is.Not.Empty);
            for (var i = 0; i < plainValues.Count; i++)
                Assert.That(ringedValues[i], Is.EqualTo(plainValues[i] * 1.5).Within(1e-9));

            ringed.Gear.Unequip(GearSlot.RingLeft);
            Assert.That(ringed.Weapons.Modifiers.Select(m => m.Value), Is.EqualTo(plainValues));
        }

        private static GameLoop Real() =>
            new GameLoop(TestWorld.Real, new FakeInputSource(), new GameState(4) { Class = HeroClass.Viking }, new StatSheet());

        [Test]
        public void EyeOfTheDevourerRaisesEveryGemLevel()
        {
            var plain = SkillTestHero.Of(HeroClass.Viking, "iron_rim_bash");
            var eyed = SkillTestHero.Of(HeroClass.Viking, "iron_rim_bash");
            eyed.Loop.Hero.Add(new Modifier(CombatStats.GemLevelBonus, ModifierKind.Flat, 1));
            var gem = TestWorld.Data.Gems["iron_rim_bash"];
            var perLevel = TestWorld.Data.Gems.Levels.EffectPerLevel;

            Assert.That(eyed.Loop.Skills.LevelPower(gem) - plain.Loop.Skills.LevelPower(gem), Is.EqualTo(perLevel * plain.Loop.Skills.ColorPower(gem)).Within(1e-9));
        }

        [Test]
        public void ApprenticeRingDoublesExperienceOfTheLowestGem()
        {
            var plain = SkillTestHero.Of(HeroClass.Viking, "iron_rim_bash", "no_mercy");
            var ringed = SkillTestHero.Of(HeroClass.Viking, "iron_rim_bash", "no_mercy");
            ringed.Loop.Hero.Add(new Modifier(CombatStats.LowestGemExperience, ModifierKind.Flat, 1));
            foreach (var hero in new[] { plain, ringed })
            {
                hero.State.ProgressOf(TestWorld.Data.Gems["iron_rim_bash"]).Level = 5;
                hero.State.Kills += 1;
                hero.Loop.Tick();
            }

            var low = TestWorld.Data.Gems["no_mercy"];
            var high = TestWorld.Data.Gems["iron_rim_bash"];
            Assert.That(ringed.State.ProgressOf(low).Experience, Is.EqualTo(2 * plain.State.ProgressOf(low).Experience).Within(1e-9));
            Assert.That(plain.State.ProgressOf(low).Experience, Is.GreaterThan(0));
            Assert.That(ringed.State.ProgressOf(high).Experience, Is.EqualTo(plain.State.ProgressOf(high).Experience).Within(1e-9));
        }
    }
}
