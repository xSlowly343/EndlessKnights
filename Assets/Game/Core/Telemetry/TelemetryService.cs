using System;
using System.Collections.Generic;
using System.IO;
using Game.Core.Input;

namespace Game.Core.Telemetry
{
    public sealed class TelemetryService
    {
        private readonly TelemetryStore _store;
        private readonly ITelemetrySink _sink;
        private readonly Func<string> _newInstallId;
        private readonly string _gameVersion;
        private TelemetryCounters _sessionStart;
        private long _sessionDay;

        public TelemetryService(TelemetryStore store, ITelemetrySink sink, string gameVersion, Func<string>? newInstallId = null)
        {
            _store = store;
            _sink = sink;
            _gameVersion = gameVersion;
            _newInstallId = newInstallId ?? NewInstallId;
        }

        public event Action<string>? Problem;

        public TelemetryState? State { get; private set; }
        public bool Enabled => State != null;

        public static string NewInstallId() => Guid.NewGuid().ToString("N");

        public void UseConsent(bool given, long localDay, InputStats session, in TelemetryProgress progress)
        {
            if (!given)
            {
                State = null;
                Guard(_store.Forget);
                return;
            }
            if (Enabled)
                return;
            State = LoadOrCreate(localDay);
            StartSession(localDay, session, progress);
        }

        public void Observe(long localDay, InputStats session, in TelemetryProgress progress)
        {
            if (State == null)
                return;
            if (localDay != _sessionDay)
            {
                EndSession(session, progress);
                StartSession(localDay, session, progress);
                return;
            }
            State.ObservePrestiges(progress.Prestiges, progress.TotalPlaySeconds);
            State.OpenSession = Packet(session, progress, false);
            Save();
        }

        public TelemetryPacket? EndSession(InputStats session, in TelemetryProgress progress)
        {
            if (State == null)
                return null;
            State.ObservePrestiges(progress.Prestiges, progress.TotalPlaySeconds);
            var packet = Packet(session, progress, true);
            State.OpenSession = null;
            Guard(() => _store.Enqueue(packet));
            Save();
            return packet;
        }

        public int Flush()
        {
            if (State == null)
                return 0;
            var sent = 0;
            Guard(() => sent = _store.Flush(_sink));
            return sent;
        }

        private TelemetryState LoadOrCreate(long localDay)
        {
            var problems = new List<string>();
            TelemetryState? loaded = null;
            Guard(() => loaded = _store.LoadState(problems));
            foreach (var problem in problems)
                Problem?.Invoke(problem);
            return loaded ?? new TelemetryState(_newInstallId(), localDay);
        }

        private void StartSession(long localDay, InputStats session, in TelemetryProgress progress)
        {
            var state = State!;
            if (state.OpenSession != null)
            {
                var unfinished = state.OpenSession;
                Guard(() => _store.Enqueue(unfinished));
                state.OpenSession = null;
            }
            state.Sessions++;
            state.MarkDay(localDay);
            state.ObservePrestiges(progress.Prestiges, progress.TotalPlaySeconds);
            _sessionStart = TelemetryCounters.Of(session);
            _sessionDay = localDay;
            Save();
        }

        private TelemetryPacket Packet(InputStats session, in TelemetryProgress progress, bool closed)
        {
            var state = State!;
            var counters = TelemetryCounters.Of(session).Since(_sessionStart);
            return new TelemetryPacket
            {
                InstallId = state.InstallId,
                GameVersion = _gameVersion,
                Session = state.Sessions,
                Closed = closed,
                SessionSeconds = (long)System.Math.Round(counters.Seconds),
                KeysPerMinute = System.Math.Round(counters.PerMinute(counters.Keys), 1),
                ClicksPerMinute = System.Math.Round(counters.PerMinute(counters.Clicks), 1),
                MouseMeters = System.Math.Round(counters.MouseMeters, 1),
                TotalPlaySeconds = (long)System.Math.Round(progress.TotalPlaySeconds),
                FirstPrestigeSeconds = state.FirstPrestigeSeconds,
                Prestiges = progress.Prestiges,
                RecordStage = progress.RecordStage,
                Character = progress.Character,
                DaysSinceFirst = state.DayOffset(_sessionDay),
                ReturnDays = new List<int>(state.ReturnDays)
            };
        }

        private void Save()
        {
            var state = State;
            if (state != null)
                Guard(() => _store.SaveState(state));
        }

        private void Guard(Action action)
        {
            try
            {
                action();
            }
            catch (IOException error)
            {
                Problem?.Invoke(error.Message);
            }
            catch (UnauthorizedAccessException error)
            {
                Problem?.Invoke(error.Message);
            }
        }
    }
}
