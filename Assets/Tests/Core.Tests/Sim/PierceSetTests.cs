using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class PierceSetTests
    {
        private static ItemCatalog Items => TestWorld.Real.Items;

        private static (GameLoop Loop, FakeInputSource Input) Hero(params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = new GameLoop(TestWorld.WithPierce, input, new GameState(), hero);
            loop.Tick();
            loop.Events.Clear();
            return (loop, input);
        }

        private static Modifier Flat(StatId stat, double value) => new Modifier(stat, ModifierKind.Flat, value);

        [Test]
        public void FourArmourLinesAndAJewelryLineCarryThePierceStats()
        {
            foreach (var line in new[] { "piercing_blade", "wavebreaker", "colossus_ram", "dusk_cloak", "lightning_sparks" })
                Assert.That(Items.Sets.Count(s => s.Theme == line), Is.EqualTo(6), line);
            Assert.That(Items.SetOf("lightning_sparks", ItemRarity.Rare)!.Jewelry, Is.True);
            Assert.That(Items.Consonances.Any(c => c.Armor == "piercing_blade" && c.Jewelry == "lightning_sparks"), Is.True);
            Assert.That(Items.SetOf("piercing_blade", ItemRarity.Rift)!.PieceLines(ItemSlot.Weapon)[0].Value, Is.EqualTo(2));
        }

        [Test]
        public void PierceShareGrowsButStopsAtTheCap()
        {
            var (loop, _) = Hero(Flat(CombatStats.PierceShare, 0.2));
            Assert.That(loop.Combat.PierceShare, Is.EqualTo(0.7).Within(1e-12));
            var (capped, _) = Hero(Flat(CombatStats.PierceShare, 5));
            Assert.That(capped.Combat.PierceShare, Is.EqualTo(1));
        }

        [Test]
        public void AKillSkipsTheNextEnemiesWithFullRewardsButNeverTheBoss()
        {
            var (loop, _) = Hero(Flat(CombatStats.EnemySkip, 2));
            var gold = loop.State.Gold;
            loop.Stages.OnEnemyKilled();
            Assert.That(loop.State.KillsOnStage, Is.EqualTo(3));
            Assert.That(loop.State.Gold, Is.GreaterThan(gold));

            var (capped, _) = Hero(Flat(CombatStats.EnemySkip, 50));
            capped.Stages.OnEnemyKilled();
            Assert.That(capped.State.KillsOnStage, Is.EqualTo(System.Math.Min(5, TestWorld.WithPierce.Stages.EnemiesOnStage(1))));
            while (capped.State.EnemyType == EnemyType.Normal)
                capped.Stages.OnEnemyKilled();
            Assert.That(capped.State.KillsOnStage, Is.EqualTo(TestWorld.WithPierce.Stages.EnemiesOnStage(1)));
            Assert.That(capped.State.EnemyType, Is.EqualTo(EnemyType.Boss));
        }

        [Test]
        public void SplashWoundsTheNextEnemiesInTheQueue()
        {
            var (loop, input) = Hero(Flat(CombatStats.SplashShare, 0.5), Flat(CombatStats.SplashTargets, 2));
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 30));
            input.PressKeys(1);
            loop.Tick();
            var queue = loop.State.QueueDamage;
            Assert.That(queue[0].Sign, Is.GreaterThan(0));
            Assert.That(queue[1], Is.EqualTo(queue[0]));
            Assert.That(queue[2].Sign, Is.Zero);
        }

        [Test]
        public void TheChainWoundsTheBossButCannotKillIt()
        {
            var (loop, input) = Hero(new Modifier(CombatStats.Damage, ModifierKind.More, 1e12), Flat(CombatStats.BossPierce, 5));
            input.PressKeys(1);
            loop.Tick();
            Assert.That(loop.State.EnemyType, Is.EqualTo(EnemyType.Boss));
            Assert.That(loop.State.EnemyHp, Is.EqualTo(loop.State.EnemyMaxHp * 0.5), "the chain takes at most half of the boss bar");
        }

        [Test]
        public void BossOverkillCarriesIntoTheNextStage()
        {
            var (loop, _) = Hero(Flat(CombatStats.StageCarry, 1));
            while (loop.State.EnemyType == EnemyType.Normal)
                loop.Stages.OnEnemyKilled();
            loop.State.EnemyHp = BigNumber.Zero;
            loop.State.PierceDamage = BigNumber.Zero;
            var carried = BigNumber.Create(1, 40);
            loop.State.PierceDamage = carried;
            loop.Stages.OnEnemyKilled();
            Assert.That(loop.State.Stage, Is.EqualTo(2));
            Assert.That(loop.State.KillsOnStage, Is.GreaterThan(0), "the carried overkill kills the first enemies of stage 2");
        }

        private static long KillBossesUntilSkip(GameLoop loop)
        {
            for (var boss = 0; boss < 300; boss++)
            {
                while (loop.State.EnemyType == EnemyType.Normal)
                    loop.Stages.OnEnemyKilled();
                loop.Events.Clear();
                var before = loop.State.Stage;
                loop.Stages.OnEnemyKilled();
                if (loop.Events.CountOf(GameEventKind.StageSkipped) > 0)
                    return before;
            }
            Assert.Fail("no stage skip in 300 bosses at a 15% chance");
            return 0;
        }

        [Test]
        public void ABossKillMaySkipTenStagesButNotPastTheRecord()
        {
            Assert.That(TestWorld.Real.Combat.Pierce.MaxStageSkipChance, Is.EqualTo(0.15));
            var (loop, _) = Hero(Flat(CombatStats.StageSkipChance, 1));
            loop.State.RecordStage = 100_000;
            var before = KillBossesUntilSkip(loop);
            Assert.That(loop.State.Stage, Is.EqualTo(before + 1 + 10));

            var (capped, _) = Hero(Flat(CombatStats.StageSkipChance, 1));
            capped.State.RecordStage = 1;
            for (var i = 0; i < 300 && capped.Events.CountOf(GameEventKind.StageSkipped) == 0; i++)
            {
                capped.State.RecordStage = capped.State.Stage + 4;
                while (capped.State.EnemyType == EnemyType.Normal)
                    capped.Stages.OnEnemyKilled();
                capped.Events.Clear();
                capped.Stages.OnEnemyKilled();
            }
            Assert.That(capped.Events.CountOf(GameEventKind.StageSkipped), Is.EqualTo(1));
            Assert.That(capped.State.Stage, Is.EqualTo(capped.State.RecordStage), "the skip stops on the record");
        }

        [Test]
        public void SkippedEnemiesCountForQuestsAndTheBestiary()
        {
            var (loop, _) = Hero(Flat(CombatStats.EnemySkip, 2));
            var enemy = loop.State.Enemy!;
            var kills = loop.State.Kills;
            var before = loop.Bestiary.KillsOf(enemy.Id);
            loop.Events.Clear();
            loop.Stages.OnEnemyKilled();
            loop.Bestiary.Tick(0);
            Assert.That(loop.State.Kills - kills, Is.EqualTo(3), "quests and achievements read this counter");
            Assert.That(loop.Bestiary.KillsOf(enemy.Id) - before, Is.EqualTo(3));
        }
    }
}
