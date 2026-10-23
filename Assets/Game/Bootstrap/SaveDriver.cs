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
        private const string StudioFolder = "RiftWarden";
        private const string GameFolder = "Rift Warden";
        private const string PreviousStudioFolder = "EndlessKnights";
        private const string PreviousGameFolder = "Endless Knights";

        private readonly AutosaveTimer _autosave = new AutosaveTimer();
        private SaveStore _store = null!;
        private GameState _state = null!;
        private ClockStamp? _clock;

        public static string SaveDirectory => InAppData(StudioFolder, GameFolder);

        private static string PreviousSaveDirectory => InAppData(PreviousStudioFolder, PreviousGameFolder);

        private static string InAppData(string studio, string game) =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), studio, game);

        public static SaveFile? TryLoad(GameData data)
        {
            AdoptPreviousSave();
            var result = new SaveStore(SaveDirectory).Load(text => SaveCodec.Decode(text, data));
            foreach (var problem in result.Problems)
                Debug.LogWarning("Save: " + problem);
            return result.Save;
        }

        private static void AdoptPreviousSave()
        {
            try
            {
                if (new SaveStore(SaveDirectory).AdoptFrom(PreviousSaveDirectory))
                    Debug.Log("Save: copied from " + PreviousSaveDirectory);
            }
            catch (IOException error)
            {
                Debug.LogWarning("Save: previous save not copied: " + error.Message);
            }
            catch (UnauthorizedAccessException error)
            {
                Debug.LogWarning("Save: previous save not copied: " + error.Message);
            }
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
