namespace Game.Core.Sim
{
    public sealed class GameState
    {
        public long Tick { get; set; }

        public double ElapsedSeconds => Tick * GameLoop.TickSeconds;
    }
}
