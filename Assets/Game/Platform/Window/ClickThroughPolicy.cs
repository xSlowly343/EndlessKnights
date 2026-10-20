namespace Game.Platform.Windows.Window
{
    public static class ClickThroughPolicy
    {
        public static bool ShouldPassClicks(bool cursorOverSprite, bool mouseButtonHeld, bool passingClicksNow)
        {
            if (mouseButtonHeld)
                return passingClicksNow;
            return !cursorOverSprite;
        }
    }
}
