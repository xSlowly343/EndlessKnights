using System;
using System.Collections.Generic;
using Game.Core.Content.Tree;
using Game.Core.Stats;

namespace Game.Core.Build
{
    public sealed class AscendancyState
    {
        private const int ActivityCapacity = 32;

        private readonly HashSet<string> _activities = new HashSet<string>(ActivityCapacity, StringComparer.Ordinal);
        private readonly HashSet<string> _forkPicks = new HashSet<string>(StringComparer.Ordinal);

        public string? Subclass { get; private set; }
        public long LastFreeChangeUnixMs { get; set; }
        public IReadOnlyCollection<string> CompletedActivities => _activities;
        public IReadOnlyCollection<string> ForkPicks => _forkPicks;

        public TraitDef? Trait(AscendancyCatalog catalog) =>
            Subclass != null && catalog.TryGet(Subclass, out var subclass) ? subclass.Trait : null;

        public int TraitLevel(AscendancyCatalog catalog) =>
            Trait(catalog) == null ? 0 : System.Math.Min(TraitDef.MaxLevel, PointsEarned(catalog));

        public bool Picked(string optionId) => _forkPicks.Contains(optionId);

        private bool PickedAny(TraitFork fork)
        {
            foreach (var choice in fork.Choices)
            {
                if (_forkPicks.Contains(choice.Id))
                    return true;
            }
            return false;
        }

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

        public bool Unlocked(AscendancyCatalog catalog) => PointsEarned(catalog) > 0;

        public int ChoicesWaiting(AscendancyCatalog catalog, int heroLevel)
        {
            if (!Unlocked(catalog))
                return 0;
            var trait = Trait(catalog);
            if (Subclass == null || trait == null)
                return Subclass == null ? 1 : 0;
            var waiting = 0;
            foreach (var fork in trait.Forks)
            {
                if (heroLevel >= fork.HeroLevel && !PickedAny(fork))
                    waiting++;
            }
            return waiting;
        }

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
            _forkPicks.Clear();
        }

        public IReadOnlyList<Modifier> Modifiers(AscendancyCatalog catalog)
        {
            var modifiers = new List<Modifier>();
            if (Subclass == null || !catalog.TryGet(Subclass, out var subclass))
                return modifiers;
            if (subclass.Trait is { } trait)
                AddTrait(trait, TraitLevel(catalog), catalog.ForkScaleAt(TraitLevel(catalog)), modifiers);
            return modifiers;
        }

        public void Restore(string? subclass, IEnumerable<string> activities, IEnumerable<string>? forkPicks = null)
        {
            Subclass = subclass;
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
            _forkPicks.Clear();
            return SubclassResult.Chosen;
        }

        private void AddTrait(TraitDef trait, int level, double forkScale, List<Modifier> modifiers)
        {
            for (var i = 0; i < level; i++)
                modifiers.AddRange(trait.Levels[i]);
            modifiers.AddRange(trait.Party);
            foreach (var fork in trait.Forks)
            {
                foreach (var choice in fork.Choices)
                {
                    if (_forkPicks.Contains(choice.Id))
                        AddScaled(choice.Modifiers, forkScale, modifiers);
                }
            }
        }

        private static void AddScaled(IReadOnlyList<Modifier> source, double scale, List<Modifier> modifiers)
        {
            foreach (var modifier in source)
                modifiers.Add(GrowsWithLevel(modifier) ? modifier.WithValue(modifier.Value * scale) : modifier);
        }

        public static bool GrowsWithLevel(Modifier modifier)
        {
            if (!(modifier.Value > 0))
                return false;
            if (Game.Core.Party.CoopLine.IsCoop(modifier.Stat))
                return true;
            return modifier.Kind == ModifierKind.Increased || modifier.Kind == ModifierKind.More;
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
    }
}
