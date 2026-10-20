using System;

namespace Game.Platform.Windows.Input
{
    public static class InjectedInputFilter
    {
        public static bool IsInjected(IntPtr device) => device == IntPtr.Zero;
    }
}
