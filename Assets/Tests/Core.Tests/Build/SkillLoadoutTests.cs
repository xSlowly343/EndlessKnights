using System.Linq;
using Game.Core.Build;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class SkillLoadoutTests
    {
        private static GemDef Gem(string id) => TestWorld.Data.Gems[id];

        private static GameLoop WithGems(params string[] ids)
        {
            var loop = TestWorld.Loop();
            foreach (var id in ids)
                loop.State.OwnedGems.Add(Gem(id));
            return loop;
        }

        private static GameLoop Fighting(FakeInputSource input)
        {
            var loop = TestWorld.Loop(input, TestWorld.WithAllWeaponPlacesOpen(new GameState { Class = HeroClass.Ronin }));
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            return loop;
        }

        private static int CastsOver(GameLoop loop, int ticks)
        {
            var casts = 0;
            for (var i = 0; i < ticks; i++)
            {
                loop.State.Resource = 100;
                loop.Tick();
                casts += loop.Events.CountOf(GameEventKind.SkillCast);
                loop.Events.Clear();
            }
            return casts;
        }

        private static SkillSlotRole[] Roles(GameLoop loop) => loop.State.Skills.Select(slot => slot.Role).ToArray();

        [Test]
        public void SkillsTakeTheButtonsFirstThenTheAutomaticSlots()
        {
            var loop = WithGems("through_stride", "key_drum", "lull");

            foreach (var id in new[] { "through_stride", "key_drum", "lull" })
                Assert.That(loop.Book.Equip(Gem(id)), Is.EqualTo(BookResult.Done));

            Assert.That(Roles(loop), Is.EqualTo(new[] { SkillSlotRole.Left, SkillSlotRole.Right, SkillSlotRole.Auto }));
        }

        [Test]
        public void AurasTakeTheAuraSlotsFirstAndThenAnySlot()
        {
            var auras = new[] { "march", "golden_fever", "tailwind", "exposure", "freeze_frame", "overheat", "quick_stride", "lasso" };
            var loop = WithGems(auras);

            for (var i = 0; i < 7; i++)
                Assert.That(loop.Book.Equip(Gem(auras[i])), Is.EqualTo(BookResult.Done));

            Assert.That(Roles(loop), Is.EqualTo(new[]
            {
                SkillSlotRole.Aura, SkillSlotRole.Aura, SkillSlotRole.Aura, SkillSlotRole.Aura,
                SkillSlotRole.Left, SkillSlotRole.Right, SkillSlotRole.Auto
            }));
            Assert.That(loop.Book.Equip(Gem(auras[7])), Is.EqualTo(BookResult.NoFreeSlot));
        }

        [Test]
        public void AnAuraSlotTakesOnlyAuras()
        {
            var loop = WithGems("through_stride", "march");
            loop.Book.Equip(Gem("march"));
            loop.Book.Equip(Gem("through_stride"));

            Assert.That(loop.State.Skills.Count(slot => slot.Role == SkillSlotRole.Aura), Is.EqualTo(1));
            Assert.That(loop.Book.Assign(1, SkillSlotRole.Aura), Is.EqualTo(BookResult.WrongSlot));
            Assert.That(loop.Book.Assign(0, SkillSlotRole.Auto), Is.EqualTo(BookResult.Done));
            Assert.That(loop.State.Skills[0].Role, Is.EqualTo(SkillSlotRole.Auto));
        }

        [Test]
        public void OneSkillStandsInOneSlot()
        {
            var loop = WithGems("through_stride", "through_stride");

            Assert.That(loop.Book.Equip(Gem("through_stride")), Is.EqualTo(BookResult.Done));
            Assert.That(loop.Book.Equip(Gem("through_stride")), Is.EqualTo(BookResult.AlreadyInBook));
        }

        [Test]
        public void TheButtonsSwapWhenASkillMovesOntoATakenButton()
        {
            var loop = WithGems("through_stride", "key_drum", "lull");
            loop.Book.Equip(Gem("through_stride"));
            loop.Book.Equip(Gem("key_drum"));
            loop.Book.Equip(Gem("lull"));

            Assert.That(loop.Book.Assign(1, SkillSlotRole.Left), Is.EqualTo(BookResult.Done));
            Assert.That(Roles(loop), Is.EqualTo(new[] { SkillSlotRole.Right, SkillSlotRole.Left, SkillSlotRole.Auto }));
            Assert.That(loop.Book.Assign(0, SkillSlotRole.Auto), Is.EqualTo(BookResult.NoFreeSlot), "the only open automatic slot is taken");
        }

        [Test]
        public void AnAutomaticSlotCastsOnItsCooldownWithoutClicks()
        {
            var input = new FakeInputSource();
            var loop = Fighting(input);
            loop.Skills.Equip(Gem("through_stride"), role: SkillSlotRole.Auto);
            var cooldownTicks = (int)(Gem("through_stride").Skill!.CooldownSeconds * GameLoop.TicksPerSecond);

            Assert.That(CastsOver(loop, 1), Is.EqualTo(1));
            Assert.That(CastsOver(loop, cooldownTicks - 2), Is.EqualTo(0));
            Assert.That(CastsOver(loop, 4), Is.EqualTo(1));
        }

        [Test]
        public void AButtonSlotWaitsForItsClick()
        {
            var input = new FakeInputSource();
            var loop = Fighting(input);
            loop.Skills.Equip(Gem("through_stride"), role: SkillSlotRole.Left);
            loop.Skills.Equip(Gem("lightning_zigzag"), role: SkillSlotRole.Right);

            Assert.That(CastsOver(loop, 50), Is.EqualTo(0));
            input.ClickLeft();
            Assert.That(CastsOver(loop, 1), Is.EqualTo(1));
            Assert.That(loop.State.Skills[0].Ready, Is.False);
            Assert.That(loop.State.Skills[1].Ready, Is.True);
        }

        [Test]
        public void TheLeftButtonFollowsTheLeftSlotWhereverItStands()
        {
            var input = new FakeInputSource();
            var loop = Fighting(input);
            loop.Skills.Equip(Gem("march"), role: SkillSlotRole.Aura);
            loop.Skills.Equip(Gem("through_stride"), role: SkillSlotRole.Left);
            CastsOver(loop, 1);

            input.ClickLeft();
            Assert.That(CastsOver(loop, 1), Is.EqualTo(1));
            Assert.That(loop.State.Skills[1].Ready, Is.False);
        }

        [Test]
        public void AnAuraSlotKeepsItsAuraUp()
        {
            var input = new FakeInputSource();
            var loop = Fighting(input);
            loop.Skills.Equip(Gem("march"), role: SkillSlotRole.Aura);
            var durationTicks = (int)(Gem("march").Skill!.DurationSeconds * GameLoop.TicksPerSecond);

            Assert.That(CastsOver(loop, 1), Is.EqualTo(1));
            Assert.That(loop.State.Skills[0].AuraApplied, Is.True);
            Assert.That(CastsOver(loop, durationTicks - 2), Is.EqualTo(0));
            Assert.That(CastsOver(loop, 3), Is.EqualTo(1));
            Assert.That(loop.State.Skills[0].AuraApplied, Is.True);
        }

        [Test]
        public void APresetKeepsWhereEachSkillStands()
        {
            var loop = WithGems("through_stride", "key_drum", "march");
            loop.Book.Equip(Gem("through_stride"));
            loop.Book.Equip(Gem("key_drum"));
            loop.Book.Equip(Gem("march"));
            loop.Book.Assign(1, SkillSlotRole.Left);
            var before = Roles(loop);

            loop.SwitchPreset();
            Assert.That(loop.State.Skills, Is.Empty);
            loop.SwitchPreset();

            Assert.That(Roles(loop), Is.EqualTo(before));
            Assert.That(before, Is.EqualTo(new[] { SkillSlotRole.Right, SkillSlotRole.Left, SkillSlotRole.Aura }));
        }

        [Test]
        public void TheSaveKeepsWhereEachSkillStands()
        {
            var loop = WithGems("through_stride", "key_drum", "march");
            loop.Book.Equip(Gem("through_stride"));
            loop.Book.Equip(Gem("key_drum"));
            loop.Book.Equip(Gem("march"));
            loop.Book.Assign(0, SkillSlotRole.Right);

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12))), TestWorld.Data).State;

            Assert.That(loaded.Skills.Select(slot => slot.Role), Is.EqualTo(Roles(loop)));
        }
    }
}
