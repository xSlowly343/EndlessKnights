using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim;

namespace Game.Core.Party
{
    public sealed class PartyColossusMeter
    {
        private readonly double[] _direct = new double[PartyTrialFight.Kinds];
        private readonly double[] _background = new double[PartyTrialFight.Kinds];
        private PartyColossusRules _rules;

        public PartyColossusMeter(PartyColossusRules rules)
        {
            _rules = rules;
        }

        public double Offline { get; private set; }
        public double OfflineSeconds { get; private set; }
        public double Direct => Sum(_direct);
        public double Background => Sum(_background);
        public double Pending => Direct + Background;

        public double DirectOf(PartyTrialDamage kind) => _direct[(int)kind];

        public double BackgroundOf(PartyTrialDamage kind) => _background[(int)kind];

        public void UseRules(PartyColossusRules rules) => _rules = rules;

        public void Note(CombatSystem combat, StageTable stages, long recordStage, bool atColossus, bool active, bool counts, double power)
        {
            if (!counts || !active)
                return;
            var boss = stages.BossHp(System.Math.Max(1, recordStage));
            if (boss.Sign <= 0)
                return;
            var scale = _rules.PointsPerBoss * System.Math.Max(0, power) * (atColossus ? 1 : _rules.Background);
            var target = atColossus ? _direct : _background;
            for (var kind = 0; kind < PartyTrialFight.Kinds; kind++)
            {
                var damage = combat.TickDamage((PartyTrialDamage)kind);
                if (damage.Sign > 0)
                    target[kind] += (damage / boss).ToDouble() * scale;
            }
        }

        public void NoteOffline(OfflineResult result, StageTable stages, long recordStage, bool counts, double power)
        {
            if (!counts || result.Kills <= 0 || result.Seconds <= 0)
                return;
            var boss = stages.BossHp(System.Math.Max(1, recordStage));
            if (boss.Sign <= 0)
                return;
            var damage = stages.NormalHp(System.Math.Max(1, result.StageAfter)) * (double)result.Kills;
            Offline += (damage / boss).ToDouble() * _rules.PointsPerBoss * _rules.Offline * System.Math.Max(0, power);
            OfflineSeconds += result.Seconds;
        }

        public void TakePending(double[] direct, double[] background)
        {
            for (var kind = 0; kind < PartyTrialFight.Kinds; kind++)
            {
                direct[kind] = _direct[kind];
                background[kind] = _background[kind];
            }
            System.Array.Clear(_direct, 0, _direct.Length);
            System.Array.Clear(_background, 0, _background.Length);
        }

        public void Return(double[] direct, double[] background)
        {
            for (var kind = 0; kind < PartyTrialFight.Kinds; kind++)
            {
                _direct[kind] += direct[kind];
                _background[kind] += background[kind];
            }
        }

        public (double Points, double Seconds) TakeOffline()
        {
            var taken = (Offline, OfflineSeconds);
            Offline = 0;
            OfflineSeconds = 0;
            return taken;
        }

        private static double Sum(double[] values) => values[0] + values[1] + values[2] + values[3];
    }
}
