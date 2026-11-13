using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public enum MasterTrialResult
    {
        Started,
        NotInTown,
        NotBuilt,
        NoMaster,
        Busy
    }

    public sealed class MasterTrial
    {
        private readonly GameData _data;
        private readonly HandedInputSource _input = new HandedInputSource();
        private GameLoop? _loop;
        private long _ticksLeft;

        public MasterTrial(GameData data)
        {
            _data = data;
        }

        public bool Active => _loop != null;
        public HeroClass Master { get; private set; }
        public string Building { get; private set; } = string.Empty;
        public double SecondsLeft => _ticksLeft * GameLoop.TickSeconds;
        public long Kills { get; private set; }
        public BigNumber Damage { get; private set; } = BigNumber.Zero;
        public double Resource => _loop == null ? 0 : _loop.State.Resource;
        public GameState? Fight => _loop?.State;

        internal void Start(HeroClass master, string building, ulong seed)
        {
            var rules = _data.Guilds.Trial;
            var state = new GameState(seed) { HeroLevel = System.Math.Max(rules.HeroLevel, _data.TreePoints.ClassChoiceLevel) };
            var loop = new GameLoop(_data, _input, state, new StatSheet());
            loop.ChooseClass(master);
            state.Stage = rules.Stage;
            state.HighestStage = rules.Stage;
            state.RecordStage = rules.Stage;
            _loop = loop;
            Master = master;
            Building = building;
            _ticksLeft = (long)System.Math.Round(rules.Seconds * GameLoop.TicksPerSecond);
            Kills = 0;
            Damage = BigNumber.Zero;
        }

        internal bool Step(InputSnapshot input)
        {
            if (_loop == null)
                return false;
            _input.Hand(input);
            _loop.Tick();
            _loop.Events.Clear();
            _loop.Drops.Flush();
            Kills = _loop.State.Kills;
            Damage = _loop.State.TotalDamage;
            _ticksLeft--;
            return _ticksLeft <= 0;
        }

        internal void Stop() => _loop = null;
    }
}
