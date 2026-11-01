using System.Collections.Generic;
using System.Linq;
using Game.Core.Build;
using Game.Core.Content.Gems;
using Game.Core.Content.Weapons;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class MasterCircleTests
    {
        [SetUp]
        public void SetUp() => Texts.UseLanguage("ru");

        private static GameLoop Duelist(int heroLevel)
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Duelist, HeroLevel = heroLevel });
            while (loop.State.Skills.Count > 0)
                loop.Book.Unequip(0);
            return loop;
        }

        private static IReadOnlyList<int> Levels => TestWorld.Data.Weapons.SkillRules.UnlockLevels;

        private static List<MasterPlace> Circle(GameLoop loop)
        {
            var places = new List<MasterPlace>();
            MasterCircle.Fill(loop.State.Weapons.Right!, loop.State.HeroLevel, Levels, loop.Data.Gems, loop.Book, places);
            return places;
        }

        [Test]
        public void TheCircleShowsAllSevenPlacesAndGreysTheClosedOnes()
        {
            var places = Circle(Duelist(8));

            Assert.That(places.Select(place => place.Gem.Id), Is.EqualTo(TestWorld.Data.Weapons.Types.First(type => type.Id == "spectral_rapier").StarterSkills));
            Assert.That(places.Select(place => place.Open), Is.EqualTo(new[] { true, true, true, false, false, false, false }));
        }

        [Test]
        public void PuttingAnOpenSkillInTheBookMarksItsPlace()
        {
            var loop = Duelist(1);

            Assert.That(loop.Book.PlaceFromWeapon(loop.Data.Gems["duelist_hit_1"], Levels), Is.EqualTo(BookResult.Done));

            var place = Circle(loop)[0];
            Assert.That(place.InBook, Is.True);
            Assert.That(place.BookRow, Is.EqualTo(0));
            Assert.That(place.CanPlace, Is.False);
            Assert.That(loop.Book.PlaceFromWeapon(loop.Data.Gems["duelist_hit_1"], Levels), Is.EqualTo(BookResult.AlreadyInBook));
        }

        [Test]
        public void AClosedPlaceOrAnotherWeaponsSkillStaysOutOfTheBook()
        {
            var loop = Duelist(1);

            Assert.That(loop.Book.PlaceFromWeapon(loop.Data.Gems["duelist_queue_1"], Levels), Is.EqualTo(BookResult.NotOnWeapon));
            Assert.That(loop.Book.PlaceFromWeapon(loop.Data.Gems["runedancer_strong_3"], Levels), Is.EqualTo(BookResult.NotOnWeapon));
            Assert.That(loop.State.Skills, Is.Empty);
        }

        [Test]
        public void HandsListOnlyTheFilledOnes()
        {
            var loop = Duelist(1);
            var hands = new List<WeaponHand>();

            MasterCircle.Hands(loop.State, hands);

            Assert.That(hands, Is.EqualTo(new[] { WeaponHand.Right }));
        }

        [Test]
        public void TheFirstPlaceSitsOnTopOfTheCircle()
        {
            var (x, y) = MasterCircle.Position(0, 7, 92);

            Assert.That(x, Is.EqualTo(0).Within(1e-9));
            Assert.That(y, Is.EqualTo(-92).Within(1e-9));
        }

        [Test]
        public void TextsNameTheHandTierAndKind()
        {
            var gems = TestWorld.Real.Gems;
            var katana = TestWorld.Real.Weapons.Roll(TestWorld.Real.Weapons.StarterFor(HeroClass.Duelist), Game.Core.Content.Items.ItemRarity.Legendary, new Rng(3));

            Assert.That(MasterTexts.Hand(katana), Is.EqualTo(katana.NameRu + " · Легендарная"));
            Assert.That(MasterTexts.Tier(SkillTier.Rare), Is.EqualTo("Редкий"));
            Assert.That(MasterTexts.Line(gems["duelist_hit_1"].Skill!), Is.EqualTo("быстрый · 10 · 3 с"));
            Assert.That(MasterTexts.Line(gems["runedancer_aura_3"].Skill!), Is.EqualTo("аура, резерв 20"));
            Assert.That(MasterTexts.Line(gems["duelist_auto_1"].Skill!), Is.EqualTo("скилл · 10 · 4 с"));
        }
    }
}
