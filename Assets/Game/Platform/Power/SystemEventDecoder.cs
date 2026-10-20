namespace Game.Platform.Windows.Power
{
    public static class SystemEventDecoder
    {
        public const uint PowerBroadcastMessage = 0x0218;
        public const uint SessionChangeMessage = 0x02B1;

        private const long PowerSuspend = 0x0004;
        private const long PowerResumeSuspend = 0x0007;
        private const long PowerResumeAutomatic = 0x0012;
        private const long SessionLock = 0x0007;
        private const long SessionUnlock = 0x0008;

        public static bool TryDecode(uint message, long wParam, out SystemEvent systemEvent)
        {
            systemEvent = default;
            if (message == PowerBroadcastMessage)
            {
                if (wParam == PowerSuspend)
                    systemEvent = SystemEvent.Suspend;
                else if (wParam == PowerResumeAutomatic || wParam == PowerResumeSuspend)
                    systemEvent = SystemEvent.Resume;
                else
                    return false;
                return true;
            }

            if (message == SessionChangeMessage)
            {
                if (wParam == SessionLock)
                    systemEvent = SystemEvent.SessionLocked;
                else if (wParam == SessionUnlock)
                    systemEvent = SystemEvent.SessionUnlocked;
                else
                    return false;
                return true;
            }

            return false;
        }
    }
}
