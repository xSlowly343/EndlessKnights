using System.Collections.Generic;

namespace Game.Core.Telemetry
{
    public sealed class TelemetryState
    {
        public const int MaxTrackedDay = 365;

        private readonly List<int> _returnDays = new List<int>();

        public TelemetryState(string installId, long firstLocalDay)
        {
            InstallId = installId;
            FirstLocalDay = firstLocalDay;
        }

        public string InstallId { get; }
        public long FirstLocalDay { get; }
        public IReadOnlyList<int> ReturnDays => _returnDays;
        public int Sessions { get; set; }
        public long? FirstPrestigeSeconds { get; set; }
        public bool SawNoPrestige { get; set; }
        public TelemetryPacket? OpenSession { get; set; }

        public int DayOffset(long localDay) => (int)System.Math.Clamp(localDay - FirstLocalDay, 0, MaxTrackedDay);

        public bool MarkDay(long localDay)
        {
            var offset = localDay - FirstLocalDay;
            if (offset < 0 || offset > MaxTrackedDay)
                return false;
            var index = _returnDays.BinarySearch((int)offset);
            if (index >= 0)
                return false;
            _returnDays.Insert(~index, (int)offset);
            return true;
        }

        public void ObservePrestiges(int prestiges, double totalPlaySeconds)
        {
            if (prestiges <= 0)
            {
                SawNoPrestige = true;
                return;
            }
            if (SawNoPrestige && FirstPrestigeSeconds == null)
                FirstPrestigeSeconds = (long)System.Math.Round(totalPlaySeconds);
        }
    }
}
