using System.Collections.Generic;
using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Tree;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class WhispererTests
    {
        private static WhispererRules Rules => TestWorld.Data.Combat.Classes.Whisperer;

        private static Modifier Flat(StatId stat, double value) => new Modifier(stat, ModifierKind.Flat, value);

        private static (GameLoop Loop, FakeInputSource Input) Hero(params Modifier[] modifiers) => Hero(HeroClass.Whisperer, EnemyType.Normal, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Boss(params Modifier[] modifiers) => Hero(HeroClass.Whisperer, EnemyType.Boss, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, EnemyType enemy, params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, TestWorld.WithAllWeaponPlacesOpen(new GameState { Class = heroClass }), hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            Spawn(loop, enemy);
            loop.Tick();
            loop.Events.Clear();
            return (loop, input);
        }

        private static void Spawn(GameLoop loop, EnemyType type, EnemyDef? enemy = null)
        {
            loop.State.SpawnEnemy(enemy, type, BigNumber.Create(1, 50));
            loop.State.BossTicksLeft = type == EnemyType.Normal ? 0 : 100_000;
        }

        private static void Party(GameLoop loop, int members)
        {
            loop.State.SharedEnemy = true;
            for (var slot = 1; slot < members; slot++)
                loop.PartyBuffs.Ally(slot).Present = true;
            loop.State.PartyMembers = members;
        }

        private static List<double> Damage(GameLoop loop)
        {
            var damage = new List<double>();
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage && loop.Events[i].Source != DamageSource.Mouse)
                    damage.Add(loop.Events[i].Amount.ToDouble());
            }
            return damage;
        }

        private static List<double> Hit(GameLoop loop, FakeInputSource input)
        {
            loop.Events.Clear();
            input.PressKeys(1);
            loop.Tick();
            var damage = Damage(loop);
            loop.Tick();
            return damage;
        }

        private static void Hits(GameLoop loop, FakeInputSource input, int count)
        {
            for (var i = 0; i < count; i++)
                Hit(loop, input);
        }

        private static void Wait(GameLoop loop, double seconds)
        {
            for (var i = 0; i < System.Math.Round(seconds * GameLoop.TicksPerSecond); i++)
                loop.Tick();
        }

        private static double Expected(GameLoop loop, double multiplier) =>
            loop.Combat.SkillDamage(loop.Combat.BaseKeyDamage, multiplier * loop.State.FocusMultiplier, StatTags.Keyboard).ToDouble();

        private static List<double> Cast(GameLoop loop, string skill)
        {
            loop.Events.Clear();
            loop.Combat.Whisperer.OnSkillCast(TestWorld.Data.Gems[skill]);
            return Damage(loop);
        }

        private static bool HasRelay(GameLoop loop, TraitRelayKind kind, out TraitRelay found)
        {
            for (var i = 0; i < loop.State.TraitRelays.Count; i++)
            {
                if (loop.State.TraitRelays[i].Kind != kind)
                    continue;
                found = loop.State.TraitRelays[i];
                return true;
            }
            found = default;
            return false;
        }

        private static bool HasEffect(GameLoop loop, int id, out TraitEnemyEffect found)
        {
            for (var i = 0; i < loop.State.TraitEnemyEffects.Count; i++)
            {
                if (loop.State.TraitEnemyEffects[i].Id != id)
                    continue;
                found = loop.State.TraitEnemyEffects[i];
                return true;
            }
            found = default;
            return false;
        }

        [Test]
        public void EveryTwentyFiveHitsTieAKnotInOrder()
        {
            Assert.That(Rules.KnotHits, Is.EqualTo(25));
            var (loop, input) = Hero(Flat(WhispererStats.KnotPower, 0.1));
            Hits(loop, input, 25);
            Assert.That(loop.State.Whisperer.Knots, Is.EqualTo(1));
            Assert.That(loop.Combat.Whisperer.DamageTaken(), Is.EqualTo(1.1).Within(1e-9));

            Hits(loop, input, 25);
            Assert.That(loop.State.Whisperer.Knots, Is.EqualTo(2));
            Assert.That(loop.Combat.Whisperer.DamageTaken(), Is.EqualTo(1.1).Within(1e-9));
            Assert.That(loop.State.TraitTimerSlow, Is.EqualTo(Rules.KnotSlow).Within(1e-9));

            Hits(loop, input, 25);
            Assert.That(loop.Combat.Whisperer.DamageTaken(), Is.EqualTo(1.2).Within(1e-9));
        }

        [Test]
        public void TheSeventhKnotDoublesTheFirstSix()
        {
            var (loop, _) = Hero(Flat(WhispererStats.KnotPower, 0.1));
            Cast(loop, "whisperer_strong_3");
            loop.Tick();

            Assert.That(loop.State.Whisperer.Knots, Is.EqualTo(WhispererState.MaxKnots));
            Assert.That(loop.Combat.Whisperer.DamageTaken(), Is.EqualTo(1 + 0.1 * 3 * Rules.KnotSeventh).Within(1e-9));
            Assert.That(loop.State.TraitPartyCritMultiplier, Is.EqualTo(Rules.KnotCritMultiplier * Rules.KnotSeventh).Within(1e-9));
            Assert.That(loop.State.TraitPartyGold, Is.EqualTo(Rules.KnotGold * Rules.KnotSeventh).Within(1e-9));
            Assert.That(loop.State.TraitTimerSlow, Is.EqualTo(Rules.KnotSlow * Rules.KnotSeventh).Within(1e-9));
        }

        [Test]
        public void AlliesHitsBringTheNextKnotCloser()
        {
            var (loop, _) = Hero(Flat(WhispererStats.KnotPower, 0.1));
            loop.Combat.Whisperer.AddAllyHits(Rules.KnotHits);
            Assert.That(loop.State.Whisperer.Knots, Is.EqualTo(1));
        }

        [Test]
        public void KnotsDropOnANewEnemyUnlessTheThreadRemembers()
        {
            var (loop, _) = Hero(Flat(WhispererStats.KnotPower, 0.1));
            Cast(loop, "whisperer_path_1");
            Spawn(loop, EnemyType.Normal);
            loop.Tick();
            Assert.That(loop.State.Whisperer.Knots, Is.Zero);

            var (carry, _) = Hero(Flat(WhispererStats.KnotPower, 0.1), Flat(WhispererStats.KnotCarry, 1));
            Cast(carry, "whisperer_path_1");
            Spawn(carry, EnemyType.Normal);
            carry.Tick();
            Assert.That(carry.State.Whisperer.Knots, Is.EqualTo(2));
        }

        [Test]
        public void TripleDoubleBlastAndColossusKnotsAreRules()
        {
            var (triple, _) = Hero(Flat(WhispererStats.KnotPower, 0.1), Flat(WhispererStats.KnotTriple, 1));
            Cast(triple, "whisperer_strong_3");
            Assert.That(triple.Combat.Whisperer.DamageTaken(), Is.EqualTo(1 + 0.1 * 3 * Rules.KnotTriple).Within(1e-9));

            var (pair, pairInput) = Hero(Flat(WhispererStats.KnotPower, 0.1), Flat(WhispererStats.KnotDouble, 1));
            Hits(pair, pairInput, Rules.KnotHits);
            Assert.That(pair.State.Whisperer.Knots, Is.EqualTo(2));

            var (blast, _) = Hero(Flat(WhispererStats.KnotPower, 0.1), Flat(WhispererStats.KnotBlast, 1));
            Assert.That(Cast(blast, "whisperer_path_1"), Has.Count.EqualTo(1));

            var (boss, bossInput) = Boss(Flat(WhispererStats.KnotPower, 0.1), Flat(WhispererStats.KnotBoss, 1));
            Hits(boss, bossInput, Rules.KnotHits / 2);
            Assert.That(boss.State.Whisperer.Knots, Is.EqualTo(1));
        }

        [Test]
        public void InAPartyTheKnotsHangOnTheHostEnemy()
        {
            var (loop, _) = Hero(Flat(WhispererStats.KnotPower, 0.1));
            Party(loop, 2);
            Cast(loop, "whisperer_path_1");
            loop.Tick();

            Assert.That(HasEffect(loop, WhispererMechanics.KnotEffect, out var effect), Is.True);
            Assert.That(effect.Strength, Is.EqualTo(0.1).Within(1e-9));
            Assert.That(effect.TimerSlow, Is.EqualTo(Rules.KnotSlow).Within(1e-9));
            Assert.That(loop.Combat.Whisperer.DamageTaken(), Is.EqualTo(1));
            Assert.That(loop.State.TraitTimerSlow, Is.Zero);
        }

        [Test]
        public void TheNameComesAfterAHundredAndFiftyBossHits()
        {
            Assert.That(Rules.NameHits, Is.EqualTo(150));
            var (loop, input) = Boss(Flat(WhispererStats.NamePower, 0.4));
            Cast(loop, "whisperer_path_2");
            Hits(loop, input, 99);
            Assert.That(loop.State.Whisperer.Named, Is.False);
            Assert.That(loop.Combat.Whisperer.DamageTaken(), Is.EqualTo(1));

            Hit(loop, input);
            Assert.That(loop.State.Whisperer.Named, Is.True);
            Assert.That(loop.Combat.Whisperer.DamageTaken(), Is.EqualTo(1.4).Within(1e-9));
        }

        [Test]
        public void TheNameStaysSilentOnOrdinaryEnemies()
        {
            var (loop, input) = Hero(Flat(WhispererStats.NamePower, 0.4));
            Cast(loop, "whisperer_path_2");
            Hits(loop, input, 150);
            Assert.That(loop.State.Whisperer.Named, Is.False);
            Assert.That(loop.Combat.Whisperer.NameBonus, Is.Zero);
        }

        [Test]
        public void InAPartyTheNameIsCalledAndEveryAllysSyllableSpeaksIt()
        {
            var (loop, input) = Hero(Flat(WhispererStats.NamePower, 0.4));
            Party(loop, 3);
            Spawn(loop, EnemyType.Boss);
            loop.Tick();
            Assert.That(HasRelay(loop, TraitRelayKind.NameCall, out var call), Is.True);
            Assert.That(call.To, Is.EqualTo(TraitRelay.AllSlots));
            Assert.That(call.Every, Is.EqualTo(Rules.SyllableHits));

            loop.State.TraitRelays.Clear();
            Hits(loop, input, Rules.SyllableHits);
            loop.Combat.Whisperer.ReceiveSyllable(1);
            Assert.That(loop.State.Whisperer.Named, Is.False);
            loop.Combat.Whisperer.ReceiveSyllable(2);
            Assert.That(loop.State.Whisperer.Named, Is.True);
            Assert.That(loop.Combat.Whisperer.NameBonus, Is.EqualTo(0.4 + Rules.Syllable * 3).Within(1e-9));
            loop.Tick();
            Assert.That(HasEffect(loop, WhispererMechanics.NameEffect, out var effect), Is.True);
            Assert.That(effect.Strength, Is.EqualTo(0.4 + Rules.Syllable * 3).Within(1e-9));
        }

        [Test]
        public void AnAllyWritesItsSyllableAndSendsItBack()
        {
            var (loop, input) = Hero(HeroClass.Runedancer, EnemyType.Boss);
            loop.State.Boons.CallName(2, 5, 100);
            Hits(loop, input, 5);
            Assert.That(loop.State.Boons.SyllableGiven, Is.True);
            Assert.That(HasRelay(loop, TraitRelayKind.Syllable, out var syllable), Is.True);
            Assert.That(syllable.To, Is.EqualTo(2));
        }

        [Test]
        public void MemoryTimeAndLoudNameAreRules()
        {
            var bossDef = TestWorld.Data.Enemies.All.First(enemy => enemy.Type == EnemyType.Boss);
            var (memory, memoryInput) = Boss(Flat(WhispererStats.NamePower, 0.4), Flat(WhispererStats.NameMemory, 1));
            Spawn(memory, EnemyType.Boss, bossDef);
            memory.Tick();
            Cast(memory, "whisperer_path_2");
            Cast(memory, "whisperer_path_2");
            Cast(memory, "whisperer_path_2");
            Assert.That(memory.State.Whisperer.Named, Is.True);
            Spawn(memory, EnemyType.Boss, bossDef);
            memory.Tick();
            Assert.That(memory.State.Whisperer.Named, Is.True);

            var (time, _) = Boss(Flat(WhispererStats.NamePower, 0.4), Flat(WhispererStats.NameTime, 1));
            var left = time.State.BossTicksLeft;
            Cast(time, "whisperer_path_2");
            Cast(time, "whisperer_path_2");
            Cast(time, "whisperer_path_2");
            Assert.That(time.State.BossTicksLeft - left, Is.EqualTo((int)(Rules.NameTimeSeconds * GameLoop.TicksPerSecond)));

            var (loud, _) = Boss(Flat(WhispererStats.NamePower, 0.4), Flat(WhispererStats.NameBlast, 1));
            Cast(loud, "whisperer_path_2");
            Cast(loud, "whisperer_path_2");
            Assert.That(Cast(loud, "whisperer_path_2"), Has.Count.EqualTo(1));
        }

        [Test]
        public void TheVowSilencesHerHitsAndTriplesHerCurses()
        {
            Assert.That(Rules.VowCurse, Is.EqualTo(3));
            var (loop, input) = Hero(Flat(WhispererStats.VowPower, 3));
            Assert.That(Hit(loop, input), Is.Empty);

            Cast(loop, "whisperer_queue_1");
            Assert.That(loop.State.Whisperer.Curse, Is.EqualTo(0.08 * 3).Within(1e-9));
            Assert.That(loop.Combat.Whisperer.DamageTaken(), Is.EqualTo(1.24).Within(1e-9));
        }

        [Test]
        public void EveryTenSilentHitsAWhisperCursesTheNextHit()
        {
            var (loop, input) = Hero(Flat(WhispererStats.VowPower, 3));
            Hits(loop, input, Rules.HushHits);
            Assert.That(loop.State.Boons.Hushes, Is.EqualTo(1));
            Assert.That(loop.State.Boons.HushOwner, Is.EqualTo(-1));

            var expected = Expected(loop, 3);
            var damage = Hit(loop, input);
            Assert.That(damage, Has.Count.EqualTo(1));
            Assert.That(damage[0] / expected, Is.EqualTo(1).Within(0.1));
            Assert.That(loop.State.Boons.Hushes, Is.Zero);
        }

        [Test]
        public void InAPartyTheWhisperGoesToAnAllyAndItsHitsExtendTheCurse()
        {
            var (loop, input) = Hero(Flat(WhispererStats.VowPower, 3));
            Party(loop, 2);
            Cast(loop, "whisperer_queue_1");
            Hits(loop, input, Rules.HushHits - 1);
            loop.State.TraitRelays.Clear();
            Hit(loop, input);
            Assert.That(HasRelay(loop, TraitRelayKind.Hush, out var hush), Is.True);
            Assert.That(hush.To, Is.EqualTo(LanternbearerMechanics.AnyAlly));
            Assert.That(loop.State.Boons.Hushes, Is.Zero);

            var ticks = loop.State.Whisperer.CurseTicks;
            loop.Combat.Whisperer.ReceiveHushHits(2);
            Assert.That(loop.State.Whisperer.CurseTicks - ticks, Is.EqualTo((int)(2 * Rules.HushExtendSeconds * GameLoop.TicksPerSecond)));
        }

        [Test]
        public void AnAllyWithAWhisperHitsHarderAndReportsBack()
        {
            var (loop, input) = Hero(HeroClass.Runedancer, EnemyType.Normal);
            loop.State.Boons.Hush(1, 4, 1, 3);
            Assert.That(Hit(loop, input), Has.Count.EqualTo(2));
            Assert.That(HasRelay(loop, TraitRelayKind.HushHit, out var back), Is.True);
            Assert.That(back.To, Is.EqualTo(1));
            Assert.That(back.Count, Is.EqualTo(1));
        }

        [Test]
        public void CursesDoNotFadeInAPause()
        {
            var (loop, input) = Hero(Flat(WhispererStats.VowPower, 3));
            Cast(loop, "whisperer_queue_1");
            Wait(loop, 30);
            Assert.That(loop.State.Whisperer.CurseTicks, Is.GreaterThan(0));
            Assert.That(loop.Combat.Whisperer.CurseShare, Is.GreaterThan(0));
        }

        [Test]
        public void BrokenGrowingAndWavingVowsAreRules()
        {
            var (broken, brokenInput) = Hero(Flat(WhispererStats.VowPower, 3), Flat(WhispererStats.VowBreak, 1));
            Wait(broken, Rules.VowBreakEverySeconds);
            Assert.That(broken.Combat.Whisperer.StrikeMultiplier(), Is.EqualTo(1));
            Assert.That(Hit(broken, brokenInput), Is.Not.Empty);

            var (grow, growInput) = Hero(Flat(WhispererStats.VowPower, 3), Flat(WhispererStats.VowGrow, 1));
            Hits(grow, growInput, 10);
            Assert.That(grow.State.Whisperer.Grow, Is.EqualTo(10 * Rules.VowGrowPerHit).Within(1e-9));

            var (wave, waveInput) = Hero(Flat(WhispererStats.VowPower, 3), Flat(WhispererStats.HushAll, 1));
            Hits(wave, waveInput, Rules.HushHits);
            Assert.That(wave.State.Boons.Hushes, Is.EqualTo(2));
        }

        [Test]
        public void SharedSkillsWorkForEveryTrait()
        {
            var (loop, _) = Boss();
            var pending = loop.State.PendingResource;
            Cast(loop, "whisperer_hit_3");
            Assert.That(loop.State.PendingResource - pending, Is.EqualTo(15).Within(1e-9));

            Cast(loop, "whisperer_strong_1");
            Assert.That(loop.State.BossTimerStopTicks, Is.EqualTo(5 * GameLoop.TicksPerSecond));

            Cast(loop, "whisperer_auto_1");
            var ticks = loop.State.Whisperer.CurseTicks;
            Cast(loop, "whisperer_auto_3");
            Assert.That(loop.State.Whisperer.CurseTicks - ticks, Is.EqualTo(4 * GameLoop.TicksPerSecond));
            Assert.That(Cast(loop, "whisperer_hit_2"), Has.Count.EqualTo(1));
        }

        [Test]
        public void OtherCharactersIgnoreWhispererStats()
        {
            var (loop, input) = Hero(HeroClass.Runedancer, EnemyType.Boss, Flat(WhispererStats.KnotPower, 0.1), Flat(WhispererStats.NamePower, 0.4), Flat(WhispererStats.VowPower, 3));
            var hits = Hit(loop, input);
            Hits(loop, input, 170);

            Assert.That(hits, Is.Not.Empty);
            Assert.That(loop.State.Whisperer.Knots, Is.Zero);
            Assert.That(loop.State.Whisperer.Named, Is.False);
            Assert.That(loop.State.Boons.Hushes, Is.Zero);
            Assert.That(loop.Combat.Whisperer.DamageTaken(), Is.EqualTo(1));
        }

        [Test]
        public void EveryForkOfWhispererIsARuleWithAText()
        {
            foreach (var path in TestWorld.Real.Ascendancy.SubclassesOf("whisperer"))
            {
                Assert.That(path.Trait!.PartyTextRu, Is.Not.Empty, path.Id);
                Assert.That(path.Trait.PartyTextEn, Is.Not.Empty, path.Id);
                foreach (var option in path.Trait.Forks.SelectMany(fork => fork.Choices))
                {
                    Assert.That(option.TextRu, Is.Not.Empty, option.Id);
                    Assert.That(option.TextEn, Is.Not.Empty, option.Id);
                    Assert.That(option.Modifiers.Any(modifier => modifier.Stat.Name != CombatStats.Damage.Name), Is.True, option.Id);
                }
            }
        }

        [Test]
        public void TheWhispererTreeHasTwentySixSmallFourteenMediumSixLegendaryAndFourCoreNodes()
        {
            var tree = TestWorld.Real.ClassTrees["whisperer"];
            var count = tree.Nodes.GroupBy(node => node.Kind).ToDictionary(group => group.Key, group => group.Count());

            Assert.That(count[TreeNodeKind.Small], Is.EqualTo(26));
            Assert.That(count[TreeNodeKind.Medium], Is.EqualTo(14));
            Assert.That(count[TreeNodeKind.Legendary], Is.EqualTo(6));
            Assert.That(count[TreeNodeKind.Core], Is.EqualTo(4));
            Assert.That(tree.Nodes.Count(node => node.Template.First.Modifiers.Concat(node.Template.Second.Modifiers).Concat(node.Template.Third.Modifiers)
                .Any(modifier => modifier.Stat.Name.StartsWith("party") || modifier.When == ModifierCondition.InParty
                    || modifier.Stat.Name == WhispererStats.KnotParty.Name || modifier.Stat.Name == WhispererStats.NameParty.Name
                    || modifier.Stat.Name == WhispererStats.HushParty.Name)), Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void TheWhispererMechanicsDoNotAllocate()
        {
            var probeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (System.GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var (knots, knotsInput) = Hero(Flat(WhispererStats.KnotPower, 0.1), Flat(WhispererStats.KnotBlast, 1));
            var (name, nameInput) = Boss(Flat(WhispererStats.NamePower, 0.4), Flat(WhispererStats.NameHits, -100));
            var (vow, vowInput) = Hero(Flat(WhispererStats.VowPower, 3), Flat(WhispererStats.VowGrow, 1));
            void Run(GameLoop loop, FakeInputSource input, int ticks)
            {
                for (var tick = 0; tick < ticks; tick++)
                {
                    input.PressKeys(1);
                    loop.Tick();
                    loop.Events.Clear();
                }
            }
            Run(knots, knotsInput, 600);
            Run(name, nameInput, 600);
            Run(vow, vowInput, 600);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            Run(knots, knotsInput, 300);
            Run(name, nameInput, 300);
            Run(vow, vowInput, 300);

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
