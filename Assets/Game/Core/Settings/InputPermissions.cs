using Game.Core.Input;

namespace Game.Core.Settings
{
    public readonly struct InputPermissions
    {
        public static readonly InputPermissions All = new InputPermissions(true, true, true, true, true);
        public static readonly InputPermissions None = default;

        public readonly bool Consent;
        public readonly bool Keys;
        public readonly bool MouseMovement;
        public readonly bool Clicks;
        public readonly bool Wheel;

        public InputPermissions(bool consent, bool keys, bool mouseMovement, bool clicks, bool wheel)
        {
            Consent = consent;
            Keys = keys;
            MouseMovement = mouseMovement;
            Clicks = clicks;
            Wheel = wheel;
        }

        public static InputPermissions From(GameSettings settings) =>
            new InputPermissions(settings.InputConsentGiven, settings.CountKeys, settings.CountMouseMovement, settings.CountClicks, settings.CountWheel);

        public bool CountsAnything => Consent && (Keys || MouseMovement || Clicks || Wheel);

        public InputSnapshot Apply(in InputSnapshot input)
        {
            if (!Consent)
                return new InputSnapshot(secondsSinceLastInput: input.SecondsSinceLastInput, suppressed: input.Suppressed);
            return new InputSnapshot(
                Keys ? input.KeyDowns : 0,
                Keys ? input.EnterDowns : 0,
                Keys ? input.BackspaceDowns : 0,
                Keys ? input.SpaceDowns : 0,
                Clicks ? input.LeftClicks : 0,
                Clicks ? input.RightClicks : 0,
                Clicks ? input.MiddleClicks : 0,
                Wheel ? input.WheelTicks : 0,
                MouseMovement ? input.MouseDistancePx : 0f,
                MouseMovement ? input.Flicks : 0,
                MouseMovement ? input.CircleTurns : 0f,
                input.SecondsSinceLastInput,
                Keys ? input.RhythmCv : 0f,
                input.Suppressed);
        }
    }
}
