using Game.Core.Sim;
using Game.Core.Stats;

namespace Game.Core.Party
{
    public sealed class CoopGiver
    {
        public const int MaxLines = 48;

        private readonly CoopLine?[] _lines = new CoopLine?[MaxLines];
        private readonly int[] _statIndex = new int[MaxLines];
        private readonly int[] _stacks = new int[MaxLines];
        private readonly int[] _ticksLeft = new int[MaxLines];
        private readonly double[] _counter = new double[MaxLines];
        private readonly CoopLine?[] _previousLines = new CoopLine?[MaxLines];
        private readonly int[] _previousStacks = new int[MaxLines];
        private readonly int[] _previousTicks = new int[MaxLines];
        private readonly double[] _previousCounter = new double[MaxLines];
        private int _count;
        private int _version = -1;

        public double[] Self { get; } = new double[CoopRules.MaxStats];
        public double[] Allies { get; } = new double[CoopRules.MaxStats];
        public double EnemyDamageTaken { get; private set; }
        public double EnemyTimerSlow { get; private set; }
        public int LineCount => _count;

        public CoopLine? Line(int index) => _lines[index];

        public int Stacks(int index) => _stacks[index];

        public void Tick(StatSheet hero, GameState state, CoopRules rules, double modeSelf, double modePower)
        {
            if (hero.ModifierVersion != _version)
                Rescan(hero, state, rules);
            System.Array.Clear(Self, 0, Self.Length);
            System.Array.Clear(Allies, 0, Allies.Length);
            EnemyDamageTaken = 0;
            EnemyTimerSlow = 0;
            var debuffPower = System.Math.Max(0, hero.Value(CombatStats.EnemyDebuffEffect, 1));
            for (var i = 0; i < _count; i++)
            {
                var line = _lines[i]!;
                TickAfter(i, line, state);
                var value = hero.Value(line.Source, 0);
                if (!(value > 0))
                    continue;
                value *= Multiplier(i, line, hero, state);
                if (!(value > 0))
                    continue;
                Route(i, line, value, modeSelf, modePower, debuffPower);
            }
        }

        private void Route(int index, CoopLine line, double value, double modeSelf, double modePower, double debuffPower)
        {
            if (line.To == CoopTarget.Enemy)
            {
                if (line.Target == CoopRules.EnemyDamageTaken)
                    EnemyDamageTaken += value * modePower * debuffPower;
                else
                    EnemyTimerSlow += value * modePower;
                return;
            }
            var stat = _statIndex[index];
            if (stat < 0)
                return;
            switch (line.To)
            {
                case CoopTarget.Self:
                    Self[stat] += value;
                    break;
                case CoopTarget.Party:
                    Self[stat] += value * modeSelf;
                    Allies[stat] += value * modePower;
                    break;
                case CoopTarget.Allies:
                    Allies[stat] += value * modePower;
                    break;
            }
        }

        private double Multiplier(int index, CoopLine line, StatSheet hero, GameState state)
        {
            if (line.HasWhile && !(CoopSignals.Read(line.WhileSignal, state) > 0))
                return 0;
            var multiplier = 1.0;
            if (line.HasPer)
                multiplier *= System.Math.Min(line.PerCap, System.Math.Floor(CoopSignals.Read(line.PerSignal, state) / line.PerStep + 1e-9));
            if (line.HasAfter)
                multiplier *= _stacks[index];
            if (line.ShareStat is { } share)
                multiplier *= line.ShareKind == ModifierKind.Flat ? hero.Value(share, 0) : hero.Value(share, 1) - 1;
            return multiplier;
        }

        private void TickAfter(int index, CoopLine line, GameState state)
        {
            if (!line.HasAfter)
                return;
            var counter = CoopSignals.Read(line.AfterSignal, state);
            var fired = counter - _counter[index];
            _counter[index] = counter;
            if (fired >= 1)
            {
                _stacks[index] = (int)System.Math.Min(line.AfterStacks, _stacks[index] + fired);
                _ticksLeft[index] = (int)System.Math.Round(line.AfterSeconds * GameLoop.TicksPerSecond);
                return;
            }
            if (_ticksLeft[index] > 0 && --_ticksLeft[index] == 0)
                _stacks[index] = 0;
        }

        private void Rescan(StatSheet hero, GameState state, CoopRules rules)
        {
            _version = hero.ModifierVersion;
            var previous = _count;
            for (var i = 0; i < previous; i++)
            {
                _previousLines[i] = _lines[i];
                _previousStacks[i] = _stacks[i];
                _previousTicks[i] = _ticksLeft[i];
                _previousCounter[i] = _counter[i];
            }
            _count = 0;
            var modifiers = hero.Modifiers;
            for (var m = 0; m < modifiers.Count && _count < MaxLines; m++)
            {
                var stat = modifiers[m].Stat;
                if (!CoopLine.IsCoop(stat) || !CoopLine.TryGet(stat, out var line) || Known(line))
                    continue;
                _lines[_count] = line;
                _statIndex[_count] = line.To == CoopTarget.Enemy ? -1 : rules.IndexOf(line.Target, line.Kind);
                Restore(_count, line, previous, state);
                _count++;
            }
            for (var i = _count; i < MaxLines; i++)
                _lines[i] = null;
            for (var i = 0; i < previous; i++)
                _previousLines[i] = null;
        }

        private void Restore(int index, CoopLine line, int previous, GameState state)
        {
            for (var i = 0; i < previous; i++)
            {
                if (!ReferenceEquals(_previousLines[i], line))
                    continue;
                _stacks[index] = _previousStacks[i];
                _ticksLeft[index] = _previousTicks[i];
                _counter[index] = _previousCounter[i];
                return;
            }
            _stacks[index] = 0;
            _ticksLeft[index] = 0;
            _counter[index] = line.HasAfter ? CoopSignals.Read(line.AfterSignal, state) : 0;
        }

        private bool Known(CoopLine line)
        {
            for (var i = 0; i < _count; i++)
            {
                if (ReferenceEquals(_lines[i], line))
                    return true;
            }
            return false;
        }
    }
}
