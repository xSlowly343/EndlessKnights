using System.Collections.Generic;
using System.Linq;
using Game.Core.Build;
using Game.Core.Content.Gems;
using Game.Core.Party.Wire;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class SkillWindowModelsTests
    {
        [SetUp]
        public void SetUp() => Texts.UseLanguage("ru");

        private static GemDef Gem(string id) => TestWorld.Data.Gems[id];

        private static GameLoop Duelist(int heroLevel)
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Duelist, HeroLevel = heroLevel });
            while (loop.State.Skills.Count > 0)
                loop.Book.Unequip(0);
            return loop;
        }

        [Test]
        public void TheBarHasNinePlacesInTheOrderOfTheStrip()
        {
            var loop = Duelist(12);
            loop.Skills.Equip(Gem("duelist_hit_1"), role: SkillSlotRole.Right);
            loop.Skills.Equip(Gem("duelist_aura_2"), role: SkillSlotRole.Aura);
            var places = new SkillBarPlace[SkillBarLayout.Places];

            SkillBarLayout.Fill(loop.State.Skills, loop.Book, places);

            Assert.That(places.Select(p => p.Role), Is.EqualTo(new[] { SkillSlotRole.Left, SkillSlotRole.Right, SkillSlotRole.Auto, SkillSlotRole.Auto, SkillSlotRole.Aura, SkillSlotRole.Aura, SkillSlotRole.Aura, SkillSlotRole.Aura, SkillSlotRole.Aura }));
            Assert.That(places[0].Empty, Is.True);
            Assert.That(loop.State.Skills[places[1].Row].Skill, Is.SameAs(Gem("duelist_hit_1")));
            Assert.That(loop.State.Skills[places[4].Row].Skill, Is.SameAs(Gem("duelist_aura_2")));
            Assert.That(places[2].Open, Is.True);
            Assert.That(SkillBarTexts.Locked(places[3]), Is.EqualTo("этап 100"));
            Assert.That(SkillBarTexts.Locked(places[5]), Is.EqualTo("ур. 25"));
            Assert.That(SkillBarTexts.Locked(places[8]), Is.EqualTo("от вещи"));
            Assert.That(SkillBarTexts.Key(places[0].Role) + SkillBarTexts.Key(places[1].Role), Is.EqualTo("ЛП"));
            Assert.That(places[4].CastsByItself, Is.True);
        }

        [Test]
        public void ABookSkillGoesIntoTheChosenSlotAndAnActiveNeverIntoAnAuraSlot()
        {
            var loop = Duelist(12);
            loop.State.OwnedGems.AddRange(new[] { Gem("duelist_aura_2"), Gem("duelist_hit_1") });

            Assert.That(BookPlacing.PutInto(loop.Book, Gem("duelist_aura_2"), SkillSlotRole.Aura), Is.EqualTo(BookResult.Done));
            Assert.That(BookPlacing.PutInto(loop.Book, Gem("duelist_hit_1"), SkillSlotRole.Aura), Is.EqualTo(BookResult.WrongSlot));
            Assert.That(BookPlacing.PutInto(loop.Book, Gem("duelist_hit_1"), SkillSlotRole.Auto), Is.EqualTo(BookResult.Done));
            Assert.That(BookPlacing.PutInto(loop.Book, Gem("duelist_aura_2"), SkillSlotRole.Left), Is.EqualTo(BookResult.Done));

            Assert.That(loop.State.Skills.Single(s => s.Skill == Gem("duelist_hit_1")).Role, Is.EqualTo(SkillSlotRole.Auto));
            Assert.That(loop.State.Skills.Single(s => s.Skill == Gem("duelist_aura_2")).Role, Is.EqualTo(SkillSlotRole.Left));
        }

        [Test]
        public void TheSkillWindowShowsTiersAndTakesABranch()
        {
            var loop = Duelist(30);
            var row = loop.Skills.Equip(Gem("duelist_hit_1"), role: SkillSlotRole.Left);
            loop.State.ProgressOf(Gem("duelist_hit_1")).Level = 5;
            var window = new SkillWindowModel(loop);

            window.Show(row);

            Assert.That(window.TierCount, Is.EqualTo(3));
            Assert.That(window.Tier(0).Open, Is.True);
            Assert.That(window.Tier(1).Open, Is.False);
            Assert.That(window.Tier(0).HasChoice, Is.False);
            Assert.That(window.ExperienceToNext, Is.GreaterThan(0));
            Assert.That(window.Choose(1, 0), Is.EqualTo(BranchResult.LevelTooLow));
            Assert.That(BranchTexts.Result(BranchResult.LevelTooLow), Is.Not.Empty.And.Not.EqualTo("branch.result.levelTooLow"));
            Assert.That(window.Choose(0, 1), Is.EqualTo(BranchResult.Done));
            Assert.That(window.Tier(0).Chosen, Is.EqualTo(1));
            Assert.That(BranchTexts.Cost(window.Tier(0).ChangeCost, default), Is.EqualTo("бесплатно"));
            Assert.That(BranchTexts.Summary(new SkillBranch("x", "x", "x", 1.3, 0.75, 1, 1, null)), Is.EqualTo("урон ×1.3 · откат ×0.75"));
        }

        [Test]
        public void TheSkillWindowOffersOnlySupportsThatFitAndMarksTheBusyOnes()
        {
            var loop = Duelist(5);
            var first = loop.Skills.Equip(Gem("duelist_hit_1"), role: SkillSlotRole.Left);
            var second = loop.Skills.Equip(Gem("duelist_strong_1"), role: SkillSlotRole.Right);
            var window = new SkillWindowModel(loop);
            window.Show(second);
            Assert.That(loop.Book.SetSupport(first, 0, Gem("rebound")), Is.EqualTo(BookResult.Done));

            window.Refresh();

            Assert.That(window.Supports.All(s => s.Gem.CanSupport(Gem("duelist_strong_1"))), Is.True);
            Assert.That(window.Supports.Single(s => s.Gem == Gem("rebound")).Offer, Is.EqualTo(BookResult.Done), "one support may sit on two skills");
            Assert.That(window.Supports.Single(s => s.Gem == Gem("executioner")).Offer, Is.EqualTo(BookResult.LevelTooLow));
            Assert.That(window.Supports.Single(s => s.Gem == Gem("chain")).Available, Is.True);
            Assert.That(window.MaxSockets, Is.EqualTo(5));
        }

        [Test]
        public void TreeTabsShowTheClassTreeCommonSlotsAndThePath()
        {
            var loop = Duelist(1);
            var tabs = new List<TreeTab>();

            TreeTabs.Fill(loop, tabs);

            Assert.That(tabs.Select(t => t.Kind), Is.EqualTo(new[] { TreeTabKind.Class, TreeTabKind.Common, TreeTabKind.Common, TreeTabKind.Path }));
            Assert.That(tabs[1].Open, Is.False);
            Assert.That(TreeTabs.Locked(tabs[1]), Is.EqualTo("с 20-го уровня"));
            Assert.That(TreeTabs.Locked(tabs[2]), Is.EqualTo("с 30-го уровня"));
            Assert.That(TreeTabs.Title(tabs[0]), Is.EqualTo("Дерево класса"));
        }

        [Test]
        public void AnAllysLoadoutReadsBackIntoNamedPlaces()
        {
            var loadout = new PartyLoadoutMessage();
            loadout.Set(0, new LoadoutSkill { GemNetId = Gem("duelist_hit_1").NetId, Level = 7, Combo = (byte)SkillBranches.Index(1, -1, -1) });
            loadout.Set(5, new LoadoutSkill { GemNetId = Gem("duelist_aura_2").NetId, Level = 3, Combo = (byte)SkillBranches.Plain });
            var places = new AllySkillPlace[SkillLoadout.MaxSlots];

            AllyLoadoutView.Fill(loadout, TestWorld.Data.Gems, places);

            Assert.That(places[0].Skill, Is.SameAs(Gem("duelist_hit_1")));
            Assert.That(places[0].Level, Is.EqualTo(7));
            Assert.That(places[5].Role, Is.EqualTo(SkillSlotRole.Aura));
            Assert.That(places[1].Empty, Is.True);
            var secondOption = BranchTexts.Name(Gem("duelist_hit_1").Branches[0].Options[1]);
            Assert.That(AllyLoadoutView.Branches(places[0]), Is.EqualTo(secondOption + " · — · —"));
        }
    }
}
