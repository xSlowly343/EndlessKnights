using System;
using System.Collections.Generic;
using Game.Core.Text;

namespace Game.Core.Content
{
    public sealed class ChangeLog
    {
        public const string FileName = "changelog.json";
        public const int SupportedVersion = 1;
        public const int MaxLines = 7;
        public const int MaxLineLength = 60;

        private ChangeLog(IReadOnlyList<ChangeLogEntry> entries)
        {
            Entries = entries;
        }

        public IReadOnlyList<ChangeLogEntry> Entries { get; }

        public ChangeLogEntry? Latest => Entries.Count > 0 ? Entries[0] : null;

        public static ChangeLog Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var entries = new List<ChangeLogEntry>();
            foreach (var item in root.Field("entries").Items())
            {
                var lines = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
                var byLanguage = item.Field("lines");
                foreach (var language in byLanguage.FieldNames())
                    lines[language] = ReadLines(byLanguage.Field(language));
                if (!lines.ContainsKey(StringTable.DefaultLanguage))
                    throw byLanguage.Error($"lines for '{StringTable.DefaultLanguage}' are required");
                entries.Add(new ChangeLogEntry(item.Field("game").AsString(), lines));
            }
            return new ChangeLog(entries);
        }

        private static IReadOnlyList<string> ReadLines(ContentNode node)
        {
            var lines = new List<string>();
            foreach (var line in node.Items())
            {
                var text = line.AsString();
                if (text.Length == 0 || text.Length > MaxLineLength)
                    throw line.Error($"must be 1 to {MaxLineLength} characters to fit the card");
                lines.Add(text);
            }
            if (lines.Count == 0 || lines.Count > MaxLines)
                throw node.Error($"must have 1 to {MaxLines} lines");
            return lines;
        }
    }

    public sealed class ChangeLogEntry
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _lines;

        public ChangeLogEntry(string game, IReadOnlyDictionary<string, IReadOnlyList<string>> lines)
        {
            Game = game;
            _lines = lines;
        }

        public string Game { get; }

        public IReadOnlyList<string> Lines(string language) =>
            _lines.TryGetValue(language, out var lines) ? lines : _lines[StringTable.DefaultLanguage];
    }
}
