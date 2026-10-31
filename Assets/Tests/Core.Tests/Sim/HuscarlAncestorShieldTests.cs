using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class HuscarlAncestorShieldTests
    {
        private static ClassRules Rules => TestWorld.Data.Combat.Classes;

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            loop.Events.Clear();
            return (loop, input);
        }

        private static GameEvent Key(GameLoop loop, FakeInputSource input)
        {
            loop.Events.Clear();
            input.PressKeys(1);
            loop.Tick();
            loop.Tick();
            for (var i = loop.Events.Count - 1; i >= 0; i--)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage)
                    return loop.Events[i];
            }
            throw new AssertionException("no damage events");
        }

        private static int ShieldAt(GameLoop loop, FakeInputSource input, int keys)
        {
            for (var i = 1; i <= keys; i++)
            {
                if ((Key(loop, input).Flags & HitFlags.ShieldStrike) != 0)
                    return i;
            }
            return 0;
        }

        private static bool Stunned(GameLoop loop) =>
            (loop.Synergy.OwnMarks & TestWorld.Data.Synergy.MaskOf(new[] { "stun" })) != 0;

        [Test]
        public void EveryFifteenthKeyIsAShieldStrikeThatHitsTwiceAsHardAndStuns()
        {
            Assert.That(Rules.ShieldEvery, Is.EqualTo(15));
            Assert.That(Rules.ShieldStunSeconds, Is.EqualTo(2));
            var (loop, input) = Hero(HeroClass.Viking);

            Assert.That(ShieldAt(loop, input, Rules.ShieldEvery - 1), Is.Zero);
            var shield = Key(loop, input);
            Assert.That((shield.Flags & HitFlags.ShieldStrike) != 0, Is.True);
            Assert.That(loop.State.StunTicks, Is.EqualTo((int)(Rules.ShieldStunSeconds * GameLoop.TicksPerSecond) - 1).Within(1));
            var next = Key(loop, input);

            Assert.That((next.Flags & HitFlags.ShieldStrike) == 0, Is.True);
            Assert.That(shield.Amount.ToDouble() / next.Amount.ToDouble(), Is.EqualTo(Rules.ShieldMultiplier).Within(0.05));
        }

        [Test]
        public void TheStunIsAMarkForAlliesAndWearsOff()
        {
            var (loop, input) = Hero(HeroClass.Viking);
            ShieldAt(loop, input, Rules.ShieldEvery);
            loop.Tick();
            Assert.That(Stunned(loop), Is.True);

            for (var i = 0; i < Rules.ShieldStunSeconds * GameLoop.TicksPerSecond + 1; i++)
                loop.Tick();

            Assert.That(loop.State.StunTicks, Is.Zero);
            Assert.That(Stunned(loop), Is.False);
        }

        [Test]
        public void ShieldEveryFromTheTreeBringsTheStrikeCloserButNotBelowTheFloor()
        {
            var (loop, input) = Hero(HeroClass.Viking, new Modifier(CombatStats.ShieldEvery, ModifierKind.Flat, -1));
            Assert.That(ShieldAt(loop, input, Rules.ShieldEvery), Is.EqualTo(Rules.ShieldEvery - 1));

            var (fast, fastInput) = Hero(HeroClass.Viking, new Modifier(CombatStats.ShieldEvery, ModifierKind.Flat, -100));
            Assert.That(ShieldAt(fast, fastInput, Rules.ShieldEvery), Is.EqualTo(Rules.ShieldEveryMin));
        }

        [Test]
        public void ShieldDamageAndDamageVsStunnedStrengthenTheBlow()
        {
            var (plain, plainInput) = Hero(HeroClass.Viking);
            var (strong, strongInput) = Hero(HeroClass.Viking,
                new Modifier(CombatStats.ShieldDamage, ModifierKind.Increased, 0.5),
                new Modifier(CombatStats.DamageVsStunned, ModifierKind.Increased, 0.2));
            ShieldAt(plain, plainInput, Rules.ShieldEvery);
            ShieldAt(strong, strongInput, Rules.ShieldEvery);

            var plainAfter = Key(plain, plainInput).Amount.ToDouble();
            var strongAfter = Key(strong, strongInput).Amount.ToDouble();

            Assert.That(strongAfter / plainAfter, Is.EqualTo(1.2).Within(1e-6));
        }

        [Test]
        public void ShieldResourceIsGainedOnTheBlow()
        {
            var (loop, input) = Hero(HeroClass.Viking, new Modifier(CombatStats.ShieldResource, ModifierKind.Flat, 10));
            var (plain, plainInput) = Hero(HeroClass.Viking);
            ShieldAt(loop, input, Rules.ShieldEvery);
            ShieldAt(plain, plainInput, Rules.ShieldEvery);
            loop.Tick();
            plain.Tick();

            Assert.That(loop.State.Resource - plain.State.Resource, Is.EqualTo(10).Within(1e-6));
        }

        [Test]
        public void OtherClassesHaveNoShield()
        {
            var (loop, input) = Hero(HeroClass.Metalhead);

            Assert.That(ShieldAt(loop, input, Rules.ShieldEvery * 2), Is.Zero);
            Assert.That(loop.State.KeysTowardShield, Is.Zero);
        }

        [Test]
        public void TheCountTowardTheShieldIsSaved()
        {
            var (loop, input) = Hero(HeroClass.Viking);
            ShieldAt(loop, input, 7);

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State;

            Assert.That(loaded.KeysTowardShield, Is.EqualTo(7));
        }
    }
}
