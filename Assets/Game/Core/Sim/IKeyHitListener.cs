using Game.Core.Math;

namespace Game.Core.Sim
{
    public interface IKeyHitListener
    {
        void OnKeyHit(BigNumber damage);
    }
}
