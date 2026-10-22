namespace Game.Core.Content
{
    public sealed class ClassRules
    {
        public ClassRules(double comboTimeoutSeconds, int comboStrikeEvery, double comboStrikeMultiplier, int notesPerMelody)
        {
            ComboTimeoutSeconds = comboTimeoutSeconds;
            ComboStrikeEvery = comboStrikeEvery;
            ComboStrikeMultiplier = comboStrikeMultiplier;
            NotesPerMelody = notesPerMelody;
        }

        public double ComboTimeoutSeconds { get; }
        public int ComboStrikeEvery { get; }
        public double ComboStrikeMultiplier { get; }
        public int NotesPerMelody { get; }

        public static ClassRules Read(ContentNode node)
        {
            var combo = node.Field("typist").Field("combo");
            return new ClassRules(
                combo.Field("timeoutSeconds").AsNumber(0.1, 60),
                combo.Field("strikeEvery").AsInt(1, 10000),
                combo.Field("strikeMultiplier").AsNumber(1, 1000),
                node.Field("bard").Field("notesPerMelody").AsInt(1, 100));
        }
    }
}
