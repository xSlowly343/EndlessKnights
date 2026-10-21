using System.IO;

namespace Game.Core.Tests.Content
{
    public static class TestData
    {
        public static string ReadGameDataFile(string fileName)
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "Data", fileName);
                if (File.Exists(candidate))
                    return File.ReadAllText(candidate);
                directory = directory.Parent;
            }
            throw new FileNotFoundException($"Game data file {fileName} not found above {Directory.GetCurrentDirectory()}");
        }
    }
}
