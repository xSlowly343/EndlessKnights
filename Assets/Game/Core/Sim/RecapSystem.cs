using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum RecapKind
    {
        None,
        Day,
        Week
    }

    public sealed class RecapSystem
    {
        public const int DaysInWeek = 7;

        private readonly GameState _state;

        public RecapSystem(GameState state)
        {
            _state = state;
        }

        public RecapState State => _state.Recap;

        public void SetDay(long day)
        {
            var recap = State;
            if (recap.Day < 0)
            {
                recap.Day = day;
                Capture(recap.Start);
                return;
            }
            if (day <= recap.Day)
                return;
            var closed = Today;
            if (!closed.Empty)
            {
                recap.Days.Add(closed);
                while (recap.Days.Count > RecapState.DaysKept)
                    recap.Days.RemoveAt(0);
            }
            recap.Day = day;
            Capture(recap.Start);
        }

        public RecapDay Today
        {
            get
            {
                var start = State.Start;
                var now = new RecapDay();
                Capture(now);
                return new RecapDay
                {
                    Day = State.Day,
                    Keys = System.Math.Max(0, now.Keys - start.Keys),
                    Clicks = System.Math.Max(0, now.Clicks - start.Clicks),
                    MouseMeters = System.Math.Max(0, now.MouseMeters - start.MouseMeters),
                    PlaySeconds = System.Math.Max(0, now.PlaySeconds - start.PlaySeconds),
                    Kills = System.Math.Max(0, now.Kills - start.Kills),
                    Bosses = System.Math.Max(0, now.Bosses - start.Bosses),
                    Stages = System.Math.Max(0, now.RecordStage - start.RecordStage),
                    RecordStage = now.RecordStage,
                    Prestiges = System.Math.Max(0, now.Prestiges - start.Prestiges),
                    Levels = System.Math.Max(0, now.Levels - start.Levels)
                };
            }
        }

        public RecapDay? LastDay => State.Days.Count > 0 ? State.Days[State.Days.Count - 1] : null;

        public RecapDay Week(long week, out int daysPlayed)
        {
            var total = new RecapDay { Day = week * DaysInWeek - 3 };
            daysPlayed = 0;
            foreach (var day in State.Days)
            {
                if (ChronicleCatalog.WeekOf(day.Day) != week)
                    continue;
                total.Add(day);
                daysPlayed++;
            }
            if (State.Day >= 0 && ChronicleCatalog.WeekOf(State.Day) == week)
            {
                var today = Today;
                if (!today.Empty)
                {
                    total.Add(today);
                    daysPlayed++;
                }
            }
            return total;
        }

        public RecapKind Due
        {
            get
            {
                var last = LastDay;
                if (last == null)
                    return RecapKind.None;
                var lastWeek = ChronicleCatalog.WeekOf(last.Day);
                if (ChronicleCatalog.WeekOf(State.Day) > lastWeek && State.ShownWeek < lastWeek)
                    return RecapKind.Week;
                return State.ShownDay < last.Day ? RecapKind.Day : RecapKind.None;
            }
        }

        public void MarkShown(RecapKind kind)
        {
            var last = LastDay;
            if (last == null || kind == RecapKind.None)
                return;
            State.ShownDay = last.Day;
            if (kind == RecapKind.Week)
                State.ShownWeek = ChronicleCatalog.WeekOf(last.Day);
        }

        private void Capture(RecapDay into)
        {
            var input = _state.TotalInput;
            into.Keys = input.Keys;
            into.Clicks = input.Clicks;
            into.MouseMeters = input.MouseMeters;
            into.PlaySeconds = input.PlaySeconds;
            into.Kills = _state.Kills;
            into.Bosses = _state.BossKills;
            into.RecordStage = _state.RecordStage;
            into.Prestiges = _state.Prestiges;
            into.Levels = _state.HeroLevel;
        }
    }
}
