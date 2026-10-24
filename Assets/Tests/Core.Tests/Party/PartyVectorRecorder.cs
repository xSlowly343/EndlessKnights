using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Game.Core.Content;
using Game.Core.Party;

namespace Game.Core.Tests.Party
{
    public sealed class PartyVectorRecorder
    {
        private readonly PartyEncounter _encounter;
        private readonly StringBuilder _steps = new StringBuilder();
        private readonly string _name;
        private readonly ulong _seed;
        private int _count;

        public PartyVectorRecorder(string name, PartyRules rules, StageTable stages, ulong seed)
        {
            _name = name;
            _seed = seed;
            _encounter = new PartyEncounter(rules, stages, seed);
        }

        public PartyEncounter Encounter => _encounter;

        public int Join(ulong player) => Record($"\"op\": \"join\", \"player\": \"{player}\"", () => _encounter.Join(player));

        public void Leave(int slot) => Record($"\"op\": \"leave\", \"slot\": {slot}", () => _encounter.Leave(slot));

        public void Input(int slot) => Record($"\"op\": \"input\", \"slot\": {slot}", () => _encounter.ReportInput(slot));

        public void Peaceful(int slot, bool peaceful) => Record($"\"op\": \"peaceful\", \"slot\": {slot}, \"value\": {Bool(peaceful)}", () => _encounter.SetPeaceful(slot, peaceful));

        public void Connected(int slot, bool connected) => Record($"\"op\": \"connected\", \"slot\": {slot}, \"value\": {Bool(connected)}", () => _encounter.SetConnected(slot, connected));

        public double Damage(int slot, uint enemyId, double share) =>
            Record($"\"op\": \"damage\", \"slot\": {slot}, \"enemyId\": {enemyId}, \"share\": {Number(share)}", () => _encounter.ApplyDamage(slot, enemyId, share));

        public double Hit(int slot, double share) => Damage(slot, _encounter.EnemyId, share);

        public bool Effect(int id, double damageTaken, double seconds) =>
            Record($"\"op\": \"effect\", \"id\": {id}, \"damageTaken\": {Number(damageTaken)}, \"seconds\": {Number(seconds)}", () => _encounter.ApplyEnemyEffect(id, damageTaken, seconds));

        public bool RequestBoss() => Record("\"op\": \"requestBoss\"", () => _encounter.RequestBossFight());

        public void Advance(int ticks) => Record($"\"op\": \"advance\", \"ticks\": {ticks}", () =>
        {
            for (var i = 0; i < ticks; i++)
                _encounter.Advance();
        });

        public string ToJson() =>
            $"    {{\n      \"name\": \"{_name}\",\n      \"seed\": \"{_seed}\",\n      \"steps\": [\n{_steps}\n      ]\n    }}";

        private void Record(string op, System.Action action) => Record(op, () =>
        {
            action();
            return 0;
        });

        private T Record<T>(string op, System.Func<T> action)
        {
            var result = action();
            if (_count++ > 0)
                _steps.Append(",\n");
            _steps.Append("        { ").Append(op).Append(", \"result\": ").Append(Result(result)).Append(", \"expect\": ").Append(Snapshot()).Append(" }");
            return result;
        }

        private static string Result<T>(T value) => value switch
        {
            bool flag => Bool(flag),
            double number => Number(number),
            int whole => whole.ToString(CultureInfo.InvariantCulture),
            _ => "null"
        };

        private string Snapshot()
        {
            var e = _encounter;
            var builder = new StringBuilder();
            builder.Append("{ \"tick\": ").Append(e.Tick)
                .Append(", \"enemyId\": ").Append(e.EnemyId)
                .Append(", \"enemyKind\": ").Append((int)e.EnemyKind)
                .Append(", \"enemySeed\": \"").Append(e.EnemySeed).Append('"')
                .Append(", \"killsOnStage\": ").Append(e.KillsOnStage)
                .Append(", \"farming\": ").Append(Bool(e.Farming))
                .Append(", \"farmKills\": ").Append(e.FarmKills)
                .Append(", \"bossTicksLeft\": ").Append(e.BossTicksLeft)
                .Append(", \"barFull\": ").Append(Number(e.BarFull))
                .Append(", \"barLeft\": ").Append(Number(e.BarLeft))
                .Append(", \"fighters\": ").Append(e.Fighters)
                .Append(", \"damageTaken\": ").Append(Number(e.DamageTakenMultiplier))
                .Append(", \"members\": [");
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                var member = e.Member(slot);
                if (slot > 0)
                    builder.Append(", ");
                builder.Append(member.Joined ? $"{{ \"player\": \"{member.PlayerId}\", \"weight\": {member.Weight}, \"presence\": {(int)e.Presence(slot)} }}" : "null");
            }
            builder.Append("], \"events\": [");
            for (var i = 0; i < e.Events.Count; i++)
            {
                var partyEvent = e.Events[i];
                if (i > 0)
                    builder.Append(", ");
                builder.Append("{ \"kind\": ").Append((int)partyEvent.Kind)
                    .Append(", \"tick\": ").Append(partyEvent.Tick)
                    .Append(", \"enemyId\": ").Append(partyEvent.EnemyId)
                    .Append(", \"enemyKind\": ").Append((int)partyEvent.EnemyKind)
                    .Append(", \"shares\": [").Append(Number(partyEvent.Share0)).Append(", ").Append(Number(partyEvent.Share1)).Append(", ")
                    .Append(Number(partyEvent.Share2)).Append(", ").Append(Number(partyEvent.Share3)).Append("] }");
            }
            builder.Append("] }");
            e.Events.Clear();
            return builder.ToString();
        }

        private static string Bool(bool value) => value ? "true" : "false";

        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
