using System.Linq;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class HeroSummaryTests
    {
        [SetUp]
        public void UseRussian()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        [Test]
        public void TitleNamesTheClassAndLevel()
        {
            var state = new GameState { Class = HeroClass.Runedancer, HeroLevel = 42 };

            Assert.That(HeroSummary.Title(state), Is.EqualTo("Runedancer · ур. 42"));
        }

        [Test]
        public void EveryClassHasAName()
        {
            foreach (var heroClass in HeroClassNames.ByName.Values)
                Assert.That(HeroSummary.ClassName(heroClass), Does.Not.StartWith("class."), heroClass.ToString());
        }

        [Test]
        public void StarterWeaponIsShownInTheWeaponSlot()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Runedancer });
            var starter = loop.Weapons.Give(loop.Data.Weapons.StarterFor(HeroClass.Runedancer));
            loop.Weapons.Equip(starter);

            var hand = HeroSummary.Hand(loop);

            Assert.That(hand.Name, Is.EqualTo(starter.NameRu));
            Assert.That(hand.Color, Is.EqualTo(starter.Type.Color));
            Assert.That(hand.IsEmpty, Is.False);

            loop.Weapons.Unequip();

            Assert.That(HeroSummary.Hand(loop).Name, Is.EqualTo("пусто"));
        }

        [Test]
        public void SixTilesWithKeyDamageFirst()
        {
            var loop = TestWorld.Loop();

            var tiles = HeroSummary.Tiles(loop, NumberFormatSettings.Default);

            Assert.That(tiles.Count, Is.EqualTo(6));
            Assert.That(tiles[0].Label, Is.EqualTo("Урон за удар"));
            Assert.That(tiles.All(tile => tile.Value.Length > 0), Is.True);
            Assert.That(tiles[2].Value, Does.Contain("%").And.Contain("×"));
        }

        [Test]
        public void HintPointsToFreeTreePoints()
        {
            var state = new GameState();
            Assert.That(HeroSummary.Hint(state), Is.Null);

            state.TreePointsEarned = 3;

            Assert.That(HeroSummary.Hint(state), Is.EqualTo("Свободных очков дерева: 3"));
        }
    }
}
