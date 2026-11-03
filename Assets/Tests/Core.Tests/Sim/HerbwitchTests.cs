using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class HerbwitchTests
    {
        private static HerbwitchRules Rules => TestWorld.Data.Combat.Classes.Herbwitch;

        private static Modifier Flat(StatId stat, double value) => new Modifier(stat, ModifierKind.Flat, value);

        private static (GameLoop Loop, FakeInputSource Input) Hero(params Modifier[] modifiers) => Hero(HeroClass.Herbwitch, EnemyType.Normal, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Boss(params Modifier[] modifiers) => Hero(HeroClass.Herbwitch, EnemyType.Boss, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, EnemyType enemy, params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, TestWorld.WithAllSkillsOpen(new GameState { Class = heroClass }), hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            Spawn(loop, enemy);
            loop.Tick();
            loop.Events.Clear();
            return (loop, input);
        }

        private static void Spawn(GameLoop loop, EnemyType type)
        {
            loop.State.SpawnEnemy(null, type, BigNumber.Create(1, 50));
            loop.State.BossTicksLeft = type == EnemyType.Normal ? 0 : 100_000;
        }

        private static void Party(GameLoop loop, int members)
        {
            loop.State.SharedEnemy = true;
            for (var slot = 1; slot < members; slot++)
                loop.PartyBuffs.Ally(slot).Present = true;
            loop.State.PartyMembers = members;
        }

        private static List<double> Damage(GameLoop loop)
        {
            var damage = new List<double>();
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage && loop.Events[i].Source != DamageSource.Mouse)
                    damage.Add(loop.Events[i].Amount.ToDouble());
            }
            return damage;
        }

        private static void Hits(GameLoop loop, FakeInputSource input, int count)
        {
            for (var i = 0; i < count; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
            }
        }

        private static void Wait(GameLoop loop, double seconds)
        {
            for (var i = 0; i < System.Math.Round(seconds * GameLoop.TicksPerSecond); i++)
                loop.Tick();
        }

        private static List<double> Cast(GameLoop loop, string skill)
        {
            loop.Events.Clear();
            loop.Combat.Herbwitch.OnSkillCast(TestWorld.Data.Gems[skill]);
            return Damage(loop);
        }

        private static void Kill(GameLoop loop)
        {
            loop.Combat.Herbwitch.OnKillRewarded();
            Spawn(loop, EnemyType.Normal);
            loop.Tick();
        }

        [Test]
        public void EveryFifteenHitsPlantASeedThatStingsEverySecond()
        {
            var (loop, input) = Hero(Flat(HerbwitchStats.BloomPower, 0.12));
            Hits(loop, input, 15);
            Assert.That(loop.State.Herbwitch.Vines, Is.EqualTo(1));
            loop.Events.Clear();
            Wait(loop, 1);
            Assert.That(Damage(loop), Has.Count.EqualTo(1));
        }

        [Test]
        public void AVineGrowsFromSproutToBushToFlowerDoublingEachStage()
        {
            var (loop, _) = Hero(Flat(HerbwitchStats.BloomPower, 0.12));
            Cast(loop, "herbwitch_hit_3");
            Assert.That(loop.State.Herbwitch.VineStage[0], Is.Zero);
            Wait(loop, Rules.GrowSeconds + 0.1);
            Assert.That(loop.State.Herbwitch.VineStage[0], Is.EqualTo(1));
            Wait(loop, Rules.GrowSeconds);
            Assert.That(loop.State.Herbwitch.VineStage[0], Is.EqualTo(2));
            Assert.That(loop.State.Herbwitch.Flowers, Is.EqualTo(1));
            Assert.That(loop.Combat.Herbwitch.VineDamage(2) / loop.Combat.Herbwitch.VineDamage(0), Is.EqualTo(4).Within(1e-9));
        }

        [Test]
        public void ANewEnemyStartsTheGardenAgain()
        {
            var (loop, _) = Hero(Flat(HerbwitchStats.BloomPower, 0.12));
            Cast(loop, "herbwitch_hit_3");
            Kill(loop);
            Assert.That(loop.State.Herbwitch.Vines, Is.Zero);
            var (replant, _) = Hero(Flat(HerbwitchStats.BloomPower, 0.12), Flat(HerbwitchStats.BloomReplant, 1));
            Cast(replant, "herbwitch_path_1");
            Kill(replant);
            Assert.That(replant.State.Herbwitch.Vines, Is.EqualTo(Rules.Vines));
            Assert.That(replant.State.Herbwitch.VineStage[0], Is.EqualTo(2));
        }

        [Test]
        public void AFlowerDropsAPetalAndThePetalPlantsSeedsWithHits()
        {
            var (loop, input) = Hero(Flat(HerbwitchStats.BloomPower, 0.12));
            Cast(loop, "herbwitch_path_1");
            Assert.That(loop.State.Boons.PetalTicks, Is.GreaterThan(0));
            Hits(loop, input, Rules.PetalHits);
            Assert.That(loop.State.Boons.PetalSeeds, Is.EqualTo(1));
        }

        [Test]
        public void ThornyBushAndOneFlowerChangeTheGarden()
        {
            var (thorn, _) = Hero(Flat(HerbwitchStats.BloomPower, 0.12), Flat(HerbwitchStats.BloomThorn, 1));
            Assert.That(thorn.Combat.Herbwitch.TopStage, Is.EqualTo(1));
            Assert.That(thorn.Combat.Herbwitch.VineDamage(1), Is.EqualTo(0.12 * Rules.ThornMultiplier).Within(1e-9));
            var (single, _) = Hero(Flat(HerbwitchStats.BloomPower, 0.12), Flat(HerbwitchStats.BloomSingle, 1));
            Assert.That(single.Combat.Herbwitch.MaxVines, Is.EqualTo(1));
            Assert.That(single.Combat.Herbwitch.VineDamage(0), Is.EqualTo(0.12 * Rules.SingleMultiplier).Within(1e-9));
        }

        [Test]
        public void EveryHitAddsAStackOfPoisonThatStingsEverySecond()
        {
            var (loop, input) = Hero(Flat(HerbwitchStats.MyceliumPower, 0.02));
            Hits(loop, input, 10);
            Assert.That(loop.State.Herbwitch.Venom, Is.EqualTo(10));
            loop.Events.Clear();
            Wait(loop, 1);
            Assert.That(Damage(loop), Has.Count.EqualTo(1));
            loop.State.Herbwitch.Venom = 49;
            Hits(loop, input, 3);
            Assert.That(loop.State.Herbwitch.Venom, Is.EqualTo(Rules.VenomMax));
        }

        [Test]
        public void HalfThePoisonFlowsToTheNextEnemy()
        {
            var (loop, _) = Hero(Flat(HerbwitchStats.MyceliumPower, 0.02));
            loop.State.Herbwitch.Venom = 40;
            Kill(loop);
            Assert.That(loop.State.Herbwitch.Venom, Is.EqualTo(20).Within(1e-9));
            var (deep, _) = Hero(Flat(HerbwitchStats.MyceliumPower, 0.02), Flat(HerbwitchStats.MyceliumDeep, 1));
            deep.State.Herbwitch.Venom = 40;
            Kill(deep);
            Assert.That(deep.State.Herbwitch.Venom, Is.EqualTo(30).Within(1e-9));
            var (toxic, _) = Hero(Flat(HerbwitchStats.MyceliumPower, 0.02), Flat(HerbwitchStats.MyceliumNoCarry, 1));
            toxic.State.Herbwitch.Venom = 40;
            Kill(toxic);
            Assert.That(toxic.State.Herbwitch.Venom, Is.Zero);
        }

        [Test]
        public void TheWebStingsTwiceAsOftenAndToxicSporesDoubleTheStacks()
        {
            var (web, _) = Hero(Flat(HerbwitchStats.MyceliumPower, 0.02), Flat(HerbwitchStats.MyceliumFast, 1));
            web.State.Herbwitch.Venom = 10;
            web.Events.Clear();
            Wait(web, 1);
            Assert.That(Damage(web), Has.Count.EqualTo(2));
            var (toxic, input) = Hero(Flat(HerbwitchStats.MyceliumPower, 0.02), Flat(HerbwitchStats.MyceliumDouble, 1));
            Hits(toxic, input, 5);
            Assert.That(toxic.State.Herbwitch.Venom, Is.EqualTo(10));
        }

        [Test]
        public void SapPoisonDoesNotTickAndEveryTwentiethHitDrinksIt()
        {
            var (loop, input) = Hero(Flat(HerbwitchStats.SapPower, 0.12));
            Hits(loop, input, 19);
            Assert.That(loop.State.Herbwitch.Brew, Is.EqualTo(38).Within(1e-9));
            loop.Events.Clear();
            Wait(loop, 1);
            Assert.That(Damage(loop), Is.Empty);
            var resource = loop.State.Resource;
            Hits(loop, input, 1);
            Assert.That(loop.State.Herbwitch.Drinks, Is.EqualTo(1));
            Assert.That(loop.State.Herbwitch.Brew, Is.Zero);
            Assert.That(loop.State.Resource, Is.GreaterThan(resource + 5));
        }

        [Test]
        public void UndrunkPoisonIsLostWithTheEnemyUnlessASipIsLeft()
        {
            var (loop, _) = Hero(Flat(HerbwitchStats.SapPower, 0.12));
            loop.State.Herbwitch.Brew = 30;
            Kill(loop);
            Assert.That(loop.State.Herbwitch.Brew, Is.Zero);
            var (later, _) = Hero(Flat(HerbwitchStats.SapPower, 0.12), Flat(HerbwitchStats.SapLeave, 1));
            later.State.Herbwitch.Brew = 30;
            Kill(later);
            Assert.That(later.State.Herbwitch.Brew, Is.EqualTo(9).Within(1e-9));
        }

        [Test]
        public void PoisonBreathBurnsAfterADrink()
        {
            var (loop, _) = Hero(Flat(HerbwitchStats.SapPower, 0.12), Flat(HerbwitchStats.SapPoison, 1));
            loop.State.Herbwitch.Brew = 10;
            Cast(loop, "herbwitch_path_3");
            Assert.That(loop.State.Herbwitch.PartyPoisonTicks, Is.GreaterThan(0));
            loop.Events.Clear();
            Wait(loop, 1);
            Assert.That(Damage(loop), Has.Count.EqualTo(1));
        }

        [Test]
        public void SkillsPlantSeedsAddPoisonAndBloomTheGarden()
        {
            var (loop, _) = Hero(Flat(HerbwitchStats.MyceliumPower, 0.02));
            Cast(loop, "herbwitch_path_2");
            Assert.That(loop.State.Herbwitch.Venom, Is.EqualTo(15));
            Assert.That(Cast(loop, "herbwitch_strong_3"), Has.Count.EqualTo(1));
            var (garden, _) = Hero(Flat(HerbwitchStats.BloomPower, 0.12));
            Cast(garden, "herbwitch_strong_2");
            Assert.That(garden.State.Herbwitch.VineStage[0], Is.EqualTo(2));
        }

        [Test]
        public void TheBloomingStaffIsTheWeapon()
        {
            Assert.That(TestWorld.Real.Gems["herbwitch_hit_1"].Skill!.Weapon, Is.EqualTo("blooming_staff"));
        }
    }
}
