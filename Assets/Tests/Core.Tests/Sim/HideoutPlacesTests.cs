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
    }
}
