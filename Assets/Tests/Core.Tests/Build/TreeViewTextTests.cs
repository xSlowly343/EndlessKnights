using Game.Core.Build;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class TreeViewTextTests
    {
        [Test]
        public void DescribesModifierKinds()
        {
            Assert.That(ModifierText.Describe(new Modifier(new StatId("damage"), ModifierKind.Increased, 0.05)), Is.EqualTo("+5% урон"));
            Assert.That(ModifierText.Describe(new Modifier(new StatId("damage"), ModifierKind.More, 0.08)), Is.EqualTo("ещё +8% урон"));
            Assert.That(ModifierText.Describe(new Modifier(new StatId("strength"), ModifierKind.Flat, 10)), Is.EqualTo("+10 Сила"));
            Assert.That(ModifierText.Describe(new Modifier(new StatId("critMultiplier"), ModifierKind.Flat, 0.2)), Is.EqualTo("+20% множитель крита"));
            Assert.That(ModifierText.Describe(new Modifier(new StatId("bossTimerSeconds"), ModifierKind.Flat, -10)), Is.EqualTo("-10 с таймер босса"));
            Assert.That(ModifierText.Describe(new Modifier(new StatId("damage"), ModifierKind.Increased, 0.1, StatTags.Click)), Is.EqualTo("+10% урон от кликов"));
        }
    }
}
