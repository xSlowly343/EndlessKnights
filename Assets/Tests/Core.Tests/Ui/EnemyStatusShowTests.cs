using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class EnemyStatusShowTests
    {
        private static readonly StatusCaps Caps = new StatusCaps { PoisonMax = 50, BurnMax = 30, FractureMax = 25, EarthMax = 0.3, WaterMax = 5 };

        private static GameState Fighting(EnemyType type = EnemyType.Normal)
        {
            var state = new GameState();
            state.SpawnEnemy(null, type, BigNumber.Create(1, 50));
            return state;
        }

        private static EnemyStatusShow Shown(GameState state)
        {
            var show = new EnemyStatusShow();
            show.Read(state, Caps);
            return show;
        }

        [Test]
        public void CleanEnemyShowsNothing()
        {
            var show = Shown(Fighting());
            Assert.That(show.BadgeCount, Is.EqualTo(0));
            Assert.That(show.Outline, Is.EqualTo(StatusOutline.None));
            for (var i = 0; i < EnemyStatusShow.StatusCount; i++)
                Assert.That(show.Tier((EnemyStatus)i), Is.EqualTo(0), ((EnemyStatus)i).ToString());
            Assert.That(show.TryTakeFlash(100, out _), Is.False);
        }

        [TestCase(1, 1)]
        [TestCase(9, 1)]
        [TestCase(10, 2)]
        [TestCase(29, 2)]
        [TestCase(30, 3)]
        [TestCase(50, 3)]
        public void PoisonTierFollowsStacks(int stacks, int tier)
        {
            var state = Fighting();
            state.PoisonStacks = stacks;
            Assert.That(Shown(state).Tier(EnemyStatus.Poison), Is.EqualTo(tier));
        }

        [TestCase(1, 1)]
        [TestCase(9, 1)]
        [TestCase(10, 2)]
        [TestCase(19, 2)]
        [TestCase(20, 3)]
        public void BurnTierFollowsStacks(int stacks, int tier)
        {
            var state = Fighting();
            state.BurnStacks = stacks;
            Assert.That(Shown(state).Tier(EnemyStatus.Burn), Is.EqualTo(tier));
        }

        [TestCase(1, 1)]
        [TestCase(5, 1)]
        [TestCase(6, 2)]
        [TestCase(15, 2)]
        [TestCase(16, 3)]
        [TestCase(24, 3)]
        [TestCase(25, 4)]
        public void FractureTierFollowsStacks(int stacks, int tier)
        {
            var state = Fighting();
            state.FractureStacks = stacks;
            Assert.That(Shown(state).Tier(EnemyStatus.Fracture), Is.EqualTo(tier));
        }

        [Test]
        public void EmberBodyHidesUnderBurnButKeepsItsBadge()
        {
            var state = Fighting();
            state.EmberTicks = 30;
            var show = Shown(state);
            Assert.That(show.ShowsBody(EnemyStatus.Ember), Is.True);
            state.BurnStacks = 3;
            show.Read(state, Caps);
            Assert.That(show.ShowsBody(EnemyStatus.Ember), Is.False);
            Assert.That(show.ShowsBody(EnemyStatus.Burn), Is.True);
            Assert.That(show.BadgeCount, Is.EqualTo(2));
        }

        [Test]
        public void CurseOutlineWinsOverVulnerability()
        {
            var state = Fighting();
            state.VulnerabilityTicks = 40;
            var show = Shown(state);
            Assert.That(show.Outline, Is.EqualTo(StatusOutline.Vulnerable));
            state.Whisperer.CurseTicks = 40;
            show.Read(state, Caps);
            Assert.That(show.Outline, Is.EqualTo(StatusOutline.Curse));
        }

        [Test]
        public void TimedStatusFadesInItsLastSecondAndEndlessMarkNeverFades()
        {
            var state = Fighting();
            state.StunTicks = EnemyStatusShow.FadingTicks + 1;
            state.HunterMarkTicks = int.MaxValue;
            var show = Shown(state);
            Assert.That(show.Fading(EnemyStatus.Stun), Is.False);
            Assert.That(show.Fading(EnemyStatus.Mark), Is.False);
            state.StunTicks = EnemyStatusShow.FadingTicks;
            show.Read(state, Caps);
            Assert.That(show.Fading(EnemyStatus.Stun), Is.True);
        }

        [Test]
        public void BadgesComeInOrderOfAppearanceWithNumbers()
        {
            var state = Fighting();
            var show = new EnemyStatusShow();
            state.FractureStacks = 7;
            show.Read(state, Caps);
            state.PoisonStacks = 12.7;
            show.Read(state, Caps);
            state.EarthShred = 0.13;
            state.StunTicks = 20;
            show.Read(state, Caps);
            Assert.That(show.BadgeCount, Is.EqualTo(4));
            Assert.That(show.Badge(0).Status, Is.EqualTo(EnemyStatus.Fracture));
            Assert.That(show.Badge(0).Number, Is.EqualTo(7));
            Assert.That(show.Badge(1).Status, Is.EqualTo(EnemyStatus.Poison));
            Assert.That(show.Badge(1).Number, Is.EqualTo(12));
            Assert.That(show.Badge(1).Kind, Is.EqualTo(StatusNumber.Stacks));
            Assert.That(show.Badge(2).Status, Is.EqualTo(EnemyStatus.Stun));
            Assert.That(show.Badge(2).Kind, Is.EqualTo(StatusNumber.None));
            Assert.That(show.Badge(3).Status, Is.EqualTo(EnemyStatus.EarthShred));
            Assert.That(show.Badge(3).Kind, Is.EqualTo(StatusNumber.Percent));
            Assert.That(show.Badge(3).Number, Is.EqualTo(13));
        }

        [Test]
        public void NoMoreThanSixBadges()
        {
            var state = Fighting(EnemyType.Boss);
            state.PoisonStacks = 1;
            state.BurnStacks = 1;
            state.EmberTicks = 5;
            state.StunTicks = 5;
            state.BlindTicks = 5;
            state.FractureStacks = 1;
            state.EarthShred = 0.1;
            state.WaterSeconds = 1;
            Assert.That(Shown(state).BadgeCount, Is.EqualTo(EnemyStatusShow.MaxBadges));
        }

        [Test]
        public void WaterShowsOnlyOnBossInTwoSteps()
        {
            var state = Fighting();
            state.WaterSeconds = 4;
            Assert.That(Shown(state).Tier(EnemyStatus.WaterTime), Is.EqualTo(0));
            state = Fighting(EnemyType.Boss);
            state.WaterSeconds = 2;
            Assert.That(Shown(state).Tier(EnemyStatus.WaterTime), Is.EqualTo(1));
            state.WaterSeconds = 2.5;
            Assert.That(Shown(state).Tier(EnemyStatus.WaterTime), Is.EqualTo(2));
        }

        [Test]
        public void DeadEnemyShowsNothing()
        {
            var state = Fighting();
            state.PoisonStacks = 20;
            state.StunTicks = 20;
            state.EnemyWeakened = true;
            var show = Shown(state);
            Assert.That(show.BadgeCount, Is.EqualTo(3));
            state.EnemyHp = BigNumber.Zero;
            show.Read(state, Caps);
            Assert.That(show.BadgeCount, Is.EqualTo(0));
            Assert.That(show.Has(EnemyStatus.Weakened), Is.False);
        }

        [Test]
        public void BiomeElementsAreShownWhileEnemyLives()
        {
            var state = Fighting();
            state.EnemyResists = (int)Element.Water;
            state.EnemyWeakTo = (int)Element.Corrosion;
            var show = Shown(state);
            Assert.That(show.Resists, Is.EqualTo((int)Element.Water));
            Assert.That(show.WeakTo, Is.EqualTo((int)Element.Corrosion));
        }

        [Test]
        public void FlashesFollowCountersOneAtATimeWithAGap()
        {
            var state = Fighting();
            var show = Shown(state);
            state.Discharges++;
            state.AirCarries++;
            show.Read(state, Caps);
            Assert.That(show.TryTakeFlash(10, out var first), Is.True);
            Assert.That(first, Is.EqualTo(StatusFlash.Lightning));
            Assert.That(show.TryTakeFlash(10 + EnemyStatusShow.FlashGapTicks - 1, out _), Is.False);
            Assert.That(show.TryTakeFlash(10 + EnemyStatusShow.FlashGapTicks, out var second), Is.True);
            Assert.That(second, Is.EqualTo(StatusFlash.Air));
            Assert.That(show.TryTakeFlash(100, out _), Is.False);
        }

        [Test]
        public void FractureStackFlashesAsChipOrAsCorrosion()
        {
            var state = Fighting();
            var show = Shown(state);
            state.FractureStacks = 1;
            state.FracturesLaid++;
            show.Read(state, Caps);
            Assert.That(show.TryTakeFlash(10, out var chip), Is.True);
            Assert.That(chip, Is.EqualTo(StatusFlash.FractureChip));
            state.FractureStacks = 2;
            state.FracturesLaid++;
            state.CorrosionFractures++;
            show.Read(state, Caps);
            Assert.That(show.TryTakeFlash(20, out var corrosion), Is.True);
            Assert.That(corrosion, Is.EqualTo(StatusFlash.Corrosion));
            Assert.That(show.TryTakeFlash(30, out _), Is.False);
        }

        [Test]
        public void NewEnemyDoesNotInheritFlashesButAMarkedOneGetsTheMarkFlash()
        {
            var state = Fighting();
            var show = Shown(state);
            state.Tick = 50;
            state.Discharges += 3;
            state.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            state.HunterMarkTicks = 30;
            show.Read(state, Caps);
            Assert.That(show.TryTakeFlash(60, out var flash), Is.True);
            Assert.That(flash, Is.EqualTo(StatusFlash.MarkOn));
            Assert.That(show.TryTakeFlash(70, out _), Is.False);
            show.Read(state, Caps);
            Assert.That(show.TryTakeFlash(80, out _), Is.False);
        }

        [Test]
        public void ReadingDoesNotAllocate()
        {
            var state = Fighting(EnemyType.Boss);
            state.PoisonStacks = 12;
            state.FractureStacks = 5;
            state.StunTicks = 9;
            var show = Shown(state);
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 200; i++)
            {
                state.Discharges++;
                show.Read(state, Caps);
                show.TryTakeFlash(i * 5, out _);
            }
            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
