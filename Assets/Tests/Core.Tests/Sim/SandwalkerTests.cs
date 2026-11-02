using System.Collections.Generic;
using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Tree;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Party;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class SandwalkerTests
    {
        private static SandwalkerRules Rules => TestWorld.Data.Combat.Classes.Sandwalker;

        private static Modifier Flat(StatId stat, double value) => new Modifier(stat, ModifierKind.Flat, value);

        private static (GameLoop Loop, FakeInputSource Input) Hero(params Modifier[] modifiers) => Hero(HeroClass.Sandwalker, modifiers);

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

        private static void Step(GameLoop loop, FakeInputSource input, double px, int ticks = 1)
        {
            for (var i = 0; i < ticks; i++)
            {
                input.MoveMouse((float)(px / ticks));
                loop.Tick();
            }
        }

        private static void Wait(GameLoop loop, double seconds)
        {
            for (var i = 0; i < System.Math.Round(seconds * GameLoop.TicksPerSecond); i++)
                loop.Tick();
        }

        private static double Expected(GameLoop loop, double multiplier) =>
            loop.Combat.SkillDamage(loop.Combat.BaseKeyDamage, multiplier * loop.State.FocusMultiplier, StatTags.Keyboard).ToDouble();

        private static List<double> Cast(GameLoop loop, string skill)
        {
            loop.Events.Clear();
            loop.Combat.Sandwalker.OnSkillCast(TestWorld.Data.Gems[skill]);
            return Damage(loop);
        }

        private static int Equip(GameLoop loop, string skill)
        {
            TestWorld.ArmFor(loop, TestWorld.Data.Gems[skill]);
            loop.Skills.Equip(TestWorld.Data.Gems[skill], role: SkillTestHero.ManualRole(loop.State.Skills.Count));
            return loop.State.Skills.Count - 1;
        }

        [Test]
        public void TheGlassStartsOnTopAndTurnsAfterTwoHundredHits()
        {
            Assert.That(Rules.FlipEvery, Is.EqualTo(200));
            var (loop, input) = Hero(Flat(SandwalkerStats.FlipPower, 0.5));
            Assert.That(loop.Combat.Sandwalker.DamageTaken(), Is.EqualTo(1.5).Within(1e-9));

            Hits(loop, input, 199);
            Assert.That(loop.State.Sandwalker.Bottom, Is.False);
            Hit(loop, input);

            Assert.That(loop.State.Sandwalker.Flips, Is.EqualTo(1));
            Assert.That(loop.State.Sandwalker.Bottom, Is.True);
            Assert.That(loop.Combat.Sandwalker.DamageTaken(), Is.EqualTo(1 - Rules.FlipPenalty).Within(1e-9));
            Hits(loop, input, 200);
            Assert.That(loop.State.Sandwalker.Bottom, Is.False);
        }

        [Test]
        public void SandStandsInAPause()
        {
            var (loop, input) = Hero(Flat(SandwalkerStats.FlipPower, 0.5));
            Hits(loop, input, 50);

            Wait(loop, 30);

            Assert.That(loop.State.Sandwalker.Grains, Is.EqualTo(50));
            Assert.That(loop.State.Sandwalker.Flips, Is.Zero);
        }

        [Test]
        public void OnTheBottomTheBossTimerStandsStillForTheHeroAndTheParty()
        {
            var (loop, input) = Hero(Flat(SandwalkerStats.FlipPower, 0.5));
            Spawn(loop, EnemyType.Boss);
            loop.Tick();
            var running = loop.State.BossTicksLeft;
            Wait(loop, 1);
            Assert.That(loop.State.BossTicksLeft, Is.LessThan(running));
            Assert.That(CoopSignals.Read(CoopSignal.SandBottom, loop.State), Is.Zero);

            Cast(loop, "sandwalker_path_1");
            Hits(loop, input, 150);
            Assert.That(loop.State.Sandwalker.Bottom, Is.True);
            loop.Tick();
            var stopped = loop.State.BossTicksLeft;
            Wait(loop, 3);

            Assert.That(loop.State.BossTicksLeft, Is.EqualTo(stopped));
            Assert.That(CoopSignals.Read(CoopSignal.SandBottom, loop.State), Is.EqualTo(1), "the bottom line stops the timer for the party");
        }

        [Test]
        public void AHandOnTheGlassTurnsItWithASkillAndStepsCanPourSand()
        {
            var (hand, _) = Hero(Flat(SandwalkerStats.FlipPower, 0.5), Flat(SandwalkerStats.FlipBySkill, 1), Flat(SandwalkerStats.FlipSand, 15));
            hand.State.Resource = 0;
            Cast(hand, "sandwalker_hit_2");
            Assert.That(hand.State.Sandwalker.Bottom, Is.True);
            hand.Tick();
            Assert.That(hand.State.Resource, Is.EqualTo(15 + 8).Within(1));

            var (step, stepInput) = Hero(Flat(SandwalkerStats.FlipPower, 0.5), Flat(SandwalkerStats.FlipStep, 1));
            Step(step, stepInput, Rules.FlipStepPx * 10, 5);
            Assert.That(step.State.Sandwalker.Grains, Is.EqualTo(10));
        }

        [Test]
        public void SandInTheEyesBlindsOnEveryTurnAndTheColossusGlassFlowsOnlyAtBosses()
        {
            var (blind, _) = Hero(Flat(SandwalkerStats.FlipPower, 0.5), Flat(SandwalkerStats.FlipBySkill, 1), Flat(SandwalkerStats.FlipBlind, 1));
            Cast(blind, "sandwalker_path_1");
            Assert.That(blind.State.BlindTicks, Is.EqualTo(System.Math.Round(Rules.Blind.Seconds * GameLoop.TicksPerSecond)));
            Assert.That(blind.Combat.Sandwalker.DamageTaken(), Is.EqualTo((1 - Rules.FlipPenalty) * (1 + Rules.Blind.DamageTaken)).Within(1e-9));

            var (colossus, input) = Hero(Flat(SandwalkerStats.FlipPower, 0.5), Flat(SandwalkerStats.FlipBossOnly, 1));
            Hits(colossus, input, 250);
            Assert.That(colossus.State.Sandwalker.Flips, Is.Zero);
            Assert.That(colossus.Combat.Sandwalker.DamageTaken(), Is.EqualTo(1.5).Within(1e-9));
            Spawn(colossus, EnemyType.Boss);
            Hits(colossus, input, 200);
            Assert.That(colossus.State.Sandwalker.Bottom, Is.True);
        }

        [Test]
        public void EveryThreeThousandPxOfStepsTheCaravanCampsForTheHeroAndTheAllies()
        {
            Assert.That(Rules.CampPx, Is.EqualTo(3000));
            var (loop, input) = Hero(Flat(SandwalkerStats.CaravanPower, 6));
            var slot = Equip(loop, "sandwalker_strong_3");
            var shortSlot = Equip(loop, "sandwalker_hit_1");
            loop.State.Skills[slot].CooldownTicks = 300;
            loop.State.Skills[shortSlot].CooldownTicks = 30;
            loop.State.Resource = 0;
            var expected = Expected(loop, 6);

            loop.Events.Clear();
            input.MoveMouse((float)Rules.CampPx);
            loop.Tick();

            Assert.That(loop.State.Sandwalker.Camps, Is.EqualTo(1));
            Assert.That(Damage(loop).Max() / expected, Is.EqualTo(1).Within(0.05));
            Assert.That(loop.State.Skills[slot].CooldownTicks, Is.EqualTo(300 - 1 - Rules.CampCutSeconds * GameLoop.TicksPerSecond).Within(1));
            Assert.That(loop.State.Skills[shortSlot].CooldownTicks, Is.EqualTo(29).Within(1));
            Assert.That(loop.State.TraitPartyResourceGift, Is.EqualTo(Rules.CampSand));
            Assert.That(CoopSignals.Read(CoopSignal.Camp, loop.State), Is.EqualTo(1));
            loop.Tick();
            Assert.That(loop.State.Resource, Is.GreaterThanOrEqualTo(Rules.CampSand));
            Assert.That(loop.State.Sandwalker.Camps, Is.EqualTo(1), "without steps the caravan stands");
        }

        [Test]
        public void PackCamelsWalkWithoutStepsAndTheHaltCampsAtOnce()
        {
            var (camels, _) = Hero(Flat(SandwalkerStats.CaravanPower, 6), Flat(SandwalkerStats.CaravanIdle, 1));
            Wait(camels, Rules.CampPx / Rules.CaravanIdlePx + 0.5);
            Assert.That(camels.State.Sandwalker.Camps, Is.EqualTo(1));

            var (halt, _) = Hero(Flat(SandwalkerStats.CaravanPower, 6));
            Cast(halt, "sandwalker_path_2");
            Assert.That(halt.State.Sandwalker.Camps, Is.EqualTo(1));
        }

        [Test]
        public void TheOasisStrikesThriceAndTheCaravanseraiCutsEveryCooldown()
        {
            var (oasis, input) = Hero(Flat(SandwalkerStats.CaravanPower, 6), Flat(SandwalkerStats.CaravanOasis, 1));
            for (var i = 0; i < Rules.OasisEvery - 1; i++)
                Step(oasis, input, Rules.CampPx);
            var expected = Expected(oasis, 6 * Rules.OasisMultiplier);
            oasis.Events.Clear();
            Step(oasis, input, Rules.CampPx);
            Assert.That(Damage(oasis).Max() / expected, Is.EqualTo(1).Within(0.05));
            Assert.That(oasis.State.TraitPartyResourceGift, Is.EqualTo(Rules.CampSand * Rules.OasisSandMultiplier).Within(1e-9));

            var (all, allInput) = Hero(Flat(SandwalkerStats.CaravanPower, 6), Flat(SandwalkerStats.CaravanAll, 1),
                CoopStatNames.Line("{ 'to': 'party', 'stat': 'gold', 'kind': 'increased', 'value': 0.25, 'after': { 'signal': 'camp', 'seconds': 10 } }"));
            var first = Equip(all, "sandwalker_strong_3");
            var second = Equip(all, "sandwalker_strong_2");
            all.State.Skills[first].CooldownTicks = 300;
            all.State.Skills[second].CooldownTicks = 200;
            Step(all, allInput, Rules.CampPx);
            Assert.That(all.State.Skills[first].CooldownTicks, Is.EqualTo(289).Within(1));
            Assert.That(all.State.Skills[second].CooldownTicks, Is.EqualTo(189).Within(1));
            all.Tick();
            Assert.That(all.PartyBuffs.CoopApplied(TestWorld.Data.Party.Buffs.Coop.IndexOf(CombatStats.Gold, ModifierKind.Increased)), Is.EqualTo(0.25).Within(1e-9));
        }

        [Test]
        public void StepsRaiseMiragesThatStrikeEverySecondAndFade()
        {
            Assert.That(Rules.MiragePx, Is.EqualTo(2000));
            Assert.That(Rules.MirageMax, Is.EqualTo(5));
            var (loop, input) = Hero(Flat(SandwalkerStats.MiragePower, 0.6));
            Assert.That(loop.Combat.Sandwalker.DamageTaken(), Is.EqualTo(1 - Rules.MirageSelfPenalty).Within(1e-9));

            Step(loop, input, Rules.MiragePx * 2);
            Assert.That(loop.State.Sandwalker.Mirages, Is.EqualTo(2));
            var expected = Expected(loop, 0.6 * 2);
            var strikes = new List<double>();
            for (var i = 0; i < GameLoop.TicksPerSecond; i++)
            {
                loop.Events.Clear();
                loop.Tick();
                strikes.AddRange(Damage(loop));
            }
            Assert.That(strikes.Single() / expected, Is.EqualTo(1).Within(0.05));

            Step(loop, input, Rules.MiragePx * 10);
            Assert.That(loop.State.Sandwalker.Mirages, Is.EqualTo(Rules.MirageMax));
            Wait(loop, Rules.MirageSeconds + 0.5);
            Assert.That(loop.State.Sandwalker.Mirages, Is.Zero);
            Assert.That(loop.State.Sandwalker.MiragesFaded, Is.EqualTo(Rules.MirageMax));
        }

        [Test]
        public void MiragesGiveTheBossTimerSecondsAndSlowItForTheParty()
        {
            var (loop, input) = Hero(Flat(SandwalkerStats.MiragePower, 0.6), Flat(SandwalkerStats.MirageOnBoss, 1));
            Step(loop, input, Rules.MiragePx);
            Spawn(loop, EnemyType.Boss);
            var before = loop.State.BossTicksLeft;
            loop.Tick();

            Assert.That(loop.State.Sandwalker.Mirages, Is.EqualTo(2), "the haze brings a mirage to the boss");
            Assert.That(loop.State.BossTicksLeft - before, Is.EqualTo(2 * Rules.MirageTime * GameLoop.TicksPerSecond - 1).Within(1));
            Assert.That(CoopSignals.Read(CoopSignal.Mirages, loop.State), Is.EqualTo(2));

            var mid = loop.State.BossTicksLeft;
            Cast(loop, "sandwalker_path_3");
            Assert.That(loop.State.BossTicksLeft - mid, Is.EqualTo(Rules.MirageTime * GameLoop.TicksPerSecond));
        }

        [Test]
        public void TheDoubleRepeatsHitsAndABlindingMirageBlindsWhenItFades()
        {
            var (echo, input) = Hero(Flat(SandwalkerStats.MiragePower, 0.6), Flat(SandwalkerStats.MirageEcho, 0.1));
            Step(echo, input, Rules.MiragePx * 3);
            echo.State.Sandwalker.MirageStrikeTicks = -1000;
            var expected = Expected(echo, 0.1 * 3);
            var hit = Hit(echo, input);
            Assert.That(hit.Count, Is.EqualTo(2));
            Assert.That(hit.Min() / expected, Is.EqualTo(1).Within(0.05));

            var (blind, blindInput) = Hero(Flat(SandwalkerStats.MiragePower, 0.6), Flat(SandwalkerStats.MirageBlind, 1));
            Step(blind, blindInput, Rules.MiragePx);
            Wait(blind, Rules.MirageSeconds);
            Assert.That(blind.State.Sandwalker.MiragesFaded, Is.EqualTo(1));
            Assert.That(blind.State.BlindTicks, Is.GreaterThan(0));

            var (blows, blowsInput) = Hero(Flat(SandwalkerStats.MiragePower, 0.6), Flat(SandwalkerStats.MirageHits, 40));
            Hits(blows, blowsInput, 40);
            Assert.That(blows.State.Sandwalker.Mirages, Is.EqualTo(1));
        }

        [Test]
        public void TimeSkillsWorkForEveryTraitAndOnlyInABossFight()
        {
            var (loop, input) = Hero();
            var before = loop.State.BossTicksLeft;
            Cast(loop, "sandwalker_strong_3");
            Assert.That(loop.State.BossTicksLeft, Is.EqualTo(before));
            Assert.That(loop.State.TraitPartyTimerSlow, Is.Zero);

            Spawn(loop, EnemyType.Boss);
            loop.Tick();
            before = loop.State.BossTicksLeft;
            Cast(loop, "sandwalker_strong_3");
            Assert.That(loop.State.BossTicksLeft - before, Is.EqualTo(8 * GameLoop.TicksPerSecond));
            loop.Tick();
            Assert.That(loop.State.TraitPartyTimerSlow, Is.EqualTo(Rules.PartyTimeSlow));

            Cast(loop, "sandwalker_strong_1");
            loop.Tick();
            Assert.That(loop.State.TraitPartyTimerSlow, Is.EqualTo(1));
            Assert.That(loop.State.BlindTicks, Is.GreaterThan(0));
        }

        [Test]
        public void StepSkillsHitHarderRightAfterAStep()
        {
            var (loop, input) = Hero();
            var still = Cast(loop, "sandwalker_hit_3");
            Assert.That(still, Is.Empty);

            Step(loop, input, 100);
            var expected = Expected(loop, 4);
            var stepped = Cast(loop, "sandwalker_hit_3");
            Assert.That(stepped.Single() / expected, Is.EqualTo(1).Within(0.05));

            Step(loop, input, 1000, 4);
            expected = Expected(loop, 0.5 * 1.1);
            var traveller = Cast(loop, "sandwalker_auto_2");
            Assert.That(traveller.Single() / expected, Is.EqualTo(1).Within(0.05));
            Assert.That(loop.State.Sandwalker.StepSinceCastPx, Is.Zero);
        }

        [Test]
        public void OtherCharactersIgnoreSandwalkerStats()
        {
            var (loop, input) = Hero(HeroClass.Runedancer, Flat(SandwalkerStats.FlipPower, 0.5), Flat(SandwalkerStats.CaravanPower, 6), Flat(SandwalkerStats.MiragePower, 0.6));
            Hits(loop, input, 220);
            Step(loop, input, 10_000, 4);

            Assert.That(loop.State.Sandwalker.Flips, Is.Zero);
            Assert.That(loop.State.Sandwalker.Camps, Is.Zero);
            Assert.That(loop.State.Sandwalker.Mirages, Is.Zero);
            Assert.That(loop.Combat.Sandwalker.DamageTaken(), Is.EqualTo(1));
            Assert.That(loop.State.TraitPartyTimerSlow, Is.Zero);
        }

        [Test]
        public void EveryForkOfSandwalkerIsARuleWithAText()
        {
            foreach (var path in TestWorld.Real.Ascendancy.SubclassesOf("sandwalker"))
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
        public void TheSandwalkerTreeHasTwentySixSmallFourteenMediumSixLegendaryAndFourCoreNodes()
        {
            var tree = TestWorld.Real.ClassTrees["sandwalker"];
            var count = tree.Nodes.GroupBy(node => node.Kind).ToDictionary(group => group.Key, group => group.Count());

            Assert.That(count[TreeNodeKind.Small], Is.EqualTo(26));
            Assert.That(count[TreeNodeKind.Medium], Is.EqualTo(14));
            Assert.That(count[TreeNodeKind.Legendary], Is.EqualTo(6));
            Assert.That(count[TreeNodeKind.Core], Is.EqualTo(4));
            Assert.That(tree.Nodes.Count(node => node.Template.First.Modifiers.Concat(node.Template.Second.Modifiers).Concat(node.Template.Third.Modifiers)
                .Any(modifier => CoopLine.IsCoop(modifier.Stat))), Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void TheSandwalkerMechanicsDoNotAllocate()
        {
            var probeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (System.GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var (flip, flipInput) = Hero(Flat(SandwalkerStats.FlipPower, 0.5), Flat(SandwalkerStats.FlipStep, 1), Flat(SandwalkerStats.FlipBlind, 1));
            var (caravan, caravanInput) = Hero(Flat(SandwalkerStats.CaravanPower, 6), Flat(SandwalkerStats.CaravanOasis, 1), Flat(SandwalkerStats.CaravanHitPx, 40));
            var (mirage, mirageInput) = Hero(Flat(SandwalkerStats.MiragePower, 0.6), Flat(SandwalkerStats.MirageEcho, 0.1), Flat(SandwalkerStats.MirageBlind, 1));
            void Run(GameLoop loop, FakeInputSource input, int ticks)
            {
                for (var tick = 0; tick < ticks; tick++)
                {
                    input.PressKeys(1);
                    input.MoveMouse(400);
                    loop.Tick();
                    loop.Events.Clear();
                    loop.Drops.Flush();
                }
            }
            Run(flip, flipInput, 600);
            Run(caravan, caravanInput, 600);
            Run(mirage, mirageInput, 600);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            Run(flip, flipInput, 300);
            Run(caravan, caravanInput, 300);
            Run(mirage, mirageInput, 300);

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
