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

        private static GameLoop Ronin(int heroLevel)
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Ronin, HeroLevel = heroLevel });
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
            var places = Circle(Ronin(8));

            Assert.That(places.Select(place => place.Gem.Id), Is.EqualTo(TestWorld.Data.Weapons.Types.First(type => type.Id == "katana").StarterSkills));
            Assert.That(places.Select(place => place.Open), Is.EqualTo(new[] { true, true, true, false, false, false, false }));
        }

        [Test]
        public void PuttingAnOpenSkillInTheBookMarksItsPlace()
        {
            var loop = Ronin(1);

            Assert.That(loop.Book.PlaceFromWeapon(loop.Data.Gems["thin_cut"], Levels), Is.EqualTo(BookResult.Done));

            var place = Circle(loop)[0];
            Assert.That(place.InBook, Is.True);
            Assert.That(place.BookRow, Is.EqualTo(0));
            Assert.That(place.CanPlace, Is.False);
            Assert.That(loop.Book.PlaceFromWeapon(loop.Data.Gems["thin_cut"], Levels), Is.EqualTo(BookResult.AlreadyInBook));
        }

        [Test]
        public void AClosedPlaceOrAnotherWeaponsSkillStaysOutOfTheBook()
        {
            var loop = Ronin(1);

            Assert.That(loop.Book.PlaceFromWeapon(loop.Data.Gems["no_rush"], Levels), Is.EqualTo(BookResult.NotOnWeapon));
            Assert.That(loop.Book.PlaceFromWeapon(loop.Data.Gems["tempering"], Levels), Is.EqualTo(BookResult.NotOnWeapon));
            Assert.That(loop.State.Skills, Is.Empty);
        }

        [Test]
        public void ASkillWaitingForItsMechanicCannotBePlaced()
        {
            var loop = Ronin(50);
            var locked = loop.Data.Gems["hook_and_cleaver"];
            loop.State.Weapons.Right = new WeaponItem(loop.State.Weapons.Right!.Base, loop.State.Weapons.Right.Rarity, new[] { locked.Id });

            Assert.That(loop.Book.PlaceFromWeapon(locked, Levels), Is.EqualTo(BookResult.WaitsForMechanic));
            Assert.That(Circle(loop)[0].CanPlace, Is.False);
        }

        [Test]
        public void HandsListOnlyTheFilledOnes()
        {
            var loop = Ronin(1);
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
            var katana = TestWorld.Real.Weapons.Roll(TestWorld.Real.Weapons.StarterFor(HeroClass.Ronin), Game.Core.Content.Items.ItemRarity.Legendary, new Rng(3));

            Assert.That(MasterTexts.Hand(katana), Is.EqualTo(katana.NameRu + " · Легендарная"));
            Assert.That(MasterTexts.Tier(SkillTier.Rare), Is.EqualTo("Редкий"));
            Assert.That(MasterTexts.Line(gems["thin_cut"].Skill!), Is.EqualTo("быстрый · 20 · 3 с"));
            Assert.That(MasterTexts.Line(gems["forge_bellows"].Skill!), Is.EqualTo("аура, резерв 20"));
            Assert.That(MasterTexts.Line(gems["kitchen_rhythm"].Skill!), Is.EqualTo("скилл · 40 · 30 с"));
        }
    }
}
