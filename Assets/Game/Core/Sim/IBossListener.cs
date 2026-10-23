using Game.Core.Content;

namespace Game.Core.Sim
{
    public interface IBossListener
    {
        void OnBossDefeated(EnemyType type, long stageBefore);
    }
}
