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
    public class MinstrelTests
    {
        private static MinstrelRules Rules => TestWorld.Data.Combat.Classes.Minstrel;

        private static Modifier Flat(StatId stat, double value) => new Modifier(stat, ModifierKind.Flat, value);

        private static (GameLoop Loop, FakeInputSource Input) Hero(params Modifier[] modifiers) => Hero(HeroClass.Minstrel, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            Spawn(loop, EnemyType.Normal);
            loop.Tick();
            loop.Events.Clear();
            return (loop, input);
        }

        private static void Spawn(GameLoop loop, EnemyType type)
        {
            loop.State.SpawnEnemy(null, type, BigNumber.Create(1, 50));
            loop.State.BossTicksLeft = type == EnemyType.Normal ? 0 : 100_000;
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
            loop.Combat.Minstrel.OnSkillCast(TestWorld.Data.Gems[skill]);
            return Damage(loop);
        }

        private static int Equip(GameLoop loop, string skill)
        {
            TestWorld.ArmFor(loop, TestWorld.Data.Gems[skill]);
            loop.Skills.Equip(TestWorld.Data.Gems[skill], role: SkillTestHero.ManualRole(loop.State.Skills.Count));
            return loop.State.Skills.Count - 1;
        }

        [Test]
        public void FourLinesOfTenHitsMakeAVerseThatRingsTwelveSeconds()
        {
            Assert.That(Rules.LineHits, Is.EqualTo(10));
            Assert.That(Rules.LinesPerVerse, Is.EqualTo(4));
            var (loop, input) = Hero(Flat(MinstrelStats.VersePower, 0.1));

            Hits(loop, input, 39);
            Assert.That(loop.State.Minstrel.Lines, Is.EqualTo(3));
            Assert.That(loop.State.Minstrel.Verses, Is.Zero);
            Hit(loop, input);

            Assert.That(loop.State.Minstrel.Verses, Is.EqualTo(1));
            Assert.That(loop.Combat.Minstrel.DamageTaken(DamageSource.Keyboard), Is.EqualTo(1.1).Within(1e-9));
            Assert.That(loop.State.TraitPartyDamage, Is.EqualTo(0.1).Within(1e-9));
        }

        [Test]
        public void InAPauseSheFinishesTheVerseAndFallsSilent()
        {
            var (loop, input) = Hero(Flat(MinstrelStats.VersePower, 0.1));
            Cast(loop, "minstrel_path_1");
            Assert.That(loop.State.Minstrel.Verses, Is.EqualTo(1));

            Wait(loop, Rules.VerseSeconds - 1);
            Assert.That(loop.State.Minstrel.Verses, Is.EqualTo(1));
            Wait(loop, 1.5);

            Assert.That(loop.State.Minstrel.Verses, Is.Zero);
            Assert.That(loop.State.TraitPartyDamage, Is.Zero);
        }

        [Test]
        public void ThreeVersesInARowStartAChorusThatDoublesEveryBuffForTheParty()
        {
            var (loop, input) = Hero(Flat(MinstrelStats.VersePower, 0.1));
            Cast(loop, "minstrel_path_1");
            Cast(loop, "minstrel_path_1");
            Assert.That(loop.Combat.Minstrel.InChorus, Is.False);
            Cast(loop, "minstrel_path_1");
            Cast(loop, "minstrel_strong_2");
            loop.Tick();

            Assert.That(loop.State.Minstrel.Choruses, Is.EqualTo(1));
            Assert.That(loop.Combat.Minstrel.InChorus, Is.True);
            Assert.That(loop.Combat.Minstrel.VerseBonus, Is.EqualTo(3 * 0.1 * 2).Within(1e-9));
            Assert.That(loop.State.TraitPartyDamage, Is.EqualTo(0.6 + 0.3 * 2).Within(1e-9));

            Wait(loop, Rules.ChorusSeconds);
            Assert.That(loop.Combat.Minstrel.InChorus, Is.False);
            Assert.That(loop.State.Minstrel.Verses, Is.EqualTo(3));
        }

        [Test]
        public void TheEncoreRepeatsTheChorusOnceAndTheLoudChorusEndsTheVerses()
        {
            var (encore, _) = Hero(Flat(MinstrelStats.VersePower, 0.1), Flat(MinstrelStats.ChorusEncore, 1), Flat(MinstrelStats.ChorusBoost, -0.3), Flat(MinstrelStats.VerseSeconds, 30));
            for (var i = 0; i < 3; i++)
                Cast(encore, "minstrel_path_1");
            Assert.That(encore.Combat.Minstrel.ChorusMultiplier, Is.EqualTo(1.7).Within(1e-9));
            Wait(encore, Rules.ChorusSeconds + 1);
            Assert.That(encore.Combat.Minstrel.InChorus, Is.True);
            Wait(encore, Rules.ChorusSeconds);
            Assert.That(encore.Combat.Minstrel.InChorus, Is.False);

            var (loud, _) = Hero(Flat(MinstrelStats.VersePower, 0.1), Flat(MinstrelStats.ChorusBoost, 1), Flat(MinstrelStats.ChorusSpend, 1));
            for (var i = 0; i < 3; i++)
                Cast(loud, "minstrel_path_1");
            Assert.That(loud.Combat.Minstrel.VerseBonus, Is.EqualTo(3 * 0.1 * 3).Within(1e-9));
            Wait(loud, Rules.ChorusSeconds + 0.2);
            Assert.That(loud.State.Minstrel.Verses, Is.Zero);
        }

        [Test]
        public void TheChoirGrowsWithAlliesAndTheEndlessSongExtendsTheChorus()
        {
            var (choir, _) = Hero(Flat(MinstrelStats.VersePower, 0.1), Flat(MinstrelStats.ChorusChoir, 1));
            choir.State.PartyMembers = 3;
            for (var i = 0; i < 6; i++)
                Cast(choir, "minstrel_path_1");
            Assert.That(choir.Combat.Minstrel.VerseMax, Is.EqualTo(5));
            Assert.That(choir.State.Minstrel.Verses, Is.EqualTo(5));

            var (endless, _) = Hero(Flat(MinstrelStats.VersePower, 0.1), Flat(MinstrelStats.ChorusEndless, 1));
            for (var i = 0; i < 3; i++)
                Cast(endless, "minstrel_path_1");
            var before = endless.State.Minstrel.ChorusTicks;
            Cast(endless, "minstrel_path_1");
            Assert.That(endless.State.Minstrel.ChorusTicks - before, Is.EqualTo(Rules.EndlessSeconds * GameLoop.TicksPerSecond));
        }

        [Test]
        public void SingAlongGivesResourceToHerAndTheAlliesOnEveryChorus()
        {
            var (loop, _) = Hero(Flat(MinstrelStats.VersePower, 0.1), Flat(MinstrelStats.ChorusGift, 1));
            var pending = loop.State.PendingResource;
            for (var i = 0; i < 3; i++)
                Cast(loop, "minstrel_path_1");

            Assert.That(loop.State.PendingResource - pending, Is.EqualTo(Rules.ChorusGift).Within(1e-9));
            Assert.That(loop.State.TraitPartyResourceGift, Is.EqualTo(Rules.ChorusGift).Within(1e-9));
        }

        [Test]
        public void TheMelodyChangesEveryFortyHitsAndEachMelodyHelpsHerDifferently()
        {
            Assert.That(Rules.SongHits, Is.EqualTo(40));
            var (loop, input) = Hero(Flat(MinstrelStats.SongPower, 0.15));
            Assert.That(loop.State.Minstrel.Melody, Is.EqualTo(MinstrelMechanics.Fanfare));
            Assert.That(loop.Combat.Minstrel.StrikeMultiplier(), Is.EqualTo(1.15).Within(1e-9));
            Assert.That(loop.Combat.Minstrel.DamageTaken(DamageSource.Skill), Is.EqualTo(1).Within(1e-9));

            Hits(loop, input, 40);
            Assert.That(loop.State.Minstrel.Melody, Is.EqualTo(MinstrelMechanics.March));
            Assert.That(loop.Combat.Minstrel.StrikeMultiplier(), Is.EqualTo(1).Within(1e-9));
            var slot = Equip(loop, "minstrel_strong_1");
            loop.State.Skills[slot].CooldownTicks = 200;
            Wait(loop, 2);
            Assert.That(loop.State.Skills[slot].CooldownTicks, Is.EqualTo(200 - 20 - 3).Within(1));

            Cast(loop, "minstrel_path_2");
            Assert.That(loop.State.Minstrel.Melody, Is.EqualTo(MinstrelMechanics.Hymn));
            Assert.That(loop.Combat.Minstrel.DamageTaken(DamageSource.Skill), Is.EqualTo(1.15).Within(1e-9));
            Assert.That(loop.Combat.Minstrel.DamageTaken(DamageSource.Keyboard), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void EveryAllyHearsASongForTheirRoleAndTheAudienceGetsResource()
        {
            var (loop, input) = Hero(Flat(MinstrelStats.SongPower, 0.15), Flat(MinstrelStats.SongGift, 1));
            loop.Tick();

            Assert.That(loop.State.TraitPartyDamage, Is.EqualTo(0.15).Within(1e-9));
            Assert.That(loop.State.TraitPartyCooldown, Is.EqualTo(0.15 * Rules.SongPartyCooldown).Within(1e-9));
            Assert.That(loop.State.TraitPartyCritChance, Is.EqualTo(0.15 * Rules.SongPartyCrit).Within(1e-9));

            var pending = loop.State.PendingResource;
            Cast(loop, "minstrel_path_2");
            Assert.That(loop.State.PendingResource - pending, Is.EqualTo(Rules.SongGift).Within(1e-9));
            Assert.That(loop.State.TraitPartyResourceGift, Is.EqualTo(Rules.SongGift).Within(1e-9));
        }

        [Test]
        public void LouderAndSongForHerselfShiftTheSongBetweenHerAndTheParty()
        {
            var (louder, _) = Hero(Flat(MinstrelStats.SongPower, 0.25), Flat(MinstrelStats.SongSelfPenalty, 0.5));
            louder.Tick();
            Assert.That(louder.Combat.Minstrel.SelfSong, Is.EqualTo(0.125).Within(1e-9));
            Assert.That(louder.State.TraitPartyDamage, Is.EqualTo(0.25).Within(1e-9));

            var (herself, _) = Hero(Flat(MinstrelStats.SongPower, 0.15), Flat(MinstrelStats.SongSolo, 1), Flat(MinstrelStats.SongPartyPenalty, 0.5));
            herself.Tick();
            Assert.That(herself.Combat.Minstrel.SelfSong, Is.EqualTo(0.3).Within(1e-9));
            Assert.That(herself.State.TraitPartyDamage, Is.EqualTo(0.075).Within(1e-9));
            herself.State.PartyMembers = 2;
            Assert.That(herself.Combat.Minstrel.SelfSong, Is.EqualTo(0.15).Within(1e-9));
        }

        [Test]
        public void TheDuetAndTheSongForEveryonePlaySeveralMelodiesAndTheMedleyStrikes()
        {
            var (duet, _) = Hero(Flat(MinstrelStats.SongPower, 0.15), Flat(MinstrelStats.SongDuet, 1));
            Assert.That(duet.Combat.Minstrel.Sings(MinstrelMechanics.Fanfare), Is.True);
            Assert.That(duet.Combat.Minstrel.Sings(MinstrelMechanics.March), Is.True);
            Assert.That(duet.Combat.Minstrel.Sings(MinstrelMechanics.Hymn), Is.False);

            var (all, _) = Hero(Flat(MinstrelStats.SongPower, 0.15), Flat(MinstrelStats.SongAll, 1));
            Wait(all, Rules.SongAllEverySeconds);
            Assert.That(all.Combat.Minstrel.AllSongs, Is.True);
            Assert.That(all.Combat.Minstrel.Sings(MinstrelMechanics.Hymn), Is.True);
            Assert.That(all.Combat.Minstrel.SelfSong, Is.EqualTo(0.3).Within(1e-9));
            Wait(all, Rules.SongAllSeconds);
            Assert.That(all.Combat.Minstrel.AllSongs, Is.False);

            var (medley, input) = Hero(Flat(MinstrelStats.SongPower, 0.15), Flat(MinstrelStats.SongHits, -15), Flat(MinstrelStats.SongChangeHit, 3));
            Hits(medley, input, 24);
            var expected = Expected(medley, 3);
            var damage = Hit(medley, input);
            Assert.That(damage.Count, Is.EqualTo(2));
            Assert.That(damage.Max() / expected, Is.EqualTo(1).Within(0.05));
        }

        [Test]
        public void TheEngagementKeepsTheMelodyUntilTheTuneSkill()
        {
            var (loop, input) = Hero(Flat(MinstrelStats.SongPower, 0.15), Flat(MinstrelStats.SongStill, 1));
            Hits(loop, input, 80);
            Assert.That(loop.State.Minstrel.Melody, Is.EqualTo(MinstrelMechanics.Fanfare));

            Cast(loop, "minstrel_path_2");
            Assert.That(loop.State.Minstrel.Melody, Is.EqualTo(MinstrelMechanics.March));
        }

        [Test]
        public void SevenNotesPutTheEnemyToSleepAndTheNightmareDropsTheStoredDamage()
        {
            Assert.That(Rules.NoteHits, Is.EqualTo(20));
            Assert.That(Rules.SleepNotes, Is.EqualTo(7));
            var (loop, input) = Hero(Flat(MinstrelStats.SleepPower, 1.4));
            Cast(loop, "minstrel_path_3");
            Cast(loop, "minstrel_path_3");
            Hits(loop, input, 19);
            Assert.That(Hit(loop, input), Has.Count.EqualTo(1));
            Assert.That(loop.State.Minstrel.Asleep, Is.True);
            Assert.That(loop.State.TraitPartyDamage, Is.EqualTo(0.4).Within(1e-9));

            loop.Events.Clear();
            for (var i = 0; i < 5; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
            }
            Assert.That(Damage(loop), Is.Empty);
            var stored = loop.State.Minstrel.Dream.ToDouble();
            Assert.That(stored / (5 * Expected(loop, 1.4)), Is.EqualTo(1).Within(0.05));

            Wait(loop, Rules.SleepSeconds);
            var wake = Damage(loop);
            Assert.That(loop.State.Minstrel.Asleep, Is.False);
            Assert.That(wake.Single(), Is.EqualTo(stored).Within(stored * 1e-9));
            Assert.That(loop.State.TraitPartyDamage, Is.Zero);
        }

        [Test]
        public void TheLightSleepHitsAtOnceAndSleepDustAddsANotePerSkill()
        {
            var (loop, input) = Hero(Flat(MinstrelStats.SleepPower, 1.4), Flat(MinstrelStats.SleepLight, 1), Flat(MinstrelStats.SleepByCast, 1));
            Cast(loop, "minstrel_hit_1");
            Assert.That(loop.State.Minstrel.Notes, Is.EqualTo(1));
            Cast(loop, "minstrel_path_3");
            Cast(loop, "minstrel_path_3");
            Assert.That(loop.State.Minstrel.Asleep, Is.True);

            var damage = Hit(loop, input);
            Assert.That(damage, Has.Count.EqualTo(1));
            Assert.That(loop.State.Minstrel.Dream.Sign, Is.Zero);
            Assert.That(loop.Combat.Minstrel.DamageTaken(DamageSource.Keyboard), Is.EqualTo(1.4).Within(1e-9));
        }

        [Test]
        public void TheWakingDreamStartsABossFightAsleepAndTheNightmareForgetsNotes()
        {
            var (dream, dreamInput) = Hero(Flat(MinstrelStats.SleepPower, 1.4), Flat(MinstrelStats.SleepOnBoss, 1));
            Cast(dream, "minstrel_path_3");
            Assert.That(dream.State.Minstrel.Notes, Is.Zero);
            Spawn(dream, EnemyType.Boss);
            dream.Tick();
            Assert.That(dream.State.Minstrel.Asleep, Is.True);
            Assert.That(dream.State.Minstrel.SleepTicks, Is.EqualTo(Rules.SleepBossSeconds * GameLoop.TicksPerSecond - 1).Within(1));

            var (nightmare, _) = Hero(Flat(MinstrelStats.SleepPower, 1.4), Flat(MinstrelStats.SleepForget, 1));
            Cast(nightmare, "minstrel_path_3");
            Spawn(nightmare, EnemyType.Normal);
            nightmare.Tick();
            Assert.That(nightmare.State.Minstrel.Notes, Is.Zero);
        }

        [Test]
        public void SharedSkillsWorkForEveryTrait()
        {
            var (loop, input) = Hero();
            Cast(loop, "minstrel_strong_2");
            loop.Tick();
            Assert.That(loop.State.TraitPartyDamage, Is.EqualTo(0.3).Within(1e-9));
            Assert.That(loop.Combat.Minstrel.DamageTaken(DamageSource.Keyboard), Is.EqualTo(1.3).Within(1e-9));
            Assert.That(loop.Combat.Minstrel.Climax, Is.True);

            var expected = Expected(loop, 3 * 1.3);
            var climax = Cast(loop, "minstrel_hit_3");
            Assert.That(climax.Single() / expected, Is.EqualTo(1).Within(0.05));

            Cast(loop, "minstrel_strong_3");
            Assert.That(loop.Combat.Minstrel.StrikeMultiplier(), Is.EqualTo(Rules.SoloMultiplier).Within(1e-9));

            var (verse, _) = Hero(Flat(MinstrelStats.VersePower, 0.1));
            Cast(verse, "minstrel_path_1");
            Cast(verse, "minstrel_path_1");
            var finale = Expected(verse, 4 * 2 * 1.2);
            var hit = Cast(verse, "minstrel_strong_1");
            Assert.That(hit.Single() / finale, Is.EqualTo(1).Within(0.05));
            Assert.That(verse.State.Minstrel.Verses, Is.Zero);
        }

        [Test]
        public void OtherCharactersIgnoreMinstrelStats()
        {
            var (loop, input) = Hero(HeroClass.Runedancer, Flat(MinstrelStats.VersePower, 0.1), Flat(MinstrelStats.SongPower, 0.15), Flat(MinstrelStats.SleepPower, 1.4));
            Hits(loop, input, 200);

            Assert.That(loop.State.Minstrel.Verses, Is.Zero);
            Assert.That(loop.State.Minstrel.MelodyChanges, Is.Zero);
            Assert.That(loop.State.Minstrel.Sleeps, Is.Zero);
            Assert.That(loop.Combat.Minstrel.DamageTaken(DamageSource.Keyboard), Is.EqualTo(1));
            Assert.That(loop.Combat.Minstrel.StrikeMultiplier(), Is.EqualTo(1));
        }

        [Test]
        public void EveryForkOfMinstrelIsARuleWithAText()
        {
            foreach (var path in TestWorld.Real.Ascendancy.SubclassesOf("minstrel"))
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
        public void TheMinstrelTreeHasTwentySixSmallFourteenMediumSixLegendaryAndFourCoreNodes()
        {
            var tree = TestWorld.Real.ClassTrees["minstrel"];
            var count = tree.Nodes.GroupBy(node => node.Kind).ToDictionary(group => group.Key, group => group.Count());

            Assert.That(count[TreeNodeKind.Small], Is.EqualTo(26));
            Assert.That(count[TreeNodeKind.Medium], Is.EqualTo(14));
            Assert.That(count[TreeNodeKind.Legendary], Is.EqualTo(6));
            Assert.That(count[TreeNodeKind.Core], Is.EqualTo(4));
            Assert.That(tree.Nodes.Count(node => node.Template.First.Modifiers.Concat(node.Template.Second.Modifiers).Concat(node.Template.Third.Modifiers)
                .Any(modifier => modifier.Stat.Name.StartsWith("party") || modifier.When == ModifierCondition.InParty
                    || modifier.Stat.Name == MinstrelStats.VerseParty.Name || modifier.Stat.Name == MinstrelStats.SongParty.Name || modifier.Stat.Name == MinstrelStats.SleepParty.Name)), Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void TheMinstrelMechanicsDoNotAllocate()
        {
            var probeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (System.GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var (verse, verseInput) = Hero(Flat(MinstrelStats.VersePower, 0.1), Flat(MinstrelStats.ChorusEncore, 1), Flat(MinstrelStats.ChorusGift, 1));
            var (song, songInput) = Hero(Flat(MinstrelStats.SongPower, 0.15), Flat(MinstrelStats.SongAll, 1), Flat(MinstrelStats.SongChangeHit, 3));
            var (sleep, sleepInput) = Hero(Flat(MinstrelStats.SleepPower, 1.4), Flat(MinstrelStats.NoteHits, -15));
            void Run(GameLoop loop, FakeInputSource input, int ticks)
            {
                for (var tick = 0; tick < ticks; tick++)
                {
                    input.PressKeys(1);
                    loop.Tick();
                    loop.Events.Clear();
                    loop.Drops.Flush();
                }
            }
            Run(verse, verseInput, 600);
            Run(song, songInput, 600);
            Run(sleep, sleepInput, 600);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            Run(verse, verseInput, 300);
            Run(song, songInput, 300);
            Run(sleep, sleepInput, 300);

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
