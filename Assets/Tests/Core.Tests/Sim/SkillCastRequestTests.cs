using Game.Core.Content.Gems;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class SkillCastRequestTests
    {
        private static GemDef Gem(string id) => TestWorld.Data.Gems[id];

        [Test]
        public void RequestedCastHappensOnTheNextTick()
        {
            var loop = TestWorld.Loop();
            var slot = loop.Skills.Equip(Gem("through_stride"));
            loop.State.Resource = 100;

            loop.RequestCast(slot);
            Assert.That(loop.State.Skills[slot].Ready, Is.True);

            loop.Tick();
            Assert.That(loop.State.Skills[slot].Ready, Is.False);
            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(1));
        }

        [Test]
        public void RequestIsUsedOnce()
        {
            var loop = TestWorld.Loop();
            var slot = loop.Skills.Equip(Gem("through_stride"));
            loop.State.Resource = 100;
            loop.RequestCast(slot);
            loop.Tick();
            loop.State.Skills[slot].CooldownTicks = 0;

            loop.Tick();

            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(1));
        }

        [Test]
        public void RequestForAMissingSlotDoesNothing()
        {
            var loop = TestWorld.Loop();
            var before = loop.State.Skills.Count;

            loop.RequestCast(before + 3);
            loop.RequestCast(-1);
            loop.RequestCast(GameLoop.MaxRequestedSlots);
            loop.Tick();

            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(0));
        }

        [Test]
        public void CooldownFractionFallsFromOneToZero()
        {
            var loop = TestWorld.Loop();
            var slot = loop.Skills.Equip(Gem("through_stride"));
            loop.State.Resource = 100;
            loop.Skills.TryCast(slot);
            var skill = loop.State.Skills[slot];
            var total = skill.CooldownTicks;

            Assert.That(skill.CooldownFraction, Is.EqualTo(1d));
            for (var i = 0; i < total / 2; i++)
                loop.Tick();
            Assert.That(skill.CooldownFraction, Is.EqualTo((total - total / 2) / (double)total).Within(1e-9));
            for (var i = total / 2; i < total; i++)
                loop.Tick();
            Assert.That(skill.CooldownFraction, Is.EqualTo(0d));
        }

        [Test]
        public void CooldownLoadedWithoutTotalShowsFull()
        {
            var slot = new SkillSlot(Gem("through_stride")) { CooldownTicks = 40 };

            Assert.That(slot.CooldownFraction, Is.EqualTo(1d));
        }
    }
}
