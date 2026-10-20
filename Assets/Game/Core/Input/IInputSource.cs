namespace Game.Core.Input
{
    public interface IInputSource
    {
        InputSnapshot Drain();
    }
}
