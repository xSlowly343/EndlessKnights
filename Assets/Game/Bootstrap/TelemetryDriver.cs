using System;
using Game.Core.Clock;
using Game.Core.Settings;
using Game.Core.Sim;
using Game.Core.Telemetry;
using Game.Core.Ui;
using Game.Presentation.Ui;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class TelemetryDriver : MonoBehaviour
    {
        private const float ObserveSeconds = 60f;

        private TelemetryService _service = null!;
        private GameLoop _loop = null!;
        private GameSettings _settings = null!;
        private float _sinceObserve;

        public static TelemetryDriver Attach(GameObject host, GameLoop loop, GameSettings settings, SettingsPanel settingsPanel)
        {
            var driver = host.AddComponent<TelemetryDriver>();
            driver._loop = loop;
            driver._settings = settings;
            driver._service = new TelemetryService(new TelemetryStore(SaveDriver.SaveDirectory), NoTelemetrySink.Instance, Application.version);
            driver._service.Problem += problem => Debug.LogWarning("Telemetry: " + problem);
            driver.ApplyConsent();
            settingsPanel.Changed += driver.OnSettingChanged;
            return driver;
        }

        private static long Today => LocalDay.Of(DateTimeOffset.Now);

        private void OnSettingChanged(SettingId id)
        {
            if (id == SettingId.Telemetry)
                ApplyConsent();
        }

        private void ApplyConsent() =>
            _service.UseConsent(_settings.TelemetryConsentGiven, Today, _loop.SessionInput, TelemetryProgress.From(_loop.State));

        private void Update()
        {
            if (!_service.Enabled)
                return;
            _sinceObserve += Time.unscaledDeltaTime;
            if (_sinceObserve < ObserveSeconds)
                return;
            _sinceObserve = 0f;
            _service.Observe(Today, _loop.SessionInput, TelemetryProgress.From(_loop.State));
            _service.Flush();
        }

        private void OnApplicationQuit()
        {
            _service.EndSession(_loop.SessionInput, TelemetryProgress.From(_loop.State));
            _service.Flush();
        }
    }
}
