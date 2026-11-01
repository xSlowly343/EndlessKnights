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
    public class DuelistTests
    {
        private static DuelistRules Rules => TestWorld.Data.Combat.Classes.Duelist;

        private static Modifier Flat(StatId stat, double value) => new Modifier(stat, ModifierKind.Flat, value);

        private static (GameLoop Loop, FakeInputSource Input) Hero(params Modifier[] modifiers) => Hero(HeroClass.Duelist, modifiers);

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
            loop.Tick();
            loop.Events.Clear();
            return (loop, input);
        }

        private static List<GameEvent> DamageEvents(GameLoop loop)
        {
            var damage = new List<GameEvent>();
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage)
                    damage.Add(loop.Events[i]);
            }
            return damage;
        }

        private static List<double> Hit(GameLoop loop, FakeInputSource input)
        {
            loop.Events.Clear();
            input.PressKeys(1);
            loop.Tick();
            var damage = DamageEvents(loop).Select(e => e.Amount.ToDouble()).ToList();
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

        private static void Cast(GameLoop loop, string skill) => loop.Combat.Duelist.OnSkillCast(TestWorld.Data.Gems[skill]);

        private static void Rush(GameLoop loop, FakeInputSource input, double seconds)
        {
            for (var i = 0; i < System.Math.Round(seconds * GameLoop.TicksPerSecond); i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }
        }

        [Test]
        public void TheBreachOpensEveryFourSecondsForOneSecond()
        {
            Assert.That(Rules.BreachEverySeconds, Is.EqualTo(4));
            Assert.That(Rules.BreachSeconds, Is.EqualTo(1));
            var (loop, _) = Hero(Flat(DuelistStats.BreachLunge, 2));

            Wait(loop, 3.8);
            Assert.That(loop.State.Duelist.BreachOpen, Is.False);
            Wait(loop, 0.3);
            Assert.That(loop.State.Duelist.BreachOpen, Is.True);
            Wait(loop, 1);
            Assert.That(loop.State.Duelist.BreachOpen, Is.False);
        }

        [Test]
        public void ALungeIntoTheBreachHitsThreeTimesAndGivesHonor()
        {
            var (loop, input) = Hero(Flat(DuelistStats.BreachLunge, 2));
            var resource = loop.State.Resource;
            Assert.That(loop.Combat.Duelist.StrikeMultiplier(), Is.EqualTo(1));
            Hit(loop, input);
            var plainHonor = loop.State.Resource - resource;

            Cast(loop, "duelist_path_1");
            resource = loop.State.Resource;
            Assert.That(loop.Combat.Duelist.StrikeMultiplier(), Is.EqualTo(3).Within(1e-9));
            Hit(loop, input);

            Assert.That(loop.State.Resource - resource - plainHonor, Is.EqualTo(Rules.BreachHonor).Within(1e-6));
            Assert.That(loop.State.Duelist.Lunges, Is.EqualTo(1));
        }

        [Test]
        public void ThePartyHitsHarderWhileTheBreachIsOpen()
        {
            var (loop, _) = Hero(Flat(DuelistStats.BreachLunge, 2));
            Cast(loop, "duelist_path_1");
            loop.Tick();

            Assert.That(loop.State.TraitPartyDamage, Is.EqualTo(Rules.BreachPartyDamage).Within(1e-9));
            Assert.That(loop.PartyBuffs.OwnValue(0), Is.GreaterThanOrEqualTo(Rules.BreachPartyDamage - 1e-9));
            Wait(loop, 1.5);
            Assert.That(loop.State.TraitPartyDamage, Is.Zero);
        }

        [Test]
        public void PressExtendsTheBreachWithEveryLunge()
        {
            var (plain, plainInput) = Hero(Flat(DuelistStats.BreachLunge, 2));
            var (press, pressInput) = Hero(Flat(DuelistStats.BreachLunge, 2), Flat(DuelistStats.BreachPress, 0.1));
            Cast(plain, "duelist_path_1");
            Cast(press, "duelist_path_1");

            Hits(plain, plainInput, 3);
            Hits(press, pressInput, 3);

            Assert.That(plain.State.Duelist.BreachOpenTicks, Is.EqualTo(4));
            Assert.That(press.State.Duelist.BreachOpenTicks, Is.EqualTo(7));
        }

        [Test]
        public void ADoubleLungeStrikesTwiceButGivesNoHonor()
        {
            var (loop, input) = Hero(Flat(DuelistStats.BreachLunge, 2), Flat(DuelistStats.BreachDouble, 1));
            var resource = loop.State.Resource;
            Hit(loop, input);
            var plainHonor = loop.State.Resource - resource;
            Cast(loop, "duelist_path_1");
            resource = loop.State.Resource;

            Assert.That(loop.Combat.Duelist.StrikeMultiplier(), Is.EqualTo(6 * Rules.BreachDoubleShare).Within(1e-9));
            Hit(loop, input);

            Assert.That(loop.State.Resource - resource, Is.EqualTo(plainHonor).Within(1e-6));
        }

        [Test]
        public void ExposedWeaknessTradesTheLungeForPartyDamage()
        {
            var (loop, input) = Hero(Flat(DuelistStats.BreachLunge, 2), Flat(DuelistStats.BreachExpose, 0.05));
            Cast(loop, "duelist_path_1");

            Assert.That(loop.Combat.Duelist.StrikeMultiplier(), Is.EqualTo(1));
            Hits(loop, input, 3);

            Assert.That(loop.State.Duelist.ExposeShare, Is.EqualTo(0.15).Within(1e-9));
            Assert.That(loop.State.TraitPartyDamage, Is.EqualTo(Rules.BreachPartyDamage + 0.15).Within(1e-9));
        }

        [Test]
        public void TheEndlessOpeningKeepsTheBreachOpenWithoutHonor()
        {
            var (loop, input) = Hero(Flat(DuelistStats.BreachLunge, 2), Flat(DuelistStats.BreachAlways, 1));
            var (plain, plainInput) = Hero();
            var resource = loop.State.Resource;
            var plainResource = plain.State.Resource;

            Hit(loop, input);
            Hit(plain, plainInput);

            Assert.That(loop.State.Duelist.BreachOpen, Is.True);
            Assert.That(loop.Combat.Duelist.StrikeMultiplier(), Is.EqualTo(3).Within(1e-9));
            Assert.That(loop.State.Duelist.Lunges, Is.EqualTo(1));
            Assert.That(loop.State.Resource - resource, Is.EqualTo(plain.State.Resource - plainResource).Within(1e-6));
        }

        [Test]
        public void ReadingTheFoeWeakensHitsOutsideTheBreach()
        {
            var (loop, _) = Hero(Flat(DuelistStats.BreachLunge, 2), Flat(DuelistStats.BreachRead, 1));

            Assert.That(loop.Combat.Duelist.StrikeMultiplier(), Is.EqualTo(1 - Rules.BreachOutsidePenalty).Within(1e-9));
            Cast(loop, "duelist_path_1");
            Assert.That(loop.Combat.Duelist.StrikeMultiplier(), Is.EqualTo(3).Within(1e-9));
        }

        [Test]
        public void TheRiposteStrikesWhenTheBreachCloses()
        {
            var (loop, input) = Hero(Flat(DuelistStats.BreachLunge, 2), Flat(DuelistStats.BreachRiposte, 1));
            Cast(loop, "duelist_path_1");
            Hits(loop, input, 2);
            loop.Events.Clear();

            Wait(loop, 1);

            Assert.That(DamageEvents(loop).Count(e => e.Source == DamageSource.Skill), Is.EqualTo(1));
            Assert.That(loop.State.Duelist.LungesInBreach, Is.Zero);
        }

        [Test]
        public void TenHitsLeaveATouchAndFiveTouchesStrikeATouche()
        {
            Assert.That(Rules.ToucheEvery, Is.EqualTo(10));
            Assert.That(Rules.ToucheMarks, Is.EqualTo(5));
            var (loop, input) = Hero(Flat(DuelistStats.TouchePower, 10));

            Hits(loop, input, 49);
            Assert.That(loop.State.Duelist.Marks, Is.EqualTo(4));
            var fiftieth = Hit(loop, input);

            Assert.That(fiftieth, Has.Count.EqualTo(2));
            Assert.That(fiftieth[1] / fiftieth[0], Is.EqualTo(10).Within(1e-6));
            Assert.That(loop.State.Duelist.Touches, Is.EqualTo(1));
            Assert.That(loop.State.Duelist.Marks, Is.Zero);
        }

        private static void ReadyTouche(GameLoop loop)
        {
            loop.State.Duelist.Marks = loop.Combat.Duelist.MarksNeeded - 1;
            loop.State.Duelist.TouchProgress = loop.Combat.Duelist.TouchEvery - 1;
        }

        [Test]
        public void ATouchéKillsAnEnemyBelowTheThreshold()
        {
            var (loop, input) = Hero(Flat(DuelistStats.TouchePower, 10), Flat(DuelistStats.ToucheExecute, 0.12));
            var kills = loop.State.Kills;
            loop.State.EnemyHp = loop.State.EnemyMaxHp * 0.11;
            ReadyTouche(loop);

            Hit(loop, input);

            Assert.That(loop.State.Kills, Is.EqualTo(kills + 1));
        }

        private static void Boss(GameLoop loop)
        {
            loop.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
            loop.State.BossTicksLeft = 100_000;
            loop.Tick();
        }

        [TestCase(0.05, true)]
        [TestCase(0.1, false)]
        public void ABossFallsOnlyBelowHalfTheThreshold(double hpShare, bool falls)
        {
            var (loop, input) = Hero(Flat(DuelistStats.TouchePower, 0.01), Flat(DuelistStats.ToucheExecute, 0.12));
            Boss(loop);
            var kills = loop.State.Kills;
            loop.State.EnemyHp = loop.State.EnemyMaxHp * hpShare;
            ReadyTouche(loop);

            Hit(loop, input);

            Assert.That(loop.State.Kills > kills, Is.EqualTo(falls));
        }

        [Test]
        public void ASecondRoundCarriesTwoMarksToTheNextEnemy()
        {
            var (loop, input) = Hero(Flat(DuelistStats.TouchePower, 10), Flat(DuelistStats.ToucheExecute, 0.12), Flat(DuelistStats.ToucheCarry, 2));
            loop.State.EnemyHp = loop.State.EnemyMaxHp * 0.11;
            ReadyTouche(loop);

            Hit(loop, input);
            if (loop.State.Duelist.NewEnemy)
                loop.Tick();

            Assert.That(loop.State.Duelist.Marks, Is.EqualTo(2));
        }

        [Test]
        public void MarksBurnOutOnANewEnemyUnlessTheDuelIsUnfinished()
        {
            var (plain, _) = Hero(Flat(DuelistStats.TouchePower, 10));
            var (keep, _) = Hero(Flat(DuelistStats.TouchePower, 10), Flat(DuelistStats.ToucheKeep, 1));
            plain.State.Duelist.Marks = 3;
            keep.State.Duelist.Marks = 3;

            plain.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            keep.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            plain.Tick();
            keep.Tick();

            Assert.That(plain.State.Duelist.Marks, Is.Zero);
            Assert.That(keep.State.Duelist.Marks, Is.EqualTo(3));
        }

        [Test]
        public void APublicDuelRaisesThePartyInsteadOfStriking()
        {
            var (loop, input) = Hero(Flat(DuelistStats.TouchePower, 10), Flat(DuelistStats.TouchePublic, 0.3));
            ReadyTouche(loop);

            var hit = Hit(loop, input);

            Assert.That(hit, Has.Count.EqualTo(1));
            Assert.That(loop.State.Duelist.Touches, Is.EqualTo(1));
            Assert.That(loop.State.TraitPartyDamage, Is.EqualTo(0.3).Within(1e-9));
        }

        [Test]
        public void ATouchéOnABossGivesThePartyGold()
        {
            var (loop, input) = Hero(Flat(DuelistStats.TouchePower, 10));
            Boss(loop);
            ReadyTouche(loop);

            Hit(loop, input);

            Assert.That(loop.State.TraitPartyGold, Is.EqualTo(Rules.TouchePartyGold).Within(1e-9));
        }

        [Test]
        public void TheSaluteLeavesTwoMarksButNotWithFlawlessForm()
        {
            var (loop, _) = Hero(Flat(DuelistStats.TouchePower, 10));
            var (flawless, _) = Hero(Flat(DuelistStats.TouchePower, 10), Flat(DuelistStats.ToucheFlawless, 1));

            Cast(loop, "duelist_path_2");
            Cast(flawless, "duelist_path_2");

            Assert.That(loop.State.Duelist.Marks, Is.EqualTo(2));
            Assert.That(flawless.State.Duelist.Marks, Is.Zero);
            Assert.That(flawless.Combat.Duelist.MarksNeeded, Is.EqualTo(Rules.ToucheMarks - 1));
        }

        [Test]
        public void AFastDuelistCallsThePhantomAndHitsWeakerHerself()
        {
            var (loop, input) = Hero(Flat(DuelistStats.PhantomPower, 0.6));
            var (plain, plainInput) = Hero();

            Rush(loop, input, 1.5);
            Rush(plain, plainInput, 1.5);
            loop.Events.Clear();
            plain.Events.Clear();
            input.PressKeys(1);
            plainInput.PressKeys(1);
            loop.Tick();
            plain.Tick();
            var pair = DamageEvents(loop).Select(e => e.Amount.ToDouble()).ToList();
            var single = DamageEvents(plain).Single().Amount.ToDouble();

            Assert.That(loop.State.Duelist.PhantomFighting, Is.True);
            Assert.That(pair, Has.Count.EqualTo(2));
            Assert.That(pair[0] / single, Is.EqualTo(1 - Rules.PhantomSelfPenalty).Within(1e-6));
            Assert.That(pair[1] / single, Is.EqualTo(0.6).Within(1e-6));
        }

        [Test]
        public void ASlowDuelistFightsAlone()
        {
            var (loop, input) = Hero(Flat(DuelistStats.PhantomPower, 0.6));
            for (var i = 0; i < 30; i++)
            {
                input.PressKeys(i % 3 == 0 ? 1 : 0);
                loop.Tick();
            }

            Assert.That(loop.State.Duelist.PhantomFighting, Is.False);
            Assert.That(DamageEvents(loop).All(e => e.Source == DamageSource.Keyboard), Is.True);
        }

        [Test]
        public void SwitchingPlacesSendsThePhantomInWithoutHits()
        {
            var (loop, _) = Hero(Flat(DuelistStats.PhantomPower, 0.6));
            Cast(loop, "duelist_path_3");
            loop.Tick();

            Assert.That(loop.State.Duelist.PhantomFighting, Is.True);
            Wait(loop, 6);
            Assert.That(loop.State.Duelist.PhantomFighting, Is.False);
        }

        [Test]
        public void EveryThirtySecondsThePhantomSecondsTheParty()
        {
            var (loop, _) = Hero(Flat(DuelistStats.PhantomPower, 0.6));

            Wait(loop, Rules.PhantomPartyEverySeconds);

            Assert.That(loop.State.Duelist.PhantomAwayTicks, Is.GreaterThan(0));
            Assert.That(loop.State.TraitPartyDamage, Is.EqualTo(Rules.PhantomPartyDamage).Within(1e-9));
            Wait(loop, Rules.PhantomPartySeconds);
            Assert.That(loop.State.TraitPartyDamage, Is.Zero);
        }

        [Test]
        public void InARoleSwapOnlyThePhantomStrikes()
        {
            var (loop, input) = Hero(Flat(DuelistStats.PhantomPower, 1.5), Flat(DuelistStats.PhantomSwap, 1));

            var hit = Hit(loop, input);

            Assert.That(hit, Has.Count.EqualTo(1));
            Assert.That(DamageEvents(loop).Single().Source, Is.EqualTo(DamageSource.Skill));
        }

        [Test]
        public void TheFeintPhantomRaisesThePartyCritInsteadOfStriking()
        {
            var (loop, input) = Hero(Flat(DuelistStats.PhantomPower, 0.6), Flat(DuelistStats.PhantomFeint, 1));
            Rush(loop, input, 1.5);
            loop.Events.Clear();
            input.PressKeys(1);
            loop.Tick();

            Assert.That(DamageEvents(loop), Has.Count.EqualTo(1));
            Assert.That(loop.State.TraitPartyCritChance, Is.EqualTo(Rules.PhantomFeintCrit).Within(1e-9));
        }

        [Test]
        public void ThePatientSecondStrikesWhileTheDuelistPauses()
        {
            var (loop, _) = Hero(Flat(DuelistStats.PhantomPower, 0.6), Flat(DuelistStats.PhantomPause, 1));

            Wait(loop, 3);

            Assert.That(DamageEvents(loop).Count(e => e.Source == DamageSource.Skill), Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void OtherCharactersIgnoreDuelistStats()
        {
            var (loop, input) = Hero(HeroClass.Runedancer, Flat(DuelistStats.BreachLunge, 2), Flat(DuelistStats.TouchePower, 10), Flat(DuelistStats.PhantomPower, 0.6));

            Rush(loop, input, 6);

            Assert.That(loop.State.Duelist.Touches, Is.Zero);
            Assert.That(loop.State.Duelist.Lunges, Is.Zero);
            Assert.That(loop.State.Duelist.PhantomFighting, Is.False);
        }

        [Test]
        public void EveryForkOfDuelistIsARuleWithAText()
        {
            foreach (var path in TestWorld.Real.Ascendancy.SubclassesOf("duelist"))
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
        public void TheDuelistTreeHasTwentySixSmallFourteenMediumSixLegendaryAndFourCoreNodes()
        {
            var tree = TestWorld.Real.ClassTrees["duelist"];
            var count = tree.Nodes.GroupBy(node => node.Kind).ToDictionary(group => group.Key, group => group.Count());

            Assert.That(count[TreeNodeKind.Small], Is.EqualTo(26));
            Assert.That(count[TreeNodeKind.Medium], Is.EqualTo(14));
            Assert.That(count[TreeNodeKind.Legendary], Is.EqualTo(6));
            Assert.That(count[TreeNodeKind.Core], Is.EqualTo(4));
            Assert.That(tree.Nodes.Count(node => node.Template.First.Modifiers.Concat(node.Template.Second.Modifiers).Concat(node.Template.Third.Modifiers)
                .Any(modifier => modifier.Stat.Name.StartsWith("party") || modifier.When == ModifierCondition.InParty || modifier.Stat.Name.EndsWith("PartyDamage") || modifier.Stat.Name.EndsWith("PartyGold"))), Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void TheDuelistMechanicsDoNotAllocate()
        {
            var probeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (System.GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var (loop, input) = Hero(Flat(DuelistStats.BreachLunge, 2), Flat(DuelistStats.BreachPress, 0.1), Flat(DuelistStats.BreachRiposte, 1),
                Flat(DuelistStats.TouchePower, 10), Flat(DuelistStats.ToucheExecute, 0.12), Flat(DuelistStats.PhantomPower, 0.6), Flat(DuelistStats.PhantomLunge, 1));
            void Run(int ticks)
            {
                for (var tick = 0; tick < ticks; tick++)
                {
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
