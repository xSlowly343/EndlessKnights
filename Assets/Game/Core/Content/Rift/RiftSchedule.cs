using System;
using System.Collections.Generic;
using Game.Core.Math;

namespace Game.Core.Content.Rift
{
    public sealed class RiftSchedule
    {
        private const long HourMs = 3600L * 1000;
        private const long DayMs = 24 * HourMs;
        private const long WeekMs = 7 * DayMs;

        private readonly IReadOnlyList<(DayOfWeek Day, int HourUtc)> _starts;

        public RiftSchedule(IReadOnlyList<(DayOfWeek Day, int HourUtc)> starts, double durationHours, double announceMinutes)
        {
            _starts = starts;
            DurationMs = (long)System.Math.Round(durationHours * HourMs);
            AnnounceMs = (long)System.Math.Round(announceMinutes * 60 * 1000);
        }

        public long DurationMs { get; }
        public long AnnounceMs { get; }

        public RiftWindow? WindowAt(long unixMs)
        {
            foreach (var start in _starts)
            {
                var latest = LatestStart(start.Day, start.HourUtc, unixMs);
                var window = new RiftWindow(latest, latest + DurationMs);
                if (window.Contains(unixMs))
                    return window;
            }
            return null;
        }

        public RiftWindow Next(long unixMs)
        {
            var best = long.MaxValue;
            foreach (var start in _starts)
            {
                var next = LatestStart(start.Day, start.HourUtc, unixMs);
                if (next <= unixMs)
                    next += WeekMs;
                best = System.Math.Min(best, next);
            }
            return new RiftWindow(best, best + DurationMs);
        }

        public bool Announced(long unixMs) => WindowAt(unixMs) == null && Next(unixMs).StartUnixMs - unixMs <= AnnounceMs;

        public static IReadOnlyList<int> PickModifiers(long windowId, int modifierCount, int pick)
        {
            var order = new int[modifierCount];
            for (var i = 0; i < order.Length; i++)
                order[i] = i;
            var take = System.Math.Min(pick, modifierCount);
            for (var i = 0; i < take; i++)
            {
                var swap = i + (int)(SplitMix.At(unchecked((ulong)windowId), (ulong)i + 1) % (ulong)(modifierCount - i));
                (order[i], order[swap]) = (order[swap], order[i]);
            }
            var picked = new int[take];
            Array.Copy(order, picked, take);
            return picked;
        }

        private static long LatestStart(DayOfWeek day, int hourUtc, long unixMs)
        {
            var dayStart = FloorDiv(unixMs, DayMs) * DayMs;
            var today = (int)((FloorDiv(unixMs, DayMs) + 4) % 7);
            var daysBack = (today - (int)day + 7) % 7;
            var start = dayStart - daysBack * DayMs + hourUtc * HourMs;
            return start > unixMs ? start - WeekMs : start;
        }

        private static long FloorDiv(long value, long divisor) => value >= 0 ? value / divisor : -((-value + divisor - 1) / divisor);
    }
}
