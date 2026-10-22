using Game.Core.Content;
using Game.Core.Math;

namespace Game.Core.Sim
{
    public readonly struct GameEvent
    {
        public readonly GameEventKind Kind;
        public readonly long Tick;
        public readonly BigNumber Amount;
        public readonly DamageSource Source;
        public readonly HitFlags Flags;
        public readonly long Stage;
        public readonly EnemyDef? Enemy;

        private GameEvent(GameEventKind kind, long tick, BigNumber amount, DamageSource source, HitFlags flags, long stage, EnemyDef? enemy)
        {
            Kind = kind;
            Tick = tick;
            Amount = amount;
            Source = source;
            Flags = flags;
            Stage = stage;
            Enemy = enemy;
        }

        public bool IsCrit => (Flags & HitFlags.Crit) != 0;

        public static GameEvent Damage(long tick, BigNumber amount, DamageSource source, HitFlags flags) =>
            new GameEvent(GameEventKind.Damage, tick, amount, source, flags, 0, null);

        public static GameEvent ForEnemy(GameEventKind kind, long tick, long stage, EnemyDef? enemy) =>
            new GameEvent(kind, tick, BigNumber.Zero, DamageSource.None, HitFlags.None, stage, enemy);

        public static GameEvent ForStage(GameEventKind kind, long tick, long stage) =>
            new GameEvent(kind, tick, BigNumber.Zero, DamageSource.None, HitFlags.None, stage, null);

        public static GameEvent Gold(long tick, BigNumber amount, long stage) =>
            new GameEvent(GameEventKind.GoldGained, tick, amount, DamageSource.None, HitFlags.None, stage, null);
    }
}
