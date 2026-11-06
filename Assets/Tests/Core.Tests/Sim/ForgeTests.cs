using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Content.Json;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ForgeTests
    {
        private static ForgeRules Rules => TestWorld.Data.Forge;
        private const float PixelsPerMeter = (float)(1 / InputStats.DefaultMetersPerPixel);

        private static GearItem Coif(ItemRarity rarity = ItemRarity.Rare, int itemLevel = 1)
        {
            Assert.That(TestWorld.Data.Items.TryGetBase("mail_coif", out var gearBase), Is.True);
            return new GearItem(gearBase, rarity, itemLevel, System.Array.Empty<GearAffix>());
        }

        private static (GameLoop Loop, FakeInputSource Input) Smith()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            loop.State.RecordStage = 51;
            loop.Tick();
            loop.Camp.SetGuilds(new[] { "smiths" });
            return (loop, input);
        }

        private static void Work(GameLoop loop, FakeInputSource input, double heat)
        {
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Peaceful.SetActivity(ForgeSystem.Activity);
            input.ClickLeft();
            loop.Tick();
            Assert.That(loop.Forge.Busy, Is.True, "no quench before the hammering is done");
            for (var i = 0; i < Rules.Hammers; i += 10)
            {
                input.PressKeys(10);
                loop.Tick();
            }
            loop.State.Forge.Heat = heat;
            input.ClickLeft();
            loop.Tick();
        }

        [Test]
        public void SmeltingTurnsUnwantedLootIntoTheOreOfItsBiome()
        {
            var (loop, _) = Smith();
            loop.State.Drops.Gear.Add(Coif(ItemRarity.Rare, 1));
            loop.State.Drops.Gear.Add(Coif(ItemRarity.Simple, 3));
            Assert.That(loop.Forge.SmeltGear(5), Is.EqualTo(ForgeResult.NoItem));
            Assert.That(loop.Forge.SmeltGear(0), Is.EqualTo(ForgeResult.Done));
            Assert.That(loop.Camp.Count("ore_hills"), Is.EqualTo(Rules.SmeltBase + Rules.SmeltPerRarity * (int)ItemRarity.Rare));
            Assert.That(loop.Forge.SmeltGear(0), Is.EqualTo(ForgeResult.Done));
            Assert.That(loop.Camp.Count("ore_ice"), Is.EqualTo(Rules.SmeltBase), "item level 3 is the third biome");
            Assert.That(loop.State.Drops.Gear, Is.Empty);
            loop.State.Weapons.Bag.Add(TestWorld.Data.Weapons.Starter(TestWorld.Data.Weapons.Bases.First()));
            Assert.That(loop.Forge.SmeltWeapon(0), Is.EqualTo(ForgeResult.Done));
            Assert.That(loop.State.Forge.Smelted, Is.EqualTo(3));
        }

        [Test]
        public void TheForgeWaitsForTheSmithsGuild()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            loop.State.RecordStage = 51;
            loop.Tick();
            loop.State.Drops.Gear.Add(Coif());
            Assert.That(loop.Forge.SmeltGear(0), Is.EqualTo(ForgeResult.Closed));
            Assert.That(loop.Forge.TemperWeapon(), Is.EqualTo(ForgeResult.Closed));
        }

        [Test]
        public void QuenchingInTheHeatZoneTempersTheBaseFasterUpToFivePercent()
        {
            var (loop, input) = Smith();
            var coif = Coif();
            loop.State.Drops.Gear.Add(coif);
            loop.Gear.Equip(coif);
            var effect = loop.Hero.Value(CombatStats.RedGemEffect, 1);
            Assert.That(loop.Forge.TemperGear(GearSlot.Helmet), Is.EqualTo(ForgeResult.NoOre));
            Assert.That(loop.Forge.TemperGear(GearSlot.Boots), Is.EqualTo(ForgeResult.NoItem));
            loop.Camp.Add("ore_hills", 100);
            Assert.That(loop.Forge.TemperGear(GearSlot.Helmet), Is.EqualTo(ForgeResult.Done));
            Assert.That(loop.Forge.TemperWeapon(), Is.EqualTo(ForgeResult.Busy));
            Assert.That(loop.Camp.Count("ore_hills"), Is.EqualTo(100 - Rules.TemperOre));
            Work(loop, input, (Rules.ZoneFrom + Rules.ZoneTo) / 2);
            Assert.That(coif.Forge.Temper, Is.EqualTo(Rules.StepInZone).Within(1e-12));
            Assert.That(loop.Hero.Value(CombatStats.RedGemEffect, 1) - 1, Is.EqualTo((effect - 1) * (1 + Rules.StepInZone)).Within(1e-9));

            loop.Forge.TemperGear(GearSlot.Helmet);
            Work(loop, input, 0.1);
            Assert.That(coif.Forge.Temper, Is.EqualTo(Rules.StepInZone + Rules.StepOutside).Within(1e-12), "a cold quench gives less");

            for (var i = 0; i < 20 && loop.Forge.TemperGear(GearSlot.Helmet) == ForgeResult.Done; i++)
                Work(loop, input, Rules.ZoneFrom);
            Assert.That(coif.Forge.Temper, Is.EqualTo(Rules.MaxTemper).Within(1e-12));
            Assert.That(loop.Forge.TemperGear(GearSlot.Helmet), Is.EqualTo(ForgeResult.Maxed));
        }

        [Test]
        public void TheMouseKeepsTheHeatAndItCoolsWhenLeftAlone()
        {
            var (loop, input) = Smith();
            loop.Camp.Add("ore_hills", 10);
            Assert.That(loop.Forge.TemperWeapon(), Is.EqualTo(ForgeResult.Done));
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Peaceful.SetActivity(ForgeSystem.Activity);
            for (var i = 0; i < 10; i++)
            {
                input.MoveMouse(PixelsPerMeter * 0.2f);
                loop.Tick();
            }
            var hot = loop.State.Forge.Heat;
            Assert.That(hot, Is.GreaterThan(0.5));
            for (var i = 0; i < 30; i++)
                loop.Tick();
            Assert.That(loop.State.Forge.Heat, Is.LessThan(hot));
            Assert.That(ForgeView.Gauge(loop.Forge).Heat, Is.EqualTo(loop.State.Forge.Heat));
        }

        [Test]
        public void AWeaponCarriesTheBrandOfAGuardianItKilled()
        {
            var (loop, _) = Smith();
            var weapon = loop.State.Weapons.Held!;
            var guardian = TestWorld.Data.Enemies.All.First(e => e.Type == Game.Core.Content.EnemyType.Guardian);
            Assert.That(loop.Forge.Brand(guardian.Id), Is.EqualTo(ForgeResult.NotSlain));
            loop.Events.Clear();
            loop.Events.Add(GameEvent.ForEnemy(GameEventKind.EnemyKilled, loop.State.Tick, 50, guardian));
            loop.Forge.NoteKills(0);
            loop.Forge.NoteKills(0);
            Assert.That(weapon.Forge.Slain, Is.EqualTo(new[] { guardian.Id }));
            Assert.That(loop.Forge.Brand(guardian.Id), Is.EqualTo(ForgeResult.Done));
            Assert.That(weapon.Forge.Brand, Is.EqualTo(guardian.Id));
        }

        [Test]
        public void AutomaticSalvageAlsoGivesTheOreOfTheBiome()
        {
            var (loop, _) = Smith();
            loop.State.Drops.SalvageUpTo = ItemRarity.Legendary;
            var dust = loop.State.Drops.Dust;
            loop.Forge.SalvageOre(new ItemDrop(DropSource.Record, 120, null, Coif(ItemRarity.Rare, 3), true, 10));
            Assert.That(loop.Camp.Count("ore_ice"), Is.EqualTo(loop.Forge.SmeltYield(ItemRarity.Rare)), "the ore of the third biome");
            Assert.That(loop.State.Forge.SalvageOre, Is.EqualTo(loop.Forge.SmeltYield(ItemRarity.Rare)));

            loop.State.Forge.SalvageOre = 0;
            loop.State.Drops.SalvageUpTo = ItemRarity.Primordial;
            for (var i = 0; i < 50 && loop.State.Drops.SalvagedItems == 0; i++)
            {
                loop.Events.Clear();
                loop.Drops.OnBossDefeated(7, Game.Core.Content.EnemyType.Boss, 7, raisesRecord: true);
                loop.Drops.Flush();
            }
            Assert.That(loop.State.Drops.SalvagedItems, Is.GreaterThan(0));
            Assert.That(loop.State.Forge.SalvageOre, Is.GreaterThan(0), "a salvaged boss drop gives ore");
            Assert.That(loop.State.Drops.Dust, Is.GreaterThan(dust), "dust for rerolls stays");
        }

        [Test]
        public void TemperAndBrandSurviveTheSave()
        {
            var (loop, _) = Smith();
            var coif = Coif();
            loop.State.Drops.Gear.Add(coif);
            loop.Gear.Equip(coif);
            coif.Forge.Temper = 0.03;
            loop.State.Weapons.Held!.Forge.Temper = 0.02;
            loop.State.Weapons.Held!.Forge.Slain.Add("hills_guardian");
            loop.State.Weapons.Held!.Forge.Brand = "hills_guardian";
            loop.Camp.Add("ore_hills", 10);
            loop.Forge.TemperWeapon();
            loop.State.Forge.Hammers = 7;
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(JsonWriter.Write(GameStateJson.Write(copy)), Is.EqualTo(JsonWriter.Write(GameStateJson.Write(loop.State))));
            Assert.That(copy.Gear.In(GearSlot.Helmet)!.Forge.Temper, Is.EqualTo(0.03));
            Assert.That(copy.Weapons.Held!.Forge.Brand, Is.EqualTo("hills_guardian"));
            Assert.That(copy.Forge.Target, Is.EqualTo(ForgeTarget.Weapon));
        }

        [Test]
        public void ForgeWorkDoesNotAllocate()
        {
            var (loop, input) = Smith();
            loop.Camp.Add("ore_hills", 10);
            loop.Forge.TemperWeapon();
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Peaceful.SetActivity(ForgeSystem.Activity);
            loop.Tick();
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 30; i++)
            {
                input.PressKeys(1);
                input.MoveMouse(100);
                loop.Tick();
                loop.Events.Clear();
            }
            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        [TestCase(@"""building"": ""forge""", @"""building"": ""anvil""", "unknown building 'anvil'")]
        [TestCase(@"""max"": 0.05", @"""max"": 0.1", "0.1")]
        public void BrokenForgeIsReported(string from, string to, string message)
        {
            var json = Content.TestData.ReadGameDataFile(ForgeRules.FileName).Replace(from, to);
            var error = Assert.Throws<ContentException>(() => ForgeRules.Load(json, TestWorld.Data.Camp, TestWorld.Data.Stages.Biomes))!;
            Assert.That(error.Message, Does.Contain(message));
        }
    }
}
