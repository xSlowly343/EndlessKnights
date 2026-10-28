using System;
using System.Collections.Generic;
using System.Threading;
using Game.Core.Diagnostics;
using Game.Core.Telemetry;

namespace Game.Core.Intake
{
    public sealed class IntakeSink : ITelemetrySink, IErrorReportSink
    {
        private readonly IIntakePost _post;
        private readonly string _url;
        private readonly Action<Action> _run;
        private readonly HashSet<string> _finished = new HashSet<string>(StringComparer.Ordinal);
        private readonly object _lock = new object();
        private string? _inFlight;

        public IntakeSink(IIntakePost post, string url, Action<Action>? run = null)
        {
            _post = post;
            _url = url;
            _run = run ?? (work => ThreadPool.QueueUserWorkItem(_ => work()));
        }

        public int Delivered { get; private set; }
        public int Rejected { get; private set; }
        public int Failed { get; private set; }

        public bool TrySend(string json)
        {
            lock (_lock)
            {
                if (_finished.Remove(json))
                    return true;
                if (_inFlight != null)
                    return false;
                _inFlight = json;
            }
            _run(() => Send(json));
            lock (_lock)
                return _finished.Remove(json);
        }

        private void Send(string json)
        {
            IntakeOutcome outcome;
            try
            {
                outcome = _post.Post(_url, json);
            }
            catch (Exception)
            {
                outcome = IntakeOutcome.Failed;
            }
            lock (_lock)
            {
                _inFlight = null;
                switch (outcome)
                {
                    case IntakeOutcome.Delivered:
                        Delivered++;
                        _finished.Add(json);
                        break;
                    case IntakeOutcome.Rejected:
                        Rejected++;
                        _finished.Add(json);
                        break;
                    default:
                        Failed++;
                        break;
                }
            }
        }
    }
}
