namespace Game.Core.Ui
{
    public enum TooltipSide
    {
        Left,
        Right,
        Inside
    }

    public static class TooltipPlacement
    {
        public static TooltipSide Side(double roomLeftPx, double roomRightPx, double widthPx, double gapPx)
        {
            var need = widthPx + gapPx;
            if (roomLeftPx >= need && roomLeftPx >= roomRightPx)
                return TooltipSide.Left;
            if (roomRightPx >= need)
                return TooltipSide.Right;
            return roomLeftPx >= need ? TooltipSide.Left : TooltipSide.Inside;
        }

        public static double Top(double anchorY, double height, double bottomY, double topY)
        {
            var lowest = System.Math.Min(topY, bottomY + height);
            return System.Math.Max(lowest, System.Math.Min(topY, anchorY + height / 2));
        }
    }
}
