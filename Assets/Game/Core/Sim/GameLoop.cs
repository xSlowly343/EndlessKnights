using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class GameLoop
    {
        public const int TicksPerSecond = 10;
        public const double TickSeconds = 1.0 / TicksPerSecond;
        public const int MaxCatchUpTicks = 50;

        private const double TimeEpsilon = 1e-9;

        private readonly IInputSource _input;
        private readonly InputRules _inputRules;
        private readonly FocusSystem _focus;
        private readonly EnergySystem _energy;
        private readonly CombatSystem _combat;
        private readonly StageSystem _stage;
        private readonly LootSystem _loot;
        private readonly DpsMeter _dps;
        private readonly SkillSystem _skills;
        private readonly TriggerSystem _triggers;
        private readonly ModifierLayer _treeLayer;
        private GameData _data;
        private double _pendingSeconds;

        public GameLoop(GameData data, IInputSource input, GameState state, StatSheet hero)
        {
            _input = input;
            _data = data;
            State = state;
            Hero = hero;
            _inputRules = new InputRules(data.InputCaps);
            _focus = new FocusSystem(data.Combat.Focus, state);
            _energy = new EnergySystem(data.Combat.Energy, state);
            _combat = new CombatSystem(data.Combat, hero, state, Events);
            _loot = new LootSystem(data.Stages, hero, state, Events);
            _stage = new StageSystem(data.Stages, data.Enemies, hero, state, Events, _loot);
            _combat.OnKill(_stage);
            _skills = new SkillSystem(state, Events, _energy, _combat, hero);
            _combat.OnKeyHit(_skills);
            _triggers = new TriggerSystem(state, _skills);
            _treeLayer = new ModifierLayer(hero);
            RebuildTree();
            _dps = new DpsMeter(data.Combat.Offline, data.Combat.Focus, state);
            _stage.EnsureEnemy();
        }

        public StatSheet Hero { get; }
        public CombatSystem Combat => _combat;
        public StageSystem Stages => _stage;
        public FocusSystem Focus => _focus;
        public EnergySystem Energy => _energy;
        public LootSystem Loot => _loot;
        public SkillSystem Skills => _skills;

        public GameState State { get; }
        public EventQueue Events { get; } = new EventQueue();
        public InputSnapshot LastInput { get; private set; }
        public bool Paused { get; set; }

        public int Advance(double seconds)
        {
            if (Paused || seconds <= 0)
                return 0;

            _pendingSeconds += seconds;
            var ticks = 0;
            while (_pendingSeconds + TimeEpsilon >= TickSeconds)
            {
                _pendingSeconds -= TickSeconds;
                if (ticks == MaxCatchUpTicks)
                {
                    _pendingSeconds = 0;
                    break;
                }
                Tick();
                ticks++;
            }
            if (_pendingSeconds < 0)
                _pendingSeconds = 0;
            return ticks;
        }

        public AllocationResult AllocateNode(string nodeId, string? attribute = null, int? masteryOption = null)
        {
            var result = State.Tree.Allocate(_data.Tree, nodeId, attribute, masteryOption);
            if (result == AllocationResult.Allocated)
                RebuildTree();
            return result;
        }

        public void RebuildTree() => _treeLayer.Set(State.Tree.Modifiers(_data.Tree));

        public void UseData(GameData data)
        {
            _data = data;
            _inputRules.UseCaps(data.InputCaps);
            _focus.UseRules(data.Combat.Focus);
            _energy.UseRules(data.Combat.Energy);
            _combat.UseRules(data.Combat);
            _stage.UseData(data.Stages, data.Enemies);
            _loot.UseData(data.Stages);
            _dps.UseRules(data.Combat.Offline, data.Combat.Focus);
            _skills.UseGems(data.Gems);
            RebuildTree();
        }

        public void Tick()
        {
            LastInput = _input.Drain();
            var input = _inputRules.Apply(LastInput);
            var idleTicksBefore = State.IdleTicks;
            _focus.Tick(input);
            _energy.Tick(input);
            _skills.Tick(input, idleTicksBefore);
            _triggers.Tick(input);
            _stage.Tick();
            _combat.Tick(input);
            _dps.Tick(_combat.LastTickDamage);
            State.Tick++;
        }
    }
}
