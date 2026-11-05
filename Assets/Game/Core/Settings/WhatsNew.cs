using Game.Core.Content;

namespace Game.Core.Settings
{
    public static class WhatsNew
    {
        public static bool ShowOnStart(GameSettings settings, ChangeLog? changeLog, bool firstRun)
        {
            var latest = changeLog?.Latest;
            if (latest == null || settings.SeenChangeLog == latest.Game)
                return false;
            settings.SeenChangeLog = latest.Game;
            return !firstRun;
        }
    }
}
