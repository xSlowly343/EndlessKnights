using System.Collections.Generic;
using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Tree;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class LanternbearerTests
    {
        private static LanternbearerRules Rules => TestWorld.Data.Combat.Classes.Lanternbearer;

        private static Modifier Flat(StatId stat, double value) => new Modifier(stat, ModifierKind.Flat, value);

        private static (GameLoop Loop, FakeInputSource Input) Hero(params Modifier[] modifiers) => Hero(HeroClass.Lanternbearer, false, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, bool crits, params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(crits ? new Modifier(CombatStats.CritChance, ModifierKind.Flat, 5) : new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, TestWorld.WithAllSkillsOpen(new GameState { Class = heroClass }), hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            Spawn(loop, EnemyType.Normal);
            loop.Tick();
            loop.Events.Clear();
            return (loop, input);
        }

        private static void Spawn(GameLoop loop, EnemyType type)
        {
            loop.State.SpawnEnemy(null, type, BigNumber.Create(1, 50));
            loop.State.BossTicksLeft = type == EnemyType.Normal ? 0 : 100_000;
        }

        private static List<double> Damage(GameLoop loop)
        {
            var damage = new List<double>();
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage && loop.Events[i].Source != DamageSource.Mouse)
                    damage.Add(loop.Events[i].Amount.ToDouble());
            }
            return damage;
        }

        private static List<double> Hit(GameLoop loop, FakeInputSource input)
        {
            loop.Events.Clear();
            input.PressKeys(1);
            loop.Tick();
            var damage = Damage(loop);
            loop.Tick();
            return damage;
        }

        private static void Hits(GameLoop loop, FakeInputSource input, int count)
        {
            for (var i = 0; i < count; i++)
                Hit(loop, input);
        }

        private static void Wait(GameLoop loop, double seconds)
        {
            for (var i = 0; i < System.Math.Round(seconds * GameLoop.TicksPerSecond); i++)
                loop.Tick();
        }

        private static double Expected(GameLoop loop, double multiplier) =>
            loop.Combat.SkillDamage(loop.Combat.BaseKeyDamage, multiplier * loop.State.FocusMultiplier, StatTags.Strike).ToDouble();

        private static List<double> Cast(GameLoop loop, string skill)
        {
            loop.Events.Clear();
            loop.Combat.Lanternbearer.OnSkillCast(TestWorld.Data.Gems[skill]);
            return Damage(loop);
        }

        private static int Equip(GameLoop loop, string skill)
        {
            loop.Skills.Equip(TestWorld.Data.Gems[skill], role: SkillTestHero.ManualRole(loop.State.Skills.Count));
            return loop.State.Skills.Count - 1;
        }

        [Test]
        public void EveryHitLightsTheEnemyAndAFullyLitEnemyTakesMore()
        {
            Assert.That(Rules.LightPerHit, Is.EqualTo(0.01));
            var (loop, input) = Hero(Flat(LanternbearerStats.LightPower, 0.35), Flat(LanternbearerStats.LightShadowSeconds, 1000));
            Hits(loop, input, 50);
            Assert.That(loop.State.Lanternbearer.Light, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(loop.Combat.Lanternbearer.DamageTaken(), Is.EqualTo(1).Within(1e-9));

            Hits(loop, input, 50);
            Assert.That(loop.State.Lanternbearer.LitFull, Is.True);
            Assert.That(loop.Combat.Lanternbearer.DamageTaken(), Is.EqualTo(1.35).Within(1e-9));
        }

        [Test]
        public void TheShadowDimsAQuarterEveryTenSecondsEvenInAPause()
        {
            var (loop, input) = Hero(Flat(LanternbearerStats.LightPower, 0.35));
            Cast(loop, "lanternbearer_strong_1");
            Assert.That(loop.State.Lanternbearer.LitFull, Is.True);

            Wait(loop, Rules.ShadowSeconds);
            Assert.That(loop.State.Lanternbearer.Light, Is.EqualTo(1 - Rules.ShadowShare).Within(1e-9));
            Assert.That(loop.State.Lanternbearer.LitFull, Is.False);
        }

        [Test]
        public void ColossusLightNoonOilAndCarryAreRules()
        {
            var (boss, _) = Hero(Flat(LanternbearerStats.LightPower, 0.35), Flat(LanternbearerStats.LightBoss, 1), Flat(LanternbearerStats.LightOil, 1));
            Spawn(boss, EnemyType.Boss);
            boss.Tick();
            var pending = boss.State.PendingResource;
            Cast(boss, "lanternbearer_strong_1");
            Assert.That(boss.State.PendingResource - pending, Is.EqualTo(Rules.LightOil).Within(1e-9));
            Wait(boss, Rules.ShadowSeconds * 2);
            Assert.That(boss.State.Lanternbearer.LitFull, Is.True);

            var (carry, _) = Hero(Flat(LanternbearerStats.LightPower, 0.35), Flat(LanternbearerStats.LightCarry, 0.5));
            Cast(carry, "lanternbearer_strong_1");
            Spawn(carry, EnemyType.Normal);
            carry.Tick();
            Assert.That(carry.State.Lanternbearer.Light, Is.EqualTo(0.5).Within(1e-9));
        }

        [Test]
        public void ABurstOfLightFlaresThenDropsToHalf()
        {
            var (loop, _) = Hero(Flat(LanternbearerStats.LightPower, 0.4), Flat(LanternbearerStats.LightFlare, 1), Flat(LanternbearerStats.LightShadowSeconds, 1000));
            Cast(loop, "lanternbearer_strong_1");
            Assert.That(loop.Combat.Lanternbearer.DamageTaken(), Is.EqualTo(1 + 0.4 * Rules.FlareMultiplier).Within(1e-9));
            Wait(loop, Rules.FlareSeconds);
            Assert.That(loop.State.Lanternbearer.Light, Is.EqualTo(Rules.FlareKeep).Within(1e-9));
        }

        [Test]
        public void SoloFirefliesCircleHerAndHerCritsLightSparksForOil()
        {
            var (loop, input) = Hero(HeroClass.Lanternbearer, true, Flat(LanternbearerStats.SparkPower, 2));
            Hits(loop, input, Rules.FireflyHits);
            Assert.That(loop.State.Boons.Fireflies, Is.EqualTo(1));

            var damage = Hit(loop, input);
            Assert.That(damage.Count, Is.EqualTo(2));
            Assert.That(loop.State.Boons.Sparks, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void FirefliesBurnOutAndTheDyingOneFlares()
        {
            var (loop, input) = Hero(Flat(LanternbearerStats.SparkPower, 2), Flat(LanternbearerStats.FireflyBurst, 1));
            Cast(loop, "lanternbearer_path_2");
            Assert.That(loop.State.Boons.Fireflies, Is.EqualTo(3));
            loop.Events.Clear();
            Wait(loop, Rules.FireflySeconds);
            Assert.That(loop.State.Boons.Fireflies, Is.Zero);
            Assert.That(Damage(loop), Has.Count.EqualTo(1));
        }

        [Test]
        public void TheFireflyLimitHoldsAndEveryTenthHitCanSpark()
        {
            var (loop, input) = Hero(Flat(LanternbearerStats.SparkPower, 2), Flat(LanternbearerStats.SparkEvery, 1));
            for (var i = 0; i < 5; i++)
                Cast(loop, "lanternbearer_path_2");
            Assert.That(loop.State.Boons.Fireflies, Is.EqualTo(Rules.FireflyMax));

            var before = loop.State.Boons.Sparks;
            Hits(loop, input, Rules.SparkEvery);
            Assert.That(loop.State.Boons.Sparks - before, Is.EqualTo(1));
        }

        [Test]
        public void TheBeaconFlashesEveryFortySecondsAndMakesTheNextSkillFreeAndStronger()
        {
            Assert.That(Rules.BeaconSeconds, Is.EqualTo(40));
            var (loop, input) = Hero(Flat(LanternbearerStats.BeaconPower, 0.5));
            Wait(loop, Rules.BeaconSeconds);
            Assert.That(loop.State.Lanternbearer.Flashes, Is.EqualTo(1));
            Assert.That(loop.State.Boons.BeaconCharges, Is.EqualTo(1));

            var slot = Equip(loop, "lanternbearer_strong_1");
            loop.State.Resource = 0;
            Assert.That(loop.Skills.TryCast(slot), Is.EqualTo(CastResult.Cast));
            Assert.That(loop.State.Skills[slot].CooldownTicks, Is.Zero);
            Assert.That(loop.State.Boons.BeaconCharges, Is.Zero);
            Assert.That(loop.Skills.TryCast(slot), Is.EqualTo(CastResult.NotEnoughResource));
        }

        [Test]
        public void TwoSkillsSignalFireAndTheSilentLanternAreRules()
        {
            var (two, _) = Hero(Flat(LanternbearerStats.BeaconPower, 0.5), Flat(LanternbearerStats.BeaconCharges, 1));
            Cast(two, "lanternbearer_path_3");
            Assert.That(two.State.Boons.BeaconCharges, Is.EqualTo(2));

            var (signal, _) = Hero(Flat(LanternbearerStats.BeaconPower, 0.5), Flat(LanternbearerStats.BeaconCut, 2));
            var slot = Equip(signal, "lanternbearer_strong_3");
            signal.State.Skills[slot].CooldownTicks = 100;
            Cast(signal, "lanternbearer_path_3");
            Assert.That(signal.State.Skills[slot].CooldownTicks, Is.EqualTo(80));

            var (silent, _) = Hero(Flat(LanternbearerStats.BeaconPower, 0.5), Flat(LanternbearerStats.BeaconMute, 1));
            var mute = Equip(silent, "lanternbearer_strong_1");
            silent.State.Resource = 1000;
            Assert.That(silent.Skills.TryCast(mute), Is.EqualTo(CastResult.TraitCastsIt));
        }

        [Test]
        public void SharedSkillsWorkForEveryTrait()
        {
            var (loop, input) = Hero();
            loop.State.Resource = 40;
            Cast(loop, "lanternbearer_strong_2");
            loop.Tick();
            Assert.That(loop.State.Resource, Is.Zero);
            Assert.That(loop.State.TraitPartyDamage, Is.EqualTo(40 * 0.005).Within(1e-6));

            var pending = loop.State.PendingResource;
            Cast(loop, "lanternbearer_hit_2");
            Assert.That(loop.State.PendingResource - pending, Is.EqualTo(8).Within(1e-9));
        }

        [Test]
        public void TheBeamHitsHarderTheBrighterTheEnemy()
        {
            var (loop, input) = Hero(Flat(LanternbearerStats.LightPower, 0.35), Flat(LanternbearerStats.LightShadowSeconds, 1000));
            Hits(loop, input, 50);
            var expected = Expected(loop, 3 * 0.5);
            var beam = Cast(loop, "lanternbearer_hit_1");
            Assert.That(beam.Single() / expected, Is.EqualTo(1).Within(0.05));
        }

        [Test]
        public void OtherCharactersIgnoreLanternbearerStats()
        {
            var (loop, input) = Hero(HeroClass.Runedancer, false, Flat(LanternbearerStats.LightPower, 0.35), Flat(LanternbearerStats.SparkPower, 2), Flat(LanternbearerStats.BeaconPower, 0.5));
            Hits(loop, input, 120);
            Wait(loop, 70);

            Assert.That(loop.State.Lanternbearer.Light, Is.Zero);
            Assert.That(loop.State.Boons.Fireflies, Is.Zero);
            Assert.That(loop.State.Lanternbearer.Flashes, Is.Zero);
            Assert.That(loop.Combat.Lanternbearer.DamageTaken(), Is.EqualTo(1));
        }

        [Test]
        public void EveryForkOfLanternbearerIsARuleWithAText()
        {
            foreach (var path in TestWorld.Real.Ascendancy.SubclassesOf("lanternbearer"))
            {
                Assert.That(path.Trait!.PartyTextRu, Is.Not.Empty, path.Id);
                Assert.That(path.Trait.PartyTextEn, Is.Not.Empty, path.Id);
                foreach (var option in path.Trait.Forks.SelectMany(fork => fork.Choices))
                {
                    Assert.That(option.TextRu, Is.Not.Empty, option.Id);
                    Assert.That(option.TextEn, Is.Not.Empty, option.Id);
                    Assert.That(option.Modifiers.Any(modifier => modifier.Stat.Name != CombatStats.Damage.Name), Is.True, option.Id);
                }
            }
        }

        [Test]
        public void TheLanternbearerTreeHasTwentySixSmallFourteenMediumSixLegendaryAndFourCoreNodes()
        {
            var tree = TestWorld.Real.ClassTrees["lanternbearer"];
            var count = tree.Nodes.GroupBy(node => node.Kind).ToDictionary(group => group.Key, group => group.Count());

            Assert.That(count[TreeNodeKind.Small], Is.EqualTo(26));
            Assert.That(count[TreeNodeKind.Medium], Is.EqualTo(14));
            Assert.That(count[TreeNodeKind.Legendary], Is.EqualTo(6));
            Assert.That(count[TreeNodeKind.Core], Is.EqualTo(4));
            Assert.That(tree.Nodes.Count(node => node.Template.First.Modifiers.Concat(node.Template.Second.Modifiers).Concat(node.Template.Third.Modifiers)
                .Any(modifier => Game.Core.Party.CoopLine.IsCoop(modifier.Stat) || modifier.Stat.Name.EndsWith("Party")
                    || modifier.Stat.Name == LanternbearerStats.LightParty.Name || modifier.Stat.Name == LanternbearerStats.BeaconParty.Name)), Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void TheLanternbearerMechanicsDoNotAllocate()
        {
            var probeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (System.GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var (light, lightInput) = Hero(Flat(LanternbearerStats.LightPower, 0.35), Flat(LanternbearerStats.LightFlare, 1));
            var (flies, fliesInput) = Hero(HeroClass.Lanternbearer, true, Flat(LanternbearerStats.SparkPower, 2), Flat(LanternbearerStats.FireflyBurst, 1));
            var (beacon, beaconInput) = Hero(Flat(LanternbearerStats.BeaconPower, 0.5), Flat(LanternbearerStats.BeaconSeconds, -50));
            void Run(GameLoop loop, FakeInputSource input, int ticks)
            {
                for (var tick = 0; tick < ticks; tick++)
                {
                    input.PressKeys(1);
                    loop.Tick();
                    loop.Events.Clear();
                }
            }
            Run(light, lightInput, 600);
            Run(flies, fliesInput, 600);
            Run(beacon, beaconInput, 600);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            Run(light, lightInput, 300);
            Run(flies, fliesInput, 300);
            Run(beacon, beaconInput, 300);

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
