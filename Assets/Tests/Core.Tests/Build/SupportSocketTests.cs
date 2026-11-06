using System.Linq;
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
            var loop = TestWorld.Loop(new FakeInputSource(), TestWorld.WithAllSkillsOpen(new GameState { Class = HeroClass.Runedancer }), hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.OwnedGems.Clear();
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            return loop;
        }

        private static int Equip(GameLoop loop, string skill, params string[] owned)
        {
            var gem = Gem(skill);
            loop.State.OwnedGems.Add(gem);
            foreach (var id in owned)
                loop.State.OwnedGems.Add(Gem(id));
            if (SkillBook.IsClassSkill(gem) && !loop.Book.OfHeroClass(gem))
                loop.Skills.Equip(gem, role: loop.Book.FreeRoleFor(gem));
            else
                Assert.That(loop.Book.Equip(gem), Is.EqualTo(BookResult.Done));
            return loop.State.Skills.Count - 1;
        }

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
            var slot = Equip(loop, "runedancer_hit_1", "rebound");

            Assert.That(loop.Book.SetSupport(slot, 1, Gem("rebound")), Is.EqualTo(BookResult.NoSuchSocket));
            Assert.That(loop.Book.SetSupport(slot, 0, Gem("rebound")), Is.EqualTo(BookResult.Done));
            Assert.That(loop.State.Skills[slot].Supports[0], Is.SameAs(Gem("rebound")));
        }

        [Test]
        public void OneSupportFitsTwoSkillsButOnlyOnceOnEach()
        {
            var loop = Hero();
            loop.State.RecordStage = 300;
            var first = Equip(loop, "runedancer_hit_1", "chain");
            var second = Equip(loop, "runedancer_queue_1");

            Assert.That(loop.Book.SetSupport(first, 0, Gem("chain")), Is.EqualTo(BookResult.Done));
            Assert.That(loop.Book.SetSupport(second, 0, Gem("chain")), Is.EqualTo(BookResult.Done));
            Assert.That(loop.Book.SetSupport(first, 1, Gem("chain")), Is.EqualTo(BookResult.AlreadyOnSkill));
            Assert.That(loop.Book.SetSupport(first, 0, Gem("chain")), Is.EqualTo(BookResult.Done));
        }

        [Test]
        public void SocketedSupportIsNoLongerAFreeCopy()
        {
            var loop = Hero();
            var slot = Equip(loop, "runedancer_hit_1", "rebound");

            loop.Book.SetSupport(slot, 0, Gem("rebound"));
            Assert.That(loop.Book.FreeCopies(Gem("rebound")), Is.EqualTo(0));

            loop.Book.SetSupport(slot, 0, null);
            Assert.That(loop.Book.FreeCopies(Gem("rebound")), Is.EqualTo(1));
        }

        [Test]
        public void ASupportOpensWithTheHeroLevel()
        {
            var loop = Hero();
            var slot = Equip(loop, "runedancer_hit_1");
            loop.State.HeroLevel = Gem("rebound").UnlockLevel - 1;

            Assert.That(loop.Book.SetSupport(slot, 0, Gem("rebound")), Is.EqualTo(BookResult.LevelTooLow));
            Assert.That(loop.Book.SetSupport(slot, 0, Gem("march")), Is.EqualTo(BookResult.WrongKind));
            loop.State.HeroLevel = Gem("rebound").UnlockLevel;
            Assert.That(loop.Book.SetSupport(slot, 0, Gem("rebound")), Is.EqualTo(BookResult.Done), "no gem to own, the level opens it");
        }

        [Test]
        public void TheOpenSupportsFollowTheHeroLevel()
        {
            var loop = Hero();
            var open = new System.Collections.Generic.List<GemDef>();
            loop.State.HeroLevel = 1;
            loop.Book.OpenSupports(TestWorld.Data.Gems.All, open);
            Assert.That(open, Is.Empty);

            loop.State.HeroLevel = 100;
            loop.Book.OpenSupports(TestWorld.Data.Gems.All, open);
            Assert.That(open.Count, Is.EqualTo(31));
            Assert.That(open.Where(gem => gem.ForSkill != null), Is.Empty);
        }

        private static GemCatalog WithSupport(string supports, double damage, double mana) =>
            GemCatalog.Load(TestData.ReadGameDataFile(GemCatalog.FileName).Replace("\"gems\": [",
                "\"gems\": [\n    { \"id\": \"plain_help\", \"kind\": \"support\", \"fits\": [" + supports + "], \"damageMult\": " + damage.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                ", \"manaMult\": " + mana.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", \"level\": 1, \"color\": \"white\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"effect\": { \"type\": \"lines\", \"lines\": [{ \"effect\": \"experience\", \"value\": 0 }] } },"));

        [Test]
        public void ASupportFitsOnlyTheSkillTypesItNames()
        {
            var loop = Hero();
            var slot = Equip(loop, "runedancer_hit_1");
            var dots = WithSupport("\"dot\"", 1, 1)["plain_help"];
            var keys = WithSupport("\"strike\"", 1, 1)["plain_help"];

            Assert.That(loop.Book.SetSupport(slot, 0, dots), Is.EqualTo(BookResult.DoesNotFit));
            Assert.That(loop.Book.SetSupport(slot, 0, keys), Is.EqualTo(BookResult.Done));
        }

        [Test]
        public void TheSkillOffersOnlyTheSupportsThatFitIt()
        {
            var loop = Hero();
            loop.State.HeroLevel = 100;
            var slot = Equip(loop, "runedancer_hit_1");
            var catalog = new System.Collections.Generic.List<GemDef>(TestWorld.Data.Gems.All)
            {
                WithSupport("\"dot\"", 1, 1)["plain_help"],
            };
            var offered = new System.Collections.Generic.List<GemDef>();

            loop.Book.SupportsFor(slot, catalog, offered);

            Assert.That(offered, Is.Not.Empty);
            Assert.That(offered.Exists(g => g.Id == "plain_help"), Is.False);
            foreach (var support in offered)
                Assert.That(loop.Book.SetSupport(slot, 0, support), Is.Not.EqualTo(BookResult.DoesNotFit).And.Not.EqualTo(BookResult.LevelTooLow), support.Id);
            loop.Book.SupportsFor(-1, catalog, offered);
            Assert.That(offered, Is.Empty);
        }

        [Test]
        public void ASupportMultipliesDamageAndMana()
        {
            var plain = Hero();
            var helped = Hero();
            var plainSlot = Equip(plain, "runedancer_hit_1");
            var slot = Equip(helped, "runedancer_hit_1");
            Assert.That(helped.Book.SetSupport(slot, 0, WithSupport("\"strike\"", 1.5, 2)["plain_help"]), Is.EqualTo(BookResult.Done));

            Assert.That(CastDamage(helped, slot) / CastDamage(plain, plainSlot), Is.EqualTo(1.5).Within(1e-6));
            plain.State.Resource = 100;
            helped.State.Resource = 100;
            plain.State.Skills[plainSlot].CooldownTicks = 0;
            helped.State.Skills[slot].CooldownTicks = 0;
            plain.Skills.TryCast(plainSlot);
            helped.Skills.TryCast(slot);
            Assert.That(100 - helped.State.Resource, Is.EqualTo(2 * (100 - plain.State.Resource)).Within(1e-9));
        }

        [Test]
        public void ColoredSupportsNeedNoAttribute()
        {
            var loop = Hero();
            var slot = Equip(loop, "runedancer_hit_1");

            Assert.That(loop.Book.SetSupport(slot, 0, Gem("chain")), Is.EqualTo(BookResult.Done));
        }

        [Test]
        public void SupportWithoutItsMechanicWaits()
        {
            var catalog = GemCatalog.Load(TestData.ReadGameDataFile(GemCatalog.FileName).Replace("\"gems\": [",
                "\"gems\": [\n    { \"id\": \"echo_keys\", \"kind\": \"support\", \"fits\": [\"strike\"], \"damageMult\": 1, \"manaMult\": 1, \"level\": 1, \"color\": \"white\", \"name\": { \"ru\": \"Эхо\", \"en\": \"Echo\" }, \"effect\": { \"type\": \"repeatEveryKeys\" } },"));
            var loop = Hero();
            var slot = Equip(loop, "runedancer_hit_1");
            loop.State.OwnedGems.Add(catalog["echo_keys"]);

            Assert.That(loop.Book.SetSupport(slot, 0, catalog["echo_keys"]), Is.EqualTo(BookResult.WaitsForMechanic));
        }

        [Test]
        public void ExecutionerDoublesItsSkillOnWoundedEnemies()
        {
            var plain = Hero();
            var supported = Hero();
            var plainSlot = Equip(plain, "runedancer_hit_1");
            var slot = Equip(supported, "runedancer_hit_1", "executioner");
            supported.Book.SetSupport(slot, 0, Gem("executioner"));
            foreach (var loop in new[] { plain, supported })
                loop.State.EnemyHp = loop.State.EnemyMaxHp * 0.2;

            Assert.That(CastDamage(supported, slot) / CastDamage(plain, plainSlot), Is.EqualTo(2).Within(1e-6));
        }

        [Test]
        public void ExecutionerDoesNothingOnHealthyEnemies()
        {
            var plain = Hero();
            var supported = Hero();
            var plainSlot = Equip(plain, "runedancer_hit_1");
            var slot = Equip(supported, "runedancer_hit_1", "executioner");
            supported.Book.SetSupport(slot, 0, Gem("executioner"));

            Assert.That(CastDamage(supported, slot) / CastDamage(plain, plainSlot), Is.EqualTo(1).Within(1e-6));
        }

        [Test]
        public void ChainSendsPartOfTheSkillHitToTheNextEnemy()
        {
            var loop = Hero();
            var slot = Equip(loop, "runedancer_hit_1", "chain");
            loop.Book.SetSupport(slot, 0, Gem("chain"));

            var hit = CastDamage(loop, slot);

            var share = Gem("chain").Support!.QueueShare;
            Assert.That(loop.State.QueueDamage[0].ToDouble(), Is.EqualTo(hit * share).Within(hit * 1e-6));
        }

        [Test]
        public void SupportDoesNotChangeKeyHits()
        {
            var loop = Hero();
            var slot = Equip(loop, "runedancer_hit_1", "chain");
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
            var plainSlot = Equip(plain, "runedancer_hit_1");
            var slot = Equip(supported, "runedancer_hit_1", "rebound");
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
            var slot = Equip(loop, "runedancer_hit_1", "rebound");
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
            var slot = Equip(loop, "runedancer_hit_1", "rebound");
            loop.Book.SetSupport(slot, 0, Gem("rebound"));

            loop.SwitchPreset();
            Assert.That(loop.State.Skills, Is.Empty);
            loop.SwitchPreset();

            Assert.That(loop.State.Skills[0].Supports[0], Is.SameAs(Gem("rebound")));
        }
            [Test]
        public void ApprenticeTradesPowerForExperience()
        {
            var loop = Hero(new Modifier(CombatStats.Damage, ModifierKind.More, 1e60, StatTags.Strike));
            var slot = Equip(loop, "runedancer_hit_1", "apprentice");
            loop.Book.SetSupport(slot, 0, Gem("apprentice"));
            loop.State.Resource = 100;
            loop.Skills.TryCast(slot);
            Assert.That(loop.State.Skills[slot].CastPower, Is.EqualTo(0.9).Within(1e-9));

            var input = new FakeInputSource();
            var typing = TestWorld.Loop(input, loop.State, loop.Hero);
            typing.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(10));
            input.PressKeys(1);
            typing.Tick();

            Assert.That(typing.State.GemProgress["runedancer_hit_1"].Experience, Is.EqualTo((TestWorld.Data.Gems.Levels.ExperiencePerKill + TestWorld.Data.Gems.Levels.ExperiencePerCast) * 1.5).Within(1e-9), "the cast before the tick also grows the skill");
        }

        [Test]
        public void BossHunterIsStrongOnBossesAndWeakerOnTheRest()
        {
            var plain = Hero();
            var hunter = Hero();
            var plainSlot = Equip(plain, "runedancer_hit_1");
            var slot = Equip(hunter, "runedancer_hit_1", "boss_hunter");
            hunter.Book.SetSupport(slot, 0, Gem("boss_hunter"));
            var support = Gem("boss_hunter").Support!;

            Assert.That(support.Kind, Is.EqualTo(SupportKind.Lines));
            Assert.That(CastDamage(hunter, slot) / CastDamage(plain, plainSlot), Is.EqualTo(0.95).Within(1e-6));

            foreach (var loop in new[] { plain, hunter })
                loop.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
            Assert.That(CastDamage(hunter, slot) / CastDamage(plain, plainSlot), Is.EqualTo(1.6).Within(1e-6));
        }

        [Test]
        public void DeathMarkHitsMarkedEnemiesHarderAndKillsGiveResource()
        {
            var loop = Hero();
            var slot = Equip(loop, "runedancer_hit_1", "death_mark");
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
            var loop = Hero();
            var slot = Equip(loop, "runedancer_hit_1", support);
            Assert.That(loop.Book.SetSupport(slot, 0, Gem(support)), Is.EqualTo(BookResult.Done));
            setup(loop);
            loop.State.Resource = 100;
            loop.Skills.TryCast(slot);
            return loop.State.Skills[slot].CastPower;
        }

        [Test]
        public void QuietTimeDoublesTheCastAfterALongPause()
        {
            var pause = (long)(30 * GameLoop.TicksPerSecond);

            Assert.That(CastPowerWith("quiet_time", loop => loop.State.IdleTicksBeforeInput = pause), Is.EqualTo(2).Within(1e-9));
            Assert.That(CastPowerWith("quiet_time", loop => loop.State.IdleTicksBeforeInput = pause - 1), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void BladeHungerGrowsWithKeyHitsSinceTheLastCastUpToItsCap()
        {
            Assert.That(CastPowerWith("blade_hunger", loop => loop.State.StrikeHits += 40), Is.EqualTo(1.1).Within(1e-9));
            Assert.That(CastPowerWith("blade_hunger", loop => loop.State.StrikeHits += 1_000_000), Is.EqualTo(1.4).Within(1e-9));
        }

        [Test]
        public void BladeHungerStartsAgainAfterEveryCast()
        {
            var loop = Hero();
            var slot = Equip(loop, "runedancer_hit_1", "blade_hunger");
            loop.Book.SetSupport(slot, 0, Gem("blade_hunger"));
            loop.State.StrikeHits += 1000;
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
            Assert.That(CastPowerWith("acceleration", loop => loop.State.FocusMultiplier = 2), Is.EqualTo(1.16).Within(1e-9));
            Assert.That(CastPowerWith("acceleration", loop => loop.State.FocusMultiplier = 100), Is.EqualTo(1.56).Within(1e-9));
        }

        [Test]
        public void CritFocusLetsTheSkillCritFromFocusAndHitHarder()
        {
            var plain = Hero();
            var focused = Hero();
            var plainSlot = Equip(plain, "runedancer_hit_1");
            var slot = Equip(focused, "runedancer_hit_1", "crit_focus");
            focused.Book.SetSupport(slot, 0, Gem("crit_focus"));
            focused.State.FocusMultiplier = 2;
            Assert.That(SupportEffects.CritChance(focused.State.Skills[slot], focused.State), Is.EqualTo(0.06).Within(1e-9));

            plain.State.FocusMultiplier = 1000;
            focused.State.FocusMultiplier = 1000;
            var ratio = CastDamage(focused, slot) / CastDamage(plain, plainSlot);

            Assert.That(ratio, Is.EqualTo(focused.Combat.CritMultiplier(StatTags.Skill) * 1.2).Within(1e-6));
        }

        [Test]
        public void WideReachHitsTheEnemyAfterTheNext()
        {
            var loop = Hero();
            var slot = Equip(loop, "runedancer_hit_1", "wide_reach");
            loop.Book.SetSupport(slot, 0, Gem("wide_reach"));
            var support = Gem("wide_reach").Support!;

            var hit = CastDamage(loop, slot);

            Assert.That(loop.State.QueueDamage[support.QueueSlot].ToDouble(), Is.EqualTo(hit * support.QueueShare).Within(hit * 1e-6));
        }

        [Test]
        public void DeferredPaymentCostsLessAndDrainsOverTime()
        {
            var loop = Hero();
            var gem = Gem("runedancer_hit_1");
            var slot = Equip(loop, "runedancer_hit_1", "deferred_payment");
            loop.Book.SetSupport(slot, 0, Gem("deferred_payment"));
            var support = Gem("deferred_payment").Support!;
            var cost = gem.Skill!.ResourceCost * support.CostMultiplier * Gem("deferred_payment").ManaMultiplier;
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
            var slot = Equip(loop, "runedancer_hit_1", "deferred_payment");
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
            var slot = Supported(loop, "runedancer_hit_1", "double_charge");
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
            var plainSlot = Equip(plain, "runedancer_hit_1");
            var slot = Supported(twins, "runedancer_hit_1", "twins");
            var support = Gem("twins").Support!;
            var first = CastDamage(twins, slot);
            Assert.That(first, Is.EqualTo(CastDamage(plain, plainSlot) * Gem("twins").DamageMultiplier).Within(first * 1e-9));

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
        public void InkPoisonPoisonsTheEnemyOnTheFirstSkillHits()
        {
            var loop = Hero();
            var slot = Supported(loop, "runedancer_hit_1", "ink_poison");
            var support = Gem("ink_poison").Support!;

            for (var i = 0; i < support.PoisonHitsMax + 2; i++)
                CastDamage(loop, slot);

            Assert.That(loop.State.PoisonStacks, Is.EqualTo(support.PoisonPerHit * support.PoisonHitsMax).Within(1e-9));
        }

        [Test]
        public void SeriesRhythmCutsTheCooldownEveryTwentyFiveHitsAndRewardsLongSeries()
        {
            var support = Gem("series_rhythm").Support!;
            var loop = Hero();
            var slot = Supported(loop, "runedancer_hit_1", "series_rhythm");
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
            var loop = Hero();
            var slot = Supported(loop, "runedancer_hit_1", "series_finish");
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
        public void TailwindStretchesTheEffectWithMouseDistanceUpToItsCap()
        {
            var support = Gem("tailwind_support").Support!;
            var loop = Hero();
            var slot = Supported(loop, "hurricane", "tailwind_support");
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
            var loop = Hero();
            var slot = Supported(loop, "march", "tailwind_support");
            loop.State.Resource = 100;

            loop.Skills.TryCast(slot);

            Assert.That(loop.State.Skills[slot].AuraPower, Is.EqualTo(support.AuraPowerMultiplier).Within(1e-9));
        }

        [Test]
        public void TargetHunterSendsTheSkillHitToTheNextEnemyStronger()
        {
            var support = Gem("target_hunter").Support!;
            var plain = Hero();
            var hunter = Hero();
            var plainSlot = Equip(plain, "runedancer_hit_1");
            var slot = Supported(hunter, "runedancer_hit_1", "target_hunter");
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
            var loop = Hero();
            var slot = Supported(loop, "runedancer_hit_1", "cursed");

            CastDamage(loop, slot);

            Assert.That(loop.State.VulnerabilityTicks, Is.EqualTo((int)(support.VulnerabilitySeconds * GameLoop.TicksPerSecond)));
            Assert.That(loop.Combat.ConditionMultiplier(StatTags.Strike), Is.EqualTo(1 + support.VulnerabilityShare).Within(1e-9));
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(100));
            Assert.That(loop.Combat.ConditionMultiplier(StatTags.Strike), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void LingeringBlightAddsMoreStacksLongerCursesAndAHigherCap()
        {
            var support = Gem("lingering_blight").Support!;
            var plain = Hero();
            var blight = Hero();
            var plainSlot = Equip(plain, "alchemist_hit_1");
            var slot = Supported(blight, "alchemist_hit_1", "lingering_blight");
            plain.State.Resource = 100;
            blight.State.Resource = 100;
            plain.Skills.TryCast(plainSlot);
            blight.Skills.TryCast(slot);
            Assert.That(blight.State.PoisonStacks, Is.EqualTo(plain.State.PoisonStacks * support.AfflictionMultiplier).Within(1e-9));

            var plainMax = plain.Skills.Dots.PoisonMax;
            plain.Tick();
            blight.Tick();
            Assert.That(blight.Skills.Dots.PoisonMax, Is.EqualTo(plainMax * support.StackCapMultiplier).Within(1e-9));

            var curser = Hero();
            var plainCurser = Hero();
            var curse = Supported(curser, "whisperer_hit_2", "lingering_blight");
            var plainCurse = Equip(plainCurser, "whisperer_hit_2");
            curser.State.Resource = 100;
            plainCurser.State.Resource = 100;
            curser.Skills.TryCast(curse);
            plainCurser.Skills.TryCast(plainCurse);
            Assert.That(curser.State.Skills[curse].ActiveTotalTicks, Is.EqualTo((int)System.Math.Round(plainCurser.State.Skills[plainCurse].ActiveTotalTicks * support.AfflictionMultiplier)));
        }

        [Test]
        public void PawnbrokerTradesPowerForRicherKills()
        {
            var loop = Hero();
            var slot = Supported(loop, "runedancer_hit_1", "pawnbroker");
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1));
            var gold = loop.Loot.GoldFor(loop.State.Stage, EnemyType.Normal, null);
            var before = loop.State.Gold;

            CastDamage(loop, slot);

            Assert.That(loop.State.Skills[slot].CastPower, Is.EqualTo(0.7).Within(1e-9));
            Assert.That((loop.State.Gold - before).ToDouble(), Is.EqualTo((gold * 1.4).ToDouble()).Within(1e-6));
        }

        [Test]
        public void KeyEchoRepeatsTheSkillEveryFiftyKeysAtHalfPower()
        {
            var support = Gem("key_echo").Support!;
            var loop = Hero();
            var slot = Supported(loop, "runedancer_hit_1", "key_echo");
            var first = CastDamage(loop, slot);
            var input = new FakeInputSource();
            var typing = TestWorld.Loop(input, loop.State, loop.Hero);
            typing.State.HitsOnEnemy = 10;

            for (var i = 0; i < support.EchoEveryKeys - 1; i++)
            {
                input.PressKeys(1);
                typing.Tick();
                typing.Tick();
            }
            typing.Events.Clear();
            input.PressKeys(1);
            typing.Tick();

            var repeat = 0.0;
            for (var i = 0; i < typing.Events.Count; i++)
            {
                if (typing.Events[i].Kind == GameEventKind.Damage && typing.Events[i].Source == DamageSource.Skill)
                    repeat += typing.Events[i].Amount.ToDouble();
            }
            Assert.That(repeat, Is.EqualTo(first * support.EchoPower).Within(first * 1e-6));
        }

        [Test]
        public void LegacyIsStrongerOnlyInAParty()
        {
            Assert.That(CastPowerWith("legacy", l => l.State.PartyMembers = 1), Is.EqualTo(1).Within(1e-9));
            Assert.That(CastPowerWith("legacy", l => l.State.PartyMembers = 3), Is.EqualTo(1.1).Within(1e-9));
        }

        [Test]
        public void ASupportMakesTheSkillCostMoreByItsManaMultiplier()
        {
            var plain = Hero();
            var chained = Hero();
            var plainSlot = Equip(plain, "runedancer_hit_1");
            var slot = Supported(chained, "runedancer_hit_1", "chain");
            plain.State.Resource = 100;
            chained.State.Resource = 100;

            plain.Skills.TryCast(plainSlot);
            chained.Skills.TryCast(slot);

            Assert.That(Gem("chain").ManaMultiplier, Is.GreaterThan(1));
            Assert.That(100 - chained.State.Resource, Is.EqualTo((100 - plain.State.Resource) * Gem("chain").ManaMultiplier).Within(1e-9));
        }

        [Test]
        public void AnAuraIsOfferedAuraSupportsAndAStrikeIsOfferedStrikeSupports()
        {
            var loop = Hero();
            loop.State.HeroLevel = 100;
            var strike = Equip(loop, "runedancer_hit_1");
            var aura = Equip(loop, "march");
            var offered = new System.Collections.Generic.List<GemDef>();

            loop.Book.SupportsFor(aura, TestWorld.Data.Gems.All, offered);
            Assert.That(offered.Select(gem => gem.Id), Does.Contain("resonance").And.Not.Contain("chain"));

            loop.Book.SupportsFor(strike, TestWorld.Data.Gems.All, offered);
            Assert.That(offered.Select(gem => gem.Id), Does.Contain("chain").And.Not.Contain("resonance").And.Not.Contain("minion_trail"));
        }

        [Test]
        public void ResonanceAndLegacyHangTheirPartyLinesWhileSocketedOncePerKind()
        {
            var loop = Hero();
            var first = Equip(loop, "march", "resonance");
            var second = Equip(loop, "runedancer_aura_1");
            loop.Book.SetSupport(first, 0, Gem("resonance"));
            loop.Book.SetSupport(second, 0, Gem("resonance"));

            loop.Tick();

            Assert.That(loop.Skills.SupportPartyLines, Is.EqualTo(Gem("resonance").Support!.PartyLines));
            Assert.That(Game.Core.Party.CoopLine.TryGet(loop.Skills.SupportPartyLines[0].Stat, out var line), Is.True);
            Assert.That(line.To, Is.EqualTo(Game.Core.Party.CoopTarget.Allies));

            loop.Book.SetSupport(first, 0, null);
            loop.Book.SetSupport(second, 0, null);
            loop.Tick();

            Assert.That(loop.Skills.SupportPartyLines, Is.Empty);
        }

        [Test]
        public void ASupportLineTakesWhenOrUnlessButNotBoth()
        {
            var json = TestData.ReadGameDataFile(GemCatalog.FileName).Replace(
                "{\"effect\": \"power\", \"value\": 0.6, \"when\": {\"signal\": \"boss\"}}",
                "{\"effect\": \"power\", \"value\": 0.6, \"when\": {\"signal\": \"boss\"}, \"unless\": {\"signal\": \"boss\"}}");
            var unknown = TestData.ReadGameDataFile(GemCatalog.FileName).Replace("\"signal\": \"boss\"}}", "\"signal\": \"moon\"}}");

            Assert.That(Assert.Throws<ContentException>(() => GemCatalog.Load(json))!.Message, Does.Contain("not both"));
            Assert.That(Assert.Throws<ContentException>(() => GemCatalog.Load(unknown))!.Message, Does.Contain("moon"));
        }

        [Test]
        public void ResonanceGoesIntoASocketNowThatThePartyHasBuffs()
        {
            var loop = Hero();
            var slot = Equip(loop, "march", "resonance");

            Assert.That(loop.Book.SetSupport(slot, 0, Gem("resonance")), Is.EqualTo(BookResult.Done));
        }
    }
}
