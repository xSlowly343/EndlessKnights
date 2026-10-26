using System.Collections.Generic;
using System.Linq;
using Game.Core.Content.Gems;
using Game.Core.Content.Weapons;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class WeaponPoolTests
    {
        private static readonly int[] TierSizes = { 12, 9, 6, 3, 5 };

        private static IEnumerable<WeaponType> FullPools() =>
            TestWorld.Real.Weapons.Types.Where(type => ClassSkills(type).Count() >= 35);

        private static IEnumerable<GemDef> ClassSkills(WeaponType type) =>
            TestWorld.Real.Gems.All.Where(gem => gem.Skill?.Weapon == type.Id && gem.Ascendancy == null);

        [Test]
        public void FilledPoolsHoldThirtyFiveSkillsSplitTwelveNineSixThreeFive()
        {
            Assert.That(FullPools().Select(type => type.Id), Does.Contain("katana"));
            foreach (var type in FullPools())
            {
                var skills = ClassSkills(type).ToList();
                Assert.That(skills, Has.Count.EqualTo(35), type.Id);
                for (var tier = 0; tier < TierSizes.Length; tier++)
                    Assert.That(skills.Count(gem => (int)gem.Skill!.Tier == tier), Is.EqualTo(TierSizes[tier]), type.Id + " tier " + (SkillTier)tier);
            }
        }

        [Test]
        public void EveryTierOfAFilledPoolHasAFastAndAHeavySkillToRoll()
        {
            foreach (var type in FullPools())
            {
                var pool = TestWorld.Real.Weapons.PoolOf(type);
                for (var tier = SkillTier.Common; tier <= SkillTier.Legendary; tier++)
                {
                    Assert.That(pool.Any(gem => gem.Skill!.Tier == tier && gem.Skill.Role == SkillRole.Fast), type.Id + " fast " + tier);
                    Assert.That(pool.Any(gem => gem.Skill!.Tier == tier && gem.Skill.Role == SkillRole.Heavy), type.Id + " heavy " + tier);
                }
            }
        }

        [Test]
        public void LockedSkillsStayOutOfTheRoll()
        {
            foreach (var type in TestWorld.Real.Weapons.Types)
                Assert.That(TestWorld.Real.Weapons.PoolOf(type).Where(gem => gem.Locked), Is.Empty, type.Id);
        }
    }
}
