using System.Collections.Generic;
using Game.Core.Math;
using Game.Core.Settings;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class SettingsScreen
    {
        public const int VolumeSteps = 10;

        public static readonly IReadOnlyList<int> FrameRates = new[] { 30, 60, 144 };
        public static readonly IReadOnlyList<string> Languages = new[] { "ru", "en" };

        private static readonly NumberFormat[] Formats = { NumberFormat.Letters, NumberFormat.Scientific, NumberFormat.Engineering, NumberFormat.Full };

        private static readonly SettingId[][] Tabs =
        {
            new[] { SettingId.Language, SettingId.AlwaysOnTop, SettingId.ResetPosition, SettingId.FrameRate, SettingId.NumberFormat, SettingId.Decimals, SettingId.InputConsent, SettingId.WhatsNew },
            new[] { SettingId.MasterVolume, SettingId.MusicVolume, SettingId.HitVolume, SettingId.SkillVolume, SettingId.InterfaceVolume, SettingId.NotificationVolume },
            new[] { SettingId.PrivacyPolicy, SettingId.Eula },
        };

        public static int TabCount => Tabs.Length;

        public static int MaxRows
        {
            get
            {
                var max = 0;
                foreach (var tab in Tabs)
                    max = System.Math.Max(max, tab.Length);
                return max;
            }
        }

        public static IReadOnlyList<SettingId> Rows(int tab) => Tabs[System.Math.Clamp(tab, 0, Tabs.Length - 1)];

        public static SettingKind KindOf(SettingId id) => id switch
        {
            SettingId.Paused or SettingId.AlwaysOnTop or SettingId.ClickThrough or SettingId.CountKeys or SettingId.CountMouseMovement
                or SettingId.CountClicks or SettingId.CountWheel or SettingId.ShowInputDebug => SettingKind.Toggle,
            SettingId.ResetPosition or SettingId.InputConsent or SettingId.WhatsNew or SettingId.PrivacyPolicy or SettingId.Eula => SettingKind.Action,
            _ => SettingKind.Choice
        };

        public static string Label(SettingId id) => Texts.Get("settings." + Key(id));

        public static string ActionLabel(SettingId id) => Texts.Get(id == SettingId.ResetPosition ? "settings.reset" : "settings.open");

        public static bool IsOn(SettingId id, GameSettings settings) => id switch
        {
            SettingId.Paused => settings.Paused,
            SettingId.AlwaysOnTop => settings.AlwaysOnTop,
            SettingId.ClickThrough => settings.ClickThrough,
            SettingId.CountKeys => settings.CountKeys,
            SettingId.CountMouseMovement => settings.CountMouseMovement,
            SettingId.CountClicks => settings.CountClicks,
            SettingId.CountWheel => settings.CountWheel,
            SettingId.ShowInputDebug => settings.ShowInputDebug,
            _ => false
        };

        public static string Value(SettingId id, GameSettings settings) => KindOf(id) switch
        {
            SettingKind.Toggle => Texts.Get(IsOn(id, settings) ? "settings.on" : "settings.off"),
            SettingKind.Action => ActionLabel(id),
            _ => ChoiceValue(id, settings)
        };

        public static bool Change(SettingId id, GameSettings settings, int step)
        {
            switch (id)
            {
                case SettingId.Language:
                    settings.Language = Cycle(Languages, settings.Language, step);
                    return true;
                case SettingId.Paused:
                    settings.Paused = !settings.Paused;
                    return true;
                case SettingId.AlwaysOnTop:
                    settings.AlwaysOnTop = !settings.AlwaysOnTop;
                    return true;
                case SettingId.ClickThrough:
                    settings.ClickThrough = !settings.ClickThrough;
                    return true;
                case SettingId.ResetPosition:
                    settings.StripOffset = 0;
                    return true;
                case SettingId.FrameRate:
                    settings.FrameRate = Cycle(FrameRates, settings.FrameRate, step);
                    return true;
                case SettingId.NumberFormat:
                    var number = settings.NumberFormat;
                    settings.NumberFormat = new NumberFormatSettings(Cycle(Formats, number.Format, step), number.Decimals, number.ThousandsSeparator, number.DecimalSeparator);
                    return true;
                case SettingId.Decimals:
                    var current = settings.NumberFormat;
                    var decimals = System.Math.Clamp(current.Decimals + step, 0, NumberFormatSettings.MaxDecimals);
                    if (decimals == current.Decimals)
                        return false;
                    settings.NumberFormat = new NumberFormatSettings(current.Format, decimals, current.ThousandsSeparator, current.DecimalSeparator);
                    return true;
                case SettingId.CountKeys:
                    settings.CountKeys = !settings.CountKeys;
                    return true;
                case SettingId.CountMouseMovement:
                    settings.CountMouseMovement = !settings.CountMouseMovement;
                    return true;
                case SettingId.CountClicks:
                    settings.CountClicks = !settings.CountClicks;
                    return true;
                case SettingId.CountWheel:
                    settings.CountWheel = !settings.CountWheel;
                    return true;
                case SettingId.ShowInputDebug:
                    settings.ShowInputDebug = !settings.ShowInputDebug;
                    return true;
                case SettingId.InputConsent:
                case SettingId.WhatsNew:
                case SettingId.PrivacyPolicy:
                case SettingId.Eula:
                    return true;
                default:
                    return ChangeVolume(id, settings, step);
            }
        }

        public static string Note(int tab) => tab switch
        {
            0 => Texts.Get("settings.credits"),
            1 => Texts.Get("settings.soundNote"),
            _ => string.Empty
        };

        public static string Version(string version) => Texts.Format("settings.version", version);

        public static string WhatsNewTitle(string version) => Texts.Format("settings.whatsNewTitle", version);

        public static string LanguageName(string language) => Texts.Get("settings.language." + language);

        private static string ChoiceValue(SettingId id, GameSettings settings) => id switch
        {
            SettingId.Language => LanguageName(settings.Language),
            SettingId.FrameRate => Texts.Format("settings.fps", settings.FrameRate),
            SettingId.NumberFormat => Texts.Get("settings.format." + FormatKey(settings.NumberFormat.Format)),
            SettingId.Decimals => settings.NumberFormat.Decimals.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => Texts.Format("battle.percent", (int)System.Math.Round(VolumeOf(id, settings) * 100))
        };

        private static bool ChangeVolume(SettingId id, GameSettings settings, int step)
        {
            var before = VolumeOf(id, settings);
            var steps = (int)System.Math.Round(before * VolumeSteps);
            var after = System.Math.Clamp(steps + step, 0, VolumeSteps) / (double)VolumeSteps;
            if (System.Math.Abs(after - before) < 1e-9)
                return false;
            switch (id)
            {
                case SettingId.MasterVolume: settings.MasterVolume = after; break;
                case SettingId.MusicVolume: settings.MusicVolume = after; break;
                case SettingId.HitVolume: settings.HitVolume = after; break;
                case SettingId.SkillVolume: settings.SkillVolume = after; break;
                case SettingId.InterfaceVolume: settings.InterfaceVolume = after; break;
                case SettingId.NotificationVolume: settings.NotificationVolume = after; break;
                default: return false;
            }
            return true;
        }

        private static double VolumeOf(SettingId id, GameSettings settings) => id switch
        {
            SettingId.MasterVolume => settings.MasterVolume,
            SettingId.MusicVolume => settings.MusicVolume,
            SettingId.HitVolume => settings.HitVolume,
            SettingId.SkillVolume => settings.SkillVolume,
            SettingId.InterfaceVolume => settings.InterfaceVolume,
            SettingId.NotificationVolume => settings.NotificationVolume,
            _ => 0
        };

        private static T Cycle<T>(IReadOnlyList<T> options, T current, int step)
        {
            var index = 0;
            for (var i = 0; i < options.Count; i++)
                if (EqualityComparer<T>.Default.Equals(options[i], current))
                    index = i;
            var next = ((index + step) % options.Count + options.Count) % options.Count;
            return options[next];
        }

        private static string FormatKey(NumberFormat format) => format.ToString().ToLowerInvariant();

        private static string Key(SettingId id)
        {
            var name = id.ToString();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
