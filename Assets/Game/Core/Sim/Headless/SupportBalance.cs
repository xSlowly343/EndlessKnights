using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Tree;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim.Headless
{
    public sealed class SupportBalance
    {
        public const string HostSkill = "iron_rim_bash";
        public const long Stage = 40;
        public const double Attributes = 100;

        private readonly GameData _data;
        private readonly ulong _seed;

        public SupportBalance(GameData data, ulong seed = 1)
        {
            _data = data;
            _seed = seed;
        }

        public double Multiplier(GemDef support, BotProfile profile, double seconds)
        {
            var plain = SkillDamage(null, profile, seconds);
            return plain.Sign <= 0 ? 0 : (SkillDamage(support, profile, seconds) / plain).ToDouble();
        }

        public BigNumber SkillDamage(GemDef? support, BotProfile profile, double seconds)
        {
            var loop = Hero(profile);
            var host = _data.Gems[HostSkill];
            Arm(loop, host);
            loop.State.OwnedGems.Add(host);
            if (loop.Book.Equip(host) != BookResult.Done)
                throw new System.InvalidOperationException($"{HostSkill} cannot be equipped for the support check");
            if (support != null)
            {
                loop.State.OwnedGems.Add(support);
                var placed = loop.Book.SetSupport(0, 0, support);
                if (placed != BookResult.Done)
                    throw new System.InvalidOperationException($"{support.Id} cannot be socketed: {placed}");
            }
            var damage = BigNumber.Zero;
            var ticks = (long)System.Math.Round(seconds * GameLoop.TicksPerSecond);
            for (var tick = 0L; tick < ticks; tick++)
            {
                loop.State.Farming = true;
                loop.State.FarmKills = 0;
                loop.RequestCast(0);
                loop.Tick();
                for (var i = 0; i < loop.Events.Count; i++)
                {
                    if (loop.Events[i].Kind == GameEventKind.Damage && loop.Events[i].Source == DamageSource.Skill)
                        damage += loop.Events[i].Amount;
                }
                loop.Events.Clear();
            }
            return damage;
        }

        private GameLoop Hero(BotProfile profile)
        {
            var state = new GameState(_seed) { Class = HeroClass.Viking, Stage = Stage, HighestStage = Stage, RecordStage = Stage };
            var levels = _data.Weapons.SkillRules.UnlockLevels;
            state.HeroLevel = levels[levels.Count - 1];
            var hero = new StatSheet();
            foreach (var attribute in new[] { "strength", "dexterity", "intelligence" })
                hero.Add(new Modifier(PassiveTree.AttributeStat(attribute), ModifierKind.Flat, Attributes));
            var loop = new GameLoop(_data, new BotInput(profile, _seed ^ 0x5EEDUL), state, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.OwnedGems.Clear();
            return loop;
        }

        private static void Arm(GameLoop loop, GemDef skill)
        {
            var type = skill.Skill?.Weapon;
            if (type == null || loop.State.Weapons.Holds(type))
                return;
            foreach (var weapon in loop.Data.Weapons.Bases)
            {
                if (weapon.Type.Id != type)
                    continue;
                loop.Weapons.Equip(WeaponHand.Left, loop.Weapons.Give(weapon));
                return;
            }
        }
    }
}
