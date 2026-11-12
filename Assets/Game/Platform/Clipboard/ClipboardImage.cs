using System;
using System.Runtime.InteropServices;

namespace Game.Platform.Windows.Clipboard
{
    public static class ClipboardImage
    {
        public const int HeaderBytes = 40;
        private const int BytesPerPixel = 4;
        private const uint FormatDib = 8;
        private const uint MemoryMoveable = 0x0002;

        public static byte[] DibOf(byte[] rgba, int width, int height)
        {
            if (width <= 0 || height <= 0 || rgba.Length != width * height * BytesPerPixel)
                throw new ArgumentException("the pixels do not match the size", nameof(rgba));
            var dib = new byte[HeaderBytes + rgba.Length];
            WriteInt(dib, 0, HeaderBytes);
            WriteInt(dib, 4, width);
            WriteInt(dib, 8, height);
            dib[12] = 1;
            dib[14] = 8 * BytesPerPixel;
            WriteInt(dib, 20, rgba.Length);
            for (var i = 0; i < rgba.Length; i += BytesPerPixel)
            {
                var at = HeaderBytes + i;
                dib[at] = rgba[i + 2];
                dib[at + 1] = rgba[i + 1];
                dib[at + 2] = rgba[i];
                dib[at + 3] = byte.MaxValue;
            }
            return dib;
        }

        public static bool Copy(byte[] rgba, int width, int height)
        {
            var dib = DibOf(rgba, width, height);
            var memory = GlobalAlloc(MemoryMoveable, new UIntPtr((uint)dib.Length));
            if (memory == IntPtr.Zero)
                return false;
            var target = GlobalLock(memory);
            if (target == IntPtr.Zero)
            {
                GlobalFree(memory);
                return false;
            }
            Marshal.Copy(dib, 0, target, dib.Length);
            GlobalUnlock(memory);
            if (!OpenClipboard(IntPtr.Zero))
            {
                GlobalFree(memory);
                return false;
            }
            EmptyClipboard();
            var placed = SetClipboardData(FormatDib, memory) != IntPtr.Zero;
            CloseClipboard();
            if (!placed)
                GlobalFree(memory);
            return placed;
        }

        private static void WriteInt(byte[] target, int at, int value)
        {
            target[at] = (byte)value;
            target[at + 1] = (byte)(value >> 8);
            target[at + 2] = (byte)(value >> 16);
            target[at + 3] = (byte)(value >> 24);
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenClipboard(IntPtr owner);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint format, IntPtr memory);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseClipboard();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr memory);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr memory);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalFree(IntPtr memory);
    }
}
