using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Settings;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class SettingsScreen
    {
        public const int VolumeSteps = 10;

        public static readonly IReadOnlyList<int> FrameRates = new[] { 30, 60, 144 };
        public static readonly IReadOnlyList<string> Languages = new[] { "ru", "en" };

        private static readonly NumberFormat[] Formats = { NumberFormat.Letters, NumberFormat.Scientific, NumberFormat.Engineering, NumberFormat.Full };

        public const int SliderSteps = 20;
        public const int LookTab = 4;

        private static readonly SettingGroup[][] TabGroups =
        {
            new[]
            {
                new SettingGroup("numbers", new[] { SettingId.Language, SettingId.NumberFormat, SettingId.Decimals }),
                new SettingGroup("strikes", new[] { SettingId.HeroStrikeOpacity, SettingId.AllyStrikeOpacity }),
                new SettingGroup("build", new[] { SettingId.BuildCopy, SettingId.BuildPaste }),
            },
            new[]
            {
                new SettingGroup("window", new[] { SettingId.AlwaysOnTop, SettingId.ResetPosition }),
                new SettingGroup("picture", new[] { SettingId.FrameRate, SettingId.CompanionStrip }),
            },
            new[]
            {
                new SettingGroup("volume", new[] { SettingId.MasterVolume }),
                new SettingGroup("volumeKinds", new[] { SettingId.MusicVolume, SettingId.HitVolume, SettingId.SkillVolume, SettingId.InterfaceVolume, SettingId.NotificationVolume }),
            },
            new[]
            {
                new SettingGroup("about", new[] { SettingId.WhatsNew, SettingId.InputConsent, SettingId.Coupon }),
            },
            new[]
            {
                new SettingGroup("look", new[] { SettingId.HeroCloak, SettingId.HeroEyes, SettingId.HeroStrike }),
            },
        };

        private static readonly SettingId[][] Tabs = Flatten(TabGroups);
        private static readonly GameSettings Defaults = new GameSettings();

        public static int TabCount => LookTab;

        public static int MaxGroups
        {
            get
            {
                var max = 0;
                foreach (var tab in TabGroups)
                    max = System.Math.Max(max, tab.Length);
                return max;
            }
        }

        public static IReadOnlyList<SettingGroup> Groups(int tab) => TabGroups[System.Math.Clamp(tab, 0, TabGroups.Length - 1)];

        private static SettingId[][] Flatten(SettingGroup[][] tabs)
        {
            var flat = new SettingId[tabs.Length][];
            for (var i = 0; i < tabs.Length; i++)
            {
                var rows = new List<SettingId>();
                foreach (var group in tabs[i])
                    rows.AddRange(group.Rows);
                flat[i] = rows.ToArray();
            }
            return flat;
        }

        public static HeroLookChooser? Looks { get; private set; }

        public static void UseLooks(HeroLookChooser? looks) => Looks = looks;

        public static string LookSlotOf(SettingId id) => id switch
        {
            SettingId.HeroCloak => CosmeticCatalog.CloakSlot,
            SettingId.HeroEyes => CosmeticCatalog.EyesSlot,
            SettingId.HeroStrike => CosmeticCatalog.StrikeSlot,
            _ => string.Empty
        };

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
            SettingId.ResetPosition or SettingId.InputConsent or SettingId.WhatsNew or SettingId.Coupon or SettingId.BuildCopy or SettingId.BuildPaste => SettingKind.Action,
            SettingId.Language or SettingId.FrameRate or SettingId.NumberFormat or SettingId.CompanionStrip => SettingKind.Segments,
            SettingId.MasterVolume or SettingId.MusicVolume or SettingId.HitVolume or SettingId.SkillVolume or SettingId.InterfaceVolume
                or SettingId.NotificationVolume or SettingId.HeroStrikeOpacity or SettingId.AllyStrikeOpacity => SettingKind.Slider,
            _ => SettingKind.Choice
        };

        public static string Hint(SettingId id) => Texts.Get("settings.hint." + Key(id));

        public static string HintLine(SettingId id, GameSettings settings)
        {
            var preview = Preview(id, settings);
            return preview.Length > 0 ? Texts.Format("settings.preview", preview) : Hint(id);
        }

        public static double PreviewAlpha(SettingId id, GameSettings settings) =>
            id == SettingId.HeroStrikeOpacity || id == SettingId.AllyStrikeOpacity ? VolumeOf(id, settings) : -1;

        public static string IconKey(SettingId id) => Key(id);

        public static bool AnyChanged(int tab, GameSettings settings)
        {
            if (!CanReset(tab))
                return false;
            foreach (var id in Rows(tab))
                if (!IsDefault(id, settings))
                    return true;
            return false;
        }

        public static int OptionCount(SettingId id) => id switch
        {
            SettingId.Language => Languages.Count,
            SettingId.FrameRate => FrameRates.Count,
            SettingId.NumberFormat => Formats.Length,
            SettingId.CompanionStrip => SettingsCodec.CompanionStripModes.Length,
            _ => 0
        };

        public static string OptionLabel(SettingId id, int option) => id switch
        {
            SettingId.Language => Texts.Get("settings.language.short." + Languages[option]),
            SettingId.FrameRate => FrameRates[option].ToString(System.Globalization.CultureInfo.InvariantCulture),
            SettingId.NumberFormat => Texts.Get("settings.format.short." + FormatKey(Formats[option])),
            SettingId.CompanionStrip => Texts.Get("settings.companionStrip." + SettingsCodec.CompanionStripModes[option]),
            _ => string.Empty
        };

        public static int Selected(SettingId id, GameSettings settings) => id switch
        {
            SettingId.Language => IndexOf(Languages, settings.Language),
            SettingId.FrameRate => IndexOf(FrameRates, settings.FrameRate),
            SettingId.NumberFormat => System.Array.IndexOf(Formats, settings.NumberFormat.Format),
            SettingId.CompanionStrip => IndexOf(SettingsCodec.CompanionStripModes, settings.CompanionStrip),
            _ => -1
        };

        public static bool Select(SettingId id, GameSettings settings, int option)
        {
            if (option < 0 || option >= OptionCount(id) || option == Selected(id, settings))
                return false;
            switch (id)
            {
                case SettingId.Language:
                    settings.Language = Languages[option];
                    return true;
                case SettingId.FrameRate:
                    settings.FrameRate = FrameRates[option];
                    return true;
                case SettingId.NumberFormat:
                    var number = settings.NumberFormat;
                    settings.NumberFormat = new NumberFormatSettings(Formats[option], number.Decimals, number.ThousandsSeparator, number.DecimalSeparator);
                    return true;
                case SettingId.CompanionStrip:
                    settings.CompanionStrip = SettingsCodec.CompanionStripModes[option];
                    return true;
                default:
                    return false;
            }
        }

        public static double Fraction(SettingId id, GameSettings settings) => KindOf(id) == SettingKind.Slider ? VolumeOf(id, settings) : 0;

        public static bool SetFraction(SettingId id, GameSettings settings, double fraction)
        {
            if (KindOf(id) != SettingKind.Slider)
                return false;
            var snapped = System.Math.Round(System.Math.Clamp(fraction, 0, 1) * SliderSteps, System.MidpointRounding.AwayFromZero) / SliderSteps;
            return SetVolume(id, settings, snapped);
        }

        public static string Preview(SettingId id, GameSettings settings) => id switch
        {
            SettingId.NumberFormat or SettingId.Decimals => NumberFormatter.Format(PreviewNumber, settings.NumberFormat),
            _ => string.Empty
        };

        public static bool IsDefault(SettingId id, GameSettings settings) => KindOf(id) switch
        {
            SettingKind.Toggle => IsOn(id, settings) == IsOn(id, Defaults),
            SettingKind.Action => true,
            SettingKind.Slider => System.Math.Abs(VolumeOf(id, settings) - VolumeOf(id, Defaults)) < 1e-9,
            SettingKind.Segments => Selected(id, settings) == Selected(id, Defaults),
            _ => id != SettingId.Decimals || settings.NumberFormat.Decimals == Defaults.NumberFormat.Decimals
        };

        public static bool CanReset(int tab) => tab >= 0 && tab < LookTab && tab != AboutTab;

        public static void ResetTab(int tab, GameSettings settings, List<SettingId> changed)
        {
            if (!CanReset(tab))
                return;
            foreach (var id in Rows(tab))
            {
                if (IsDefault(id, settings))
                    continue;
                switch (KindOf(id))
                {
                    case SettingKind.Toggle:
                        Change(id, settings, 1);
                        break;
                    case SettingKind.Slider:
                        SetVolume(id, settings, VolumeOf(id, Defaults));
                        break;
                    case SettingKind.Segments:
                        Select(id, settings, Selected(id, Defaults));
                        break;
                    default:
                        var number = settings.NumberFormat;
                        settings.NumberFormat = new NumberFormatSettings(number.Format, Defaults.NumberFormat.Decimals, number.ThousandsSeparator, number.DecimalSeparator);
                        break;
                }
                changed.Add(id);
            }
        }

        public static string ResetLabel => Texts.Get("settings.resetTab");

        public static string GroupTitle(SettingGroup group) => Texts.Get("settings.group." + group.Key);

        public static string Credits => Texts.Get("settings.credits");

        public const int AboutTab = 3;

        private static readonly BigNumber PreviewNumber = BigNumber.FromDouble(1234567.891);

        private static int IndexOf<T>(IReadOnlyList<T> options, T value)
        {
            for (var i = 0; i < options.Count; i++)
                if (EqualityComparer<T>.Default.Equals(options[i], value))
                    return i;
            return -1;
        }

        public static string Label(SettingId id) => Texts.Get("settings." + Key(id));

        public static string ActionLabel(SettingId id) => Texts.Get(id switch
        {
            SettingId.ResetPosition => "settings.reset",
            SettingId.Coupon or SettingId.BuildPaste => "settings.paste",
            SettingId.BuildCopy => "settings.copy",
            _ => "settings.open"
        });

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
                case SettingId.CompanionStrip:
                    settings.CompanionStrip = Cycle(SettingsCodec.CompanionStripModes, settings.CompanionStrip, step);
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
                case SettingId.Coupon:
                case SettingId.BuildCopy:
                case SettingId.BuildPaste:
                    return true;
                case SettingId.HeroCloak:
                case SettingId.HeroEyes:
                case SettingId.HeroStrike:
                    return Looks != null && Looks.Step(LookSlotOf(id), step);
                default:
                    return ChangeVolume(id, settings, step);
            }
        }

        public static string Note(int tab) => tab switch
        {
            2 => Texts.Get("settings.soundNote"),
            AboutTab => Texts.Get("settings.credits"),
            LookTab => Texts.Get("settings.lookNote"),
            _ => string.Empty
        };

        public static string Version(string version) => Texts.Format("settings.version", version);

        public static string WhatsNewTitle(string version) => Texts.Format("settings.whatsNewTitle", version);

        public static StripMode StripModeOf(GameSettings settings) => settings.CompanionStrip switch
        {
            "homeOnly" => StripMode.HomeOnly,
            "off" => StripMode.Off,
            _ => StripMode.Always
        };

        public static string LanguageName(string language) => Texts.Get("settings.language." + language);

        private static string ChoiceValue(SettingId id, GameSettings settings) => id switch
        {
            SettingId.Language => LanguageName(settings.Language),
            SettingId.CompanionStrip => Texts.Get("settings.companionStrip." + settings.CompanionStrip),
            SettingId.FrameRate => Texts.Format("settings.fps", settings.FrameRate),
            SettingId.NumberFormat => Texts.Get("settings.format." + FormatKey(settings.NumberFormat.Format)),
            SettingId.Decimals => settings.NumberFormat.Decimals.ToString(System.Globalization.CultureInfo.InvariantCulture),
            SettingId.HeroCloak or SettingId.HeroEyes or SettingId.HeroStrike => Looks?.Label(LookSlotOf(id), settings.Language == "ru") ?? string.Empty,
            _ => Texts.Format("battle.percent", (int)System.Math.Round(VolumeOf(id, settings) * 100))
        };

        private static bool ChangeVolume(SettingId id, GameSettings settings, int step)
        {
            var before = VolumeOf(id, settings);
            var steps = (int)System.Math.Round(before * VolumeSteps);
            var after = System.Math.Clamp(steps + step, 0, VolumeSteps) / (double)VolumeSteps;
            return SetVolume(id, settings, after);
        }

        private static bool SetVolume(SettingId id, GameSettings settings, double after)
        {
            if (System.Math.Abs(after - VolumeOf(id, settings)) < 1e-9)
                return false;
            switch (id)
            {
                case SettingId.MasterVolume: settings.MasterVolume = after; break;
                case SettingId.MusicVolume: settings.MusicVolume = after; break;
                case SettingId.HitVolume: settings.HitVolume = after; break;
                case SettingId.SkillVolume: settings.SkillVolume = after; break;
                case SettingId.InterfaceVolume: settings.InterfaceVolume = after; break;
                case SettingId.NotificationVolume: settings.NotificationVolume = after; break;
                case SettingId.HeroStrikeOpacity: settings.HeroStrikeOpacity = after; break;
                case SettingId.AllyStrikeOpacity: settings.AllyStrikeOpacity = after; break;
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
            SettingId.HeroStrikeOpacity => settings.HeroStrikeOpacity,
            SettingId.AllyStrikeOpacity => settings.AllyStrikeOpacity,
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
