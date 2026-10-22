using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Content.Tree;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim.Headless
{
    public sealed class PlayerBot
    {
        private readonly GameData _data;
        private readonly GameLoop _loop;
        private readonly NodeScore _score;
        private readonly string _attribute;
        private readonly double _stuckSecondsBeforePrestige;
        private double _stuckSeconds;

        public PlayerBot(GameData data, GameLoop loop, double stuckMinutesBeforePrestige)
        {
            _data = data;
            _loop = loop;
            _stuckSecondsBeforePrestige = stuckMinutesBeforePrestige * 60;
            var heroClass = loop.State.Class;
            _attribute = heroClass == HeroClass.Bard ? "precision" : "tempo";
            _score = new NodeScore(PlayedTags(heroClass), data.Tree.Attributes);
        }

        public void Act(double secondsSinceLastAct)
        {
            _loop.Book.GrantStartingGems();
            EquipOwnedSkills();
            SpendTreePoints();
            SpendSubclassPoints();
            PrestigeWhenStuck(secondsSinceLastAct);
            SpendEcho();
        }

        private static StatTags PlayedTags(HeroClass heroClass) =>
            heroClass == HeroClass.Bard ? StatTags.Click | StatTags.Aura : StatTags.Keyboard;

        private void EquipOwnedSkills()
        {
            foreach (var gem in _loop.State.OwnedGems)
            {
                while (gem.Kind == Content.Gems.GemKind.Skill && _loop.Book.FreeCopies(gem) > 0 && _loop.Book.Equip(gem) == BookResult.Done)
                {
                }
            }
        }

        private void SpendTreePoints()
        {
            var tree = _data.Tree;
            while (_loop.State.Tree.PointsLeft > 0)
            {
                var step = NextTreeStep(tree);
                if (step == null)
                    return;
                var node = step.Value.Node;
                var result = _loop.AllocateNode(node.Id,
                    node.Type == TreeNodeType.Attribute ? _attribute : null,
                    node.Type == TreeNodeType.Mastery ? step.Value.Option : (int?)null);
                if (result != AllocationResult.Allocated)
                    return;
            }
        }

        private (TreeNodeDef Node, int Option)? NextTreeStep(PassiveTree tree)
        {
            var allocation = _loop.State.Tree;
            var start = tree.StartOf(allocation.StartClass);
            var firstStep = new Dictionary<TreeNodeDef, TreeNodeDef>();
            var distance = new Dictionary<TreeNodeDef, int>();
            var queue = new Queue<TreeNodeDef>();
            foreach (var node in tree.Nodes)
            {
                if (!allocation.IsAllocated(tree, node.Id))
                    continue;
                distance[node] = 0;
                queue.Enqueue(node);
            }

            TreeNodeDef? best = null;
            var bestValue = 0.0;
            var bestOption = 0;
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                foreach (var next in node.Neighbours)
                {
                    if (distance.ContainsKey(next) || next.Locked || next.Type == TreeNodeType.Start)
                        continue;
                    distance[next] = distance[node] + 1;
                    firstStep[next] = distance[node] == 0 ? next : firstStep[node];
                    queue.Enqueue(next);
                    var (score, option) = Score(next);
                    var value = score / distance[next];
                    if (value > bestValue)
                    {
                        best = next;
                        bestValue = value;
                        bestOption = option;
                    }
                }
            }
            if (best == null)
                return null;
            var step = firstStep[best];
            return (step, ReferenceEquals(step, best) ? bestOption : BestOption(step).Option);
        }

        private (double Score, int Option) Score(TreeNodeDef node)
        {
            switch (node.Type)
            {
                case TreeNodeType.Attribute:
                    return (_score.Attribute(node.AttributeAmount), 0);
                case TreeNodeType.Mastery:
                    return BestOption(node);
                default:
                    return (_score.Of(node.Modifiers), 0);
            }
        }

        private (double Score, int Option) BestOption(TreeNodeDef node)
        {
            var best = (Score: double.MinValue, Option: 0);
            for (var i = 0; i < node.Options.Count; i++)
            {
                var score = _score.Of(node.Options[i]);
                if (score > best.Score)
                    best = (score, i);
            }
            return node.Options.Count == 0 ? (0, 0) : best;
        }

        private void SpendSubclassPoints()
        {
            var catalog = _data.Ascendancy;
            var ascendancy = _loop.State.Ascendancy;
            if (!ascendancy.Unlocked(catalog))
                return;
            if (ascendancy.Subclass == null)
            {
                var chosen = BestSubclass(catalog);
                if (chosen == null || _loop.ChooseSubclass(chosen.Id) != SubclassResult.Chosen)
                    return;
            }
            while (ascendancy.PointsLeft(catalog) > 0)
            {
                TreeNodeDef? best = null;
                var bestValue = 0.0;
                foreach (var node in ascendancy.Allocatable(catalog))
                {
                    var value = _score.Of(node.Modifiers) + BestLeadingTo(node, ascendancy);
                    if (value > bestValue)
                    {
                        best = node;
                        bestValue = value;
                    }
                }
                if (best == null || _loop.AllocateSubclassNode(best.Id) != AllocationResult.Allocated)
                    return;
            }
        }

        private double BestLeadingTo(TreeNodeDef node, AscendancyState ascendancy)
        {
            var best = 0.0;
            foreach (var next in node.Neighbours)
            {
                if (next.Locked || next.Type == TreeNodeType.Start || IsTaken(ascendancy, next.Id))
                    continue;
                best = System.Math.Max(best, _score.Of(next.Modifiers));
            }
            return best;
        }

        private static bool IsTaken(AscendancyState ascendancy, string id)
        {
            foreach (var taken in ascendancy.Allocated)
            {
                if (taken == id)
                    return true;
            }
            return false;
        }

        private SubclassDef? BestSubclass(AscendancyCatalog catalog)
        {
            SubclassDef? best = null;
            var bestScore = double.MinValue;
            foreach (var subclass in catalog.SubclassesOf(_loop.State.Tree.StartClass))
            {
                var notables = new List<double>();
                foreach (var node in subclass.Nodes)
                {
                    if (node.Type == TreeNodeType.Notable && !node.Locked)
                        notables.Add(_score.Of(node.Modifiers));
                }
                notables.Sort();
                notables.Reverse();
                var score = _score.Of(subclass.Start.Modifiers);
                for (var i = 0; i < notables.Count && i < catalog.MaxPoints / 2; i++)
                    score += notables[i];
                if (score > bestScore)
                {
                    best = subclass;
                    bestScore = score;
                }
            }
            return best;
        }

        private void PrestigeWhenStuck(double seconds)
        {
            var state = _loop.State;
            _stuckSeconds = state.Farming && state.BossFailsInARow > 0 ? _stuckSeconds + seconds : 0;
            if (_stuckSeconds < _stuckSecondsBeforePrestige || !_loop.Prestige.Available || _loop.Prestige.EchoGain.Sign <= 0)
                return;
            if (_loop.Prestige.TryPrestige())
                _stuckSeconds = 0;
        }

        private void SpendEcho()
        {
            var artifacts = _loop.Artifacts;
            while (_loop.Artifacts.TryUnlock() != null)
            {
            }
            while (true)
            {
                string? cheapest = null;
                var cheapestCost = BigNumber.Zero;
                foreach (var pair in _loop.State.Artifacts)
                {
                    var cost = artifacts.LevelCost(pair.Key);
                    if (cheapest == null || cost < cheapestCost)
                    {
                        cheapest = pair.Key;
                        cheapestCost = cost;
                    }
                }
                if (cheapest == null || !artifacts.TryLevelUp(cheapest))
                    return;
            }
        }
    }
}
