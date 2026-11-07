using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionRarityTests
    {
        private const double Hour = ExpeditionSystem.SecondsPerHour;
        private static CompanionRules Rules => TestWorld.Data.Companions;

        private static GameLoop Camp(ulong seed = 1)
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(seed));
            loop.Tick();
            loop.SetLocalDay(100);
            return loop;
        }

        [Test]
        public void CompanionsWithASourceHaveOwnTraits()
        {
            var fresh = Rules.Companions.Where(def => def.Unlock != null).ToList();
            Assert.That(fresh.Count, Is.EqualTo(16 + 26));
            foreach (var def in fresh)
                Assert.That(Rules.Own(def.Trait), Is.Not.Null, def.Id);
            Assert.That(Rules.Companions.Count(def => def.Source is not "shop" and not "pass"), Is.EqualTo(55));
        }

        [Test]
        public void MiniBossesAndHostsDropTheirCompanionRarely()
        {
            var boar = TestWorld.Real.Enemies.All.First(enemy => enemy.Id == "boar_mother");
            var drops = 0;
            for (ulong seed = 1; seed <= 2000; seed++)
            {
                var loop = Camp(seed);
                var first = loop.Events.Count;
                loop.Events.Add(GameEvent.ForEnemy(GameEventKind.BossDefeated, 0, 50, boar));
                loop.Nest.NoteGuardians(first);
                if (loop.Companions.Known("hills_piglet"))
                    drops++;
            }
            Assert.That(drops, Is.InRange(8, 35), "about one in a hundred");
        }

        [Test]
        public void GuildClanHideoutEchoAndAchievementsUnlockCompanions()
        {
            var loop = Camp();
            loop.State.Hideout.Tier = 4;
            loop.State.ClanCamp.Level = 10;
            loop.State.Prestiges = 10;
            loop.State.Town.Reputation["gardeners"] = 1_000_000;
            loop.Nest.CheckUnlocks();
            Assert.That(loop.Companions.Known("party_pup"), Is.True);
            Assert.That(loop.Companions.Known("banner_pup"), Is.True);
            Assert.That(loop.Companions.Known("echo_critter"), Is.True);
            Assert.That(loop.Companions.Known("market_hedgehog"), Is.True);
            Assert.That(loop.Companions.Known("chronicler"), Is.False);
            Assert.That(loop.Companions.Known("double"), Is.False, "the server grants it");
        }

        [Test]
        public void LegendaryTraitsAreStrongerAndShiningCoatsAddMore()
        {
            var loop = Camp();
            loop.Companions.Grant("jackal_pup");
            loop.Companions.Grant("post_griffin");
            Assert.That(loop.Companions.OwnMultiplier("jackal_pup", OwnEffectKind.Shards), Is.EqualTo(1.3).Within(1e-9));
            Assert.That(loop.Companions.TraitFactor("post_griffin"), Is.EqualTo(1.15).Within(1e-9));
            Assert.That(loop.Companions.Own("post_griffin", OwnEffectKind.Choice), Is.EqualTo(0.5 * 1.15).Within(1e-9));
            loop.Companions.Progress("jackal_pup").Coat = "shining";
            Assert.That(loop.Companions.OwnMultiplier("jackal_pup", OwnEffectKind.Shards), Is.EqualTo(1 + 0.3 * 1.05).Within(1e-9));
            Assert.That(loop.Companions.Stars("jackal_pup"), Is.EqualTo(2), "a shining coat is a star of the album");
        }

        [Test]
        public void HatchedCompanionsSometimesShine()
        {
            var coats = 0;
            for (ulong seed = 1; seed <= 3000; seed++)
            {
                var loop = Camp(seed);
                loop.Companions.GrantRolled("egg_griffin");
                if (loop.Companions.Progress("egg_griffin").Coat.Length > 0)
                    coats++;
            }
            Assert.That(coats, Is.InRange(10, 60));
        }

        [Test]
        public void HomeTraitsChronicleAndTheCrownWork()
        {
            var loop = Camp();
            loop.Companions.Grant("walrus_pup");
            loop.Companions.StayHome("walrus_pup");
            Assert.That(loop.State.CompanionPerks[(int)MuseumPerk.BiteFaster], Is.EqualTo(0.05).Within(1e-9));
            loop.Companions.Grant("chronicler");
            loop.Companions.Gain("camp_dog", 10);
            Assert.That(loop.Companions.Progress("camp_dog").Experience, Is.EqualTo(10 * (1 + 0.05 * 1.15)).Within(1e-9));
            loop.Companions.Grant("little_crown");
            var ingots = loop.State.Drops.Ingots;
            loop.SetLocalDay(101);
            Assert.That(loop.State.Drops.Ingots, Is.EqualTo(ingots + 1));
        }

        [Test]
        public void TheHungryCubBringsMoreButWalksLonger()
        {
            var loop = Camp();
            loop.Companions.Grant("eater_cub");
            Assert.That(loop.Expeditions.DurationFactor("eater_cub"), Is.EqualTo(1 + 0.2 * 1.15).Within(1e-9));
        }

        [Test]
        public void TheCoatSurvivesTheSave()
        {
            var loop = Camp();
            loop.Companions.Grant("jackal_pup");
            loop.Companions.Progress("jackal_pup").Coat = "underside";
            var copy = Game.Core.Save.GameStateJson.Read(new ContentNode(Game.Core.Save.GameStateJson.Write(loop.State), "save", string.Empty),
                TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Companions.Progress["jackal_pup"].Coat, Is.EqualTo("underside"));
        }
    }
}
