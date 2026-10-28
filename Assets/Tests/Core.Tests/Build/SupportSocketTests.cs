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

        private static double CastPowerWith(string support, System.Action<GameLoop> setup)
        {
            var loop = Hero(Attribute("strength", 30));
            var slot = Equip(loop, "iron_rim_bash", support);
            Assert.That(loop.Book.SetSupport(slot, 0, Gem(support)), Is.EqualTo(BookResult.Done));
            setup(loop);
            loop.State.Resource = 100;
            loop.Skills.TryCast(slot);
            return loop.State.Skills[slot].CastPower;
        }

        [Test]
        public void QuietTimeDoublesTheCastAfterALongPause()
        {
            var support = Gem("quiet_time").Support!;
            var pause = (long)(support.QuietSeconds * GameLoop.TicksPerSecond);

            Assert.That(CastPowerWith("quiet_time", loop => loop.State.IdleTicksBeforeInput = pause), Is.EqualTo(support.QuietPower).Within(1e-9));
            Assert.That(CastPowerWith("quiet_time", loop => loop.State.IdleTicksBeforeInput = pause - 1), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void BladeHungerGrowsWithKeyHitsSinceTheLastCastUpToItsCap()
        {
            var support = Gem("blade_hunger").Support!;
            var tenSteps = (long)(support.HungerHitsPerStep * 10);

            Assert.That(CastPowerWith("blade_hunger", loop => loop.State.KeyHits += tenSteps), Is.EqualTo(1 + 10 * support.HungerStep).Within(1e-9));
            Assert.That(CastPowerWith("blade_hunger", loop => loop.State.KeyHits += 1_000_000), Is.EqualTo(1 + support.HungerMax).Within(1e-9));
        }

        [Test]
        public void BladeHungerStartsAgainAfterEveryCast()
        {
            var loop = Hero(Attribute("strength", 30));
            var slot = Equip(loop, "iron_rim_bash", "blade_hunger");
            loop.Book.SetSupport(slot, 0, Gem("blade_hunger"));
            loop.State.KeyHits += 1000;
            loop.State.Resource = 100;
            loop.Skills.TryCast(slot);
            loop.State.Skills[slot].CooldownTicks = 0;
            loop.State.Resource = 100;

            loop.Skills.TryCast(slot);

            Assert.That(loop.State.Skills[slot].CastPower, Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void AccelerationGrowsWithFocusUpToItsCap()
        {
            var support = Gem("acceleration").Support!;

            Assert.That(CastPowerWith("acceleration", loop => loop.State.FocusMultiplier = 2), Is.EqualTo(1 + support.FocusBonus / support.FocusStep).Within(1e-9));
            Assert.That(CastPowerWith("acceleration", loop => loop.State.FocusMultiplier = 100), Is.EqualTo(1 + support.FocusBonusMax).Within(1e-9));
        }

        [Test]
        public void CritFocusLetsTheSkillCritFromFocusAndHitHarder()
        {
            var plain = Hero(Attribute("strength", 30));
            var focused = Hero(Attribute("strength", 30));
            var plainSlot = Equip(plain, "iron_rim_bash");
            var slot = Equip(focused, "iron_rim_bash", "crit_focus");
            focused.Book.SetSupport(slot, 0, Gem("crit_focus"));
            var support = Gem("crit_focus").Support!;
            focused.State.FocusMultiplier = 2;
            Assert.That(SupportEffects.CritChance(focused.State.Skills[slot], focused.State), Is.EqualTo(support.CritPerFocusStep / support.CritFocusStep).Within(1e-9));

            plain.State.FocusMultiplier = 1000;
            focused.State.FocusMultiplier = 1000;
            var ratio = CastDamage(focused, slot) / CastDamage(plain, plainSlot);

            Assert.That(ratio, Is.EqualTo(focused.Combat.CritMultiplier(StatTags.Skill) * support.CritMultiplier).Within(1e-6));
        }

        [Test]
        public void WideReachHitsTheEnemyAfterTheNext()
        {
            var loop = Hero();
            var slot = Equip(loop, "iron_rim_bash", "wide_reach");
            loop.Book.SetSupport(slot, 0, Gem("wide_reach"));
            var support = Gem("wide_reach").Support!;

            var hit = CastDamage(loop, slot);

            Assert.That(loop.State.QueueDamage[support.QueueSlot].ToDouble(), Is.EqualTo(hit * support.QueueShare).Within(hit * 1e-6));
        }

        [Test]
        public void DeferredPaymentCostsLessAndDrainsOverTime()
        {
            var loop = Hero();
            var gem = Gem("iron_rim_bash");
            var slot = Equip(loop, "iron_rim_bash", "deferred_payment");
            loop.Book.SetSupport(slot, 0, Gem("deferred_payment"));
            var support = Gem("deferred_payment").Support!;
            var cost = gem.Skill!.ResourceCost * support.CostMultiplier;
            loop.State.Resource = 100;

            Assert.That(loop.Skills.TryCast(slot), Is.EqualTo(CastResult.Cast));
            Assert.That(loop.State.Resource, Is.EqualTo(100).Within(1e-9));
            Assert.That(loop.State.Skills[slot].DeferredCost, Is.EqualTo(cost).Within(1e-9));

            for (var i = 0; i < GameLoop.TicksPerSecond; i++)
                loop.Skills.Tick(default, 0);

            Assert.That(loop.State.Skills[slot].DeferredCost, Is.EqualTo(cost * (1 - support.DeferPerSecond)).Within(1e-9));
            Assert.That(loop.State.Resource, Is.EqualTo(100 - cost * support.DeferPerSecond).Within(1e-9));
        }

        [Test]
        public void DeferredPaymentStillNeedsTheResourceAtCast()
        {
            var loop = Hero();
            var slot = Equip(loop, "iron_rim_bash", "deferred_payment");
            loop.Book.SetSupport(slot, 0, Gem("deferred_payment"));
            loop.State.Resource = 1;

            Assert.That(loop.Skills.TryCast(slot), Is.EqualTo(CastResult.NotEnoughResource));
        }

        private static int Supported(GameLoop loop, string skill, string support)
        {
            var slot = Equip(loop, skill, support);
            Assert.That(loop.Book.SetSupport(slot, 0, Gem(support)), Is.EqualTo(BookResult.Done));
            return slot;
        }

        private static void Wait(GameLoop loop, int ticks, FakeInputSource? input = null)
        {
            for (var i = 0; i < ticks; i++)
            {
                input?.PressKeys(1);
                loop.Tick();
            }
        }

        [Test]
        public void DoubleChargeStoresASecondCastAfterTheCooldown()
        {
            var loop = Hero();
            var slot = Supported(loop, "iron_rim_bash", "double_charge");
            var skill = loop.State.Skills[slot];
            loop.State.Resource = 100;
            loop.Skills.TryCast(slot);
            var cooldown = skill.CooldownTicks;
            Assert.That(skill.SpareCharges, Is.EqualTo(0));

            Wait(loop, cooldown);
            Assert.That(skill.Ready, Is.True);
            Assert.That(skill.SpareCharges, Is.EqualTo(0));
            Wait(loop, cooldown + 1);
            Assert.That(skill.SpareCharges, Is.EqualTo(1));

            loop.State.Resource = 100;
            Assert.That(loop.Skills.TryCast(slot), Is.EqualTo(CastResult.Cast));
            Assert.That(loop.Skills.TryCast(slot), Is.EqualTo(CastResult.Cast));
            Assert.That(loop.Skills.TryCast(slot), Is.EqualTo(CastResult.OnCooldown));
        }

        [Test]
        public void TwinsRepeatTheSkillLaterAtAThirdOfItsPower()
        {
            var plain = Hero();
            var twins = Hero();
            var plainSlot = Equip(plain, "iron_rim_bash");
            var slot = Supported(twins, "iron_rim_bash", "twins");
            var support = Gem("twins").Support!;
            var first = CastDamage(twins, slot);
            Assert.That(first, Is.EqualTo(CastDamage(plain, plainSlot)).Within(first * 1e-9));

            twins.Events.Clear();
            for (var i = 0; i < (int)(support.TwinSeconds * GameLoop.TicksPerSecond); i++)
                twins.Tick();
            var repeat = 0.0;
            for (var i = 0; i < twins.Events.Count; i++)
            {
                if (twins.Events[i].Kind == GameEventKind.Damage && twins.Events[i].Source == DamageSource.Skill)
                    repeat += twins.Events[i].Amount.ToDouble();
            }

            Assert.That(repeat, Is.EqualTo(first * support.TwinPower).Within(first * 1e-6));
        }

        [Test]
        public void LingeringLastsLongerAndHoldsWhileTyping()
        {
            var plain = Hero();
            var lingering = Hero();
            var plainSlot = Equip(plain, "space_hammer");
            var slot = Supported(lingering, "space_hammer", "lingering");
            var support = Gem("lingering").Support!;
            plain.State.Resource = 100;
            lingering.State.Resource = 100;
            plain.Skills.TryCast(plainSlot);
            lingering.Skills.TryCast(slot);
            var total = lingering.State.Skills[slot].ActiveTotalTicks;
            Assert.That(total, Is.EqualTo((int)System.Math.Round(plain.State.Skills[plainSlot].ActiveTotalTicks * support.DurationMultiplier)));

            var input = new FakeInputSource();
            var typing = TestWorld.Loop(input, lingering.State, lingering.Hero);
            Wait(typing, 20, input);

            Assert.That(typing.State.Skills[slot].ActiveTicks, Is.EqualTo(total));
        }

        [Test]
        public void InkPoisonPoisonsTheEnemyOnTheFirstSkillHits()
        {
            var loop = Hero(Attribute("strength", 30));
            var slot = Supported(loop, "iron_rim_bash", "ink_poison");
            var support = Gem("ink_poison").Support!;

            for (var i = 0; i < support.PoisonHitsMax + 2; i++)
                CastDamage(loop, slot);

            Assert.That(loop.State.PoisonStacks, Is.EqualTo(support.PoisonPerHit * support.PoisonHitsMax).Within(1e-9));
        }

        [Test]
        public void SeriesRhythmCutsTheCooldownEveryTwentyFiveHitsAndRewardsLongSeries()
        {
            var support = Gem("series_rhythm").Support!;
            var loop = Hero(Attribute("strength", 30));
            var slot = Supported(loop, "iron_rim_bash", "series_rhythm");
            loop.State.Resource = 100;
            loop.Skills.TryCast(slot);
            var before = loop.State.Skills[slot].CooldownTicks;
            loop.State.KeyStreak = support.RhythmEvery - 1;

            SupportEffects.OnKeyHit(loop.State.Skills[slot], loop.State);
            Assert.That(loop.State.Skills[slot].CooldownTicks, Is.EqualTo(before));
            loop.State.KeyStreak++;
            SupportEffects.OnKeyHit(loop.State.Skills[slot], loop.State);
            Assert.That(loop.State.Skills[slot].CooldownTicks, Is.EqualTo(before - (int)(support.RhythmCooldownSeconds * GameLoop.TicksPerSecond)));

            Assert.That(CastPowerWith("series_rhythm", l => l.State.KeyStreak = support.RhythmLongSeries + 1), Is.EqualTo(support.RhythmPower).Within(1e-9));
            Assert.That(CastPowerWith("series_rhythm", l => l.State.KeyStreak = support.RhythmLongSeries), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void SeriesFinishStrikesOnTheTwentiethHitAfterTheCast()
        {
            var support = Gem("series_finish").Support!;
            var loop = Hero(Attribute("strength", 30));
            var slot = Supported(loop, "iron_rim_bash", "series_finish");
            loop.State.Resource = 100;
            loop.Skills.TryCast(slot);
            loop.State.Resource = 0;
            var input = new FakeInputSource();
            var typing = TestWorld.Loop(input, loop.State, loop.Hero);
            typing.State.HitsOnEnemy = 10;

            for (var i = 0; i < support.FinishAfterHits - 1; i++)
            {
                input.PressKeys(1);
                typing.Tick();
                typing.Tick();
            }
            Assert.That(typing.State.Skills[slot].FinishHits, Is.EqualTo(support.FinishAfterHits - 1));

            typing.Events.Clear();
            input.PressKeys(1);
            typing.Tick();

            Assert.That(typing.State.Skills[slot].FinishHits, Is.EqualTo(-1));
            var skillHits = 0;
            for (var i = 0; i < typing.Events.Count; i++)
            {
                if (typing.Events[i].Kind == GameEventKind.Damage && typing.Events[i].Source == DamageSource.Skill)
                    skillHits++;
            }
            Assert.That(skillHits, Is.EqualTo(1));
            Assert.That(typing.State.Resource, Is.GreaterThanOrEqualTo(support.FinishResource));
        }

        [Test]
        public void LongSwingStretchesMouseDistanceAndBoostsMouseHitsWhileActive()
        {
            var support = Gem("long_swing").Support!;
            var loop = Hero(Attribute("dexterity", 30));
            var slot = Supported(loop, "space_hammer", "long_swing");
            Assert.That(SupportEffects.MouseDistance(loop.State.Skills[slot]), Is.EqualTo(support.MouseDistanceMultiplier).Within(1e-9));

            loop.State.Resource = 100;
            loop.Skills.TryCast(slot);
            loop.Tick();

            Assert.That(loop.State.SupportMouseBonus, Is.EqualTo(support.ActiveMouseDamage).Within(1e-9));
            Assert.That(loop.State.SupportKeyBonus, Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void WhirlpoolCirclesCutTheCooldownAndPowerTheNextCast()
        {
            var support = Gem("whirlpool").Support!;
            var loop = Hero(Attribute("dexterity", 30));
            var slot = Supported(loop, "iron_rim_bash", "whirlpool");
            loop.State.Resource = 100;
            loop.Skills.TryCast(slot);
            var before = loop.State.Skills[slot].CooldownTicks;
            var input = new FakeInputSource();
            var circling = TestWorld.Loop(input, loop.State, loop.Hero);

            input.Circle(support.CirclesForPower);
            circling.Tick();

            var cut = support.CirclesForPower * (int)(support.CircleCooldownSeconds * GameLoop.TicksPerSecond);
            Assert.That(circling.State.Skills[slot].CooldownTicks, Is.EqualTo(System.Math.Max(0, before - 1 - cut)));
            circling.State.Skills[slot].CooldownTicks = 0;
            circling.State.Resource = 100;
            circling.Skills.TryCast(slot);
            Assert.That(circling.State.Skills[slot].CastPower, Is.EqualTo(support.CirclePower).Within(1e-9));
        }

        [Test]
        public void TailwindStretchesTheEffectWithMouseDistanceUpToItsCap()
        {
            var support = Gem("tailwind_support").Support!;
            var loop = Hero(Attribute("dexterity", 30));
            var slot = Supported(loop, "space_hammer", "tailwind_support");
            loop.State.Resource = 100;
            loop.Skills.TryCast(slot);
            var skill = loop.State.Skills[slot];
            var total = skill.ActiveTotalTicks;
            var input = new FakeInputSource();
            var moving = TestWorld.Loop(input, loop.State, loop.Hero);

            input.MoveMouse((float)support.WindPxPerSecond);
            moving.Tick();
            Assert.That(skill.ActiveTicks, Is.EqualTo(total - 1 + GameLoop.TicksPerSecond));

            for (var i = 0; i < 100; i++)
            {
                input.MoveMouse((float)support.WindPxPerSecond);
                moving.Tick();
            }
            Assert.That(skill.WindTicks, Is.EqualTo((int)System.Math.Round(total * support.WindMax)));
        }

        [Test]
        public void TailwindMakesAurasStronger()
        {
            var support = Gem("tailwind_support").Support!;
            var loop = Hero(Attribute("dexterity", 30));
            var slot = Supported(loop, "march", "tailwind_support");
            loop.State.Resource = 100;

            loop.Skills.TryCast(slot);

            Assert.That(loop.State.Skills[slot].AuraPower, Is.EqualTo(support.AuraPowerMultiplier).Within(1e-9));
        }

        [Test]
        public void MinionTrailMakesItsSummonsLiveLongerAndRunFasterWithTheMouse()
        {
            var support = Gem("minion_trail").Support!;
            var plain = Hero(Attribute("dexterity", 30));
            var trail = Hero(Attribute("dexterity", 30));
            var plainSlot = Equip(plain, "call_a_colleague");
            var slot = Supported(trail, "call_a_colleague", "minion_trail");
            plain.State.Resource = 100;
            trail.State.Resource = 100;

            plain.Skills.TryCast(plainSlot);
            trail.Skills.TryCast(slot);

            var plainMinion = System.Array.Find(plain.State.Minions, m => m.Standing);
            var minion = System.Array.Find(trail.State.Minions, m => m.Standing);
            Assert.That(minion.TicksLeft, Is.EqualTo((int)System.Math.Round(plainMinion.TicksLeft * support.MinionLife)));
            Assert.That(minion.MouseBonus, Is.EqualTo(support.MinionMouseSpeed).Within(1e-9));
            Assert.That(plainMinion.MouseBonus, Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void SweepingFlicksAddQueueTargetsToTheSkillHits()
        {
            var support = Gem("sweeping").Support!;
            var loop = Hero(Attribute("dexterity", 30));
            var slot = Supported(loop, "iron_rim_bash", "sweeping");
            var skill = loop.State.Skills[slot];
            skill.FlickTargets = 2;

            SupportEffects.AfterHit(skill, loop.State, BigNumber.FromDouble(100), 50);

            var expected = 100 * support.FlickTargetShare * support.QueueDamageMultiplier;
            Assert.That(loop.State.QueueDamage[0].ToDouble(), Is.EqualTo(expected).Within(1e-9));
            Assert.That(loop.State.QueueDamage[1].ToDouble(), Is.EqualTo(expected).Within(1e-9));
            Assert.That(loop.State.QueueDamage[2].ToDouble(), Is.EqualTo(0));
        }

        [Test]
        public void TargetHunterSendsTheSkillHitToTheNextEnemyStronger()
        {
            var support = Gem("target_hunter").Support!;
            var plain = Hero(Attribute("dexterity", 30));
            var hunter = Hero(Attribute("dexterity", 30));
            var plainSlot = Equip(plain, "iron_rim_bash");
            var slot = Supported(hunter, "iron_rim_bash", "target_hunter");
            var hp = hunter.State.EnemyHp;

            var hit = CastDamage(plain, plainSlot);
            CastDamage(hunter, slot);

            Assert.That(hunter.State.EnemyHp, Is.EqualTo(hp));
            Assert.That(hunter.State.QueueDamage[0].ToDouble(), Is.EqualTo(hit * support.RedirectMultiplier).Within(hit * 1e-6));
        }

        [Test]
        public void CursedLeavesTheEnemyVulnerableForAWhile()
        {
            var support = Gem("cursed").Support!;
            var loop = Hero(Attribute("intelligence", 30));
            var slot = Supported(loop, "iron_rim_bash", "cursed");

            CastDamage(loop, slot);

            Assert.That(loop.State.VulnerabilityTicks, Is.EqualTo((int)(support.VulnerabilitySeconds * GameLoop.TicksPerSecond)));
            Assert.That(loop.Combat.ConditionMultiplier(StatTags.Keyboard), Is.EqualTo(1 + support.VulnerabilityShare).Within(1e-9));
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(100));
            Assert.That(loop.Combat.ConditionMultiplier(StatTags.Keyboard), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void LingeringBlightAddsMoreStacksLongerCursesAndAHigherCap()
        {
            var support = Gem("lingering_blight").Support!;
            var plain = Hero(Attribute("intelligence", 30));
            var blight = Hero(Attribute("intelligence", 30));
            var plainSlot = Equip(plain, "vial_from_belt");
            var slot = Supported(blight, "vial_from_belt", "lingering_blight");
            plain.State.Resource = 100;
            blight.State.Resource = 100;
            plain.Skills.TryCast(plainSlot);
            blight.Skills.TryCast(slot);
            Assert.That(blight.State.PoisonStacks, Is.EqualTo(plain.State.PoisonStacks * support.AfflictionMultiplier).Within(1e-9));

            var plainMax = plain.Skills.Dots.PoisonMax;
            plain.Tick();
            blight.Tick();
            Assert.That(blight.Skills.Dots.PoisonMax, Is.EqualTo(plainMax * support.StackCapMultiplier).Within(1e-9));

            var curser = Hero(Attribute("intelligence", 30));
            var plainCurser = Hero(Attribute("intelligence", 30));
            var curse = Supported(curser, "pin", "lingering_blight");
            var plainCurse = Equip(plainCurser, "pin");
            curser.State.Resource = 100;
            plainCurser.State.Resource = 100;
            curser.Skills.TryCast(curse);
            plainCurser.Skills.TryCast(plainCurse);
            Assert.That(curser.State.Skills[curse].ActiveTotalTicks, Is.EqualTo((int)System.Math.Round(plainCurser.State.Skills[plainCurse].ActiveTotalTicks * support.AfflictionMultiplier)));
        }

        [Test]
        public void PawnbrokerTradesPowerForRicherKills()
        {
            var support = Gem("pawnbroker").Support!;
            var loop = Hero();
            var slot = Supported(loop, "iron_rim_bash", "pawnbroker");
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1));
            var gold = loop.Loot.GoldFor(loop.State.Stage, EnemyType.Normal, null);
            var before = loop.State.Gold;

            CastDamage(loop, slot);

            Assert.That(loop.State.Skills[slot].CastPower, Is.EqualTo(support.Power).Within(1e-9));
            Assert.That((loop.State.Gold - before).ToDouble(), Is.EqualTo((gold * support.KillRewardMultiplier).ToDouble()).Within(1e-6));
        }
    }
}
