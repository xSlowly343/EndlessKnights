using Game.Core.Stats;

namespace Game.Core.Sim
{
    public static class MinstrelStats
    {
        public static readonly StatId VersePower = new StatId("versePower");
        public static readonly StatId LineHits = new StatId("lineHits");
        public static readonly StatId VerseSeconds = new StatId("verseSeconds");
        public static readonly StatId VerseMax = new StatId("verseMax");
        public static readonly StatId VerseParty = new StatId("verseParty");
        public static readonly StatId ChorusBoost = new StatId("chorusBoost");
        public static readonly StatId ChorusEncore = new StatId("chorusEncore");
        public static readonly StatId ChorusSpend = new StatId("chorusSpend");
        public static readonly StatId ChorusChoir = new StatId("chorusChoir");
        public static readonly StatId ChorusEndless = new StatId("chorusEndless");
        public static readonly StatId ChorusGift = new StatId("chorusGift");

        public static readonly StatId SongPower = new StatId("songPower");
        public static readonly StatId SongHits = new StatId("songHits");
        public static readonly StatId SongSelfPenalty = new StatId("songSelfPenalty");
        public static readonly StatId SongPartyPenalty = new StatId("songPartyPenalty");
        public static readonly StatId SongSolo = new StatId("songSolo");
        public static readonly StatId SongDuet = new StatId("songDuet");
        public static readonly StatId SongChangeHit = new StatId("songChangeHit");
        public static readonly StatId SongAll = new StatId("songAll");
        public static readonly StatId SongStill = new StatId("songStill");
        public static readonly StatId SongGift = new StatId("songGift");
        public static readonly StatId SongParty = new StatId("songParty");

        public static readonly StatId SleepPower = new StatId("sleepPower");
        public static readonly StatId NoteHits = new StatId("noteHits");
        public static readonly StatId SleepNotes = new StatId("sleepNotes");
        public static readonly StatId SleepSeconds = new StatId("sleepSeconds");
        public static readonly StatId SleepForget = new StatId("sleepForget");
        public static readonly StatId SleepOnBoss = new StatId("sleepOnBoss");
        public static readonly StatId SleepLight = new StatId("sleepLight");
        public static readonly StatId SleepByCast = new StatId("sleepByCast");
        public static readonly StatId SleepParty = new StatId("sleepParty");
    }
}
