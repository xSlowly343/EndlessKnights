using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class ClassMechanics
    {
        private readonly GameState _state;
        private readonly EventQueue _events;
        private readonly StatSheet _hero;
        private ClassRules _rules;

        public ClassMechanics(ClassRules rules, GameState state, EventQueue events, StatSheet hero)
        {
            _hero = hero;
            _rules = rules;
            _state = state;
            _events = events;
        }

        public void UseRules(ClassRules rules) => _rules = rules;

        public int NotesPerMelody => System.Math.Max(1, (int)System.Math.Round(_rules.NotesPerMelody + _hero.Value(CombatStats.MelodyNotes, 0)));

        public bool MelodyReady => _state.Notes >= NotesPerMelody;

        public double OnKeyHit(out HitFlags flags)
        {
            flags = HitFlags.None;
            if (_state.Class != HeroClass.Typist)
                return 1;

            _state.Combo++;
            _state.ComboIdleTicks = 0;
            if (_state.Combo % _rules.ComboStrikeEvery != 0)
                return 1;
            flags = HitFlags.ComboStrike;
            return _rules.ComboStrikeMultiplier;
        }

        public void OnClickHit()
        {
            if (_state.Class != HeroClass.Bard || MelodyReady)
                return;
            _state.Notes++;
            if (MelodyReady)
                _events.Add(GameEvent.ForStage(GameEventKind.MelodyReady, _state.Tick, _state.Stage));
        }

        public bool TryTakeMelody()
        {
            if (!MelodyReady)
                return false;
            _state.Notes = 0;
            return true;
        }

        public void SpendNotes(int notes) => _state.Notes = System.Math.Max(0, _state.Notes - notes);

        public void EndTick(int keyHits)
        {
            if (keyHits > 0 || _state.Combo == 0)
                return;
            _state.ComboIdleTicks++;
            if (_state.ComboIdleTicks > System.Math.Round(_rules.ComboTimeoutSeconds * GameLoop.TicksPerSecond))
            {
                _state.Combo = 0;
                _state.ComboIdleTicks = 0;
            }
        }
    }
}
