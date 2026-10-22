using System;
using System.Runtime.InteropServices;
using Game.Core.Clock;

namespace Game.Platform.Windows.Power
{
    public static class SystemClock
    {
        private const long MillisecondsPerMinute = 60_000;
        private static readonly UIntPtr LocalMachine = new UIntPtr(0x80000002u);
        private const string PrefetchKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters";
        private const string BootIdValue = "BootId";
        private const uint RestrictDword = 0x00000010;

        private static long? _bootId;

        public static ClockReading Read()
        {
            var wall = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var sinceBoot = (long)GetTickCount64();
            _bootId ??= ReadBootId() ?? (wall - sinceBoot) / MillisecondsPerMinute;
            return new ClockReading(wall, sinceBoot, _bootId.Value);
        }

        private static long? ReadBootId()
        {
            var size = (uint)sizeof(uint);
            var status = RegGetValueW(LocalMachine, PrefetchKey, BootIdValue, RestrictDword, IntPtr.Zero, out var value, ref size);
            return status == 0 ? value : (long?)null;
        }

        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegGetValueW(UIntPtr key, string subKey, string valueName, uint flags, IntPtr type, out uint data, ref uint dataSize);
    }
}
