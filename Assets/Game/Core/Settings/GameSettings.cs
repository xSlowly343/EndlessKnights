using Game.Core.Math;

namespace Game.Core.Settings
{
    public sealed class GameSettings
    {
        public int FrameRate { get; set; } = 60;
        public bool AlwaysOnTop { get; set; } = true;
        public bool ClickThrough { get; set; } = true;
        public bool ShowInputDebug { get; set; } = true;
        public bool Paused { get; set; }
        public double StripOffset { get; set; }
        public double StripScale { get; set; } = 1;
        public NumberFormatSettings NumberFormat { get; set; } = NumberFormatSettings.Default;
        public double MasterVolume { get; set; } = 0.8;
        public double MusicVolume { get; set; } = 0.5;
        public double HitVolume { get; set; }
        public double SkillVolume { get; set; } = 0.6;
        public double InterfaceVolume { get; set; } = 0.6;
        public double NotificationVolume { get; set; } = 0.6;
        public bool CountKeys { get; set; } = true;
        public bool CountMouseMovement { get; set; } = true;
        public bool CountClicks { get; set; } = true;
        public bool CountWheel { get; set; } = true;
        public string Language { get; set; } = "ru";
        public bool InputConsentGiven { get; set; }
        public bool TelemetryConsentGiven { get; set; }
        public bool ErrorReportConsentGiven { get; set; }
        public int AcceptedPolicyVersion { get; set; }
    }
}
