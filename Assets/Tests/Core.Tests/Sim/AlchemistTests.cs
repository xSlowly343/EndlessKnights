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
    public class AlchemistTests
    {
        private static AlchemistRules Rules => TestWorld.Data.Combat.Classes.Alchemist;

        private static Modifier Flat(StatId stat, double value) => new Modifier(stat, ModifierKind.Flat, value);

        private static (GameLoop Loop, FakeInputSource Input) Hero(params Modifier[] modifiers) => Hero(HeroClass.Alchemist, EnemyType.Normal, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Boss(params Modifier[] modifiers) => Hero(HeroClass.Alchemist, EnemyType.Boss, modifiers);

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
            loop.Combat.Alchemist.OnSkillCast(TestWorld.Data.Gems[skill]);
            return Damage(loop);
        }

        private static int Equip(GameLoop loop, string skill)
        {
            loop.Skills.Equip(TestWorld.Data.Gems[skill], role: SkillTestHero.ManualRole(loop.State.Skills.Count));
            return loop.State.Skills.Count - 1;
        }

        [Test]
        public void EveryEighthHitDropsAReagentAndThreeMakeAMix()
        {
            Assert.That(Rules.ReagentHits, Is.EqualTo(8));
            var (loop, input) = Hero(Flat(AlchemistStats.MixPower, 1));
            Hits(loop, input, 8);
            Assert.That(loop.State.Alchemist.InFlask, Is.EqualTo(1));
            Hits(loop, input, 16);
            Assert.That(loop.State.Alchemist.Mixes, Is.EqualTo(1));
            Assert.That(loop.State.Alchemist.InFlask, Is.Zero);
        }

        [Test]
        public void ThreeDifferentReagentsMakeThePhilosophersMix()
        {
            var (loop, input) = Hero(Flat(AlchemistStats.MixPower, 1));
            Hits(loop, input, 24);
            Assert.That(loop.State.Alchemist.LastRecipe, Is.EqualTo(AlchemistRules.RecipeOf(1, 1, 1)));
            Assert.That(loop.Combat.Alchemist.MixShare, Is.EqualTo(0.15).Within(1e-9));
            Assert.That(loop.Combat.Alchemist.DamageTaken(), Is.EqualTo(1.15).Within(1e-9));
        }

        [Test]
        public void EveryRecipeHasAnEffect()
        {
            for (var red = 0; red <= 3; red++)
                for (var blue = 0; blue + red <= 3; blue++)
                    Assert.That(Rules.Mix(AlchemistRules.RecipeOf(red, blue, 3 - red - blue)), Is.Not.Empty, $"{red}{blue}");
        }

        [Test]
        public void TheFlaskSkillThrowsAtOnceAndStronger()
        {
            var (loop, _) = Hero(Flat(AlchemistStats.MixPower, 1));
            loop.Combat.Alchemist.AddReagent(0);
            Cast(loop, "alchemist_path_1");
            Assert.That(loop.State.Alchemist.Mixes, Is.EqualTo(1));
            Assert.That(loop.State.Alchemist.InFlask, Is.Zero);
        }

        [Test]
        public void MixForksAreRules()
        {
            var (pure, _) = Hero(Flat(AlchemistStats.MixPower, 1), Flat(AlchemistStats.MixPure, 1));
            for (var i = 0; i < 3; i++)
                pure.Combat.Alchemist.AddReagent(1);
            Assert.That(pure.Combat.Alchemist.MixShare, Is.EqualTo(0.3 * Rules.PureBoost).Within(1e-9));

            var (stone, _) = Hero(Flat(AlchemistStats.MixPower, 1), Flat(AlchemistStats.MixStone, 1));
            stone.Combat.Alchemist.AddReagent(0);
            stone.Combat.Alchemist.AddReagent(1);
            stone.Combat.Alchemist.AddReagent(2);
            Assert.That(stone.Combat.Alchemist.MixShare, Is.EqualTo(0.15 * Rules.PhilosopherBoost).Within(1e-9));

            var (chain, _) = Hero(Flat(AlchemistStats.MixPower, 1), Flat(AlchemistStats.MixChain, 1));
            chain.Events.Clear();
            for (var i = 0; i < 3; i++)
                chain.Combat.Alchemist.AddReagent(1);
            Assert.That(Damage(chain), Has.Count.EqualTo(1));

            var (lucky, _) = Hero(Flat(AlchemistStats.MixPower, 1), Flat(AlchemistStats.MixLucky, 1));
            lucky.Events.Clear();
            for (var i = 0; i < 3 * Rules.LuckyEvery; i++)
                lucky.Combat.Alchemist.AddReagent(0);
            Assert.That(Damage(lucky), Has.Count.EqualTo(Rules.LuckyEvery + 1));
        }

        [Test]
        public void ThirtyHitsDissolveALayerAndEveryLayerMakesTheEnemyWeaker()
        {
            Assert.That(Rules.LayerHits, Is.EqualTo(30));
            var (loop, input) = Hero(Flat(AlchemistStats.SolventPower, 0.06));
            Hits(loop, input, 30);
            Assert.That(loop.State.Alchemist.LayersLeft, Is.EqualTo(Rules.Layers - 1));
            Assert.That(loop.Combat.Alchemist.DamageTaken(), Is.EqualTo(1.06).Within(1e-9));
            Assert.That(loop.State.Boons.AcidTicks, Is.GreaterThan(0));
            Hits(loop, input, 15);
            Assert.That(loop.State.Alchemist.LayersLeft, Is.EqualTo(Rules.Layers - 2));
        }

        [Test]
        public void TheSplashSkillTakesALayerAndANewEnemyHasAllLayers()
        {
            var (loop, _) = Hero(Flat(AlchemistStats.SolventPower, 0.06));
            Cast(loop, "alchemist_path_2");
            Cast(loop, "alchemist_path_2");
            Assert.That(loop.Combat.Alchemist.LayersOff, Is.EqualTo(2));
            Spawn(loop, EnemyType.Normal);
            loop.Tick();
            Assert.That(loop.Combat.Alchemist.LayersOff, Is.Zero);
        }

        [Test]
        public void CoreFumesAndDeepSolutionAreRules()
        {
            var (core, _) = Hero(Flat(AlchemistStats.SolventPower, 0.06), Flat(AlchemistStats.SolventCore, 1));
            for (var i = 0; i < Rules.Layers; i++)
                Cast(core, "alchemist_path_2");
            Assert.That(core.Combat.Alchemist.SolventDamageTaken, Is.EqualTo(0.06 * Rules.Layers + Rules.CoreBonus).Within(1e-9));

            var (fumes, _) = Hero(Flat(AlchemistStats.SolventPower, 0.06), Flat(AlchemistStats.SolventCarry, 1));
            Cast(fumes, "alchemist_path_2");
            Spawn(fumes, EnemyType.Normal);
            fumes.Tick();
            Assert.That(fumes.Combat.Alchemist.LayersOff, Is.EqualTo(1));

            var (deep, _) = Hero(Flat(AlchemistStats.SolventPower, 0.06), Flat(AlchemistStats.LayerBlast, 1));
            Assert.That(Cast(deep, "alchemist_path_2"), Has.Count.EqualTo(1));
        }

        [Test]
        public void EveryFiftyHitsAnElixirAndSheHitsWeaker()
        {
            Assert.That(Rules.ElixirHits, Is.EqualTo(50));
            var (loop, input) = Hero(Flat(AlchemistStats.ElixirPower, 3));
            Assert.That(loop.Combat.Alchemist.StrikeMultiplier(), Is.EqualTo(1 - Rules.SelfPenalty));
            Hits(loop, input, 50);
            Assert.That(loop.State.Boons.RedTicks, Is.GreaterThan(0));
            Hits(loop, input, Rules.RedEvery);
            Assert.That(loop.State.Boons.RedStrikes, Is.LessThan(Rules.RedEvery));
        }

        [Test]
        public void ElixirColoursGiveExplosionsFreeSkillsAndGoldAndTheThirdIsThePhilosophers()
        {
            var (loop, _) = Hero(Flat(AlchemistStats.ElixirPower, 3));
            Cast(loop, "alchemist_path_3");
            Assert.That(loop.State.Boons.RedTicks, Is.GreaterThan(0));
            Cast(loop, "alchemist_path_3");
            Assert.That(loop.State.Boons.FreeSkills, Is.True);
            var slot = Equip(loop, "alchemist_strong_1");
            loop.State.Resource = 0;
            Assert.That(loop.Skills.TryCast(slot), Is.EqualTo(CastResult.Cast));
            loop.State.Boons.RedTicks = 0;
            loop.State.Boons.BlueTicks = 0;
            Cast(loop, "alchemist_path_3");
            Assert.That(loop.State.Boons.RedTicks, Is.GreaterThan(0));
            Assert.That(loop.State.Boons.BlueTicks, Is.GreaterThan(0));
            Assert.That(loop.State.Boons.GoldMultiplier, Is.EqualTo(1 + Rules.GoldBonus));
        }

        [Test]
        public void ElixirForksAreRules()
        {
            var (steady, _) = Hero(Flat(AlchemistStats.ElixirPower, 3), Flat(AlchemistStats.ElixirNoPenalty, 1));
            Assert.That(steady.Combat.Alchemist.StrikeMultiplier(), Is.EqualTo(1));
            var (dose, _) = Hero(Flat(AlchemistStats.ElixirPower, 3), Flat(AlchemistStats.ElixirDouble, 1));
            Cast(dose, "alchemist_path_3");
            Cast(dose, "alchemist_path_3");
            Assert.That(dose.State.Boons.GoldTicks, Is.GreaterThan(0));
            var (two, _) = Hero(Flat(AlchemistStats.ElixirPower, 3), Flat(AlchemistStats.ElixirTwo, 1));
            Cast(two, "alchemist_path_3");
            Assert.That(two.State.Boons.RedTicks, Is.EqualTo((int)(2 * Rules.ElixirSeconds * GameLoop.TicksPerSecond)));
        }

        [Test]
        public void SharedSkillsWorkForEveryTrait()
        {
            var (loop, _) = Hero();
            Cast(loop, "alchemist_hit_2");
            Assert.That(loop.Combat.Alchemist.MixShare, Is.EqualTo(0.08).Within(1e-9));
            Cast(loop, "alchemist_strong_2");
            Assert.That(loop.Combat.Alchemist.DamageTaken(), Is.EqualTo(1.2).Within(1e-9));
        }

        [Test]
        public void OtherCharactersIgnoreAlchemistStats()
        {
            var (loop, input) = Hero(HeroClass.Runedancer, EnemyType.Normal, Flat(AlchemistStats.MixPower, 1), Flat(AlchemistStats.SolventPower, 0.06), Flat(AlchemistStats.ElixirPower, 3));
            Hits(loop, input, 60);
            Assert.That(loop.State.Alchemist.Mixes, Is.Zero);
            Assert.That(loop.State.Alchemist.LayersDissolved, Is.Zero);
            Assert.That(loop.State.Boons.Elixirs, Is.Zero);
            Assert.That(loop.Combat.Alchemist.StrikeMultiplier(), Is.EqualTo(1));
            Assert.That(loop.Combat.Alchemist.DamageTaken(), Is.EqualTo(1));
        }

        [Test]
        public void EveryForkOfAlchemistIsARuleWithAText()
        {
            foreach (var path in TestWorld.Real.Ascendancy.SubclassesOf("alchemist"))
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
        public void TheAlchemistTreeHasTwentySixSmallFourteenMediumSixLegendaryAndFourCoreNodes()
        {
            var tree = TestWorld.Real.ClassTrees["alchemist"];
            var count = tree.Nodes.GroupBy(node => node.Kind).ToDictionary(group => group.Key, group => group.Count());

            Assert.That(count[TreeNodeKind.Small], Is.EqualTo(26));
            Assert.That(count[TreeNodeKind.Medium], Is.EqualTo(14));
            Assert.That(count[TreeNodeKind.Legendary], Is.EqualTo(6));
            Assert.That(count[TreeNodeKind.Core], Is.EqualTo(4));
            Assert.That(tree.Nodes.Count(node => node.Template.First.Modifiers.Concat(node.Template.Second.Modifiers).Concat(node.Template.Third.Modifiers)
                .Any(modifier => Game.Core.Party.CoopLine.IsCoop(modifier.Stat) || modifier.Stat.Name.EndsWith("Party")
                    || modifier.Stat.Name == AlchemistStats.MixParty.Name || modifier.Stat.Name == AlchemistStats.SolventParty.Name
                    || modifier.Stat.Name == AlchemistStats.ElixirParty.Name)), Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void TheAlchemistMechanicsDoNotAllocate()
        {
            var probeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (System.GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var (mix, mixInput) = Hero(Flat(AlchemistStats.MixPower, 1), Flat(AlchemistStats.MixChain, 1));
            var (solvent, solventInput) = Hero(Flat(AlchemistStats.SolventPower, 0.06), Flat(AlchemistStats.LayerBlast, 1));
            var (elixir, elixirInput) = Hero(Flat(AlchemistStats.ElixirPower, 3), Flat(AlchemistStats.ElixirHits, -40));
            void Run(GameLoop game, FakeInputSource input, int ticks)
            {
                for (var tick = 0; tick < ticks; tick++)
                {
                    input.PressKeys(1);
                    game.Tick();
                    game.Events.Clear();
                }
            }
            Run(mix, mixInput, 600);
            Run(solvent, solventInput, 600);
            Run(elixir, elixirInput, 600);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            Run(mix, mixInput, 300);
            Run(solvent, solventInput, 300);
            Run(elixir, elixirInput, 300);

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
