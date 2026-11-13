using System.Collections.Generic;
using System.Linq;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyTreeTests
    {
        private const long Day = 86_400_000;
        private const long Now = 1_763_000_000_000;

        private static PartyTreeRules Tree => TestWorld.Data.Party.Growth.Tree;

        private static Dictionary<int, int> Levels(params (int Node, int Rank)[] ranks) => ranks.ToDictionary(r => r.Node, r => r.Rank);

        private static List<Modifier> Build(Dictionary<int, int> levels, int members, params string[] majors)
        {
            var output = new List<Modifier>();
            PartyTreeBonus.Build(Tree, levels, majors, members, output);
            return output;
        }

        private static double Sum(List<Modifier> lines, StatId stat, ModifierKind kind = ModifierKind.Increased, ModifierCondition when = ModifierCondition.Always) =>
            lines.Where(m => m.Stat == stat && m.Kind == kind && m.When == when && m.Tags == StatTags.None).Sum(m => m.Value);

        [Test]
        public void TheTreeHasFiftyNodesInSevenRows()
        {
            Assert.That(Tree.Nodes.Count, Is.EqualTo(50));
            Assert.That(Enumerable.Range(1, 7).Select(row => Tree.Nodes.Count(n => n.Row == row)), Is.EqualTo(new[] { 10, 9, 8, 7, 6, 5, 5 }));
            Assert.That(Tree.RowThresholds, Is.EqualTo(new[] { 0, 5, 15, 30, 50, 75, 105 }));
            Assert.That(Tree.RowPrices, Is.EqualTo(new[] { 100, 150, 200, 300, 400, 550, 700 }));
            Assert.That(Tree.MaxPoints, Is.EqualTo(180));
            Assert.That(Tree.Majors.Select(m => m.At), Is.EqualTo(new[] { 20, 50, 90, 140 }));
            Assert.That(Tree.Majors.All(m => m.Options.Count == 3));
            Assert.That(Tree.Nodes.Where(n => n.Row == 7).All(n => n.Keystone));
        }

        [Test]
        public void PricesFallWithBookkeepingTheSameWayAsOnTheServer()
        {
            var levels = Levels((1, 5), (9, 1), (19, 4));
            Assert.That(Tree.Price(2, levels), Is.EqualTo(91));
            Assert.That(Tree.Price(11, levels), Is.EqualTo(137));
            Assert.That(Tree.Price(20, levels), Is.EqualTo(200));
            Assert.That(Tree.Price(46, Levels()), Is.EqualTo(700));
        }

        [Test]
        public void RowsOpenByPointsAndParentsMustBeTaken()
        {
            Assert.That(Tree.CanRaise(1, Levels()), Is.EqualTo(PartyTreeRefusal.None));
            Assert.That(Tree.CanRaise(11, Levels((2, 4))), Is.EqualTo(PartyTreeRefusal.RowClosed));
            Assert.That(Tree.CanRaise(11, Levels((2, 5))), Is.EqualTo(PartyTreeRefusal.NoParent));
            Assert.That(Tree.CanRaise(11, Levels((1, 5))), Is.EqualTo(PartyTreeRefusal.None));
            Assert.That(Tree.CanRaise(1, Levels((1, 5))), Is.EqualTo(PartyTreeRefusal.MaxRank));
            Assert.That(Tree.CanRaise(99, Levels()), Is.EqualTo(PartyTreeRefusal.Unknown));
        }

        [Test]
        public void NodeLinesGrowWithLevelsAndOpenNewLinesOnThreeAndFive()
        {
            Assert.That(Sum(Build(Levels((1, 1)), 1), CombatStats.Damage), Is.EqualTo(0.03).Within(1e-9));
            Assert.That(Sum(Build(Levels((1, 2)), 1), CombatStats.Damage), Is.EqualTo(0.06).Within(1e-9));
            var third = Build(Levels((1, 3)), 1);
            Assert.That(Sum(third, CombatStats.CritChance, ModifierKind.Flat), Is.EqualTo(0.01).Within(1e-9));
            Assert.That(Sum(third, CombatStats.Damage, ModifierKind.Increased, ModifierCondition.BossOpening), Is.EqualTo(0));
            var fifth = Build(Levels((1, 5)), 1);
            Assert.That(Sum(fifth, CombatStats.Damage), Is.EqualTo(0.09).Within(1e-9));
            Assert.That(Sum(fifth, CombatStats.Damage, ModifierKind.Increased, ModifierCondition.BossOpening), Is.EqualTo(0.15).Within(1e-9));
        }

        [Test]
        public void LinesThatWaitForTheirMechanicGiveNothingYet()
        {
            Assert.That(Build(Levels((7, 5)), 4), Is.Empty, "the hideout pot comes later");
            Assert.That(Build(Levels((19, 5)), 4), Is.Empty, "a cheaper price is the server's business");
        }

        [Test]
        public void FormationCountsMembersAndWantsAFullParty()
        {
            Assert.That(Sum(Build(Levels((11, 1)), 3), CombatStats.Damage), Is.EqualTo(0.09).Within(1e-9));
            Assert.That(Sum(Build(Levels((11, 5)), 3), CombatStats.Damage), Is.EqualTo(0.27).Within(1e-9), "×3 at level 5, no +10% with three");
            Assert.That(Sum(Build(Levels((11, 5)), 4), CombatStats.Damage), Is.EqualTo(0.46).Within(1e-9), "0.03 × 3 × 4 + 0.10 with four");
        }

        [Test]
        public void KeystonesKeepTheirFirstLineAndSoftenTheMinus()
        {
            var first = Build(Levels((46, 1)), 4);
            Assert.That(Sum(first, CombatStats.Damage, ModifierKind.More), Is.EqualTo(0.25).Within(1e-9));
            Assert.That(Sum(first, CombatStats.RoleBuffEffect), Is.EqualTo(-0.30).Within(1e-9));
            Assert.That(Sum(Build(Levels((46, 4)), 4), CombatStats.Damage, ModifierKind.More), Is.EqualTo(0.25).Within(1e-9));
            Assert.That(Sum(Build(Levels((46, 3)), 4), CombatStats.RoleBuffEffect), Is.EqualTo(-0.20).Within(1e-9));
            Assert.That(Sum(Build(Levels((46, 5)), 4), CombatStats.RoleBuffEffect), Is.EqualTo(-0.10).Within(1e-9));
        }

        [Test]
        public void TheBarAndBigTalentsCountInvestedPoints()
        {
            var twenty = Levels((1, 5), (2, 5), (3, 5), (4, 5));
            var plain = Build(twenty, 2);
            Assert.That(Sum(plain, CombatStats.Damage), Is.EqualTo(0.09 + 0.02).Within(1e-9), "node 1 and one bar step of damage");
            Assert.That(Sum(plain, CombatStats.RoleBuffEffect), Is.EqualTo(0.09 + 0.09 + 0.02).Within(1e-9), "node 4, Familiar Faces and one bar step");
            Assert.That(Build(twenty, 2, "party_m50_united").Count, Is.EqualTo(plain.Count), "a talent above the bar does not work");

            var fifty = Levels((1, 5), (2, 5), (3, 5), (4, 5), (5, 5), (6, 5), (7, 5), (8, 5), (9, 5), (10, 5));
            var united = Build(fifty, 3, "party_m50_united");
            Assert.That(Sum(united, CombatStats.Damage) - Sum(Build(fifty, 3), CombatStats.Damage), Is.EqualTo(0.12).Within(1e-9));
        }

        [Test]
        public void PartyLegendRaisesTheWholeTree()
        {
            var levels = new Dictionary<int, int>();
            for (var node = 1; node <= 28; node++)
                levels[node] = 5;
            var plain = Sum(Build(levels, 1), CombatStats.Damage);
            var legend = Sum(Build(levels, 1, "party_m140_legend"), CombatStats.Damage);
            Assert.That(legend, Is.EqualTo(plain * 1.1).Within(1e-9));
        }

        [Test]
        public void TheTreeFeedsThePartyBasketAndNewcomersGetTheirShare()
        {
            var loop = TestWorld.Loop();
            var before = loop.Hero.Value(CombatStats.Damage, 1);
            loop.SetPartyStanding(true, Now - 10 * Day, 4, Now);
            loop.SetPartyTree(Levels((1, 2)), new string[0]);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1) / before, Is.EqualTo(1.06).Within(1e-9));
            Assert.That(loop.PartyGrowth.TreeLevels[1], Is.EqualTo(2));
            loop.SetPartyStanding(true, Now, 4, Now);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1) / before, Is.EqualTo(1.018).Within(1e-9));
            loop.SetPartyStanding(false, 0, 0, Now);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(before).Within(1e-9));
        }

        [Test]
        public void BondLinesStrengthenRoleBuffsAndFightingTogether()
        {
            Assert.That(Sum(Build(Levels((13, 1)), 4), CombatStats.RoleBuffEffect), Is.EqualTo(0.04).Within(1e-9));
            Assert.That(Sum(Build(Levels((13, 5)), 4), CombatStats.Damage, ModifierKind.Increased, ModifierCondition.InParty), Is.EqualTo(0.08).Within(1e-9));
            var weavers = Build(Levels((47, 1)), 4);
            Assert.That(Sum(weavers, CombatStats.RoleBuffEffect, ModifierKind.More), Is.EqualTo(0.4).Within(1e-9));
            Assert.That(Sum(weavers, CombatStats.Damage, ModifierKind.Increased, ModifierCondition.Solo), Is.EqualTo(-0.15).Within(1e-9));
            var talent = Tree.Majors[3].Options[1].Lines[0].Modifier!.Value;
            Assert.That(talent.When, Is.EqualTo(ModifierCondition.FourRoles));
        }

        [Test]
        public void FourRolesCountTheHeroAndAllies()
        {
            var loop = TestWorld.Loop();
            var bit = ModifierConditions.Bit(ModifierCondition.FourRoles);
            Assert.That(loop.Conditions.Current() & bit, Is.Zero);
            var rules = TestWorld.Data.Synergy;
            var roles = rules.RoleMaskOf(loop.State.Class);
            var slot = 0;
            foreach (HeroClass heroClass in System.Enum.GetValues(typeof(HeroClass)))
            {
                var mask = rules.RoleMaskOf(heroClass);
                if (slot >= 3 || mask == 0 || (roles & mask) != 0)
                    continue;
                loop.Synergy.SetAlly(slot++, true, heroClass);
                roles |= mask;
            }
            Assert.That(loop.Conditions.Current() & bit, Is.Not.Zero, "four different roles in the fight");
            loop.Synergy.ClearAllies();
            Assert.That(loop.Conditions.Current() & bit, Is.Zero);
        }
    }
}
