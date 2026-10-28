using Game.Core.Sim;

namespace Game.Core.Telemetry
{
    public readonly struct TelemetryProgress
    {
        public readonly long RecordStage;
        public readonly int Prestiges;
        public readonly string Character;
        public readonly double TotalPlaySeconds;

        public TelemetryProgress(long recordStage, int prestiges, string character, double totalPlaySeconds)
        {
            RecordStage = recordStage;
            Prestiges = prestiges;
            Character = character;
            TotalPlaySeconds = totalPlaySeconds;
        }

        public static TelemetryProgress From(GameState state) =>
            new TelemetryProgress(state.RecordStage, state.Prestiges, HeroClassNames.Of(state.Class), state.TotalInput.PlaySeconds);
    }
}
