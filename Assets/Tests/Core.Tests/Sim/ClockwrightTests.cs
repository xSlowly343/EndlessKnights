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
    public class ClockwrightTests
    {
        private static ClockwrightRules Rules => TestWorld.Data.Combat.Classes.Clockwright;

        private static Modifier Flat(StatId stat, double value) => new Modifier(stat, ModifierKind.Flat, value);

        private static (GameLoop Loop, FakeInputSource Input) Hero(params Modifier[] modifiers) => Hero(HeroClass.Clockwright, EnemyType.Normal, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Boss(params Modifier[] modifiers) => Hero(HeroClass.Clockwright, EnemyType.Boss, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, EnemyType enemy, params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, TestWorld.WithAllSkillsOpen(new GameState { Class = heroClass }), hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            Spawn(loop, enemy);
            loop.Tick();
            loop.Events.Clear();
            return (loop, input);
        }

        private static void Spawn(GameLoop loop, EnemyType type, EnemyDef? enemy = null)
        {
            loop.State.SpawnEnemy(enemy, type, BigNumber.Create(1, 50));
            loop.State.BossTicksLeft = type == EnemyType.Normal ? 0 : 100_000;
        }

        private static void Party(GameLoop loop, int members)
        {
            loop.State.SharedEnemy = true;
            for (var slot = 1; slot < members; slot++)
                loop.PartyBuffs.Ally(slot).Present = true;
            loop.State.PartyMembers = members;
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

        private static List<double> Cast(GameLoop loop, string skill)
        {
            loop.Events.Clear();
            loop.Combat.Clockwright.OnSkillCast(TestWorld.Data.Gems[skill]);
            return Damage(loop);
        }

        private static int Equip(GameLoop loop, string skill)
        {
            TestWorld.ArmFor(loop, TestWorld.Data.Gems[skill]);
            loop.Skills.Equip(TestWorld.Data.Gems[skill], role: SkillTestHero.ManualRole(loop.State.Skills.Count));
            return loop.State.Skills.Count - 1;
        }

        private static double Slow(GameLoop loop, double resource)
        {
            loop.State.Resource = resource;
            loop.State.ResourceFill = resource / 100;
            loop.Combat.Clockwright.Tick();
            return loop.State.TraitTimerSlow;
        }

        [Test]
        public void TheSpringSlowsTheBossTimerAboveHalfAndStopsTheClockAtZero()
        {
            var (loop, _) = Boss(Flat(ClockwrightStats.SpringPower, 0.3));
            Assert.That(Slow(loop, 80), Is.EqualTo(0.3).Within(1e-9));
            Assert.That(Slow(loop, 30), Is.EqualTo(Rules.SpringLow).Within(1e-9));
            Assert.That(Slow(loop, 0), Is.EqualTo(-Rules.SpringPenalty).Within(1e-9));
        }

        [Test]
        public void AnEmptySpringSendsThePenaltyToTheSharedBoss()
        {
            var (loop, _) = Boss(Flat(ClockwrightStats.SpringPower, 0.3));
            loop.State.SharedEnemy = true;

            Assert.That(Slow(loop, 0), Is.Zero);
            Assert.That(loop.State.TraitPartyTimerSlow, Is.EqualTo(-Rules.SpringPenalty).Within(1e-9));
            Slow(loop, 80);
            Assert.That(loop.State.TraitPartyTimerSlow, Is.Zero);
        }

        [Test]
        public void TheSlowSpringMakesTheBossTimerRunSlower()
        {
            var (loop, _) = Boss(Flat(ClockwrightStats.SpringPower, 0.3), Flat(ClockwrightStats.SpringDrain, -1));
            loop.State.Resource = 80;
            loop.Tick();
            loop.Tick();
            Assert.That(loop.State.BossTimerRate, Is.EqualTo(0.7).Within(1e-9));
        }

        [Test]
        public void TheWindRunsDownWhileHittingButNotInAPause()
        {
            var (loop, input) = Hero(Flat(ClockwrightStats.SpringPower, 0.3));
            loop.State.Resource = 50;
            Wait(loop, 30);
            var paused = loop.State.Resource;
            Assert.That(paused, Is.GreaterThan(45));
            Wait(loop, 30);
            Assert.That(loop.State.Resource, Is.EqualTo(paused).Within(1e-9));
        }

        [Test]
        public void SpringForksAreRules()
        {
            var (tight, _) = Boss(Flat(ClockwrightStats.SpringPower, 0.3), Flat(ClockwrightStats.SpringTight, 1));
            Assert.That(Slow(tight, 30), Is.Zero);
            var (forever, _) = Boss(Flat(ClockwrightStats.SpringPower, 0.3), Flat(ClockwrightStats.SpringForever, 1));
            Assert.That(Slow(forever, 0), Is.Zero);
            var (full, _) = Boss(Flat(ClockwrightStats.SpringPower, 0.3), Flat(ClockwrightStats.SpringFull, 1));
            Assert.That(Slow(full, 100), Is.EqualTo(0.3 + Rules.SpringFullSlow).Within(1e-9));
            var (strike, _) = Hero(Flat(ClockwrightStats.SpringPower, 0.3), Flat(ClockwrightStats.SpringStrike, 1));
            strike.State.ResourceFill = 0.6;
            Assert.That(strike.Combat.Clockwright.StrikeMultiplier(), Is.EqualTo(1 + Rules.SpringStrike).Within(1e-9));
            var (fast, fastInput) = Hero(Flat(ClockwrightStats.SpringPower, 0.3), Flat(ClockwrightStats.SpringFast, 1));
            var (plain, plainInput) = Hero(Flat(ClockwrightStats.SpringPower, 0.3));
            Hits(fast, fastInput, 10);
            Hits(plain, plainInput, 10);
            Assert.That(fast.State.Resource, Is.GreaterThan(plain.State.Resource));
        }

        [Test]
        public void TheLoopRepeatsHerOwnDamageAtAQuarterInSolo()
        {
            var (loop, input) = Hero(Flat(ClockwrightStats.LoopPower, 0.5));
            Hits(loop, input, 10);
            var recent = loop.State.DamageWindow.Last(loop.State.TotalDamage, (int)(Rules.LoopWindowSeconds * GameLoop.TicksPerSecond)).ToDouble();
            var loopHit = Cast(loop, "clockwright_path_2");
            Assert.That(loopHit, Has.Count.EqualTo(1));
            Assert.That(loopHit[0] / recent, Is.EqualTo(0.5 * Rules.LoopSolo).Within(1e-6));
        }

        [Test]
        public void TheLoopComesEveryFortySeconds()
        {
            Assert.That(Rules.LoopEverySeconds, Is.EqualTo(40));
            var (loop, _) = Hero(Flat(ClockwrightStats.LoopPower, 0.5));
            Wait(loop, Rules.LoopEverySeconds);
            Assert.That(loop.State.Clockwright.Loops, Is.EqualTo(1));
        }

        [Test]
        public void ColossusTrapAndDoubleLoopsAreRules()
        {
            var (normal, _) = Hero(Flat(ClockwrightStats.LoopPower, 0.5), Flat(ClockwrightStats.LoopBoss, 1));
            Wait(normal, Rules.LoopEverySeconds);
            Assert.That(normal.State.Clockwright.Loops, Is.Zero);
            var (boss, _) = Boss(Flat(ClockwrightStats.LoopPower, 0.5), Flat(ClockwrightStats.LoopBoss, 1));
            Wait(boss, Rules.LoopEverySeconds / 2);
            Assert.That(boss.State.Clockwright.Loops, Is.EqualTo(1));

            var (trap, _) = Boss(Flat(ClockwrightStats.LoopPower, 0.5), Flat(ClockwrightStats.LoopStop, 1));
            Cast(trap, "clockwright_path_2");
            Assert.That(trap.State.BossTimerStopTicks, Is.EqualTo((int)(Rules.LoopStopSeconds * GameLoop.TicksPerSecond)));

            var (twice, _) = Hero(Flat(ClockwrightStats.LoopPower, 0.5), Flat(ClockwrightStats.LoopTwice, 1));
            Assert.That(twice.Combat.Clockwright.LoopShare, Is.EqualTo(0.5 * (1 + Rules.LoopTwice)).Within(1e-9));
        }

        [Test]
        public void EverySixtyHitsTheClockTicksAndCutsCooldowns()
        {
            Assert.That(Rules.GearHits, Is.EqualTo(60));
            var (loop, input) = Hero(Flat(ClockwrightStats.GearPower, 2));
            Hits(loop, input, 60);
            Assert.That(loop.State.Clockwright.GearTicks, Is.EqualTo(1));

            var slot = Equip(loop, "clockwright_strong_3");
            loop.State.Skills[slot].CooldownTicks = 100;
            Cast(loop, "clockwright_path_3");
            Assert.That(loop.State.Skills[slot].CooldownTicks, Is.EqualTo(80));
        }

        [Test]
        public void HerOwnCooldownsAreTwiceAsLong()
        {
            var (gears, _) = Hero(Flat(ClockwrightStats.GearPower, 2));
            var (plain, _) = Hero();
            int Cooldown(GameLoop loop)
            {
                var slot = Equip(loop, "clockwright_strong_1");
                loop.State.Resource = 100;
                Assert.That(loop.Skills.TryCast(slot), Is.EqualTo(CastResult.Cast));
                return loop.State.Skills[slot].CooldownTicks;
            }
            Assert.That(Cooldown(gears) / (double)Cooldown(plain), Is.EqualTo(Rules.GearOwn).Within(0.05));
            var (soft, _) = Hero(Flat(ClockwrightStats.GearPower, 2), Flat(ClockwrightStats.GearOwn, 1));
            Assert.That(soft.Combat.Clockwright.OwnCooldownMultiplier, Is.EqualTo(Rules.GearOwnSoft));
        }

        [Test]
        public void TwoOwnCastsInOneSecondMeshTheGearsInSolo()
        {
            var (loop, _) = Hero(Flat(ClockwrightStats.GearPower, 2));
            var first = Equip(loop, "clockwright_hit_1");
            var second = Equip(loop, "clockwright_hit_2");
            loop.State.Resource = 100;
            Assert.That(loop.Skills.TryCast(first), Is.EqualTo(CastResult.Cast));
            Assert.That(loop.Skills.TryCast(second), Is.EqualTo(CastResult.Cast));
            Assert.That(loop.State.Clockwright.Meshes, Is.EqualTo(1));
            Assert.That(loop.State.Clockwright.GearTicks, Is.EqualTo(1));
        }

        [Test]
        public void ResetIdleOilAndFirmMeshAreRules()
        {
            var (reset, _) = Hero(Flat(ClockwrightStats.GearPower, 2), Flat(ClockwrightStats.GearReset, 1));
            var slot = Equip(reset, "clockwright_strong_3");
            reset.State.Skills[slot].CooldownTicks = 10_000;
            for (var i = 0; i < Rules.GearResetEvery; i++)
                Cast(reset, "clockwright_path_3");
            Assert.That(reset.State.Skills[slot].CooldownTicks, Is.EqualTo(1));

            var (idle, _) = Hero(Flat(ClockwrightStats.GearPower, 2), Flat(ClockwrightStats.GearIdle, 1));
            Wait(idle, 30);
            Assert.That(idle.State.Clockwright.GearTicks, Is.GreaterThanOrEqualTo(1));

            var (oil, _) = Hero(Flat(ClockwrightStats.GearPower, 2), Flat(ClockwrightStats.GearResource, 1));
            var pending = oil.State.PendingResource;
            Cast(oil, "clockwright_path_3");
            Assert.That(oil.State.PendingResource - pending, Is.EqualTo(Rules.GearResource).Within(1e-9));

            var (firm, _) = Hero(Flat(ClockwrightStats.GearPower, 2), Flat(ClockwrightStats.GearMesh, 1));
            var cut = Equip(firm, "clockwright_strong_3");
            var first = Equip(firm, "clockwright_hit_1");
            var second = Equip(firm, "clockwright_hit_2");
            firm.State.Resource = 100;
            firm.Skills.TryCast(first);
            firm.State.Skills[cut].CooldownTicks = 1000;
            firm.Skills.TryCast(second);
            Assert.That(firm.State.Skills[cut].CooldownTicks, Is.EqualTo(960));
        }

        [Test]
        public void SharedSkillsWorkForEveryTrait()
        {
            var (loop, _) = Boss();
            var pending = loop.State.PendingResource;
            Cast(loop, "clockwright_hit_2");
            Assert.That(loop.State.PendingResource - pending, Is.EqualTo(8).Within(1e-9));

            var slot = Equip(loop, "clockwright_strong_1");
            loop.State.Skills[slot].CooldownTicks = 100;
            Cast(loop, "clockwright_queue_3");
            Assert.That(loop.State.Skills[slot].CooldownTicks, Is.EqualTo(90));

            Cast(loop, "clockwright_strong_1");
            Assert.That(loop.State.BossTimerStopTicks, Is.EqualTo(5 * GameLoop.TicksPerSecond));

            loop.State.Resource = 40;
            Assert.That(Cast(loop, "clockwright_strong_3"), Has.Count.EqualTo(1));
            Assert.That(loop.State.Resource, Is.EqualTo(20).Within(1e-9));

            Cast(loop, "clockwright_auto_3");
            Assert.That(loop.State.Clockwright.HoldTicks, Is.EqualTo(10 * GameLoop.TicksPerSecond));
        }

        [Test]
        public void OtherCharactersIgnoreClockwrightStats()
        {
            var (loop, input) = Hero(HeroClass.Runedancer, EnemyType.Boss, Flat(ClockwrightStats.SpringPower, 0.3), Flat(ClockwrightStats.LoopPower, 0.5), Flat(ClockwrightStats.GearPower, 2));
            Hits(loop, input, 70);
            Wait(loop, 45);

            Assert.That(loop.State.Clockwright.Loops, Is.Zero);
            Assert.That(loop.State.Clockwright.GearTicks, Is.Zero);
            Assert.That(loop.State.TraitTimerSlow, Is.Zero);
            Assert.That(loop.Combat.Clockwright.OwnCooldownMultiplier, Is.EqualTo(1));
        }

        [Test]
        public void EveryForkOfClockwrightIsARuleWithAText()
        {
            foreach (var path in TestWorld.Real.Ascendancy.SubclassesOf("clockwright"))
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
        public void TheClockwrightTreeHasTwentySixSmallFourteenMediumSixLegendaryAndFourCoreNodes()
        {
            var tree = TestWorld.Real.ClassTrees["clockwright"];
            var count = tree.Nodes.GroupBy(node => node.Kind).ToDictionary(group => group.Key, group => group.Count());

            Assert.That(count[TreeNodeKind.Small], Is.EqualTo(26));
            Assert.That(count[TreeNodeKind.Medium], Is.EqualTo(14));
            Assert.That(count[TreeNodeKind.Legendary], Is.EqualTo(6));
            Assert.That(count[TreeNodeKind.Core], Is.EqualTo(4));
            Assert.That(tree.Nodes.Count(node => node.Template.First.Modifiers.Concat(node.Template.Second.Modifiers).Concat(node.Template.Third.Modifiers)
                .Any(modifier => Game.Core.Party.CoopLine.IsCoop(modifier.Stat) || modifier.Stat.Name.EndsWith("Party"))), Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void TheClockwrightMechanicsDoNotAllocate()
        {
            var probeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (System.GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var (spring, springInput) = Boss(Flat(ClockwrightStats.SpringPower, 0.3), Flat(ClockwrightStats.SpringBurst, 1));
            var (loop, loopInput) = Hero(Flat(ClockwrightStats.LoopPower, 0.5), Flat(ClockwrightStats.LoopSeconds, -35));
            var (gears, gearsInput) = Hero(Flat(ClockwrightStats.GearPower, 2), Flat(ClockwrightStats.GearHits, -50));
            void Run(GameLoop game, FakeInputSource input, int ticks)
            {
                for (var tick = 0; tick < ticks; tick++)
                {
                    input.PressKeys(1);
                    game.Tick();
                    game.Events.Clear();
                }
            }
            Run(spring, springInput, 600);
            Run(loop, loopInput, 600);
            Run(gears, gearsInput, 600);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            Run(spring, springInput, 300);
            Run(loop, loopInput, 300);
            Run(gears, gearsInput, 300);

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
