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
            var slot = loop.Skills.Equip(Gem("through_stride"), trigger);
            loop.State.Resource = 100;
            loop.Events.Clear();
            return (loop, input, slot);
        }

        [Test]
        public void ThousandPixelsOfMouseCastTheSkill()
        {
            var (loop, input, slot) = WithTrigger(Gem("cast_on_mouse_distance"));

            input.MoveMouse(600);
            loop.Tick();
            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(0));
            input.MoveMouse(400);
            loop.Tick();

            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(1));
            Assert.That(loop.State.Skills[slot].HitsLeft, Is.GreaterThan(0));
        }

        [Test]
        public void TriggeredCastCostsTheSameEnergyButLongerCooldown()
        {
            var (loop, input, slot) = WithTrigger(Gem("cast_on_mouse_distance"));
            input.MoveMouse(1000);
            loop.Tick();

            var skill = Gem("through_stride").Skill!;
            var multiplier = Gem("cast_on_mouse_distance").Trigger!.CooldownMultiplier;
            Assert.That(loop.State.Skills[slot].CooldownTicks, Is.EqualTo(skill.CooldownSeconds * multiplier * GameLoop.TicksPerSecond));
            Assert.That(loop.State.Resource, Is.EqualTo(TestWorld.Data.Combat.Resources.Max - skill.ResourceCost));
        }

        [Test]
        public void NoTriggerListensToKeys()
        {
            foreach (var gem in TestWorld.Data.Gems.All)
            {
                if (gem.Kind != GemKind.Trigger || gem.Trigger!.Condition == TriggerCondition.Idle)
                    continue;
                var (loop, input, _) = WithTrigger(gem);
                loop.State.Resource = 50;

                input.PressKeys(5);
                input.PressEnter();
                input.PressSpace();
                input.PressBackspace();
                loop.Tick();

                Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(0), gem.Id);
            }
        }

        [Test]
        public void WithoutTriggerMouseDoesNothing()
        {
            var (loop, input, _) = WithTrigger(null);

            input.MoveMouse(5000);
            loop.Tick();

            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(0));
        }

        [Test]
        public void FlickDuringCooldownWaitsForTheNextFlickAfterIt()
        {
            var (loop, input, slot) = WithTrigger(Gem("cast_on_flick"));
            input.Flick();
            loop.Tick();
            var cooldown = loop.State.Skills[slot].CooldownTicks;
            loop.State.Resource = 100;

            for (var i = 0; i < cooldown; i++)
            {
                if (i % 50 == 0)
                    input.Flick();
                loop.Tick();
            }
            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(1));

            loop.State.Resource = 100;
            input.Flick();
            loop.Tick();
            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(2));
        }

        [Test]
        public void TriggerWithoutEnergyDoesNotCast()
        {
            var (loop, input, slot) = WithTrigger(Gem("cast_on_flick"));
            loop.State.Resource = 0;

            input.Flick();
            loop.Tick();

            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(0));
            Assert.That(loop.State.Skills[slot].Ready, Is.True);
        }
    }
}
