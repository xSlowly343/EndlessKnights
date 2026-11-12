namespace Game.Platform.Windows.Window
{
    public struct StripVisibility
    {
        public bool ByHand;
        public bool AutoHide;
        public bool Exclusive;
        public bool Covered;

        public bool Hidden => ByHand || Exclusive || (AutoHide && Covered);
    }
}
