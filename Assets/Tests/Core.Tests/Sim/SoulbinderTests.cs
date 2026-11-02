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
    public class SoulbinderTests
    {
        private static SoulbinderRules Rules => TestWorld.Data.Combat.Classes.Soulbinder;

        private static Modifier Flat(StatId stat, double value) => new Modifier(stat, ModifierKind.Flat, value);

        private static (GameLoop Loop, FakeInputSource Input) Hero(params Modifier[] modifiers) => Hero(HeroClass.Soulbinder, EnemyType.Normal, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Boss(params Modifier[] modifiers) => Hero(HeroClass.Soulbinder, EnemyType.Boss, modifiers);

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
            loop.Combat.Soulbinder.OnSkillCast(TestWorld.Data.Gems[skill]);
            return Damage(loop);
        }

        private static int Equip(GameLoop loop, string skill)
        {
            TestWorld.ArmFor(loop, TestWorld.Data.Gems[skill]);
            loop.Skills.Equip(TestWorld.Data.Gems[skill], role: SkillTestHero.ManualRole(loop.State.Skills.Count));
            return loop.State.Skills.Count - 1;
        }

        private static void Kill(GameLoop loop) => loop.Combat.Soulbinder.OnKillRewarded();

        [Test]
        public void EveryKillPutsASoulIntoTheOrbUpToSix()
        {
            Assert.That(Rules.SphereSize, Is.EqualTo(6));
            var (loop, _) = Hero(Flat(SoulbinderStats.SoulPower, 0.5));
            for (var i = 0; i < 8; i++)
                Kill(loop);
            Assert.That(loop.State.Soulbinder.Souls, Is.EqualTo(6));
        }

        [Test]
        public void SpiritsStrikeByThemselvesEverySecond()
        {
            var (loop, _) = Hero(Flat(SoulbinderStats.SoulPower, 0.5));
            Kill(loop);
            Kill(loop);
            loop.Events.Clear();
            Wait(loop, 1);
            Assert.That(Damage(loop), Has.Count.EqualTo(1));
            Assert.That(loop.Combat.Soulbinder.SpiritStrength, Is.EqualTo(2).Within(1e-9));
        }

        [Test]
        public void BossSoulsAreStronger()
        {
            var (loop, _) = Boss(Flat(SoulbinderStats.SoulPower, 0.5));
            Kill(loop);
            Assert.That(loop.Combat.Soulbinder.SpiritStrength, Is.EqualTo(Rules.BossSoul).Within(1e-9));
        }

        [Test]
        public void SphereForksAreRules()
        {
            var (push, _) = Hero(Flat(SoulbinderStats.SoulPower, 0.5), Flat(SoulbinderStats.SoulPush, 1));
            for (var i = 0; i < 6; i++)
                Kill(push);
            push.Events.Clear();
            Kill(push);
            Assert.That(Damage(push), Has.Count.EqualTo(1));
            var (big, _) = Hero(Flat(SoulbinderStats.SoulPower, 0.5), Flat(SoulbinderStats.SoulMax, 2));
            Assert.That(big.Combat.Soulbinder.SphereSize, Is.EqualTo(8));
            var (hungry, _) = Hero(Flat(SoulbinderStats.SoulPower, 0.5), Flat(SoulbinderStats.SoulHungry, 1));
            Assert.That(hungry.Combat.Soulbinder.SphereSize, Is.EqualTo(4));
            var (mini, _) = Boss(Flat(SoulbinderStats.SoulPower, 0.5), Flat(SoulbinderStats.SoulMini, 1));
            Kill(mini);
            Assert.That(mini.State.Soulbinder.Souls, Is.EqualTo(2));
        }


        [Test]
        public void AFullOrbBreathesTheOldestSoulOutAndChillsTheEnemy()
        {
            var (loop, _) = Hero(Flat(SoulbinderStats.BreathPower, 0.05));
            for (var i = 0; i < 6; i++)
                Kill(loop);
            loop.Events.Clear();
            Wait(loop, 1);
            Assert.That(Damage(loop), Is.Empty);
            Kill(loop);
            Assert.That(loop.State.Soulbinder.Breaths, Is.EqualTo(1));
            Assert.That(Damage(loop), Has.Count.EqualTo(1));
            Assert.That(loop.Combat.Soulbinder.DamageTaken(), Is.EqualTo(1 + Rules.ColdDamageTaken).Within(1e-9));
            Assert.That(loop.State.Soulbinder.Souls, Is.EqualTo(6));
        }

        [Test]
        public void BreathForksAreRules()
        {
            var (small, _) = Hero(Flat(SoulbinderStats.BreathPower, 0.05), Flat(SoulbinderStats.BreathSmall, 1));
            Assert.That(small.Combat.Soulbinder.SphereSize, Is.EqualTo(3));
            var (twice, _) = Hero(Flat(SoulbinderStats.BreathPower, 0.05), Flat(SoulbinderStats.BreathTwice, 1));
            Kill(twice);
            twice.Events.Clear();
            Cast(twice, "soulbinder_path_2");
            Assert.That(Damage(twice), Has.Count.EqualTo(2));
            var (heavy, _) = Hero(Flat(SoulbinderStats.BreathPower, 0.05), Flat(SoulbinderStats.BreathNoCold, 1));
            Kill(heavy);
            Cast(heavy, "soulbinder_path_2");
            Assert.That(heavy.Combat.Soulbinder.ColdShare, Is.Zero);
        }

        [Test]
        public void EveryFiftyHitsASpiritEntersAndStrikesAlong()
        {
            Assert.That(Rules.LendHits, Is.EqualTo(50));
            var (loop, input) = Hero(Flat(SoulbinderStats.LendPower, 0.25));
            Hits(loop, input, 50);
            Assert.That(loop.State.Boons.LendTicks, Is.GreaterThan(0));
            Assert.That(Hit(loop, input), Has.Count.EqualTo(2));
            var pending = loop.State.PendingResource;
            loop.Combat.Soulbinder.OnKillRewarded();
            Assert.That(loop.State.PendingResource - pending, Is.EqualTo(Rules.SoulBackResource).Within(1e-9));
        }

        [Test]
        public void LendForksAreRules()
        {
            var (two, _) = Hero(Flat(SoulbinderStats.LendPower, 0.25), Flat(SoulbinderStats.LendTwo, 1));
            Cast(two, "soulbinder_path_3");
            Assert.That(two.State.Boons.LendTicks, Is.EqualTo((int)(2 * Rules.LendSeconds * GameLoop.TicksPerSecond)));
            var (back, _) = Hero(Flat(SoulbinderStats.LendPower, 0.25), Flat(SoulbinderStats.LendBack, 1));
            back.State.Boons.LendTicks = 10;
            back.Combat.Soulbinder.OnKillRewarded();
            Assert.That(back.State.Soulbinder.LendHits, Is.EqualTo(Rules.SoulBackHits));
        }

        [Test]
        public void SharedSkillsWorkForEveryTrait()
        {
            var (loop, _) = Hero(Flat(SoulbinderStats.SoulPower, 0.5));
            Cast(loop, "soulbinder_strong_2");
            Assert.That(loop.State.Soulbinder.Souls, Is.EqualTo(3));
            Assert.That(Cast(loop, "soulbinder_strong_1"), Has.Count.EqualTo(1));
            Assert.That(Cast(loop, "soulbinder_strong_3"), Has.Count.EqualTo(1));
        }

        [Test]
        public void OtherCharactersIgnoreSoulbinderStats()
        {
            var (loop, input) = Hero(HeroClass.Runedancer, EnemyType.Normal, Flat(SoulbinderStats.SoulPower, 0.5), Flat(SoulbinderStats.BreathPower, 0.05), Flat(SoulbinderStats.LendPower, 0.25));
            Hits(loop, input, 60);
            Kill(loop);
            Assert.That(loop.State.Soulbinder.Souls, Is.Zero);
            Assert.That(loop.State.Soulbinder.Lends, Is.Zero);
            Assert.That(loop.Combat.Soulbinder.DamageTaken(), Is.EqualTo(1));
        }

        [Test]
        public void EveryForkOfSoulbinderIsARuleWithAText()
        {
            foreach (var path in TestWorld.Real.Ascendancy.SubclassesOf("soulbinder"))
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
        public void TheSoulbinderTreeHasTwentySixSmallFourteenMediumSixLegendaryAndFourCoreNodes()
        {
            var tree = TestWorld.Real.ClassTrees["soulbinder"];
            var count = tree.Nodes.GroupBy(node => node.Kind).ToDictionary(group => group.Key, group => group.Count());

            Assert.That(count[TreeNodeKind.Small], Is.EqualTo(26));
            Assert.That(count[TreeNodeKind.Medium], Is.EqualTo(14));
            Assert.That(count[TreeNodeKind.Legendary], Is.EqualTo(6));
            Assert.That(count[TreeNodeKind.Core], Is.EqualTo(4));
            Assert.That(tree.Nodes.Count(node => node.Template.First.Modifiers.Concat(node.Template.Second.Modifiers).Concat(node.Template.Third.Modifiers)
                .Any(modifier => Game.Core.Party.CoopLine.IsCoop(modifier.Stat) || modifier.Stat.Name.EndsWith("Party")
                    || modifier.Stat.Name == SoulbinderStats.SoulParty.Name || modifier.Stat.Name == SoulbinderStats.BreathParty.Name
                    || modifier.Stat.Name == SoulbinderStats.LendParty.Name)), Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void TheSoulbinderMechanicsDoNotAllocate()
        {
            var probeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (System.GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var (sphere, sphereInput) = Hero(Flat(SoulbinderStats.SoulPower, 0.5));
            var (breath, breathInput) = Hero(Flat(SoulbinderStats.BreathPower, 0.05));
            var (lend, lendInput) = Hero(Flat(SoulbinderStats.LendPower, 0.25));
            for (var i = 0; i < 6; i++)
            {
                Kill(sphere);
                Kill(breath);
            }
            void Run(GameLoop game, FakeInputSource input, int ticks)
            {
                for (var tick = 0; tick < ticks; tick++)
                {
                    input.PressKeys(1);
                    game.Tick();
                    game.Events.Clear();
                }
            }
            Run(sphere, sphereInput, 600);
            Run(breath, breathInput, 600);
            Run(lend, lendInput, 600);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            Run(sphere, sphereInput, 300);
            Run(breath, breathInput, 300);
            Run(lend, lendInput, 300);

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
