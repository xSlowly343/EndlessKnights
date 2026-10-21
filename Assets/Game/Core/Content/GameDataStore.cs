using System;

namespace Game.Core.Content
{
    public sealed class GameDataStore
    {
        private readonly Func<string, string> _readFile;

        public GameDataStore(Func<string, string> readFile)
        {
            _readFile = readFile;
            Current = GameData.Load(readFile);
            Version = 1;
        }

        public GameData Current { get; private set; }
        public int Version { get; private set; }
        public string? LastError { get; private set; }

        public event Action<GameData>? Reloaded;

        public bool TryReload()
        {
            try
            {
                Current = GameData.Load(_readFile);
            }
            catch (ContentException error)
            {
                LastError = error.Message;
                return false;
            }

            LastError = null;
            Version++;
            Reloaded?.Invoke(Current);
            return true;
        }
    }
}
