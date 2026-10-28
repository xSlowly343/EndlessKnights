using System.IO;
using Game.Core.Content;
using Game.Core.Diagnostics;
using Game.Core.Intake;
using Game.Core.Telemetry;
using Game.Net;
using UnityEngine;

namespace Game.Bootstrap
{
    public static class NetworkFile
    {
        public static NetworkConfig Load()
        {
            try
            {
                return NetworkConfig.Load(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, NetworkConfig.FileName)));
            }
            catch (ContentException error)
            {
                Debug.LogError(error.Message);
                return NetworkConfig.Offline;
            }
            catch (IOException)
            {
                return NetworkConfig.Offline;
            }
        }

        public static ITelemetrySink TelemetrySink(NetworkConfig config) =>
            config.HasServer ? new IntakeSink(new HttpIntakePost(), config.TelemetryUrl) : NoTelemetrySink.Instance;

        public static IErrorReportSink ErrorSink(NetworkConfig config) =>
            config.HasServer ? new IntakeSink(new HttpIntakePost(), config.ErrorsUrl) : NoErrorReportSink.Instance;
    }
}
