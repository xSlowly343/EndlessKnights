using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Input;
using Game.Core.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class SharedCraftTests
    {
        private static GameLoop InHideout(FakeInputSource input, string activity, string building, params string[] partners)
        {
            var loop = TestWorld.Loop(input);
            loop.State.RecordStage = 51;
            loop.State.Camp.Tier = 1;
            loop.Tick();
            loop.Camp.SetGuilds(new[] { "smiths" });
            var hideout = loop.Hideout;
            hideout.Use(5, 2, 300, 5, 0, 1, System.Array.Empty<string>());
            var buildings = new Dictionary<string, int>();
            if (building.Length > 0)
                buildings[building] = 1;
            hideout.UseBuild(buildings, System.Array.Empty<string>(), string.Empty, 0, 0, 0, 0, 0, false, new Dictionary<string, int>(), System.Array.Empty<string>());
            hideout.UseCrafts(activity == ForgeSystem.Activity ? partners : System.Array.Empty<string>(), activity == AlchemySystem.Activity ? partners : System.Array.Empty<string>());
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Peaceful.SetPlace(PeacefulPlace.Hideout);
            loop.Peaceful.SetActivity(activity);
            return loop;
        }

        private static GearItem Coif()
        {
            Assert.That(TestWorld.Data.Items.TryGetBase("mail_coif", out var gearBase), Is.True);
            return new GearItem(gearBase, ItemRarity.Rare, 1, System.Array.Empty<GearAffix>());
        }

        private static int Forge(GameLoop loop, FakeInputSource input, double heat)
        {
            var coif = Coif();
            loop.State.Drops.Gear.Add(coif);
            loop.Gear.Equip(coif);
            loop.Camp.Add("ore_hills", 100);
            Assert.That(loop.Forge.TemperGear(GearSlot.Helmet), Is.EqualTo(ForgeResult.Done));
            var presses = 0;
            while (loop.State.Forge.Hammers < loop.Forge.HammersNeeded && presses < 1000)
            {
                input.PressKeys(1);
                loop.Tick();
                presses++;
            }
            loop.State.Forge.Heat = heat;
            input.ClickLeft();
            loop.Tick();
            Assert.That(loop.Forge.Busy, Is.False);
            return presses;
        }

        [Test]
        public void TheSharedForgeWaitsForItsBuildingInTheHideout()
        {
            var input = new FakeInputSource();
            var loop = InHideout(input, ForgeSystem.Activity, string.Empty, "Bob");
            Assert.That(loop.Forge.Open, Is.False, "no shared forge, no smithing in the hideout");
            Assert.That(loop.Forge.TemperWeapon(), Is.EqualTo(ForgeResult.Closed));
            Assert.That(loop.Hideout.Station, Is.EqualTo(HideoutSystem.ForgeStation));
            loop.Peaceful.SetPlace(PeacefulPlace.Refuge);
            Assert.That(loop.Forge.Open, Is.True, "the own forge still works at home");
        }

        [Test]
        public void TwoAtTheForgeHammerHalfAsLongAndTemperMore()
        {
            var rules = TestWorld.Data.Forge;
            var crafts = TestWorld.Data.Hideout.Crafts;
            var input = new FakeInputSource();
            var alone = InHideout(input, ForgeSystem.Activity, crafts.ForgeBuilding);
            var zone = (rules.ZoneFrom + rules.ZoneTo) / 2;
            var aloneHits = Forge(alone, input, zone);
            Assert.That(alone.State.Gear.In(GearSlot.Helmet)!.Forge.Temper, Is.EqualTo(rules.StepInZone).Within(1e-12), "alone at the shared forge it is as at home");

            var together = InHideout(input, ForgeSystem.Activity, crafts.ForgeBuilding, "Bob");
            Assert.That(together.Forge.Together, Is.True);
            var togetherHits = Forge(together, input, zone);
            Assert.That(togetherHits, Is.LessThanOrEqualTo((int)System.Math.Ceiling(aloneHits * (1 - crafts.HammersShare))));
            var mark = together.State.Gear.In(GearSlot.Helmet)!.Forge;
            var step = rules.StepInZone * (1 + crafts.StepBoost);
            Assert.That(mark.Temper, Is.EqualTo(step).Within(1e-12).Or.EqualTo(step * crafts.MasterpieceBoost).Within(1e-12));
        }

        [Test]
        public void AMasterpieceOfThePartyCarriesTheNamesOfTheMasters()
        {
            var crafts = TestWorld.Data.Hideout.Crafts;
            var input = new FakeInputSource();
            var masterpieces = 0;
            for (var seed = 0; seed < 200 && masterpieces == 0; seed++)
            {
                var loop = InHideout(input, ForgeSystem.Activity, crafts.ForgeBuilding, "Bob");
                loop.State.Rng = new Game.Core.Math.Rng((ulong)seed + 1);
                Forge(loop, input, 0.1);
                masterpieces += (int)loop.State.Forge.Masterpieces;
                if (masterpieces > 0)
                {
                    var mark = loop.State.Gear.In(GearSlot.Helmet)!.Forge;
                    Assert.That(mark.Masters, Is.EqualTo(new[] { "Bob" }));
                    Assert.That(HideoutView.Masterpiece(mark), Does.Contain("Bob"));
                }
            }
            Assert.That(masterpieces, Is.GreaterThan(0));
        }

        [Test]
        public void TwoAtTheCauldronStirLessAndShareTheBrew()
        {
            var crafts = TestWorld.Data.Hideout.Crafts;
            var input = new FakeInputSource();
            var loop = InHideout(input, AlchemySystem.Activity, crafts.CauldronBuilding, "Bob");
            loop.Camp.Add("herb_knockbloom", 10);
            loop.Camp.Add("herb_clickweed", 10);
            var recipe = loop.Alchemy.Rules.Recipe("might")!;
            Assert.That(loop.Alchemy.Brew("might"), Is.EqualTo(AlchemyResult.Done));
            Assert.That(loop.Alchemy.StirsNeeded(recipe), Is.EqualTo(recipe.Stirs * (1 - crafts.StirsShare)).Within(1e-9));
            var presses = 0;
            while (loop.Alchemy.Brewing && presses < recipe.Stirs)
            {
                input.PressKeys(1);
                loop.Tick();
                presses++;
            }
            Assert.That(loop.Alchemy.Brewing, Is.False);
            Assert.That(presses, Is.LessThanOrEqualTo((int)System.Math.Ceiling(recipe.Stirs * (1 - crafts.StirsShare))));
            var shared = loop.Hideout.TakeSharedBrews();
            Assert.That(shared, Has.Count.EqualTo(1));
            Assert.That(shared[0].Recipe, Is.EqualTo("might"));
            Assert.That(loop.Hideout.TakeSharedBrews(), Is.Empty);

            var before = loop.State.Alchemy.Potions.Count;
            loop.Hideout.Receive(new Dictionary<string, long> { ["potion:fortune:fine"] = 1, ["potion:might"] = 2, ["herb_windwort"] = 1 });
            Assert.That(loop.State.Alchemy.Potions.Count, Is.EqualTo(before + 3));
            Assert.That(loop.State.Alchemy.Potions.Exists(p => p.Recipe == "fortune" && p.Fine), Is.True);
            Assert.That(loop.Camp.Count("herb_windwort"), Is.EqualTo(1));
            Assert.That(HideoutView.Crafting(loop.Hideout), Does.Contain("Bob"));
        }
    }
}
