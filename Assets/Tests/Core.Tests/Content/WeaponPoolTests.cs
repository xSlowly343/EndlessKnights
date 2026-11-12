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
        private static IEnumerable<GemDef> ClassSkills(WeaponType type) =>
            TestWorld.Real.Gems.All.Where(gem => gem.Skill?.Weapon == type.Id && gem.Ascendancy == null);

        [Test]
        public void EveryPoolHoldsFourFamiliesOfThreeFormsAndFourAuras()
        {
            foreach (var type in TestWorld.Real.Weapons.Types)
            {
                var skills = ClassSkills(type).ToList();
                var families = skills.Where(gem => gem.Type == SkillType.Active && gem.Root == gem).ToList();
                Assert.That(families, Has.Count.EqualTo(SkillRoster.FamiliesPerClass), type.Id);
                Assert.That(families.Select(root => root.Forms.Count), Is.All.EqualTo(SkillRoster.FormsPerFamily), type.Id);
                Assert.That(skills.Count(gem => gem.Type == SkillType.Active), Is.EqualTo(SkillRoster.ActivePerClass), type.Id);
                Assert.That(skills.Count(gem => gem.Type == SkillType.Aura), Is.EqualTo(SkillRoster.AurasPerClass), type.Id);
                Assert.That(skills.Where(gem => gem.Branches.Count == 0), Is.Empty, type.Id + ": every class skill has branches");
            }
        }

        [Test]
        public void EveryFamilyKeepsOneRoleAndEveryClassHasAFastAndAHeavyFamily()
        {
            foreach (var type in TestWorld.Real.Weapons.Types)
            {
                var families = ClassSkills(type).Where(gem => gem.Type == SkillType.Active && gem.Root == gem).ToList();
                foreach (var root in families)
                    Assert.That(root.Forms.Select(form => form.Skill!.Role).Distinct().Count(), Is.EqualTo(1), root.Id);
                Assert.That(families.Any(root => root.Skill!.Role == SkillRole.Fast), type.Id + " fast");
                Assert.That(families.Any(root => root.Skill!.Role == SkillRole.Heavy), type.Id + " heavy");
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
