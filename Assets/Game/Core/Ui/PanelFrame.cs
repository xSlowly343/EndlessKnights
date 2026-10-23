namespace Game.Core.Ui
{
    public readonly struct PanelFrame
    {
        public readonly int WidthPx;
        public readonly int HeightPx;
        public readonly int HeaderPx;
        public readonly int SubTabsPx;
        public readonly int TabBarPx;

        public PanelFrame(int widthPx, int heightPx, int headerPx, int subTabsPx, int tabBarPx)
        {
            WidthPx = widthPx;
            HeightPx = heightPx;
            HeaderPx = headerPx;
            SubTabsPx = subTabsPx;
            TabBarPx = tabBarPx;
        }

        public int ContentPx => HeightPx - HeaderPx - SubTabsPx - TabBarPx;
        public int AreaHeightPx => HeightPx + PanelGeometry.GapToStripPx;
        public bool HasSubTabs => SubTabsPx > 0;
    }
}
