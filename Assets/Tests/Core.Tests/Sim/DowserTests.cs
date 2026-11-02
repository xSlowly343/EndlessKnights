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
    public class DowserTests
    {
        private static DowserRules Rules => TestWorld.Data.Combat.Classes.Dowser;

        private static Modifier Flat(StatId stat, double value) => new Modifier(stat, ModifierKind.Flat, value);

        private static (GameLoop Loop, FakeInputSource Input) Hero(params Modifier[] modifiers) => Hero(HeroClass.Dowser, EnemyType.Normal, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Boss(params Modifier[] modifiers) => Hero(HeroClass.Dowser, EnemyType.Boss, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, EnemyType enemy, params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, TestWorld.WithAllWeaponPlacesOpen(new GameState { Class = heroClass }), hero);
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
            loop.Combat.Dowser.OnSkillCast(TestWorld.Data.Gems[skill]);
            return Damage(loop);
        }

        private static bool HasRelay(GameLoop loop, TraitRelayKind kind, out TraitRelay found)
        {
            for (var i = 0; i < loop.State.TraitRelays.Count; i++)
            {
                if (loop.State.TraitRelays[i].Kind != kind)
                    continue;
                found = loop.State.TraitRelays[i];
                return true;
            }
            found = default;
            return false;
        }

        private static int Equip(GameLoop loop, string skill)
        {
            TestWorld.ArmFor(loop, TestWorld.Data.Gems[skill]);
            loop.Skills.Equip(TestWorld.Data.Gems[skill], role: SkillTestHero.ManualRole(loop.State.Skills.Count));
            return loop.State.Skills.Count - 1;
        }

        private static void Kill(GameLoop loop)
        {
            loop.State.EnemyHp = BigNumber.Zero;
            loop.Combat.Dowser.OnKillRewarded();
        }

        [Test]
        public void TheVeinHidesInTheFirstFiftyHitsAndMultipliesTheGold()
        {
            Assert.That(Rules.VeinRange, Is.EqualTo(50));
            var (loop, input) = Hero(Flat(DowserStats.VeinPower, 19));
            Assert.That(loop.State.Dowser.VeinAt, Is.InRange(1, 50));
            Hits(loop, input, loop.State.Dowser.VeinAt - 1);
            Assert.That(loop.State.EnemyGoldBonus, Is.Zero);
            Assert.That(loop.Combat.Dowser.VeinCloseness, Is.GreaterThanOrEqualTo(0));
            Hit(loop, input);
            Assert.That(loop.State.Dowser.Veins, Is.EqualTo(1));
            Assert.That(loop.State.EnemyGoldBonus, Is.EqualTo(19).Within(1e-9));
        }

        [Test]
        public void ANewEnemyHidesANewVein()
        {
            var (loop, _) = Hero(Flat(DowserStats.VeinPower, 19));
            Cast(loop, "dowser_path_1");
            Assert.That(loop.State.Dowser.VeinsStruck, Is.EqualTo(1));
            Spawn(loop, EnemyType.Normal);
            loop.Tick();
            Assert.That(loop.State.Dowser.VeinsStruck, Is.Zero);
            Assert.That(loop.State.Dowser.Hits, Is.Zero);
        }

        [Test]
        public void AlliesHitsBringTheSharedVeinCloser()
        {
            var (loop, _) = Hero(Flat(DowserStats.VeinPower, 19));
            loop.Combat.Dowser.AddAllyHits(Rules.VeinRange);
            Assert.That(loop.State.Dowser.Veins, Is.EqualTo(1));
        }

        [Test]
        public void BossesHaveNoVeinUnlessTheColossusVeinIsChosen()
        {
            var (boss, _) = Boss(Flat(DowserStats.VeinPower, 19));
            Cast(boss, "dowser_path_1");
            Assert.That(boss.State.Dowser.Veins, Is.Zero);
            var (colossus, _) = Boss(Flat(DowserStats.VeinPower, 19), Flat(DowserStats.VeinBoss, 1));
            Cast(colossus, "dowser_path_1");
            Assert.That(colossus.State.EnemyGoldBonus, Is.EqualTo(38).Within(1e-9));
        }

        [Test]
        public void InAPartyTheVeinGoldGoesToEveryoneAndItsClosenessIsShown()
        {
            var (loop, _) = Hero(Flat(DowserStats.VeinPower, 19));
            Party(loop, 2);
            loop.Combat.Dowser.AddAllyHits(1);
            Assert.That(HasRelay(loop, TraitRelayKind.VeinHint, out var hint), Is.True);
            Assert.That(hint.To, Is.EqualTo(TraitRelay.AllSlots));
            Cast(loop, "dowser_path_1");
            Assert.That(HasRelay(loop, TraitRelayKind.Vein, out var vein), Is.True);
            Assert.That(vein.Value, Is.EqualTo(19).Within(1e-9));

            var (ally, _) = Hero(HeroClass.Runedancer, EnemyType.Normal);
            ally.Combat.Dowser.ReceiveVein(19);
            Assert.That(ally.State.EnemyGoldBonus, Is.EqualTo(19).Within(1e-9));
        }

        [Test]
        public void VeinForksAreRules()
        {
            var (strike, _) = Hero(Flat(DowserStats.VeinPower, 19), Flat(DowserStats.VeinStrike, 1));
            Assert.That(Cast(strike, "dowser_path_1"), Has.Count.EqualTo(1));
            var (crit, _) = Hero(Flat(DowserStats.VeinPower, 19), Flat(DowserStats.VeinCrit, 1));
            Cast(crit, "dowser_path_1");
            Assert.That(crit.Combat.Dowser.ForcesCrit, Is.True);
            var (two, _) = Hero(Flat(DowserStats.VeinPower, 19), Flat(DowserStats.VeinTwice, 1));
            Cast(two, "dowser_path_1");
            Assert.That(two.State.Dowser.Veins, Is.EqualTo(2));
            Assert.That(two.State.EnemyGoldBonus, Is.EqualTo(19).Within(1e-9));
            var (near, _) = Hero(Flat(DowserStats.VeinPower, 19), Flat(DowserStats.VeinNear, 1));
            Assert.That(near.State.Dowser.VeinAt, Is.InRange(1, Rules.VeinNearRange));
        }

        [Test]
        public void EveryEnemyWithoutAFindRaisesTheLuckAndAFindResetsIt()
        {
            var (loop, _) = Hero(Flat(DowserStats.LuckPower, 0.003));
            for (var i = 0; i < 10; i++)
            {
                loop.State.Dowser.Luck = System.Math.Min(loop.State.Dowser.Luck, 0.0001);
                Kill(loop);
                Spawn(loop, EnemyType.Normal);
            }
            Assert.That(loop.State.Dowser.Luck, Is.GreaterThan(0));
            loop.State.Dowser.Luck = 1;
            Kill(loop);
            Assert.That(loop.State.Dowser.Finds, Is.EqualTo(1));
            Assert.That(loop.State.Dowser.Luck, Is.Zero);
        }

        [Test]
        public void InAPartyEveryKillGrowsTheLuckAndTheTopKillerRolls()
        {
            var (loop, _) = Hero(Flat(DowserStats.LuckPower, 0.003));
            Party(loop, 2);
            Kill(loop);
            Kill(loop);
            Assert.That(loop.State.Dowser.Luck, Is.EqualTo(0.006).Within(1e-9));
            loop.Tick();
            Assert.That(HasRelay(loop, TraitRelayKind.Luck, out var luck), Is.True);
            Assert.That(luck.Value, Is.EqualTo(0.006).Within(1e-9));
            loop.State.Dowser.Luck = 1;
            loop.Combat.Dowser.OnTopKill();
            Assert.That(loop.State.Dowser.Finds, Is.EqualTo(1));
        }

        [Test]
        public void AnAllyWithTheLuckRuleRollsItsTopKillAndReportsTheFind()
        {
            var (loop, _) = Hero(HeroClass.Runedancer, EnemyType.Normal);
            loop.State.Boons.LuckOwner = 2;
            loop.State.Boons.LuckChance = 1;
            loop.State.Boons.LuckTicks = 100;
            loop.Combat.Dowser.OnTopKill();
            Assert.That(HasRelay(loop, TraitRelayKind.LuckFound, out var found), Is.True);
            Assert.That(found.To, Is.EqualTo(2));
            Assert.That(loop.State.Boons.LuckChance, Is.Zero);
        }

        [Test]
        public void LuckForksAreRules()
        {
            var (keep, _) = Hero(Flat(DowserStats.LuckPower, 0.003), Flat(DowserStats.LuckKeep, 1));
            keep.State.Dowser.Luck = 1;
            Kill(keep);
            Assert.That(keep.State.Dowser.Luck, Is.EqualTo(0.5).Within(1e-9));
            var (boss, _) = Boss(Flat(DowserStats.LuckPower, 0.003), Flat(DowserStats.LuckBoss, 1));
            boss.State.Dowser.Luck = 0.1;
            Assert.That(boss.Combat.Dowser.LuckChance, Is.EqualTo(0.1 * Rules.LuckBoss).Within(1e-9));
            var (gold, _) = Hero(Flat(DowserStats.LuckPower, 0.003), Flat(DowserStats.LuckGold, 1));
            gold.State.Dowser.Luck = 0;
            Party(gold, 2);
            Kill(gold);
            gold.Combat.Dowser.Tick();
            Assert.That(gold.State.TraitPartyGold, Is.EqualTo(Rules.LuckGoldPerKill).Within(1e-9));
        }

        [Test]
        public void TheCrewHitsWeakerAndEveryTenKillsOpenAChest()
        {
            var (loop, _) = Hero(Flat(DowserStats.ArtelPower, 0.5));
            Assert.That(loop.Combat.Dowser.StrikeMultiplier(), Is.EqualTo(1 - Rules.ArtelPenalty).Within(1e-9));
            var gold = loop.State.Gold;
            for (var i = 0; i < Rules.ArtelNuggets; i++)
                Kill(loop);
            Assert.That(loop.State.Dowser.Chests, Is.EqualTo(1));
            Assert.That(loop.State.Dowser.Nuggets, Is.Zero);
            Assert.That(loop.State.Gold, Is.GreaterThan(gold));
        }

        [Test]
        public void InAPartyTheCrewRuleGoesToAlliesAndTheirKillsOpenTheirChests()
        {
            var (loop, _) = Hero(Flat(DowserStats.ArtelPower, 0.5));
            Party(loop, 2);
            loop.Tick();
            Assert.That(HasRelay(loop, TraitRelayKind.Artel, out var call), Is.True);
            Assert.That(call.Every, Is.EqualTo(Rules.ArtelNuggets));
            Kill(loop);
            Assert.That(loop.State.Dowser.Nuggets, Is.Zero);

            var (ally, _) = Hero(HeroClass.Runedancer, EnemyType.Normal);
            ally.State.Boons.ArtelOwner = 0;
            ally.State.Boons.ArtelEvery = 3;
            ally.State.Boons.ArtelChest = 0.5;
            ally.State.Boons.ArtelTicks = 100;
            for (var i = 0; i < 3; i++)
                Kill(ally);
            Assert.That(ally.State.Boons.ArtelChests, Is.EqualTo(1));
        }

        [Test]
        public void CrewForksAreRules()
        {
            var (light, _) = Hero(Flat(DowserStats.ArtelPower, 0.5), Flat(DowserStats.ArtelPenalty, -0.1));
            Assert.That(light.Combat.Dowser.StrikeMultiplier(), Is.EqualTo(0.7).Within(1e-9));
            var (boss, _) = Boss(Flat(DowserStats.ArtelPower, 0.5), Flat(DowserStats.ArtelBoss, 1));
            for (var i = 0; i < Rules.ArtelNuggets / 2; i++)
                Kill(boss);
            Assert.That(boss.State.Dowser.Chests, Is.EqualTo(1));
            var (item, _) = Hero(Flat(DowserStats.ArtelPower, 0.5), Flat(DowserStats.ArtelItem, 1));
            Assert.That(item.Combat.Dowser.ArtelItemChance, Is.EqualTo(Rules.ArtelItemRich));
        }

        [Test]
        public void SharedSkillsWorkForEveryTrait()
        {
            var (loop, _) = Hero();
            Cast(loop, "dowser_strong_1");
            Assert.That(loop.State.EnemyGoldBonus, Is.EqualTo(5).Within(1e-9));
            Assert.That(Cast(loop, "dowser_hit_3"), Has.Count.EqualTo(1));
        }

        [Test]
        public void OtherCharactersIgnoreDowserStats()
        {
            var (loop, input) = Hero(HeroClass.Runedancer, EnemyType.Normal, Flat(DowserStats.VeinPower, 19), Flat(DowserStats.LuckPower, 0.003), Flat(DowserStats.ArtelPower, 0.5));
            Hits(loop, input, 60);
            Kill(loop);
            Assert.That(loop.State.Dowser.Veins, Is.Zero);
            Assert.That(loop.State.Dowser.Luck, Is.Zero);
            Assert.That(loop.State.Dowser.Nuggets, Is.Zero);
            Assert.That(loop.Combat.Dowser.StrikeMultiplier(), Is.EqualTo(1));
        }

        [Test]
        public void EveryForkOfDowserIsARuleWithAText()
        {
            foreach (var path in TestWorld.Real.Ascendancy.SubclassesOf("dowser"))
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
        public void TheDowserTreeHasTwentySixSmallFourteenMediumSixLegendaryAndFourCoreNodes()
        {
            var tree = TestWorld.Real.ClassTrees["dowser"];
            var count = tree.Nodes.GroupBy(node => node.Kind).ToDictionary(group => group.Key, group => group.Count());

            Assert.That(count[TreeNodeKind.Small], Is.EqualTo(26));
            Assert.That(count[TreeNodeKind.Medium], Is.EqualTo(14));
            Assert.That(count[TreeNodeKind.Legendary], Is.EqualTo(6));
            Assert.That(count[TreeNodeKind.Core], Is.EqualTo(4));
            Assert.That(tree.Nodes.Count(node => node.Template.First.Modifiers.Concat(node.Template.Second.Modifiers).Concat(node.Template.Third.Modifiers)
                .Any(modifier => modifier.Stat.Name.StartsWith("party") || modifier.When == ModifierCondition.InParty
                    || modifier.Stat.Name == DowserStats.LuckParty.Name || modifier.Stat.Name == DowserStats.ArtelParty.Name)), Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void TheDowserMechanicsDoNotAllocate()
        {
            var probeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (System.GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var (vein, veinInput) = Hero(Flat(DowserStats.VeinPower, 19), Flat(DowserStats.VeinStrike, 1));
            var (luck, luckInput) = Hero(Flat(DowserStats.LuckPower, 0.003));
            var (artel, artelInput) = Hero(Flat(DowserStats.ArtelPower, 0.5));
            void Run(GameLoop game, FakeInputSource input, int ticks)
            {
                for (var tick = 0; tick < ticks; tick++)
                {
                    input.PressKeys(1);
                    game.Tick();
                    game.Events.Clear();
                }
            }
            Run(vein, veinInput, 600);
            Run(luck, luckInput, 600);
            Run(artel, artelInput, 600);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            Run(vein, veinInput, 300);
            Run(luck, luckInput, 300);
            Run(artel, artelInput, 300);

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
