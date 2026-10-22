using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CritTests
    {
        private const int Hits = 10000;

        private static GameLoop HitMany(StatSheet hero, ulong seed, System.Action<FakeInputSource> press)
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState(seed), hero);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 100));
            loop.Events.Clear();
            for (var i = 0; i < Hits; i++)
            {
                press(input);
                loop.Tick();
                loop.Tick();
            }
            return loop;
        }

        private static (int Hits, int Crits, double CritRatio) Count(GameLoop loop, DamageSource source)
        {
            var hits = 0;
            var crits = 0;
            BigNumber normal = BigNumber.Zero;
            BigNumber crit = BigNumber.Zero;
            for (var i = 0; i < loop.Events.Count; i++)
            {
                var hit = loop.Events[i];
                if (hit.Kind != GameEventKind.Damage || hit.Source != source)
                    continue;
                hits++;
                if (hit.IsCrit)
                {
                    crits++;
                    crit = hit.Amount;
                }
                else
                {
                    normal = hit.Amount;
                }
            }
            return (hits, crits, normal.IsZero ? 0 : (crit / normal).ToDouble());
        }

        [Test]
        public void BaseCritChanceHoldsOverTenThousandHits()
        {
            var loop = HitMany(new StatSheet(), 42, input => input.PressKeys(1));

            var (hits, crits, _) = Count(loop, DamageSource.Keyboard);

            var expected = TestWorld.Data.Combat.CritChance;
            var sigma = System.Math.Sqrt(expected * (1 - expected) / Hits);
            Assert.That(hits, Is.EqualTo(Hits));
            Assert.That((double)crits / hits, Is.EqualTo(expected).Within(4 * sigma));
        }

        [Test]
        public void CritHitsDealMultipliedDamage()
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.Flat, 1));
            var loop = TestWorld.Loop(input, hero: hero);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e9));
            loop.Events.Clear();

            input.PressKeys(1);
            loop.Tick();

            var hit = loop.Events[0];
            Assert.That(hit.IsCrit, Is.True);
            var expected = TestWorld.Data.Combat.KeyDamage * loop.State.FocusMultiplier * TestWorld.Data.Combat.CritMultiplier;
            Assert.That(hit.Amount.ToDouble(), Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void TaggedCritModifiersOnlyAffectTheirInput()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.Flat, 0.20, StatTags.Click));
            hero.Add(new Modifier(CombatStats.CritMultiplier, ModifierKind.Flat, 0.5, StatTags.Click));
            var loop = HitMany(hero, 7, input =>
            {
                input.PressKeys(1);
                input.ClickLeft();
            });

            var keys = Count(loop, DamageSource.Keyboard);
            var clicks = Count(loop, DamageSource.Click);
            var baseChance = TestWorld.Data.Combat.CritChance;

            Assert.That((double)keys.Crits / keys.Hits, Is.EqualTo(baseChance).Within(0.01));
            Assert.That((double)clicks.Crits / clicks.Hits, Is.EqualTo(baseChance + 0.20).Within(0.02));
            Assert.That(loop.Combat.CritMultiplier(StatTags.Click), Is.EqualTo(TestWorld.Data.Combat.CritMultiplier + 0.5).Within(1e-9));
            Assert.That(loop.Combat.CritMultiplier(StatTags.Keyboard), Is.EqualTo(TestWorld.Data.Combat.CritMultiplier).Within(1e-9));
        }

        [Test]
        public void CritChanceIsClampedToOne()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.Flat, 5));
            var loop = TestWorld.Loop(hero: hero);

            Assert.That(loop.Combat.CritChance(StatTags.Keyboard), Is.EqualTo(1));
        }

        [Test]
        public void SameSeedGivesSameCrits()
        {
            string Pattern(ulong seed)
            {
                var loop = HitMany(new StatSheet(), seed, input => input.PressKeys(1));
                var pattern = new System.Text.StringBuilder();
                for (var i = 0; i < 200; i++)
                    pattern.Append(loop.Events[i].Kind == GameEventKind.Damage && loop.Events[i].IsCrit ? 'C' : '.');
                return pattern.ToString();
            }

            Assert.That(Pattern(3), Is.EqualTo(Pattern(3)));
        }
    }
}
