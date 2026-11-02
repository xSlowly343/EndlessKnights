using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Tree;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class NewClassFeatureTests
    {
        [Test]
        public void TheFourClassesAreOfferedToPlayersWithAWeaponAStartAndThreePaths()
        {
            Assert.That(HeroClassNames.InDevelopment, Is.Empty);
            foreach (var heroClass in new[] { HeroClass.Mothcaller, HeroClass.Cartomancer, HeroClass.Herbwitch, HeroClass.Golemancer })
            {
                var name = HeroClassNames.Of(heroClass);
                Assert.That(HeroClassNames.ByName[name], Is.EqualTo(heroClass));
                Assert.That(TestWorld.Data.ClassTreeOf(name), Is.Not.Null, name);
                Assert.That(TestWorld.Data.Weapons.Types.Count(type => type.HeroClass == heroClass), Is.EqualTo(1), name);
                Assert.That(TestWorld.Data.Ascendancy.SubclassesOf(name).Count, Is.EqualTo(3), name);
                Assert.That(TestWorld.Data.SkillBook.StartingGems(name), Is.Not.Empty, name);
            }
        }
    }
}
