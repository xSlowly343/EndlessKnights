namespace Game.Core.Settings
{
    public static class PrivacyPolicy
    {
        public const int CurrentVersion = 1;

        public static bool NeedsAcceptance(GameSettings settings, int currentVersion = CurrentVersion) =>
            settings.AcceptedPolicyVersion < currentVersion;

        public static bool ShouldAskConsent(GameSettings settings, int currentVersion = CurrentVersion) =>
            !settings.InputConsentGiven || NeedsAcceptance(settings, currentVersion);

        public static bool WasUpdated(GameSettings settings, int currentVersion = CurrentVersion) =>
            settings.InputConsentGiven && NeedsAcceptance(settings, currentVersion);

        public static void Accept(GameSettings settings, int currentVersion = CurrentVersion)
        {
            settings.InputConsentGiven = true;
            if (settings.AcceptedPolicyVersion < currentVersion)
                settings.AcceptedPolicyVersion = currentVersion;
        }
    }
}
