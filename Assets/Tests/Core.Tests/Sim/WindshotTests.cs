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
    public class WindshotTests
    {
        private static WindshotRules Rules => TestWorld.Data.Combat.Classes.Windshot;

        private static Modifier Flat(StatId stat, double value) => new Modifier(stat, ModifierKind.Flat, value);

        private static (GameLoop Loop, FakeInputSource Input) Hero(params Modifier[] modifiers) => Hero(HeroClass.Windshot, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            loop.State.BossTicksLeft = 100_000;
            loop.Tick();
            loop.Events.Clear();
            return (loop, input);
        }

        private static List<double> KeyDamage(GameLoop loop)
        {
            var damage = new List<double>();
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage && loop.Events[i].Source != DamageSource.Mouse)
                    damage.Add(loop.Events[i].Amount.ToDouble());
            }
            return damage;
        }

        private static List<double> Hit(GameLoop loop, FakeInputSource input, double mousePx = 0)
        {
            loop.Events.Clear();
            input.PressKeys(1);
            if (mousePx > 0)
                input.MoveMouse((float)mousePx);
            loop.Tick();
            var damage = KeyDamage(loop);
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

        private static void Move(GameLoop loop, FakeInputSource input, double px, int ticks = 1)
        {
            for (var i = 0; i < ticks; i++)
            {
                input.MoveMouse((float)(px / ticks));
                loop.Tick();
            }
        }

        private static void Tension(GameLoop loop, double value)
        {
            loop.State.Resource = value;
            loop.Tick();
        }

        private static void Cast(GameLoop loop, string skill) => loop.Combat.Windshot.OnSkillCast(TestWorld.Data.Gems[skill]);

        [Test]
        public void EverySixthHitIsALongShotThatSpendsTension()
        {
            Assert.That(Rules.ShotEvery, Is.EqualTo(6));
            Assert.That(Rules.ShotSpend, Is.EqualTo(25));
            var (loop, input) = Hero(Flat(WindshotStats.ShotPower, 1));
            Tension(loop, 50);
            var before = loop.State.Resource;

            var plain = Hit(loop, input).Single();
            Hits(loop, input, 4);
            Assert.That(loop.State.Windshot.Shots, Is.Zero);
            var tension = loop.State.Resource;
            var shot = Hit(loop, input);

            Assert.That(loop.State.Windshot.Shots, Is.EqualTo(1));
            Assert.That(shot, Has.Count.EqualTo(2));
            Assert.That(shot[0] / plain, Is.GreaterThan(0.9));
            Assert.That(shot[1] / shot[0], Is.EqualTo(loop.Combat.Windshot.ShotMultiplier(tension)).Within(0.05));
            Assert.That(loop.State.Resource, Is.LessThan(before - Rules.ShotSpend + 5));
        }

        [Test]
        public void AShotAtFullTensionLeavesTheWindMarkForThePartyCrit()
        {
            var (loop, input) = Hero(Flat(WindshotStats.ShotPower, 1));
            Tension(loop, 1000);
            Hits(loop, input, 6);

            Assert.That(loop.State.HunterMarkTicks, Is.GreaterThan(0));
            loop.Tick();
            Assert.That(loop.State.TraitPartyCritChance, Is.EqualTo(Rules.ShotPartyCrit).Within(1e-9));

            var (half, halfInput) = Hero(Flat(WindshotStats.ShotPower, 1));
            Tension(half, 50);
            Hits(half, halfInput, 6);
            Assert.That(half.State.HunterMarkTicks, Is.Zero);
        }

        [Test]
        public void ExhaleShootsOnTheFirstHitAfterAPause()
        {
            var (loop, input) = Hero(Flat(WindshotStats.ShotPower, 1), Flat(WindshotStats.ShotPause, 1));
            Tension(loop, 50);
            Hit(loop, input);
            Wait(loop, Rules.ShotPauseSeconds + 0.1);

            Hit(loop, input);

            Assert.That(loop.State.Windshot.Shots, Is.EqualTo(1));
        }

        [Test]
        public void APiercingShotAlsoHitsTheQueue()
        {
            var (loop, input) = Hero(Flat(WindshotStats.ShotPower, 1), Flat(WindshotStats.ShotPierce, 1));
            Tension(loop, 50);
            var queued = loop.State.QueueDamage[0];

            Hits(loop, input, 6);

            Assert.That(loop.State.QueueDamage[0].ToDouble(), Is.GreaterThan(queued.ToDouble()));
        }

        [Test]
        public void AStormArrowAtFullTensionSpendsItAll()
        {
            var (loop, input) = Hero(Flat(WindshotStats.ShotPower, 1), Flat(WindshotStats.ShotStorm, 1));
            Tension(loop, 1000);

            Hits(loop, input, 6);

            Assert.That(loop.State.Resource, Is.LessThan(10));
        }

        [Test]
        public void ThePatientArcherShootsByItselfEveryThreeSeconds()
        {
            var (loop, input) = Hero(Flat(WindshotStats.ShotPower, 1), Flat(WindshotStats.ShotAuto, 1));
            Tension(loop, 50);

            Hits(loop, input, 6);
            Assert.That(loop.State.Windshot.Shots, Is.Zero);
            Wait(loop, Rules.ShotAutoSeconds);

            Assert.That(loop.State.Windshot.Shots, Is.EqualTo(1));
        }

        [Test]
        public void ACalmComesAfterFourStillSecondsAndEveryHitCritsWeaker()
        {
            Assert.That(Rules.CalmStillSeconds, Is.EqualTo(4));
            var (loop, input) = Hero(Flat(WindshotStats.CalmPower, 1));
            Move(loop, input, 100);
            Wait(loop, 3.8);
            Assert.That(loop.Combat.Windshot.InCalm, Is.False);
            var plain = Hit(loop, input).Single();
            Wait(loop, 0.3);
            Assert.That(loop.Combat.Windshot.InCalm, Is.True);

            var calm = Hit(loop, input).Single();

            var crit = loop.Combat.CritMultiplier(StatTags.Keyboard);
            Assert.That(calm / plain, Is.EqualTo(crit * (1 - Rules.CalmCritPenalty)).Within(0.05));
            Assert.That(loop.State.TraitPartyCritMultiplier, Is.EqualTo(Rules.CalmPartyCritMultiplier).Within(1e-9));
            Move(loop, input, 100);
            Assert.That(loop.Combat.Windshot.InCalm, Is.False);
            Assert.That(loop.State.TraitPartyCritMultiplier, Is.Zero);
        }

        [Test]
        public void ALightStepKeepsTheCalmButGivesNoTension()
        {
            var (loop, input) = Hero(Flat(WindshotStats.CalmPower, 1), Flat(WindshotStats.CalmDrift, 250));
            Move(loop, input, 20, 50);
            Assert.That(loop.Combat.Windshot.InCalm, Is.True);
            loop.State.Resource = 0;
            Move(loop, input, 20, 10);
            var fromStep = loop.State.Resource;

            Assert.That(fromStep, Is.LessThanOrEqualTo(1.0 + 1e-6));
            Move(loop, input, 500);
            Assert.That(loop.Combat.Windshot.InCalm, Is.False);
        }

        [Test]
        public void DeadCalmHalvesThePenaltyButWeakensHitsOutside()
        {
            var (loop, _) = Hero(Flat(WindshotStats.CalmPower, 1), Flat(WindshotStats.CalmDead, 1));

            Assert.That(loop.Combat.Windshot.CalmPenalty, Is.EqualTo(Rules.CalmCritPenalty * Rules.CalmDeadShare).Within(1e-9));
            Assert.That(loop.Combat.Windshot.StrikeMultiplier(), Is.EqualTo(Rules.CalmDeadOutside).Within(1e-9));
        }

        [Test]
        public void TheFirstArrowOfACalmHitsFourTimes()
        {
            var (loop, input) = Hero(Flat(WindshotStats.CalmPower, 1), Flat(WindshotStats.CalmFirst, 1));
            Wait(loop, Rules.CalmStillSeconds + 0.2);

            Assert.That(loop.Combat.Windshot.StrikeMultiplier(), Is.EqualTo(Rules.CalmFirstMultiplier).Within(1e-9));
            Hit(loop, input);
            Assert.That(loop.Combat.Windshot.StrikeMultiplier(), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void ABrokenCalmBringsTheStormHits()
        {
            var (loop, input) = Hero(Flat(WindshotStats.CalmPower, 1), Flat(WindshotStats.CalmStorm, 1));
            Wait(loop, Rules.CalmStillSeconds + 0.2);
            Move(loop, input, 100);

            Assert.That(loop.State.Windshot.StormHitsLeft, Is.EqualTo(Rules.CalmStormHits));
            Assert.That(loop.Combat.Windshot.StrikeMultiplier(), Is.EqualTo(Rules.CalmStormMultiplier).Within(1e-9));
            Hits(loop, input, Rules.CalmStormHits);
            Assert.That(loop.State.Windshot.StormHitsLeft, Is.Zero);
        }

        [Test]
        public void EndlessCalmNeverBreaks()
        {
            var (loop, input) = Hero(Flat(WindshotStats.CalmPower, 1), Flat(WindshotStats.CalmEternal, 1));
            Move(loop, input, 1000, 5);

            Assert.That(loop.Combat.Windshot.InCalm, Is.True);
            Assert.That(loop.Combat.Windshot.CalmPenalty, Is.EqualTo(Rules.CalmEternalPenalty).Within(1e-9));
        }

        [Test]
        public void TheSilentShotGivesACalmAtOnce()
        {
            var (loop, input) = Hero(Flat(WindshotStats.CalmPower, 1));
            Move(loop, input, 100);
            Cast(loop, "windshot_path_2");

            Assert.That(loop.Combat.Windshot.InCalm, Is.True);
        }

        [Test]
        public void ThreeThousandPixelsGiveAGustOfTenDoubleArrows()
        {
            Assert.That(Rules.GustPx, Is.EqualTo(3000));
            Assert.That(Rules.GustHits, Is.EqualTo(10));
            var (loop, input) = Hero(Flat(WindshotStats.GustPower, 0.5));
            Move(loop, input, 2900, 10);
            Assert.That(loop.State.Windshot.GustActive, Is.False);
            Move(loop, input, 200);
            Assert.That(loop.State.Windshot.GustHitsLeft, Is.EqualTo(Rules.GustHits));
            loop.Tick();

            var gust = Hit(loop, input);

            Assert.That(gust, Has.Count.EqualTo(2));
            Assert.That(gust[1] / gust[0], Is.EqualTo(0.5).Within(0.05));
            Hits(loop, input, Rules.GustHits - 1);
            Assert.That(loop.State.Windshot.GustActive, Is.False);
        }

        [Test]
        public void GustsStoreUpToFiveAndGiveThePartyResource()
        {
            var (loop, input) = Hero(Flat(WindshotStats.GustPower, 0.5));
            Move(loop, input, 30000, 20);

            Assert.That(loop.State.Windshot.Gusts, Is.EqualTo(Rules.GustMax));
            Assert.That(loop.State.TraitPartyResource, Is.EqualTo(Rules.GustPartyResource).Within(1e-9));
        }

        [Test]
        public void AGaleRisesOnFiveGustsAndDoublesEveryHit()
        {
            var (loop, input) = Hero(Flat(WindshotStats.GustPower, 0.5), Flat(WindshotStats.GustStorm, 1));
            Move(loop, input, 3000 * 4 + 100, 10);
            Assert.That(loop.State.Windshot.GustActive, Is.False);
            Move(loop, input, 3000, 2);

            Assert.That(loop.State.Windshot.GustStormTicks, Is.GreaterThan(0));
            Hits(loop, input, 20);
            Assert.That(loop.State.Windshot.GustActive, Is.True);
        }

        [Test]
        public void TheWindmillBringsGustsByItself()
        {
            var (loop, _) = Hero(Flat(WindshotStats.GustPower, 0.5), Flat(WindshotStats.GustTimer, 1));
            Wait(loop, Rules.GustTimerSeconds + 0.1);

            Assert.That(loop.State.Windshot.GustsStarted, Is.EqualTo(1));
        }

        [Test]
        public void SkillsDrawTensionAndBringGustsAndShots()
        {
            var (loop, _) = Hero(Flat(WindshotStats.ShotPower, 1), Flat(WindshotStats.GustPower, 0.5));
            Tension(loop, 0);
            var before = loop.State.Resource;
            Cast(loop, "windshot_hit_2");
            loop.Tick();
            Assert.That(loop.State.Resource - before, Is.GreaterThanOrEqualTo(8 - 1e-6));

            Cast(loop, "windshot_path_3");
            Assert.That(loop.State.Windshot.GustsStarted, Is.EqualTo(1));
            Assert.That(loop.State.Windshot.Gusts, Is.EqualTo(1));

            Cast(loop, "windshot_path_1");
            Assert.That(loop.State.Windshot.Shots, Is.EqualTo(1));
        }

        [Test]
        public void OtherCharactersIgnoreWindshotStats()
        {
            var (loop, input) = Hero(HeroClass.Runedancer, Flat(WindshotStats.ShotPower, 1), Flat(WindshotStats.CalmPower, 1), Flat(WindshotStats.GustPower, 0.5));
            Wait(loop, 5);
            Move(loop, input, 9000, 10);
            Hits(loop, input, 12);

            Assert.That(loop.State.Windshot.Shots, Is.Zero);
            Assert.That(loop.State.Windshot.GustsStarted, Is.Zero);
            Assert.That(loop.Combat.Windshot.InCalm, Is.False);
            Assert.That(loop.State.StepTensionBlocked, Is.False);
        }

        [Test]
        public void EveryForkOfWindshotIsARuleWithAText()
        {
            foreach (var path in TestWorld.Real.Ascendancy.SubclassesOf("windshot"))
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
        public void TheWindshotTreeHasTwentySixSmallFourteenMediumSixLegendaryAndFourCoreNodes()
        {
            var tree = TestWorld.Real.ClassTrees["windshot"];
            var count = tree.Nodes.GroupBy(node => node.Kind).ToDictionary(group => group.Key, group => group.Count());

            Assert.That(count[TreeNodeKind.Small], Is.EqualTo(26));
            Assert.That(count[TreeNodeKind.Medium], Is.EqualTo(14));
            Assert.That(count[TreeNodeKind.Legendary], Is.EqualTo(6));
            Assert.That(count[TreeNodeKind.Core], Is.EqualTo(4));
            Assert.That(tree.Nodes.Count(node => node.Template.First.Modifiers.Concat(node.Template.Second.Modifiers).Concat(node.Template.Third.Modifiers)
                .Any(modifier => modifier.Stat.Name.StartsWith("party") || modifier.When == ModifierCondition.InParty || modifier.Stat.Name.EndsWith("PartyCrit") || modifier.Stat.Name.EndsWith("PartyResource"))), Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void TheWindshotMechanicsDoNotAllocate()
        {
            var probeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (System.GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var (loop, input) = Hero(Flat(WindshotStats.ShotPower, 1), Flat(WindshotStats.ShotPierce, 1), Flat(WindshotStats.CalmPower, 1),
                Flat(WindshotStats.CalmStorm, 1), Flat(WindshotStats.GustPower, 0.5), Flat(WindshotStats.GustQueue, 1));
            void Run(int ticks)
            {
                for (var tick = 0; tick < ticks; tick++)
                {
                    input.PressKeys(1);
                    if (tick % 60 < 20)
                        input.MoveMouse(300);
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
