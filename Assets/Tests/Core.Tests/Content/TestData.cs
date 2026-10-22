using System.IO;

namespace Game.Core.Tests.Content
{
    public static class TestData
    {
        public static string ReadGameDataFile(string fileName) => ReadAboveCurrentDirectory(Path.Combine("Assets", "StreamingAssets", "Data", fileName));

        public static string ReadSaveFixture(string fileName) => ReadAboveCurrentDirectory(Path.Combine("Assets", "Tests", "Core.Tests", "Save", "Fixtures", fileName));

        private static string ReadAboveCurrentDirectory(string relativePath)
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(candidate))
                    return File.ReadAllText(candidate);
                directory = directory.Parent;
            }
            throw new FileNotFoundException($"{relativePath} not found above {Directory.GetCurrentDirectory()}");
        }
    }
}
