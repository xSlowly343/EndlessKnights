namespace Game.Core.Content
{
    public sealed class MinstrelRules
    {
        public static readonly MinstrelRules None = new MinstrelRules();

        public double SoloMultiplier { get; private set; } = 2;
        public int LineHits { get; private set; } = 10;
        public int LineHitsMin { get; private set; } = 3;
        public int LinesPerVerse { get; private set; } = 4;
        public double VerseSeconds { get; private set; } = 12;
        public double VerseSecondsMin { get; private set; } = 2;
        public int VerseMax { get; private set; } = 3;
        public int ChorusVerses { get; private set; } = 3;
        public double ChorusMultiplier { get; private set; } = 2;
        public double ChorusSeconds { get; private set; } = 8;
        public double EndlessSeconds { get; private set; } = 4;
        public double ChorusGift { get; private set; } = 10;
        public int SongHits { get; private set; } = 40;
        public int SongHitsMin { get; private set; } = 5;
        public double SongPartyCooldown { get; private set; } = 0.5;
        public double SongPartyCrit { get; private set; } = 0.2;
        public double SongSoloMultiplier { get; private set; } = 2;
        public double SongAllEverySeconds { get; private set; } = 30;
        public double SongAllSeconds { get; private set; } = 5;
        public double SongAllMultiplier { get; private set; } = 2;
        public double SongGift { get; private set; } = 10;
        public int NoteHits { get; private set; } = 20;
        public int NoteHitsMin { get; private set; } = 4;
        public int SleepNotes { get; private set; } = 7;
        public int SleepNotesMin { get; private set; } = 2;
        public double SleepSeconds { get; private set; } = 4;
        public double SleepSecondsMin { get; private set; } = 1;
        public double SleepBossSeconds { get; private set; } = 8;

        public static MinstrelRules Read(ContentNode node)
        {
            var common = node.Field("common");
            var verse = node.Field("verse");
            var song = node.Field("song");
            var lullaby = node.Field("lullaby");
            return new MinstrelRules
            {
                SoloMultiplier = common.Field("soloMultiplier").AsNumber(1, 100),
                LineHits = verse.Field("lineHits").AsInt(1, 10_000),
                LineHitsMin = verse.Field("lineHitsMin").AsInt(1, 10_000),
                LinesPerVerse = verse.Field("lines").AsInt(1, 100),
                VerseSeconds = verse.Field("seconds").AsNumber(0.1, 3600),
                VerseSecondsMin = verse.Field("secondsMin").AsNumber(0.1, 3600),
                VerseMax = verse.Field("max").AsInt(1, 100),
                ChorusVerses = verse.Field("chorusVerses").AsInt(1, 100),
                ChorusMultiplier = verse.Field("chorusMultiplier").AsNumber(1, 100),
                ChorusSeconds = verse.Field("chorusSeconds").AsNumber(0.1, 3600),
                EndlessSeconds = verse.Field("endlessSeconds").AsNumber(0, 3600),
                ChorusGift = verse.Field("gift").AsNumber(0, 1000),
                SongHits = song.Field("hits").AsInt(1, 10_000),
                SongHitsMin = song.Field("hitsMin").AsInt(1, 10_000),
                SongPartyCooldown = song.Field("partyCooldown").AsNumber(0, 10),
                SongPartyCrit = song.Field("partyCrit").AsNumber(0, 10),
                SongSoloMultiplier = song.Field("soloMultiplier").AsNumber(1, 100),
                SongAllEverySeconds = song.Field("allEverySeconds").AsNumber(0.1, 3600),
                SongAllSeconds = song.Field("allSeconds").AsNumber(0, 3600),
                SongAllMultiplier = song.Field("allMultiplier").AsNumber(1, 100),
                SongGift = song.Field("gift").AsNumber(0, 1000),
                NoteHits = lullaby.Field("noteHits").AsInt(1, 10_000),
                NoteHitsMin = lullaby.Field("noteHitsMin").AsInt(1, 10_000),
                SleepNotes = lullaby.Field("notes").AsInt(1, 1000),
                SleepNotesMin = lullaby.Field("notesMin").AsInt(1, 1000),
                SleepSeconds = lullaby.Field("seconds").AsNumber(0.1, 3600),
                SleepSecondsMin = lullaby.Field("secondsMin").AsNumber(0.1, 3600),
                SleepBossSeconds = lullaby.Field("bossSeconds").AsNumber(0, 3600)
            };
        }
    }
}
