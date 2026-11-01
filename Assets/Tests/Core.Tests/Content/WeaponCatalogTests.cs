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
        public void EighteenTypesOnePerCharacterAndFiftyFourBases()
        {
            Assert.That(Catalog.Types.Count, Is.EqualTo(18));
            Assert.That(Catalog.Bases.Count, Is.EqualTo(54));
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
            var starter = Catalog.StarterFor(HeroClass.Runedancer);

            Assert.That(starter.Id, Is.EqualTo("apprentice_daggers"));
            Assert.That(starter.Type.Id, Is.EqualTo("rune_daggers"));
            Assert.That(Catalog.StarterFor(HeroClass.Ghostcaptain).Id, Is.EqualTo("flintlock_pistol"));
        }

        [Test]
        public void StartingClassSkillsNeedTheirWeapon()
        {
            Assert.That(TestWorld.Real.Gems["runedancer_hit_1"].Skill!.Weapon, Is.EqualTo("rune_daggers"));
            Assert.That(TestWorld.Real.Gems["minstrel_aura_1"].Skill!.Weapon, Is.EqualTo("enchanted_lute"));
            Assert.That(TestWorld.Real.Gems["whisperer_hit_1"].Skill!.Weapon, Is.EqualTo("whisperer_doll"));
            Assert.That(TestWorld.Real.Gems["through_stride"].Skill!.Weapon, Is.Null);
        }

        [Test]
        public void UnknownGemWeaponIsRejected()
        {
            var gems = GemCatalog.Load(TestData.ReadGameDataFile(GemCatalog.FileName).Replace("\"weapon\": \"rune_daggers\"", "\"weapon\": \"axee\""));

            var error = Assert.Throws<ContentException>(() => Catalog.BindGems(gems))!;

            Assert.That(error.Message, Does.Contain("axee"));
        }

        [Test]
        public void EveryClassNeedsAType()
        {
            var json = TestData.ReadGameDataFile(WeaponCatalog.FileName).Replace("\"class\": \"ghostcaptain\"", "\"class\": \"runedancer\"");

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
