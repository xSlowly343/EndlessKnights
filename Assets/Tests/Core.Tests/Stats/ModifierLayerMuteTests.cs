using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Stats
{
    public class ModifierLayerMuteTests
    {
        [Test]
        public void AMutedLayerKeepsItsModifiersButAppliesNone()
        {
            var sheet = new StatSheet();
            var layer = new ModifierLayer(sheet);
            layer.Set(new[] { new Modifier(CombatStats.Damage, ModifierKind.Flat, 5) });
            Assert.That(sheet.Value(CombatStats.Damage, 1), Is.EqualTo(6));

            layer.Muted = true;
            Assert.That(sheet.Value(CombatStats.Damage, 1), Is.EqualTo(1));
            layer.Set(new[] { new Modifier(CombatStats.Damage, ModifierKind.Flat, 9) });
            Assert.That(sheet.Value(CombatStats.Damage, 1), Is.EqualTo(1), "rebuilt while muted");

            layer.Muted = false;
            Assert.That(sheet.Value(CombatStats.Damage, 1), Is.EqualTo(10));
        }
    }
}
