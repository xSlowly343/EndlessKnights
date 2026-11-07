using Game.Core.Content.Items;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class DamageGroupTests
    {
        [Test]
        public void TheOldDamageStatIsNowAttackPower()
        {
            Assert.That(CombatStats.Damage.Name, Is.EqualTo("attackPower"));
        }

        [Test]
        public void AdditionalDamageMultipliesEveryHitAsItsOwnGroup()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.AdditionalDamage, ModifierKind.Flat, 0.1));
            hero.Add(new Modifier(CombatStats.AdditionalDamage, ModifierKind.Flat, 0.05));
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1), hero);
            loop.Tick();
            Assert.That(loop.Combat.Additional, Is.EqualTo(1.15).Within(1e-9));
            Assert.That(loop.Combat.BaseKeyDamage, Is.EqualTo(TestWorld.Data.Combat.StrikeDamage * 1.15).Within(1e-9));
        }

        [Test]
        public void WeaponQualityAddsUpToThirtyPercent()
        {
            var rules = TestWorld.Data.Combat.Attack;
            Assert.That(rules.QualityBonus(0), Is.Zero);
            Assert.That(rules.QualityBonus(100), Is.EqualTo(0.3).Within(1e-9));
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            loop.State.Weapons.Held!.Quality = 50;
            loop.Tick();
            Assert.That(loop.Combat.Additional, Is.EqualTo(1.15).Within(1e-9));
        }

        [Test]
        public void DroppedWeaponsRollAQuality()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            for (var i = 0; i < 200; i++)
            {
                var quality = DropSystem.QualityRoll(i, 100);
                Assert.That(quality, Is.InRange(0, 100));
                seen.Add(quality);
            }
            Assert.That(seen.Count, Is.GreaterThan(50));
        }

        [Test]
        public void TheForgeRerollKeepsTheBetterQuality()
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            var weapon = loop.State.Weapons.Held!;
            weapon.Quality = 90;
            Assert.That(loop.Forge.RerollQuality(), Is.EqualTo(ForgeResult.NoShards));
            loop.State.Drops.Shards = TestWorld.Data.Combat.Attack.QualityRerollShards * 20;
            for (var i = 0; i < 20; i++)
                loop.Forge.RerollQuality();
            Assert.That(weapon.Quality, Is.GreaterThanOrEqualTo(90));
            weapon.Quality = 100;
            Assert.That(loop.Forge.RerollQuality(), Is.EqualTo(ForgeResult.Maxed));
        }

        [Test]
        public void QualitySurvivesASave()
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            loop.State.Weapons.Held!.Quality = 42;
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty),
                TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Weapons.Held!.Quality, Is.EqualTo(42));
        }
    }
}
