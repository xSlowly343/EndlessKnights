namespace Game.Platform.Windows.Window
{
    public sealed class PointerEdges
    {
        public bool Pressed { get; private set; }
        public bool Held { get; private set; }
        public bool Released { get; private set; }

        public void Update(bool buttonDown, bool pressedSinceLastPoll = false)
        {
            var quickTap = pressedSinceLastPoll && !buttonDown && !Held;
            Pressed = (buttonDown && !Held) || quickTap;
            Released = (!buttonDown && Held) || quickTap;
            Held = buttonDown;
        }
    }
}
