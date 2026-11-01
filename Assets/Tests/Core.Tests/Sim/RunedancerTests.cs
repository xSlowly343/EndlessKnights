using System.Collections.Generic;
using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Tree;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class RunedancerTests
    {
        private static RunedancerRules Rules => TestWorld.Data.Combat.Classes.Runedancer;

        private static Modifier Flat(StatId stat, double value) => new Modifier(stat, ModifierKind.Flat, value);

        private static (GameLoop Loop, FakeInputSource Input) Hero(params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Runedancer }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            loop.Tick();
            loop.Events.Clear();
            return (loop, input);
        }

        private static List<double> DamageEvents(GameLoop loop)
        {
            var damage = new List<double>();
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage)
                    damage.Add(loop.Events[i].Amount.ToDouble());
            }
            return damage;
        }

        private static List<double> Hit(GameLoop loop, FakeInputSource input)
        {
            loop.Events.Clear();
            input.PressKeys(1);
            loop.Tick();
            var damage = DamageEvents(loop);
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
            for (var i = 0; i < seconds * GameLoop.TicksPerSecond; i++)
                loop.Tick();
        }

        [Test]
        public void FiveHitsWriteAWordThatFlaresTenTimesAHit()
        {
            Assert.That(Rules.WordLength, Is.EqualTo(5));
            var (loop, input) = Hero(Flat(RunedancerStats.RuneWord, 10));

            for (var i = 0; i < 4; i++)
                Assert.That(Hit(loop, input), Has.Count.EqualTo(1), "hit " + (i + 1));
            var fifth = Hit(loop, input);

            Assert.That(fifth, Has.Count.EqualTo(2));
            Assert.That(fifth[1] / fifth[0], Is.EqualTo(10).Within(1e-6));
            Assert.That(loop.State.Runedancer.Words, Is.EqualTo(1));
            Assert.That(loop.State.Runedancer.Runes, Is.Zero);
        }

        [Test]
        public void RunesFadeAfterTenSecondsOfPause()
        {
            var (loop, input) = Hero(Flat(RunedancerStats.RuneWord, 10));
            Hits(loop, input, 3);
            Wait(loop, 9);
            Assert.That(loop.State.Runedancer.Runes, Is.EqualTo(3));

            Wait(loop, Rules.FadeSeconds);
            Hits(loop, input, 2);

            Assert.That(loop.State.Runedancer.Words, Is.Zero);
            Assert.That(loop.State.Runedancer.Runes, Is.EqualTo(2));
        }

        [Test]
        public void ANewEnemyStartsWithoutRunesUnlessTheRuneOfLightCarriesThem()
        {
            var (plain, plainInput) = Hero(Flat(RunedancerStats.RuneWord, 10));
            var (light, lightInput) = Hero(Flat(RunedancerStats.RuneWord, 10), Flat(RunedancerStats.RuneCarry, 1));
            Hits(plain, plainInput, 3);
            Hits(light, lightInput, 3);

            plain.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            light.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            plain.Tick();
            light.Tick();

            Assert.That(plain.State.Runedancer.Runes, Is.Zero);
            Assert.That(light.State.Runedancer.Runes, Is.EqualTo(3));
        }

        [Test]
        public void AWordSealsTheEnemyAndTheSealRaisesThePartyForFourSeconds()
        {
            var (loop, input) = Hero(Flat(RunedancerStats.RuneWord, 10));
            Hits(loop, input, 5);

            Assert.That(loop.State.TraitPartyDamage, Is.EqualTo(Rules.SealDamage).Within(1e-9));
            Assert.That(loop.PartyBuffs.OwnValue(0), Is.GreaterThanOrEqualTo(Rules.SealDamage - 1e-9));
            Wait(loop, Rules.SealSeconds);
            Assert.That(loop.State.TraitPartyDamage, Is.Zero);
        }

        [Test]
        public void TheWeaveTradesTheFlareForAStrongerSeal()
        {
            var (loop, input) = Hero(Flat(RunedancerStats.RuneWord, 10), Flat(RunedancerStats.RuneWeave, 0.4));
            Hits(loop, input, 4);
            var fifth = Hit(loop, input);

            Assert.That(fifth, Has.Count.EqualTo(1));
            Assert.That(loop.State.TraitPartyDamage, Is.EqualTo(0.4).Within(1e-9));
        }

        [Test]
        public void TheRhymeSendsTheWordIntoTheNextEnemy()
        {
            var (loop, input) = Hero(Flat(RunedancerStats.RuneWord, 10), Flat(RunedancerStats.RuneRhyme, 1));
            Hits(loop, input, 5);

            Assert.That(loop.State.QueueDamage[0].Sign, Is.GreaterThan(0));
        }

        [Test]
        public void SilentScriptHitsDoNotHurtButTheWordIsFourTimesStronger()
        {
            var (plain, plainInput) = Hero(Flat(RunedancerStats.RuneWord, 10));
            var (silent, silentInput) = Hero(Flat(RunedancerStats.RuneWord, 10), Flat(RunedancerStats.RuneSilent, 1), new Modifier(RunedancerStats.RuneWord, ModifierKind.More, 3));
            Hits(plain, plainInput, 4);
            Hits(silent, silentInput, 4);
            var plainWord = Hit(plain, plainInput);
            var silentWord = Hit(silent, silentInput);

            Assert.That(silentWord, Has.Count.EqualTo(1));
            Assert.That(silentWord[0] / plainWord[1], Is.EqualTo(4).Within(0.05));
        }

        [Test]
        public void TheCalligrapherShortensTheWordButSkillsWriteNoRunes()
        {
            var (loop, input) = Hero(Flat(RunedancerStats.RuneWord, 10), Flat(RunedancerStats.RuneCalligrapher, 1));
            Assert.That(loop.Combat.Runedancer.WordLength, Is.EqualTo(Rules.WordLength - 1));

            loop.Combat.Runedancer.OnSkillCast(TestWorld.Data.Gems["runedancer_hit_2"]);
            Assert.That(loop.State.Runedancer.Runes, Is.Zero);
            Hits(loop, input, Rules.WordLength - 1);
            Assert.That(loop.State.Runedancer.Words, Is.EqualTo(1));
        }

        [Test]
        public void SkillsLayRunesAndTheAlphabetFinishesTheWord()
        {
            var (loop, input) = Hero(Flat(RunedancerStats.RuneWord, 10));
            loop.Combat.Runedancer.OnSkillCast(TestWorld.Data.Gems["runedancer_hit_2"]);
            Assert.That(loop.State.Runedancer.Runes, Is.EqualTo(2));

            loop.Combat.Runedancer.OnSkillCast(TestWorld.Data.Gems["runedancer_strong_1"]);
            Assert.That(loop.State.Runedancer.Words, Is.EqualTo(1));
            Assert.That(loop.State.Runedancer.Runes, Is.Zero);
        }

        [Test]
        public void EveryThirdHitHangsADaggerAndTheRainDropsThemAll()
        {
            Assert.That(Rules.BladeEvery, Is.EqualTo(3));
            var (loop, input) = Hero(Flat(RunedancerStats.BladeDamage, 1.5));
            Hits(loop, input, 5);
            var single = Hit(loop, input)[0];

            Assert.That(loop.State.Runedancer.Blades, Is.EqualTo(2));
            Assert.That(loop.State.TraitPartyCritChance, Is.EqualTo(2 * Rules.BladePartyCrit).Within(1e-9));
            loop.Events.Clear();
            loop.Combat.Runedancer.OnSkillCast(TestWorld.Data.Gems["runedancer_path_2"]);
            var rain = DamageEvents(loop).Single();

            Assert.That(loop.State.Runedancer.Blades, Is.Zero);
            Assert.That(rain / single, Is.EqualTo(3).Within(0.05));
        }

        [Test]
        public void DaggersStopAtTheLimitAndHalfMoveOnWithTheRemnant()
        {
            var (loop, input) = Hero(Flat(RunedancerStats.BladeDamage, 1.5), Flat(RunedancerStats.BladeKeep, 0.5));
            Hits(loop, input, 3 * (Rules.BladeMax + 2));
            Assert.That(loop.State.Runedancer.Blades, Is.EqualTo(Rules.BladeMax));

            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            loop.Tick();

            Assert.That(loop.State.Runedancer.Blades, Is.EqualTo(Rules.BladeMax / 2));
        }

        [Test]
        public void HundredHandsRainsByItselfAndCannotBeCastByHand()
        {
            var (loop, input) = Hero(Flat(RunedancerStats.BladeDamage, 1.5), Flat(RunedancerStats.BladeAutoRain, 1));
            Assert.That(loop.Combat.Runedancer.Forbids(TestWorld.Data.Gems["runedancer_path_2"]), Is.True);

            Hits(loop, input, 3 * Rules.BladeMax - 1);
            Assert.That(loop.State.Runedancer.Blades, Is.EqualTo(Rules.BladeMax - 1));
            Hit(loop, input);
            Assert.That(loop.State.Runedancer.Blades, Is.Zero);
        }

        [Test]
        public void AGlintHangsADaggerOnEveryCritInstead()
        {
            var (loop, input) = Hero(Flat(RunedancerStats.BladeDamage, 1.5), Flat(RunedancerStats.BladeOnCrit, 1), Flat(CombatStats.CritEvery, 2));
            Hits(loop, input, 6);

            Assert.That(loop.State.Runedancer.Blades, Is.EqualTo(3));
        }

        [Test]
        public void TheWhirlGrowsWithHitsPerSecondAndLingersAfterTheStop()
        {
            var (loop, input) = Hero(Flat(RunedancerStats.WhirlDamage, 1), Flat(RunedancerStats.WhirlLinger, 3));
            for (var tick = 0; tick < 20; tick++)
            {
                input.PressKeys(1);
                loop.Tick();
            }

            Assert.That(loop.State.Runedancer.WhirlBlades, Is.EqualTo(Rules.WhirlMax));
            Assert.That(loop.State.TraitPartyDamage, Is.EqualTo(Rules.WhirlMax * Rules.WhirlPartyDamage).Within(1e-9));
            Wait(loop, 3);
            Assert.That(loop.State.Runedancer.WhirlBlades, Is.EqualTo(Rules.WhirlMax));
            Wait(loop, 1.5);
            Assert.That(loop.State.Runedancer.WhirlBlades, Is.Zero);
        }

        [Test]
        public void TheWhirlStrikesWithAllItsDaggersOnceASecond()
        {
            Assert.That(Rules.WhirlHitsPerSecond, Is.EqualTo(1));
            var (loop, input) = Hero(Flat(RunedancerStats.WhirlDamage, 1), Flat(RunedancerStats.WhirlSteady, 4));
            var key = Hit(loop, input)[0];
            Wait(loop, 1);
            loop.Events.Clear();
            Wait(loop, 2);

            var strikes = DamageEvents(loop);
            Assert.That(loop.State.Runedancer.WhirlBlades, Is.EqualTo(4));
            Assert.That(strikes, Has.Count.EqualTo(2));
            Assert.That(strikes[0] / key, Is.EqualTo(4).Within(0.5));
        }

        [Test]
        public void TheSquallDiesAtOnceAndTheTwisterSpinsToTheLimit()
        {
            var (loop, input) = Hero(Flat(RunedancerStats.WhirlDamage, 1), Flat(RunedancerStats.WhirlLinger, 3), Flat(RunedancerStats.WhirlGale, 1));
            loop.Combat.Runedancer.OnSkillCast(TestWorld.Data.Gems["runedancer_path_3"]);
            loop.Tick();
            Assert.That(loop.State.Runedancer.WhirlBlades, Is.EqualTo(Rules.WhirlMax));

            Wait(loop, 7);
            Assert.That(loop.State.Runedancer.WhirlBlades, Is.Zero);
        }

        [Test]
        public void TheShadowStrikesForAPausedDancer()
        {
            var (loop, _) = Hero(Flat(RunedancerStats.RuneWord, 10), Flat(RunedancerStats.ShadowDancer, 1));
            Wait(loop, 3);

            Assert.That(loop.State.Runedancer.Words, Is.GreaterThan(0));
        }

        [Test]
        public void OtherCharactersIgnoreRunedancerStats()
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(Flat(RunedancerStats.RuneWord, 10));
            hero.Add(Flat(RunedancerStats.BladeDamage, 1.5));
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Duelist }, hero);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            for (var i = 0; i < 10; i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }

            Assert.That(loop.State.Runedancer.Words, Is.Zero);
            Assert.That(loop.State.Runedancer.Blades, Is.Zero);
        }

        [Test]
        public void EveryForkOfRunedancerIsARuleWithAText()
        {
            foreach (var path in TestWorld.Real.Ascendancy.SubclassesOf("runedancer"))
            {
                Assert.That(path.Trait!.PartyTextRu, Is.Not.Empty, path.Id);
                foreach (var option in path.Trait.Forks.SelectMany(fork => fork.Choices))
                {
                    Assert.That(option.TextRu, Is.Not.Empty, option.Id);
                    Assert.That(option.TextEn, Is.Not.Empty, option.Id);
                    Assert.That(option.Modifiers.Any(modifier => modifier.Stat.Name != CombatStats.Damage.Name), Is.True, option.Id);
                }
            }
        }

        [Test]
        public void TheRunedancerTreeHasTwentySixSmallFourteenMediumSixLegendaryAndFourCoreNodes()
        {
            var tree = TestWorld.Real.ClassTrees["runedancer"];
            var count = tree.Nodes.GroupBy(node => node.Kind).ToDictionary(group => group.Key, group => group.Count());

            Assert.That(count[TreeNodeKind.Small], Is.EqualTo(26));
            Assert.That(count[TreeNodeKind.Medium], Is.EqualTo(14));
            Assert.That(count[TreeNodeKind.Legendary], Is.EqualTo(6));
            Assert.That(count[TreeNodeKind.Core], Is.EqualTo(4));
            Assert.That(tree.Nodes.Where(node => node.Kind == TreeNodeKind.Core).All(node => node.Row >= 6), Is.True);
            Assert.That(tree.Nodes.Count(node => node.Template.First.Modifiers.Concat(node.Template.Second.Modifiers).Concat(node.Template.Third.Modifiers)
                .Any(modifier => modifier.Stat.Name.StartsWith("party") || modifier.When == ModifierCondition.InParty)), Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void TheLoaderRefusesACoreNodeInALowRow()
        {
            var templates = TestWorld.Real.TreeTemplates;
            var json = TestData.ReadGameDataFile(ClassTreeLoader.FileNameOf("runedancer"))
                .Replace("\"parent\": \"runedancer_r2c0\", \"passive\": true, \"kind\": \"medium\"", "\"parent\": \"runedancer_r2c0\", \"passive\": true, \"kind\": \"core\"")
                .Replace("\"id\": \"runedancer_r6c4\", \"row\": 6, \"column\": 4, \"parent\": \"runedancer_r2c6\", \"passive\": true, \"kind\": \"core\"", "\"id\": \"runedancer_r6c4\", \"row\": 6, \"column\": 4, \"parent\": \"runedancer_r2c6\", \"passive\": true, \"kind\": \"medium\"");

            var error = Assert.Throws<ContentException>(() => ClassTreeLoader.Load(templates, "runedancer", json));
            Assert.That(error!.Message, Does.Contain("start from row 6"));
        }

        [Test]
        public void TheRunedancerMechanicsDoNotAllocate()
        {
            var probeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (System.GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var (loop, input) = Hero(Flat(RunedancerStats.RuneWord, 10), Flat(RunedancerStats.BladeDamage, 1.5), Flat(RunedancerStats.WhirlDamage, 1),
                Flat(RunedancerStats.WhirlLinger, 3), Flat(RunedancerStats.BladeFall, 1), Flat(RunedancerStats.RuneRhyme, 1));
            void Run(int ticks)
            {
                for (var tick = 0; tick < ticks; tick++)
                {
                    if (tick % 2 == 0)
                        input.PressKeys(1);
                    loop.Tick();
                    loop.Events.Clear();
                    loop.Drops.Flush();
                }
            }
            Run(600);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            Run(300);

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
