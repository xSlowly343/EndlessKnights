using System.IO;
using System.Threading;
using Game.Core.Content;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class GameDataHotReload : MonoBehaviour
    {
        private const float SettleSeconds = 0.3f;

        private GameDataStore _store = null!;
        private FileSystemWatcher? _watcher;
        private int _changed;
        private float _reloadAt = float.MaxValue;

        public static void Attach(GameObject host, GameDataStore store)
        {
            var reload = host.AddComponent<GameDataHotReload>();
            reload._store = store;
            reload._watcher = new FileSystemWatcher(Path.GetDirectoryName(GameDataFiles.PathOf(StageTable.FileName))!, "*.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            reload._watcher.Changed += reload.OnFileChanged;
            reload._watcher.Created += reload.OnFileChanged;
            reload._watcher.Renamed += reload.OnFileChanged;
        }

        private void OnFileChanged(object sender, FileSystemEventArgs change) => Interlocked.Exchange(ref _changed, 1);

        private void Update()
        {
            if (Interlocked.Exchange(ref _changed, 0) == 1)
                _reloadAt = Time.unscaledTime + SettleSeconds;
            if (Time.unscaledTime < _reloadAt)
                return;
            _reloadAt = float.MaxValue;

            if (_store.TryReload())
                Debug.Log($"Game data reloaded (version {_store.Version})");
            else
                Debug.LogError($"Game data not reloaded, keeping the previous version: {_store.LastError}");
        }

        private void OnDestroy()
        {
            _watcher?.Dispose();
        }
    }
}
