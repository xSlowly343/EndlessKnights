using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionRosterThreeTests
    {
        private static GameLoop Camp()
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            loop.SetLocalDay(100);
            return loop;
        }

        [Test]
        public void EveryHeroClassHasItsOwnCompanion()
        {
            var rules = TestWorld.Data.Companions;
            foreach (var name in HeroClassNames.ByName.Keys)
            {
                var found = 0;
                foreach (var def in rules.Companions)
                    found += def.Unlock != null && def.Unlock.Kind == UnlockKind.ClassLevel && def.Unlock.Class == name ? 1 : 0;
                Assert.That(found, Is.EqualTo(1), name);
            }
        }

        [Test]
        public void ACharacterCompanionComesAtLevelFiftyOfItsClass()
        {
            var loop = Camp();
            loop.State.ClassChosen = true;
            loop.State.Class = HeroClass.Runedancer;
            loop.State.HeroLevel = 49;
            loop.Nest.CheckUnlocks();
            Assert.That(loop.Companions.Owns("rune_stoat"), Is.False);
            loop.State.HeroLevel = 50;
            loop.Nest.CheckUnlocks();
            Assert.That(loop.Companions.Owns("rune_stoat"), Is.True);
            Assert.That(loop.Companions.Owns("fencing_crane"), Is.False);
            Assert.That(loop.Nest.ShopPriceOf("fencing_crane"), Is.EqualTo(300));
            Assert.That(loop.Nest.ShopPriceOf("egg_griffin"), Is.EqualTo(200));
        }

        [Test]
        public void HomeTraitsOfTheNewCompanionsWorkForTheWholeCamp()
        {
            var loop = Camp();
            foreach (var id in new[] { "sand_jerboa", "singing_cricket", "rag_puppy", "spirit_firefly", "clay_pigeon" })
            {
                loop.Companions.Grant(id);
                loop.Companions.Progress(id).Stage = 3;
            }
            Assert.That(loop.Companions.HomeOwn(OwnEffectKind.Hatch), Is.Zero, "only at home");
            Assert.That(loop.Companions.StayHome("sand_jerboa"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Companions.HomeOwn(OwnEffectKind.Hatch), Is.GreaterThan(0.04));
            loop.Companions.LeaveHome("sand_jerboa");
            Assert.That(loop.Companions.StayHome("singing_cricket"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Companions.HomeOwn(OwnEffectKind.Bond), Is.GreaterThan(0.04));
            Assert.That(loop.Companions.HomeOwn(OwnEffectKind.Hatch), Is.Zero);
        }

        [Test]
        public void TheParrotMakesEveryBiomeItsOwn()
        {
            var loop = Camp();
            loop.Companions.Grant("ship_parrot");
            Assert.That(loop.Companions.BiomeBonus("ship_parrot", "sand_ruins"), Is.EqualTo(loop.Companions.BiomeBonus("ship_parrot", "ice_peak")));
            Assert.That(loop.Companions.BiomeBonus("ship_parrot", "sand_ruins"), Is.GreaterThan(0));
            Assert.That(loop.Companions.BiomeBonus("camp_dog", "sand_ruins"), Is.Zero);
        }

        [Test]
        public void TheCraneCutsOnlyTheFirstTripOfTheDay()
        {
            var loop = Camp();
            loop.Companions.Grant("fencing_crane");
            loop.Expeditions.Send("fencing_crane", "green_hills", 8);
            var first = loop.State.Expeditions.Active[0].Seconds;
            loop.Expeditions.Finish(0);
            loop.Expeditions.Send("fencing_crane", "green_hills", 8);
            var second = loop.State.Expeditions.Active[0].Seconds;
            Assert.That(first, Is.LessThan(second * 0.95));
        }

        [Test]
        public void TheClockworkMouseBringsAShortTripBackOnceADay()
        {
            var loop = Camp();
            loop.Companions.Grant("clockwork_mouse");
            Assert.That(loop.Companions.Rewind(0), Is.EqualTo(CompanionResult.UnknownCompanion));
            loop.Expeditions.Send("camp_dog", "green_hills", 1);
            Assert.That(loop.Companions.Rewind(0), Is.EqualTo(CompanionResult.NotHome));
            loop.Companions.Progress("clockwork_mouse").Stage = 3;
            loop.Companions.StayHome("clockwork_mouse");
            Assert.That(loop.Companions.Rewind(0), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Expeditions.Busy("camp_dog"), Is.False);
            loop.Expeditions.Send("camp_dog", "green_hills", 1);
            Assert.That(loop.Companions.Rewind(0), Is.EqualTo(CompanionResult.TooSoon));
        }

        [Test]
        public void UndersideTwinsDropFromTheRiftBosses()
        {
            var rules = TestWorld.Data.Companions;
            var twins = 0;
            foreach (var def in rules.Companions)
            {
                if (def.Source != "underside")
                    continue;
                twins++;
                Assert.That(def.Rarity, Is.EqualTo("legendary"));
                Assert.That(def.Unlock!.Kind, Is.EqualTo(UnlockKind.Drop));
                Assert.That(TestWorld.Data.Enemies.TryGet(def.Unlock.Enemy, out _), Is.True, def.Id);
            }
            Assert.That(twins, Is.EqualTo(6));
        }
    }
}
