using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class ResourceSystem
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private ResourceRules _rules;
        private HeroClass _class;
        private ClassResource _resource;

        public ResourceSystem(ResourceRules rules, GameState state, StatSheet hero)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _class = state.Class;
            _resource = rules.For(HeroClassNames.Of(state.Class));
        }

        public void UseRules(ResourceRules rules)
        {
            _rules = rules;
            _resource = rules.For(HeroClassNames.Of(_class));
        }

        public double Max => System.Math.Max(0, _rules.Max + _hero.Value(CombatStats.ResourceMax, 0) - _state.ReservedResource);
        public bool IsFull => _state.Resource >= Max;
        public ClassResource Rules => Current;

        private ClassResource Current
        {
            get
            {
                if (_class != _state.Class)
                {
                    _class = _state.Class;
                    _resource = _rules.For(HeroClassNames.Of(_class));
                }
                return _resource;
            }
        }

        public bool Overheated => _state.OverheatTicks > 0;
        public double GainedThisTick { get; private set; }

        public void Tick(in EffectiveInput input)
        {
            var resource = Current;
            GainedThisTick = 0;
            _state.KeylessTicks = input.Keys > 0 ? 0 : _state.KeylessTicks + 1;
            _state.MouseIdleTicks = input.MousePx > 0 ? 0 : _state.MouseIdleTicks + 1;
            _state.MouseWindow.Record(input.MousePx);
            _state.ClicklessTicks = input.Clicks > 0 || input.LeftClicks + input.RightClicks + input.MiddleClicks > 0 ? 0 : _state.ClicklessTicks + 1;
            _state.Reading = Reading(resource, input);
            if (resource.Inverse)
            {
                TickInverse(resource, input);
                NoteFill();
                return;
            }
            var gained = input.Keys * PerKey(resource) * _hero.Value(CombatStats.ResourceGain, 1, StatTags.Keyboard);
            if (resource.MousePxPerPoint > 0)
                gained += input.MousePx / resource.MousePxPerPoint * _hero.Value(CombatStats.ResourceGain, 1, StatTags.Mouse);
            gained += resource.PerSecond * GameLoop.TickSeconds * _hero.Value(CombatStats.ResourceGain, 1);
            gained += input.Clicks * resource.PerClick * _hero.Value(CombatStats.ResourceGain, 1, StatTags.Click);
            if (_state.Reading)
                gained += resource.ReadingPerSecond * GameLoop.TickSeconds * _hero.Value(CombatStats.ResourceGain, 1, StatTags.Mouse) * System.Math.Max(0, _hero.Value(CombatStats.ReadingGain, 1));
            gained += _state.PendingResource;
            _state.PendingResource = 0;
            var value = System.Math.Min(Max, _state.Resource + gained);
            GainedThisTick += System.Math.Max(0, value - _state.Resource);
            if (Decays(resource, input))
                value -= resource.DecayPerSecond * System.Math.Max(0, _hero.Value(CombatStats.ResourceDecay, 1)) * GameLoop.TickSeconds;
            if (_state.DecayHoldTicks > 0)
                _state.DecayHoldTicks--;
            _state.Resource = System.Math.Max(0, value);
            NoteFill();
        }

        private double PerKey(ClassResource resource) => resource.PerKey + System.Math.Max(0, _hero.Value(CombatStats.ResourcePerKey, 0));

        private void NoteFill()
        {
            var max = Max;
            _state.ResourceFill = max > 0 ? System.Math.Min(1, _state.Resource / max) : 0;
        }

        public void Gain(double amount)
        {
            if (amount <= 0)
                return;
            var before = _state.Resource;
            _state.Resource = System.Math.Min(Max, _state.Resource + amount);
            if (!Current.Inverse)
                GainedThisTick += System.Math.Max(0, _state.Resource - before);
        }

        public double SpendAll()
        {
            var spent = _state.Resource;
            _state.Resource = 0;
            return spent;
        }

        public void HoldDecay(double seconds) =>
            _state.DecayHoldTicks = System.Math.Max(_state.DecayHoldTicks, (int)System.Math.Round(seconds * GameLoop.TicksPerSecond));

        public void ClampToMax() => _state.Resource = System.Math.Min(Max, _state.Resource);

        public bool TrySpend(double amount)
        {
            if (Current.Inverse)
                return TryBuild(amount);
            if (amount < 0 || _state.Resource < amount)
                return false;
            _state.Resource -= amount;
            return true;
        }

        private void TickInverse(ClassResource resource, in EffectiveInput input)
        {
            if (_state.OverheatTicks > 0)
                _state.OverheatTicks--;
            var decay = resource.InverseDecayPerSecond * System.Math.Max(0, _hero.Value(CombatStats.PressureDecay, 1)) * GameLoop.TickSeconds;
            _state.Resource = System.Math.Max(0, System.Math.Min(Max, _state.Resource + _state.PendingResource) - decay);
            _state.PendingResource = 0;
            var fromKeys = input.Keys * System.Math.Max(0, _hero.Value(CombatStats.ResourcePerKey, 0)) * _hero.Value(CombatStats.ResourceGain, 1, StatTags.Keyboard);
            if (fromKeys > 0 && !Overheated)
                Build(fromKeys);
        }

        private bool TryBuild(double amount)
        {
            if (amount < 0 || Overheated)
                return false;
            Build(amount);
            return true;
        }

        private void Build(double amount)
        {
            _state.Resource = System.Math.Min(Max, _state.Resource + amount);
            if (_state.Resource >= Max && _hero.Value(CombatStats.OverheatDisabled, 0) <= 0)
                _state.OverheatTicks = (int)System.Math.Round((Current.OverheatSeconds + _hero.Value(CombatStats.OverheatSeconds, 0)) * GameLoop.TicksPerSecond);
        }

        private bool Reading(ClassResource resource, in EffectiveInput input) =>
            resource.ReadingPerSecond > 0 && input.MousePx > 0
            && _state.KeylessTicks * GameLoop.TickSeconds > resource.ReadingAfterKeylessSeconds;

        private bool Decays(ClassResource resource, in EffectiveInput input)
        {
            if (resource.DecayPerSecond <= 0 || _state.DecayHoldTicks > 0)
                return false;
            if (resource.MouseHoldsDecay && input.MousePx > 0)
                return false;
            return _state.KeylessTicks * GameLoop.TickSeconds > resource.DecayAfterKeylessSeconds + _hero.Value(CombatStats.RageBurnDelaySeconds, 0);
        }
    }
}
