using Game.Core.Input;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class EnergySystemTests
    {
        private static Game.Core.Content.EnergyRules Rules => TestWorld.Data.Combat.Energy;

        [Test]
        public void KeysClicksAndMouseGiveEnergy()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);

            input.PressKeys(2);
            input.ClickLeft();
            input.MoveMouse((float)(Rules.MousePxPerPoint * 1.5));
            loop.Tick();

            Assert.That(loop.State.Energy, Is.EqualTo(2 * Rules.PerKey + Rules.PerClick + 1.5).Within(1e-9));
        }

        [Test]
        public void AllMouseButtonsAndWheelGiveEnergyLikeClicks()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);

            input.ClickRight();
            input.ClickMiddle();
            input.ScrollWheel(1);
            loop.Tick();

            Assert.That(loop.State.Energy, Is.EqualTo(3 * Rules.PerClick).Within(1e-9));
        }

        [Test]
        public void EnergyStopsAtMaximum()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);

            for (var tick = 0; tick < 2000; tick++)
            {
                input.PressKeys(1);
                loop.Tick();
            }

            Assert.That(loop.State.Energy, Is.EqualTo(Rules.Max));
            Assert.That(loop.Energy.IsFull, Is.True);
        }

        [Test]
        public void CappedTypingGivesCappedEnergy()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);

            for (var tick = 0; tick < 30; tick++)
            {
                input.PressKeys(3);
                loop.Tick();
            }

            Assert.That(loop.State.Energy, Is.LessThan(30 * 3 * Rules.PerKey));
            Assert.That(loop.State.Energy, Is.InRange(3 * System.Math.Sqrt(8 * 15) * Rules.PerKey, 50 * Rules.PerKey));
        }

        [Test]
        public void SpendingNeedsEnoughEnergy()
        {
            var loop = TestWorld.Loop();
            loop.State.Energy = 30;

            Assert.That(loop.Energy.TrySpend(40), Is.False);
            Assert.That(loop.State.Energy, Is.EqualTo(30));
            Assert.That(loop.Energy.TrySpend(30), Is.True);
            Assert.That(loop.State.Energy, Is.EqualTo(0));
        }
    }
}
