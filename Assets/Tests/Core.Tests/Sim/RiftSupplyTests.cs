using Game.Core.Input;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class RiftSupplyTests
    {
        [Test]
        public void ASupplyFromTheRearWorksOnlyInsideTheRiftOfItsWindow()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var supply = loop.RiftSupply;
            loop.State.Rift.WindowId = 100;
            var before = loop.Hero.Value(CombatStats.Damage, 1);
            supply.Receive(100, "dish:carp_pie");
            Assert.That(supply.HasSupply, Is.True);
            Assert.That(supply.Active, Is.False, "not inside the rift yet");
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(before).Within(1e-9));

            loop.State.Rift.Inside = true;
            supply.Tick();
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.GreaterThan(before), "the pie works in the rift");
            loop.State.Rift.Inside = false;
            supply.Tick();
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(before).Within(1e-9));

            loop.State.Rift.Inside = true;
            loop.State.Rift.WindowId = 101;
            supply.Tick();
            Assert.That(supply.HasSupply, Is.False, "a supply lasts one window");
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(before).Within(1e-9));
            supply.Receive(101, "dish:mud_pie");
            Assert.That(supply.HasSupply, Is.False, "an unknown supply is ignored");
        }

        [Test]
        public void TheCookPacksADishOrAPotionAndGetsItBackIfSendingFails()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var supply = loop.RiftSupply;
            Assert.That(supply.Pack("dish:carp_pie"), Is.EqualTo(RiftSupplyResult.NotOwned));
            loop.State.Cooking.Meals.Add("carp_pie");
            Assert.That(supply.Pack("dish:carp_pie"), Is.EqualTo(RiftSupplyResult.Done));
            Assert.That(loop.State.Cooking.Meals, Is.Empty);
            supply.Unpack("dish:carp_pie");
            Assert.That(loop.State.Cooking.Meals, Is.EqualTo(new[] { "carp_pie" }));
            var recipe = TestWorld.Data.Alchemy.Recipes[0].Id;
            loop.State.Alchemy.Potions.Add(new Potion { Recipe = recipe, Fine = false });
            Assert.That(supply.Pack("potion:" + recipe + ":fine"), Is.EqualTo(RiftSupplyResult.NotOwned), "a plain potion is not a fine one");
            Assert.That(supply.Pack("potion:" + recipe), Is.EqualTo(RiftSupplyResult.Done));
            Assert.That(loop.State.Alchemy.Potions, Is.Empty);
            Assert.That(supply.Pack("potion:poison"), Is.EqualTo(RiftSupplyResult.Unknown));
            Assert.That(supply.ModifiersOf("potion:" + recipe + ":fine")[0].Value,
                Is.EqualTo(TestWorld.Data.Alchemy.Recipes[0].Modifiers[0].Value * TestWorld.Data.Alchemy.FineMultiplier).Within(1e-9));
        }

        [Test]
        public void TheSupplySurvivesTheSaveAndTicksWithoutAllocating()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.State.Rift.WindowId = 7;
            loop.RiftSupply.Receive(7, "dish:ember_stew");
            var copy = Game.Core.Save.GameStateJson.Read(new Game.Core.Content.ContentNode(Game.Core.Save.GameStateJson.Write(loop.State), "save", string.Empty),
                TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Rift.Supply, Is.EqualTo("dish:ember_stew"));
            Assert.That(copy.Rift.SupplyWindow, Is.EqualTo(7));
            loop.RiftSupply.Tick();
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
                loop.RiftSupply.Tick();
            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
