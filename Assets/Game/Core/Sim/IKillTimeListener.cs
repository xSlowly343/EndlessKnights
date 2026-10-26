using Game.Core.Content;

namespace Game.Core.Sim
{
    public interface IKillTimeListener
    {
        void OnKillTimed(EnemyType type, double lifeSeconds, bool shared);
    }
}
