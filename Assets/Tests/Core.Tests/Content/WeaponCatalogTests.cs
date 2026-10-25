using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Weapons;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class WeaponCatalogTests
    {
        private static WeaponCatalog Catalog => TestWorld.Real.Weapons;

        [Test]
        public void FourteenTypesOnePerCharacterAndFortyTwoBases()
        {
            Assert.That(Catalog.Types.Count, Is.EqualTo(14));
            Assert.That(Catalog.Bases.Count, Is.EqualTo(42));
            Assert.That(Catalog.Types.Select(type => type.HeroClass).Distinct().Count(), Is.EqualTo(HeroClassNames.ByName.Count));
        }

        [Test]
        public void EachTypeHasEarlyMiddleAndLateBaseAndOnlyTheLateOneHasASecondLine()
        {
            foreach (var type in Catalog.Types)
            {
                var bases = Catalog.Bases.Where(weaponBase => weaponBase.Type == type).OrderBy(weaponBase => weaponBase.FromBiome).ToList();
                Assert.That(bases.Select(weaponBase => weaponBase.FromBiome), Is.EqualTo(new[] { 1, 3, 5 }), type.Id);
                Assert.That(bases[0].Innate, Is.Empty, type.Id);
                Assert.That(bases[1].Innate, Is.Empty, type.Id);
                Assert.That(bases[2].Innate, Is.Not.Empty, type.Id);
                Assert.That(type.Innate, Is.Not.Empty, type.Id);
            }
        }

        [Test]
        public void StarterIsTheEarlyBaseOfTheOwnType()
        {
            var starter = Catalog.StarterFor(HeroClass.Viking);

            Assert.That(starter.Id, Is.EqualTo("woodcutter_axe"));
            Assert.That(starter.Type.Id, Is.EqualTo("axe"));
            Assert.That(Catalog.StarterFor(HeroClass.Captain).Id, Is.EqualTo("flintlock_pistol"));
        }

        [Test]
        public void StartingClassSkillsNeedTheirWeapon()
        {
            Assert.That(TestWorld.Real.Gems["iron_rim_bash"].Skill!.Weapon, Is.EqualTo("axe"));
            Assert.That(TestWorld.Real.Gems["hearth_song"].Skill!.Weapon, Is.EqualTo("electric_guitar"));
            Assert.That(TestWorld.Real.Gems["charm_prick"].Skill!.Weapon, Is.EqualTo("voodoo_doll"));
            Assert.That(TestWorld.Real.Gems["stenography"].Skill!.Weapon, Is.Null);
        }

        [Test]
        public void UnknownGemWeaponIsRejected()
        {
            var gems = GemCatalog.Load(TestData.ReadGameDataFile(GemCatalog.FileName).Replace("\"weapon\": \"axe\"", "\"weapon\": \"axee\""));

            var error = Assert.Throws<ContentException>(() => Catalog.RequireGemWeapons(gems))!;

            Assert.That(error.Message, Does.Contain("axee"));
        }

        [Test]
        public void EveryClassNeedsAType()
        {
            var json = TestData.ReadGameDataFile(WeaponCatalog.FileName).Replace("\"class\": \"captain\"", "\"class\": \"viking\"");

            Assert.Throws<ContentException>(() => WeaponCatalog.Load(json));
        }

        [Test]
        public void BaseOfUnknownTypeIsRejected()
        {
            var json = TestData.ReadGameDataFile(WeaponCatalog.FileName).Replace("\"type\": \"pistol\"", "\"type\": \"musket\"");

            Assert.Throws<ContentException>(() => WeaponCatalog.Load(json));
        }
    }
}
