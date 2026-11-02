using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CartomancerTests
    {
        private static CartomancerRules Rules => TestWorld.Data.Combat.Classes.Cartomancer;

        private static Modifier Flat(StatId stat, double value) => new Modifier(stat, ModifierKind.Flat, value);

        private static (GameLoop Loop, FakeInputSource Input) Hero(params Modifier[] modifiers) => Hero(HeroClass.Cartomancer, EnemyType.Normal, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Boss(params Modifier[] modifiers) => Hero(HeroClass.Cartomancer, EnemyType.Boss, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, EnemyType enemy, params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, TestWorld.WithAllWeaponPlacesOpen(new GameState { Class = heroClass }), hero);
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
            loop.Combat.Cartomancer.OnSkillCast(TestWorld.Data.Gems[skill]);
            return Damage(loop);
        }

        private static void Kill(GameLoop loop) => loop.Combat.Cartomancer.OnKillRewarded();

        private static int Equip(GameLoop loop, string skill)
        {
            TestWorld.ArmFor(loop, TestWorld.Data.Gems[skill]);
            loop.Skills.Equip(TestWorld.Data.Gems[skill], role: SkillTestHero.ManualRole(loop.State.Skills.Count));
            return loop.State.Skills.Count - 1;
        }

        [Test]
        public void EveryTenHitsDrawACard()
        {
            var (loop, input) = Hero(Flat(CartomancerStats.SpreadPower, 0.3));
            Hits(loop, input, 20);
            Assert.That(loop.State.Cartomancer.Draws, Is.EqualTo(2));
        }

        [Test]
        public void ThreeCardsOfOneSuitOpenAMajorArcanum()
        {
            var (loop, _) = Hero(Flat(CartomancerStats.SpreadPower, 0.3));
            loop.Combat.Cartomancer.AddCard(CartomancerState.Swords);
            loop.Combat.Cartomancer.AddCard(CartomancerState.Swords);
            Assert.That(loop.Combat.Cartomancer.ArcanaActive(CartomancerState.Swords), Is.False);
            var before = loop.Combat.Cartomancer.DamageTaken();
            loop.Combat.Cartomancer.AddCard(CartomancerState.Swords);
            Assert.That(loop.Combat.Cartomancer.ArcanaActive(CartomancerState.Swords), Is.True);
            Assert.That(loop.State.Cartomancer.Hand[CartomancerState.Swords], Is.Zero);
            Assert.That(loop.Combat.Cartomancer.DamageTaken() / before, Is.EqualTo(1.3).Within(1e-9));
            Wait(loop, Rules.ArcanaSeconds + 0.2);
            Assert.That(loop.Combat.Cartomancer.ArcanaActive(CartomancerState.Swords), Is.False);
        }

        [Test]
        public void CupsGiveResourceAndCoinsGiveGold()
        {
            var (loop, _) = Hero(Flat(CartomancerStats.SpreadPower, 0.3));
            for (var i = 0; i < 3; i++)
            {
                loop.Combat.Cartomancer.AddCard(CartomancerState.Cups);
                loop.Combat.Cartomancer.AddCard(CartomancerState.Coins);
            }
            var resource = loop.State.Resource;
            Wait(loop, 1.1);
            Assert.That(loop.State.Resource, Is.GreaterThan(resource + 1));
            Assert.That(loop.State.EnemyGoldBonus, Is.EqualTo(0.3 * Rules.CoinsGold).Within(1e-9));
        }

        [Test]
        public void WandsCutCooldowns()
        {
            var (loop, _) = Hero(Flat(CartomancerStats.SpreadPower, 0.3));
            var slot = Equip(loop, "cartomancer_hit_1");
            loop.State.Skills[slot].CooldownTicks = 100;
            for (var i = 0; i < 3; i++)
                loop.Combat.Cartomancer.AddCard(CartomancerState.Wands);
            Wait(loop, 1);
            Assert.That(loop.State.Skills[slot].CooldownTicks, Is.LessThan(90 - 2));
        }

        [Test]
        public void AMarkedDeckOpensAnArcanumFromTwoCardsAndPalmReadingHasNone()
        {
            var (pair, _) = Hero(Flat(CartomancerStats.SpreadPower, 0.3), Flat(CartomancerStats.SpreadPair, 1));
            pair.Combat.Cartomancer.AddCard(CartomancerState.Coins);
            pair.Combat.Cartomancer.AddCard(CartomancerState.Coins);
            Assert.That(pair.Combat.Cartomancer.ArcanaActive(CartomancerState.Coins), Is.True);
            var (palm, _) = Hero(Flat(CartomancerStats.SpreadPower, 0.5), Flat(CartomancerStats.SpreadHand, 1));
            for (var i = 0; i < 4; i++)
                palm.Combat.Cartomancer.AddCard(CartomancerState.Swords);
            Assert.That(palm.Combat.Cartomancer.ArcanaActive(CartomancerState.Swords), Is.False);
            Assert.That(palm.Combat.Cartomancer.DamageTaken(), Is.EqualTo(1 + 4 * Rules.HandBonus).Within(1e-9));
        }

        [Test]
        public void ABossGetsATermAndAKillInTimeDoublesTheReward()
        {
            var (loop, _) = Boss(Flat(CartomancerStats.ForecastPower, 1));
            loop.Tick();
            Assert.That(loop.State.Cartomancer.TermTicks, Is.GreaterThan(0));
            Assert.That(loop.State.EnemyGoldBonus, Is.EqualTo(1).Within(1e-9));
            Kill(loop);
            Assert.That(loop.State.Cartomancer.Wins, Is.EqualTo(1));
            Assert.That(loop.State.Cartomancer.TermTicks, Is.Zero);
        }

        [Test]
        public void AMissedTermTakesTheRewardAndWeakensHerUntilTheNextBoss()
        {
            var (loop, _) = Boss(Flat(CartomancerStats.ForecastPower, 1));
            loop.Tick();
            Wait(loop, loop.State.Cartomancer.TermTicks * GameLoop.TickSeconds + 0.2);
            Assert.That(loop.State.Cartomancer.Losses, Is.EqualTo(1));
            Assert.That(loop.State.EnemyGoldBonus, Is.EqualTo(0).Within(1e-9));
            Assert.That(loop.Combat.Cartomancer.DamageTaken(), Is.EqualTo(1 - Rules.Penalty).Within(1e-9));
            Spawn(loop, EnemyType.Normal);
            loop.Tick();
            Assert.That(loop.State.Cartomancer.Penalty, Is.True);
            Spawn(loop, EnemyType.Boss);
            loop.Tick();
            Assert.That(loop.State.Cartomancer.Penalty, Is.False);
            var (safe, _) = Boss(Flat(CartomancerStats.ForecastPower, 1), Flat(CartomancerStats.ForecastSafe, 1));
            safe.Tick();
            Wait(safe, safe.State.Cartomancer.TermTicks * GameLoop.TickSeconds + 0.2);
            Assert.That(safe.State.Cartomancer.Penalty, Is.False);
        }

        [Test]
        public void AStreakOfWinsRaisesTheNextReward()
        {
            var (loop, _) = Boss(Flat(CartomancerStats.ForecastPower, 1), Flat(CartomancerStats.ForecastStreak, 1));
            loop.Tick();
            Kill(loop);
            Spawn(loop, EnemyType.Boss);
            loop.State.EnemyGoldBonus = 0;
            loop.Tick();
            Assert.That(loop.State.EnemyGoldBonus, Is.EqualTo(1 + Rules.StreakStep).Within(1e-9));
        }

        [Test]
        public void ReversedCardsAreLaidOnTheEnemyAndSwordsExposeIt()
        {
            var (loop, input) = Hero(Flat(CartomancerStats.ReversedPower, 0.04));
            Hits(loop, input, 30);
            Assert.That(loop.State.Cartomancer.LaidCount, Is.EqualTo(3));
            loop.State.Cartomancer.Laid[CartomancerState.Swords] = 3;
            Assert.That(loop.Combat.Cartomancer.SwordsShare, Is.EqualTo(0.12).Within(1e-9));
            Assert.That(loop.Combat.Cartomancer.DamageTaken(), Is.EqualTo(1.12).Within(1e-9));
        }

        [Test]
        public void CardsFallOffOnANewEnemyUnlessKept()
        {
            var (loop, _) = Hero(Flat(CartomancerStats.ReversedPower, 0.04));
            loop.State.Cartomancer.Laid[CartomancerState.Cups] = 4;
            Spawn(loop, EnemyType.Normal);
            loop.Tick();
            Assert.That(loop.State.Cartomancer.LaidCount, Is.Zero);
            var (kept, _) = Hero(Flat(CartomancerStats.ReversedPower, 0.04), Flat(CartomancerStats.ReversedKeep, 1));
            kept.State.Cartomancer.Laid[CartomancerState.Cups] = 4;
            Spawn(kept, EnemyType.Normal);
            kept.Tick();
            Assert.That(kept.State.Cartomancer.LaidCount, Is.EqualTo(2));
        }

        [Test]
        public void SoloEveryFortyHitsRevealTheBiggestSuitTwiceAsStrong()
        {
            var (loop, input) = Hero(Flat(CartomancerStats.ReversedPower, 0.04), Flat(CartomancerStats.ReversedHits, 1000));
            loop.State.Cartomancer.Laid[CartomancerState.Swords] = 2;
            Hits(loop, input, 39);
            Assert.That(loop.State.Cartomancer.Reveals, Is.Zero);
            Hits(loop, input, 1);
            Assert.That(loop.State.Cartomancer.Reveals, Is.EqualTo(1));
            Assert.That(loop.Combat.Cartomancer.SwordsShare, Is.EqualTo(0.16).Within(1e-9));
        }

        [Test]
        public void SoloWandsSlowTheBossTimerAndCupsWoundTheEnemy()
        {
            var (loop, _) = Boss(Flat(CartomancerStats.ReversedPower, 0.04));
            loop.State.Cartomancer.Laid[CartomancerState.Wands] = 2;
            loop.State.Cartomancer.Laid[CartomancerState.Cups] = 2;
            loop.Events.Clear();
            Wait(loop, 1);
            Assert.That(loop.State.TraitTimerSlow, Is.EqualTo(2 * 0.04 * Rules.WandsSlow).Within(1e-9));
            Assert.That(Damage(loop), Has.Count.EqualTo(1));
        }

        [Test]
        public void SkillsDrawCardsRevealAndOpenArcana()
        {
            var (loop, _) = Hero(Flat(CartomancerStats.SpreadPower, 0.3));
            Cast(loop, "cartomancer_path_1");
            Assert.That(loop.State.Cartomancer.Draws, Is.EqualTo(3));
            Cast(loop, "cartomancer_strong_1");
            Assert.That(loop.State.Cartomancer.Arcana, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void TheTarotDeckIsTheWeapon()
        {
            Assert.That(TestWorld.Real.Gems["cartomancer_hit_1"].Skill!.Weapon, Is.EqualTo("tarot_deck"));
        }
    }
}
