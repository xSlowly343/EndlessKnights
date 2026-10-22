namespace Game.Core.Content
{
    public sealed class FocusRules
    {
        public FocusRules(double min, double max, double perEvent, double mousePxPerEvent, double resetAfterIdleSeconds,
            double macroCvBelow, double macroAfterSeconds)
        {
            Min = min;
            Max = max;
            PerEvent = perEvent;
            MousePxPerEvent = mousePxPerEvent;
            ResetAfterIdleSeconds = resetAfterIdleSeconds;
            MacroCvBelow = macroCvBelow;
            MacroAfterSeconds = macroAfterSeconds;
        }

        public double Min { get; }
        public double Max { get; }
        public double PerEvent { get; }
        public double MousePxPerEvent { get; }
        public double ResetAfterIdleSeconds { get; }
        public double MacroCvBelow { get; }
        public double MacroAfterSeconds { get; }

        public static FocusRules Read(ContentNode node)
        {
            var min = node.Field("min").AsNumber(0.01, 1000);
            var max = node.Field("max").AsNumber(0.01, 1000);
            if (max < min)
                throw node.Field("max").Error($"must not be lower than min ({min})");
            var macro = node.Field("macroRhythm");
            return new FocusRules(
                min,
                max,
                node.Field("perEvent").AsNumber(0, 100),
                node.Field("mousePxPerEvent").AsNumber(1, 1e6),
                node.Field("resetAfterIdleSeconds").AsNumber(0.1, 3600),
                macro.Field("cvBelow").AsNumber(0, 10),
                macro.Field("afterSeconds").AsNumber(0.1, 3600));
        }
    }
}
