using System.Collections.Generic;

namespace Game.Core.Telemetry
{
    public sealed class TelemetryPacket
    {
        public const int CurrentSchema = 1;

        public int Schema { get; set; } = CurrentSchema;
        public string InstallId { get; set; } = string.Empty;
        public string GameVersion { get; set; } = string.Empty;
        public int Session { get; set; }
        public bool Closed { get; set; }
        public long SessionSeconds { get; set; }
        public double KeysPerMinute { get; set; }
        public double ClicksPerMinute { get; set; }
        public double MouseMeters { get; set; }
        public long TotalPlaySeconds { get; set; }
        public long? FirstPrestigeSeconds { get; set; }
        public int Prestiges { get; set; }
        public long RecordStage { get; set; } = 1;
        public string Character { get; set; } = string.Empty;
        public int DaysSinceFirst { get; set; }
        public IReadOnlyList<int> ReturnDays { get; set; } = new int[0];
    }
}
