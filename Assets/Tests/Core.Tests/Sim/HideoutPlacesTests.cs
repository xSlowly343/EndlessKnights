using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class HideoutPlacesTests
    {
        private static GameLoop Built(params string[] buildings)
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.Hideout.Use(7, 2, 1000, 12, 0, 2, System.Array.Empty<string>());
            var levels = new Dictionary<string, int>();
            foreach (var building in buildings)
                levels[building] = 1;
            loop.Hideout.UseBuild(levels, System.Array.Empty<string>(), string.Empty, 0, 0, 0, 0, 0, false, new Dictionary<string, int>(), System.Array.Empty<string>());
            return loop;
        }

        [Test]
        public void TheDummyShowsWhichRoleBuffsThisPartyBringsAndWhichAreMissing()
        {
            var rules = TestWorld.Data.Hideout;
            var synergy = TestWorld.Data.Synergy;
            Assert.That(HideoutView.Dummy(Built().Hideout.State, synergy, "runedancer"), Is.EqualTo(new[] { Texts.Get("hideout.dummy.none") }));
            var loop = Built(rules.Dummy);
            loop.Hideout.UseCorners(true, new[] { new HideoutCorner("Ann", "runedancer", "", System.Array.Empty<string>()), new HideoutCorner("Bob", "minstrel", "", System.Array.Empty<string>()) });
            var lines = HideoutView.Dummy(loop.Hideout.State, synergy, "alchemist");
            Assert.That(lines, Has.Count.EqualTo(4), "three roles and the missing ones");
            Assert.That(lines[0], Does.StartWith(PartyCardText.Role(synergy.RoleOf(Game.Core.Sim.HeroClass.Runedancer))));
            Assert.That(lines[3], Does.Contain(PartyCardText.Role(synergy.RoleOf(Game.Core.Sim.HeroClass.Soulbinder))));
            Assert.That(HideoutView.Dummy(loop.Hideout.State, synergy, HeroClassNames.Novice), Has.Count.EqualTo(3), "a novice brings no role");
        }

        [Test]
        public void TheTrophyHallAndThePierWaitForTheirBuildings()
        {
            var rules = TestWorld.Data.Hideout;
            var bare = Built();
            Assert.That(HideoutView.TrophyHall(bare.Hideout.State), Is.EqualTo(new[] { Texts.Get("hideout.trophies.none") }));
            Assert.That(HideoutView.Pier(bare.Hideout.State), Is.EqualTo(Texts.Get("hideout.pier.none")));
            Assert.That(bare.State.Hideout.PierRareBoost, Is.Zero);

            var loop = Built(rules.TrophyHall, rules.Pier);
            loop.Hideout.UseTrophies(1200, 4, 31, 40, new[] { new TrophyFeat("old_guard", 2) });
            var hall = HideoutView.TrophyHall(loop.Hideout.State);
            Assert.That(hall, Has.Count.EqualTo(3));
            Assert.That(hall[0], Does.Contain("4").And.Contain("1200"));
            Assert.That(hall[2], Does.Contain(PartyChronicleText.Feat("old_guard")));
            Assert.That(loop.State.Hideout.PierRareBoost, Is.EqualTo(rules.PierRareBoost));
            Assert.That(HideoutView.Pier(loop.Hideout.State), Does.Contain("10"));
            loop.Hideout.Leave();
            Assert.That(loop.State.Hideout.PierRareBoost, Is.Zero);
            Assert.That(loop.State.Hideout.Trophies.Feats, Is.Empty);
        }

        [Test]
        public void ThePartyOrderShowsItsNeedsAndTheDaysLeft()
        {
            var loop = Built();
            Assert.That(HideoutView.Order(loop.Hideout.State), Is.EqualTo(new[] { Texts.Get("hideout.order.none") }));

            loop.Hideout.UseOrder("hunt_and_fish", false, 4, true, new[] { new PartyOrderNeed("bosses", 7, 20), new PartyOrderNeed("fishTogether", 0, 30) });
            var lines = HideoutView.Order(loop.Hideout.State);
            Assert.That(lines, Has.Count.EqualTo(4));
            Assert.That(lines[0], Does.Contain(Texts.Get("hideout.order.hunt_and_fish")).And.Contain("4"));
            Assert.That(lines[1], Does.Contain("7").And.Contain("20"));
            Assert.That(lines[3], Is.EqualTo(Texts.Get("hideout.order.redraw")));
            foreach (var goal in new[] { "hunt_and_fish", "hunt_and_fire", "guardians_and_trip", "guardians_and_garden", "hunt_and_garden" })
                Assert.That(Texts.Has("hideout.order." + goal), Is.True, goal);
            foreach (var counter in new[] { "bosses", "guardians", "fishTogether", "sitMinutes", "partyTrips", "gardenHarvests" })
                Assert.That(Texts.Has("hideout.order.counter." + counter), Is.True, counter);

            loop.Hideout.Leave();
            Assert.That(loop.State.Hideout.Order.Id, Is.Empty);
        }

        [Test]
        public void ADeliveryFromTheHideoutIsTakenOnceEvenIfItComesTwice()
        {
            var loop = Built();
            var items = new System.Collections.Generic.Dictionary<string, long> { ["ember_coal"] = 2 };
            var before = loop.Camp.Count("ember_coal");

            Assert.That(loop.Hideout.Receive(1800000000001, items), Is.True);
            Assert.That(loop.Hideout.Receive(1800000000001, items), Is.False);
            Assert.That(loop.Camp.Count("ember_coal"), Is.EqualTo(before + 2));
            Assert.That(loop.Hideout.Receive(1800000000002, items), Is.True);
            Assert.That(loop.Camp.Count("ember_coal"), Is.EqualTo(before + 4));

            var restored = Game.Core.Save.GameStateJson.Read(new Game.Core.Content.ContentNode(Game.Core.Save.GameStateJson.Write(loop.State), "save", string.Empty),
                TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(restored.Hideout.ClaimSeq, Is.EqualTo(1800000000002));
        }
    }
}
