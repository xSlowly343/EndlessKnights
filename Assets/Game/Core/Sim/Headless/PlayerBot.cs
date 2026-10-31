using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Content.Items;
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
        private readonly HashSet<Content.Gems.GemDef> _triedGems = new HashSet<Content.Gems.GemDef>();
        private readonly double _stuckSecondsBeforePrestige;
        private const int BossFailsBeforePrestige = 1;
        private const double SlowStageSeconds = 60;
        private readonly HeroClass _heroClass;
        private double _stuckSeconds;
        private double _stageSeconds;
        private long _stage;

        public PlayerBot(GameData data, GameLoop loop, double stuckMinutesBeforePrestige, bool inParty = false)
        {
            _data = data;
            _loop = loop;
            _stuckSecondsBeforePrestige = stuckMinutesBeforePrestige * 60;
            var heroClass = loop.State.Class;
            _heroClass = heroClass;
            _score = new NodeScore(PlayedTags(heroClass), HeroAttributes.Names, inParty);
        }

        public void Act(double secondsSinceLastAct)
        {
            if (_loop.ClassChoiceReady)
                _loop.ChooseClass(_heroClass);
            EquipStartingSkills();
            SpendTreePoints();
            SpendSubclassPoints();
            PrestigeWhenStuck(secondsSinceLastAct);
            SpendEcho();
            CutUncutGems();
            PlaceSlates();
            _loop.Upgrades.BuyMax();
        }

        private static StatTags PlayedTags(HeroClass heroClass) =>
            heroClass == HeroClass.Metalhead ? StatTags.Keyboard | StatTags.Aura : StatTags.Keyboard;

        public void EquipStartingSkills()
        {
            _loop.Book.GrantStartingGems();
            EquipOwnedSkills();
        }

        private void EquipOwnedSkills()
        {
            if (_loop.State.Skills.Count >= _loop.Book.SlotCount)
                return;
            _triedGems.Clear();
            foreach (var gem in _loop.State.OwnedGems)
            {
                if (gem.Kind != Content.Gems.GemKind.Skill || !_triedGems.Add(gem))
                    continue;
                while (_loop.Book.FreeCopies(gem) > 0 && _loop.Book.Equip(gem) == BookResult.Done)
                {
                }
            }
        }

        private void CutUncutGems()
        {
            while (_loop.BuyUncutGem())
            {
            }
            foreach (var gem in _loop.Drops.PoolOf(Content.Gems.GemKind.Skill))
            {
                if (_loop.Drops.UncutGems == 0)
                    return;
                if (!_loop.State.OwnedGems.Contains(gem))
                    _loop.CutGem(gem);
            }
        }

        private void PlaceSlates()
        {
            var slates = _loop.Slates;
            if (!slates.Open || slates.Bag.Count == 0)
                return;
            var bag = new List<SlateItem>(slates.Bag);
            bag.Sort((a, b) => SlateScore(b).CompareTo(SlateScore(a)));
            foreach (var slate in bag)
            {
                if (!TryPlace(slate))
                    ReplaceWeakest(slate);
            }
        }

        private double SlateScore(SlateItem slate)
        {
            var modifiers = new List<Modifier>();
            _loop.Slates.ModifiersOf(slate, 0, modifiers);
            return _score.Of(modifiers);
        }

        private bool TryPlace(SlateItem slate)
        {
            var slates = _loop.Slates;
            var (width, height) = slates.Size;
            var best = (X: -1, Y: -1, Rotation: 0, Touching: -1);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    for (var rotation = 0; rotation < Content.Slates.SlateShape.Rotations; rotation++)
                    {
                        if (slates.Fits(slate, x, y, rotation) != SlateResult.Done)
                            continue;
                        var touching = SameThemeAround(slate, x, y, rotation);
                        if (touching > best.Touching)
                            best = (x, y, rotation, touching);
                    }
                }
            }
            return best.Touching >= 0 && slates.Place(slate, best.X, best.Y, best.Rotation) == SlateResult.Done;
        }

        private int SameThemeAround(SlateItem slate, int x, int y, int rotation)
        {
            var seen = new HashSet<SlatePlacement>();
            foreach (var (cellX, cellY) in slate.Shape.Cells(rotation))
            {
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var neighbour = _loop.Slates.At(x + cellX + dx, y + cellY + dy);
                    if (neighbour != null && neighbour.Slate.Theme == slate.Theme)
                        seen.Add(neighbour);
                }
            }
            return seen.Count;
        }

        private void ReplaceWeakest(SlateItem slate)
        {
            var slates = _loop.Slates;
            var score = SlateScore(slate);
            SlatePlacement? weakest = null;
            var weakestScore = double.MaxValue;
            foreach (var placement in slates.Board)
            {
                var placedScore = SlateScore(placement.Slate);
                if (placement.Slate.Shape.Size >= slate.Shape.Size && placedScore < weakestScore)
                {
                    weakest = placement;
                    weakestScore = placedScore;
                }
            }
            if (weakest == null || weakestScore >= score)
                return;
            slates.Remove(weakest.Slate);
            if (TryPlace(slate))
                slates.Dismantle(weakest.Slate);
            else
                slates.Place(weakest.Slate, weakest.X, weakest.Y, weakest.Rotation);
        }

        public void SpendTreePoints()
        {
            SpendCommonTreePoints();
            if (_loop.ClassTreeDef is { } classTree)
                SpendRankedPoints(classTree, _loop.State.ClassTree);
        }

        private void SpendCommonTreePoints()
        {
            for (var slot = 0; slot < _loop.CommonTreeSlots.Count; slot++)
            {
                if (!_loop.CommonTreeSlotOpen(slot))
                    continue;
                if (_loop.CommonTreeDef(slot) == null && BestCommonTree() is { } best)
                    _loop.ChooseCommonTree(slot, best);
                if (_loop.CommonTreeDef(slot) is { } tree)
                    SpendRankedPoints(tree, _loop.CommonTreeAllocation(slot)!);
            }
        }

        private string? BestCommonTree()
        {
            string? best = null;
            var bestValue = double.MinValue;
            foreach (var pair in _data.CommonTrees)
            {
                if (_loop.State.CommonTrees.Exists(slot => slot?.TreeId == pair.Key))
                    continue;
                var value = 0.0;
                foreach (var node in pair.Value.Nodes)
                    value += _score.Of(node.ModifiersAt(TreeTemplate.Ranks));
                if (value > bestValue)
                {
                    best = pair.Key;
                    bestValue = value;
                }
            }
            return best;
        }

        private void SpendRankedPoints(ClassTree tree, ClassTreeAllocation allocation)
        {
            while (allocation.PointsLeft > 0)
            {
                ClassTreeNode? best = null;
                var bestValue = double.MinValue;
                foreach (var node in tree.Nodes)
                {
                    if (allocation.CheckRaise(tree, node.Id) != RankResult.Done)
                        continue;
                    var rank = allocation.RankOf(node.Id);
                    var value = _score.Of(node.ModifiersAt(rank + 1)) - _score.Of(node.ModifiersAt(rank)) + node.Row * 1e-3;
                    if (value > bestValue)
                    {
                        best = node;
                        bestValue = value;
                    }
                }
                if (best == null || _loop.RaiseNode(best.Id) != RankResult.Done)
                    break;
            }
            ChooseMajorTalents(tree, allocation);
        }

        private void ChooseMajorTalents(ClassTree tree, ClassTreeAllocation allocation)
        {
            foreach (var slot in tree.Majors)
            {
                if (allocation.PointsSpent < slot.At || allocation.Majors.ContainsKey(slot.At))
                    continue;
                var best = slot.Options[0];
                foreach (var option in slot.Options)
                {
                    if (_score.Of(option.Modifiers) > _score.Of(best.Modifiers))
                        best = option;
                }
                _loop.ChooseMajorTalent(slot.At, best.Id);
            }
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
            foreach (var subclass in catalog.SubclassesOf(_loop.State.ClassName))
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
            if (state.Stage != _stage)
            {
                _stage = state.Stage;
                _stageSeconds = 0;
            }
            else if (!state.FightingBoss)
            {
                _stageSeconds += seconds;
            }
            var bossFailed = state.BossFailsInARow >= BossFailsBeforePrestige;
            var slowStage = _stageSeconds > SlowStageSeconds;
            if (!bossFailed && !slowStage && _stuckSeconds < _stuckSecondsBeforePrestige)
                return;
            if (!_loop.Prestige.Available || _loop.Prestige.EchoGain.Sign <= 0)
                return;
            if (_loop.Prestige.TryPrestige())
            {
                _stuckSeconds = 0;
                _stageSeconds = 0;
            }
        }

        private void SpendEcho()
        {
            var artifacts = _loop.Artifacts;
            while (true)
            {
                var cheapest = CheapestLevel(out var levelCost);
                var canUnlock = !artifacts.AllUnlocked && !artifacts.WaitsForGroup && artifacts.Currency >= artifacts.UnlockCost;
                if (canUnlock && (cheapest == null || artifacts.UnlockCost <= levelCost) && artifacts.TryUnlock() != null)
                    continue;
                if (cheapest == null || !artifacts.TryLevelUp(cheapest))
                    return;
            }
        }

        private string? CheapestLevel(out BigNumber cost)
        {
            var artifacts = _loop.Artifacts;
            string? cheapest = null;
            cost = BigNumber.Zero;
            foreach (var pair in _loop.State.Artifacts)
            {
                if (artifacts.IsMaxed(pair.Key))
                    continue;
                var next = artifacts.LevelCost(pair.Key);
                if (cheapest == null || next < cost)
                {
                    cheapest = pair.Key;
                    cost = next;
                }
            }
            return cheapest;
        }
    }
}
