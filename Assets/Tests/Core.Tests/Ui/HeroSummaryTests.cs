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
            var state = new GameState { Class = HeroClass.Viking, HeroLevel = 42 };

            Assert.That(HeroSummary.Title(state), Is.EqualTo("Викинг · ур. 42"));
        }

        [Test]
        public void EveryClassHasAName()
        {
            foreach (var heroClass in HeroClassNames.ByName.Values)
                Assert.That(HeroSummary.ClassName(heroClass), Does.Not.StartWith("class."), heroClass.ToString());
        }

        [Test]
        public void StarterWeaponIsShownInTheRightHand()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Viking });
            var starter = loop.Data.Weapons.StarterFor(HeroClass.Viking);
            loop.Weapons.Give(starter);
            loop.Weapons.Equip(WeaponHand.Right, starter);

            var right = HeroSummary.Hand(loop, WeaponHand.Right);
            var left = HeroSummary.Hand(loop, WeaponHand.Left);

            Assert.That(right.Name, Is.EqualTo(starter.NameRu));
            Assert.That(right.Color, Is.EqualTo(starter.Type.Color));
            Assert.That(left.IsEmpty, Is.True);
            Assert.That(left.Name, Is.EqualTo("пусто"));
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

            state.Tree.PointsEarned = 3;

            Assert.That(HeroSummary.Hint(state), Is.EqualTo("Свободных очков дерева: 3"));
        }
    }
}
