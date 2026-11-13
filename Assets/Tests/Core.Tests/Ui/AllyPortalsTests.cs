using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class AllyPortalsTests
    {
        private static StripAlly Ally(PartyPresence presence, StripPlace place = StripPlace.Hideout, int slot = 1) =>
            new StripAlly(slot, 0, presence, string.Empty, default, presence == PartyPresence.Peaceful ? new PartyPastime(AllyActivity.Garden, place) : default);

        [Test]
        public void TheFirstLookOpensNoPortal()
        {
            var portals = new AllyPortals(3);
            portals.Observe(0, Ally(PartyPresence.Peaceful), StripPlace.Home);
            Assert.That(portals.Active(0), Is.False);
        }

        [Test]
        public void AnAllyLeavingTheFightOpensAPortalToWhereTheyGo()
        {
            var portals = new AllyPortals(3);
            portals.Observe(0, Ally(PartyPresence.Fighting), StripPlace.Home);
            portals.Observe(0, Ally(PartyPresence.Peaceful, StripPlace.Town), StripPlace.Home);
            Assert.That(portals.Active(0), Is.True);
            Assert.That(portals.Leaving(0), Is.True);
            Assert.That(portals.Destination(0), Is.EqualTo(StripPlace.Town));
            for (var i = 0; i < AllyPortals.PortalTicks; i++)
                portals.Tick();
            Assert.That(portals.Active(0), Is.False);
        }

        [Test]
        public void AnAllyComingBackStepsOutOfThePortalOfThePlaceTheyLeft()
        {
            var portals = new AllyPortals(3);
            portals.Observe(1, Ally(PartyPresence.Peaceful, StripPlace.ClanCamp), StripPlace.Home);
            portals.Observe(1, Ally(PartyPresence.Fighting), StripPlace.Home);
            Assert.That(portals.Active(1), Is.True);
            Assert.That(portals.Leaving(1), Is.False);
            Assert.That(portals.Destination(1), Is.EqualTo(StripPlace.ClanCamp));
        }

        [Test]
        public void RestingIsStillTheFightAndOfflineOpensNothing()
        {
            var portals = new AllyPortals(3);
            portals.Observe(0, Ally(PartyPresence.Fighting), StripPlace.Home);
            portals.Observe(0, Ally(PartyPresence.Resting), StripPlace.Home);
            Assert.That(portals.Active(0), Is.False);
            portals.Observe(0, Ally(PartyPresence.Offline), StripPlace.Home);
            Assert.That(portals.Active(0), Is.False);
            portals.Observe(0, Ally(PartyPresence.Peaceful), StripPlace.Home);
            Assert.That(portals.Active(0), Is.False, "coming online is not a portal");
        }

        [Test]
        public void ADifferentAllyInTheSameSeatOpensNothing()
        {
            var portals = new AllyPortals(3);
            portals.Observe(0, Ally(PartyPresence.Fighting, slot: 1), StripPlace.Home);
            portals.Observe(0, Ally(PartyPresence.Peaceful, slot: 2), StripPlace.Home);
            Assert.That(portals.Active(0), Is.False);
        }
    }
}
