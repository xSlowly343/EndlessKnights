namespace Game.Core.Telemetry
{
    public sealed class NoTelemetrySink : ITelemetrySink
    {
        public static readonly NoTelemetrySink Instance = new NoTelemetrySink();

        public bool TrySend(string packetJson) => false;
    }
}
