using System;
using System.Text.RegularExpressions;

namespace Game.Core.Diagnostics
{
    public sealed class LogScrubber
    {
        public const string UserMarker = "%USER%";
        public const string KeyMarker = "[key]";
        public const int MaxLineLength = 500;
        public const int MinUserNameLength = 3;

        private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        private static readonly Regex WindowsUserFolder = new Regex(@"\b[a-z]:[\\/]+(?:users|documents and settings)[\\/]+[^\\/:*?""<>|\r\n]+", Options);
        private static readonly Regex UnixUserFolder = new Regex(@"(?<![\w.])/(?:home|users)/[^/\s]+", Options);
        private static readonly Regex KeyCode = new Regex(@"\b(?:keycode|virtualkey|vkey|vk|scancode|scan code|makecode)\b\s*[.=:]?\s*[\w]+", Options);

        private readonly Regex? _profile;
        private readonly Regex? _userName;

        public LogScrubber(string? userProfile = null, string? userName = null)
        {
            if (!string.IsNullOrWhiteSpace(userProfile))
                _profile = new Regex(SlashAgnostic(userProfile!.TrimEnd('\\', '/')), Options);
            if (!string.IsNullOrWhiteSpace(userName) && userName!.Trim().Length >= MinUserNameLength)
                _userName = new Regex(@"(?<!\w)" + Regex.Escape(userName.Trim()) + @"(?!\w)", Options);
        }

        public string Scrub(string line)
        {
            var text = line ?? string.Empty;
            if (_profile != null)
                text = _profile.Replace(text, UserMarker);
            text = WindowsUserFolder.Replace(text, UserMarker);
            text = UnixUserFolder.Replace(text, UserMarker);
            if (_userName != null)
                text = _userName.Replace(text, UserMarker);
            text = KeyCode.Replace(text, KeyMarker);
            return text.Length > MaxLineLength ? text.Substring(0, MaxLineLength) : text;
        }

        private static string SlashAgnostic(string path)
        {
            var parts = path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
                parts[i] = Regex.Escape(parts[i]);
            var prefix = path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("\\", StringComparison.Ordinal) ? @"[\\/]+" : string.Empty;
            return prefix + string.Join(@"[\\/]+", parts);
        }
    }
}
