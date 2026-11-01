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
    public class ShardcallerTests
    {
        private static ShardcallerRules Rules => TestWorld.Data.Combat.Classes.Shardcaller;

        private static Modifier Flat(StatId stat, double value) => new Modifier(stat, ModifierKind.Flat, value);

        private static (GameLoop Loop, FakeInputSource Input) Hero(params Modifier[] modifiers) => Hero(HeroClass.Shardcaller, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            hero.Add(new Modifier(CombatStats.OverloadDisabled, ModifierKind.Flat, 1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            Spawn(loop);
            loop.State.BossTicksLeft = 100_000;
            loop.Tick();
            loop.Events.Clear();
            return (loop, input);
        }

        private static void Spawn(GameLoop loop) => loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));

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

        private static double PlainHit()
        {
            var (loop, input) = Hero();
            return Hit(loop, input).Single();
        }

        private static double Expected(GameLoop loop, double multiplier) =>
            loop.Combat.SkillDamage(loop.Combat.BaseKeyDamage, multiplier * loop.State.FocusMultiplier, StatTags.Keyboard).ToDouble();

        private static List<double> Cast(GameLoop loop, string skill)
        {
            loop.Events.Clear();
            loop.Combat.Shardcaller.OnSkillCast(TestWorld.Data.Gems[skill]);
            return Damage(loop);
        }

        [Test]
        public void EveryEighthHitPlantsACrystalUpToTenAndThePartyHitsHarder()
        {
            Assert.That(Rules.ShardEvery, Is.EqualTo(8));
            Assert.That(Rules.ShardMax, Is.EqualTo(10));
            var (loop, input) = Hero(Flat(ShardcallerStats.ShardPower, 1));

            Hits(loop, input, 7);
            Assert.That(loop.State.Shardcaller.Crystals, Is.Zero);
            Hit(loop, input);
            Assert.That(loop.State.Shardcaller.Crystals, Is.EqualTo(1));
            Assert.That(loop.State.TraitPartyDamage, Is.EqualTo(Rules.ShardArmor).Within(1e-9));

            Hits(loop, input, 8 * 12);
            Assert.That(loop.State.Shardcaller.Crystals, Is.EqualTo(Rules.ShardMax));
            Assert.That(loop.State.TraitPartyDamage, Is.EqualTo(Rules.ShardMax * Rules.ShardArmor).Within(1e-9));
        }

        [Test]
        public void ResonanceBurstsAllCrystalsForTheirCount()
        {
            var (loop, input) = Hero(Flat(ShardcallerStats.ShardPower, 1.5));
            Hits(loop, input, 8 * 4);
            var expected = Expected(loop, 1.5 * 4);

            var burst = Cast(loop, "shardcaller_path_1");

            Assert.That(loop.State.Shardcaller.Detonations, Is.EqualTo(1));
            Assert.That(burst.Single() / expected, Is.EqualTo(1).Within(0.01));
            Assert.That(loop.State.Shardcaller.Crystals, Is.Zero);
        }

        [Test]
        public void CrystalsOfASlainEnemyBurstOnTheNextWithFlyingShards()
        {
            var (loop, input) = Hero(Flat(ShardcallerStats.ShardPower, 1), Flat(ShardcallerStats.ShardOnKill, 1));
            Hits(loop, input, 8 * 3);
            var expected = Expected(loop, 3 * Rules.ShardOnKillShare);

            Spawn(loop);
            loop.Events.Clear();
            loop.Tick();

            Assert.That(loop.State.Shardcaller.Crystals, Is.Zero);
            Assert.That(Damage(loop).Single() / expected, Is.EqualTo(1).Within(0.05));

            var (plain, plainInput) = Hero(Flat(ShardcallerStats.ShardPower, 1));
            Hits(plain, plainInput, 8 * 3);
            Spawn(plain);
            plain.Events.Clear();
            plain.Tick();
            Assert.That(Damage(plain), Is.Empty);
        }

        [Test]
        public void TheResonantEchoKeepsHalfAndShrapnelHitsTheQueue()
        {
            var (echo, echoInput) = Hero(Flat(ShardcallerStats.ShardPower, 1), Flat(ShardcallerStats.ShardEcho, 1));
            Hits(echo, echoInput, 8 * 6);
            Cast(echo, "shardcaller_path_1");
            Assert.That(echo.State.Shardcaller.Crystals, Is.EqualTo(3));

            var (shrapnel, shrapnelInput) = Hero(Flat(ShardcallerStats.ShardPower, 1), Flat(ShardcallerStats.ShardQueue, 1));
            Hits(shrapnel, shrapnelInput, 8 * 2);
            var queued = shrapnel.State.QueueDamage[0].ToDouble();
            Cast(shrapnel, "shardcaller_path_1");
            Assert.That(shrapnel.State.QueueDamage[0].ToDouble(), Is.GreaterThan(queued));
        }

        [Test]
        public void ARockslideBurstsByItselfAtFullCount()
        {
            var (loop, input) = Hero(Flat(ShardcallerStats.ShardPower, 1), Flat(ShardcallerStats.ShardAvalanche, 1));

            Hits(loop, input, 8 * Rules.ShardMax);

            Assert.That(loop.State.Shardcaller.Detonations, Is.EqualTo(1));
            Assert.That(loop.State.Shardcaller.Crystals, Is.Zero);
        }

        [Test]
        public void SkillsPlantCrystalsAndTheCollapseBurstsThemTwice()
        {
            var (loop, _) = Hero(Flat(ShardcallerStats.ShardPower, 1));
            Cast(loop, "shardcaller_hit_2");
            Cast(loop, "shardcaller_queue_3");
            Assert.That(loop.State.Shardcaller.Crystals, Is.EqualTo(3));
            var expected = Expected(loop, 3 * 2);

            var burst = Cast(loop, "shardcaller_strong_1");

            Assert.That(burst.Single() / expected, Is.EqualTo(1).Within(0.01));
        }

        [Test]
        public void TheGardenGrowsAndRingsEvenWithoutHits()
        {
            Assert.That(Rules.GardenEvery, Is.EqualTo(16));
            Assert.That(Rules.GardenGrowSeconds, Is.EqualTo(3));
            var (loop, input) = Hero(Flat(ShardcallerStats.GardenPower, 0.5));
            Hits(loop, input, 15);
            Assert.That(loop.State.Shardcaller.Crystals, Is.Zero);
            Hit(loop, input);
            Assert.That(loop.State.Shardcaller.Crystals, Is.EqualTo(1));
            Assert.That(loop.State.Shardcaller.Size, Is.EqualTo(1));
            Cast(loop, "shardcaller_hit_2");
            loop.Events.Clear();

            var ticks = 0;
            while (loop.State.Shardcaller.Pulses == 0 && ticks++ < 100)
            {
                loop.Events.Clear();
                loop.Tick();
            }

            Assert.That(ticks, Is.EqualTo(Rules.GardenGrowSeconds * GameLoop.TicksPerSecond).Within(2));
            Assert.That(loop.State.Shardcaller.Size, Is.EqualTo(2));
            Assert.That(Damage(loop).Single() / Expected(loop, 0.5 * 2 * 2), Is.EqualTo(1).Within(0.05));
            Assert.That(loop.State.TraitPartyDamage, Is.EqualTo(2 * 2 * Rules.GardenArmor).Within(1e-9));
            Wait(loop, Rules.GardenGrowSeconds * 6);
            Assert.That(loop.State.Shardcaller.Size, Is.EqualTo(Rules.GardenSizeMax));
        }

        [Test]
        public void TheSeedAndTheTransplantStartTheNextEnemyWithAGarden()
        {
            var (seed, _) = Hero(Flat(ShardcallerStats.GardenPower, 0.5), Flat(ShardcallerStats.GardenSeed, 1));
            Spawn(seed);
            seed.Tick();
            Assert.That(seed.State.Shardcaller.Crystals, Is.EqualTo(1));
            Assert.That(seed.State.Shardcaller.Size, Is.EqualTo(1));

            var (transplant, _) = Hero(Flat(ShardcallerStats.GardenPower, 0.5), Flat(ShardcallerStats.GardenTransplant, 1));
            Cast(transplant, "shardcaller_queue_3");
            Cast(transplant, "shardcaller_path_2");
            Spawn(transplant);
            transplant.Tick();
            Assert.That(transplant.State.Shardcaller.Crystals, Is.EqualTo(3));
            Assert.That(transplant.State.Shardcaller.Size, Is.EqualTo(2));
        }

        [Test]
        public void AGardenAtFullSizeBloomsAndStartsAgain()
        {
            var (loop, _) = Hero(Flat(ShardcallerStats.GardenPower, 0.5), Flat(ShardcallerStats.GardenBloom, 1));
            Cast(loop, "shardcaller_hit_2");

            Wait(loop, Rules.GardenGrowSeconds * (Rules.GardenSizeMax - 1) + 0.1);

            Assert.That(loop.State.Shardcaller.Blooms, Is.EqualTo(1));
            Assert.That(loop.State.Shardcaller.Size, Is.EqualTo(1));
        }

        [Test]
        public void TheShellWeakensHitsAndItsCrackMakesThemFourfold()
        {
            Assert.That(Rules.ShellPenalty, Is.EqualTo(0.2));
            Assert.That(Rules.ShellEverySeconds, Is.EqualTo(5));
            var plain = PlainHit();
            var (loop, input) = Hero(Flat(ShardcallerStats.ShellPower, 4));

            var shelled = Hit(loop, input).Single();
            Assert.That(shelled / plain, Is.EqualTo(1 - Rules.ShellPenalty).Within(0.02));
            Assert.That(loop.State.TraitPartyDamage, Is.Zero);
            var guard = 0;
            while (!loop.State.Shardcaller.Cracked && guard++ < 100)
                loop.Tick();
            Assert.That(loop.Combat.Shardcaller.DamageTaken(), Is.EqualTo(4).Within(1e-9));
            Hit(loop, input);

            Assert.That(loop.State.Shardcaller.Cracks, Is.EqualTo(1));
            Assert.That(loop.State.TraitPartyDamage, Is.EqualTo(Rules.ShellPartyDamage).Within(1e-9));
            Wait(loop, Rules.ShellCrackSeconds);
            Assert.That(loop.State.Shardcaller.Cracked, Is.False);
            Assert.That(loop.State.TraitPartyDamage, Is.Zero);
        }

        [Test]
        public void TheRingOfGlassCracksAtOnceAndEveryFifthShatterIsDoubled()
        {
            var (loop, _) = Hero(Flat(ShardcallerStats.ShellPower, 4), Flat(ShardcallerStats.ShellShatter, 1), Flat(ShardcallerStats.ShellResonance, 10));
            loop.State.Resource = 0;
            for (var i = 0; i < 4; i++)
            {
                Cast(loop, "shardcaller_path_3");
                Assert.That(loop.State.Shardcaller.Cracked, Is.True);
                Assert.That(loop.Combat.Shardcaller.DamageTaken(), Is.EqualTo(4).Within(1e-9));
            }
            loop.Tick();
            Assert.That(loop.State.Resource, Is.EqualTo(40).Within(0.5));

            Cast(loop, "shardcaller_path_3");

            Assert.That(loop.State.Shardcaller.Cracks, Is.EqualTo(5));
            Assert.That(loop.Combat.Shardcaller.DamageTaken(), Is.EqualTo(4 * Rules.ShellShatterMultiplier).Within(1e-9));
        }

        [Test]
        public void SensitiveGlassBringsTheCrackCloserOnACriticalHit()
        {
            var (loop, _) = Hero(Flat(ShardcallerStats.ShellPower, 4), Flat(ShardcallerStats.ShellCritHaste, 1));
            var before = loop.State.Shardcaller.ShellTicks;

            loop.Combat.Shardcaller.AfterStrike(true);
            loop.Combat.Shardcaller.AfterStrike(false);

            Assert.That(loop.State.Shardcaller.ShellTicks - before, Is.EqualTo(System.Math.Round(Rules.ShellCritHasteSeconds * GameLoop.TicksPerSecond)));
        }

        [Test]
        public void ThePrisonAndTheHeartOfTheMountainWorkForEveryTrait()
        {
            var (loop, _) = Hero();
            Cast(loop, "shardcaller_strong_2");
            Assert.That(loop.Combat.Shardcaller.DamageTaken(), Is.EqualTo(Rules.PrisonMultiplier).Within(1e-9));

            Cast(loop, "shardcaller_strong_3");
            loop.Tick();
            Assert.That(loop.State.TraitPartyDamage, Is.EqualTo(Rules.HeartPartyDamage).Within(1e-9));
            Wait(loop, 7);
            Assert.That(loop.State.TraitPartyDamage, Is.Zero);
            Assert.That(loop.Combat.Shardcaller.DamageTaken(), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void OtherCharactersIgnoreShardcallerStats()
        {
            var (loop, input) = Hero(HeroClass.Runedancer, Flat(ShardcallerStats.ShardPower, 1), Flat(ShardcallerStats.ShellPower, 4));
            Hits(loop, input, 40);
            Wait(loop, 6);

            Assert.That(loop.State.Shardcaller.Crystals, Is.Zero);
            Assert.That(loop.State.Shardcaller.Cracks, Is.Zero);
            Assert.That(loop.Combat.Shardcaller.DamageTaken(), Is.EqualTo(1));
        }

        [Test]
        public void EveryForkOfShardcallerIsARuleWithAText()
        {
            foreach (var path in TestWorld.Real.Ascendancy.SubclassesOf("shardcaller"))
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
        public void TheShardcallerTreeHasTwentySixSmallFourteenMediumSixLegendaryAndFourCoreNodes()
        {
            var tree = TestWorld.Real.ClassTrees["shardcaller"];
            var count = tree.Nodes.GroupBy(node => node.Kind).ToDictionary(group => group.Key, group => group.Count());

            Assert.That(count[TreeNodeKind.Small], Is.EqualTo(26));
            Assert.That(count[TreeNodeKind.Medium], Is.EqualTo(14));
            Assert.That(count[TreeNodeKind.Legendary], Is.EqualTo(6));
            Assert.That(count[TreeNodeKind.Core], Is.EqualTo(4));
            Assert.That(tree.Nodes.Count(node => node.Template.First.Modifiers.Concat(node.Template.Second.Modifiers).Concat(node.Template.Third.Modifiers)
                .Any(modifier => modifier.Stat.Name.StartsWith("party") || modifier.When == ModifierCondition.InParty || modifier.Stat.Name.EndsWith("Armor") || modifier.Stat.Name.EndsWith("PartyDamage"))), Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void TheShardcallerMechanicsDoNotAllocate()
        {
            var probeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (System.GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var (shards, shardsInput) = Hero(Flat(ShardcallerStats.ShardPower, 1), Flat(ShardcallerStats.ShardQueue, 1), Flat(ShardcallerStats.ShardAvalanche, 1),
                Flat(ShardcallerStats.ShellPower, 4), Flat(ShardcallerStats.ShellShatter, 1));
            var (garden, gardenInput) = Hero(Flat(ShardcallerStats.GardenPower, 0.5), Flat(ShardcallerStats.GardenQueue, 1), Flat(ShardcallerStats.GardenBloom, 1));
            void Run(GameLoop loop, FakeInputSource input, int ticks)
            {
                for (var tick = 0; tick < ticks; tick++)
                {
                    input.PressKeys(1);
                    loop.Tick();
                    loop.Events.Clear();
                    loop.Drops.Flush();
                }
            }
            Run(shards, shardsInput, 600);
            Run(garden, gardenInput, 600);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            Run(shards, shardsInput, 300);
            Run(garden, gardenInput, 300);

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
