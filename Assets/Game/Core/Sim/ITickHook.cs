namespace Game.Core.Sim
{
    public interface ITickHook
    {
        void BeforeTick();
        void AfterTick();
    }
}
