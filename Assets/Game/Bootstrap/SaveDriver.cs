using System;
using System.IO;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Platform.Windows.Power;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class SaveDriver : MonoBehaviour
    {
        private const string StudioFolder = "EndlessKnights";
        private const string GameFolder = "Endless Knights";

        private readonly AutosaveTimer _autosave = new AutosaveTimer();
        private SaveStore _store = null!;
        private GameState _state = null!;
        private ClockStamp? _clock;

        public static string SaveDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), StudioFolder, GameFolder);

        public static SaveFile? TryLoad(GameData data)
        {
            var result = new SaveStore(SaveDirectory).Load(text => SaveCodec.Decode(text, data));
            foreach (var problem in result.Problems)
                Debug.LogWarning("Save: " + problem);
            return result.Save;
        }

        public static SaveDriver Attach(GameObject host, GameState state, ClockStamp? loadedClock)
        {
            var driver = host.AddComponent<SaveDriver>();
            driver._store = new SaveStore(SaveDirectory);
            driver._state = state;
            driver._clock = loadedClock;
            return driver;
        }

        public void SaveNow()
        {
            var now = SystemClock.Read();
            var stamp = _clock.HasValue ? _clock.Value.Next(now) : ClockStamp.First(now);
            try
            {
                Directory.CreateDirectory(_store.Directory);
                _store.Write(SaveCodec.Encode(_state, stamp));
                _clock = stamp;
            }
            catch (IOException error)
            {
                Debug.LogError("Save failed: " + error.Message);
            }
            catch (UnauthorizedAccessException error)
            {
                Debug.LogError("Save failed: " + error.Message);
            }
        }

        private void Update()
        {
            if (_autosave.Advance(Time.unscaledDeltaTime))
                SaveNow();
        }

        private void OnApplicationQuit() => SaveNow();
    }
}
