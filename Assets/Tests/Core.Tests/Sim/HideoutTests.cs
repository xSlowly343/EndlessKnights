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
    public class HideoutTests
    {
        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        [Test]
        public void TheBaseTiersAndTheirBuildingsComeFromTheRules()
        {
            var rules = TestWorld.Real.Hideout;
            Assert.That(rules.Tiers.Count, Is.EqualTo(3));
            Assert.That(rules.Tiers[0].NameRu, Is.EqualTo("Привал"));
            Assert.That(rules.Tiers[1].SitHours, Is.EqualTo(2));
            var json = TestData.ReadGameDataFile(HideoutRules.FileName);
            Assert.Throws<ContentException>(() => HideoutRules.Load(json.Replace("\"opens\": [\"garden\", \"storage\"]", "\"opens\": [\"moat\"]")));
            Assert.Throws<ContentException>(() => HideoutRules.Load(json.Replace("\"bosses\": 50,", "\"bosses\": 5,")));
        }

        [Test]
        public void TheServerAnswerSetsTheTierBuildingsAndWhoIsByTheFire()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var hideout = loop.Hideout;
            Assert.That(HideoutView.TierName(hideout), Is.EqualTo("Пустой Привал — нужен отряд из двоих"));
            Assert.That(HideoutView.NextTier(hideout), Has.Member("в отряде 0 из 2"));

            hideout.Use(1, 2, 4, 0, 1800, 0, new[] { "Ann", "Bob" });
            Assert.That(HideoutView.TierName(hideout), Is.EqualTo("Привал"));
            Assert.That(HideoutView.Buildings(hideout), Is.EqualTo(new[] { "Костёр", "Уголок участника", "Уголок участника", "Стол Хроники" }));
            Assert.That(HideoutView.AtTheFire(hideout.State), Is.EqualTo("У костра: Ann, Bob"));
            var next = HideoutView.NextTier(hideout);
            Assert.That(next[0], Is.EqualTo("До ступени «Двор»:"));
            Assert.That(next, Has.Member("боссов вместе 4 из 10"));
            Assert.That(next, Has.Member("у костра вместе 0.5 из 2 ч"));

            hideout.Use(2, 2, 12, 0, 7300, 0, System.Array.Empty<string>());
            Assert.That(HideoutView.NextTier(hideout), Has.Member("заказов отряда 0 из 1"));
            Assert.That(HideoutView.NextTier(hideout), Has.Member("костёр 2-го уровня"));
            Assert.That(HideoutView.AtTheFire(hideout.State), Is.EqualTo("У костра никого"));

            hideout.Use(3, 2, 60, 1, 7300, 2, System.Array.Empty<string>());
            Assert.That(hideout.NextTier, Is.Null);
            Assert.That(HideoutView.NextTier(hideout), Is.Empty);
        }

        [Test]
        public void InTheHideoutNoPersonalCraftMovesButPeacefulActionsCount()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Peaceful.SetPlace(PeacefulPlace.Hideout);
            Assert.That(loop.Hideout.Inside, Is.True);
            input.PressKeys(6);
            loop.Tick();
            Assert.That(loop.Peaceful.Work(loop.Peaceful.Activity).Keys, Is.Zero);
            Assert.That(loop.Guilds.UnsentActions, Is.EqualTo(6));

            loop.Peaceful.SetPlace(PeacefulPlace.Refuge);
            input.PressKeys(4);
            loop.Tick();
            Assert.That(loop.Hideout.Inside, Is.False);
        }

        [Test]
        public void TheSaveKeepsThePlaceAndTheLastAnswerAndLeavingClearsThem()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Peaceful.SetPlace(PeacefulPlace.Hideout);
            loop.Hideout.Use(2, 3, 14, 0, 7300, 0, new[] { "Ann" });
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Peaceful.Place, Is.EqualTo(PeacefulPlace.Hideout));
            Assert.That(copy.Hideout.Tier, Is.EqualTo(2));
            Assert.That(copy.Hideout.SitSeconds, Is.EqualTo(7300));
            Assert.That(copy.Hideout.Here, Is.Empty, "who is by the fire is only known online");

            loop.Hideout.Leave();
            Assert.That(loop.Hideout.State.Tier, Is.Zero);
            Assert.That(loop.State.Peaceful.Place, Is.EqualTo(PeacefulPlace.Refuge));
        }
    }
}
