using System;
using Game.Core.Clock;
using Game.Core.Content.Gems;
using Game.Core.Save;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class SkillSystemTests
    {
        private static GemDef Gem(string id) => TestWorld.Data.Gems[id];

        private static void Idle(GameLoop loop, int ticks)
        {
            for (var i = 0; i < ticks; i++)
                loop.Tick();
        }

        [Test]
        public void EquippedSkillGoesIntoTheNextSlot()
        {
            var loop = TestWorld.Loop();

            Assert.That(loop.Skills.Equip(Gem("stenography")), Is.EqualTo(0));
            Assert.That(loop.Skills.Equip(Gem("march")), Is.EqualTo(1));
            Assert.That(loop.State.Skills.Count, Is.EqualTo(2));
        }

        [Test]
        public void OnlySkillGemsGoIntoSlotsAndOnlyTriggersIntoTriggerSlots()
        {
            var loop = TestWorld.Loop();

            Assert.Throws<ArgumentException>(() => loop.Skills.Equip(Gem("cast_on_enter")));
            Assert.Throws<ArgumentException>(() => loop.Skills.Equip(Gem("march"), Gem("lull")));
        }

        [Test]
        public void CastSpendsEnergyAndStartsCooldown()
        {
            var loop = TestWorld.Loop();
            var slot = loop.Skills.Equip(Gem("stenography"));
            loop.State.Energy = 100;

            Assert.That(loop.Skills.TryCast(slot), Is.EqualTo(CastResult.Cast));

            var skill = Gem("stenography").Skill!;
            Assert.That(loop.State.Energy, Is.EqualTo(100 - skill.EnergyCost));
            Assert.That(loop.State.Skills[slot].CooldownTicks, Is.EqualTo(skill.CooldownSeconds * GameLoop.TicksPerSecond));
            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(1));
        }

        [Test]
        public void CastIsRefusedWithoutEnergyAndKeepsEnergy()
        {
            var loop = TestWorld.Loop();
            var slot = loop.Skills.Equip(Gem("stenography"));
            loop.State.Energy = 10;

            Assert.That(loop.Skills.TryCast(slot), Is.EqualTo(CastResult.NotEnoughEnergy));
            Assert.That(loop.State.Energy, Is.EqualTo(10));
            Assert.That(loop.State.Skills[slot].Ready, Is.True);
        }

        [Test]
        public void CooldownBlocksUntilItRunsOut()
        {
            var loop = TestWorld.Loop();
            var slot = loop.Skills.Equip(Gem("stenography"));
            loop.State.Energy = 100;
            loop.Skills.TryCast(slot);
            var cooldownTicks = loop.State.Skills[slot].CooldownTicks;
            loop.State.Energy = 100;

            Idle(loop, cooldownTicks - 1);
            Assert.That(loop.Skills.TryCast(slot), Is.EqualTo(CastResult.OnCooldown));

            Idle(loop, 1);
            Assert.That(loop.Skills.TryCast(slot), Is.EqualTo(CastResult.Cast));
        }

        [Test]
        public void DurationCountsDown()
        {
            var loop = TestWorld.Loop();
            var slot = loop.Skills.Equip(Gem("march"));
            loop.State.Notes = 4;

            loop.Skills.TryCast(slot);
            Assert.That(loop.State.Skills[slot].ActiveTicks, Is.EqualTo(Gem("march").Skill!.DurationSeconds * GameLoop.TicksPerSecond));

            Idle(loop, 10);
            Assert.That(loop.State.Skills[slot].ActiveTicks, Is.EqualTo((Gem("march").Skill!.DurationSeconds - 1) * GameLoop.TicksPerSecond));
        }

        [Test]
        public void NoteSkillNeedsNotesAndSpendsThem()
        {
            var loop = TestWorld.Loop();
            var slot = loop.Skills.Equip(Gem("march"));
            loop.State.Notes = 3;

            Assert.That(loop.Skills.TryCast(slot), Is.EqualTo(CastResult.NotEnoughNotes));

            loop.State.Notes = 4;
            Assert.That(loop.Skills.TryCast(slot), Is.EqualTo(CastResult.Cast));
            Assert.That(loop.State.Notes, Is.EqualTo(0));
        }

        [Test]
        public void TriggeredCastHasLongerCooldown()
        {
            var loop = TestWorld.Loop();
            var slot = loop.Skills.Equip(Gem("stenography"), Gem("cast_on_enter"));
            loop.State.Energy = 100;

            loop.Skills.TryCast(slot, triggered: true);

            var expected = Gem("stenography").Skill!.CooldownSeconds * Gem("cast_on_enter").Trigger!.CooldownMultiplier;
            Assert.That(loop.State.Skills[slot].CooldownTicks, Is.EqualTo(expected * GameLoop.TicksPerSecond));
        }

        [Test]
        public void PassiveSkillCannotBeCastAndEmptySlotIsReported()
        {
            var loop = TestWorld.Loop();
            var slot = loop.Skills.Equip(Gem("lull"));

            Assert.That(loop.Skills.TryCast(slot), Is.EqualTo(CastResult.Passive));
            Assert.That(loop.Skills.TryCast(5), Is.EqualTo(CastResult.NoSkill));
        }

        [Test]
        public void SkillSlotsAreSaved()
        {
            var loop = TestWorld.Loop();
            var slot = loop.Skills.Equip(Gem("stenography"), Gem("cast_on_enter"));
            loop.Skills.Equip(Gem("lull"));
            loop.State.Energy = 100;
            loop.Skills.TryCast(slot);
            Idle(loop, 7);

            var clock = ClockStamp.First(new ClockReading(1, 1, 1));
            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, clock), TestWorld.Data).State;

            Assert.That(loaded.Skills.Count, Is.EqualTo(2));
            Assert.That(loaded.Skills[0].Skill, Is.SameAs(Gem("stenography")));
            Assert.That(loaded.Skills[0].Trigger, Is.SameAs(Gem("cast_on_enter")));
            Assert.That(loaded.Skills[0].CooldownTicks, Is.EqualTo(loop.State.Skills[0].CooldownTicks));
            Assert.That(loaded.Skills[1].Trigger, Is.Null);
        }
    }
}
