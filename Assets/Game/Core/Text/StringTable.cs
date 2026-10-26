using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Core.Content;

namespace Game.Core.Text
{
    public sealed class StringTable
    {
        public const string FileName = "strings.json";
        public const int SupportedVersion = 1;
        public const string DefaultLanguage = "ru";

        private static readonly Regex Placeholder = new Regex(@"\{(\d+)\}", RegexOptions.CultureInvariant);

        private readonly Dictionary<string, Dictionary<string, string>> _byLanguage;

        private StringTable(IReadOnlyList<string> languages, Dictionary<string, Dictionary<string, string>> byLanguage)
        {
            Languages = languages;
            _byLanguage = byLanguage;
        }

        public static StringTable Empty { get; } = new StringTable(new[] { DefaultLanguage },
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal) { [DefaultLanguage] = new Dictionary<string, string>(StringComparer.Ordinal) });

        public IReadOnlyList<string> Languages { get; }

        public int Count => _byLanguage[Languages[0]].Count;

        public bool Supports(string language) => _byLanguage.ContainsKey(language);

        public bool TryGet(string language, string key, out string text)
        {
            text = string.Empty;
            return _byLanguage.TryGetValue(language, out var strings) && strings.TryGetValue(key, out text!);
        }

        public static StringTable Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var languages = ReadLanguages(root.Field("languages"));
            var byLanguage = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (var language in languages)
                byLanguage[language] = new Dictionary<string, string>(StringComparer.Ordinal);

            var strings = root.Field("strings");
            foreach (var key in strings.FieldNames())
                ReadEntry(strings.Field(key), key, languages, byLanguage);
            return new StringTable(languages, byLanguage);
        }

        private static List<string> ReadLanguages(ContentNode node)
        {
            var languages = new List<string>();
            foreach (var item in node.Items())
            {
                var language = item.AsString();
                if (languages.Contains(language))
                    throw item.Error($"language '{language}' is listed twice");
                languages.Add(language);
            }
            if (!languages.Contains(DefaultLanguage))
                throw node.Error($"the list must contain '{DefaultLanguage}'");
            return languages;
        }

        private static void ReadEntry(ContentNode entry, string key, List<string> languages, Dictionary<string, Dictionary<string, string>> byLanguage)
        {
            string? placeholders = null;
            foreach (var name in entry.FieldNames())
            {
                if (!byLanguage.ContainsKey(name))
                    throw entry.Field(name).Error($"language '{name}' is not in the languages list");
            }
            foreach (var language in languages)
            {
                if (!entry.HasField(language))
                    throw entry.Error($"'{key}' has no '{language}' text");
                var text = entry.Field(language).AsString();
                if (text.Length == 0)
                    throw entry.Field(language).Error($"'{key}' has an empty '{language}' text");
                var found = PlaceholdersOf(text);
                if (placeholders == null)
                    placeholders = found;
                else if (placeholders != found)
                    throw entry.Field(language).Error($"'{key}' uses placeholders {found} in '{language}' but {placeholders} in '{languages[0]}'");
                byLanguage[language][key] = text;
            }
        }

        private static string PlaceholdersOf(string text)
        {
            var numbers = new SortedSet<int>();
            foreach (Match match in Placeholder.Matches(text))
                numbers.Add(int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
            return "{" + string.Join(",", numbers) + "}";
        }
    }
}
