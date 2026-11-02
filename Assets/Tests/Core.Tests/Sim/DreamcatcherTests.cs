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
    public class DreamcatcherTests
    {
        private static DreamcatcherRules Rules => TestWorld.Data.Combat.Classes.Dreamcatcher;

        private static Modifier Flat(StatId stat, double value) => new Modifier(stat, ModifierKind.Flat, value);

        private static EnemyDef Kind(int index) => TestWorld.Data.Enemies.Normals("green_hills")[index];

        private static (GameLoop Loop, FakeInputSource Input) Hero(params Modifier[] modifiers) => Hero(HeroClass.Dreamcatcher, modifiers);

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

        private static void Spawn(GameLoop loop, EnemyType type, EnemyDef? enemy = null, double hp = 1e50)
        {
            loop.State.SpawnEnemy(enemy, type, BigNumber.FromDouble(hp));
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

        private static void Hit(GameLoop loop, FakeInputSource input)
        {
            input.PressKeys(1);
            loop.Tick();
            loop.Tick();
        }

        private static void Hits(GameLoop loop, FakeInputSource input, int count)
        {
            for (var i = 0; i < count; i++)
                Hit(loop, input);
        }

        private static void Step(GameLoop loop, FakeInputSource input, double px, int ticks = 1)
        {
            for (var i = 0; i < ticks; i++)
            {
                input.MoveMouse((float)(px / ticks));
                loop.Tick();
            }
        }

        private static void Wait(GameLoop loop, double seconds)
        {
            for (var i = 0; i < System.Math.Round(seconds * GameLoop.TicksPerSecond); i++)
                loop.Tick();
        }

        private static void Kill(GameLoop loop, FakeInputSource input, EnemyDef? enemy = null, EnemyType type = EnemyType.Normal)
        {
            Spawn(loop, type, enemy, 1e-9);
            loop.Tick();
            Hit(loop, input);
        }

        private static double Expected(GameLoop loop, double multiplier) =>
            loop.Combat.SkillDamage(loop.Combat.BaseKeyDamage, multiplier * loop.State.FocusMultiplier, StatTags.Keyboard).ToDouble();

        private static List<double> Cast(GameLoop loop, string skill)
        {
            loop.Events.Clear();
            loop.Combat.Dreamcatcher.OnSkillCast(TestWorld.Data.Gems[skill]);
            return Damage(loop);
        }

        [Test]
        public void StepsWeaveUpToThreeNetsThatHangInAPause()
        {
            Assert.That(Rules.NetPx, Is.EqualTo(2000));
            var (loop, input) = Hero(Flat(DreamcatcherStats.NetPower, 0.5));
            Step(loop, input, 1900, 2);
            Assert.That(loop.State.Dreamcatcher.Nets, Is.Zero);
            Step(loop, input, 200);
            Assert.That(loop.State.Dreamcatcher.Nets, Is.EqualTo(1));

            Step(loop, input, 10_000, 5);
            Assert.That(loop.State.Dreamcatcher.Nets, Is.EqualTo(Rules.NetMax));
            Wait(loop, 30);
            Assert.That(loop.State.Dreamcatcher.Nets, Is.EqualTo(Rules.NetMax));
        }

        [Test]
        public void AnEnemyWalkingIntoANetIsHelplessForThreeSeconds()
        {
            var (loop, input) = Hero(Flat(DreamcatcherStats.NetPower, 0.5));
            Cast(loop, "dreamcatcher_path_1");
            Assert.That(loop.State.Dreamcatcher.Nets, Is.EqualTo(2));

            Spawn(loop, EnemyType.Normal);
            loop.Tick();
            Assert.That(loop.State.Dreamcatcher.Nets, Is.EqualTo(1));
            Assert.That(loop.State.Dreamcatcher.Caught, Is.True);
            Assert.That(loop.Combat.Dreamcatcher.DamageTaken(), Is.EqualTo(1.5).Within(1e-9));

            Wait(loop, Rules.NetSeconds);
            Assert.That(loop.State.Dreamcatcher.Caught, Is.False);
            Assert.That(loop.Combat.Dreamcatcher.DamageTaken(), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void AnEnemyDyingInTheNetMayDropATrophyAndATightNetComesBack()
        {
            var (loop, input) = Hero(Flat(DreamcatcherStats.NetPower, 0.5), Flat(DreamcatcherStats.NetTrophy, 1), Flat(DreamcatcherStats.NetReturn, 1));
            Cast(loop, "dreamcatcher_path_1");
            var gems = loop.State.OwnedGems.Count;

            Kill(loop, input, Kind(0));
            loop.Drops.Flush();

            Assert.That(loop.State.Dreamcatcher.Trophies, Is.EqualTo(1));
            Assert.That(loop.State.OwnedGems.Count, Is.GreaterThan(gems));
            Assert.That(loop.State.Dreamcatcher.Catches, Is.GreaterThanOrEqualTo(1));
            Assert.That(loop.State.Dreamcatcher.Nets, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void NetsCatchBossesOnlyWithTheColossusNetAndTheClockStopsTheTimer()
        {
            var (plain, _) = Hero(Flat(DreamcatcherStats.NetPower, 0.5));
            Cast(plain, "dreamcatcher_path_1");
            Spawn(plain, EnemyType.Boss);
            plain.Tick();
            Assert.That(plain.State.Dreamcatcher.Caught, Is.False);

            var (clock, _) = Hero(Flat(DreamcatcherStats.NetPower, 0.5), Flat(DreamcatcherStats.NetBoss, 1), Flat(DreamcatcherStats.NetTimer, 1));
            Cast(clock, "dreamcatcher_path_1");
            Spawn(clock, EnemyType.Boss);
            clock.Tick();
            var left = clock.State.BossTicksLeft;
            Wait(clock, 1);
            Assert.That(clock.State.Dreamcatcher.Caught, Is.True);
            Assert.That(clock.State.BossTicksLeft, Is.EqualTo(left));
        }

        [Test]
        public void EveryKillGoesIntoTheAlbumAndThePartyHitsThatKindHarder()
        {
            var (loop, input) = Hero(Flat(DreamcatcherStats.AlbumPower, 0.01));
            for (var i = 0; i < 5; i++)
                Kill(loop, input, Kind(0));
            var index = loop.Combat.Dreamcatcher.SpeciesIndex(Kind(0).Id);
            Assert.That(loop.State.Dreamcatcher.Album[index], Is.EqualTo(5));

            Spawn(loop, EnemyType.Normal, Kind(0));
            loop.Tick();
            Assert.That(loop.State.TraitShareDamage, Is.EqualTo(0.05).Within(1e-9));
            Spawn(loop, EnemyType.Normal, Kind(1));
            loop.Tick();
            Assert.That(loop.State.TraitShareDamage, Is.Zero);

            Cast(loop, "dreamcatcher_path_2");
            loop.Tick();
            Assert.That(loop.State.TraitShareDamage, Is.EqualTo(0.03).Within(1e-9));
        }

        [Test]
        public void TheAlbumBonusStopsAtItsLimit()
        {
            var (loop, input) = Hero(Flat(DreamcatcherStats.AlbumPower, 0.01));
            var index = loop.Combat.Dreamcatcher.SpeciesIndex(Kind(0).Id);
            loop.State.Dreamcatcher.Album[index] = 500;
            Assert.That(loop.Combat.Dreamcatcher.AlbumBonus(index), Is.EqualTo(Rules.AlbumMax).Within(1e-9));
        }

        [Test]
        public void AFullPageGivesTheNextBossOfTheBiomeAPropheticDream()
        {
            var (loop, input) = Hero(Flat(DreamcatcherStats.AlbumPower, 0.01));
            var normals = TestWorld.Data.Enemies.Normals("green_hills");
            foreach (var kind in normals)
                loop.State.Dreamcatcher.Album[loop.Combat.Dreamcatcher.SpeciesIndex(kind.Id)] = Rules.PageMin;
            var biome = loop.Combat.Dreamcatcher.BiomeIndex("green_hills");
            Assert.That(loop.Combat.Dreamcatcher.PageFull(biome), Is.True);

            Spawn(loop, EnemyType.Boss, TestWorld.Data.Enemies.Boss("green_hills"));
            loop.Tick();
            Assert.That(loop.State.Dreamcatcher.Prophecies, Is.EqualTo(1));
            Assert.That(loop.Combat.Dreamcatcher.DamageTaken(), Is.EqualTo(1 + Rules.ProphecyPower).Within(1e-9));

            Spawn(loop, EnemyType.Boss, TestWorld.Data.Enemies.Boss("green_hills"));
            loop.Tick();
            Assert.That(loop.State.Dreamcatcher.Prophecies, Is.EqualTo(1));
        }

        [Test]
        public void TheAlbumEndsAtARebirthUnlessMemoryKeepsHalf()
        {
            var (plain, _) = Hero(Flat(DreamcatcherStats.AlbumPower, 0.01));
            var index = plain.Combat.Dreamcatcher.SpeciesIndex(Kind(0).Id);
            plain.State.Dreamcatcher.Album[index] = 10;
            plain.State.Prestiges++;
            plain.Tick();
            Assert.That(plain.State.Dreamcatcher.Album[index], Is.Zero);

            var (memory, _) = Hero(Flat(DreamcatcherStats.AlbumPower, 0.01), Flat(DreamcatcherStats.AlbumKeep, 1));
            memory.State.Dreamcatcher.Album[index] = 10;
            memory.State.Prestiges++;
            memory.Tick();
            Assert.That(memory.State.Dreamcatcher.Album[index], Is.EqualTo(5));
        }

        [Test]
        public void QuickSketchesCountTwiceTheLucidAlbumHelpsEverywhereAndOthersPagesNeedAParty()
        {
            var (sketch, sketchInput) = Hero(Flat(DreamcatcherStats.AlbumPower, 0.01), Flat(DreamcatcherStats.AlbumDouble, 1));
            Kill(sketch, sketchInput, Kind(0));
            Assert.That(sketch.State.Dreamcatcher.Album[sketch.Combat.Dreamcatcher.SpeciesIndex(Kind(0).Id)], Is.EqualTo(2));

            var (lucid, _) = Hero(Flat(DreamcatcherStats.AlbumPower, 0.01), Flat(DreamcatcherStats.AlbumAll, 1));
            lucid.State.Dreamcatcher.Album[lucid.Combat.Dreamcatcher.SpeciesIndex(Kind(0).Id)] = 20;
            Assert.That(lucid.Combat.Dreamcatcher.AlbumBonus(lucid.Combat.Dreamcatcher.SpeciesIndex(Kind(1).Id)), Is.EqualTo(0.1).Within(1e-9));

            var (others, othersInput) = Hero(Flat(DreamcatcherStats.AlbumPower, 0.01), Flat(DreamcatcherStats.AlbumAllies, 1));
            Kill(others, othersInput, Kind(0));
            Assert.That(others.State.Dreamcatcher.Specimens, Is.Zero);
        }

        [Test]
        public void HundredHitsCallADreamSpiritThreeTimesTougherThatAlwaysDropsLoot()
        {
            Assert.That(Rules.BaitHits, Is.EqualTo(100));
            var (loop, input) = Hero(Flat(DreamcatcherStats.SpiritLoot, 1));
            Hits(loop, input, 100);
            Assert.That(loop.State.Dreamcatcher.SpiritsPending, Is.EqualTo(1));

            Spawn(loop, EnemyType.Normal, Kind(0), 100);
            loop.Tick();
            Assert.That(loop.State.Dreamcatcher.SpiritAlive, Is.True);
            Assert.That(loop.State.EnemySpirit, Is.True);
            Assert.That(loop.State.EnemyMaxHp.ToDouble(), Is.EqualTo(100 * Rules.SpiritHp).Within(1e-6));

            var gems = loop.State.OwnedGems.Count;
            var shards = loop.State.Drops.Shards;
            loop.State.ScaleEnemyHp(1e-12);
            Hit(loop, input);
            loop.Drops.Flush();
            Assert.That(loop.State.Dreamcatcher.SpiritKills, Is.EqualTo(1));
            Assert.That(loop.State.OwnedGems.Count - gems, Is.EqualTo(Rules.SpiritGems));
            Assert.That(loop.State.Drops.Shards - shards, Is.EqualTo(Rules.SpiritShards));
        }

        [Test]
        public void NetsDoNotCatchASpiritAndTheHornMakesItWeaker()
        {
            var (loop, input) = Hero(Flat(DreamcatcherStats.SpiritLoot, 1), Flat(DreamcatcherStats.SpiritHorn, 1));
            Cast(loop, "dreamcatcher_path_3");
            Cast(loop, "dreamcatcher_path_3");
            Spawn(loop, EnemyType.Normal, Kind(0));
            loop.Tick();
            Assert.That(loop.State.Dreamcatcher.SpiritAlive, Is.True);
            Assert.That(loop.Combat.Dreamcatcher.DamageTaken(), Is.EqualTo(1 + Rules.HornPower).Within(1e-9));
            Assert.That(loop.State.Dreamcatcher.Caught, Is.False);
        }

        [Test]
        public void SweetBaitGrowsWithSkillsAndTheGuideSpiritGivesResource()
        {
            var (loop, input) = Hero(Flat(DreamcatcherStats.SpiritLoot, 1), Flat(DreamcatcherStats.BaitByCast, 1), Flat(DreamcatcherStats.SpiritGift, 1));
            Cast(loop, "dreamcatcher_hit_2");
            Assert.That(loop.State.Dreamcatcher.Bait, Is.EqualTo(Rules.BaitPerCast));

            var pending = loop.State.PendingResource;
            loop.Combat.Dreamcatcher.GrantSpiritLoot(false, true);
            Assert.That(loop.State.PendingResource - pending, Is.EqualTo(Rules.SpiritGift).Within(1e-9));
            Assert.That(loop.State.TraitPartyResourceGift, Is.EqualTo(Rules.SpiritGift).Within(1e-9));
        }

        [Test]
        public void SharedSkillsWorkForEveryTrait()
        {
            var (loop, input) = Hero();
            var plain = Cast(loop, "dreamcatcher_hit_3");
            Assert.That(plain, Is.Empty);

            Cast(loop, "dreamcatcher_strong_1");
            Assert.That(loop.Combat.Dreamcatcher.DamageTaken(), Is.EqualTo(Rules.DozeMultiplier).Within(1e-9));
            var expected = Expected(loop, 4 * Rules.DozeMultiplier);
            var prick = Cast(loop, "dreamcatcher_hit_3");
            Assert.That(prick.Single() / expected, Is.EqualTo(1).Within(0.05));

            Wait(loop, 6);
            Cast(loop, "dreamcatcher_strong_3");
            Assert.That(loop.Combat.Dreamcatcher.DamageTaken(), Is.EqualTo(1 + Rules.LucidPower).Within(1e-9));
        }

        [Test]
        public void InAPartyTheNetAndTheSpiritGoToTheHost()
        {
            var (loop, input) = Hero(Flat(DreamcatcherStats.NetPower, 0.5), Flat(DreamcatcherStats.SpiritLoot, 1));
            loop.State.SharedEnemy = true;
            Cast(loop, "dreamcatcher_path_1");
            Spawn(loop, EnemyType.Normal, Kind(0));
            loop.State.SharedEnemy = true;
            loop.Tick();

            Assert.That(loop.State.TraitEnemyEffects.Count, Is.EqualTo(1));
            Assert.That(loop.State.TraitEnemyEffects[0].Id, Is.EqualTo(DreamcatcherMechanics.NetEffect));
            Assert.That(loop.State.TraitEnemyEffects[0].Strength, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(loop.Combat.Dreamcatcher.DamageTaken(), Is.EqualTo(1).Within(1e-9));

            Cast(loop, "dreamcatcher_path_3");
            Cast(loop, "dreamcatcher_path_3");
            Assert.That(loop.State.TraitSpiritCalls, Is.EqualTo(1));
            Assert.That(loop.State.Dreamcatcher.SpiritsPending, Is.Zero);
        }

        [Test]
        public void OtherCharactersIgnoreDreamcatcherStats()
        {
            var (loop, input) = Hero(HeroClass.Runedancer, Flat(DreamcatcherStats.NetPower, 0.5), Flat(DreamcatcherStats.AlbumPower, 0.01), Flat(DreamcatcherStats.SpiritLoot, 1));
            Hits(loop, input, 120);
            Step(loop, input, 10_000, 4);
            Kill(loop, input, Kind(0));

            Assert.That(loop.State.Dreamcatcher.Nets, Is.Zero);
            Assert.That(loop.State.Dreamcatcher.Specimens, Is.Zero);
            Assert.That(loop.State.Dreamcatcher.SpiritsPending, Is.Zero);
            Assert.That(loop.Combat.Dreamcatcher.DamageTaken(), Is.EqualTo(1));
        }

        [Test]
        public void EveryForkOfDreamcatcherIsARuleWithAText()
        {
            foreach (var path in TestWorld.Real.Ascendancy.SubclassesOf("dreamcatcher"))
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
        public void TheDreamcatcherTreeHasTwentySixSmallFourteenMediumSixLegendaryAndFourCoreNodes()
        {
            var tree = TestWorld.Real.ClassTrees["dreamcatcher"];
            var count = tree.Nodes.GroupBy(node => node.Kind).ToDictionary(group => group.Key, group => group.Count());

            Assert.That(count[TreeNodeKind.Small], Is.EqualTo(26));
            Assert.That(count[TreeNodeKind.Medium], Is.EqualTo(14));
            Assert.That(count[TreeNodeKind.Legendary], Is.EqualTo(6));
            Assert.That(count[TreeNodeKind.Core], Is.EqualTo(4));
            Assert.That(tree.Nodes.Count(node => node.Template.First.Modifiers.Concat(node.Template.Second.Modifiers).Concat(node.Template.Third.Modifiers)
                .Any(modifier => Game.Core.Party.CoopLine.IsCoop(modifier.Stat) || modifier.Stat.Name.EndsWith("Party")
                    || modifier.Stat.Name == DreamcatcherStats.NetParty.Name || modifier.Stat.Name == DreamcatcherStats.AlbumParty.Name)), Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void TheDreamcatcherMechanicsDoNotAllocate()
        {
            var probeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (System.GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var (nets, netsInput) = Hero(Flat(DreamcatcherStats.NetPower, 0.5), Flat(DreamcatcherStats.NetBoss, 1));
            var (album, albumInput) = Hero(Flat(DreamcatcherStats.AlbumPower, 0.01), Flat(DreamcatcherStats.AlbumAll, 1));
            var (bait, baitInput) = Hero(Flat(DreamcatcherStats.SpiritLoot, 1), Flat(DreamcatcherStats.SpiritHorn, 1));
            void Run(GameLoop loop, FakeInputSource input, int ticks)
            {
                for (var tick = 0; tick < ticks; tick++)
                {
                    input.PressKeys(1);
                    input.MoveMouse(300);
                    loop.Tick();
                    loop.Events.Clear();
                }
            }
            Run(nets, netsInput, 600);
            Run(album, albumInput, 600);
            Run(bait, baitInput, 600);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            Run(nets, netsInput, 300);
            Run(album, albumInput, 300);
            Run(bait, baitInput, 300);

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
