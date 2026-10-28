namespace Game.Core.Telemetry
{
    public interface ITelemetrySink
    {
        bool TrySend(string packetJson);
    }
}
