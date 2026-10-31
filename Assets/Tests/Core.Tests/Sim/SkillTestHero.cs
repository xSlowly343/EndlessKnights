using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Weapons;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;

namespace Game.Core.Tests.Sim
{
    public sealed class SkillTestHero
    {
        private SkillTestHero(GameLoop loop, FakeInputSource input)
        {
            Loop = loop;
            Input = input;
        }

        public GameLoop Loop { get; }
        public FakeInputSource Input { get; }
        public GameState State => Loop.State;
        public double KeyDamage => TestWorld.Data.Combat.KeyDamage;

        public static SkillTestHero Of(HeroClass heroClass, params string[] skills)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            var loop = TestWorld.Loop(input, TestWorld.WithAllWeaponPlacesOpen(new GameState { Class = heroClass }), hero);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            foreach (var skill in skills)
                loop.Skills.Equip(TestWorld.Data.Gems[skill]);
            PutOnTheWeapon(loop, skills);
            loop.State.Resource = 100;
            loop.State.OverdriveSpent = true;
            loop.Events.Clear();
            return new SkillTestHero(loop, input);
        }

        private static void PutOnTheWeapon(GameLoop loop, string[] skills)
        {
            var right = loop.State.Weapons.Right;
            if (right == null)
                return;
            var extra = skills.Where(id =>
            {
                var gem = TestWorld.Data.Gems[id];
                return gem.Skill?.Weapon == right.Type.Id && gem.Ascendancy == null && !right.Skills.Contains(id);
            }).ToList();
            if (extra.Count > 0)
                loop.State.Weapons.Right = new WeaponItem(right.Base, right.Rarity, right.Skills.Concat(extra).ToList());
        }

        public static SkillTestHero Borrowing(HeroClass heroClass, HeroClass weaponOwner, params string[] skills)
        {
            var test = Of(heroClass, skills);
            var weapon = test.Loop.Weapons.Give(TestWorld.Data.Weapons.StarterFor(weaponOwner));
            test.Loop.Weapons.Equip(WeaponHand.Left, weapon);
            test.Loop.Events.Clear();
            return test;
        }

        public CastResult Cast(int slot) => Loop.Skills.TryCast(slot);

        public void Ticks(int count, int keysPerTick = 0)
        {
            for (var i = 0; i < count; i++)
            {
                if (keysPerTick > 0)
                    Input.PressKeys(keysPerTick);
                Loop.Tick();
            }
        }

        public void KillCurrentEnemy()
        {
            State.EnemyHp = BigNumber.FromDouble(1e-6);
            Input.PressKeys(1);
            Loop.Tick();
        }

        public double DamageFrom(DamageSource source)
        {
            var total = 0.0;
            for (var i = 0; i < Loop.Events.Count; i++)
            {
                if (Loop.Events[i].Kind == GameEventKind.Damage && Loop.Events[i].Source == source)
                    total += Loop.Events[i].Amount.ToDouble();
            }
            return total;
        }

        public double SkillDamage(string gem, double multiplier) =>
            Loop.Combat.SkillDamage(KeyDamage, multiplier, TestWorld.Data.Gems[gem].Tags).ToDouble();

        public double Queued(int position) => State.QueueDamage[position].ToDouble();
    }
}
