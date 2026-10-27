using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Clock;
using Game.Core.Content.Gems;
using Game.Core.Content.Tree;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class SupportSocketTests
    {
        private static GemDef Gem(string id) => TestWorld.Data.Gems[id];

        private static GameLoop Hero(params Modifier[] modifiers)
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(new FakeInputSource(), TestWorld.WithAllWeaponPlacesOpen(TestWorld.Classless(new GameState { Class = HeroClass.Viking })), hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.OwnedGems.Clear();
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            return loop;
        }

        private static int Equip(GameLoop loop, string skill, params string[] owned)
        {
            var gem = Gem(skill);
            TestWorld.ArmFor(loop, gem);
            loop.State.OwnedGems.Add(gem);
            foreach (var id in owned)
                loop.State.OwnedGems.Add(Gem(id));
            Assert.That(loop.Book.Equip(gem), Is.EqualTo(BookResult.Done));
            return loop.State.Skills.Count - 1;
        }

        private static Modifier Attribute(string attribute, double value) =>
            new Modifier(PassiveTree.AttributeStat(attribute), ModifierKind.Flat, value);

        private static double CastDamage(GameLoop loop, int slot)
        {
            loop.State.Resource = 100;
            loop.State.Skills[slot].CooldownTicks = 0;
            loop.Events.Clear();
            Assert.That(loop.Skills.TryCast(slot), Is.EqualTo(CastResult.Cast));
            var total = 0.0;
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage && loop.Events[i].Source == DamageSource.Skill)
                    total += loop.Events[i].Amount.ToDouble();
            }
            return total;
        }

        [Test]
        public void OneSocketAtStartAndOneMoreForRecordsFiftyAndTwoHundredFifty()
        {
            var loop = Hero();

            Assert.That(loop.Book.SocketCount, Is.EqualTo(1));
            loop.State.RecordStage = 50;
            Assert.That(loop.Book.SocketCount, Is.EqualTo(2));
            loop.State.RecordStage = 250;
            Assert.That(loop.Book.SocketCount, Is.EqualTo(3));
        }

        [Test]
        public void SupportGoesOnlyIntoAnOpenSocket()
        {
            var loop = Hero();
            var slot = Equip(loop, "iron_rim_bash", "rebound");

            Assert.That(loop.Book.SetSupport(slot, 1, Gem("rebound")), Is.EqualTo(BookResult.NoSuchSocket));
            Assert.That(loop.Book.SetSupport(slot, 0, Gem("rebound")), Is.EqualTo(BookResult.Done));
            Assert.That(loop.State.Skills[slot].Supports[0], Is.SameAs(Gem("rebound")));
        }

        [Test]
        public void EverySupportOnlyOncePerBuild()
        {
            var loop = Hero();
            var first = Equip(loop, "iron_rim_bash", "rebound", "rebound");
            var second = Equip(loop, "march");

            Assert.That(loop.Book.SetSupport(first, 0, Gem("rebound")), Is.EqualTo(BookResult.Done));
            Assert.That(loop.Book.SetSupport(second, 0, Gem("rebound")), Is.EqualTo(BookResult.AlreadyInBuild));
            Assert.That(loop.Book.SetSupport(first, 0, Gem("rebound")), Is.EqualTo(BookResult.Done));
        }

        [Test]
        public void SocketedSupportIsNoLongerAFreeCopy()
        {
            var loop = Hero();
            var slot = Equip(loop, "iron_rim_bash", "rebound");

            loop.Book.SetSupport(slot, 0, Gem("rebound"));
            Assert.That(loop.Book.FreeCopies(Gem("rebound")), Is.EqualTo(0));

            loop.Book.SetSupport(slot, 0, null);
            Assert.That(loop.Book.FreeCopies(Gem("rebound")), Is.EqualTo(1));
        }

        [Test]
        public void SupportMustBeOwned()
        {
            var loop = Hero();
            var slot = Equip(loop, "iron_rim_bash");

            Assert.That(loop.Book.SetSupport(slot, 0, Gem("rebound")), Is.EqualTo(BookResult.NotOwned));
            Assert.That(loop.Book.SetSupport(slot, 0, Gem("march")), Is.EqualTo(BookResult.WrongKind));
        }

        [Test]
        public void ColoredSupportNeedsFifteenPointsOfItsAttribute()
        {
            var weak = Hero();
            var strong = Hero(Attribute("dexterity", 15));
            var weakSlot = Equip(weak, "iron_rim_bash", "chain");
            var strongSlot = Equip(strong, "iron_rim_bash", "chain");

            Assert.That(weak.Book.SetSupport(weakSlot, 0, Gem("chain")), Is.EqualTo(BookResult.TooManyColored));
            Assert.That(strong.Book.SetSupport(strongSlot, 0, Gem("chain")), Is.EqualTo(BookResult.Done));
            Assert.That(SkillBook.AttributeOf(GemColor.White), Is.Null);
        }

        [Test]
        public void SupportWithoutItsMechanicWaits()
        {
            var catalog = GemCatalog.Load(TestData.ReadGameDataFile(GemCatalog.FileName).Replace("\"gems\": [",
                "\"gems\": [\n    { \"id\": \"echo_keys\", \"kind\": \"support\", \"color\": \"white\", \"name\": { \"ru\": \"Эхо\", \"en\": \"Echo\" }, \"effect\": { \"type\": \"repeatEveryKeys\" } },"));
            var loop = Hero();
            var slot = Equip(loop, "iron_rim_bash");
            loop.State.OwnedGems.Add(catalog["echo_keys"]);

            Assert.That(loop.Book.SetSupport(slot, 0, catalog["echo_keys"]), Is.EqualTo(BookResult.WaitsForMechanic));
        }

        [Test]
        public void ExecutionerDoublesItsSkillOnWoundedEnemies()
        {
            var plain = Hero(Attribute("intelligence", 15));
            var supported = Hero(Attribute("intelligence", 15));
            var plainSlot = Equip(plain, "iron_rim_bash");
            var slot = Equip(supported, "iron_rim_bash", "executioner");
            supported.Book.SetSupport(slot, 0, Gem("executioner"));
            foreach (var loop in new[] { plain, supported })
                loop.State.EnemyHp = loop.State.EnemyMaxHp * 0.2;

            Assert.That(CastDamage(supported, slot) / CastDamage(plain, plainSlot), Is.EqualTo(2).Within(1e-6));
        }

        [Test]
        public void ExecutionerDoesNothingOnHealthyEnemies()
        {
            var plain = Hero(Attribute("intelligence", 15));
            var supported = Hero(Attribute("intelligence", 15));
            var plainSlot = Equip(plain, "iron_rim_bash");
            var slot = Equip(supported, "iron_rim_bash", "executioner");
            supported.Book.SetSupport(slot, 0, Gem("executioner"));

            Assert.That(CastDamage(supported, slot) / CastDamage(plain, plainSlot), Is.EqualTo(1).Within(1e-6));
        }

        [Test]
        public void ChainSendsPartOfTheSkillHitToTheNextEnemy()
        {
            var loop = Hero(Attribute("dexterity", 15));
            var slot = Equip(loop, "iron_rim_bash", "chain");
            loop.Book.SetSupport(slot, 0, Gem("chain"));

            var hit = CastDamage(loop, slot);

            var share = Gem("chain").Support!.QueueShare;
            Assert.That(loop.State.QueueDamage[0].ToDouble(), Is.EqualTo(hit * share).Within(hit * 1e-6));
        }

        [Test]
        public void SupportDoesNotChangeKeyHits()
        {
            var loop = Hero(Attribute("dexterity", 15));
            var slot = Equip(loop, "iron_rim_bash", "chain");
            loop.Book.SetSupport(slot, 0, Gem("chain"));
            var input = new FakeInputSource();
            var typing = TestWorld.Loop(input, loop.State, loop.Hero);
            typing.State.HitsOnEnemy = 10;

            input.PressKeys(1);
            typing.Tick();

            Assert.That(typing.State.QueueDamage[0].Sign, Is.EqualTo(0));
        }

        [Test]
        public void ReboundShortensTheCooldownAndKillsCutItFurther()
        {
            var plain = Hero();
            var supported = Hero(new Modifier(CombatStats.Damage, ModifierKind.More, 1e60));
            var plainSlot = Equip(plain, "iron_rim_bash");
            var slot = Equip(supported, "iron_rim_bash", "rebound");
            supported.Book.SetSupport(slot, 0, Gem("rebound"));
            supported.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1));

            CastDamage(plain, plainSlot);
            CastDamage(supported, slot);

            var full = plain.State.Skills[plainSlot].CooldownTicks;
            Assert.That(supported.State.Skills[slot].CooldownTicks, Is.EqualTo((int)System.Math.Round(full * 0.9) - GameLoop.TicksPerSecond));
        }

        [Test]
        public void SupportsSurviveSaveAndLoad()
        {
            var loop = Hero();
            var slot = Equip(loop, "iron_rim_bash", "rebound");
            loop.Book.SetSupport(slot, 0, Gem("rebound"));

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State;

            Assert.That(loaded.Skills[slot].Supports[0], Is.SameAs(Gem("rebound")));
        }

        [Test]
        public void OldSavesWithoutSupportsStillLoad()
        {
            var save = SaveCodec.Decode(TestData.ReadSaveFixture("save_v2.json"), TestWorld.Data);

            foreach (var slot in save.State.Skills)
                Assert.That(slot.Supports, Is.All.Null);
        }

        [Test]
        public void PresetKeepsItsSupports()
        {
            var loop = Hero();
            var slot = Equip(loop, "iron_rim_bash", "rebound");
            loop.Book.SetSupport(slot, 0, Gem("rebound"));

            loop.SwitchPreset();
            Assert.That(loop.State.Skills, Is.Empty);
            loop.SwitchPreset();

            Assert.That(loop.State.Skills[0].Supports[0], Is.SameAs(Gem("rebound")));
        }
            [Test]
        public void ApprenticeTradesPowerForExperience()
        {
            var loop = Hero(new Modifier(CombatStats.Damage, ModifierKind.More, 1e60, StatTags.Keyboard));
            var slot = Equip(loop, "iron_rim_bash", "apprentice");
            loop.Book.SetSupport(slot, 0, Gem("apprentice"));
            loop.State.Resource = 100;
            loop.Skills.TryCast(slot);
            Assert.That(loop.State.Skills[slot].CastPower, Is.EqualTo(0.9).Within(1e-9));

            var input = new FakeInputSource();
            var typing = TestWorld.Loop(input, loop.State, loop.Hero);
            typing.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(10));
            input.PressKeys(1);
            typing.Tick();

            Assert.That(typing.State.GemProgress["iron_rim_bash"].Experience, Is.EqualTo(TestWorld.Data.Gems.Levels.ExperiencePerKill * 1.5).Within(1e-9));
        }

        [Test]
        public void BossHunterIsStrongOnBossesAndWeakerOnTheRest()
        {
            var plain = Hero(Attribute("intelligence", 15));
            var hunter = Hero(Attribute("intelligence", 15));
            var plainSlot = Equip(plain, "iron_rim_bash");
            var slot = Equip(hunter, "iron_rim_bash", "boss_hunter");
            hunter.Book.SetSupport(slot, 0, Gem("boss_hunter"));
            var support = Gem("boss_hunter").Support!;

            Assert.That(CastDamage(hunter, slot) / CastDamage(plain, plainSlot), Is.EqualTo(support.NormalMultiplier).Within(1e-6));

            foreach (var loop in new[] { plain, hunter })
                loop.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
            Assert.That(CastDamage(hunter, slot) / CastDamage(plain, plainSlot), Is.EqualTo(support.BossMultiplier).Within(1e-6));
        }

        [Test]
        public void DeathMarkHitsMarkedEnemiesHarderAndKillsGiveResource()
        {
            var loop = Hero(Attribute("intelligence", 15));
            var slot = Equip(loop, "iron_rim_bash", "death_mark");
            loop.Book.SetSupport(slot, 0, Gem("death_mark"));
            var support = Gem("death_mark").Support!;

            var first = CastDamage(loop, slot);
            var second = CastDamage(loop, slot);
            Assert.That(second / first, Is.EqualTo(support.MarkedMultiplier).Within(1e-6));

            loop.State.EnemyHp = BigNumber.FromDouble(1);
            loop.State.PendingResource = 0;
            CastDamage(loop, slot);
            Assert.That(loop.State.PendingResource, Is.EqualTo(support.KillResource).Within(1e-9));
        }
    }
}
