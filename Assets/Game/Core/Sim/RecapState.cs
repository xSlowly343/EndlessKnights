using System.Collections.Generic;

namespace Game.Core.Sim
{
    public sealed class RecapDay
    {
        public long Day { get; set; }
        public double Keys { get; set; }
        public double Clicks { get; set; }
        public double MouseMeters { get; set; }
        public double PlaySeconds { get; set; }
        public long Kills { get; set; }
        public long Bosses { get; set; }
        public long Stages { get; set; }
        public long RecordStage { get; set; }
        public long Prestiges { get; set; }
        public long Levels { get; set; }

        public bool Empty => PlaySeconds <= 0 && Keys <= 0 && Clicks <= 0 && Kills <= 0;

        public void Add(RecapDay other)
        {
            Keys += other.Keys;
            Clicks += other.Clicks;
            MouseMeters += other.MouseMeters;
            PlaySeconds += other.PlaySeconds;
            Kills += other.Kills;
            Bosses += other.Bosses;
            Stages += other.Stages;
            Prestiges += other.Prestiges;
            Levels += other.Levels;
            RecordStage = System.Math.Max(RecordStage, other.RecordStage);
        }
    }

    public sealed class RecapState
    {
        public const int DaysKept = 14;

        public long Day { get; set; } = -1;
        public RecapDay Start { get; } = new RecapDay();
        public List<RecapDay> Days { get; } = new List<RecapDay>();
        public long ShownDay { get; set; } = -1;
        public long ShownWeek { get; set; } = -1;
    }
}
