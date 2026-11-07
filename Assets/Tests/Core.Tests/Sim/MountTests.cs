using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class MountTests
    {
        private static MountRules Rules => TestWorld.Data.Combat.Mount;

        private static GameLoop Rich(long record)
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.State.RecordStage = record;
            loop.State.Gold = BigNumber.Create(1, 300);
            loop.State.Mount.Scan = 1e9;
            loop.Tick();
            return loop;
        }

        [Test]
        public void EightSlotsOfTwentyFiveLevelsAtFourPercentEach()
        {
            Assert.That(MountRules.Slots, Is.EqualTo(8));
            Assert.That(Rules.MaxLevel, Is.EqualTo(25));
            Assert.That(Rules.Factor(25), Is.EqualTo(System.Math.Pow(1.04, 25)).Within(1e-9));
            Assert.That(Rules.Unlocked(1999), Is.EqualTo(0));
            Assert.That(Rules.Unlocked(50000), Is.EqualTo(25));
        }

        [Test]
        public void ALevelOpensForEveryTwoThousandRecordStages()
        {
            var loop = Rich(4000);
            Assert.That(loop.Mount.Raise(MountSystem.WeaponSlot), Is.EqualTo(MountResult.Done));
            Assert.That(loop.Mount.Raise(MountSystem.WeaponSlot), Is.EqualTo(MountResult.Done));
            Assert.That(loop.Mount.Raise(MountSystem.WeaponSlot), Is.EqualTo(MountResult.Locked));
            Assert.That(loop.Mount.Raise(8), Is.EqualTo(MountResult.BadSlot));
            Assert.That(loop.State.Mount.Cutters, Is.EqualTo(2 * Rules.CuttersPerLevel));
        }

        [Test]
        public void RaisingCostsScanAndGold()
        {
            var loop = Rich(2000);
            loop.State.Mount.Scan = Rules.ScanCost(0) - 1;
            Assert.That(loop.Mount.Raise(1), Is.EqualTo(MountResult.NoScan));
            loop.State.Mount.Scan = Rules.ScanCost(0);
            loop.State.Gold = BigNumber.Zero;
            Assert.That(loop.Mount.Raise(1), Is.EqualTo(MountResult.NoGold));
            loop.State.Gold = loop.Mount.GoldCost(1);
            Assert.That(loop.Mount.Raise(1), Is.EqualTo(MountResult.Done));
            Assert.That(loop.State.Mount.Scan, Is.EqualTo(0).Within(1e-9));
            Assert.That(Rules.ScanCost(5), Is.GreaterThan(Rules.ScanCost(0)));
        }

        [Test]
        public void TheWeaponSlotRaisesTheWeaponDamageUnderTheRoot()
        {
            var loop = Rich(2000);
            var before = loop.Combat.Attack;
            loop.Mount.Raise(MountSystem.WeaponSlot);
            Assert.That(loop.Combat.Attack, Is.EqualTo(before * System.Math.Sqrt(Rules.Step)).Within(1e-9));
        }

        [Test]
        public void ANewItemGetsTheWholeMountOfItsSlot()
        {
            var loop = Rich(2000);
            loop.Mount.Raise(MountSystem.SlotOf(GearSlot.Helmet));
            Assert.That(TestWorld.Data.Items.TryGetBase("mail_coif", out var coifBase), Is.True);
            var coif = new GearItem(coifBase, ItemRarity.Rare, 1, System.Array.Empty<GearAffix>());
            loop.State.Drops.Gear.Add(coif);
            loop.Gear.Equip(coif);
            Assert.That(loop.Combat.ItemAttribute(), Is.EqualTo(TestWorld.Data.Combat.Attack.Attribute(ItemRarity.Rare, 1) * Rules.Step).Within(1e-9));
        }

        [Test]
        public void AFullMountAddsDamageToTheWholeHero()
        {
            Assert.That(Rules.FullDamage(9), Is.Zero);
            Assert.That(Rules.FullDamage(10), Is.EqualTo(0.05));
            Assert.That(Rules.FullDamage(25), Is.EqualTo(0.15));
            var loop = Rich(1);
            for (var i = 0; i < MountRules.Slots; i++)
                loop.State.Mount.Levels[i] = 10;
            loop.Mount.Refresh();
            loop.RefreshClassAttribute();
            Assert.That(loop.Combat.Additional, Is.EqualTo(1.05).Within(1e-9), "the full mount sits in the additional damage group");
        }

        [Test]
        public void SalvagingGivesScanByRarity()
        {
            var loop = Rich(1);
            loop.State.Mount.Scan = 0;
            Assert.That(TestWorld.Data.Items.TryGetBase("mail_coif", out var coifBase), Is.True);
            var coif = new GearItem(coifBase, ItemRarity.Epic, 1, System.Array.Empty<GearAffix>());
            loop.Mount.NoteSalvage(new ItemDrop(DropSource.Record, 1, null, coif, true, 0));
            Assert.That(loop.State.Mount.Scan, Is.EqualTo(Rules.ScanFor((int)ItemRarity.Epic)));
        }

        [Test]
        public void TheMountSurvivesASave()
        {
            var loop = Rich(4000);
            loop.Mount.Raise(3);
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty),
                TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Mount.Levels[3], Is.EqualTo(1));
            Assert.That(copy.Mount.Cutters, Is.EqualTo(Rules.CuttersPerLevel));
            Assert.That(copy.Mount.Scan, Is.EqualTo(loop.State.Mount.Scan).Within(1e-6));
        }
    }
}
