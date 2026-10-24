using System.Linq;
using Game.Core.Content.Gems;
using Game.Core.Content.Weapons;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class WeaponSwapTests
    {
        private GemDef _axeSkill = null!;
        private GemDef _otherSkill = null!;
        private WeaponBase _axe = null!;
        private WeaponBase _other = null!;

        [SetUp]
        public void SetUp()
        {
            var data = TestWorld.Real;
            Texts.UseLanguage("ru");
            var weaponSkills = data.Gems.All.Where(gem => gem.Skill?.Weapon != null).ToList();
            _axe = data.Weapons.StarterFor(HeroClass.Viking);
            _axeSkill = weaponSkills.First(gem => gem.Skill!.Weapon == _axe.Type.Id);
            _otherSkill = weaponSkills.First(gem => gem.Skill!.Weapon != _axe.Type.Id);
            _other = data.Weapons.Bases.First(weapon => weapon.Type.Id == _otherSkill.Skill!.Weapon);
        }

        [Test]
        public void NewTypeLightsUpItsSkillsAndTheReplacedTypeGoesOut()
        {
            var state = new GameState();
            state.Weapons.Right = _axe;
            state.Skills.Add(new SkillSlot(_axeSkill));
            state.Skills.Add(new SkillSlot(_otherSkill));

            var swap = WeaponSwap.For(state, _other, WeaponHand.Right);

            Assert.That(swap.Replaced, Is.SameAs(_axe));
            Assert.That(swap.LightUp, Is.EqualTo(new[] { _otherSkill.NameRu }));
            Assert.That(swap.GoOut, Is.EqualTo(new[] { _axeSkill.NameRu }));
            Assert.That(ItemTexts.GoOut(swap), Does.Contain(_axe.NameRu).And.Contain(_axeSkill.NameRu));
        }

        [Test]
        public void OtherHandKeepsItsSkillsLit()
        {
            var state = new GameState();
            state.Weapons.Right = _axe;
            state.Skills.Add(new SkillSlot(_axeSkill));

            var swap = WeaponSwap.For(state, _other, WeaponHand.Left);

            Assert.That(swap.Replaced, Is.Null);
            Assert.That(swap.GoOut, Is.Empty);
        }

        [Test]
        public void ArmorAndJewelryFiltersAreEmptyForNow()
        {
            var state = new GameState();
            state.Weapons.Bag.Add(_other);

            Assert.That(ItemTexts.Bag(state, ItemFilter.All), Has.Count.EqualTo(1));
            Assert.That(ItemTexts.Bag(state, ItemFilter.Weapons), Has.Count.EqualTo(1));
            Assert.That(ItemTexts.Bag(state, ItemFilter.Armor), Is.Empty);
            Assert.That(ItemTexts.Empty(ItemFilter.Armor), Is.EqualTo("Таких вещей пока нет"));
        }
    }
}
