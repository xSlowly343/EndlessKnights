using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ClanCampTests
    {
        private const long Hour = 3_600_000L;
        private static ClanCampRules Rules => TestWorld.Real.ClanCamp;

        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        private static double Damage(GameLoop loop) => loop.Hero.Value(CombatStats.Damage, 1);

        [Test]
        public void LevelsNeedMorePointsAndTheFeastTargetFollowsTheClanSize()
        {
            Assert.That(Rules.LevelAt(0), Is.EqualTo(0));
            Assert.That(Rules.LevelAt(Rules.Levels[0].Points), Is.EqualTo(1));
            Assert.That(Rules.LevelAt(Rules.Levels[Rules.MaxLevel - 1].Points + 1), Is.EqualTo(Rules.MaxLevel));
            Assert.That(Rules.Feast.Target(2), Is.EqualTo(Rules.Feast.MinDishes));
            Assert.That(Rules.Feast.Target(30), Is.EqualTo(Rules.Feast.MaxDishes));
            Assert.That(Rules.PointsOf("herb_windwort"), Is.EqualTo(1));
            Assert.That(Rules.PointsOf("fish_hills_roach"), Is.EqualTo(0));
        }

        [Test]
        public void BrokenCampRulesAreRejected()
        {
            var json = TestData.ReadGameDataFile(ClanCampRules.FileName);
            Assert.Throws<ContentException>(() => ClanCampRules.Load(json.Replace("\"points\": 1500,", "\"points\": 400,")));
            Assert.Throws<ContentException>(() => ClanCampRules.Load(json.Replace("{ \"item\": \"herb_windwort\", \"points\": 1 }", "{ \"item\": \"herb_knockbloom\", \"points\": 1 }")));
            Assert.Throws<ContentException>(() => ClanCampRules.Load(json.Replace("{ \"stat\": \"attackPower\", \"kind\": \"increased\", \"value\": 0.03 }", "{ \"stat\": \"attackPower\", \"kind\": \"more\", \"value\": 0.03 }")));
        }

        [Test]
        public void TheCampFireAndTheFeastBuffTheHeroWhileTheAnswerIsFresh()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var plain = Damage(loop);
            var camp = loop.ClanCamp;

            camp.Use(2, 0, 1000);
            Assert.That(camp.Active, Is.True);
            Assert.That(camp.Feasting, Is.False);
            var built = Damage(loop);
            Assert.That(built, Is.GreaterThan(plain));

            camp.Use(2, 1000 + 48 * Hour, 1000);
            Assert.That(camp.Feasting, Is.True);
            Assert.That(Damage(loop), Is.GreaterThan(built));

            camp.Refresh(1000 + (Rules.OfflineHours + 1) * Hour);
            Assert.That(camp.Active, Is.False);
            Assert.That(Damage(loop), Is.EqualTo(plain).Within(1e-9), "a stale answer gives nothing");

            camp.Use(2, 1000 + 48 * Hour, 1000 + 47 * Hour);
            camp.Refresh(1000 + 49 * Hour);
            Assert.That(camp.Active, Is.True);
            Assert.That(camp.Feasting, Is.False, "the feast ends with the raid week");
            Assert.That(Damage(loop), Is.EqualTo(built).Within(1e-9));

            camp.Clear(1000 + 50 * Hour);
            Assert.That(Damage(loop), Is.EqualTo(plain).Within(1e-9));
        }

        [Test]
        public void MaterialsAndMealsLeaveTheBagAndComeBackIfTheServerRefuses()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var camp = loop.ClanCamp;
            loop.State.Camp.Bag["herb_windwort"] = 5;

            Assert.That(camp.PackMaterial("herb_windwort", 6), Is.EqualTo(ClanCampResult.NotEnough));
            Assert.That(camp.PackMaterial("fish_hills_roach", 1), Is.EqualTo(ClanCampResult.NotAMaterial));
            Assert.That(camp.PackMaterial("herb_windwort", Rules.MaxGive + 1), Is.EqualTo(ClanCampResult.BadAmount));
            Assert.That(camp.PackMaterial("herb_windwort", 5), Is.EqualTo(ClanCampResult.Done));
            Assert.That(loop.State.Camp.Bag.ContainsKey("herb_windwort"), Is.False);
            Assert.That(camp.ReturnMaterial("herb_windwort", 5), Is.EqualTo(ClanCampResult.Done));
            Assert.That(loop.State.Camp.Bag["herb_windwort"], Is.EqualTo(5));

            loop.State.Cooking.Meals.Add("fish_soup");
            Assert.That(camp.PackMeal(3, out _), Is.EqualTo(ClanCampResult.NoSuchMeal));
            Assert.That(camp.PackMeal(0, out var dish), Is.EqualTo(ClanCampResult.Done));
            Assert.That(dish, Is.EqualTo("fish_soup"));
            Assert.That(loop.State.Cooking.Meals, Is.Empty);
            camp.ReturnMeal(dish);
            Assert.That(loop.State.Cooking.Meals, Is.EqualTo(new[] { "fish_soup" }));
        }

        [Test]
        public void TheSaveKeepsTheCampAnswerAndTheViewDescribesIt()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.ClanCamp.Use(3, 9_000_000_000_000, 8_000_000_000_000);
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.ClanCamp.Level, Is.EqualTo(3));
            Assert.That(copy.ClanCamp.FeastUntilUnixMs, Is.EqualTo(9_000_000_000_000));
            Assert.That(copy.ClanCamp.AnsweredUnixMs, Is.EqualTo(8_000_000_000_000));

            Assert.That(ClanCampView.LevelName(Rules, 0), Is.EqualTo("Пустая поляна"));
            Assert.That(ClanCampView.LevelName(Rules, 1), Is.EqualTo("Костёр"));
            Assert.That(ClanCampView.Progress(Rules, 120), Is.EqualTo("120 из 500 — до «Костёр»"));
            Assert.That(ClanCampView.Feast(Rules, 1, 0, 5, false), Is.EqualTo("Пир откроется с постройкой «Пиршественный стол»"));
            Assert.That(ClanCampView.Feast(Rules, 2, 3, 5, false), Is.EqualTo("На столе 3 из 5 блюд — пир на всю рейд-неделю"));
            loop.State.Camp.Bag["ore_sky"] = 2;
            var rows = ClanCampView.Materials(Rules, new CampSystem(TestWorld.Real.Camp, loop.State, loop.Events));
            var sky = rows.Find(row => row.Item == "ore_sky");
            Assert.That(sky.InBag, Is.EqualTo(2));
            Assert.That(sky.Points, Is.EqualTo(4));
            Assert.That(sky.CanGive, Is.True);
        }
    }
}
