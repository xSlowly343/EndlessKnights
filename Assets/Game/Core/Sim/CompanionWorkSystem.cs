using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum WorkResult
    {
        Done,
        UnknownTask,
        NotOnBoard,
        Finished,
        WrongKind,
        NoItems,
        NoRerolls,
        UnknownCompanion,
        Busy,
        UnknownGuild,
        TooSoon,
        BadParty
    }

    public sealed class BurrowRun
    {
        public int Floors { get; internal set; }
        public double Power { get; internal set; }
        public int Souls { get; internal set; }
        public long Shards { get; internal set; }
        public int Bones { get; internal set; }
    }

    public sealed class CompanionWorkSystem
    {
        private readonly GameState _state;
        private readonly CampSystem _camp;
        private readonly CompanionSystem _companions;
        private readonly ExpeditionSystem _expeditions;

        public CompanionWorkSystem(GameState state, CampSystem camp, CompanionSystem companions, ExpeditionSystem expeditions)
        {
            _state = state;
            _camp = camp;
            _companions = companions;
            _expeditions = expeditions;
            companions.Work = this;
        }

        private CompanionWorkRules Rules => _companions.Rules.Work;
        private CompanionState State => _companions.State;

        public IReadOnlyList<string> Board => State.Board;
        public bool Helping(string companion) => State.Helper.Length > 0 && State.Helper == companion;

        public void SetDay(long day)
        {
            if (State.BoardDay == day)
                return;
            State.BoardDay = day;
            State.Rerolls = 0;
            State.BoardDone.Clear();
            Fill(day, 0);
        }

        private void Fill(long day, int salt)
        {
            State.Board.Clear();
            var pool = Rules.Pool;
            var seed = (ulong)System.Math.Max(0, day) * 0x9E3779B97F4A7C15UL + (ulong)salt * 0xBF58476D1CE4E5B9UL;
            var guard = 0;
            while (State.Board.Count < Rules.BoardTasks && guard++ < 1000)
            {
                seed ^= seed >> 30;
                seed *= 0xBF58476D1CE4E5B9UL;
                seed ^= seed >> 27;
                var id = pool[(int)(seed % (ulong)pool.Count)].Id;
                if (!State.Board.Contains(id))
                    State.Board.Add(id);
            }
        }

        public WorkResult Reroll(int index)
        {
            if (index < 0 || index >= State.Board.Count)
                return WorkResult.NotOnBoard;
            if (State.Rerolls >= Rules.RerollsPerDay)
                return WorkResult.NoRerolls;
            if (State.BoardDone.Contains(State.Board[index]))
                return WorkResult.Finished;
            State.Rerolls++;
            var pool = Rules.Pool;
            for (var shift = 1; shift <= pool.Count; shift++)
            {
                var candidate = pool[(IndexOf(State.Board[index]) + shift * 7) % pool.Count].Id;
                if (!State.Board.Contains(candidate))
                {
                    State.Board[index] = candidate;
                    return WorkResult.Done;
                }
            }
            return WorkResult.Done;
        }

        private int IndexOf(string id)
        {
            for (var i = 0; i < Rules.Pool.Count; i++)
            {
                if (Rules.Pool[i].Id == id)
                    return i;
            }
            return 0;
        }

        public WorkResult HandIn(string taskId, string companion)
        {
            var task = Rules.Task(taskId);
            if (task == null)
                return WorkResult.UnknownTask;
            if (!State.Board.Contains(taskId))
                return WorkResult.NotOnBoard;
            if (State.BoardDone.Contains(taskId))
                return WorkResult.Finished;
            if (task.Kind != BoardTaskKind.Bring)
                return WorkResult.WrongKind;
            if (!_companions.Known(companion))
                return WorkResult.UnknownCompanion;
            if (!_camp.Take(task.Item, task.Amount))
                return WorkResult.NoItems;
            Reward(task, companion);
            return WorkResult.Done;
        }

        public void NoteTrip(Expedition expedition)
        {
            for (var i = 0; i < State.Board.Count; i++)
            {
                var task = Rules.Task(State.Board[i]);
                if (task == null || task.Kind != BoardTaskKind.Trip || State.BoardDone.Contains(task.Id) || task.Biome != expedition.Biome)
                    continue;
                if (task.Trait.Length > 0 && !HasTrait(expedition.Companion, task.Trait) && (expedition.Partner.Length == 0 || !HasTrait(expedition.Partner, task.Trait)))
                    continue;
                Reward(task, expedition.Companion);
            }
        }

        private bool HasTrait(string companion, string trait)
        {
            if (_companions.BaseTrait(companion) == trait)
                return true;
            return _companions.State.Progress.TryGetValue(companion, out var progress) && progress.Traits.Contains(trait);
        }

        private void Reward(BoardTask task, string companion)
        {
            State.BoardDone.Add(task.Id);
            _companions.Gain(companion, task.Experience);
            _companions.AddSouls(task.Souls);
            if (task.Food.Length > 0 && task.FoodAmount > 0)
                _camp.Add(task.Food, task.FoodAmount);
        }

        public WorkResult Help(string companion, string guild)
        {
            if (!_companions.Known(companion))
                return WorkResult.UnknownCompanion;
            if (State.Helper.Length > 0 || _expeditions.Busy(companion) || _companions.AtHome(companion))
                return WorkResult.Busy;
            var guilds = Rules.HelpGuilds;
            var known = false;
            for (var i = 0; i < guilds.Count; i++)
                known |= guilds[i] == guild;
            if (!known)
                return WorkResult.UnknownGuild;
            State.Helper = companion;
            State.HelpGuild = guild;
            State.HelpSeconds = Rules.HelpSeconds * System.Math.Max(0.1, 1 - _companions.TreeStat(MenagerieStat.HelpTime));
            return WorkResult.Done;
        }

        public void Advance(double seconds)
        {
            if (State.Helper.Length == 0 || seconds <= 0)
                return;
            State.HelpSeconds -= seconds;
            if (State.HelpSeconds > 0)
                return;
            _companions.AddBond(State.Helper, Rules.HelpBond);
            var reputation = _state.Town.Reputation;
            reputation[State.HelpGuild] = (reputation.TryGetValue(State.HelpGuild, out var points) ? points : 0) + Rules.HelpReputation;
            State.Helper = string.Empty;
            State.HelpGuild = string.Empty;
            State.HelpSeconds = 0;
        }

        public long Week => System.Math.Max(0, State.Day) / Rules.BurrowDays;

        public bool BurrowReady => State.BurrowWeek != Week;

        public double Power(string companion)
        {
            var rules = Rules;
            var power = _companions.LevelOf(companion) + _companions.StageOf(companion) * rules.PowerPerStage + _companions.StarsOf(companion) * rules.PowerPerStar;
            var traits = _companions.TraitsOf(companion);
            for (var i = 0; i < traits.Count; i++)
                power += _companions.RankOf(companion, traits[i]) * rules.PowerPerRank;
            return power;
        }

        public WorkResult Burrow(IReadOnlyList<string> party, out BurrowRun run)
        {
            run = new BurrowRun();
            if (!BurrowReady)
                return WorkResult.TooSoon;
            if (party.Count == 0 || party.Count > Rules.BurrowParty)
                return WorkResult.BadParty;
            for (var i = 0; i < party.Count; i++)
            {
                if (!_companions.Known(party[i]))
                    return WorkResult.UnknownCompanion;
                if (_expeditions.Busy(party[i]) || Helping(party[i]))
                    return WorkResult.Busy;
                for (var j = 0; j < i; j++)
                {
                    if (party[j] == party[i])
                        return WorkResult.BadParty;
                }
                run.Power += Power(party[i]);
            }
            State.BurrowWeek = Week;
            var floors = Rules.Floors;
            for (var i = 0; i < floors.Count && run.Power >= floors[i].Power; i++)
            {
                run.Floors++;
                run.Souls += floors[i].Souls;
                run.Shards += floors[i].Shards;
                run.Bones += floors[i].Bones;
            }
            var burrow = 1 + _companions.TreeStat(MenagerieStat.BurrowLoot);
            run.Souls = (int)System.Math.Floor(run.Souls * burrow + 1e-9);
            run.Shards = (long)System.Math.Floor(run.Shards * burrow + 1e-9);
            _companions.AddSouls(run.Souls);
            _state.Drops.Shards += run.Shards;
            if (run.Bones > 0)
            {
                var place = _companions.Rules.BiomeOf(_companions.Rules.Companion(party[0])?.Biome ?? string.Empty);
                if (place != null)
                    _camp.Add(place.Bone, run.Bones);
            }
            return WorkResult.Done;
        }
    }
}
