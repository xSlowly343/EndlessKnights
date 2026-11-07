using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class TalentTests
    {
        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        private static GameLoop LoopAs(HeroClass heroClass, FakeInputSource? input = null)
        {
            var loop = TestWorld.RealLoop(input);
            loop.State.ClassChosen = true;
            loop.State.Class = heroClass;
            loop.Tick();
            return loop;
        }

        [Test]
        public void EveryCharacterHasOneTalentAndBrokenTalentsAreRejected()
        {
            var rules = TestWorld.Real.Talents;
            Assert.That(rules.Count, Is.EqualTo(HeroClassNames.ByName.Count));
            var json = TestData.ReadGameDataFile(TalentRules.FileName);
            Assert.Throws<ContentException>(() => TalentRules.Load(json.Replace("\"class\": \"golemancer\"", "\"class\": \"huscarl\"")));
            Assert.Throws<ContentException>(() => TalentRules.Load(json.Replace("\"class\": \"golemancer\"", "\"class\": \"herbwitch\"")));
            Assert.Throws<ContentException>(() => TalentRules.Load(json.Replace("{ \"perk\": \"shorterTrips\", \"value\": 0.2 }", "{ \"perk\": \"shorterTrips\", \"value\": 0.9 }")));
            Assert.Throws<ContentException>(() => TalentRules.Load(json.Replace("{ \"perk\": \"shorterTrips\", \"value\": 0.2 }", "{ \"perk\": \"fasterHorses\", \"value\": 0.2 }")));
            Assert.Throws<ContentException>(() => TalentRules.Load(json.Replace("\"perks\": [{ \"perk\": \"longerRoad\", \"value\": 0.5 }]", "\"waits\": \"17.18\", \"perks\": [{ \"perk\": \"longerRoad\", \"value\": 0.5 }]")));
        }

        [Test]
        public void TheTalentFollowsTheCurrentCharacter()
        {
            var loop = LoopAs(HeroClass.Clockwright);
            Assert.That(loop.State.Perk(MuseumPerk.ShorterTrips), Is.EqualTo(0.2).Within(1e-9));
            Assert.That(TalentView.Name(loop.Talents.Current!), Is.EqualTo("Точные часы"));
            Assert.That(TalentView.Status(loop.Talents.Current!), Is.EqualTo("Работает, пока играешь этим персонажем"));

            loop.State.Class = HeroClass.Herbwitch;
            loop.Tick();
            Assert.That(loop.State.Perk(MuseumPerk.ShorterTrips), Is.Zero);
            Assert.That(loop.State.Perk(MuseumPerk.GrowFaster), Is.EqualTo(0.15).Within(1e-9));
            Assert.That(loop.State.Perk(MuseumPerk.ExtraHerb), Is.EqualTo(1));

            loop.State.Class = HeroClass.Golemancer;
            loop.Tick();
            Assert.That(loop.State.Perk(MuseumPerk.Reputation), Is.EqualTo(0.25).Within(1e-9));
            Assert.That(loop.Talents.Current!.Works, Is.True);

            loop.State.ClassChosen = false;
            loop.Tick();
            Assert.That(loop.Talents.Current, Is.Null);
            Assert.That(loop.State.Perk(MuseumPerk.GrowFaster), Is.Zero, "a novice has no talent");
        }

        [Test]
        public void EveryTalentWorksAndTheSandwalkerTakesTheLongRoad()
        {
            var loop = LoopAs(HeroClass.Sandwalker);
            Assert.That(loop.Talents.Current!.Works, Is.True);
            Assert.That(loop.State.Perk(MuseumPerk.LongerRoad), Is.EqualTo(0.5).Within(1e-9));
            foreach (var pair in HeroClassNames.ByName)
                Assert.That(TestWorld.Real.Talents.Of(pair.Value)!.Works, Is.True, pair.Key);
        }

        [Test]
        public void TheLanternbearerBuildsTheTownHalfAgainFaster()
        {
            var input = new FakeInputSource();
            var loop = LoopAs(HeroClass.Lanternbearer, input);
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            input.PressKeys(3);
            loop.Tick();
            input.PressKeys(3);
            loop.Tick();
            Assert.That(loop.Guilds.UnsentActions, Is.EqualTo(9));
        }

        [Test]
        public void TheDreamcatcherSeesTheForecastWithoutTheSkySquare()
        {
            var loop = LoopAs(HeroClass.Dreamcatcher);
            loop.State.RecordStage = 1;
            Assert.That(loop.Guilds.ObservatoryOpen, Is.True);
            loop.State.Class = HeroClass.Duelist;
            loop.Tick();
            Assert.That(loop.Guilds.ObservatoryOpen, Is.False);
        }

        [Test]
        public void TheCartomancerSwapsOneOrderADayAndTheSaveRemembersIt()
        {
            var loop = LoopAs(HeroClass.Cartomancer);
            loop.State.RecordStage = 1000;
            loop.Town.SetDay(20400);
            var before = loop.State.Town.Orders[0].Request;
            Assert.That(loop.Town.CanRedraw, Is.True);
            Assert.That(loop.Town.Redraw(9), Is.EqualTo(TownResult.NoOrder));
            Assert.That(loop.Town.Redraw(0), Is.EqualTo(TownResult.Done));
            Assert.That(loop.State.Town.Orders.Count, Is.EqualTo(3));
            Assert.That(loop.State.Town.Orders.Exists(order => order.Request == before), Is.False);
            Assert.That(loop.Town.Redraw(0), Is.EqualTo(TownResult.NoRedraw));

            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Town.RedrawDay, Is.EqualTo(20400));
            loop.Town.SetDay(20401);
            Assert.That(loop.Town.CanRedraw, Is.True, "a new day gives a new swap");

            loop.State.Class = HeroClass.Duelist;
            loop.Tick();
            Assert.That(loop.Town.Redraw(0), Is.EqualTo(TownResult.NoRedraw), "only the Cartomancer swaps");
        }
    }
}
