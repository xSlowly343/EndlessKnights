using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Stats;

namespace Game.Core.Sim.Headless
{
    public sealed class BranchTrial
    {
        public const double DefaultSeconds = 300;

        private readonly GameData _data;
        private readonly BotProfile _profile;
        private readonly long _ticks;

        public BranchTrial(GameData data, BotProfile profile, double seconds = DefaultSeconds)
        {
            _data = data;
            _profile = profile;
            _ticks = (long)System.Math.Round(seconds * GameLoop.TicksPerSecond);
        }

        public int Best(GameState state, GemDef skill, int tier)
        {
            var best = 0;
            var bestDamage = BigNumber.Zero;
            for (var option = 0; option < skill.Branches[tier].Options.Count; option++)
            {
                var damage = DamageWith(state, skill, tier, option);
                if (option == 0 || damage > bestDamage)
                {
                    best = option;
                    bestDamage = damage;
                }
            }
            return best;
        }

        private BigNumber DamageWith(GameState state, GemDef skill, int tier, int option) =>
            DamageAfter(state, loop => BotBranches.Choose(loop, new BotLoadout(loop), skill, tier, option) == Build.BranchResult.Done);

        public BigNumber DamageAfter(GameState state, System.Func<GameLoop, bool> change)
        {
            var copy = Copy(state);
            var loop = new GameLoop(_data, new BotInput(_profile, (ulong)state.Tick ^ 0x7121A1UL), copy, new StatSheet());
            if (!change(loop))
                return BigNumber.Zero;
            var before = copy.TotalDamage;
            for (var tick = 0L; tick < _ticks; tick++)
            {
                loop.Tick();
                loop.Drops.Flush();
                loop.Events.Clear();
            }
            return copy.TotalDamage - before;
        }

        private GameState Copy(GameState state) =>
            GameStateJson.Read(new ContentNode(GameStateJson.Write(state), "trial", string.Empty), _data.Enemies, _data.Gems, _data.Weapons, _data.Items, _data.Slates);
    }
}
