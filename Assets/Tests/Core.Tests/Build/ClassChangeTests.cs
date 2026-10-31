using System.Collections.Generic;
using System.Linq;
using Game.Core.Build;
using Game.Core.Clock;
using Game.Core.Content.Gems;
using Game.Core.Content.Items;
using Game.Core.Content.Tree;
using Game.Core.Content.Weapons;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class ClassChangeTests
    {
        private static readonly ClockStamp Clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

        private static WeaponCatalog Weapons => TestWorld.Data.Weapons;

        private static GameLoop Viking()
        {
            var state = new GameState { Class = HeroClass.Viking, HeroLevel = 30 };
            var loop = new GameLoop(TestWorld.Data, new FakeInputSource(), state, new StatSheet());
            loop.Book.GrantStartingGems();
            loop.Levels.SyncTreePoints();
            return loop;
        }

        private static List<GemDef> ClassGems(WeaponType type) =>
            TestWorld.Data.Gems.All.Where(gem => gem.Skill?.Weapon == type.Id && gem.Ascendancy == null).ToList();

        private static void SpendTreePoints(GameLoop loop, int count)
        {
            TestWorld.RaiseClassTree(loop, count);
        }

        private static string Snapshot(GameLoop loop)
        {
            var state = loop.State;
            var weapons = new[] { state.Weapons.Right, state.Weapons.Left }.Concat(state.Weapons.Bag)
                .Select(weapon => weapon == null ? "-" : $"{weapon.Id}/{weapon.Rarity}/{string.Join(",", weapon.Skills)}/{weapon.Unique?.Id}");
            var gems = state.OwnedGems.Select(gem => gem.Id).OrderBy(id => id);
            var levels = state.GemProgress.Where(pair => pair.Value.Level > 1).OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value.Level}");
            var slots = state.Skills.Select(slot => $"{slot.Skill.Id}+{slot.Supports[0]?.Id}");
            return string.Join(" | ", string.Join(";", weapons), string.Join(";", gems), string.Join(";", levels), string.Join(";", slots));
        }

        [Test]
        public void EveryClassGemHasItsOwnPairInEveryOtherClassAndComesBackTheSameWay()
        {
            var table = Weapons.Reforge;
            foreach (var from in Weapons.Types)
            {
                foreach (var to in Weapons.Types.Where(type => type != from))
                {
                    var targets = new HashSet<GemDef>();
                    foreach (var gem in ClassGems(from))
                    {
                        var pair = table.Gem(gem, from, to);
                        Assert.That(pair, Is.Not.Null, $"{gem.Id}: {from.Id} -> {to.Id}");
                        Assert.That(pair!.Skill!.Weapon, Is.EqualTo(to.Id));
                        Assert.That(targets.Add(pair), Is.True, $"{pair.Id} is taken twice from {from.Id}");
                        Assert.That(table.Gem(pair, to, from), Is.SameAs(gem), $"{gem.Id} does not come back from {to.Id}");
                    }
                }
            }
        }

        [Test]
        public void ReforgedGemsKeepTheirTierAndMostlyTheirRole()
        {
            var table = Weapons.Reforge;
            var axe = Weapons.TypeOf(HeroClass.Viking);
            foreach (var to in Weapons.Types.Where(type => type != axe))
            {
                var gems = ClassGems(axe).Where(gem => !gem.Locked).ToList();
                var sameTier = gems.Count(gem => table.Gem(gem, axe, to)!.Skill!.Tier == gem.Skill!.Tier);
                var sameRole = gems.Count(gem => table.Gem(gem, axe, to)!.Skill!.Role == gem.Skill!.Role);
                Assert.That(sameTier, Is.GreaterThanOrEqualTo(gems.Count - 3), to.Id);
                Assert.That(sameRole, Is.GreaterThanOrEqualTo(gems.Count - 5), to.Id);
            }
            var bow = Weapons.TypeOf(HeroClass.Amazon);
            Assert.That(table.Gem(TestWorld.Data.Gems["iron_rim_bash"], bow, axe), Is.Null, "only gems of the class being left are reforged");
            Assert.That(table.Gem(TestWorld.Data.Gems["lull"], axe, bow), Is.Null, "general gems stay as they are");
        }

        [Test]
        public void TheFirstChangeIsFreeAndTheNextOnesArePaid()
        {
            var loop = Viking();

            Assert.That(loop.ClassChangeIsFree, Is.True);
            Assert.That(loop.ChangeClassFree(HeroClass.Viking), Is.EqualTo(ClassChangeResult.SameClass));
            Assert.That(loop.ChangeClassFree(HeroClass.Ronin), Is.EqualTo(ClassChangeResult.Changed));
            Assert.That(loop.State.Class, Is.EqualTo(HeroClass.Ronin));
            Assert.That(loop.ClassChangeIsFree, Is.False);
            Assert.That(loop.ChangeClassFree(HeroClass.Viking), Is.EqualTo(ClassChangeResult.NotFree));
            Assert.That(loop.ChangeClassPaid(HeroClass.Viking), Is.EqualTo(ClassChangeResult.Changed));
            Assert.That(loop.State.ClassChanges, Is.EqualTo(2));
            Assert.That(TestWorld.Data.Ascendancy.CrystalChangeCost, Is.EqualTo(100));
        }

        [Test]
        public void ANoviceChoosesAClassInsteadOfChangingIt()
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState { ClassChosen = false });

            Assert.That(loop.ChangeClassFree(HeroClass.Ronin), Is.EqualTo(ClassChangeResult.NoClassYet));
            Assert.That(loop.State.ClassChanges, Is.Zero);
        }

        [Test]
        public void TheTreeAndThePathAreResetAndAllTheirPointsMoveToTheNewClass()
        {
            var loop = Viking();
            SpendTreePoints(loop, 5);
            var earned = loop.State.TreePointsEarned;
            Assert.That(loop.CompleteActivity("garden"), Is.True);
            Assert.That(loop.ChooseSubclass("berserker"), Is.EqualTo(SubclassResult.Chosen));
            var pathNode = loop.State.Ascendancy.Allocatable(TestWorld.Data.Ascendancy).First();
            Assert.That(loop.AllocateSubclassNode(pathNode.Id), Is.EqualTo(AllocationResult.Allocated));
            var pathPoints = loop.State.Ascendancy.PointsEarned(TestWorld.Data.Ascendancy);

            Assert.That(loop.ChangeClassFree(HeroClass.Ronin), Is.EqualTo(ClassChangeResult.Changed));

            var state = loop.State;
            Assert.That(state.ClassName, Is.EqualTo("ronin"));
            Assert.That(state.TreePointsSpent, Is.Zero);
            Assert.That(state.TreePointsLeft, Is.EqualTo(earned));
            Assert.That(state.ClassTree.TreeId, Is.EqualTo("ronin"));
            Assert.That(state.OtherPreset.ClassTree.TreeId, Is.EqualTo("ronin"));
            Assert.That(state.Ascendancy.Subclass, Is.Null);
            Assert.That(state.Ascendancy.PointsLeft(TestWorld.Data.Ascendancy), Is.EqualTo(pathPoints));
            Assert.That(state.OwnedGems.Any(gem => gem.Ascendancy != null), Is.False, "the skill of the old path goes away with it");
            Assert.That(loop.ChooseSubclass("ninja"), Is.EqualTo(SubclassResult.Chosen), "a path of the new class can be chosen at once");
            Assert.That(loop.Resource.Rules, Is.SameAs(TestWorld.Data.Combat.Resources.For("ronin")));
            SpendTreePoints(loop, earned);
            Assert.That(state.TreePointsLeft, Is.Zero);
        }

        [Test]
        public void WeaponsAndGemsAreReforgedAndChangingBackLosesNothing()
        {
            var loop = Viking();
            var state = loop.State;
            var axe = Weapons.TypeOf(HeroClass.Viking);
            var katana = Weapons.TypeOf(HeroClass.Ronin);
            var jarl = Weapons.Bases.Last(weaponBase => weaponBase.Type == axe);
            var rolled = loop.Weapons.Give(Weapons.Roll(jarl, ItemRarity.Mythic, new Rng(7)));
            loop.Weapons.Equip(WeaponHand.Left, rolled);
            var bow = loop.Weapons.Give(Weapons.Roll(Weapons.StarterFor(HeroClass.Amazon), ItemRarity.Rare, new Rng(3)));
            var corsair = loop.Weapons.Give(Weapons.RollUnique(Weapons.Uniques.First(unique => unique.Id == "corsair_axe"), new Rng(5)));
            var fast = state.Skills[0].Skill;
            state.ProgressOf(fast).Level = 9;
            var support = TestWorld.Data.Gems.All.First(gem => gem.Kind == GemKind.Support);
            state.Skills[0].Supports[0] = support;
            var before = Snapshot(loop);

            Assert.That(loop.ChangeClassFree(HeroClass.Ronin), Is.EqualTo(ClassChangeResult.Changed));

            Assert.That(state.Weapons.Right!.Type, Is.SameAs(katana));
            Assert.That(state.Weapons.Right.Base.FromBiome, Is.EqualTo(1));
            var left = state.Weapons.Left!;
            Assert.That(left.Type, Is.SameAs(katana));
            Assert.That(left.Base, Is.SameAs(Weapons.Bases.Last(weaponBase => weaponBase.Type == katana)), "the same step by biome");
            Assert.That(left.Rarity, Is.EqualTo(ItemRarity.Mythic));
            Assert.That(left.Skills.Count, Is.EqualTo(rolled.Skills.Count));
            for (var i = 0; i < left.Skills.Count; i++)
                Assert.That(TestWorld.Data.Gems[left.Skills[i]].Skill!.Weapon, Is.EqualTo("katana"));
            Assert.That(state.Weapons.Bag, Does.Contain(bow), "a weapon of a third class stays as it is");
            Assert.That(state.Weapons.Bag, Does.Contain(corsair), "a unique weapon stays as it is");

            var reforgedFast = state.Skills[0].Skill;
            Assert.That(reforgedFast.Skill!.Weapon, Is.EqualTo("katana"));
            Assert.That(reforgedFast.Skill.Tier, Is.EqualTo(fast.Skill!.Tier));
            Assert.That(state.LevelOf(reforgedFast), Is.EqualTo(9), "the level moves to the new gem");
            Assert.That(state.Skills[0].Supports[0], Is.SameAs(support));
            Assert.That(state.Skills.All(slot => slot.Skill.Skill!.Weapon != "axe"), Is.True);
            Assert.That(state.OwnedGems.Any(gem => gem.Skill?.Weapon == "axe"), Is.False);
            Assert.That(state.OwnedGems.Select(gem => gem.Id), Does.Contain("lull"));

            Assert.That(loop.ChangeClassPaid(HeroClass.Viking), Is.EqualTo(ClassChangeResult.Changed));

            Assert.That(Snapshot(loop), Is.EqualTo(before));
        }

        [Test]
        public void TheSaveRemembersHowManyTimesTheClassWasChanged()
        {
            var loop = Viking();
            loop.ChangeClassFree(HeroClass.Captain);

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, Clock), TestWorld.Data).State;

            Assert.That(loaded.ClassChanges, Is.EqualTo(1));
            Assert.That(loaded.Class, Is.EqualTo(HeroClass.Captain));
            Assert.That(new GameState().ClassChanges, Is.Zero);
        }
    }
}
