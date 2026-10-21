namespace Game.Core.Content
{
    public sealed class InputDetection
    {
        public InputDetection(double flickWindowMs, double flickSpeedPxPerSecond, double circleMinStepPx, double circlePauseMs,
            double jigglerWindowMs, double jigglerMinDisplacementPx, int rhythmIntervals, double rhythmMaxIntervalMs)
        {
            FlickWindowMs = flickWindowMs;
            FlickSpeedPxPerSecond = flickSpeedPxPerSecond;
            CircleMinStepPx = circleMinStepPx;
            CirclePauseMs = circlePauseMs;
            JigglerWindowMs = jigglerWindowMs;
            JigglerMinDisplacementPx = jigglerMinDisplacementPx;
            RhythmIntervals = rhythmIntervals;
            RhythmMaxIntervalMs = rhythmMaxIntervalMs;
        }

        public double FlickWindowMs { get; }
        public double FlickSpeedPxPerSecond { get; }
        public double CircleMinStepPx { get; }
        public double CirclePauseMs { get; }
        public double JigglerWindowMs { get; }
        public double JigglerMinDisplacementPx { get; }
        public int RhythmIntervals { get; }
        public double RhythmMaxIntervalMs { get; }

        public static InputDetection Read(ContentNode node)
        {
            var flick = node.Field("flick");
            var circle = node.Field("circle");
            var jiggler = node.Field("jiggler");
            var rhythm = node.Field("rhythm");
            return new InputDetection(
                flick.Field("windowMs").AsNumber(10, 1000),
                flick.Field("speedPxPerSecond").AsNumber(100, 100000),
                circle.Field("minStepPx").AsNumber(0, 100),
                circle.Field("pauseMs").AsNumber(10, 10000),
                jiggler.Field("windowMs").AsNumber(10, 10000),
                jiggler.Field("minDisplacementPx").AsNumber(0, 1000),
                rhythm.Field("intervals").AsInt(10, 10000),
                rhythm.Field("maxIntervalMs").AsNumber(10, 60000));
        }
    }
}
