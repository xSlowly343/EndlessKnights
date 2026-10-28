using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ArtifactRulesTests
    {
        private static ArtifactCatalog Catalog => TestWorld.Data.Artifacts;

        private static GameLoop With(HeroClass heroClass, params (string Id, int Level)[] artifacts)
        {
            var state = TestWorld.Classless(new GameState(3) { Class = heroClass });
            foreach (var (id, level) in artifacts)
                state.Artifacts[id] = level;
            var loop = TestWorld.Loop(state: state);
            loop.Artifacts.Rebuild();
            return loop;
        }

        private static GameLoop With(params (string Id, int Level)[] artifacts) => With(HeroClass.Viking, artifacts);

        [Test]
        public void EveryGroupHasOneClassArtifactPerHero()
        {
            for (var group = 1; group <= Catalog.Groups; group++)
            {
                var classes = Catalog.All.Where(a => a.Group == group && a.Type == ArtifactType.Class).Select(a => a.Class).ToList();
                Assert.That(classes.Count, Is.EqualTo(14), "group " + group);
                Assert.That(classes.Distinct().Count(), Is.EqualTo(14), "group " + group);
            }
        }

        [Test]
        public void EveryGroupFollowsTheSameTemplate()
        {
            for (var group = 1; group <= Catalog.Groups; group++)
            {
                var inGroup = Catalog.All.Where(a => a.Group == group).ToList();
                Assert.That(inGroup.Count(a => a.Type == ArtifactType.Damage), Is.EqualTo(4), "group " + group);
                Assert.That(inGroup.Count(a => a.Type == ArtifactType.Economy), Is.EqualTo(3), "group " + group);
                Assert.That(inGroup.Count(a => a.Type == ArtifactType.Tempo), Is.EqualTo(2), "group " + group);
                Assert.That(inGroup.Count(a => a.Type == ArtifactType.Meta), Is.EqualTo(2), "group " + group);
                Assert.That(inGroup.Count(a => a.Type == ArtifactType.Late), Is.EqualTo(5), "group " + group);
            }
        }

        [Test]
        public void TheNextGroupOpensOnlyAfterThePreviousIsFull()
        {
            var loop = TestWorld.Loop(state: TestWorld.Classless(new GameState(5)));
            loop.State.Echo = BigNumber.FromDouble(1e12);

            for (var i = 0; i < 30; i++)
            {
                Assert.That(loop.Artifacts.OpenGroup, Is.EqualTo(1));
                Assert.That(loop.Artifacts.TryUnlock()!.Group, Is.EqualTo(1));
            }

            Assert.That(loop.Artifacts.OpenGroup, Is.EqualTo(2));
            Assert.That(loop.Artifacts.Chances().All(c => c.Artifact.Group == 2), Is.True);
            Assert.That(loop.Artifacts.TryUnlock()!.Group, Is.EqualTo(2));
        }

        [Test]
        public void CodexOfTheRiftIsInTheFirstGroup()
        {
            Assert.That(Catalog.TryGet("rift_codex", out var codex), Is.True);
            Assert.That(codex.Group, Is.EqualTo(1));
        }

        [Test]
        public void CappedArtifactsStopLevelling()
        {
            var loop = With(("hourglass", 30), ("pathfinder_seal", 30));
            loop.State.Echo = BigNumber.FromDouble(1e30);

            Assert.That(loop.Artifacts.IsMaxed("hourglass"), Is.True);
            Assert.That(loop.Artifacts.TryLevelUp("hourglass"), Is.False);
            Assert.That(loop.State.Artifacts["hourglass"], Is.EqualTo(30));
            Assert.That(loop.Artifacts.TryLevelUp("pathfinder_seal"), Is.True);
            Assert.That(Catalog.All.Count(a => a.Capped), Is.EqualTo(1));
        }

        [Test]
        public void LevelsAboveTheCapCountAsTheCap()
        {
            var capped = With(("hourglass", 30));
            var over = With(("hourglass", 500));

            Assert.That(over.Hero.Value(CombatStats.BossTimerSeconds, 0), Is.EqualTo(capped.Hero.Value(CombatStats.BossTimerSeconds, 0)));
            Assert.That(over.Hero.Value(CombatStats.BossTimerSeconds, 0), Is.EqualTo(60).Within(1e-9));
        }

        [Test]
        public void ClassArtifactWorksOnlyForItsHero()
        {
            var viking = With(HeroClass.Viking, ("jarl_horn", 5));
            var ronin = With(HeroClass.Ronin, ("jarl_horn", 5));

            Assert.That(viking.Hero.Value(CombatStats.ResourceGain, 1), Is.EqualTo(1.2).Within(1e-9));
            Assert.That(ronin.Hero.Value(CombatStats.ResourceGain, 1), Is.EqualTo(1).Within(1e-9));
            Assert.That(ronin.Hero.Value(CombatStats.Damage, 100), Is.EqualTo(100 * (1 + 5 * Catalog.AllDamagePerLevel)).Within(1e-9));
        }

        [Test]
        public void SkyBladeRaisesTheGeneralBonusOfEveryArtifact()
        {
            var plain = With(("hourglass", 10));
            var boosted = With(("hourglass", 10), ("sky_blade", 5));

            var general = 15 * Catalog.AllDamagePerLevel * (1 + 5 * 0.04);
            Assert.That(plain.Hero.Value(CombatStats.Damage, 100), Is.EqualTo(100 * (1 + 10 * Catalog.AllDamagePerLevel)).Within(1e-9));
            Assert.That(boosted.Hero.Value(CombatStats.Damage, 100), Is.EqualTo(100 * (1 + general)).Within(1e-9));
        }

        [Test]
        public void AntiquaryLensStrengthensDamageArtifactsOnly()
        {
            var loop = With(("titan_skull", 10), ("colossus_purse", 10), ("antiquary_lens", 5));

            Assert.That(loop.Hero.Value(CombatStats.DamageVsBoss, 1), Is.EqualTo(1 + 0.8 * 1.1).Within(1e-9));
            Assert.That(loop.Hero.Value(CombatStats.Gold, 1, StatTags.Gold), Is.EqualTo(2).Within(1e-9));
        }

        [Test]
        public void AncientTreasuryStrengthensEconomyArtifacts()
        {
            var loop = With(("colossus_purse", 10), ("ancient_treasury", 10));

            Assert.That(loop.Hero.Value(CombatStats.Gold, 1, StatTags.Gold), Is.EqualTo(1 + 1.0 * 1.2).Within(1e-9));
        }

        [Test]
        public void StarDustGrowsWithEveryOpenArtifact()
        {
            var few = With(("star_dust", 10));
            var many = With(("star_dust", 10), ("hourglass", 1), ("anchor", 1), ("luck_axe", 1));

            var fewDamage = few.Hero.Value(CombatStats.Damage, 1) - 10 * Catalog.AllDamagePerLevel;
            var manyDamage = many.Hero.Value(CombatStats.Damage, 1) - 13 * Catalog.AllDamagePerLevel;
            Assert.That(fewDamage, Is.EqualTo(1 + 0.001 * 10).Within(1e-9));
            Assert.That(manyDamage, Is.EqualTo(1 + 0.001 * 10 * 4).Within(1e-9));
        }

        [Test]
        public void PathfinderSealMakesTheNextUnlockCheaper()
        {
            var plain = With(("hourglass", 1));
            var sealed_ = With(("pathfinder_seal", 10));

            Assert.That(sealed_.Artifacts.UnlockCost.ToDouble(), Is.EqualTo(plain.Artifacts.UnlockCost.ToDouble() * 0.9).Within(1e-6));
        }

        [Test]
        public void RiftCodexGivesMoreEcho()
        {
            var plain = With();
            var codex = With(("rift_codex", 4));
            plain.State.RunGoldEarned = BigNumber.FromDouble(1e9);
            codex.State.RunGoldEarned = BigNumber.FromDouble(1e9);

            var expected = System.Math.Floor(plain.Prestige.EchoGain.ToDouble() * 1.2);
            Assert.That(plain.Prestige.EchoGain.Sign, Is.GreaterThan(0));
            Assert.That(codex.Prestige.EchoGain.ToDouble(), Is.EqualTo(expected).Within(1));
        }

        [Test]
        public void ParchmentOfForesightStartsTheRunFurtherButNotPastTheRecord()
        {
            var loop = With(("foresight_parchment", 5));
            loop.State.HighestStage = 80;
            loop.State.RecordStage = 80;
            loop.State.RunGoldEarned = BigNumber.FromDouble(1e9);

            Assert.That(loop.Prestige.TryPrestige(), Is.True);
            Assert.That(loop.State.Stage, Is.EqualTo(6));
            Assert.That(loop.State.HighestStage, Is.EqualTo(6));

            var low = With(("foresight_parchment", 5));
            low.State.RecordStage = 3;
            Assert.That(low.Prestige.StartStage, Is.EqualTo(3));
        }

        [Test]
        public void MaskOfTheLostKingLowersUpgradeCost()
        {
            var plain = With();
            var masked = With(("lost_king_mask", 20));

            Assert.That(masked.Upgrades.CostFor(3).ToDouble(), Is.EqualTo(plain.Upgrades.CostFor(3).ToDouble() * 0.8).Within(1e-6));
            plain.State.Gold = plain.Upgrades.CostFor(5) * 0.8;
            masked.State.Gold = plain.State.Gold;
            Assert.That(masked.Upgrades.Affordable(), Is.EqualTo(5));
            Assert.That(plain.Upgrades.Affordable(), Is.LessThan(5));
        }

        [Test]
        public void ProspectorSieveGivesTenfoldGoldFromNormalEnemiesOnly()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.GoldJackpotChance, ModifierKind.Flat, 1));
            var luckyState = new GameState();
            var plainState = new GameState();
            var multiplier = TestWorld.Data.Artifacts.JackpotMultiplier;
            var lucky = new LootSystem(TestWorld.Data.Stages, hero, luckyState, new EventQueue()) { JackpotMultiplier = multiplier };
            var plain = new LootSystem(TestWorld.Data.Stages, new StatSheet(), plainState, new EventQueue()) { JackpotMultiplier = multiplier };

            lucky.OnKill(5, EnemyType.Normal, null);
            plain.OnKill(5, EnemyType.Normal, null);
            var normalGold = plainState.Gold.ToDouble();
            Assert.That(multiplier, Is.EqualTo(10));
            Assert.That(luckyState.Gold.ToDouble(), Is.EqualTo(normalGold * 10).Within(1e-6));

            lucky.OnKill(5, EnemyType.Boss, null);
            plain.OnKill(5, EnemyType.Boss, null);
            var bossGold = plainState.Gold.ToDouble() - normalGold;
            Assert.That(luckyState.Gold.ToDouble(), Is.EqualTo(normalGold * 10 + bossGold).Within(1e-6));
        }

        [Test]
        public void WardenBannerHitsGuardiansHarder()
        {
            var loop = With(("warden_banner", 5));
            var normal = loop.Combat.ConditionMultiplier(StatTags.Keyboard);
            loop.State.SpawnEnemy(null, EnemyType.Guardian, BigNumber.FromDouble(1e9));

            Assert.That(loop.Combat.ConditionMultiplier(StatTags.Keyboard) / normal, Is.EqualTo(1.5).Within(1e-9));
        }

        [Test]
        public void MoonLanternCountsMoreDamageOffline()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.OfflineDpsShare, ModifierKind.Flat, 0.1));
            var state = new GameState { RecentDps = BigNumber.FromDouble(1000) };

            var plain = new OfflineCalculator(TestWorld.Data, new StatSheet()).OfflineDps(state).ToDouble();
            var lit = new OfflineCalculator(TestWorld.Data, hero).OfflineDps(state).ToDouble();

            Assert.That(lit - plain, Is.EqualTo(100).Within(1e-6));
        }

        [Test]
        public void RiftBootsBringTheWandererMoreOften()
        {
            var json = TestData.ReadGameDataFile(CombatRules.FileName)
                .Replace("\"wanderer\": { \"chance\": 0.05", "\"wanderer\": { \"chance\": 0")
                .Replace("\"golden\": { \"chance\": 0.002", "\"golden\": { \"chance\": 0");
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1e12));
            hero.Add(new Modifier(CombatStats.WandererChance, ModifierKind.Flat, 1));
            var state = TestWorld.Classless(new GameState(9) { Class = HeroClass.Viking, Stage = 40, HighestStage = 40, RecordStage = 300 });
            var loop = TestWorld.Loop(input, state, hero);
            loop.Stages.Encounters = CombatRules.Load(json).Encounters;

            var jumped = false;
            for (var tick = 0; tick < 3000 && !jumped; tick++)
            {
                input.PressKeys(3);
                loop.Tick();
                for (var i = 0; i < loop.Events.Count; i++)
                    jumped |= loop.Events[i].Kind == GameEventKind.WandererJumped;
                loop.Events.Clear();
            }

            Assert.That(jumped, Is.True);
        }
    }
}
