using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyGrowthLayerTests
    {
        private const long Day = 86_400_000;
        private const long Now = 1_763_000_000_000;

        private static PartyGrowthRules Rules => TestWorld.Data.Party.Growth;

        private static Modifier Damage(double value, ModifierKind kind = ModifierKind.Increased) => new Modifier(CombatStats.Damage, kind, value);

        private static double DamageOf(GameLoop loop) => loop.Hero.Value(CombatStats.Damage, 1);

        [Test]
        public void TheBasketRulesAreRead()
        {
            Assert.That(Rules.DamageCap, Is.EqualTo(3));
            Assert.That(Rules.PeacefulCap, Is.EqualTo(2));
            Assert.That(Rules.NewcomerShare(0), Is.EqualTo(0.3));
            Assert.That(Rules.NewcomerShare(1), Is.EqualTo(0.6));
            Assert.That(Rules.NewcomerShare(2), Is.EqualTo(1));
            Assert.That(Rules.NewcomerShare(40), Is.EqualTo(1));
            Assert.That(Rules.RejoinHours, Is.EqualTo(24));
            Assert.That(Rules.KickedRejoinHours, Is.EqualTo(8));
            Assert.That(Rules.TreeNodes, Is.EqualTo(50));
            Assert.That(Rules.MaxVotes, Is.EqualTo(3));
            Assert.That(Rules.FreeResetDays, Is.EqualTo(7));
            Assert.That(Rules.ResetFee(2000, false), Is.EqualTo(200));
            Assert.That(Rules.ResetFee(2000, true), Is.EqualTo(0));
        }

        [Test]
        public void WithoutAPartyThereIsNoPartyPower()
        {
            var loop = TestWorld.Loop();
            var before = DamageOf(loop);
            loop.SetPartyGrowthSources(new[] { Damage(1) });
            Assert.That(DamageOf(loop), Is.EqualTo(before).Within(1e-9));
            Assert.That(loop.PartyGrowth.DamageMultiplier, Is.EqualTo(1));
        }

        [Test]
        public void ThePartyBasketMultipliesTheOthersAndWorksWithNobodyOnline()
        {
            var loop = TestWorld.Loop();
            var general = new ModifierLayer(loop.Hero);
            general.Set(new[] { Damage(1) });
            var before = DamageOf(loop);
            loop.State.PartyAllies = 0;
            loop.SetPartyStanding(true, Now - 10 * Day, 4, Now);
            loop.SetPartyGrowthSources(new[] { Damage(1) });
            Assert.That(DamageOf(loop) / before, Is.EqualTo(2).Within(1e-9), "its own basket: ×2 on top, not +100% into the general one");
            Assert.That(loop.PartyGrowth.Members, Is.EqualTo(4), "members, not who is online");
            Assert.That(loop.PartyGrowth.DamageMultiplier, Is.EqualTo(2).Within(1e-9));
        }

        [Test]
        public void ThePartyLayerNeverGoesAboveItsCap()
        {
            var loop = TestWorld.Loop();
            var before = DamageOf(loop);
            loop.SetPartyStanding(true, 0, 2, Now);
            loop.SetPartyGrowthSources(new[] { Damage(2), Damage(0.5, ModifierKind.More) });
            Assert.That(loop.PartyGrowth.Capped, Is.True);
            Assert.That(loop.PartyGrowth.DamageMultiplier, Is.EqualTo(Rules.DamageCap));
            Assert.That(DamageOf(loop) / before, Is.EqualTo(Rules.DamageCap).Within(1e-9));
        }

        [Test]
        public void ANewcomerGrowsIntoThePartyPowerOverThreeDays()
        {
            var loop = TestWorld.Loop();
            var before = DamageOf(loop);
            loop.SetPartyGrowthSources(new[] { Damage(1) });
            loop.SetPartyStanding(true, Now, 3, Now);
            Assert.That(DamageOf(loop) / before, Is.EqualTo(1.3).Within(1e-9), "30% on the first day");
            loop.SetServerTime(Now + Day + 1);
            Assert.That(DamageOf(loop) / before, Is.EqualTo(1.6).Within(1e-9), "60% on the second");
            loop.SetServerTime(Now + 2 * Day + 1);
            Assert.That(DamageOf(loop) / before, Is.EqualTo(2).Within(1e-9), "all of it from the third");
            loop.SetPartyStanding(false, 0, 0, Now + 3 * Day);
            Assert.That(DamageOf(loop), Is.EqualTo(before).Within(1e-9), "leaving takes it away");
        }

        [Test]
        public void ThePeacefulBasketHasItsOwnCap()
        {
            var loop = TestWorld.Loop();
            loop.SetPartyStanding(true, 0, 2, Now);
            loop.SetPartyGrowthSources(new[] { new Modifier(CombatStats.PartyPeaceful, ModifierKind.Flat, 0.4) });
            Assert.That(loop.State.PeacefulBonus.Party, Is.EqualTo(0.4).Within(1e-9));
            loop.SetPartyGrowthSources(new[] { new Modifier(CombatStats.PartyPeaceful, ModifierKind.Flat, 1.5) });
            Assert.That(loop.PartyGrowth.Peaceful, Is.EqualTo(Rules.PeacefulCap - 1).Within(1e-9));
            Assert.That(loop.State.PeacefulBonus.Party, Is.EqualTo(Rules.PeacefulCap - 1).Within(1e-9));
        }

        [Test]
        public void PassingTheLayersOwnListsKeepsThem()
        {
            var loop = TestWorld.Loop();
            loop.SetPartyStanding(true, Now - 10 * Day, 4, Now);
            loop.SetPartyTree(new System.Collections.Generic.Dictionary<int, int> { [1] = 2 }, new[] { "party_m20_morning" });
            var layer = loop.PartyGrowth;
            layer.SetTree(layer.TreeLevels, layer.TreeMajors);
            Assert.That(layer.TreeLevels[1], Is.EqualTo(2));
            Assert.That(layer.TreeMajors, Is.EqualTo(new[] { "party_m20_morning" }));
            layer.SetMascot("wolf", 10, new System.Collections.Generic.Dictionary<string, int> { ["power_aura"] = 1 });
            layer.SetMascot("wolf", 11, layer.RiteLevels);
            Assert.That(layer.RiteLevels["power_aura"], Is.EqualTo(1));
        }
    }
}
