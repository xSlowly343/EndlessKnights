using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class DistillTests
    {
        private static DistillRules Rules => TestWorld.Real.Distill;
        private static ColossusSeedRules Seed => TestWorld.Real.Colossus;
        private const float PixelsPerMeter = (float)(1 / InputStats.DefaultMetersPerPixel);

        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        private static (GameLoop Loop, FakeInputSource Input) Still()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.RealLoop(input);
            loop.State.RecordStage = 120;
            loop.Tick();
            loop.Camp.SetGuilds(new[] { "distillers" });
            return (loop, input);
        }

        private static void Stock(GameLoop loop, int times = 1)
        {
            foreach (var herb in TestWorld.Real.Alchemy.Recipe("might")!.Herbs)
                loop.Camp.Add(herb, times);
            loop.Camp.Add(Rules.Fuel.Item, Rules.Fuel.Count * times);
        }

        private static void Work(GameLoop loop)
        {
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Peaceful.SetActivity(DistillSystem.Activity);
        }

        [Test]
        public void TheStillWaitsForTheDistillersAndTakesHerbsAndCoal()
        {
            var loop = TestWorld.RealLoop();
            loop.State.RecordStage = 120;
            loop.Tick();
            Stock(loop);
            Assert.That(loop.Distill.Start("might"), Is.EqualTo(DistillResult.Closed));
            Assert.That(DistillView.Status(loop.Distill), Does.StartWith("Перегонный куб ставит гильдия перегонщиков"));
            loop.Camp.SetGuilds(new[] { "distillers" });
            Assert.That(loop.Distill.Start("storm"), Is.EqualTo(DistillResult.UnknownRecipe));
            Assert.That(loop.Distill.Start("might"), Is.EqualTo(DistillResult.Done));
            Assert.That(loop.Camp.Count(Rules.Fuel.Item), Is.Zero);
            Assert.That(loop.Distill.Start("might"), Is.EqualTo(DistillResult.Busy));
        }

        [Test]
        public void FanningTheFireAndVentingMakesALongerTincture()
        {
            var (loop, input) = Still();
            Stock(loop);
            loop.Distill.Start("might");
            Work(loop);
            for (var i = 0; i < 500 && loop.Distill.Busy; i++)
            {
                input.MoveMouse(PixelsPerMeter * 0.5f);
                if (loop.State.Distill.Pressure > 0.6)
                    input.ClickLeft();
                loop.Tick();
            }
            Assert.That(loop.State.Distill.Done, Is.EqualTo(1));
            Assert.That(loop.State.Distill.Bursts, Is.Zero);
            var tincture = loop.State.Alchemy.Potions.Single();
            Assert.That(tincture.Lasting, Is.GreaterThanOrEqualTo(Rules.TinctureLasting));
            Assert.That(loop.Alchemy.Drink(0), Is.EqualTo(AlchemyResult.Done));
            var seconds = TestWorld.Real.Alchemy.Recipe("might")!.Seconds;
            Assert.That(loop.State.Alchemy.ActiveTicksLeft, Is.EqualTo((long)System.Math.Round(seconds * tincture.Lasting * GameLoop.TicksPerSecond)));
        }

        [Test]
        public void PressureLeftAloneBurstsTheStill()
        {
            var (loop, _) = Still();
            Stock(loop, 20);
            var miracles = 0;
            for (var run = 0; run < 20; run++)
            {
                loop.Distill.Start("might");
                Work(loop);
                for (var i = 0; i < 1000 && loop.Distill.Busy; i++)
                    loop.Tick();
            }
            Assert.That(loop.State.Distill.Bursts, Is.EqualTo(20));
            miracles = loop.State.Alchemy.Potions.Count(p => p.Fine);
            Assert.That(loop.State.Alchemy.Potions.Count, Is.EqualTo(miracles), "a burst gives a miracle or nothing");
            Assert.That(loop.State.Distill.Miracles, Is.EqualTo(miracles));
        }

        [Test]
        public void TheDowserKeepsThePressureLower()
        {
            var (loop, _) = Still();
            var plain = loop.Distill.PressureRate;
            loop.State.TalentPerks[(int)MuseumPerk.SlowerPressure] = 0.3;
            Assert.That(loop.Distill.PressureRate, Is.EqualTo(plain * 0.7).Within(1e-12));
            Assert.That(TestWorld.Real.Talents.Of(HeroClass.Dowser)!.Perks.Any(p => p.Perk == MuseumPerk.SlowerPressure), Is.True);
        }

        [Test]
        public void AGuardianDropsAColossusSeedOnceAWeek()
        {
            var (loop, _) = Still();
            var guardian = TestWorld.Real.Enemies.All.First(enemy => enemy.Type == EnemyType.Guardian);
            void Kill()
            {
                var first = loop.Events.Count;
                loop.Events.Add(GameEvent.ForEnemy(GameEventKind.BossDefeated, 0, 50, guardian));
                loop.Colossus.NoteGuardians(first);
            }
            Kill();
            Kill();
            Assert.That(loop.State.Colossus.SeedsWaiting, Is.EqualTo(1));
            loop.Colossus.Collect();
            Assert.That(loop.Camp.Count(Seed.Seed), Is.EqualTo(1));
            loop.Colossus.Advance(Seed.CooldownSeconds);
            Kill();
            Assert.That(loop.State.Colossus.SeedsWaiting, Is.EqualTo(1), "a week later the next one drops");
        }

        [Test]
        public void TheColossusSeedNeedsAWeekAndAllFourInputsThenBloomsForever()
        {
            var (loop, input) = Still();
            Assert.That(loop.Colossus.Plant(), Is.EqualTo(ColossusResult.NoSeed));
            loop.Camp.Add(Seed.Seed, 1);
            Assert.That(loop.Colossus.Plant(), Is.EqualTo(ColossusResult.Done));
            loop.Colossus.Advance(Seed.GrowSeconds);
            Assert.That(loop.State.Colossus.Grown, Is.False, "time alone is not enough");
            Assert.That(DistillView.Colossus(loop.Colossus), Does.StartWith("время 100%, нажатия 0%"));
            loop.State.Colossus.Keys = Seed.NeedKeys;
            loop.State.Colossus.Clicks = Seed.NeedClicks;
            loop.State.Colossus.MouseMeters = Seed.NeedMouseMeters;
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Peaceful.SetActivity(GardenSystem.Activity);
            loop.State.Colossus.IdleSeconds = Seed.NeedIdleSeconds - 0.01;
            input.MoveMouse(0);
            for (var i = 0; i < 30 && !loop.State.Colossus.Grown; i++)
                loop.Tick();
            Assert.That(loop.State.Colossus.Grown, Is.True, "quiet time in the garden finishes it");
            Assert.That(loop.Camp.Count(Seed.Bloom), Is.EqualTo(1));
            loop.Colossus.Advance(Seed.BloomEverySeconds * 100);
            loop.Colossus.Collect();
            Assert.That(loop.Camp.Count(Seed.Bloom), Is.EqualTo(1 + Seed.MaxBlooms));

            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Colossus.Grown, Is.True);
        }

        [Test]
        public void ABloomInTheStillMakesAMiracleAndTheSaveKeepsTheTincture()
        {
            var (loop, input) = Still();
            Stock(loop);
            Assert.That(loop.Distill.Start("might", bloom: true), Is.EqualTo(DistillResult.NoBloom));
            loop.Camp.Add(Seed.Bloom, 1);
            Assert.That(loop.Distill.Start("might", bloom: true), Is.EqualTo(DistillResult.Done));
            Work(loop);
            for (var i = 0; i < 500 && loop.Distill.Busy; i++)
            {
                input.MoveMouse(PixelsPerMeter * 0.5f);
                if (loop.State.Distill.Pressure > 0.6)
                    input.ClickLeft();
                loop.Tick();
            }
            var miracle = loop.State.Alchemy.Potions.Single();
            Assert.That(miracle.Fine, Is.True);
            Assert.That(miracle.Lasting, Is.EqualTo(Rules.MiracleLasting));
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Alchemy.Potions.Single().Lasting, Is.EqualTo(Rules.MiracleLasting));
            Assert.That(copy.Distill.Miracles, Is.EqualTo(1));
        }
    }
}
