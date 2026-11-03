using System.Linq;
using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class CarrierSupportTests
    {
        private static GemDef Gem(string id) => TestWorld.Data.Gems[id];

        private static GameLoop Hero(FakeInputSource input, HeroClass heroClass = HeroClass.Runedancer)
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            var state = TestWorld.WithAllSkillsOpen(new GameState { Class = heroClass, RecordStage = 1000 });
            var loop = TestWorld.Loop(input, state, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            return loop;
        }

        private static int Carried(GameLoop loop, string carrier, SkillSlotRole role = SkillSlotRole.Left)
        {
            var slot = loop.Skills.Equip(Gem("runedancer_hit_1"), role: role);
            Assert.That(loop.Book.SetSupport(slot, 0, Gem(carrier)), Is.EqualTo(BookResult.Done));
            loop.State.Resource = 100;
            loop.State.Skills[slot].CooldownTicks = 0;
            return slot;
        }

        private static double ManualPower(GameLoop loop, int slot)
        {
            loop.State.Resource = 100;
            loop.State.Skills[slot].CooldownTicks = 0;
            Assert.That(loop.Skills.TryCast(slot), Is.EqualTo(CastResult.Cast));
            return loop.State.Skills[slot].CastPower;
        }

        [Test]
        public void ThereAreFiveCarriersAndAllGoOnlyIntoTheFirstSocket()
        {
            var carriers = TestWorld.Data.Gems.All.Where(gem => gem.Support?.IsCarrier == true).ToList();
            Assert.That(carriers.Select(gem => gem.Support!.CarrierOn), Is.EquivalentTo(new[]
                { CarrierTrigger.Boss, CarrierTrigger.FullResource, CarrierTrigger.Keys, CarrierTrigger.Mouse, CarrierTrigger.LowHp }));
            Assert.That(carriers.Select(gem => gem.Socket), Is.All.EqualTo(1));

            var loop = Hero(new FakeInputSource());
            var slot = loop.Skills.Equip(Gem("runedancer_hit_1"), role: SkillSlotRole.Left);
            Assert.That(loop.Book.SocketsAt(slot), Is.GreaterThanOrEqualTo(3));
            Assert.That(loop.Book.SetSupport(slot, 1, Gem("carrier_keys")), Is.EqualTo(BookResult.WrongSocket));
            Assert.That(loop.Book.SetSupport(slot, 0, Gem("carrier_keys")), Is.EqualTo(BookResult.Done));
        }

        [Test]
        public void TheKeyCarrierCastsTheButtonSkillAfterFiftyKeysAtSeventyPercent()
        {
            var input = new FakeInputSource();
            var loop = Hero(input);
            var slot = Carried(loop, "carrier_keys");
            var manual = ManualPower(loop, slot);
            loop.State.Skills[slot].CooldownTicks = 0;
            loop.State.Resource = 100;
            var before = loop.State.Skills[slot].LastCastTick;

            for (var key = 0; key < 49; key++)
            {
                input.PressKeys(1);
                loop.Tick();
            }
            Assert.That(loop.State.Skills[slot].CarrierCount, Is.EqualTo(49));
            Assert.That(loop.State.Skills[slot].LastCastTick, Is.EqualTo(before), "49 keys are not enough");

            input.PressKeys(1);
            loop.Tick();
            loop.Tick();
            Assert.That(loop.State.Skills[slot].LastCastTick, Is.GreaterThan(before));
            Assert.That(loop.State.Skills[slot].CarrierCount, Is.Zero);
            Assert.That(loop.State.Skills[slot].CastPower, Is.EqualTo(manual * 0.7).Within(manual * 1e-6));
        }

        [Test]
        public void TheMouseCarrierCastsAfterFiveThousandPixels()
        {
            var input = new FakeInputSource();
            var loop = Hero(input);
            var slot = Carried(loop, "carrier_mouse", SkillSlotRole.Right);
            var before = loop.State.Skills[slot].LastCastTick;

            input.MoveMouse(4000);
            loop.Tick();
            Assert.That(loop.State.Skills[slot].LastCastTick, Is.EqualTo(before));

            input.MoveMouse(1000);
            loop.Tick();
            Assert.That(loop.State.Skills[slot].LastCastTick, Is.GreaterThan(before));
        }

        [Test]
        public void ACarrierOnAnAutoSkillDoesNothing()
        {
            var input = new FakeInputSource();
            var loop = Hero(input);
            var slot = Carried(loop, "carrier_keys", SkillSlotRole.Auto);
            loop.State.Skills[slot].Role = SkillSlotRole.Auto;
            loop.State.Skills[slot].CooldownTicks = 1000;

            input.PressKeys(60);
            loop.Tick();

            Assert.That(loop.State.Skills[slot].CarrierCount, Is.Zero);
        }

        [Test]
        public void TheLowHpCarrierWaitsForTheEnemyToWeaken()
        {
            var input = new FakeInputSource();
            var loop = Hero(input);
            var slot = Carried(loop, "carrier_low");
            var before = loop.State.Skills[slot].LastCastTick;

            loop.Tick();
            Assert.That(loop.State.Skills[slot].LastCastTick, Is.EqualTo(before));

            loop.State.EnemyHp = loop.State.EnemyMaxHp * 0.2;
            loop.State.Skills[slot].CooldownTicks = 0;
            loop.State.Resource = 100;
            loop.Tick();
            Assert.That(loop.State.Skills[slot].LastCastTick, Is.GreaterThan(before));
        }

        [Test]
        public void EveryPathSkillHasOneGreatSupportForTheThirdSocket()
        {
            var great = TestWorld.Data.Gems.All.Where(gem => gem.ForSkill != null).ToList();

            Assert.That(great.Count, Is.EqualTo(54));
            Assert.That(great.Select(gem => gem.ForSkill), Is.EquivalentTo(TestWorld.Data.Gems.All.Where(gem => gem.Ascendancy != null).Select(gem => gem.Id)));
            Assert.That(great.Select(gem => gem.Socket), Is.All.EqualTo(3));
            Assert.That(great.Select(gem => gem.Support!.Power), Is.All.EqualTo(1.2).Within(1e-9));
        }

        [Test]
        public void AGreatSupportFitsOnlyItsPathSkillInTheThirdSocket()
        {
            var loop = Hero(new FakeInputSource(), HeroClass.Duelist);
            Assert.That(loop.CompleteActivity("garden"), Is.True);
            Assert.That(loop.Book.SupportOpen(Gem("great_duelist_path_1")), Is.False, "no path yet");
            Assert.That(loop.ChooseSubclass("duelist_trait_1"), Is.EqualTo(SubclassResult.Chosen));
            Assert.That(loop.Book.Equip(Gem("duelist_path_1")), Is.EqualTo(BookResult.Done));
            var slot = loop.Book.RowOf(Gem("duelist_path_1"));
            var great = Gem("great_duelist_path_1");

            Assert.That(loop.Book.SupportOpen(great), Is.True);
            Assert.That(loop.Book.SupportOpen(Gem("great_duelist_path_2")), Is.False, "another path");
            Assert.That(loop.Book.SetSupport(slot, 0, great), Is.EqualTo(BookResult.WrongSocket));
            Assert.That(loop.Book.SetSupport(slot, 2, great), Is.EqualTo(BookResult.Done));

            var other = loop.Skills.Equip(Gem("duelist_hit_1"), role: SkillSlotRole.Left);
            Assert.That(loop.Book.SetSupport(other, 2, great), Is.EqualTo(BookResult.DoesNotFit));
        }
    }
}
