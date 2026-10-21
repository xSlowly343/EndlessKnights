using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    public class InputPrivacyTests
    {
        private const string GameSourcesRoot = "Assets/Game";

        private static readonly Regex KeyCodeAccess = new Regex(@"\b(virtualKey|VirtualKey|MakeCode|makeCode|keyCode|KeyCode)\b");
        private static readonly Regex AsyncKeyState = new Regex(@"\bGetAsyncKeyState\b");

        private static readonly string[] FilesAllowedToSeeKeyCodes =
        {
            "Platform/Input/KeyboardEventFilter.cs",
            "Platform/Input/RawInputService.cs",
            "Platform/Input/UnityInputSource.cs",
            "Platform/Window/NativeMethods.cs"
        };

        private static readonly string[] FilesAllowedToPollKeyState =
        {
            "Platform/Window/NativeMethods.cs",
            "Platform/Window/PointerState.cs"
        };

        private static string[] SourcesMatching(Regex pattern) =>
            Directory.GetFiles(GameSourcesRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => pattern.IsMatch(File.ReadAllText(path)))
                .Select(path => path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/').Substring(GameSourcesRoot.Length + 1))
                .ToArray();

        [Test]
        public void KeyCodesStayInsideTheInputThreadFiles()
        {
            Assert.That(SourcesMatching(KeyCodeAccess), Is.SubsetOf(FilesAllowedToSeeKeyCodes));
        }

        [Test]
        public void KeyStatePollingIsOnlyUsedForTheMouseButton()
        {
            Assert.That(SourcesMatching(AsyncKeyState), Is.SubsetOf(FilesAllowedToPollKeyState));
        }

        [Test]
        public void CoreNeverSeesKeyCodes()
        {
            Assert.That(SourcesMatching(KeyCodeAccess).Where(path => path.StartsWith("Core/")), Is.Empty);
        }
    }
}
