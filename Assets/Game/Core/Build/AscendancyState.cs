using System;
using System.Collections.Generic;
using Game.Core.Content.Tree;
using Game.Core.Stats;

namespace Game.Core.Build
{
    public sealed class AscendancyState
    {
        private const int ActivityCapacity = 32;

        private readonly HashSet<string> _allocated = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _activities = new HashSet<string>(ActivityCapacity, StringComparer.Ordinal);
        private readonly HashSet<string> _forkPicks = new HashSet<string>(StringComparer.Ordinal);

        public string? Subclass { get; private set; }
        public long LastFreeChangeUnixMs { get; set; }
        public IReadOnlyCollection<string> Allocated => _allocated;
        public IReadOnlyCollection<string> CompletedActivities => _activities;
        public IReadOnlyCollection<string> ForkPicks => _forkPicks;

        public TraitDef? Trait(AscendancyCatalog catalog) =>
            Subclass != null && catalog.TryGet(Subclass, out var subclass) ? subclass.Trait : null;

        public int TraitLevel(AscendancyCatalog catalog) =>
            Trait(catalog) == null ? 0 : System.Math.Min(TraitDef.MaxLevel, PointsEarned(catalog));

        public bool Picked(string optionId) => _forkPicks.Contains(optionId);

        public Game.Core.Party.PartyGiveMode PartyMode(AscendancyCatalog catalog)
        {
            if (Subclass == null || !catalog.TryGet(Subclass, out var subclass))
                return Game.Core.Party.PartyGiveMode.Together;
            var mode = subclass.PartyMode;
            if (subclass.Trait == null)
                return mode;
            foreach (var fork in subclass.Trait.Forks)
            {
                foreach (var choice in fork.Choices)
                {
                    if (choice.PartyMode is { } picked && _forkPicks.Contains(choice.Id))
                        mode = picked;
                }
            }
            return mode;
        }

        public TraitForkResult PickFork(AscendancyCatalog catalog, int heroLevel, string optionId)
        {
            var trait = Trait(catalog);
            if (trait == null)
                return TraitForkResult.NoTrait;
            if (!trait.TryFind(optionId, out var fork, out _))
                return TraitForkResult.UnknownOption;
            if (heroLevel < fork.HeroLevel)
                return TraitForkResult.HeroLevelTooLow;
            foreach (var choice in fork.Choices)
            {
                if (_forkPicks.Contains(choice.Id))
                    return TraitForkResult.AlreadyPicked;
            }
            _forkPicks.Add(optionId);
            return TraitForkResult.Picked;
        }

        public int PointsEarned(AscendancyCatalog catalog)
        {
            var points = 0;
            foreach (var activity in catalog.Activities)
            {
                if (_activities.Contains(activity.Id))
                    points++;
            }
            return points;
        }

        public int PointsLeft(AscendancyCatalog catalog) => PointsEarned(catalog) - _allocated.Count;

        public bool Unlocked(AscendancyCatalog catalog) => PointsEarned(catalog) > 0;

        public bool Complete(string activityId) => _activities.Add(activityId);

        public SubclassResult Choose(AscendancyCatalog catalog, string heroClass, string subclassId)
        {
            if (Subclass != null)
                return SubclassResult.AlreadyChosen;
            var check = CheckSubclass(catalog, heroClass, subclassId);
            if (check != SubclassResult.Chosen)
                return check;
            Subclass = subclassId;
            return SubclassResult.Chosen;
        }

        public bool FreeChangeReady(AscendancyCatalog catalog, long nowUnixMs) =>
            nowUnixMs - LastFreeChangeUnixMs >= catalog.FreeChangeHours * 3_600_000;

        public SubclassResult ChangeFree(AscendancyCatalog catalog, string heroClass, string subclassId, long nowUnixMs)
        {
            if (Subclass == null)
                return SubclassResult.NotChosen;
            if (!FreeChangeReady(catalog, nowUnixMs))
                return SubclassResult.NotYet;
            var result = Change(catalog, heroClass, subclassId);
            if (result == SubclassResult.Chosen)
                LastFreeChangeUnixMs = nowUnixMs;
            return result;
        }

        public SubclassResult ChangePaid(AscendancyCatalog catalog, string heroClass, string subclassId) =>
            Subclass == null ? SubclassResult.NotChosen : Change(catalog, heroClass, subclassId);

        public void Reset()
        {
            Subclass = null;
            _allocated.Clear();
            _forkPicks.Clear();
        }

        public AllocationResult Allocate(AscendancyCatalog catalog, string nodeId)
        {
            var check = Check(catalog, nodeId);
            if (check == AllocationResult.Allocated)
                _allocated.Add(nodeId);
            return check;
        }

        public bool IsAllocated(string nodeId) => _allocated.Contains(nodeId);

        public bool CanRemove(AscendancyCatalog catalog, string nodeId)
        {
            if (!_allocated.Contains(nodeId) || Subclass == null || !catalog.TryGet(Subclass, out var subclass))
                return false;
            var reached = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<TreeNodeDef>();
            pending.Push(subclass.Start);
            while (pending.Count > 0)
            {
                foreach (var neighbour in pending.Pop().Neighbours)
                {
                    if (neighbour.Id != nodeId && _allocated.Contains(neighbour.Id) && reached.Add(neighbour.Id))
                        pending.Push(neighbour);
                }
            }
            return reached.Count == _allocated.Count - 1;
        }

        public bool Remove(AscendancyCatalog catalog, string nodeId)
        {
            if (!CanRemove(catalog, nodeId))
                return false;
            _allocated.Remove(nodeId);
            return true;
        }

        public AllocationResult Check(AscendancyCatalog catalog, string nodeId)
        {
            if (Subclass == null || !catalog.TryGet(Subclass, out var subclass))
                return AllocationResult.Locked;
            if (!subclass.TryGet(nodeId, out var node))
                return AllocationResult.UnknownNode;
            if (_allocated.Contains(nodeId) || node.Type == TreeNodeType.Start)
                return AllocationResult.AlreadyAllocated;
            if (node.Locked)
                return AllocationResult.Locked;
            if (PointsLeft(catalog) <= 0)
                return AllocationResult.NoPoints;
            if (!TouchesAllocated(subclass, node))
                return AllocationResult.NotConnected;
            return AllocationResult.Allocated;
        }

        public IReadOnlyList<TreeNodeDef> Allocatable(AscendancyCatalog catalog)
        {
            var allocatable = new List<TreeNodeDef>();
            if (Subclass == null || !catalog.TryGet(Subclass, out var subclass))
                return allocatable;
            foreach (var node in subclass.Nodes)
            {
                if (!_allocated.Contains(node.Id) && !node.Locked && node.Type != TreeNodeType.Start && TouchesAllocated(subclass, node))
                    allocatable.Add(node);
            }
            return allocatable;
        }

        public IReadOnlyList<Modifier> Modifiers(AscendancyCatalog catalog)
        {
            var modifiers = new List<Modifier>();
            if (Subclass == null || !catalog.TryGet(Subclass, out var subclass))
                return modifiers;
            modifiers.AddRange(subclass.Start.Modifiers);
            if (subclass.Trait is { } trait)
                AddTrait(trait, TraitLevel(catalog), modifiers);
            foreach (var id in _allocated)
            {
                if (subclass.TryGet(id, out var node))
                    modifiers.AddRange(node.Modifiers);
            }
            return modifiers;
        }

        public void Restore(string? subclass, IEnumerable<string> allocated, IEnumerable<string> activities, IEnumerable<string>? forkPicks = null)
        {
            Subclass = subclass;
            _allocated.Clear();
            foreach (var id in allocated)
                _allocated.Add(id);
            _forkPicks.Clear();
            if (forkPicks != null)
            {
                foreach (var id in forkPicks)
                    _forkPicks.Add(id);
            }
            _activities.Clear();
            foreach (var id in activities)
                _activities.Add(id);
        }

        private SubclassResult Change(AscendancyCatalog catalog, string heroClass, string subclassId)
        {
            var check = CheckSubclass(catalog, heroClass, subclassId);
            if (check != SubclassResult.Chosen)
                return check;
            Subclass = subclassId;
            _allocated.Clear();
            _forkPicks.Clear();
            return SubclassResult.Chosen;
        }

        private void AddTrait(TraitDef trait, int level, List<Modifier> modifiers)
        {
            for (var i = 0; i < level; i++)
                modifiers.AddRange(trait.Levels[i]);
            modifiers.AddRange(trait.Party);
            foreach (var fork in trait.Forks)
            {
                foreach (var choice in fork.Choices)
                {
                    if (_forkPicks.Contains(choice.Id))
                        modifiers.AddRange(choice.Modifiers);
                }
            }
        }

        private SubclassResult CheckSubclass(AscendancyCatalog catalog, string heroClass, string subclassId)
        {
            if (!Unlocked(catalog))
                return SubclassResult.Locked;
            if (!catalog.TryGet(subclassId, out var subclass))
                return SubclassResult.UnknownSubclass;
            if (!string.Equals(subclass.HeroClass, heroClass, StringComparison.Ordinal))
                return SubclassResult.WrongClass;
            return SubclassResult.Chosen;
        }

        private bool TouchesAllocated(SubclassDef subclass, TreeNodeDef node)
        {
            foreach (var neighbour in node.Neighbours)
            {
                if (ReferenceEquals(neighbour, subclass.Start) || _allocated.Contains(neighbour.Id))
                    return true;
            }
            return false;
        }
    }
}
