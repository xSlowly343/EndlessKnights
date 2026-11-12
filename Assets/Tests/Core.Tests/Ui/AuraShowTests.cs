using System.Collections.Generic;
using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class AuraShowTests
    {
        private static GameData Data => TestWorld.Real;
        private static AuraShowRules Rules => Data.AuraShow;
        private static GemDef Gem(string id) => Data.Gems[id];

        private static IEnumerable<GemDef> Auras => Data.Gems.All.Where(gem => gem.Type == SkillType.Aura);

        private static GameLoop Fighting()
        {
            var loop = TestWorld.Loop(new FakeInputSource(), TestWorld.WithAllSkillsOpen(new GameState { Class = HeroClass.Duelist }));
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            return loop;
        }

        private static void Run(GameLoop loop, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                loop.State.Resource = 100;
                loop.Tick();
                loop.Events.Clear();
            }
        }

        private static string RulesJson(string replace, string with) =>
            TestData.ReadGameDataFile(AuraShowRules.FileName).Replace(replace, with);

        [Test]
        public void EveryAuraHasOneOfTheEightThemesAndEveryThemeIsUsed()
        {
            var used = new HashSet<AuraTheme>();
            foreach (var gem in Auras)
                used.Add(Rules.ThemeOf(gem));

            Assert.That(Auras.Count(), Is.GreaterThanOrEqualTo(80));
            Assert.That(used.Count, Is.EqualTo(AuraThemes.Count));
        }

        [TestCase("blade_on_watch", AuraTheme.Edge)]
        [TestCase("dreamcatcher_aura_4", AuraTheme.Calm)]
        [TestCase("minstrel_aura_6", AuraTheme.Hunt)]
        [TestCase("soulbinder_aura_1", AuraTheme.Kin)]
        public void AThemeNamedInTheDataWinsOverTheRule(string id, AuraTheme theme)
        {
            Assert.That(Rules.ThemeOf(Gem(id)), Is.EqualTo(theme));
        }

        [TestCase("exposure", AuraTheme.Might)]
        [TestCase("march", AuraTheme.Might)]
        [TestCase("golden_fever", AuraTheme.Fortune)]
        [TestCase("whisperer_aura_1", AuraTheme.Hex)]
        [TestCase("duelist_aura_6", AuraTheme.Hunt)]
        [TestCase("golemancer_aura_5", AuraTheme.Kin)]
        public void TheRuleReadsTheFirstStatOtherThanAttackPower(string id, AuraTheme theme)
        {
            Assert.That(Rules.ThemeOf(Gem(id)), Is.EqualTo(theme));
        }

        [Test]
        public void AClassAuraCarriesTheSigilOfItsClassAndACommonAuraTheCommonSigil()
        {
            foreach (var gem in Auras)
            {
                var sigil = Rules.SigilOf(gem);
                var owner = gem.Id.Contains("_aura_") ? gem.Id.Substring(0, gem.Id.IndexOf("_aura_", System.StringComparison.Ordinal)) : null;
                var expected = owner != null && HeroClassNames.ByName.ContainsKey(owner) ? owner : AuraShowRules.CommonSigil;
                Assert.That(sigil, Is.EqualTo(expected), gem.Id);
            }
        }

        [Test]
        public void EveryClassHasAuraSigils()
        {
            var sigils = new HashSet<string>(Auras.Select(Rules.SigilOf));

            foreach (var name in HeroClassNames.ByName.Keys)
                Assert.That(sigils, Does.Contain(name));
        }

        [Test]
        public void AnOverrideForAGemThatIsNotAnAuraDoesNotLoad()
        {
            var json = RulesJson("\"blade_on_watch\": \"edge\"", "\"through_stride\": \"edge\"");

            var error = Assert.Throws<ContentException>(() => AuraShowRules.Load(json, Data.Gems))!;
            Assert.That(error.Message, Does.Contain("through_stride"));
        }

        [Test]
        public void AnUnknownThemeDoesNotLoad()
        {
            var json = RulesJson("\"fallback\": \"might\"", "\"fallback\": \"sparkle\"");

            Assert.Throws<ContentException>(() => AuraShowRules.Load(json, Data.Gems));
        }

        [Test]
        public void AMissingThemeDoesNotLoad()
        {
            var json = RulesJson("    { \"id\": \"kin\", \"stats\": [] }\n", string.Empty).Replace("\"damageVsStunned\"] },", "\"damageVsStunned\"] }");

            Assert.Throws<ContentException>(() => AuraShowRules.Load(json, Data.Gems));
        }

        [Test]
        public void AnAuraLightsOnceAndShowsTheCommonSigil()
        {
            var loop = Fighting();
            loop.Skills.Equip(Gem("march"), role: SkillSlotRole.Aura);
            var show = new AuraShowState(Rules);
            show.Update(loop.State.Skills, loop.PartyBuffs);

            Run(loop, 1);
            show.Update(loop.State.Skills, loop.PartyBuffs);

            Assert.That(show.Count, Is.EqualTo(1));
            Assert.That(show.Mark(0).Lit, Is.True);
            Assert.That(show.Mark(0).Theme, Is.EqualTo(AuraTheme.Might));
            Assert.That(show.Mark(0).Timed, Is.True);
            Assert.That(show.Sigil, Is.EqualTo(AuraShowRules.CommonSigil));
            Assert.That(show.AnyTimed, Is.True);

            show.Update(loop.State.Skills, loop.PartyBuffs);
            Assert.That(show.Mark(0).Lit, Is.False);
        }

        [Test]
        public void ATimedAuraWarnsBeforeItEnds()
        {
            var loop = Fighting();
            loop.Skills.Equip(Gem("march"), role: SkillSlotRole.Aura);
            var show = new AuraShowState(Rules);
            Run(loop, 1);
            show.Update(loop.State.Skills, loop.PartyBuffs);
            Assert.That(show.Mark(0).Warning, Is.False);

            var duration = (int)(Gem("march").Skill!.DurationSeconds * GameLoop.TicksPerSecond);
            Run(loop, duration - (int)(Rules.WarnSeconds * GameLoop.TicksPerSecond));
            show.Update(loop.State.Skills, loop.PartyBuffs);

            Assert.That(show.Mark(0).Warning, Is.True);
            Assert.That(show.Mark(0).SecondsLeft, Is.LessThanOrEqualTo(Rules.WarnSeconds));
        }

        [Test]
        public void AnAuraThatGoesOutFadesFromItsPlace()
        {
            var skills = new List<SkillSlot> { Applied("duelist_aura_2"), Applied("duelist_aura_4") };
            var show = new AuraShowState(Rules);
            show.Update(skills, null);

            skills[0].AuraApplied = false;
            show.Update(skills, null);

            Assert.That(show.Count, Is.EqualTo(1));
            Assert.That(show.FadedCount, Is.EqualTo(1));
            Assert.That(show.Faded(0).Index, Is.EqualTo(0));
            Assert.That(show.Faded(0).CountBefore, Is.EqualTo(2));
            Assert.That(show.Mark(0).Gem, Is.SameAs(Gem("duelist_aura_4")));
            Assert.That(show.Mark(0).Lit, Is.False);
        }

        [Test]
        public void TheFirstUpdateAfterARestartLightsNothing()
        {
            var skills = new List<SkillSlot> { Applied("duelist_aura_2") };
            var show = new AuraShowState(Rules);
            show.Update(skills, null);
            show.Restart();

            skills.Add(Applied("duelist_aura_4"));
            show.Update(skills, null);

            Assert.That(show.Count, Is.EqualTo(2));
            Assert.That(show.Mark(1).Lit, Is.False);
        }

        [Test]
        public void AClassAuraGivesItsClassSigilEvenAfterACommonOne()
        {
            var skills = new List<SkillSlot> { Applied("exposure"), Applied("clockwright_aura_1") };
            var show = new AuraShowState(Rules);

            show.Update(skills, null);

            Assert.That(show.Sigil, Is.EqualTo("clockwright"));
            Assert.That(show.SigilColor, Is.EqualTo(Gem("clockwright_aura_1").Color));
        }

        [Test]
        public void ASkillThatIsNotAnAuraIsNotShown()
        {
            var skills = new List<SkillSlot> { Applied("through_stride") };
            var show = new AuraShowState(Rules);

            show.Update(skills, null);

            Assert.That(show.Count, Is.EqualTo(0));
            Assert.That(show.HasSigil, Is.False);
        }

        [Test]
        public void AnAllyWithAnAuraGetsAnArcThatIsFreshOnce()
        {
            var loop = Fighting();
            var show = new AuraShowState(Rules);
            show.Update(loop.State.Skills, loop.PartyBuffs);

            GiveAlly(loop.PartyBuffs, 1, "clockwright_aura_1", 1);
            show.Update(loop.State.Skills, loop.PartyBuffs);

            Assert.That(show.Arc(1).Present, Is.True);
            Assert.That(show.Arc(1).Fresh, Is.True);
            Assert.That(show.Arc(1).Color, Is.EqualTo(Gem("clockwright_aura_1").Color));
            Assert.That(show.Arc(0).Present, Is.False);

            show.Update(loop.State.Skills, loop.PartyBuffs);
            Assert.That(show.Arc(1).Fresh, Is.False);
        }

        [Test]
        public void AnAllyWhoseAurasAreAllCoveredHasAFadedArc()
        {
            var loop = Fighting();
            loop.State.Skills.Add(Applied("clockwright_aura_1", strength: 5));
            GiveAlly(loop.PartyBuffs, 0, "clockwright_aura_1", 1);
            var show = new AuraShowState(Rules);

            show.Update(loop.State.Skills, loop.PartyBuffs);

            Assert.That(show.Arc(0).Present, Is.True);
            Assert.That(show.Arc(0).Faded, Is.True);
        }

        [Test]
        public void UpdatingTheShowAllocatesNothing()
        {
            var loop = Fighting();
            loop.Skills.Equip(Gem("march"), role: SkillSlotRole.Aura);
            GiveAlly(loop.PartyBuffs, 2, "clockwright_aura_1", 1);
            Run(loop, 1);
            var show = new AuraShowState(Rules);
            show.Update(loop.State.Skills, loop.PartyBuffs);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++)
                show.Update(loop.State.Skills, loop.PartyBuffs);

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        [Test]
        public void ThePartyListHasOwnAndAllyAurasWithTheirStatus()
        {
            var loop = Fighting();
            loop.State.Skills.Add(Applied("clockwright_aura_1", strength: 5));
            loop.State.Skills.Add(Applied("overheat"));
            GiveAlly(loop.PartyBuffs, 0, "clockwright_aura_1", 1);
            GiveAlly(loop.PartyBuffs, 1, "duelist_aura_2", 2);
            GiveAlly(loop.PartyBuffs, 2, "duelist_aura_2", 1);
            var rows = new List<PartyAuraRow>();

            PartyAuraList.Fill(loop.State.Skills, loop.PartyBuffs, Rules, rows);

            Assert.That(rows.Select(row => (row.Giver, row.Gem.Id, row.Status)), Is.EqualTo(new[]
            {
                (PartyAuraList.Hero, "clockwright_aura_1", PartyAuraStatus.Working),
                (PartyAuraList.Hero, "overheat", PartyAuraStatus.HeroOnly),
                (0, "clockwright_aura_1", PartyAuraStatus.OwnStronger),
                (1, "duelist_aura_2", PartyAuraStatus.Working),
                (2, "duelist_aura_2", PartyAuraStatus.AllyStronger)
            }));
            Assert.That(rows[4].CoveredBy, Is.EqualTo(1));
            Assert.That(PartyAuraList.Working(rows), Is.EqualTo(3));
            Assert.That(PartyAuraList.Summary(rows), Is.EqualTo("Действуют 3 из 5"));
            Assert.That(PartyAuraList.Status(rows[4], slot => "Игрок " + slot), Is.EqualTo("перекрыта: у Игрок 1 сильнее"));
            Assert.That(rows[3].Effect, Is.Not.Empty);
            Assert.That(rows[3].Reserve, Is.EqualTo(((AuraEffect)Gem("duelist_aura_2").Skill!.Effect).Reserve));
            Assert.That(PartyAuraList.Kind(rows[3]), Is.EqualTo("резерв 15"));
        }

        [Test]
        public void AnEmptyListSaysSo()
        {
            var rows = new List<PartyAuraRow>();

            PartyAuraList.Fill(new List<SkillSlot>(), null, Rules, rows);

            Assert.That(rows, Is.Empty);
            Assert.That(PartyAuraList.Summary(rows), Is.EqualTo("Ауры не включены"));
        }

        [Test]
        public void EveryThemeHasAName()
        {
            for (var theme = 0; theme < AuraThemes.Count; theme++)
                Assert.That(PartyAuraList.ThemeName((AuraTheme)theme), Does.Not.StartWith("aura.theme."));
        }

        private static SkillSlot Applied(string id, double strength = 1) =>
            new SkillSlot(Gem(id)) { Role = SkillSlotRole.Aura, AuraApplied = true, AuraStrength = strength };

        private static void GiveAlly(PartyBuffs buffs, int slot, string gem, double strength)
        {
            var ally = buffs.Ally(slot);
            ally.Present = true;
            var aura = ally.Auras.First(each => !each.Active);
            aura.Gem = Gem(gem);
            aura.Strength = strength;
            aura.TicksLeft = 100;
            buffs.MarkChanged();
            buffs.Tick();
        }
    }
}
