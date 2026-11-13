using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim;

namespace Game.Core.Party
{
    public sealed class PartyColossusMeter
    {
        private PartyColossusRules _rules;
        private BigNumber _lastTotal;
        private bool _started;

        public PartyColossusMeter(PartyColossusRules rules)
        {
            _rules = rules;
        }

        public double Direct { get; private set; }
        public double Background { get; private set; }
        public double Offline { get; private set; }
        public double OfflineSeconds { get; private set; }
        public double Pending => Direct + Background;

        public void UseRules(PartyColossusRules rules) => _rules = rules;

        public void Note(BigNumber totalDamage, StageTable stages, long recordStage, bool atColossus, bool active, bool counts, double power)
        {
            var delta = _started ? totalDamage - _lastTotal : BigNumber.Zero;
            _lastTotal = totalDamage;
            _started = true;
            if (!counts || !active || delta.Sign <= 0)
                return;
            var points = Points(delta, stages, recordStage) * System.Math.Max(0, power);
            if (atColossus)
                Direct += points;
            else
                Background += points * _rules.Background;
        }

        public void NoteOffline(OfflineResult result, StageTable stages, long recordStage, bool counts, double power)
        {
            if (!counts || result.Kills <= 0 || result.Seconds <= 0)
                return;
            var damage = stages.NormalHp(System.Math.Max(1, result.StageAfter)) * (double)result.Kills;
            Offline += Points(damage, stages, recordStage) * _rules.Offline * System.Math.Max(0, power);
            OfflineSeconds += result.Seconds;
        }

        public double TakePending()
        {
            var points = Pending;
            Direct = 0;
            Background = 0;
            return points;
        }

        public (double Points, double Seconds) TakeOffline()
        {
            var taken = (Offline, OfflineSeconds);
            Offline = 0;
            OfflineSeconds = 0;
            return taken;
        }

        private double Points(BigNumber damage, StageTable stages, long recordStage)
        {
            var boss = stages.BossHp(System.Math.Max(1, recordStage));
            return boss.Sign <= 0 ? 0 : (damage / boss).ToDouble() * _rules.PointsPerBoss;
        }
    }
}
