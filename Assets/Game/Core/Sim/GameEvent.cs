using Game.Core.Content;
using Game.Core.Content.Gems;
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
        public readonly GemDef? Gem;
        public readonly ItemDrop? Drop;

        private GameEvent(GameEventKind kind, long tick, BigNumber amount, DamageSource source, HitFlags flags, long stage, EnemyDef? enemy, GemDef? gem = null, ItemDrop? drop = null)
        {
            Gem = gem;
            Drop = drop;
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

        public static GameEvent ForSkill(GameEventKind kind, long tick, GemDef gem) =>
            new GameEvent(kind, tick, BigNumber.Zero, DamageSource.None, HitFlags.None, 0, null, gem);

        public static GameEvent ForDrop(long tick, ItemDrop drop) =>
            new GameEvent(GameEventKind.ItemDropped, tick, BigNumber.Zero, DamageSource.None, HitFlags.None, drop.Stage, null, null, drop);

        public static GameEvent Gold(long tick, BigNumber amount, long stage) => WithAmount(GameEventKind.GoldGained, tick, amount, stage);

        public static GameEvent WithAmount(GameEventKind kind, long tick, BigNumber amount, long stage) =>
            new GameEvent(kind, tick, amount, DamageSource.None, HitFlags.None, stage, null);
    }
}
