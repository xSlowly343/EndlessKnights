using System;

namespace Game.Platform.Windows.Window
{
    public enum FocusModeState
    {
        Normal,
        NoFocusPending,
        NoFocus
    }

    public sealed class FocusMode
    {
        public FocusModeState State { get; private set; } = FocusModeState.Normal;
        public bool IsNoFocus => State != FocusModeState.Normal;

        public event Action<bool>? Changed;

        public void Toggle()
        {
            MoveTo(IsNoFocus ? FocusModeState.Normal : FocusModeState.NoFocusPending);
        }

        public void Observe(bool gameIsForeground)
        {
            if (State == FocusModeState.NoFocusPending && !gameIsForeground)
                MoveTo(FocusModeState.NoFocus);
            else if (State == FocusModeState.NoFocus && gameIsForeground)
                MoveTo(FocusModeState.Normal);
        }

        private void MoveTo(FocusModeState next)
        {
            var wasNoFocus = IsNoFocus;
            State = next;
            if (wasNoFocus != IsNoFocus)
                Changed?.Invoke(IsNoFocus);
        }
    }
}
