using System.IO;
using Game.Core.Content;
using UnityEngine;

namespace Game.Bootstrap
{
    public static class GameDataFiles
    {
        private const string DataFolder = "Data";

        public static string PathOf(string fileName) => Path.Combine(Application.streamingAssetsPath, DataFolder, fileName);

        public static string Read(string fileName) => File.ReadAllText(PathOf(fileName));

        public static InputCaps? TryLoadInputCaps()
        {
            try
            {
                return InputCaps.Load(Read(InputCaps.FileName));
            }
            catch (ContentException error)
            {
                Debug.LogError(error.Message);
                return null;
            }
            catch (IOException error)
            {
                Debug.LogError($"{InputCaps.FileName}: {error.Message}");
                return null;
            }
        }
    }
}
