using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum TreeResult
    {
        Done,
        UnknownNode,
        NoPoints,
        RowLocked,
        NeedsParent,
        MaxRank,
        NotLearned,
        Holds,
        NoSouls,
        TooSoon,
        UnknownMajor,
        BarTooShort
    }

    public sealed class MenagerieTreeSystem
    {
        private readonly CompanionSystem _companions;
        private readonly double[] _stats = new double[(int)MenagerieStat.Count];
        private readonly double[] _perks = new double[MuseumRules.PerkCount];

        public MenagerieTreeSystem(CompanionSystem companions)
        {
            _companions = companions;
            companions.Tree = this;
            Changed();
        }

        public MenagerieTreeRules Rules => _companions.Rules.Menagerie;
        private CompanionState State => _companions.State;

        public double Stat(MenagerieStat stat) => _stats[(int)stat];

        public double Perk(MuseumPerk perk) => _perks[(int)perk];

        public int FamiliesCollected { get; private set; }
        public int FamiliesAdult { get; private set; }

        public bool Collected(CompanionFamily family) => FamilyHas(family, false);

        public bool AllAdults(CompanionFamily family) => FamilyHas(family, true);

        private bool FamilyHas(CompanionFamily family, bool adults)
        {
            var rules = _companions.Rules;
            var paid = rules.Album.PaidSources;
            var any = false;
            for (var i = 0; i < family.Members.Count; i++)
            {
                var def = rules.Companion(family.Members[i]);
                if (def == null || Contains(paid, def.Source))
                    continue;
                any = true;
                if (!_companions.Known(def.Id))
                    return false;
                if (adults && _companions.StageOf(def.Id) < rules.Families.AdultStage)
                    return false;
            }
            return any;
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                    return true;
            }
            return false;
        }

        public int Level => State.MenagerieLevel;
        public double Experience => State.MenagerieExperience;

        public int RankOf(int number) => State.TreeRanks.TryGetValue(number, out var rank) ? rank : 0;

        public int Spent
        {
            get
            {
                var spent = 0;
                foreach (var rank in State.TreeRanks.Values)
                    spent += rank;
                return spent;
            }
        }

        public int Evolutions
        {
            get
            {
                var evolutions = 0;
                foreach (var progress in State.Progress.Values)
                    evolutions += progress.Stage;
                return evolutions;
            }
        }

        public int ExtraPoints { get; set; }

        public int Points
        {
            get
            {
                var rules = Rules;
                var points = State.MenagerieLevel + System.Math.Min(Evolutions, rules.EvolutionsCounted) * rules.EvolutionPoints + ExtraPoints
                    + FamiliesCollected * _companions.Rules.Families.TreePoints;
                if (rules.AlbumStars > 0 && _companions.AlbumStars() >= rules.AlbumStars)
                    points += rules.AlbumPoints;
                return System.Math.Min(rules.MaxPoints, points);
            }
        }

        public int Free => Points - Spent;

        public void AddExperience(double companionExperience)
        {
            var rules = Rules;
            if (companionExperience <= 0 || State.MenagerieLevel >= rules.MaxLevel)
                return;
            State.MenagerieExperience += companionExperience * rules.ExperienceShare;
            while (State.MenagerieLevel < rules.MaxLevel && State.MenagerieExperience >= rules.ExperienceFor(State.MenagerieLevel + 1))
            {
                State.MenagerieExperience -= rules.ExperienceFor(State.MenagerieLevel + 1);
                State.MenagerieLevel++;
            }
            if (State.MenagerieLevel >= rules.MaxLevel)
                State.MenagerieExperience = 0;
        }

        public TreeResult CanLearn(int number)
        {
            var node = Rules.Node(number);
            if (node == null)
                return TreeResult.UnknownNode;
            if (RankOf(number) >= MenagerieTreeRules.MaxRank)
                return TreeResult.MaxRank;
            if (Free <= 0)
                return TreeResult.NoPoints;
            if (PointsBelow(node.Row, 0) < Rules.Threshold(node.Row))
                return TreeResult.RowLocked;
            if (node.Parent > 0 && RankOf(node.Parent) < 1)
                return TreeResult.NeedsParent;
            return TreeResult.Done;
        }

        public TreeResult Learn(int number)
        {
            var result = CanLearn(number);
            if (result != TreeResult.Done)
                return result;
            State.TreeRanks[number] = RankOf(number) + 1;
            Changed();
            return TreeResult.Done;
        }

        public TreeResult Refund(int number)
        {
            var rank = RankOf(number);
            if (Rules.Node(number) == null)
                return TreeResult.UnknownNode;
            if (rank <= 0)
                return TreeResult.NotLearned;
            if (HoldsOthers(number, rank))
                return TreeResult.Holds;
            if (State.Souls < Rules.RefundSouls)
                return TreeResult.NoSouls;
            State.Souls -= Rules.RefundSouls;
            if (rank == 1)
                State.TreeRanks.Remove(number);
            else
                State.TreeRanks[number] = rank - 1;
            if (MajorsAbove(Spent))
                DropMajors(Spent);
            Changed();
            return TreeResult.Done;
        }

        public int PointsBelow(int row, int without)
        {
            var points = 0;
            foreach (var pair in State.TreeRanks)
            {
                var node = Rules.Node(pair.Key);
                if (node != null && node.Row < row)
                    points += pair.Key == without ? pair.Value - 1 : pair.Value;
            }
            return points;
        }

        private bool HoldsOthers(int number, int rank)
        {
            foreach (var pair in State.TreeRanks)
            {
                if (pair.Value <= 0 || pair.Key == number)
                    continue;
                var node = Rules.Node(pair.Key);
                if (node == null)
                    continue;
                if (rank == 1 && node.Parent == number)
                    return true;
                if (PointsBelow(node.Row, number) < Rules.Threshold(node.Row))
                    return true;
            }
            return false;
        }

        public bool FreeResetReady => State.Day < 0 || State.TreeResetDay < 0 || State.Day - State.TreeResetDay >= Rules.FreeResetDays;

        public TreeResult ResetAll()
        {
            if (!FreeResetReady)
                return TreeResult.TooSoon;
            State.TreeResetDay = State.Day;
            State.TreeRanks.Clear();
            State.TreeMajors.Clear();
            Changed();
            return TreeResult.Done;
        }

        public TreeResult ChooseMajor(int at, string option)
        {
            var majors = Rules.Majors;
            for (var i = 0; i < majors.Count; i++)
            {
                if (majors[i].At != at)
                    continue;
                if (Spent < at)
                    return TreeResult.BarTooShort;
                for (var j = 0; j < majors[i].Options.Count; j++)
                {
                    if (majors[i].Options[j].Id != option)
                        continue;
                    State.TreeMajors[at] = option;
                    Changed();
                    return TreeResult.Done;
                }
                return TreeResult.UnknownMajor;
            }
            return TreeResult.UnknownMajor;
        }

        public string MajorAt(int at) => State.TreeMajors.TryGetValue(at, out var option) ? option : string.Empty;

        public int BarPassives => Spent / Rules.BarEvery;

        private bool MajorsAbove(int spent)
        {
            foreach (var at in State.TreeMajors.Keys)
            {
                if (at > spent)
                    return true;
            }
            return false;
        }

        private void DropMajors(int spent)
        {
            var drop = new List<int>();
            foreach (var at in State.TreeMajors.Keys)
            {
                if (at > spent)
                    drop.Add(at);
            }
            for (var i = 0; i < drop.Count; i++)
                State.TreeMajors.Remove(drop[i]);
        }

        public void Changed() => _companions.ApplyPerks();

        public void Recalculate()
        {
            for (var i = 0; i < _stats.Length; i++)
                _stats[i] = 0;
            for (var i = 0; i < _perks.Length; i++)
                _perks[i] = 0;
            var families = _companions.Rules.Families.Families;
            FamiliesCollected = 0;
            FamiliesAdult = 0;
            for (var i = 0; i < families.Count; i++)
            {
                if (!Collected(families[i]))
                    continue;
                FamiliesCollected++;
                Add(families[i].Collected, 1);
                if (!AllAdults(families[i]))
                    continue;
                FamiliesAdult++;
                Add(families[i].Adults, 1);
            }
            var rules = Rules;
            foreach (var pair in State.TreeRanks)
            {
                var node = rules.Node(pair.Key);
                if (node == null || pair.Value <= 0)
                    continue;
                Add(node.A, node.AMultiplier(pair.Value));
                if (pair.Value >= 3)
                    Add(node.B, 1);
                if (pair.Value >= MenagerieTreeRules.MaxRank)
                    Add(node.C, 1);
            }
            foreach (var pair in State.TreeMajors)
            {
                var major = Major(pair.Key, pair.Value);
                if (major != null)
                    Add(major.Lines, 1);
            }
            var passives = rules.BarPassives;
            if (passives.Count > 0)
            {
                var count = BarPassives;
                for (var i = 0; i < count; i++)
                {
                    Add(passives[i % passives.Count], 1);
                }
            }
        }

        public MenagerieMajor? Major(int at, string option)
        {
            var majors = Rules.Majors;
            for (var i = 0; i < majors.Count; i++)
            {
                if (majors[i].At != at)
                    continue;
                for (var j = 0; j < majors[i].Options.Count; j++)
                {
                    if (majors[i].Options[j].Id == option)
                        return majors[i].Options[j];
                }
            }
            return null;
        }

        private void Add(IReadOnlyList<MenagerieLine> lines, double multiplier)
        {
            for (var i = 0; i < lines.Count; i++)
                Add(lines[i], multiplier);
        }

        private void Add(MenagerieLine line, double multiplier)
        {
            if (!line.Works)
                return;
            if (line.Stat == MenagerieStat.Perk)
                _perks[(int)line.Perk] += line.Value * multiplier;
            else
                _stats[(int)line.Stat] += line.Value * multiplier;
        }
    }
}
