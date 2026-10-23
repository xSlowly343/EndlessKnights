using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class TriggerSystemTests
    {
        private static GemDef Gem(string id) => TestWorld.Data.Gems[id];

        private static (GameLoop Loop, FakeInputSource Input, int Slot) WithTrigger(GemDef? trigger)
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            var slot = loop.Skills.Equip(Gem("stenography"), trigger);
            loop.State.Resource = 100;
            loop.Events.Clear();
            return (loop, input, slot);
        }

        [Test]
        public void EnterCastsTheSkill()
        {
            var (loop, input, slot) = WithTrigger(Gem("cast_on_enter"));

            input.PressEnter();
            loop.Tick();

            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(1));
            Assert.That(loop.State.Skills[slot].HitsLeft, Is.GreaterThan(0));
        }

        [Test]
        public void TriggeredCastCostsTheSameEnergyButLongerCooldown()
        {
            var (loop, input, slot) = WithTrigger(Gem("cast_on_enter"));
            input.PressEnter();
            loop.Tick();

            var skill = Gem("stenography").Skill!;
            var multiplier = Gem("cast_on_enter").Trigger!.CooldownMultiplier;
            Assert.That(loop.State.Skills[slot].CooldownTicks, Is.EqualTo(skill.CooldownSeconds * multiplier * GameLoop.TicksPerSecond));
            Assert.That(loop.State.Resource, Is.EqualTo(TestWorld.Data.Combat.Resources.Max - skill.ResourceCost));
        }

        [Test]
        public void OtherKeysDoNotTrigger()
        {
            var (loop, input, _) = WithTrigger(Gem("cast_on_enter"));

            input.PressKeys(5);
            input.PressSpace();
            input.PressBackspace();
            loop.Tick();

            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(0));
        }

        [Test]
        public void WithoutTriggerEnterDoesNothing()
        {
            var (loop, input, _) = WithTrigger(null);

            input.PressEnter();
            loop.Tick();

            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(0));
        }

        [Test]
        public void EnterDuringCooldownWaitsForTheNextEnterAfterIt()
        {
            var (loop, input, slot) = WithTrigger(Gem("cast_on_enter"));
            input.PressEnter();
            loop.Tick();
            var cooldown = loop.State.Skills[slot].CooldownTicks;
            loop.State.Resource = 100;

            for (var i = 0; i < cooldown; i++)
            {
                if (i % 50 == 0)
                    input.PressEnter();
                loop.Tick();
            }
            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(1));

            loop.State.Resource = 100;
            input.PressEnter();
            loop.Tick();
            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(2));
        }

        [Test]
        public void TriggerWithoutEnergyDoesNotCast()
        {
            var (loop, input, slot) = WithTrigger(Gem("cast_on_enter"));
            loop.State.Resource = 0;

            input.PressEnter();
            loop.Tick();

            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(0));
            Assert.That(loop.State.Skills[slot].Ready, Is.True);
        }
    }
}
