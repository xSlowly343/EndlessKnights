using Game.Core.Content;

namespace Game.Core.Sim
{
    public sealed class CompanionAbilitySystem
    {
        public const string Unlock = "ability";

        private readonly GameState _state;
        private readonly CompanionSystem _companions;
        private readonly ExpeditionSystem _expeditions;
        private readonly GardenSystem _garden;
        private readonly DigSystem _digs;
        private readonly LevelSystem _levels;

        public CompanionAbilitySystem(GameState state, CompanionSystem companions, ExpeditionSystem expeditions, GardenSystem garden, DigSystem digs, LevelSystem levels)
        {
            _state = state;
            _companions = companions;
            _expeditions = expeditions;
            _garden = garden;
            _digs = digs;
            _levels = levels;
            companions.Abilities = this;
        }

        private CompanionState State => _companions.State;

        public CompanionAbility? AbilityOf(string companion)
        {
            var family = _companions.Rules.Families.FamilyOf(companion);
            return family == null ? null : _companions.Rules.Abilities.Of(family.Id);
        }

        public bool Ready(string companion) => Check(companion) == CompanionResult.Done;

        public CompanionResult Check(string companion)
        {
            if (!_companions.Known(companion))
                return CompanionResult.UnknownCompanion;
            var ability = AbilityOf(companion);
            if (ability == null || !_companions.Unlocked(companion, Unlock))
                return CompanionResult.Locked;
            if (!ability.Works)
                return CompanionResult.Waits;
            if (_companions.Progress(companion).AbilityDay == State.Day)
                return CompanionResult.TooSoon;
            if (ability.Kind == AbilityKind.Rally && !SameClass(companion))
                return CompanionResult.WrongClass;
            return CompanionResult.Done;
        }

        public CompanionResult Use(string companion, int trip = -1)
        {
            var result = Check(companion);
            if (result != CompanionResult.Done)
                return result;
            var ability = AbilityOf(companion)!;
            if (!Apply(ability, trip))
                return CompanionResult.NothingToDo;
            _companions.Progress(companion).AbilityDay = State.Day;
            return CompanionResult.Done;
        }

        private bool Apply(CompanionAbility ability, int trip)
        {
            switch (ability.Kind)
            {
                case AbilityKind.Harvest:
                    return _garden.Ripen() > 0;
                case AbilityKind.Dig:
                    return _digs.DigNow();
                case AbilityKind.Bite:
                    State.QuickBiteSeconds = ability.Seconds;
                    State.QuickBiteFactor = ability.Value;
                    return true;
                case AbilityKind.Forge:
                    if (State.SureForge)
                        return false;
                    State.SureForge = true;
                    return true;
                case AbilityKind.Tailwind:
                    return _expeditions.Hasten(ability.Seconds) > 0;
                case AbilityKind.Fold:
                    return Fold(ability.Value, trip);
                case AbilityKind.Rally:
                    State.XpBoostSeconds = ability.Seconds;
                    State.XpBoost = ability.Value;
                    _levels.ExtraXp = ability.Value;
                    return true;
                default:
                    return false;
            }
        }

        private bool Fold(double loot, int trip)
        {
            var active = _state.Expeditions.Active;
            if (trip < 0 || trip >= active.Count)
            {
                trip = -1;
                for (var i = 0; i < active.Count; i++)
                {
                    if (!active[i].Colossus && (trip < 0 || active[i].Seconds - active[i].Elapsed > active[trip].Seconds - active[trip].Elapsed))
                        trip = i;
                }
            }
            if (trip < 0 || active[trip].Colossus)
                return false;
            _expeditions.FinishWithLoot(trip, loot);
            return true;
        }

        private bool SameClass(string companion)
        {
            var unlock = _companions.Rules.Companion(companion)?.Unlock;
            if (unlock == null || unlock.Class.Length == 0)
                return true;
            return _state.ClassChosen && HeroClassNames.ByName.TryGetValue(unlock.Class, out var heroClass) && heroClass == _state.Class;
        }

        public void Advance(double seconds)
        {
            if (State.XpBoostSeconds > 0)
            {
                State.XpBoostSeconds = System.Math.Max(0, State.XpBoostSeconds - seconds);
                if (State.XpBoostSeconds <= 0)
                    State.XpBoost = 0;
            }
            _levels.ExtraXp = State.XpBoostSeconds > 0 ? State.XpBoost : 0;
        }
    }
}
