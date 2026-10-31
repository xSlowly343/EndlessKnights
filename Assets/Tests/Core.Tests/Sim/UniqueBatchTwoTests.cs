using Game.Core.Content.Items;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class UniqueBatchTwoTests
    {
        private static GameLoop Wearing(string id)
        {
            var loop = TestWorld.Loop(state: new GameState(4) { Class = HeroClass.Viking });
            Assert.That(TestWorld.Data.Items.TryGetUnique(id, out var unique), Is.True, id);
            var item = new GearItem(unique, 3);
            loop.State.Drops.Gear.Add(item);
            Assert.That(loop.Gear.Equip(item), Is.True);
            return loop;
        }

        [Test]
        public void WarbandHelmStrengthensTheParty()
        {
            var loop = Wearing("warband_helm");

            Assert.That(loop.Hero.TotalsOf(new StatId("partyDamage")).IncreasedMultiplier, Is.EqualTo(1.2).Within(1e-9));
        }

        [Test]
        public void HeadsmanMaskCullsWeakEnemies()
        {
            var loop = Wearing("headsman_mask");

            Assert.That(loop.Hero.Value(CombatStats.CullThreshold, 0), Is.EqualTo(0.05).Within(1e-9));
        }

        [Test]
        public void ShadowTwinRingRepeatsKeyHits()
        {
            var plain = SkillTestHero.Of(HeroClass.Viking);
            var ringed = SkillTestHero.Of(HeroClass.Viking);
            Assert.That(TestWorld.Data.Items.TryGetUnique("shadow_twin_ring", out var ring), Is.True);
            var item = new GearItem(ring, 3);
            ringed.State.Drops.Gear.Add(item);
            ringed.Loop.Gear.Equip(item);

            plain.Ticks(60, 1);
            ringed.Ticks(60, 1);

            Assert.That(plain.DamageFrom(DamageSource.Skill), Is.Zero);
            Assert.That(ringed.DamageFrom(DamageSource.Skill), Is.GreaterThan(0));
        }
    }
}
