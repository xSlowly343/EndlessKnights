using System;
using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Json;
using Game.Core.Math;

namespace Game.Core.Settings
{
    public static class SettingsCodec
    {
        public const string FileName = "settings.json";
        public const int CurrentVersion = 1;

        private static readonly int[] FrameRates = { 30, 60, 144 };
        private static readonly string[] Languages = { "ru", "en" };

        private static readonly Dictionary<string, NumberFormat> Formats = new Dictionary<string, NumberFormat>(StringComparer.Ordinal)
        {
            ["letters"] = NumberFormat.Letters,
            ["scientific"] = NumberFormat.Scientific,
            ["engineering"] = NumberFormat.Engineering,
            ["full"] = NumberFormat.Full
        };

        public static string Encode(GameSettings settings)
        {
            var number = settings.NumberFormat;
            var fields = new Dictionary<string, JsonValue>
            {
                ["version"] = JsonValue.Of(CurrentVersion),
                ["frameRate"] = JsonValue.Of(settings.FrameRate),
                ["alwaysOnTop"] = JsonValue.Of(settings.AlwaysOnTop),
                ["clickThrough"] = JsonValue.Of(settings.ClickThrough),
                ["showInputDebug"] = JsonValue.Of(settings.ShowInputDebug),
                ["paused"] = JsonValue.Of(settings.Paused),
                ["stripOffset"] = JsonValue.Of(settings.StripOffset),
                ["stripScale"] = JsonValue.Of(settings.StripScale),
                ["numberFormat"] = JsonValue.Of(NameOf(number.Format)),
                ["decimals"] = JsonValue.Of(number.Decimals),
                ["thousandsSeparator"] = JsonValue.Of(number.ThousandsSeparator),
                ["decimalSeparator"] = JsonValue.Of(number.DecimalSeparator),
                ["volume"] = JsonValue.Object(new Dictionary<string, JsonValue>
                {
                    ["master"] = JsonValue.Of(settings.MasterVolume),
                    ["music"] = JsonValue.Of(settings.MusicVolume),
                    ["hits"] = JsonValue.Of(settings.HitVolume),
                    ["skills"] = JsonValue.Of(settings.SkillVolume),
                    ["interface"] = JsonValue.Of(settings.InterfaceVolume),
                    ["notifications"] = JsonValue.Of(settings.NotificationVolume)
                }),
                ["input"] = JsonValue.Object(new Dictionary<string, JsonValue>
                {
                    ["keys"] = JsonValue.Of(settings.CountKeys),
                    ["mouseMovement"] = JsonValue.Of(settings.CountMouseMovement),
                    ["clicks"] = JsonValue.Of(settings.CountClicks),
                    ["wheel"] = JsonValue.Of(settings.CountWheel),
                    ["consentGiven"] = JsonValue.Of(settings.InputConsentGiven)
                }),
                ["language"] = JsonValue.Of(settings.Language),
                ["privacy"] = JsonValue.Object(new Dictionary<string, JsonValue>
                {
                    ["policyVersion"] = JsonValue.Of(settings.AcceptedPolicyVersion)
                })
            };
            return JsonWriter.Write(JsonValue.Object(fields)) + "\n";
        }

        public static GameSettings Decode(string text, List<string> problems)
        {
            var settings = new GameSettings();
            ContentNode root;
            try
            {
                root = ContentNode.Root(text, FileName);
            }
            catch (ContentException error)
            {
                problems.Add(error.Message);
                return settings;
            }

            settings.FrameRate = Read(root, "frameRate", node => Pick(node, node.AsInt(), FrameRates), settings.FrameRate, problems);
            settings.AlwaysOnTop = Read(root, "alwaysOnTop", node => node.AsBool(), settings.AlwaysOnTop, problems);
            settings.ClickThrough = Read(root, "clickThrough", node => node.AsBool(), settings.ClickThrough, problems);
            settings.ShowInputDebug = Read(root, "showInputDebug", node => node.AsBool(), settings.ShowInputDebug, problems);
            settings.Paused = Read(root, "paused", node => node.AsBool(), settings.Paused, problems);
            settings.StripOffset = Read(root, "stripOffset", node => node.AsNumber(-1e6, 1e6), settings.StripOffset, problems);
            settings.StripScale = Read(root, "stripScale", node => node.AsNumber(0.75, 2), settings.StripScale, problems);
            settings.NumberFormat = new NumberFormatSettings(
                Read(root, "numberFormat", node => Pick(node, node.AsString(), Formats), settings.NumberFormat.Format, problems),
                Read(root, "decimals", node => node.AsInt(0, NumberFormatSettings.MaxDecimals), settings.NumberFormat.Decimals, problems),
                Read(root, "thousandsSeparator", node => Separator(node), settings.NumberFormat.ThousandsSeparator, problems),
                Read(root, "decimalSeparator", node => Separator(node), settings.NumberFormat.DecimalSeparator, problems));
            settings.Language = Read(root, "language", node => Pick(node, node.AsString(), Languages), settings.Language, problems);

            if (root.HasField("volume"))
            {
                var volume = root.Field("volume");
                settings.MasterVolume = Read(volume, "master", Volume, settings.MasterVolume, problems);
                settings.MusicVolume = Read(volume, "music", Volume, settings.MusicVolume, problems);
                settings.HitVolume = Read(volume, "hits", Volume, settings.HitVolume, problems);
                settings.SkillVolume = Read(volume, "skills", Volume, settings.SkillVolume, problems);
                settings.InterfaceVolume = Read(volume, "interface", Volume, settings.InterfaceVolume, problems);
                settings.NotificationVolume = Read(volume, "notifications", Volume, settings.NotificationVolume, problems);
            }
            if (root.HasField("input"))
            {
                var input = root.Field("input");
                settings.CountKeys = Read(input, "keys", node => node.AsBool(), settings.CountKeys, problems);
                settings.CountMouseMovement = Read(input, "mouseMovement", node => node.AsBool(), settings.CountMouseMovement, problems);
                settings.CountClicks = Read(input, "clicks", node => node.AsBool(), settings.CountClicks, problems);
                settings.CountWheel = Read(input, "wheel", node => node.AsBool(), settings.CountWheel, problems);
                settings.InputConsentGiven = Read(input, "consentGiven", node => node.AsBool(), settings.InputConsentGiven, problems);
            }
            if (root.HasField("privacy"))
            {
                var privacy = root.Field("privacy");
                settings.AcceptedPolicyVersion = Read(privacy, "policyVersion", node => node.AsInt(0), settings.AcceptedPolicyVersion, problems);
            }
            return settings;
        }

        private static T Read<T>(ContentNode parent, string name, Func<ContentNode, T> read, T fallback, List<string> problems)
        {
            if (!parent.HasField(name))
                return fallback;
            try
            {
                return read(parent.Field(name));
            }
            catch (ContentException error)
            {
                problems.Add(error.Message);
                return fallback;
            }
        }

        private static double Volume(ContentNode node) => node.AsNumber(0, 1);

        private static string Separator(ContentNode node)
        {
            var text = node.AsString();
            if (text.Length != 1)
                throw node.Error("a separator is one character");
            return text;
        }

        private static int Pick(ContentNode node, int value, int[] allowed)
        {
            if (Array.IndexOf(allowed, value) < 0)
                throw node.Error($"{value} is not one of {string.Join(", ", allowed)}");
            return value;
        }

        private static string Pick(ContentNode node, string value, string[] allowed)
        {
            if (Array.IndexOf(allowed, value) < 0)
                throw node.Error($"'{value}' is not one of {string.Join(", ", allowed)}");
            return value;
        }

        private static NumberFormat Pick(ContentNode node, string value, Dictionary<string, NumberFormat> names)
        {
            if (!names.TryGetValue(value, out var format))
                throw node.Error($"unknown number format '{value}'");
            return format;
        }

        private static string NameOf(NumberFormat format)
        {
            foreach (var pair in Formats)
            {
                if (pair.Value == format)
                    return pair.Key;
            }
            return "letters";
        }
    }
}
