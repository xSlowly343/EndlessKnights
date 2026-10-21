using System;
using System.Collections.Generic;
using System.IO;

namespace Game.Platform.Windows.Input
{
    public sealed class InputBlacklist
    {
        public static readonly IReadOnlyList<string> DefaultExecutables = new[]
        {
            "KeePass.exe",
            "KeePassXC.exe",
            "1Password.exe",
            "Bitwarden.exe",
            "Dashlane.exe",
            "LastPass.exe",
            "VALORANT-Win64-Shipping.exe"
        };

        private readonly HashSet<string> _executables;

        public InputBlacklist(IEnumerable<string> executables)
        {
            _executables = new HashSet<string>(executables, StringComparer.OrdinalIgnoreCase);
        }

        public static InputBlacklist Default() => new InputBlacklist(DefaultExecutables);

        public bool IsBlocked(string? executablePath)
        {
            if (string.IsNullOrEmpty(executablePath))
                return false;
            return _executables.Contains(Path.GetFileName(executablePath));
        }
    }
}
