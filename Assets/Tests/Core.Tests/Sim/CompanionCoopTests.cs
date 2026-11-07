using System.Collections.Generic;
using Game.Core.Input;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionCoopTests
    {
        private const double Hour = ExpeditionSystem.SecondsPerHour;

        private static GameLoop Camp()
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            loop.SetLocalDay(100);
            return loop;
        }

        [Test]
        public void FriendsPetsAndWarmsComeFromTheHideoutClaim()
        {
            var loop = Camp();
            loop.Nest.Lay("ice_peak");
            loop.Hideout.Receive(new Dictionary<string, long> { ["companion_pet:camp_dog"] = 5, ["companion_warm"] = 1, ["bone_hills"] = 2 });
            Assert.That(loop.Companions.Progress("camp_dog").FriendPetsToday, Is.EqualTo(3), "three friend pets a day count");
            Assert.That(loop.Companions.Progress("camp_dog").Bond, Is.EqualTo(3));
            Assert.That(loop.State.Companions.Eggs[0].SecondsLeft, Is.EqualTo(22 * Hour).Within(1e-6));
            Assert.That(loop.Camp.Count("bone_hills"), Is.EqualTo(2));
            Assert.That(loop.Camp.Count("companion_warm"), Is.Zero);
        }
    }
}
