using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Json;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CookingTests
    {
        private static CookingRules Rules => TestWorld.Data.Cooking;

        private static (GameLoop Loop, FakeInputSource Input) Cook(HeroClass heroClass = HeroClass.Minstrel)
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            loop.State.Class = heroClass;
            loop.State.RecordStage = 101;
            loop.Tick();
            loop.Camp.SetGuilds(new[] { "cooks" });
            return (loop, input);
        }

        private static void Stock(GameLoop loop, DishDef dish)
        {
            foreach (var ingredient in dish.Ingredients)
                loop.Camp.Add(ingredient, 1);
        }

        private static void Prepare(GameLoop loop, FakeInputSource input, DishDef dish)
        {
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Peaceful.SetActivity(CookingSystem.Activity);
            input.ClickLeft(dish.Plates);
            loop.Tick();
            Assert.That(loop.State.Cooking.Plates, Is.Zero, "nothing goes on the plate before it is chopped");
            for (var i = 0; i < dish.Chops; i += 5)
            {
                input.PressKeys(5);
                loop.Tick();
            }
            input.ClickLeft(dish.Plates);
            loop.Tick();
        }

        [Test]
        public void TheChefTableWaitsForTheCooksGuildAndTheIngredients()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            loop.State.RecordStage = 101;
            loop.Tick();
            Assert.That(loop.Cooking.Cook("fish_soup"), Is.EqualTo(CookingResult.Closed));
            loop.Camp.SetGuilds(new[] { "cooks" });
            Assert.That(loop.Cooking.Cook("fish_soup"), Is.EqualTo(CookingResult.NoIngredients));
            Assert.That(loop.Cooking.Cook("cake"), Is.EqualTo(CookingResult.UnknownDish));
            loop.Camp.Add("fish_hills_roach", 1);
            loop.Camp.Add("herb_windwort", 1);
            Assert.That(loop.Cooking.Cook("fish_soup"), Is.EqualTo(CookingResult.NoIngredients), "two roach are needed");
            loop.Camp.Add("fish_hills_roach", 1);
            Assert.That(CookingView.Book(loop.Cooking).Single(d => d.Id == "fish_soup").CanCook, Is.True);
            Assert.That(loop.Cooking.Cook("fish_soup"), Is.EqualTo(CookingResult.Done));
            Assert.That(loop.State.Camp.Bag, Is.Empty);
            Assert.That(loop.Cooking.Cook("fish_soup"), Is.EqualTo(CookingResult.Busy));
        }

        [Test]
        public void KeysChopClicksPlateAndTheMealLastsHoursOfBattle()
        {
            var (loop, input) = Cook();
            var dish = Rules.Dish("carp_pie")!;
            Stock(loop, dish);
            loop.Cooking.Cook(dish.Id);
            loop.Events.Clear();
            Prepare(loop, input, dish);
            Assert.That(loop.Events.CountOf(GameEventKind.DishCooked), Is.EqualTo(1));
            Assert.That(loop.State.Cooking.Meals, Is.EqualTo(new[] { dish.Id }));
            Assert.That(loop.State.Cooking.Book, Does.Contain(dish.Id));
            Assert.That(loop.Cooking.Busy, Is.False);

            loop.Peaceful.SetMode(PeacefulMode.Battle);
            loop.Tick();
            var before = loop.Hero.Value(CombatStats.Damage, 1);
            Assert.That(loop.Cooking.Eat(0), Is.EqualTo(CookingResult.Done));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1) / before, Is.EqualTo(1 + 0.04).Within(0.03), "the pie adds damage");
            Assert.That(loop.State.Cooking.ActiveTicksLeft, Is.EqualTo(dish.Seconds * GameLoop.TicksPerSecond));
            loop.State.Cooking.ActiveTicksLeft = 2;
            loop.Events.Clear();
            loop.Tick();
            loop.Tick();
            Assert.That(loop.Events.CountOf(GameEventKind.MealEnded), Is.EqualTo(1));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(before).Within(1e-9));
        }

        [Test]
        public void AFavoriteDishLastsHalfAsLongAgain()
        {
            var (loop, _) = Cook(HeroClass.Minstrel);
            Assert.That(Rules.FavoriteOf(HeroClass.Minstrel), Is.EqualTo("fish_soup"));
            loop.State.Cooking.Meals.Add("fish_soup");
            loop.State.Cooking.Meals.Add("carp_pie");
            loop.Cooking.Eat(0);
            Assert.That(loop.State.Cooking.ActiveTicksLeft, Is.EqualTo(Rules.Dish("fish_soup")!.Seconds * Rules.FavoriteMultiplier * GameLoop.TicksPerSecond));
            loop.Cooking.Eat(0);
            Assert.That(loop.State.Cooking.ActiveTicksLeft, Is.EqualTo(Rules.Dish("carp_pie")!.Seconds * GameLoop.TicksPerSecond), "a new meal replaces the old one");
            Assert.That(CookingView.Book(loop.Cooking).Single(d => d.Favorite).Id, Is.EqualTo("fish_soup"));
            Assert.That(HeroClassNames.ByName.Values.All(c => Rules.FavoriteOf(c).Length > 0), Is.True, "every character has a favorite dish");
        }

        [Test]
        public void PotionAndFoodTogetherStayUnderTheCap()
        {
            foreach (var dish in Rules.Dishes)
                foreach (var modifier in dish.Modifiers)
                    Assert.That(modifier.Value, Is.LessThanOrEqualTo(Rules.MaxShare));
            var json = Content.TestData.ReadGameDataFile(CookingRules.FileName).Replace("\"value\": 0.04 }] },\n    { \"id\": \"herb_balls\"", "\"value\": 0.05 }] },\n    { \"id\": \"herb_balls\"");
            Assert.DoesNotThrow(() => CookingRules.Load(json, TestWorld.Data.Camp, TestWorld.Data.Alchemy), "a fine storm draught 10% plus a 5% pie is 15%");
            var greedy = Content.TestData.ReadGameDataFile(CookingRules.FileName).Replace("\"maxShare\": 0.05", "\"maxShare\": 0.07").Replace("\"value\": 0.04 }] },\n    { \"id\": \"herb_balls\"", "\"value\": 0.06 }] },\n    { \"id\": \"herb_balls\"");
            var error = Assert.Throws<ContentException>(() => CookingRules.Load(greedy, TestWorld.Data.Camp, TestWorld.Data.Alchemy))!;
            Assert.That(error.Message, Does.Contain("a potion and food give at most 0.15"));
        }

        [Test]
        public void MealsAndTheStoveSurviveTheSave()
        {
            var (loop, _) = Cook();
            loop.State.Cooking.Meals.Add("ember_stew");
            loop.State.Cooking.Cooking = "fish_soup";
            loop.State.Cooking.Chops = 12;
            loop.State.Cooking.Book.Add("ember_stew");
            loop.Cooking.Eat(0);
            loop.State.Cooking.Meals.Add("fish_soup");
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(JsonWriter.Write(GameStateJson.Write(copy)), Is.EqualTo(JsonWriter.Write(GameStateJson.Write(loop.State))));
            var restored = TestWorld.Loop(new FakeInputSource(), copy);
            Assert.That(restored.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(loop.Hero.Value(CombatStats.Damage, 1)).Within(1e-9), "the meal buff comes back with the save");
        }

        [Test]
        public void CookingAndEatenMealsDoNotAllocate()
        {
            var (loop, input) = Cook();
            loop.State.Cooking.Meals.Add("ember_stew");
            loop.Cooking.Eat(0);
            for (var i = 0; i < 50; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Events.Clear();
            }
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Events.Clear();
            }
            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0), "battle with a meal");
            var dish = Rules.Dish("fish_soup")!;
            Stock(loop, dish);
            loop.Cooking.Cook(dish.Id);
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Peaceful.SetActivity(CookingSystem.Activity);
            loop.Tick();
            before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < dish.Chops / 2 - 2; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Events.Clear();
            }
            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0), "chopping in the camp");
        }

        [TestCase(@"""building"": ""chef_table""", @"""building"": ""oven""", "unknown building 'oven'")]
        [TestCase(@"""golemancer"": ""ember_stew""", @"""golemancer"": ""cake""", "unknown dish 'cake'")]
        [TestCase(@"""kind"": ""increased"", ""value"": 0.05 }] },
    { ""id"": ""carp_pie""", @"""kind"": ""flat"", ""value"": 0.05 }] },
    { ""id"": ""carp_pie""", "food gives only increased bonuses")]
        public void BrokenCookingIsReported(string from, string to, string message)
        {
            var json = Content.TestData.ReadGameDataFile(CookingRules.FileName).Replace(from, to);
            var error = Assert.Throws<ContentException>(() => CookingRules.Load(json, TestWorld.Data.Camp, TestWorld.Data.Alchemy))!;
            Assert.That(error.Message, Does.Contain(message));
        }
    }
}
