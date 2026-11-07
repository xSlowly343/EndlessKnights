using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ElementTests
    {
        private static ElementRules Rules => TestWorld.Data.Combat.Elements;

        private static StatSheet Converted(Element element, double share)
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(ElementRules.Conversion[(int)element], ModifierKind.Flat, share));
            return hero;
        }

        private static (GameLoop Loop, FakeInputSource Input) Fighting(StatSheet hero, string biome = "")
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState(7), hero);
            loop.Tick();
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 100));
            (loop.State.EnemyResists, loop.State.EnemyWeakTo) = Rules.Of(biome);
            return (loop, input);
        }

        private static BigNumber OneHit(StatSheet hero, string biome = "")
        {
            var (loop, input) = Fighting(hero, biome);
            input.PressKeys(1);
            loop.Tick();
            return loop.Combat.LastTickDamage;
        }

        [Test]
        public void EverySixElementIsStrongOnceAndWeakOnce()
        {
            var resisted = new int[ElementRules.Count];
            var weak = new int[ElementRules.Count];
            foreach (var biome in new[] { "green_hills", "sky_islands", "sand_ruins", "volcanic_forge", "ice_peak", "rift_heart" })
            {
                var (r, w) = Rules.Of(biome);
                resisted[r]++;
                weak[w]++;
            }
            Assert.That(resisted, Is.All.EqualTo(1));
            Assert.That(weak, Is.All.EqualTo(1));
            Assert.That(Rules.Of("sky_islands"), Is.EqualTo(((int)Element.Air, (int)Element.Lightning)));
        }

        [Test]
        public void PhysicalHitsStayAsTheyWere()
        {
            Assert.That(OneHit(new StatSheet(), "sky_islands"), Is.EqualTo(OneHit(new StatSheet())));
        }

        [Test]
        public void ResistanceAndWeaknessScaleTheConvertedPart()
        {
            var plain = OneHit(new StatSheet(), "green_hills");
            var resisted = OneHit(Converted(Element.Water, 1), "green_hills");
            var weak = OneHit(Converted(Element.Corrosion, 1), "green_hills");
            var half = OneHit(Converted(Element.Water, 0.5), "green_hills");
            Assert.That((resisted / plain).ToDouble(), Is.EqualTo(0.5).Within(1e-9));
            Assert.That((weak / plain).ToDouble(), Is.EqualTo(1.2).Within(1e-9));
            Assert.That((half / plain).ToDouble(), Is.EqualTo(0.75).Within(1e-9));
        }

        [Test]
        public void ConversionAbovaAWholeIsSharedOut()
        {
            var hero = Converted(Element.Water, 1);
            hero.Add(new Modifier(ElementRules.Conversion[(int)Element.Corrosion], ModifierKind.Flat, 1));
            var plain = OneHit(new StatSheet(), "green_hills");
            Assert.That((OneHit(hero, "green_hills") / plain).ToDouble(), Is.EqualTo(0.5 * 0.5 + 0.5 * 1.2).Within(1e-9));
        }

        [Test]
        public void ElementDamageIsItsOwnBasketAndPenetrationCutsResistance()
        {
            var plain = OneHit(new StatSheet());
            var hero = Converted(Element.Fire, 1);
            hero.Add(new Modifier(ElementRules.Damage[(int)Element.Fire], ModifierKind.Increased, 0.2));
            var burning = OneHit(hero);
            var (loop, input) = Fighting(hero);
            input.PressKeys(1);
            loop.Tick();
            var first = loop.Combat.LastTickDamage;
            Assert.That((first / plain).ToDouble(), Is.EqualTo(1.2).Within(1e-9));
            Assert.That(burning, Is.EqualTo(first));

            var pierced = Converted(Element.Water, 1);
            pierced.Add(new Modifier(ElementRules.Pen[(int)Element.Water], ModifierKind.Flat, 2));
            var basePlain = OneHit(new StatSheet(), "green_hills");
            Assert.That((OneHit(pierced, "green_hills") / basePlain).ToDouble(), Is.EqualTo(1.5).Within(1e-9), "resistance goes down to −50%");
        }

        [Test]
        public void FireBurnsAFifthOfTheHitOverThreeSeconds()
        {
            var (loop, input) = Fighting(Converted(Element.Fire, 1));
            input.PressKeys(1);
            loop.Tick();
            var hit = loop.Combat.LastTickDamage;
            Assert.That(loop.State.EmberPool.ToDouble(), Is.EqualTo(hit.ToDouble() * Rules.BurnShare).Within(hit.ToDouble() * 1e-9));
            var burned = BigNumber.Zero;
            for (var i = 0; i < Rules.BurnSeconds * GameLoop.TicksPerSecond; i++)
            {
                loop.Tick();
                burned += loop.Combat.LastTickDamage;
            }
            Assert.That(burned.ToDouble(), Is.EqualTo(hit.ToDouble() * Rules.BurnShare).Within(hit.ToDouble() * 1e-6));
            Assert.That(loop.State.EmberTicks, Is.Zero);
        }

        [Test]
        public void AirSendsPartOfTheHitToTheNextEnemy()
        {
            var (loop, input) = Fighting(Converted(Element.Air, 1));
            input.PressKeys(1);
            loop.Tick();
            Assert.That(loop.State.QueueDamage[0].ToDouble(), Is.EqualTo(loop.Combat.LastTickDamage.ToDouble() * Rules.AirShare).Within(1e-6));
        }

        [Test]
        public void EarthWearsTheDefenseDownToThirtyPercent()
        {
            var (loop, input) = Fighting(Converted(Element.Earth, 1));
            for (var i = 0; i < 10; i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }
            Assert.That(loop.State.EarthShred, Is.EqualTo(10 * Rules.EarthShred).Within(1e-9));
            Assert.That(loop.Combat.DefenseShred, Is.EqualTo(10 * Rules.EarthShred).Within(1e-9));
            for (var i = 0; i < 100; i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }
            Assert.That(loop.State.EarthShred, Is.EqualTo(Rules.EarthMax).Within(1e-9));
        }

        [Test]
        public void LightningDischargesAboutOneHitInTen()
        {
            var (loop, input) = Fighting(Converted(Element.Lightning, 1));
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 300));
            for (var i = 0; i < 1000; i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }
            Assert.That(loop.State.Discharges, Is.InRange(60, 140));
        }

        [Test]
        public void WaterAddsToTheBossTimerUpToFiveSeconds()
        {
            var (loop, input) = Fighting(Converted(Element.Water, 1));
            loop.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 300));
            loop.State.BossTicksLeft = 300;
            input.PressKeys(1);
            loop.Tick();
            var after = loop.State.BossTicksLeft;
            Assert.That(loop.State.WaterSeconds, Is.EqualTo(Rules.WaterSeconds).Within(1e-9));
            for (var i = 0; i < 200; i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }
            Assert.That(loop.State.WaterSeconds, Is.EqualTo(Rules.WaterMax).Within(1e-9));
            Assert.That(after, Is.GreaterThanOrEqualTo(300));
        }

        [Test]
        public void CorrosionLaysFracture()
        {
            var (loop, input) = Fighting(Converted(Element.Corrosion, 1));
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 300));
            for (var i = 0; i < 40; i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }
            Assert.That(loop.State.FracturesLaid, Is.InRange(3, 20));
        }

        [Test]
        public void StagesGiveEnemiesTheResistanceOfTheirBiome()
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            var biome = BiomeRotation.Current(TestWorld.Data.Stages, loop.State, loop.State.Stage);
            Assert.That((loop.State.EnemyResists, loop.State.EnemyWeakTo), Is.EqualTo(Rules.Of(biome)));
            Assert.That(loop.State.EnemyResists, Is.Not.EqualTo(ElementRules.None));
        }
    }
}
