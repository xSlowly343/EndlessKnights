using System;
using System.Collections.Generic;
using Game.Core.Content.Tree;
using Game.Core.Stats;

namespace Game.Core.Build
{
    public sealed class ClassTreeAllocation
    {
        private readonly Dictionary<string, int> _ranks = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<int, string> _majors = new Dictionary<int, string>();
        private readonly List<string> _plan = new List<string>();

        public ClassTreeAllocation(string heroClass)
        {
            HeroClass = heroClass;
        }

        public string HeroClass { get; }
        public int PointsEarned { get; set; }
        public int PointsSpent { get; private set; }
        public int PointsLeft => PointsEarned - PointsSpent;
        public IReadOnlyDictionary<string, int> Ranks => _ranks;
        public IReadOnlyDictionary<int, string> Majors => _majors;
        public IReadOnlyList<string> Plan => _plan;

        public int RankOf(string nodeId) => _ranks.TryGetValue(nodeId, out var rank) ? rank : 0;

        public int BarSteps(ClassTree tree) => PointsSpent / tree.BarEvery;

        public int PointsBelow(ClassTree tree, int row)
        {
            var points = 0;
            foreach (var pair in _ranks)
            {
                if (tree.TryGet(pair.Key, out var node) && node.Row < row)
                    points += pair.Value;
            }
            return points;
        }

        public bool RowOpen(ClassTree tree, int row) => PointsBelow(tree, row) >= tree.ThresholdOf(row);

        public RankResult CheckRaise(ClassTree tree, string nodeId)
        {
            if (!tree.TryGet(nodeId, out var node))
                return RankResult.UnknownNode;
            if (RankOf(nodeId) >= TreeTemplate.Ranks)
                return RankResult.MaxRank;
            if (PointsLeft <= 0)
                return RankResult.NoPoints;
            if (!RowOpen(tree, node.Row))
                return RankResult.RowClosed;
            if (node.Parent != null && RankOf(node.Parent.Id) == 0)
                return RankResult.NeedsParent;
            return RankResult.Done;
        }

        public RankResult Raise(ClassTree tree, string nodeId)
        {
            var check = CheckRaise(tree, nodeId);
            if (check != RankResult.Done)
                return check;
            _ranks[nodeId] = RankOf(nodeId) + 1;
            PointsSpent++;
            if (CountInPlan(nodeId) < _ranks[nodeId])
                _plan.Add(nodeId);
            return RankResult.Done;
        }

        public int FillFromPlan(ClassTree tree)
        {
            var raised = 0;
            var progress = true;
            while (progress && PointsLeft > 0)
            {
                progress = false;
                var wanted = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var id in _plan)
                {
                    wanted[id] = (wanted.TryGetValue(id, out var count) ? count : 0) + 1;
                    if (RankOf(id) >= wanted[id] || CheckRaise(tree, id) != RankResult.Done)
                        continue;
                    Raise(tree, id);
                    raised++;
                    progress = true;
                    break;
                }
            }
            return raised;
        }

        public void ForgetPlan() => _plan.Clear();

        public void RestorePlan(IEnumerable<string> plan)
        {
            _plan.Clear();
            _plan.AddRange(plan);
        }

        private int CountInPlan(string nodeId)
        {
            var count = 0;
            foreach (var id in _plan)
            {
                if (id == nodeId)
                    count++;
            }
            return count;
        }

        public RankResult CheckLower(ClassTree tree, string nodeId)
        {
            if (!tree.TryGet(nodeId, out var node))
                return RankResult.UnknownNode;
            var rank = RankOf(nodeId);
            if (rank == 0)
                return RankResult.NotTaken;
            if (rank == 1 && HasTakenChild(tree, node))
                return RankResult.HoldsChild;
            _ranks[nodeId] = rank - 1;
            var rowsHold = RowsHold(tree);
            _ranks[nodeId] = rank;
            return rowsHold ? RankResult.Done : RankResult.HoldsRow;
        }

        public RankResult Lower(ClassTree tree, string nodeId)
        {
            var check = CheckLower(tree, nodeId);
            if (check != RankResult.Done)
                return check;
            var rank = RankOf(nodeId) - 1;
            if (rank == 0)
                _ranks.Remove(nodeId);
            else
                _ranks[nodeId] = rank;
            PointsSpent--;
            var last = _plan.LastIndexOf(nodeId);
            if (last >= 0)
                _plan.RemoveAt(last);
            return RankResult.Done;
        }

        public RankResult ChooseMajor(ClassTree tree, int at, string talentId)
        {
            var slot = tree.MajorAt(at);
            if (slot == null || !Offers(slot, talentId))
                return RankResult.UnknownMajor;
            if (PointsSpent < at)
                return RankResult.MarkNotReached;
            _majors[at] = talentId;
            return RankResult.Done;
        }

        public bool MajorActive(int at) => _majors.ContainsKey(at) && PointsSpent >= at;

        public void Clear()
        {
            _ranks.Clear();
            _majors.Clear();
            PointsSpent = 0;
        }

        public void Restore(string nodeId, int rank)
        {
            if (rank <= 0)
                return;
            PointsSpent += rank - RankOf(nodeId);
            _ranks[nodeId] = rank;
        }

        public void RestoreMajor(int at, string talentId) => _majors[at] = talentId;

        public int DropInvalid(ClassTree tree)
        {
            var dropped = 0;
            foreach (var id in new List<string>(_ranks.Keys))
            {
                if (!tree.TryGet(id, out _) || _ranks[id] > TreeTemplate.Ranks)
                {
                    dropped += _ranks[id];
                    PointsSpent -= _ranks[id];
                    _ranks.Remove(id);
                }
            }
            if (dropped > 0 || !RowsHold(tree) || !ParentsHold(tree))
            {
                dropped += PointsSpent;
                Clear();
            }
            foreach (var pair in new List<KeyValuePair<int, string>>(_majors))
            {
                var slot = tree.MajorAt(pair.Key);
                if (slot == null || !Offers(slot, pair.Value))
                    _majors.Remove(pair.Key);
            }
            return dropped;
        }

        public List<Modifier> Modifiers(ClassTree tree)
        {
            var modifiers = new List<Modifier>();
            foreach (var pair in _ranks)
            {
                if (tree.TryGet(pair.Key, out var node))
                    modifiers.AddRange(node.Template.ModifiersAt(pair.Value));
            }
            for (var step = 1; step <= BarSteps(tree); step++)
                modifiers.AddRange(tree.BarPassiveAt(step));
            foreach (var slot in tree.Majors)
            {
                if (!MajorActive(slot.At))
                    continue;
                foreach (var option in slot.Options)
                {
                    if (option.Id == _majors[slot.At])
                        modifiers.AddRange(option.Modifiers);
                }
            }
            return modifiers;
        }

        private static bool Offers(MajorTalentSlot slot, string talentId)
        {
            foreach (var option in slot.Options)
            {
                if (option.Id == talentId)
                    return true;
            }
            return false;
        }

        private bool HasTakenChild(ClassTree tree, ClassTreeNode node)
        {
            foreach (var other in tree.Nodes)
            {
                if (other.Parent == node && RankOf(other.Id) > 0)
                    return true;
            }
            return false;
        }

        private bool RowsHold(ClassTree tree)
        {
            for (var row = 2; row <= tree.Rows; row++)
            {
                if (TakenInRow(tree, row) && !RowOpen(tree, row))
                    return false;
            }
            return true;
        }

        private bool ParentsHold(ClassTree tree)
        {
            foreach (var pair in _ranks)
            {
                if (tree.TryGet(pair.Key, out var node) && node.Parent != null && RankOf(node.Parent.Id) == 0)
                    return false;
            }
            return true;
        }

        private bool TakenInRow(ClassTree tree, int row)
        {
            foreach (var pair in _ranks)
            {
                if (tree.TryGet(pair.Key, out var node) && node.Row == row)
                    return true;
            }
            return false;
        }
    }
}
