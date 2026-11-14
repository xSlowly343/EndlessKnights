using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Tree;
using Game.Core.Input;
using Game.Core.Party;
using Game.Core.Party.Transport;
using Game.Core.Party.Wire;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class CoopBuffTests
    {
        private const ulong HostId = 1;

        private static CoopRules Rules => TestWorld.Data.Party.Buffs.Coop;

        private static Modifier Line(string json) => ModifierReader.Read(ContentNode.Root(json, "test.json"));

        private static int IndexOf(string stat, ModifierKind kind) => Rules.IndexOf(new StatId(stat), kind);

        private static (PartyBuffs Buffs, GameState State, StatSheet Hero) Giver(HeroClass heroClass = HeroClass.Minstrel, PartyGiveMode mode = PartyGiveMode.Together)
        {
            var state = new GameState { Class = heroClass, PartyMode = mode };
            var hero = new StatSheet();
            return (new PartyBuffs(TestWorld.Data.Party.Buffs, state, hero), state, hero);
        }

        [Test]
        public void ALineWithATargetBecomesACoopModifierThatTheSameJsonFindsAgain()
        {
            var json = @"{ ""to"": ""party"", ""stat"": ""attackPower"", ""kind"": ""increased"", ""value"": 0.04, ""when"": ""boss"",
                ""per"": { ""signal"": ""resourceFill"", ""step"": 0.1, ""cap"": 5 } }";
            var first = Line(json);
            var second = Line(json);

            Assert.That(CoopLine.IsCoop(first.Stat), Is.True);
            Assert.That(first.Stat, Is.EqualTo(second.Stat), "one line, one stat: levels of the same line add up");
            Assert.That(first.Kind, Is.EqualTo(ModifierKind.Flat));
            Assert.That(first.When, Is.EqualTo(ModifierCondition.Boss));
            Assert.That(CoopLine.TryGet(first.Stat, out var line), Is.True);
            Assert.That(line.To, Is.EqualTo(CoopTarget.Party));
            Assert.That(line.Target, Is.EqualTo(CombatStats.Damage));
            Assert.That(line.Kind, Is.EqualTo(ModifierKind.Increased));
            Assert.That((line.PerSignal, line.PerStep, line.PerCap), Is.EqualTo((CoopSignal.ResourceFill, 0.1d, 5)));
        }

        [TestCase(@"{ ""to"": ""enemy"", ""stat"": ""attackPower"", ""kind"": ""increased"", ""value"": 0.1 }", "enemy line")]
        [TestCase(@"{ ""to"": ""party"", ""stat"": ""damageTaken"", ""kind"": ""flat"", ""value"": 0.1 }", "goes only to the enemy")]
        [TestCase(@"{ ""to"": ""crowd"", ""stat"": ""attackPower"", ""kind"": ""increased"", ""value"": 0.1 }", "unknown party buff target")]
        [TestCase(@"{ ""to"": ""party"", ""stat"": ""attackPower"", ""kind"": ""increased"", ""value"": 0.1, ""per"": { ""signal"": ""crit"", ""step"": 1, ""cap"": 3 } }", "events go to 'after'")]
        [TestCase(@"{ ""to"": ""party"", ""stat"": ""attackPower"", ""kind"": ""increased"", ""value"": 0.1, ""after"": { ""signal"": ""combo"", ""seconds"": 3 } }", "gauges go to 'per'")]
        [TestCase(@"{ ""to"": ""party"", ""stat"": ""attackPower"", ""kind"": ""increased"", ""value"": 0.1, ""after"": { ""signal"": ""dance"", ""seconds"": 3 } }", "unknown party buff signal")]
        [TestCase(@"{ ""to"": ""party"", ""stat"": ""attackPower"", ""kind"": ""increased"", ""value"": 0.1, ""tags"": [""Strike""] }", "no tags or skill")]
        [TestCase(@"{ ""to"": ""party"", ""stat"": ""attackPower"", ""kind"": ""increased"", ""value"": 0.1, ""while"": ""crit"" }", "'while' holds on a gauge")]
        public void AWrongLineIsRefusedOnLoad(string json, string problem)
        {
            var error = Assert.Throws<ContentException>(() => Line(json));
            Assert.That(error!.Message, Does.Contain(problem));
        }

        [Test]
        public void EveryPartyBuffStatIsAStatWithAMechanic()
        {
            var known = typeof(CombatStats).GetFields().Where(field => field.FieldType == typeof(StatId)).Select(field => ((StatId)field.GetValue(null)!).Name).ToHashSet();
            Assert.That(Rules.Stats.Count, Is.GreaterThanOrEqualTo(30), "about 35 kinds of party buffs instead of 7");
            foreach (var stat in Rules.Stats)
                Assert.That(known, Does.Contain(stat.Stat.Name), $"party buff stat {stat.Stat} has a mechanic");
        }

        [Test]
        public void APartyLineBuffsTheHeroAndIsOfferedToAllies()
        {
            var (buffs, _, hero) = Giver();
            hero.Add(Line(@"{ ""to"": ""party"", ""stat"": ""attackPower"", ""kind"": ""increased"", ""value"": 0.1 }"));
            var damage = IndexOf("attackPower", ModifierKind.Increased);
            var before = hero.Value(CombatStats.Damage, 1);

            buffs.Tick();

            Assert.That(buffs.CoopApplied(damage), Is.EqualTo(0.1).Within(1e-9));
            Assert.That(buffs.CoopOffered(damage), Is.EqualTo(0.1).Within(1e-9));
            Assert.That(hero.Value(CombatStats.Damage, 1) - before, Is.EqualTo(0.1).Within(1e-9));
        }

        [Test]
        public void GearAndArtifactPartyLinesAreOfferedToAllies()
        {
            Assert.That(TestWorld.Data.Items.TryGetAffix("ring_party_gold", out var affix), Is.True);
            Assert.That(CoopLine.TryGet(affix.Stats[0].Stat, out var line), Is.True);
            Assert.That((line.To, line.Target), Is.EqualTo((CoopTarget.Party, CombatStats.Gold)));
            var (buffs, _, hero) = Giver();
            hero.Add(new Modifier(affix.Stats[0].Stat, ModifierKind.Flat, 0.1));
            Assert.That(TestWorld.Data.Artifacts.TryGet("brotherhood_banner", out var banner), Is.True);
            hero.AddRange(banner.PerLevel);

            buffs.Tick();

            Assert.That(buffs.CoopOffered(IndexOf("gold", ModifierKind.Increased)), Is.EqualTo(0.1).Within(1e-9));
            Assert.That(buffs.CoopOffered(IndexOf("attackPower", ModifierKind.Increased)), Is.EqualTo(0.04).Within(1e-9));
        }

        [Test]
        public void AnAlliesLineIsSilentForTheHeroAndASelfLineIsNotOffered()
        {
            var (buffs, _, hero) = Giver();
            hero.Add(Line(@"{ ""to"": ""allies"", ""stat"": ""critChance"", ""kind"": ""flat"", ""value"": 0.02 }"));
            hero.Add(Line(@"{ ""to"": ""self"", ""stat"": ""gold"", ""kind"": ""increased"", ""value"": 0.05 }"));

            buffs.Tick();

            Assert.That(buffs.CoopApplied(IndexOf("critChance", ModifierKind.Flat)), Is.Zero);
            Assert.That(buffs.CoopOffered(IndexOf("critChance", ModifierKind.Flat)), Is.EqualTo(0.02).Within(1e-9));
            Assert.That(buffs.CoopApplied(IndexOf("gold", ModifierKind.Increased)), Is.EqualTo(0.05).Within(1e-9));
            Assert.That(buffs.CoopOffered(IndexOf("gold", ModifierKind.Increased)), Is.Zero);
        }

        [TestCase(PartyGiveMode.Together, 0.1, 0.1)]
        [TestCase(PartyGiveMode.Support, 0.0, 0.15)]
        [TestCase(PartyGiveMode.Solo, 0.1, 0.05)]
        public void ThePathModeSplitsThePartyLine(PartyGiveMode mode, double self, double allies)
        {
            var (buffs, _, hero) = Giver(mode: mode);
            hero.Add(Line(@"{ ""to"": ""party"", ""stat"": ""attackPower"", ""kind"": ""increased"", ""value"": 0.1 }"));
            var damage = IndexOf("attackPower", ModifierKind.Increased);

            buffs.Tick();

            Assert.That(buffs.CoopApplied(damage), Is.EqualTo(self).Within(1e-9));
            Assert.That(buffs.CoopOffered(damage), Is.EqualTo(allies).Within(1e-9));
        }

        [Test]
        public void LevelsOfOneLineAddUpAndTheConditionGatesThem()
        {
            var (buffs, _, hero) = Giver();
            var json = @"{ ""to"": ""party"", ""stat"": ""gold"", ""kind"": ""increased"", ""value"": 0.03, ""when"": ""boss"" }";
            hero.Add(Line(json));
            hero.Add(Line(json));
            var gold = IndexOf("gold", ModifierKind.Increased);

            buffs.Tick();
            Assert.That(buffs.CoopOffered(gold), Is.Zero, "no boss, no buff");

            hero.SetConditions(ModifierConditions.Bit(ModifierCondition.Boss));
            buffs.Tick();
            Assert.That(buffs.CoopOffered(gold), Is.EqualTo(0.06).Within(1e-9));
        }

        [Test]
        public void APerLineCountsWholeStepsOfTheGaugeUpToTheCap()
        {
            var (buffs, state, hero) = Giver();
            hero.Add(Line(@"{ ""to"": ""party"", ""stat"": ""critMultiplier"", ""kind"": ""flat"", ""value"": 0.05, ""per"": { ""signal"": ""resourceFill"", ""step"": 0.25, ""cap"": 3 } }"));
            var crit = IndexOf("critMultiplier", ModifierKind.Flat);

            state.ResourceFill = 0.6;
            buffs.Tick();
            Assert.That(buffs.CoopOffered(crit), Is.EqualTo(0.1).Within(1e-9), "two whole steps");

            state.ResourceFill = 1;
            buffs.Tick();
            Assert.That(buffs.CoopOffered(crit), Is.EqualTo(0.15).Within(1e-9), "the cap is three steps");
        }

        [Test]
        public void AnAfterLineStacksOnEventsAndFadesWhenTheTimeRunsOut()
        {
            var (buffs, state, hero) = Giver();
            hero.Add(Line(@"{ ""to"": ""party"", ""stat"": ""skillCooldown"", ""kind"": ""increased"", ""value"": 0.03, ""after"": { ""signal"": ""crit"", ""seconds"": 1, ""stacks"": 3 } }"));
            var cooldown = IndexOf("skillCooldown", ModifierKind.Increased);
            buffs.Tick();
            Assert.That(buffs.CoopOffered(cooldown), Is.Zero, "no crit yet");

            state.Crits += 2;
            buffs.Tick();
            Assert.That(buffs.CoopOffered(cooldown), Is.EqualTo(0.06).Within(1e-9));
            Assert.That(hero.Value(CombatStats.SkillCooldown, 1), Is.EqualTo(0.94).Within(1e-9), "faster cooldowns are a negative increase");

            state.Crits += 5;
            buffs.Tick();
            Assert.That(buffs.CoopOffered(cooldown), Is.EqualTo(0.09).Within(1e-9), "three stacks at most");

            for (var i = 0; i < GameLoop.TicksPerSecond; i++)
                buffs.Tick();
            Assert.That(buffs.CoopOffered(cooldown), Is.Zero, "the stacks fade after a second without a crit");
        }

        [Test]
        public void TheVowOfSilenceMakesTheEnemyTakeMoreDamageAfterEachSilentWhisper()
        {
            var (buffs, state, hero) = Giver(HeroClass.Whisperer, PartyGiveMode.Support);
            Assert.That(TestWorld.Data.Ascendancy.TryGet("whisperer_trait_3", out var vow), Is.True);
            foreach (var modifier in vow.Trait!.Levels[0])
                hero.Add(modifier);
            buffs.Tick();
            Assert.That(buffs.Giver.EnemyDamageTaken, Is.Zero, "no whisper yet");

            state.Whisperer.HushesSent += 7;
            buffs.Tick();

            Assert.That(buffs.Giver.EnemyDamageTaken, Is.EqualTo(0.03 * 5 * TestWorld.Data.Party.Buffs.Coop.ModePower(PartyGiveMode.Support)).Within(1e-9), "five stacks at most");
        }

        [Test]
        public void DebuffEffectMakesTheEnemyLinesStronger()
        {
            var (buffs, state, hero) = Giver(HeroClass.Whisperer, PartyGiveMode.Support);
            Assert.That(TestWorld.Data.Ascendancy.TryGet("whisperer_trait_3", out var vow), Is.True);
            foreach (var modifier in vow.Trait!.Levels[0])
                hero.Add(modifier);
            hero.Add(new Modifier(CombatStats.EnemyDebuffEffect, ModifierKind.Increased, 0.2));
            buffs.Tick();
            state.Whisperer.HushesSent += 7;
            buffs.Tick();

            Assert.That(buffs.Giver.EnemyDamageTaken, Is.EqualTo(0.03 * 5 * 1.2 * TestWorld.Data.Party.Buffs.Coop.ModePower(PartyGiveMode.Support)).Within(1e-9));
        }

        [Test]
        public void AShareLineGivesAPartOfTheOwnBonus()
        {
            var (buffs, _, hero) = Giver();
            hero.Add(new Modifier(CombatStats.ResourceGain, ModifierKind.Increased, 0.4));
            hero.Add(Line(@"{ ""to"": ""allies"", ""stat"": ""resourceGain"", ""kind"": ""increased"", ""value"": 0.25, ""share"": { ""stat"": ""resourceGain"", ""kind"": ""increased"" } }"));

            buffs.Tick();

            Assert.That(buffs.CoopOffered(IndexOf("resourceGain", ModifierKind.Increased)), Is.EqualTo(0.1).Within(1e-9));
        }

        [Test]
        public void TheSameClassTakesTheBiggerBuffAndDifferentClassesAddUpToTheCap()
        {
            var (buffs, _, hero) = Giver(HeroClass.Minstrel);
            hero.Add(Line(@"{ ""to"": ""self"", ""stat"": ""critChance"", ""kind"": ""flat"", ""value"": 0.05 }"));
            var crit = IndexOf("critChance", ModifierKind.Flat);
            void Ally(int slot, HeroClass heroClass, double value)
            {
                var ally = buffs.Ally(slot);
                ally.Present = true;
                ally.HeroClass = (int)heroClass;
                ally.Coop[crit] = value;
                buffs.MarkChanged();
            }

            Ally(0, HeroClass.Minstrel, 0.08);
            buffs.Tick();
            Assert.That(buffs.CoopApplied(crit), Is.EqualTo(0.08).Within(1e-9), "two Minstrels: the bigger one");

            Ally(1, HeroClass.Windshot, 0.07);
            buffs.Tick();
            Assert.That(buffs.CoopApplied(crit), Is.EqualTo(0.15).Within(1e-9), "a Windshot adds up");

            Ally(2, HeroClass.Duelist, 0.2);
            buffs.Tick();
            Assert.That(buffs.CoopApplied(crit), Is.EqualTo(Rules.Stats[crit].Cap).Within(1e-9), "the sum stops at the cap");

            buffs.ClearAlly(2);
            buffs.Tick();
            Assert.That(buffs.CoopApplied(crit), Is.EqualTo(0.15).Within(1e-9), "a leaving ally takes the buff away");
        }

        [Test]
        public void AnEnemyLineInSoloTurnsIntoTheOwnDamageAndASlowerTimer()
        {
            var (buffs, state, hero) = Giver();
            hero.Add(Line(@"{ ""to"": ""enemy"", ""stat"": ""damageTaken"", ""kind"": ""flat"", ""value"": 0.2 }"));
            hero.Add(Line(@"{ ""to"": ""enemy"", ""stat"": ""bossTimerSlow"", ""kind"": ""flat"", ""value"": 0.3 }"));

            buffs.Tick();

            Assert.That(hero.Value(CombatStats.Damage, 1), Is.EqualTo(1.2).Within(1e-9));
            Assert.That(hero.Value(CombatStats.BossTimerSpeed, 1), Is.EqualTo(0.7).Within(1e-9));
            Assert.That(state.TraitEnemyEffects.Count, Is.Zero, "nothing goes to a host in solo");
        }

        [Test]
        public void AnOfferWithBuffsSurvivesTheWireAndTooManyBuffsAreRefused()
        {
            var offer = new PartyOfferMessage { Slot = 2 };
            for (var i = 0; i < CoopRules.MaxOffered; i++)
                Assert.That(offer.AddCoop(i, 0.01 * (i + 1)), Is.True);
            Assert.That(offer.AddCoop(20, 0.5), Is.False, "the offer holds sixteen buffs");
            var buffer = new byte[PartyCodec.MaxPacketBytes];
            var length = PartyCodec.Encode(buffer, offer);

            Assert.That(PartyCodec.TryDecode(buffer, length, out PartyOfferMessage read), Is.True);
            Assert.That(read.CoopCount, Is.EqualTo(CoopRules.MaxOffered));
            Assert.That(read.Coop(15).Stat, Is.EqualTo(15));
            Assert.That(read.Coop(15).Value, Is.EqualTo(0.16f));

            buffer[length - CoopRules.MaxOffered * 5 - 1] = CoopRules.MaxOffered + 1;
            Assert.That(PartyCodec.TryDecode(buffer, length, out PartyOfferMessage _), Is.False);
        }

        [Test]
        public void AnAllyGetsTheBuffOverTheNetworkAndTheHostWeakensTheEnemy()
        {
            var network = new LoopbackNetwork(5, 20, 60, 0, 4096);
            var host = new PartyHost(new PartyEncounter(TestWorld.Data.Party, TestWorld.Data.Stages, 3), network.Connect(HostId), TestWorld.Data.Party);
            var members = new[] { Member(network, 10, HeroClass.Lanternbearer), Member(network, 11, HeroClass.Minstrel) };
            var giver = members[0].Loop;
            var friend = members[1].Loop;
            giver.Hero.Add(Line(@"{ ""to"": ""allies"", ""stat"": ""gold"", ""kind"": ""increased"", ""value"": 0.2 }"));
            giver.Hero.Add(Line(@"{ ""to"": ""enemy"", ""stat"": ""damageTaken"", ""kind"": ""flat"", ""value"": 0.1 }"));
            for (var tick = 0; tick < 4 * GameLoop.TicksPerSecond; tick++)
            {
                network.Advance(100);
                foreach (var (loop, input, _) in members)
                {
                    input.PressKeys(1);
                    loop.Tick();
                    loop.Events.Clear();
                }
                host.Tick();
            }
            var gold = IndexOf("gold", ModifierKind.Increased);

            Assert.That(friend.PartyBuffs.CoopApplied(gold), Is.EqualTo(0.2).Within(1e-6), "the ally gets the gold");
            Assert.That(giver.PartyBuffs.CoopApplied(gold), Is.Zero, "an allies line skips the giver");
            Assert.That(host.Encounter.DamageTakenMultiplier, Is.EqualTo(1.1).Within(1e-6), "the shared enemy takes more damage from everyone");
            Assert.That(giver.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(friend.Hero.Value(CombatStats.Damage, 1)).Within(1e-9), "in a party the enemy line is not an own damage bonus");
        }

        private static (GameLoop Loop, FakeInputSource Input, PartyClient Client) Member(LoopbackNetwork network, ulong id, HeroClass heroClass)
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, TestWorld.WithAllSkillsOpen(new GameState { Stage = 5, HighestStage = 5, RecordStage = 5, Class = heroClass }));
            var client = new PartyClient(loop, network.Connect(id), HostId, TestWorld.Data.Party);
            client.Join();
            return (loop, input, client);
        }

        [Test]
        public void TheModeChangesOnlyAtTheForkOfLevel75()
        {
            string Trait(int level) => @"{ ""levels"": [ { ""modifiers"": [] }, { ""modifiers"": [] }, { ""modifiers"": [] }, { ""modifiers"": [] }, { ""modifiers"": [] } ],
                ""forks"": [ { ""level"": " + level + @", ""options"": [
                    { ""id"": ""a" + level + @""", ""name"": { ""ru"": ""А"", ""en"": ""A"" }, ""partyMode"": ""support"", ""modifiers"": [] },
                    { ""id"": ""b" + level + @""", ""name"": { ""ru"": ""Б"", ""en"": ""B"" }, ""modifiers"": [] } ] } ] }";

            var trait = TraitDef.Read(ContentNode.Root(Trait(75), "test.json"), new System.Collections.Generic.HashSet<string>());
            Assert.That(trait.Forks[0].Choices[0].PartyMode, Is.EqualTo(PartyGiveMode.Support));
            Assert.That(trait.Forks[0].Choices[1].PartyMode, Is.Null);
            var error = Assert.Throws<ContentException>(() => TraitDef.Read(ContentNode.Root(Trait(60), "test.json"), new System.Collections.Generic.HashSet<string>()));
            Assert.That(error!.Message, Does.Contain("only at the fork of level 75"));
        }

        [Test]
        public void ADoubleHitDealsTwiceTheDamage()
        {
            DamageOfOneHit(0, out var single);
            DamageOfOneHit(1, out var twice);
            Assert.That(twice / single, Is.EqualTo(2).Within(1e-6));
        }

        private static void DamageOfOneHit(double chance, out double damage)
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState { Stage = 1000, HighestStage = 1000, RecordStage = 1000 });
            loop.Hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            loop.Hero.Add(new Modifier(CombatStats.DoubleHitChance, ModifierKind.Flat, chance));
            input.PressKeys(1);
            loop.Tick();
            damage = loop.State.TotalDamage.ToDouble();
        }

        [Test]
        public void ALineReadsAsWhoGetsWhatAndWhen()
        {
            Texts.UseLanguage("ru");
            var party = Line(@"{ ""to"": ""party"", ""stat"": ""attackPower"", ""kind"": ""increased"", ""value"": 0.04, ""after"": { ""signal"": ""crit"", ""seconds"": 5, ""stacks"": 3 } }");
            var enemy = Line(@"{ ""to"": ""enemy"", ""stat"": ""damageTaken"", ""kind"": ""flat"", ""value"": 0.06 }");

            Assert.That(ModifierText.Describe(party), Is.EqualTo("себе и отряду: +4% сила атаки после события «крит» на 5 с, до 3 стаков"));
            Assert.That(ModifierText.Describe(enemy), Is.EqualTo("общий враг: +6% получаемый урон"));
        }

        [Test]
        public void EveryPathHasItsModeAndOneForkOf75ChangesIt()
        {
            var support = new[] { "alchemist_trait_3", "minstrel_trait_2", "lanternbearer_trait_2", "lanternbearer_trait_3", "whisperer_trait_3", "soulbinder_trait_3", "ghostcaptain_trait_3", "golemancer_trait_3", "clockwright_trait_3", "dowser_trait_3", "mothcaller_trait_2", "sandwalker_trait_2", "cartomancer_trait_3" };
            var solo = new[] { "runedancer_trait_2", "runedancer_trait_3", "duelist_trait_2", "duelist_trait_3", "windshot_trait_1", "windshot_trait_3", "shardcaller_trait_1", "shardcaller_trait_2", "whisperer_trait_2", "mothcaller_trait_3", "soulbinder_trait_1", "ghostcaptain_trait_2", "golemancer_trait_1" };
            var paths = TestWorld.Data.Ascendancy.Subclasses.Where(path => path.Trait != null).ToList();

            Assert.That(paths, Has.Count.EqualTo(54));
            Assert.That(paths.Count(path => path.PartyMode == PartyGiveMode.Support), Is.EqualTo(13));
            Assert.That(paths.Count(path => path.PartyMode == PartyGiveMode.Solo), Is.EqualTo(13));
            foreach (var path in paths)
            {
                var expected = support.Contains(path.Id) ? PartyGiveMode.Support : solo.Contains(path.Id) ? PartyGiveMode.Solo : PartyGiveMode.Together;
                Assert.That(path.PartyMode, Is.EqualTo(expected), path.Id);
                var changes = path.Trait!.Forks.SelectMany(fork => fork.Choices).Where(choice => choice.PartyMode != null).ToList();
                Assert.That(changes, Has.Count.EqualTo(1), path.Id);
                Assert.That(changes[0].PartyMode, Is.Not.EqualTo(path.PartyMode), path.Id);
            }
        }

        [Test]
        public void NoCharacterTreeKeepsTheOldPartyStats()
        {
            var old = new[] { "partyContribution", "partyDamage", "partyCritChance", "partyGold", "partyResource", "partyCritMultiplier", "partySkillCooldown", "partyGemDrop" };
            foreach (var tree in TestWorld.Data.ClassTrees.Values)
            {
                var stats = tree.Nodes.SelectMany(node => new[] { node.Template.First, node.Template.Second, node.Template.Third }).SelectMany(line => line.Modifiers)
                    .Concat(tree.Majors.SelectMany(slot => slot.Options).SelectMany(option => option.Modifiers)).ToList();
                Assert.That(stats.Where(modifier => old.Contains(modifier.Stat.Name) || modifier.When == ModifierCondition.InParty), Is.Empty, tree.Id);
                Assert.That(stats.Count(modifier => CoopLine.IsCoop(modifier.Stat)), Is.GreaterThanOrEqualTo(10), tree.Id);
            }
        }

        [Test]
        public void TickingPartyBuffsDoesNotAllocate()
        {
            var probeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (System.GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var (buffs, state, hero) = Giver();
            hero.Add(Line(@"{ ""to"": ""party"", ""stat"": ""attackPower"", ""kind"": ""increased"", ""value"": 0.03, ""after"": { ""signal"": ""crit"", ""seconds"": 0.5, ""stacks"": 4 } }"));
            hero.Add(Line(@"{ ""to"": ""allies"", ""stat"": ""gold"", ""kind"": ""increased"", ""value"": 0.03, ""per"": { ""signal"": ""resourceFill"", ""step"": 0.2, ""cap"": 5 } }"));
            for (var i = 0; i < 50; i++)
            {
                state.Crits++;
                state.ResourceFill = i % 6 * 0.2;
                buffs.Tick();
            }

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
            {
                if (i % 7 == 0)
                    state.Crits++;
                state.ResourceFill = i % 6 * 0.2;
                buffs.Tick();
            }

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
