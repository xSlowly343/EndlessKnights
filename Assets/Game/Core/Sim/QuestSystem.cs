using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Math;

namespace Game.Core.Sim
{
    public enum QuestResult
    {
        Done,
        NoSuchQuest,
        NotFinished,
        AlreadyClaimed,
        NoSwapsLeft
    }

    public sealed class QuestSystem
    {
        public const int DaysPerWeek = 7;
        private const ulong DailySalt = 0x9E57D41L;
        private const ulong WeeklySalt = 0x7EE41D5L;

        private readonly GameState _state;
        private readonly EventQueue _events;
        private readonly List<QuestKind> _pool = new List<QuestKind>(16);
        private QuestRules _rules;

        public QuestSystem(QuestRules rules, GameState state, EventQueue events)
        {
            _rules = rules;
            _state = state;
            _events = events;
        }

        public QuestRules Rules => _rules;
        public QuestState State => _state.Quests;
        public double StreakBonus => System.Math.Min(State.Streak, _rules.StreakMaxDays) * _rules.StreakBonusPerDay;

        public void UseRules(QuestRules rules) => _rules = rules;

        public void SetDay(long day)
        {
            var quests = State;
            if (quests.Day < 0)
            {
                quests.Day = day;
                Issue(day);
            }
            else if (day > quests.Day)
            {
                for (var passed = quests.Day; passed < day; passed++)
                    CloseDay(passed);
                var first = System.Math.Max(quests.Day + 1, day - _rules.KeepDays + 1);
                quests.Daily.RemoveAll(entry => entry.Day <= day - _rules.KeepDays);
                for (var next = first; next <= day; next++)
                    Issue(next);
                quests.Day = day;
            }
            var week = day / DaysPerWeek;
            if (week != quests.Week)
            {
                quests.Week = week;
                quests.Weekly.Clear();
                Pick(WeeklySalt, (ulong)week, _rules.PerWeek, false, quests.Weekly, week * DaysPerWeek);
            }
        }

        public void Tick(int firstEvent)
        {
            var quests = State;
            if (quests.Day < 0)
                return;
            if (!quests.Primed)
            {
                for (var kind = 0; kind < QuestRules.KindCount; kind++)
                    quests.Last[kind] = Counter((QuestKind)kind);
                quests.Primed = true;
                return;
            }
            for (var kind = 0; kind < QuestRules.KindCount; kind++)
            {
                var questKind = (QuestKind)kind;
                if (questKind == QuestKind.Guardians || questKind == QuestKind.PartyBosses)
                    continue;
                var now = Counter(questKind);
                var delta = now - quests.Last[kind];
                quests.Last[kind] = now;
                if (delta > 0)
                    Add(questKind, delta);
            }
            CountEvents(firstEvent);
        }

        public QuestResult Claim(bool weekly, int index)
        {
            var list = weekly ? State.Weekly : State.Daily;
            if (index < 0 || index >= list.Count)
                return QuestResult.NoSuchQuest;
            var entry = list[index];
            if (entry.Claimed)
                return QuestResult.AlreadyClaimed;
            if (!entry.Done)
                return QuestResult.NotFinished;
            entry.Claimed = true;
            var reward = RewardOf(weekly, entry.Kind);
            Grant(reward, weekly ? 0 : StreakBonus);
            if (reward.Friendship > 0)
                State.PartyClaimBosses = System.Math.Max(State.PartyClaimBosses, (int)System.Math.Ceiling(entry.Target));
            return QuestResult.Done;
        }

        public QuestReward RewardOf(bool weekly, QuestKind kind) =>
            weekly ? _rules.WeeklyReward : _rules.Rule(kind).Party ? _rules.PartyReward : _rules.DailyReward;

        public void PartyClaimSent() => State.PartyClaimBosses = 0;

        public QuestResult Swap(int index)
        {
            var quests = State;
            if (index < 0 || index >= quests.Daily.Count)
                return QuestResult.NoSuchQuest;
            var entry = quests.Daily[index];
            if (entry.Claimed || entry.Done)
                return QuestResult.AlreadyClaimed;
            if (quests.SwapsLeft <= 0)
                return QuestResult.NoSwapsLeft;
            Pool(true);
            foreach (var other in quests.Daily)
            {
                if (other.Day == entry.Day)
                    _pool.Remove(other.Kind);
            }
            if (_pool.Count == 0)
                return QuestResult.NoSuchQuest;
            quests.SwapsLeft--;
            quests.Swaps++;
            var kind = _pool[(int)(SplitMix.At(DailySalt ^ (ulong)entry.Day, 1000UL + (ulong)quests.Swaps) % (ulong)_pool.Count)];
            entry.Kind = kind;
            entry.Target = _rules.Target(kind, true, _state.RecordStage);
            entry.Progress = 0;
            return QuestResult.Done;
        }

        private void CloseDay(long day)
        {
            var quests = State;
            var issued = 0;
            var done = 0;
            foreach (var entry in quests.Daily)
            {
                if (entry.Day != day)
                    continue;
                issued++;
                if (entry.Done)
                    done++;
            }
            if (issued > 0 && done == issued)
                quests.Streak = System.Math.Min(_rules.StreakMaxDays, quests.Streak + 1);
            else
                quests.Streak = (int)System.Math.Floor(quests.Streak * _rules.StreakMissKeep);
        }

        private void Issue(long day)
        {
            Pick(DailySalt, (ulong)day, _rules.PerDay, true, State.Daily, day);
            State.SwapsLeft = _rules.SwapsPerDay;
        }

        private void Pick(ulong salt, ulong period, int count, bool daily, List<QuestEntry> into, long day)
        {
            Pool(daily);
            for (var i = 0; i < count && _pool.Count > 0; i++)
            {
                var at = (int)(SplitMix.At(salt ^ period, (ulong)i) % (ulong)_pool.Count);
                var kind = _pool[at];
                _pool.RemoveAt(at);
                into.Add(new QuestEntry { Kind = kind, Day = day, Target = _rules.Target(kind, daily, _state.RecordStage) });
            }
        }

        private void Pool(bool daily)
        {
            _pool.Clear();
            foreach (var rule in _rules.Kinds)
            {
                if ((daily ? rule.Daily : rule.Weekly) > 0 && rule.FromStage <= _state.RecordStage)
                    _pool.Add(rule.Kind);
            }
        }

        private void Add(QuestKind kind, double amount)
        {
            AddTo(State.Daily, kind, amount);
            AddTo(State.Weekly, kind, amount);
        }

        private static void AddTo(List<QuestEntry> list, QuestKind kind, double amount)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                if (entry.Kind == kind && !entry.Claimed && !entry.Done)
                    entry.Progress = System.Math.Min(entry.Target, entry.Progress + amount);
            }
        }

        private void CountEvents(int firstEvent)
        {
            for (var i = firstEvent; i < _events.Count; i++)
            {
                var gameEvent = _events[i];
                if (gameEvent.Kind == GameEventKind.EnemyKilled && gameEvent.Enemy?.Type == EnemyType.Guardian)
                    Add(QuestKind.Guardians, 1);
                if (gameEvent.Kind == GameEventKind.BossDefeated && _state.PartyAllies > 0)
                    Add(QuestKind.PartyBosses, 1);
            }
        }

        private double Counter(QuestKind kind) => kind switch
        {
            QuestKind.Kills => _state.Kills,
            QuestKind.Bosses => _state.BossKills,
            QuestKind.Keys => _state.TotalInput.Keys,
            QuestKind.Clicks => _state.TotalInput.Clicks,
            QuestKind.MouseMeters => _state.TotalInput.MouseMeters,
            QuestKind.SkillCasts => _state.SkillCasts,
            QuestKind.RecordStages => _state.RecordStage,
            QuestKind.Prestiges => _state.Prestiges,
            QuestKind.Salvage => _state.Drops.SalvagedItems,
            QuestKind.ActiveMinutes => _state.TotalInput.PlaySeconds / 60,
            _ => 0
        };

        private void Grant(QuestReward reward, double bonus)
        {
            var times = 1 + bonus;
            _state.Drops.Shards += (long)System.Math.Round(reward.Shards * times);
            _state.Drops.Dust += reward.Dust * times;
            _state.Drops.Ingots += (long)System.Math.Round(reward.Ingots * times);
            _state.SeasonPass.UnsentPoints += reward.PassPoints;
        }
    }
}
