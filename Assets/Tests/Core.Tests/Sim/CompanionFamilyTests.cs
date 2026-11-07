using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionFamilyTests
    {
        private static GameLoop Camp()
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            loop.SetLocalDay(100);
            return loop;
        }

        private static CompanionFamily Family(string id)
        {
            foreach (var family in TestWorld.Data.Companions.Families.Families)
            {
                if (family.Id == id)
                    return family;
            }
            throw new System.InvalidOperationException(id);
        }

        [Test]
        public void EveryCompanionIsInOneFamily()
        {
            var rules = TestWorld.Data.Companions;
            Assert.That(rules.Families.Families.Count, Is.EqualTo(8));
            foreach (var def in rules.Companions)
                Assert.That(rules.Families.FamilyOf(def.Id), Is.Not.Null, def.Id);
        }

        [Test]
        public void CollectingTheFreeMembersGivesTheBonusAndATreePoint()
        {
            var loop = Camp();
            var forge = Family("forge");
            var points = loop.Menagerie.Points;
            foreach (var member in forge.Members)
            {
                var def = loop.Companions.Rules.Companion(member)!;
                if (def.Source is "shop" or "pass")
                    continue;
                Assert.That(loop.Menagerie.Collected(forge), Is.False);
                loop.Companions.Grant(member);
                loop.Companions.ApplyPerks();
            }
            Assert.That(loop.Menagerie.Collected(forge), Is.True, "paid members are not needed");
            Assert.That(loop.Menagerie.Points, Is.EqualTo(points + 1));
            Assert.That(loop.Menagerie.Stat(MenagerieStat.TripShards), Is.EqualTo(0.05).Within(1e-9));
            Assert.That(loop.State.CompanionPerks[(int)MuseumPerk.FewerHammers], Is.Zero);
            foreach (var member in forge.Members)
            {
                if (loop.Companions.Known(member))
                    loop.Companions.Progress(member).Stage = 2;
            }
            loop.Companions.ApplyPerks();
            Assert.That(loop.Menagerie.AllAdults(forge), Is.True);
            Assert.That(loop.State.CompanionPerks[(int)MuseumPerk.FewerHammers], Is.EqualTo(0.05).Within(1e-9));
        }

        [Test]
        public void TheWardensGiveEveryCompanionATalentPointWhenAllAreAdults()
        {
            var loop = Camp();
            var wardens = Family("wardens");
            var before = loop.Companions.TalentPoints("camp_dog");
            foreach (var member in wardens.Members)
            {
                loop.Companions.Grant(member);
                loop.Companions.Progress(member).Stage = 2;
            }
            loop.Companions.ApplyPerks();
            Assert.That(loop.Companions.TalentPoints("camp_dog"), Is.EqualTo(before + 1));
            Assert.That(loop.Menagerie.Stat(MenagerieStat.AllExperience), Is.EqualTo(0.05).Within(1e-9));
        }
    }
}
