using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionBranchTests
    {
        private static GameLoop Camp()
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            loop.SetLocalDay(100);
            return loop;
        }

        [Test]
        public void EveryCompanionHasThreeBranchNodesForTeenAdultAndAwakened()
        {
            foreach (var def in TestWorld.Data.Companions.Companions)
            {
                Assert.That(def.Branch.Count, Is.EqualTo(3), def.Id);
                for (var i = 0; i < 3; i++)
                    Assert.That(def.Branch[i].Stage, Is.EqualTo(i + 1), def.Id);
            }
        }

        [Test]
        public void BranchNodesOpenWithTheStage()
        {
            var loop = Camp();
            var crow = loop.Companions;
            Assert.That(crow.OwnMultiplier("camp_crow", OwnEffectKind.Shards), Is.EqualTo(1));
            crow.Progress("camp_crow").Stage = 1;
            Assert.That(crow.OwnMultiplier("camp_crow", OwnEffectKind.Shards), Is.EqualTo(1.05).Within(1e-9));
            crow.Progress("camp_crow").Stage = 2;
            var shards = loop.State.Drops.Shards;
            loop.SetLocalDay(101);
            Assert.That(loop.State.Drops.Shards, Is.EqualTo(shards + 10), "the crow's stash");
        }

        [Test]
        public void ABranchMultiplierStacksWithTheOwnTrait()
        {
            var loop = Camp();
            loop.Companions.Grant("jackal_pup");
            var before = loop.Companions.OwnMultiplier("jackal_pup", OwnEffectKind.Shards);
            loop.Companions.Progress("jackal_pup").Stage = 1;
            var factor = loop.Companions.TraitFactor("jackal_pup");
            Assert.That(loop.Companions.OwnMultiplier("jackal_pup", OwnEffectKind.Shards), Is.EqualTo(1 + (1.3 * 1.05 - 1) * factor).Within(1e-9));
            Assert.That(before, Is.EqualTo(1 + 0.3 * factor).Within(1e-9));
        }

        [Test]
        public void HomeBranchNodesFeedThePerkPool()
        {
            var loop = Camp();
            var dog = loop.Companions.Progress("camp_dog");
            dog.Stage = 2;
            loop.Companions.StayHome("camp_dog");
            Assert.That(loop.State.CompanionPerks[(int)MuseumPerk.SeedBack], Is.GreaterThan(0));
        }
    }
}
