namespace Game.Core.Ui
{
    public static class PanelGeometry
    {
        public const int WidthPx = 480;
        public const int FullHeightPx = 600;
        public const int HalfHeightPx = 320;
        public const int WideWidthPx = 960;
        public const int WideHeightPx = 640;
        public const int HeaderPx = 46;
        public const int SubTabsPx = 40;
        public const int TabBarPx = 60;
        public const int GapToStripPx = 6;

        public static PanelFrame Frame(PanelHeight height, bool wide, bool hasSubTabs)
        {
            if (wide)
                return new PanelFrame(WideWidthPx, WideHeightPx, HeaderPx, hasSubTabs ? SubTabsPx : 0, TabBarPx);
            var full = height == PanelHeight.Full;
            var subTabs = full && hasSubTabs ? SubTabsPx : 0;
            return new PanelFrame(WidthPx, full ? FullHeightPx : HalfHeightPx, HeaderPx, subTabs, TabBarPx);
        }
    }
}
