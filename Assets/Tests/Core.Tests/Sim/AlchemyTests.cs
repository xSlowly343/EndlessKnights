using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class AlchemyTests
    {
        private static AlchemyRules Rules => TestWorld.Data.Alchemy;

        private static (GameLoop Loop, FakeInputSource Input) Alchemist()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            loop.Tick();
            loop.Camp.Add("herb_knockbloom", 10);
            loop.Camp.Add("herb_clickweed", 10);
            loop.Camp.Add("herb_hushbell", 10);
            loop.Camp.Add("herb_stormfern", 2);
            return (loop, input);
        }

        private static void Stir(GameLoop loop, FakeInputSource input, int presses)
        {
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Peaceful.SetActivity(AlchemySystem.Activity);
            for (var i = 0; i < presses; i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }
            loop.Peaceful.SetMode(PeacefulMode.Battle);
        }

        [Test]
        public void AKnownRecipeIsStirredIntoAPotionThatBuffsTheFightForAWhile()
        {
            var (loop, input) = Alchemist();
            var plain = loop.Hero.Value(CombatStats.Damage, 1);
            Assert.That(loop.Alchemy.Brew("might"), Is.EqualTo(AlchemyResult.Done));
            Assert.That(loop.Camp.Count("herb_knockbloom"), Is.EqualTo(8));
            Assert.That(loop.Alchemy.Brew("fortune"), Is.EqualTo(AlchemyResult.Busy));
            Stir(loop, input, Rules.Recipe("might")!.Stirs);
            Assert.That(loop.State.Alchemy.Potions.Count, Is.EqualTo(1));
            Assert.That(loop.Alchemy.Drink(0), Is.EqualTo(AlchemyResult.Done));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(plain * 1.06).Within(1e-9));

            loop.State.Alchemy.ActiveTicksLeft = 2;
            loop.Tick();
            loop.Tick();
            Assert.That(loop.State.Alchemy.Active, Is.Null);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(plain).Within(1e-9));
        }

        [Test]
        public void ARecipeMustBeLearnedAndAnExperimentMakesItOrAnUnknownPotion()
        {
            var (loop, input) = Alchemist();
            Assert.That(loop.Alchemy.Brew("patience"), Is.EqualTo(AlchemyResult.UnknownRecipe));
            Assert.That(loop.Alchemy.Experiment(new[] { "herb_knockbloom", "herb_hushbell", "herb_hushbell" }), Is.EqualTo(AlchemyResult.Done));
            Assert.That(loop.Alchemy.Knows("patience"), Is.True, "the right herbs teach the recipe");
            Stir(loop, input, Rules.Recipe("patience")!.Stirs);

            Assert.That(loop.Alchemy.Experiment(new[] { "herb_clickweed", "herb_hushbell" }), Is.EqualTo(AlchemyResult.Done));
            Stir(loop, input, 200);
            var unknown = loop.State.Alchemy.Potions.Last();
            Assert.That(unknown.Unknown, Is.True);
            Assert.That(AlchemyView.Potions(loop.Alchemy).Last().Name, Is.EqualTo("?"));
            Assert.That(loop.Alchemy.Knows(unknown.Recipe), Is.False);
            loop.Alchemy.Drink(loop.State.Alchemy.Potions.Count - 1);
            Assert.That(loop.Alchemy.Knows(unknown.Recipe), Is.True, "drinking it reveals the recipe");
        }

        [Test]
        public void RareHerbsMakeAFinePotionWithinTheCap()
        {
            var (loop, input) = Alchemist();
            var plain = loop.Hero.Value(CombatStats.Damage, 1);
            loop.Alchemy.Experiment(new[] { "herb_stormfern", "herb_knockbloom", "herb_knockbloom" });
            Stir(loop, input, Rules.Recipe("storm")!.Stirs);
            Assert.That(loop.State.Alchemy.Potions[0].Fine, Is.True);
            loop.Alchemy.Drink(0);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1) / plain - 1, Is.EqualTo(0.1).Within(1e-9), "a fine storm draught is +10%, the cap");
        }

        [Test]
        public void APotionAboveTheCapIsRejectedAndTheStateIsSaved()
        {
            var json = TestData.ReadGameDataFile(AlchemyRules.FileName).Replace("\"value\": 0.06 }] },\n    { \"id\": \"fortune\"", "\"value\": 0.2 }] },\n    { \"id\": \"fortune\"");
            Assert.Throws<ContentException>(() => AlchemyRules.Load(json));

            var (loop, input) = Alchemist();
            loop.Alchemy.Brew("might");
            Stir(loop, input, 10);
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Alchemy.Brewing!.Recipe, Is.EqualTo("might"));
            Assert.That(copy.Alchemy.Stirs, Is.EqualTo(10));
            Assert.That(AlchemyView.Book(loop.Alchemy, loop.Camp).Single(l => l.Id == "might").Known, Is.True);
        }
    }
}
