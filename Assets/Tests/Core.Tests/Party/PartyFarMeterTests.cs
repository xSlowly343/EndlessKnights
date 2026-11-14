using Game.Core.Party;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyFarMeterTests
    {
        [Test]
        public void SoloRiftBossesAreCountedWithTheDeepestStage()
        {
            var state = new GameState();
            var meter = new PartyFarMeter();
            meter.Note(state, 40);
            Assert.That(meter.Pending, Is.False, "a boss at home is not a far boss");
            state.Rift.Inside = true;
            meter.Note(state, 40);
            meter.Note(state, 12);
            Assert.That(meter.Take(PartyFarMeter.Rift), Is.EqualTo((2, 40)));
            Assert.That(meter.Pending, Is.False);
            meter.Return(PartyFarMeter.Rift, 2, 40);
            Assert.That(meter.RiftBosses, Is.EqualTo(2), "a failed report gives the bosses back");
            Assert.That(meter.Take(PartyFarMeter.Dungeons), Is.EqualTo((0, 0)));
        }
    }
}
