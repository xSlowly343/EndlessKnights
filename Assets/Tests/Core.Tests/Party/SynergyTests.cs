using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Synergy;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class SynergyTests
    {
        private static SynergyRules Rules => TestWorld.Data.Synergy;

        private static GameLoop Hero(HeroClass heroClass)
        {
            var loop = TestWorld.Loop();
            loop.State.Class = heroClass;
            return loop;
        }

        private static double Damage(GameLoop loop) => loop.Hero.Value(CombatStats.Damage, 1);

        [Test]
        public void EveryRoleHasABuffAndTheRolesAreThePartyRoles()
        {
            Assert.That(Rules.Roles.Count, Is.EqualTo(8));
            for (var i = 0; i < Rules.Roles.Count; i++)
                Assert.That(Rules.RoleBuffs[i], Is.Not.Empty, Rules.Roles[i]);
            foreach (var heroClass in HeroClassNames.ByName.Values)
                Assert.That(Rules.RoleOf(heroClass), Is.EqualTo(TestWorld.Data.Party.Search.RoleOf(heroClass)));
        }

        [Test]
        public void AnAllysRoleGivesItsBuffOnceAndNotToThemselves()
        {
            var loop = Hero(HeroClass.Windshot);
            loop.Tick();
            var alone = Damage(loop);

            loop.Synergy.SetAlly(0, true, HeroClass.Runedancer);
            loop.Tick();
            var withStriker = Damage(loop);
            Assert.That(withStriker, Is.GreaterThan(alone));

            loop.Synergy.SetAlly(1, true, HeroClass.Duelist);
            loop.Tick();
            Assert.That(Damage(loop), Is.EqualTo(withStriker).Within(1e-12), "the same role twice does not stack");

            loop.Synergy.SetAlly(0, false, default);
            loop.Synergy.SetAlly(1, false, default);
            loop.Tick();
            Assert.That(Damage(loop), Is.EqualTo(alone).Within(1e-12));
        }

        [Test]
        public void FourDifferentRolesGiveThreeBuffs()
        {
            var loop = Hero(HeroClass.Runedancer);
            loop.Synergy.SetAlly(0, true, HeroClass.Minstrel);
            loop.Synergy.SetAlly(1, true, HeroClass.Whisperer);
            loop.Synergy.SetAlly(2, true, HeroClass.Soulbinder);
            loop.Tick();
            var active = Enumerable.Range(0, Rules.Roles.Count).Count(i => loop.Synergy.RoleActive(i));
            Assert.That(active, Is.EqualTo(3));
            Assert.That(loop.Synergy.RoleActive(Rules.Roles.ToList().IndexOf("striker")), Is.False, "the hero's own role is not a buff for them");
        }

        [Test]
        public void BuffsHoldOnlyWhileTheHeroAndTheAllyFight()
        {
            var loop = Hero(HeroClass.Windshot);
            loop.Synergy.SetAlly(0, true, HeroClass.Runedancer, fighting: false);
            loop.Tick();
            Assert.That(loop.Synergy.AllyRoles, Is.Zero);
            loop.Synergy.SetAlly(0, true, HeroClass.Runedancer);
            loop.Synergy.OwnFighting = false;
            loop.Tick();
            Assert.That(loop.Synergy.AllyRoles, Is.Zero);
            loop.Synergy.OwnFighting = true;
            loop.Tick();
            Assert.That(loop.Synergy.AllyRoles, Is.Not.Zero);
        }

        [Test]
        public void TheRoleBuffEffectScalesEveryBuff()
        {
            var loop = Hero(HeroClass.Windshot);
            loop.Synergy.SetAlly(0, true, HeroClass.Runedancer);
            loop.Tick();
            var buff = Damage(loop);
            loop.Hero.Add(new Modifier(CombatStats.RoleBuffEffect, ModifierKind.Increased, 1));
            loop.Tick();
            Assert.That(Damage(loop) - 1, Is.EqualTo((buff - 1) * 2).Within(1e-9));
        }

        [Test]
        public void SynergyTicksDoNotAllocate()
        {
            var probeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (System.GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var loop = Hero(HeroClass.Sandwalker);
            loop.Synergy.SetAlly(0, true, HeroClass.Alchemist);
            loop.Synergy.SetAlly(1, true, HeroClass.Dowser);
            for (var tick = 0; tick < 200; tick++)
                loop.Tick();

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var tick = 0; tick < 50; tick++)
                loop.Tick();

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        [TestCase(@"""runedancer"": ""striker"", ", "", "class 'runedancer' has no role")]
        [TestCase(@"""seeker"": [", @"""finder"": [", "unknown role 'finder'")]
        public void BrokenSynergyIsReported(string from, string to, string message)
        {
            var json = TestData.ReadGameDataFile(SynergyRules.FileName).Replace(from, to);

            var error = Assert.Throws<ContentException>(() => SynergyRules.Load(json))!;

            Assert.That(error.Message, Does.Contain(message));
        }
    }
}
