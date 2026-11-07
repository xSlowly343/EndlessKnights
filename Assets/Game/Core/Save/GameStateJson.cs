using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Items;
using Game.Core.Content.Json;
using Game.Core.Content.Slates;
using Game.Core.Content.Tree;
using Game.Core.Content.Weapons;
using Game.Core.Math;
using Game.Core.Stats;
using Game.Core.Sim;
using Game.Core.Sim.Rift;

namespace Game.Core.Save
{
    public static class GameStateJson
    {
        private static readonly Dictionary<string, EnemyType> EnemyTypes = new Dictionary<string, EnemyType>
        {
            ["normal"] = EnemyType.Normal,
            ["boss"] = EnemyType.Boss,
            ["guardian"] = EnemyType.Guardian
        };

        public static JsonValue Write(GameState state)
        {
            var (rng0, rng1) = state.Rng.State;
            return Object(
                ("tick", JsonValue.Of(state.Tick)),
                ("rng", JsonValue.Array(new[] { SaveValues.Of(rng0), SaveValues.Of(rng1) })),
                ("hero", Object(
                    ("class", JsonValue.Of(HeroClassNames.Of(state.Class))),
                    ("classChosen", JsonValue.Of(state.ClassChosen)),
                    ("classChanges", JsonValue.Of(state.ClassChanges)),
                    ("level", JsonValue.Of(state.HeroLevel)),
                    ("strikeLevel", JsonValue.Of(state.StrikeLevel)),
                    ("xp", JsonValue.Of(state.HeroXp)),
                    ("focus", JsonValue.Of(state.Focus)),
                    ("focusMultiplier", JsonValue.Of(state.FocusMultiplier)),
                    ("resource", JsonValue.Of(state.Resource)),
                    ("keylessTicks", JsonValue.Of(state.KeylessTicks)),
                    ("idleTicks", JsonValue.Of(state.IdleTicks)),
                    ("macroRhythmTicks", JsonValue.Of(state.MacroRhythmTicks)),
                    ("combo", JsonValue.Of(state.Combo)),
                    ("strikeHits", JsonValue.Of(state.StrikeHits)),
                    ("comboIdleTicks", JsonValue.Of(state.ComboIdleTicks)),
                    ("moment", Object(
                        ("mouseIdleTicks", state.MouseIdleTicks < MomentTicksLimit ? JsonValue.Of(state.MouseIdleTicks) : JsonValue.Null()),
                        ("inputStreakTicks", JsonValue.Of(state.InputStreakTicks)),
                        ("afterFastKillTicks", JsonValue.Of(state.AfterFastKillTicks)),
                        ("afterCircleTicks", JsonValue.Of(state.AfterCircleTicks)),
                        ("afterFlickTicks", JsonValue.Of(state.AfterFlickTicks)))))),
                ("stage", Object(
                    ("current", JsonValue.Of(state.Stage)),
                    ("highest", JsonValue.Of(state.HighestStage)),
                    ("record", JsonValue.Of(state.RecordStage)),
                    ("kills", JsonValue.Of(state.KillsOnStage)),
                    ("bossTicksLeft", JsonValue.Of(state.BossTicksLeft)),
                    ("farming", JsonValue.Of(state.Farming)),
                    ("farmKills", JsonValue.Of(state.FarmKills)),
                    ("bossFailsInARow", JsonValue.Of(state.BossFailsInARow)),
                    ("bossBestFraction", JsonValue.Of(state.BossBestFraction)),
                    ("biome", OptionalText(state.Biome)),
                    ("previousBiome", OptionalText(state.PreviousBiome)),
                    ("biomeSinceTick", JsonValue.Of(state.BiomeSinceTick)),
                    ("biomeLeft", JsonValue.Array(LongItems(state.BiomeLeftTicks))),
                    ("biomeFresh", JsonValue.Of(state.BiomeFresh)))),
                ("enemy", Object(
                    ("id", state.Enemy == null ? JsonValue.Null() : JsonValue.Of(state.Enemy.Id)),
                    ("type", JsonValue.Of(SaveValues.NameOf(state.EnemyType, EnemyTypes))),
                    ("hp", SaveValues.Of(state.EnemyHp)),
                    ("maxHp", SaveValues.Of(state.EnemyMaxHp)),
                    ("wanderer", JsonValue.Of(state.WandererBoss)),
                    ("goldenTicks", JsonValue.Of(state.GoldenTicks)),
                    ("goldenNormalHp", SaveValues.Of(state.GoldenNormalHp)),
                    ("poison", JsonValue.Of(state.PoisonStacks)))),
                ("gold", Object(
                    ("wallet", SaveValues.Of(state.Gold)),
                    ("runEarned", SaveValues.Of(state.RunGoldEarned)))),
                ("recentDps", SaveValues.Of(state.RecentDps)),
                ("prestige", Object(
                    ("echo", SaveValues.Of(state.Echo)),
                    ("echoEarned", SaveValues.Of(state.EchoEarned)),
                    ("count", JsonValue.Of(state.Prestiges)),
                    ("stageRush", JsonValue.Of(state.StageRush)))),
                ("artifacts", JsonValue.Array(WriteArtifacts(state.Artifacts))),
                ("rift", WriteRift(state.Rift)),
                ("weapons", WriteWeapons(state.Weapons)),
                ("drops", WriteDrops(state.Drops)),
                ("quests", WriteQuests(state.Quests)),
                ("achievements", WriteAchievements(state.Achievements)),
                ("bestiary", WriteBestiary(state.Bestiary)),
                ("cosmetics", WriteCosmetics(state.Cosmetics)),
                ("seasonPass", WriteSeasonPass(state.SeasonPass)),
                ("tournament", WriteTournament(state.Tournament)),
                ("clanCamp", Object(
                    ("level", JsonValue.Of(state.ClanCamp.Level)),
                    ("feastUntilUnixMs", JsonValue.Of(state.ClanCamp.FeastUntilUnixMs.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                    ("answeredUnixMs", JsonValue.Of(state.ClanCamp.AnsweredUnixMs.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                    ("seenUnixMs", JsonValue.Of(state.ClanCamp.SeenUnixMs.ToString(System.Globalization.CultureInfo.InvariantCulture))))),
                ("awakening", WriteAwakening(state.Awakening)),
                ("conditionShares", WriteConditionShares(state.ConditionShares)),
                ("events", WriteEvents(state.Events)),
                ("peaceful", WritePeaceful(state.Peaceful)),
                ("camp", WriteCamp(state.Camp)),
                ("garden", WriteGarden(state.Garden)),
                ("alchemy", WriteAlchemy(state.Alchemy)),
                ("expeditions", WriteExpeditions(state.Expeditions)),
                ("town", WriteTown(state.Town)),
                ("fishing", WriteFishing(state.Fishing)),
                ("cooking", WriteCooking(state.Cooking)),
                ("museum", WriteMuseum(state.Museum)),
                ("decor", WriteDecor(state.Decor)),
                ("forge", Object(("work", JsonValue.Of(state.Forge.Work.ToString())), ("target", JsonValue.Of(state.Forge.Target.ToString())), ("slot", JsonValue.Of(state.Forge.Slot.ToString())), ("hammers", JsonValue.Of(state.Forge.Hammers)),
                    ("heat", JsonValue.Of(state.Forge.Heat)), ("smelted", JsonValue.Of(state.Forge.Smelted)), ("tempered", JsonValue.Of(state.Forge.Tempered)), ("salvageOre", JsonValue.Of(state.Forge.SalvageOre)), ("guestHammers", JsonValue.Of(state.Forge.GuestHammers)))),
                ("slates", WriteSlates(state.Slates)),
                ("chronicle", WriteChronicle(state.Chronicle)),
                ("atlas", WriteIds(state.Atlas)),
                ("resonance", WriteLevels(state.Resonance)),
                ("spirits", WriteLevels(state.Spirits)),
                ("gear", WriteWorn(state.Gear)),
                ("gatesCleared", WriteStages(state.GatesCleared)),
                ("mentorPerks", Object(
                    ("coopPoints", JsonValue.Of(state.MentorCoopPoints)),
                    ("coopHelp", JsonValue.Of(state.ServerCoopHelpPoints)),
                    ("earlyBookSlot", JsonValue.Of(state.EarlyBookSlot)))),
                ("clanBonus", Object(
                    ("nodes", WriteIds(state.ClanBonus.Nodes)),
                    ("answeredUnixMs", JsonValue.Of(state.ClanBonus.AnsweredUnixMs.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                    ("seenUnixMs", JsonValue.Of(state.ClanBonus.SeenUnixMs.ToString(System.Globalization.CultureInfo.InvariantCulture))))),
                ("dungeons", WriteDungeons(state.Dungeons)),
                ("setCollection", JsonValue.Array(WriteCollection(state.SetCollection))),
                ("uniquesFound", JsonValue.Array(WriteCollection(state.UniquesFound))),
                ("inputStats", Object(
                    ("keys", JsonValue.Of(state.TotalInput.Keys.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                    ("leftClicks", JsonValue.Of(state.TotalInput.LeftClicks.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                    ("rightClicks", JsonValue.Of(state.TotalInput.RightClicks.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                    ("middleClicks", JsonValue.Of(state.TotalInput.MiddleClicks.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                    ("mouseMeters", JsonValue.Of(state.TotalInput.MouseMeters)),
                    ("playSeconds", JsonValue.Of(state.TotalInput.PlaySeconds)),
                    ("idleSeconds", JsonValue.Of(state.TotalInput.IdleSeconds)),
                    ("startedUnixMs", JsonValue.Of(state.StartedUnixMs.ToString(System.Globalization.CultureInfo.InvariantCulture))))),
                ("skills", JsonValue.Array(WriteSkills(state))),
                ("ownedGems", JsonValue.Array(WriteOwnedGems(state))),
                ("legacyGems", JsonValue.Of(state.LegacyGems)),
                ("gemProgress", WriteGemProgress(state)),
                ("treePointsEarned", JsonValue.Of(state.TreePointsEarned)),
                ("classTree", WriteClassTree(state.ClassTree)),
                ("commonTrees", WriteCommonTrees(state.CommonTrees)),
                ("ascendancy", WriteAscendancy(state.Ascendancy)),
                ("presets", Object(
                    ("active", JsonValue.Of(state.ActivePreset)),
                    ("otherClassTree", WriteClassTree(state.OtherPreset.ClassTree)),
                    ("otherCommonTrees", WriteCommonTrees(state.OtherPreset.CommonTrees)),
                    ("otherSkills", JsonValue.Array(WritePresetSkills(state.OtherPreset))))),
                ("chainDamage", SaveValues.Of(state.ChainDamage)),
                ("queueDamage", JsonValue.Array(WriteQueueDamage(state))),
                ("tutorial", Object(
                    ("skipped", JsonValue.Of(state.TutorialSkipped)),
                    ("done", JsonValue.Array(WriteTutorialDone(state))))),
                ("traits", TraitStateJson.Write(state)),
                ("carry", Object(
                    ("keys", JsonValue.Of(state.KeyCarry)),
                    ("mousePx", JsonValue.Of(state.MouseCarryPx)))));
        }

        public static GameState Read(ContentNode node, EnemyCatalog enemies, GemCatalog gems, WeaponCatalog weapons, ItemCatalog? items = null, SlateRules? slates = null)
        {
            var rng = node.Field("rng").Items();
            if (rng.Count != 2)
                throw node.Field("rng").Error("expected two state words");
            var rng0 = SaveValues.ReadULong(rng[0]);
            var rng1 = SaveValues.ReadULong(rng[1]);
            if (rng0 == 0 && rng1 == 0)
                throw node.Field("rng").Error("state words must not both be zero");

            var hero = node.Field("hero");
            var stage = node.Field("stage");
            var enemy = node.Field("enemy");
            var gold = node.Field("gold");
            var carry = node.Field("carry");
            var state = new GameState
            {
                Tick = node.Field("tick").AsLong(0),
                Rng = Rng.FromState(rng0, rng1),
                Class = SaveValues.ReadEnum(hero.Field("class"), HeroClassNames.ByName),
                ClassChosen = !hero.HasField("classChosen") || hero.Field("classChosen").AsBool(),
                ClassChanges = hero.HasField("classChanges") ? hero.Field("classChanges").AsInt(0, 1_000_000) : 0,
                Focus = hero.Field("focus").AsNumber(0, 1e6),
                FocusMultiplier = hero.Field("focusMultiplier").AsNumber(0, 1e6),
                Resource = hero.Field("resource").AsNumber(0, 1e9),
                KeylessTicks = hero.Field("keylessTicks").AsLong(0),
                IdleTicks = hero.Field("idleTicks").AsLong(0),
                MacroRhythmTicks = hero.Field("macroRhythmTicks").AsLong(0),
                HeroLevel = hero.Field("level").AsInt(1),
                StrikeLevel = hero.Field("strikeLevel").AsInt(0),
                HeroXp = hero.Field("xp").AsNumber(0, 1e300),
                Combo = hero.Field("combo").AsInt(0),
                StrikeHits = hero.Field("strikeHits").AsLong(0),
                ComboIdleTicks = hero.Field("comboIdleTicks").AsLong(0),
                MouseIdleTicks = hero.HasField("moment") && !hero.Field("moment").Field("mouseIdleTicks").IsNull ? hero.Field("moment").Field("mouseIdleTicks").AsLong(0) : long.MaxValue / 2,
                InputStreakTicks = hero.HasField("moment") ? hero.Field("moment").Field("inputStreakTicks").AsLong(0) : 0,
                AfterFastKillTicks = hero.HasField("moment") ? hero.Field("moment").Field("afterFastKillTicks").AsInt(0) : 0,
                AfterCircleTicks = hero.HasField("moment") ? hero.Field("moment").Field("afterCircleTicks").AsInt(0) : 0,
                AfterFlickTicks = hero.HasField("moment") ? hero.Field("moment").Field("afterFlickTicks").AsInt(0) : 0,
                Stage = stage.Field("current").AsLong(1),
                HighestStage = stage.Field("highest").AsLong(1),
                RecordStage = stage.Field("record").AsLong(1),
                KillsOnStage = stage.Field("kills").AsInt(0),
                BossTicksLeft = stage.Field("bossTicksLeft").AsInt(0),
                Farming = stage.Field("farming").AsBool(),
                FarmKills = stage.Field("farmKills").AsInt(0),
                BossFailsInARow = stage.Field("bossFailsInARow").AsInt(0),
                BossBestFraction = stage.HasField("bossBestFraction") ? stage.Field("bossBestFraction").AsNumber(0, 1) : 0,
                Biome = ReadOptionalText(stage, "biome"),
                PreviousBiome = ReadOptionalText(stage, "previousBiome"),
                BiomeSinceTick = stage.HasField("biomeSinceTick") ? stage.Field("biomeSinceTick").AsLong(long.MinValue) : 0,
                Gold = SaveValues.ReadBigNumber(gold.Field("wallet")),
                RunGoldEarned = SaveValues.ReadBigNumber(gold.Field("runEarned")),
                RecentDps = SaveValues.ReadBigNumber(node.Field("recentDps")),
                Echo = SaveValues.ReadBigNumber(node.Field("prestige").Field("echo")),
                EchoEarned = SaveValues.ReadBigNumber(node.Field("prestige").Field("echoEarned")),
                Prestiges = node.Field("prestige").Field("count").AsInt(0),
                StageRush = node.Field("prestige").HasField("stageRush") ? node.Field("prestige").Field("stageRush").AsInt(0) : 0,
                ChainDamage = SaveValues.ReadBigNumber(node.Field("chainDamage")),
                KeyCarry = carry.Field("keys").AsNumber(0, 1e6),
                MouseCarryPx = carry.Field("mousePx").AsNumber(0, 1e9)
            };
            if (state.HighestStage < state.Stage)
                throw stage.Field("highest").Error($"must not be lower than current ({state.Stage})");

            state.SpawnEnemy(ReadEnemyDef(enemy.Field("id"), enemies), SaveValues.ReadEnum(enemy.Field("type"), EnemyTypes),
                SaveValues.ReadBigNumber(enemy.Field("maxHp")));
            state.EnemyHp = SaveValues.ReadBigNumber(enemy.Field("hp"));
            state.PoisonStacks = enemy.HasField("poison") ? enemy.Field("poison").AsNumber(0, 1e9) : 0;
            if (enemy.HasField("goldenTicks"))
            {
                state.WandererBoss = enemy.Field("wanderer").AsBool();
                state.GoldenTicks = enemy.Field("goldenTicks").AsInt(0, 100000);
                state.GoldenNormalHp = SaveValues.ReadBigNumber(enemy.Field("goldenNormalHp"));
            }
            ReadSkills(node.Field("skills"), gems, state);
            foreach (var id in node.Field("ownedGems").Items())
            {
                if (gems.TryGet(id.AsString(), out var gem))
                    state.OwnedGems.Add(gem);
            }
            state.LegacyGems = node.HasField("legacyGems") && node.Field("legacyGems").AsBool();
            state.TreePointsEarned = node.Field("treePointsEarned").AsInt(0, 1_000_000);
            state.ClassTree = ReadClassTree(node.Field("classTree"));
            state.CommonTrees = ReadCommonTrees(node.Field("commonTrees"));
            state.Ascendancy = ReadAscendancy(node.Field("ascendancy"));
            ReadTutorial(node, state);
            ReadQueueDamage(node, state);
            var input = node.Field("inputStats");
            state.TotalInput.Keys = ReadCount(input.Field("keys"));
            state.TotalInput.LeftClicks = ReadCount(input.Field("leftClicks"));
            state.TotalInput.RightClicks = ReadCount(input.Field("rightClicks"));
            state.TotalInput.MiddleClicks = ReadCount(input.Field("middleClicks"));
            state.TotalInput.MouseMeters = input.Field("mouseMeters").AsNumber(0, 1e15);
            if (input.HasField("playSeconds"))
                state.TotalInput.PlaySeconds = input.Field("playSeconds").AsNumber(0, 1e12);
            if (input.HasField("idleSeconds"))
                state.TotalInput.IdleSeconds = input.Field("idleSeconds").AsNumber(0, 1e12);
            if (input.HasField("startedUnixMs"))
                state.StartedUnixMs = (long)ReadCount(input.Field("startedUnixMs"));
            var presets = node.Field("presets");
            state.ActivePreset = presets.Field("active").AsInt(0, 1);
            state.OtherPreset = new BuildPreset(ReadClassTree(presets.Field("otherClassTree")))
            {
                CommonTrees = ReadCommonTrees(presets.Field("otherCommonTrees"))
            };
            foreach (var item in presets.Field("otherSkills").Items())
            {
                if (!gems.TryGet(item.Field("gem").AsString(), out var skill) || skill.Skill == null)
                    continue;
                state.OtherPreset.Roles.Add(ReadRole(item, state.OtherPreset.Skills.Count) ?? (SkillLoadout.IsAura(skill) ? SkillSlotRole.Aura : SkillSlotRole.Auto));
                state.OtherPreset.Skills.Add(skill);
                var supports = new GemDef?[SkillSlot.SocketCapacity];
                ReadSupports(item, gems, supports);
                state.OtherPreset.Supports.Add(supports);
            }
            ReadArtifacts(node.Field("artifacts"), state.Artifacts);
            if (node.HasField("rift"))
                ReadRift(node.Field("rift"), state.Rift);
            if (node.HasField("weapons"))
                ReadWeapons(node.Field("weapons"), weapons, items, state.Weapons);
            if (node.HasField("drops"))
                ReadDrops(node.Field("drops"), state.Drops, items);
            if (node.HasField("quests"))
                ReadQuests(node.Field("quests"), state.Quests);
            if (node.HasField("achievements"))
                ReadAchievements(node.Field("achievements"), state.Achievements);
            if (node.HasField("bestiary"))
                ReadBestiary(node.Field("bestiary"), state.Bestiary);
            if (node.HasField("cosmetics"))
                ReadCosmetics(node.Field("cosmetics"), state.Cosmetics);
            if (node.HasField("alchemy"))
                ReadAlchemy(node.Field("alchemy"), state.Alchemy);
            if (node.HasField("expeditions"))
                ReadExpeditions(node.Field("expeditions"), state.Expeditions);
            if (node.HasField("town"))
                ReadTown(node.Field("town"), state.Town);
            if (node.HasField("fishing"))
                ReadFishing(node.Field("fishing"), state.Fishing);
            if (node.HasField("cooking"))
                ReadCooking(node.Field("cooking"), state.Cooking);
            if (node.HasField("decor"))
                ReadDecor(node.Field("decor"), state.Decor);
            if (node.HasField("museum"))
            {
                var museum = node.Field("museum");
                foreach (var item in museum.Field("done").Items())
                    state.Museum.Done.Add(item.AsString());
                foreach (var item in museum.Field("shelf").Items())
                    state.Museum.Shelf.Add(item.AsString());
                state.Museum.Title = ReadOptionalText(museum, "title");
            }
            if (node.HasField("forge"))
            {
                var forge = node.Field("forge");
                state.Forge.Work = forge.HasField("work") && System.Enum.TryParse<ForgeWork>(forge.Field("work").AsString(), out var work) ? work : ForgeWork.Temper;
                state.Forge.Target = System.Enum.TryParse<ForgeTarget>(forge.Field("target").AsString(), out var target) ? target : ForgeTarget.None;
                state.Forge.Slot = System.Enum.TryParse<GearSlot>(forge.Field("slot").AsString(), out var slot) ? slot : GearSlot.Helmet;
                state.Forge.Hammers = forge.Field("hammers").AsNumber(0, double.MaxValue);
                state.Forge.Heat = forge.Field("heat").AsNumber(0, 1);
                state.Forge.Smelted = node.Field("forge").Field("smelted").AsLong(0);
                state.Forge.Tempered = node.Field("forge").Field("tempered").AsLong(0);
                state.Forge.SalvageOre = forge.HasField("salvageOre") ? forge.Field("salvageOre").AsLong(0) : 0;
                state.Forge.GuestHammers = forge.HasField("guestHammers") ? forge.Field("guestHammers").AsInt(0) : 0;
            }
            if (node.HasField("garden"))
                ReadGarden(node.Field("garden"), state.Garden);
            if (node.HasField("camp"))
                ReadCamp(node.Field("camp"), state.Camp);
            if (node.HasField("peaceful"))
                ReadPeaceful(node.Field("peaceful"), state.Peaceful);
            if (node.HasField("events"))
                ReadEvents(node.Field("events"), state.Events);
            if (node.HasField("conditionShares"))
                ReadConditionShares(node.Field("conditionShares"), state.ConditionShares);
            if (node.HasField("awakening"))
                ReadAwakening(node.Field("awakening"), state.Awakening);
            if (node.HasField("seasonPass"))
                ReadSeasonPass(node.Field("seasonPass"), state.SeasonPass);
            if (node.HasField("tournament"))
                ReadTournament(node.Field("tournament"), state.Tournament);
            if (node.HasField("clanCamp"))
                ReadClanCamp(node.Field("clanCamp"), state.ClanCamp);
            if (node.HasField("slates") && slates != null)
                ReadSlates(node.Field("slates"), slates, state.Slates);
            if (node.HasField("chronicle"))
                ReadChronicle(node.Field("chronicle"), state.Chronicle);
            if (node.HasField("atlas"))
                ReadIds(node.Field("atlas"), state.Atlas);
            if (node.HasField("resonance"))
                ReadLevels(node.Field("resonance"), state.Resonance);
            if (node.HasField("spirits"))
                ReadLevels(node.Field("spirits"), state.Spirits);
            if (node.HasField("gear") && items != null)
                ReadWorn(node.Field("gear"), items, state.Gear);
            if (node.HasField("dungeons"))
                ReadDungeons(node.Field("dungeons"), state.Dungeons);
            if (node.HasField("gatesCleared"))
            {
                foreach (var item in node.Field("gatesCleared").Items())
                    state.GatesCleared.Add(item.AsLong(1));
            }
            if (node.HasField("mentorPerks"))
            {
                var perks = node.Field("mentorPerks");
                state.MentorCoopPoints = perks.Field("coopPoints").AsInt(0, 100);
                state.EarlyBookSlot = perks.Field("earlyBookSlot").AsBool();
                if (perks.HasField("coopHelp"))
                    state.ServerCoopHelpPoints = perks.Field("coopHelp").AsInt(0, 100);
            }
            if (node.HasField("clanBonus"))
                ReadClanBonus(node.Field("clanBonus"), state.ClanBonus);
            if (node.HasField("setCollection"))
            {
                foreach (var key in node.Field("setCollection").Items())
                    state.SetCollection.Add(key.AsString());
            }
            ReadBiomeLeft(node.Field("stage"), state);
            if (node.HasField("uniquesFound"))
            {
                foreach (var key in node.Field("uniquesFound").Items())
                    state.UniquesFound.Add(key.AsString());
            }
            if (node.HasField("gemProgress"))
                ReadGemProgress(node.Field("gemProgress"), gems, state);
            else
                state.Drops.Rng = new Rng(rng0 ^ rng1 ^ DropState.SeedSalt);
            if (node.HasField("traits"))
                TraitStateJson.Read(node.Field("traits"), state, enemies);
            return state;
        }

        private static JsonValue WriteLevels(Dictionary<string, int> levels)
        {
            var ids = new List<string>(levels.Keys);
            ids.Sort(System.StringComparer.Ordinal);
            var items = new JsonValue[ids.Count];
            for (var i = 0; i < items.Length; i++)
                items[i] = Object(("id", JsonValue.Of(ids[i])), ("level", JsonValue.Of(levels[ids[i]])));
            return JsonValue.Array(items);
        }

        private static void ReadLevels(ContentNode node, Dictionary<string, int> levels)
        {
            foreach (var item in node.Items())
                levels[item.Field("id").AsString()] = item.Field("level").AsInt(0, 1000);
        }

        private static JsonValue WriteIds(List<string> ids)
        {
            var items = new JsonValue[ids.Count];
            for (var i = 0; i < items.Length; i++)
                items[i] = JsonValue.Of(ids[i]);
            return JsonValue.Array(items);
        }

        private static void ReadIds(ContentNode node, List<string> ids)
        {
            foreach (var item in node.Items())
            {
                var id = item.AsString();
                if (!ids.Contains(id))
                    ids.Add(id);
            }
        }

        private static void ReadClanBonus(ContentNode node, Party.ClanBonusState bonus)
        {
            ReadIds(node.Field("nodes"), bonus.Nodes);
            bonus.AnsweredUnixMs = (long)ReadCount(node.Field("answeredUnixMs"));
            bonus.SeenUnixMs = (long)ReadCount(node.Field("seenUnixMs"));
        }

        private static JsonValue WriteChronicle(ChronicleState chronicle)
        {
            var completed = new JsonValue[chronicle.Completed.Count];
            for (var i = 0; i < completed.Length; i++)
                completed[i] = JsonValue.Of(chronicle.Completed[i]);
            return Object(
                ("active", chronicle.Active == null ? JsonValue.Null() : JsonValue.Of(chronicle.Active)),
                ("activePrestige", JsonValue.Of(chronicle.ActivePrestige)),
                ("completed", JsonValue.Array(completed)),
                ("weekly", JsonValue.Of(chronicle.Weekly)),
                ("week", JsonValue.Of(chronicle.Week)),
                ("weeklyBest", WriteWeeklyBest(chronicle.WeeklyBest)),
                ("weeklyUnsent", WriteIdList(chronicle.WeeklyUnsent)));
        }

        private static JsonValue WriteWeeklyBest(Dictionary<string, long> best)
        {
            var items = new List<JsonValue>(best.Count);
            foreach (var pair in best)
                items.Add(Object(("id", JsonValue.Of(pair.Key)), ("stage", JsonValue.Of(pair.Value))));
            return JsonValue.Array(items.ToArray());
        }

        private static JsonValue WriteIdList(List<string> ids)
        {
            var items = new JsonValue[ids.Count];
            for (var i = 0; i < items.Length; i++)
                items[i] = JsonValue.Of(ids[i]);
            return JsonValue.Array(items);
        }

        private static void ReadChronicle(ContentNode node, ChronicleState chronicle)
        {
            var active = node.Field("active");
            chronicle.Active = active.IsNull ? null : active.AsString();
            chronicle.ActivePrestige = node.Field("activePrestige").AsInt(0);
            foreach (var item in node.Field("completed").Items())
            {
                var id = item.AsString();
                if (!chronicle.Completed.Contains(id))
                    chronicle.Completed.Add(id);
            }
            if (!node.HasField("week"))
                return;
            chronicle.Weekly = node.Field("weekly").AsBool();
            chronicle.Week = node.Field("week").AsLong();
            foreach (var item in node.Field("weeklyBest").Items())
                chronicle.WeeklyBest[item.Field("id").AsString()] = item.Field("stage").AsLong(0);
            foreach (var item in node.Field("weeklyUnsent").Items())
            {
                var id = item.AsString();
                if (!chronicle.WeeklyUnsent.Contains(id))
                    chronicle.WeeklyUnsent.Add(id);
            }
        }

        private static JsonValue WriteSlates(SlateState slates)
        {
            var (rng0, rng1) = slates.Rng.State;
            var bag = new JsonValue[slates.Bag.Count];
            for (var i = 0; i < bag.Length; i++)
                bag[i] = WriteSlate(slates.Bag[i]);
            var board = new JsonValue[slates.Board.Count];
            for (var i = 0; i < board.Length; i++)
            {
                var placement = slates.Board[i];
                board[i] = Object(("slate", WriteSlate(placement.Slate)), ("x", JsonValue.Of(placement.X)), ("y", JsonValue.Of(placement.Y)),
                    ("rotation", JsonValue.Of(placement.Rotation)));
            }
            return Object(
                ("rng", JsonValue.Array(new[] { SaveValues.Of(rng0), SaveValues.Of(rng1) })),
                ("found", JsonValue.Of(slates.Found)),
                ("bag", JsonValue.Array(bag)),
                ("board", JsonValue.Array(board)));
        }

        private static JsonValue WriteSlate(SlateItem slate)
        {
            var lines = new JsonValue[slate.Lines.Count];
            for (var i = 0; i < lines.Length; i++)
                lines[i] = JsonValue.Of(slate.Lines[i].Id);
            return Object(
                ("shape", JsonValue.Of(slate.Shape.Id)),
                ("theme", JsonValue.Of(slate.Theme.Id)),
                ("rarity", JsonValue.Of(SlateRarityNames.Of(slate.Rarity))),
                ("tempered", JsonValue.Of(slate.Tempered)),
                ("lines", JsonValue.Array(lines)));
        }

        private static void ReadSlates(ContentNode node, SlateRules rules, SlateState slates)
        {
            var rng = node.Field("rng").Items();
            if (rng.Count == 2)
            {
                var rng0 = SaveValues.ReadULong(rng[0]);
                var rng1 = SaveValues.ReadULong(rng[1]);
                if (rng0 != 0 || rng1 != 0)
                    slates.Rng = Rng.FromState(rng0, rng1);
            }
            slates.Found = node.Field("found").AsLong(0);
            foreach (var item in node.Field("bag").Items())
            {
                var slate = ReadSlate(item, rules);
                if (slate != null)
                    slates.Bag.Add(slate);
            }
            foreach (var item in node.Field("board").Items())
            {
                var slate = ReadSlate(item.Field("slate"), rules);
                if (slate != null)
                    slates.Board.Add(new SlatePlacement(slate, item.Field("x").AsInt(0, 64), item.Field("y").AsInt(0, 64), item.Field("rotation").AsInt(0, 3)));
            }
        }

        private static SlateItem? ReadSlate(ContentNode node, SlateRules rules)
        {
            if (!rules.TryGetShape(node.Field("shape").AsString(), out var shape) || !rules.TryGetTheme(node.Field("theme").AsString(), out var theme)
                || !SlateRarityNames.TryParse(node.Field("rarity").AsString(), out var rarity))
                return null;
            var lines = new List<SlateLine>();
            foreach (var id in node.Field("lines").Items())
            {
                if (rules.TryGetLine(id.AsString(), out var line))
                    lines.Add(line);
            }
            return new SlateItem(shape, theme, rarity, lines, node.Field("tempered").AsBool());
        }

        private static JsonValue WriteCosmetics(CosmeticState cosmetics)
        {
            var owned = new List<string>(cosmetics.Owned);
            owned.Sort(System.StringComparer.Ordinal);
            var keys = new List<string>(cosmetics.Equipped.Keys);
            keys.Sort(System.StringComparer.Ordinal);
            var equipped = new JsonValue[keys.Count];
            for (var i = 0; i < equipped.Length; i++)
                equipped[i] = Object(("key", JsonValue.Of(keys[i])), ("id", JsonValue.Of(cosmetics.Equipped[keys[i]])));
            return Object(("owned", WriteIdList(owned)), ("equipped", JsonValue.Array(equipped)));
        }

        private static void ReadCosmetics(ContentNode node, CosmeticState cosmetics)
        {
            foreach (var item in node.Field("owned").Items())
                cosmetics.Owned.Add(item.AsString());
            foreach (var item in node.Field("equipped").Items())
                cosmetics.Equipped[item.Field("key").AsString()] = item.Field("id").AsString();
        }

        private static JsonValue WritePotion(Potion? potion) =>
            potion == null ? JsonValue.Null() : Object(("recipe", JsonValue.Of(potion.Recipe)), ("fine", JsonValue.Of(potion.Fine)), ("unknown", JsonValue.Of(potion.Unknown)));

        private static Potion? ReadPotion(ContentNode node) =>
            node.IsNull ? null : new Potion { Recipe = node.Field("recipe").AsString(), Fine = node.Field("fine").AsBool(), Unknown = node.Field("unknown").AsBool() };

        private static JsonValue WriteAlchemy(AlchemyState alchemy)
        {
            var known = new List<string>(alchemy.Known);
            known.Sort(System.StringComparer.Ordinal);
            var potions = new JsonValue[alchemy.Potions.Count];
            for (var i = 0; i < potions.Length; i++)
                potions[i] = WritePotion(alchemy.Potions[i]);
            return Object(("known", WriteIdList(known)), ("brewing", WritePotion(alchemy.Brewing)), ("stirs", JsonValue.Of(alchemy.Stirs)),
                ("potions", JsonValue.Array(potions)), ("active", WritePotion(alchemy.Active)), ("activeTicksLeft", JsonValue.Of(alchemy.ActiveTicksLeft)));
        }

        private static void ReadAlchemy(ContentNode node, AlchemyState alchemy)
        {
            foreach (var item in node.Field("known").Items())
                alchemy.Known.Add(item.AsString());
            alchemy.Brewing = ReadPotion(node.Field("brewing"));
            alchemy.Stirs = node.Field("stirs").AsNumber(0, double.MaxValue);
            foreach (var item in node.Field("potions").Items())
                alchemy.Potions.Add(ReadPotion(item)!);
            alchemy.Active = ReadPotion(node.Field("active"));
            alchemy.ActiveTicksLeft = node.Field("activeTicksLeft").AsLong(0);
        }

        private static JsonValue WriteExpedition(Expedition expedition)
        {
            var items = new JsonValue[expedition.Items.Length];
            for (var i = 0; i < items.Length; i++)
                items[i] = JsonValue.Of(expedition.Items[i]);
            return Object(("companion", JsonValue.Of(expedition.Companion)), ("biome", JsonValue.Of(expedition.Biome)), ("hours", JsonValue.Of(expedition.Hours)),
                ("seconds", JsonValue.Of(expedition.Seconds)), ("planned", JsonValue.Of(expedition.Planned)), ("elapsed", JsonValue.Of(expedition.Elapsed)), ("letter", JsonValue.Of(expedition.Letter)),
                ("choice", JsonValue.Of(expedition.Choice)), ("waited", JsonValue.Of(expedition.Waited)), ("items", JsonValue.Array(items)),
                ("shards", JsonValue.Of(expedition.Shards)), ("rare", OptionalText(expedition.Rare)), ("done", JsonValue.Of(expedition.Done)));
        }

        private static Expedition ReadExpedition(ContentNode node)
        {
            var items = new List<long>();
            foreach (var item in node.Field("items").Items())
                items.Add(item.AsLong(0));
            return new Expedition
            {
                Companion = node.Field("companion").AsString(),
                Biome = node.Field("biome").AsString(),
                Hours = node.Field("hours").AsInt(1, 48),
                Seconds = node.Field("seconds").AsNumber(0, double.MaxValue),
                Planned = node.HasField("planned") ? node.Field("planned").AsNumber(0, double.MaxValue) : 0,
                Elapsed = node.Field("elapsed").AsNumber(0, double.MaxValue),
                Letter = node.Field("letter").AsInt(-1, 1000),
                Choice = node.Field("choice").AsBool(),
                Waited = node.Field("waited").AsBool(),
                Items = items.ToArray(),
                Shards = node.Field("shards").AsLong(0),
                Rare = ReadOptionalText(node, "rare"),
                Done = node.Field("done").AsBool()
            };
        }

        private static JsonValue WriteExpeditions(ExpeditionState expeditions)
        {
            var active = new JsonValue[expeditions.Active.Count];
            for (var i = 0; i < active.Length; i++)
                active[i] = WriteExpedition(expeditions.Active[i]);
            var reports = new JsonValue[expeditions.Reports.Count];
            for (var i = 0; i < reports.Length; i++)
                reports[i] = WriteExpedition(expeditions.Reports[i]);
            var finds = new List<string>(expeditions.Finds);
            finds.Sort(System.StringComparer.Ordinal);
            return Object(("active", JsonValue.Array(active)), ("reports", JsonValue.Array(reports)), ("returned", JsonValue.Of(expeditions.Returned)), ("finds", WriteIdList(finds)));
        }

        private static void ReadExpeditions(ContentNode node, ExpeditionState expeditions)
        {
            foreach (var item in node.Field("active").Items())
                expeditions.Active.Add(ReadExpedition(item));
            foreach (var item in node.Field("reports").Items())
                expeditions.Reports.Add(ReadExpedition(item));
            expeditions.Returned = node.Field("returned").AsLong(0);
            foreach (var item in node.Field("finds").Items())
                expeditions.Finds.Add(item.AsString());
        }

        private static JsonValue WriteTown(TownState town)
        {
            var orders = new JsonValue[town.Orders.Count];
            for (var i = 0; i < orders.Length; i++)
                orders[i] = Object(("request", JsonValue.Of(town.Orders[i].Request)), ("amount", JsonValue.Of(town.Orders[i].Amount)));
            var guilds = new List<string>(town.Reputation.Keys);
            guilds.Sort(System.StringComparer.Ordinal);
            var reputation = new JsonValue[guilds.Count];
            for (var i = 0; i < reputation.Length; i++)
                reputation[i] = Object(("guild", JsonValue.Of(guilds[i])), ("value", JsonValue.Of(town.Reputation[guilds[i]])));
            return Object(("day", JsonValue.Of(town.Day)), ("orders", JsonValue.Array(orders)), ("reputation", JsonValue.Array(reputation)), ("done", JsonValue.Of(town.Done)), ("redrawDay", JsonValue.Of(town.RedrawDay)));
        }

        private static void ReadTown(ContentNode node, TownState town)
        {
            town.Day = node.Field("day").AsLong(-1);
            foreach (var item in node.Field("orders").Items())
                town.Orders.Add(new TownOrder { Request = item.Field("request").AsString(), Amount = item.Field("amount").AsInt(1) });
            foreach (var item in node.Field("reputation").Items())
                town.Reputation[item.Field("guild").AsString()] = item.Field("value").AsLong(0);
            town.Done = node.Field("done").AsLong(0);
            town.RedrawDay = node.HasField("redrawDay") ? node.Field("redrawDay").AsLong(-1) : -1;
        }

        private static JsonValue WriteDecor(DecorState decor)
        {
            var owned = new List<string>(decor.Owned);
            owned.Sort(System.StringComparer.Ordinal);
            var placed = new JsonValue[decor.Placed.Count];
            for (var i = 0; i < placed.Length; i++)
                placed[i] = OptionalText(decor.Placed[i]);
            return Object(("owned", WriteIdList(owned)), ("placed", JsonValue.Array(placed)), ("theme", OptionalText(decor.Theme)),
                ("guestsWatered", JsonValue.Of(decor.GuestsWatered)), ("guestHammers", JsonValue.Of(decor.GuestHammers)), ("gifts", JsonValue.Of(decor.Gifts)));
        }

        private static void ReadDecor(ContentNode node, DecorState decor)
        {
            foreach (var item in node.Field("owned").Items())
                decor.Owned.Add(item.AsString());
            foreach (var item in node.Field("placed").Items())
                decor.Placed.Add(item.IsNull ? string.Empty : item.AsString());
            decor.Theme = ReadOptionalText(node, "theme");
            decor.GuestsWatered = node.Field("guestsWatered").AsLong(0);
            decor.GuestHammers = node.Field("guestHammers").AsLong(0);
            decor.Gifts = node.Field("gifts").AsLong(0);
        }

        private static JsonValue WriteMuseum(MuseumState museum)
        {
            var done = new List<string>(museum.Done);
            done.Sort(System.StringComparer.Ordinal);
            return Object(("done", WriteIdList(done)), ("shelf", WriteIdList(museum.Shelf)), ("title", OptionalText(museum.Title)));
        }

        private static JsonValue WriteCooking(CookingState cooking)
        {
            var book = new List<string>(cooking.Book);
            book.Sort(System.StringComparer.Ordinal);
            return Object(("cooking", OptionalText(cooking.Cooking)), ("chops", JsonValue.Of(cooking.Chops)), ("plates", JsonValue.Of(cooking.Plates)),
                ("meals", WriteIdList(cooking.Meals)), ("active", OptionalText(cooking.Active)), ("activeTicksLeft", JsonValue.Of(cooking.ActiveTicksLeft)),
                ("book", WriteIdList(book)), ("cooked", JsonValue.Of(cooking.Cooked)));
        }

        private static void ReadCooking(ContentNode node, CookingState cooking)
        {
            cooking.Cooking = ReadOptionalText(node, "cooking");
            cooking.Chops = node.Field("chops").AsNumber(0, double.MaxValue);
            cooking.Plates = node.Field("plates").AsNumber(0, double.MaxValue);
            foreach (var item in node.Field("meals").Items())
                cooking.Meals.Add(item.AsString());
            cooking.Active = ReadOptionalText(node, "active");
            cooking.ActiveTicksLeft = node.Field("activeTicksLeft").AsLong(0);
            foreach (var item in node.Field("book").Items())
                cooking.Book.Add(item.AsString());
            cooking.Cooked = node.Field("cooked").AsLong(0);
        }

        private static JsonValue WriteFishing(FishingState fishing)
        {
            var ids = new List<string>(fishing.Album.Keys);
            ids.Sort(System.StringComparer.Ordinal);
            var album = new JsonValue[ids.Count];
            for (var i = 0; i < album.Length; i++)
                album[i] = Object(("id", JsonValue.Of(ids[i])), ("count", JsonValue.Of(fishing.Album[ids[i]].Count)), ("bestCm", JsonValue.Of(fishing.Album[ids[i]].BestCm)));
            var legends = new List<string>(fishing.LegendTired.Keys);
            legends.Sort(System.StringComparer.Ordinal);
            var tired = new JsonValue[legends.Count];
            for (var i = 0; i < tired.Length; i++)
                tired[i] = Object(("id", JsonValue.Of(legends[i])), ("tired", JsonValue.Of(fishing.LegendTired[legends[i]])));
            return Object(("spot", OptionalText(fishing.Spot)), ("album", JsonValue.Array(album)), ("legendTired", JsonValue.Array(tired)),
                ("caught", JsonValue.Of(fishing.Caught)), ("missed", JsonValue.Of(fishing.Missed)), ("escaped", JsonValue.Of(fishing.Escaped)), ("bottles", JsonValue.Of(fishing.Bottles)));
        }

        private static void ReadFishing(ContentNode node, FishingState fishing)
        {
            fishing.Spot = ReadOptionalText(node, "spot");
            foreach (var item in node.Field("album").Items())
                fishing.Album[item.Field("id").AsString()] = new FishRecord { Count = item.Field("count").AsLong(0), BestCm = item.Field("bestCm").AsNumber(0, double.MaxValue) };
            foreach (var item in node.Field("legendTired").Items())
                fishing.LegendTired[item.Field("id").AsString()] = item.Field("tired").AsNumber(0, double.MaxValue);
            fishing.Caught = node.Field("caught").AsLong(0);
            fishing.Missed = node.Field("missed").AsLong(0);
            fishing.Escaped = node.Field("escaped").AsLong(0);
            fishing.Bottles = node.Field("bottles").AsLong(0);
        }

        private static JsonValue WriteGarden(GardenState garden)
        {
            var beds = new JsonValue[garden.Beds.Count];
            for (var i = 0; i < beds.Length; i++)
            {
                var bed = garden.Beds[i];
                beds[i] = Object(("plant", OptionalText(bed.Plant)), ("grown", JsonValue.Of(bed.Grown)), ("ready", JsonValue.Of(bed.Ready)));
            }
            var album = new List<string>(garden.Album);
            album.Sort(System.StringComparer.Ordinal);
            return Object(("beds", JsonValue.Array(beds)), ("harvests", JsonValue.Of(garden.Harvests)), ("album", WriteIdList(album)));
        }

        private static void ReadGarden(ContentNode node, GardenState garden)
        {
            foreach (var item in node.Field("beds").Items())
                garden.Beds.Add(new GardenBed { Plant = ReadOptionalText(item, "plant"), Grown = item.Field("grown").AsNumber(0, double.MaxValue), Ready = item.Field("ready").AsBool() });
            garden.Harvests = node.Field("harvests").AsLong(0);
            foreach (var item in node.Field("album").Items())
                garden.Album.Add(item.AsString());
        }

        private static JsonValue WriteCamp(CampState camp)
        {
            var guilds = new List<string>(camp.Guilds);
            guilds.Sort(System.StringComparer.Ordinal);
            var items = new List<string>(camp.Bag.Keys);
            items.Sort(System.StringComparer.Ordinal);
            var bag = new JsonValue[items.Count];
            for (var i = 0; i < bag.Length; i++)
                bag[i] = Object(("item", JsonValue.Of(items[i])), ("count", JsonValue.Of(camp.Bag[items[i]])));
            return Object(("tier", JsonValue.Of(camp.Tier)), ("guilds", WriteIdList(guilds)), ("bag", JsonValue.Array(bag)));
        }

        private static void ReadCamp(ContentNode node, CampState camp)
        {
            camp.Tier = node.Field("tier").AsInt(0, 100);
            foreach (var item in node.Field("guilds").Items())
                camp.Guilds.Add(item.AsString());
            foreach (var item in node.Field("bag").Items())
                camp.Bag[item.Field("item").AsString()] = item.Field("count").AsLong(0);
        }

        private static JsonValue WritePeaceful(PeacefulState peaceful)
        {
            var presets = new List<string>(peaceful.Presets);
            presets.Sort(System.StringComparer.Ordinal);
            var activities = new List<string>(peaceful.Work.Keys);
            activities.Sort(System.StringComparer.Ordinal);
            var work = new JsonValue[activities.Count];
            for (var i = 0; i < work.Length; i++)
            {
                var item = peaceful.Work[activities[i]];
                work[i] = Object(("activity", JsonValue.Of(activities[i])), ("keys", JsonValue.Of(item.Keys)), ("clicks", JsonValue.Of(item.Clicks)),
                    ("mouseMeters", JsonValue.Of(item.MouseMeters)), ("idleSeconds", JsonValue.Of(item.IdleSeconds)));
            }
            return Object(
                ("mode", JsonValue.Of(peaceful.Mode == PeacefulMode.Camp ? "camp" : "battle")),
                ("activity", OptionalText(peaceful.Activity)),
                ("presets", WriteIdList(presets)),
                ("scheduledBy", OptionalText(peaceful.ScheduledBy)),
                ("work", JsonValue.Array(work)),
                ("teaPause", JsonValue.Of(peaceful.TeaPause)),
                ("unsentActions", JsonValue.Of(peaceful.UnsentActions)),
                ("battleSeconds", JsonValue.Of(peaceful.BattleSeconds)),
                ("quietSeconds", JsonValue.Of(peaceful.QuietSeconds)),
                ("teaOffered", JsonValue.Of(peaceful.TeaOffered)));
        }

        private static void ReadPeaceful(ContentNode node, PeacefulState peaceful)
        {
            peaceful.Mode = node.Field("mode").AsString() == "camp" ? PeacefulMode.Camp : PeacefulMode.Battle;
            peaceful.Activity = ReadOptionalText(node, "activity");
            foreach (var item in node.Field("presets").Items())
                peaceful.Presets.Add(item.AsString());
            peaceful.ScheduledBy = ReadOptionalText(node, "scheduledBy");
            foreach (var item in node.Field("work").Items())
            {
                peaceful.Work[item.Field("activity").AsString()] = new PeacefulWork
                {
                    Keys = item.Field("keys").AsNumber(0, double.MaxValue),
                    Clicks = item.Field("clicks").AsNumber(0, double.MaxValue),
                    MouseMeters = item.Field("mouseMeters").AsNumber(0, double.MaxValue),
                    IdleSeconds = item.Field("idleSeconds").AsNumber(0, double.MaxValue)
                };
            }
            peaceful.TeaPause = node.Field("teaPause").AsBool();
            peaceful.UnsentActions = node.HasField("unsentActions") ? node.Field("unsentActions").AsLong(0) : 0;
            peaceful.BattleSeconds = node.Field("battleSeconds").AsNumber(0, double.MaxValue);
            peaceful.QuietSeconds = node.Field("quietSeconds").AsNumber(0, double.MaxValue);
            peaceful.TeaOffered = node.Field("teaOffered").AsBool();
        }

        private static JsonValue WriteEvents(EventState events)
        {
            var done = new List<string>(events.GoalsDone);
            done.Sort(System.StringComparer.Ordinal);
            var sent = new List<string>(events.GoalsSent);
            sent.Sort(System.StringComparer.Ordinal);
            return Object(
                ("window", OptionalText(events.WindowId)),
                ("unsentTokens", JsonValue.Of(events.UnsentTokens)),
                ("tokens", JsonValue.Of(events.Tokens)),
                ("guests", JsonValue.Of(events.Guests)),
                ("bossFights", JsonValue.Of(events.BossFights)),
                ("bossWins", JsonValue.Of(events.BossWins)),
                ("dailyQuests", JsonValue.Of(events.DailyQuests)),
                ("partyTicks", JsonValue.Of(events.PartyTicks)),
                ("spawns", JsonValue.Of(events.Spawns)),
                ("guest", JsonValue.Of(events.Guest)),
                ("bossDay", JsonValue.Of(events.BossDay == long.MinValue ? -1 : events.BossDay)),
                ("bossFightsToday", JsonValue.Of(events.BossFightsToday)),
                ("goalsDone", WriteIdList(done)),
                ("goalsSent", WriteIdList(sent)),
                ("fight", events.FightInside && events.FightHome != null ? Object(
                    ("home", WriteProgress(events.FightHome)),
                    ("stage", JsonValue.Of(events.FightStage)),
                    ("ticksLeft", JsonValue.Of(events.FightTicksLeft))) : JsonValue.Null()),
                ("unsentScore", JsonValue.Of(events.UnsentScore)),
                ("bestScore", JsonValue.Of(events.BestScore)));
        }

        private static void ReadEvents(ContentNode node, EventState events)
        {
            events.WindowId = ReadOptionalText(node, "window");
            events.UnsentTokens = node.Field("unsentTokens").AsLong(0);
            events.Tokens = node.Field("tokens").AsLong(0);
            events.Guests = node.Field("guests").AsLong(0);
            events.BossFights = node.Field("bossFights").AsLong(0);
            events.BossWins = node.Field("bossWins").AsLong(0);
            events.DailyQuests = node.Field("dailyQuests").AsLong(0);
            events.PartyTicks = node.Field("partyTicks").AsLong(0);
            events.Spawns = node.Field("spawns").AsLong(0);
            events.Guest = node.Field("guest").AsBool();
            var bossDay = node.Field("bossDay").AsLong(-1);
            events.BossDay = bossDay < 0 ? long.MinValue : bossDay;
            events.BossFightsToday = node.Field("bossFightsToday").AsInt(0);
            foreach (var item in node.Field("goalsDone").Items())
                events.GoalsDone.Add(item.AsString());
            foreach (var item in node.Field("goalsSent").Items())
                events.GoalsSent.Add(item.AsString());
            var fight = node.Field("fight");
            if (!fight.IsNull)
            {
                events.FightInside = true;
                events.FightHome = ReadProgress(fight.Field("home"));
                events.FightStage = fight.Field("stage").AsLong(1);
                events.FightTicksLeft = fight.Field("ticksLeft").AsInt(0);
            }
            events.UnsentScore = node.Field("unsentScore").AsLong(0);
            events.BestScore = node.Field("bestScore").AsLong(0);
        }

        private static JsonValue WriteConditionShares(double[] shares)
        {
            var items = new List<JsonValue>();
            for (var i = 0; i < shares.Length; i++)
            {
                var condition = (ModifierCondition)i;
                if (shares[i] > 0 && condition != ModifierCondition.Always && System.Enum.IsDefined(typeof(ModifierCondition), condition))
                    items.Add(Object(("when", JsonValue.Of(ModifierConditions.Key(condition).Substring("when.".Length))), ("share", JsonValue.Of(shares[i]))));
            }
            return JsonValue.Array(items.ToArray());
        }

        private static void ReadConditionShares(ContentNode node, double[] shares)
        {
            foreach (var item in node.Items())
            {
                if (ModifierConditions.TryParse(item.Field("when").AsString(), out var condition) && (int)condition < shares.Length)
                    shares[(int)condition] = item.Field("share").AsNumber(0, 1);
            }
        }

        private static JsonValue WriteAwakening(AwakeningState awakening)
        {
            var ids = new List<string>(awakening.Progress.Keys);
            ids.Sort(System.StringComparer.Ordinal);
            var progress = new JsonValue[ids.Count];
            for (var i = 0; i < progress.Length; i++)
                progress[i] = Object(("id", JsonValue.Of(ids[i])), ("progress", JsonValue.Of(awakening.Progress[ids[i]])));
            return Object(("enchanted", WriteIdList(awakening.Enchanted)), ("progress", JsonValue.Array(progress)));
        }

        private static void ReadAwakening(ContentNode node, AwakeningState awakening)
        {
            foreach (var item in node.Field("enchanted").Items())
                awakening.Enchanted.Add(item.AsString());
            foreach (var item in node.Field("progress").Items())
                awakening.Progress[item.Field("id").AsString()] = item.Field("progress").AsNumber(0, double.MaxValue);
        }

        private static JsonValue WriteSeasonPass(SeasonPassState pass) => Object(
            ("unsent", JsonValue.Of(pass.UnsentPoints)),
            ("countedMinutes", JsonValue.Of(pass.CountedMinutes)),
            ("season", JsonValue.Of(pass.Season)),
            ("points", JsonValue.Of(pass.Points)),
            ("premium", JsonValue.Of(pass.Premium)));

        private static void ReadSeasonPass(ContentNode node, SeasonPassState pass)
        {
            pass.UnsentPoints = node.Field("unsent").AsLong(0);
            pass.CountedMinutes = node.Field("countedMinutes").AsLong(-1);
            pass.Season = node.Field("season").AsInt(-1);
            pass.Points = node.Field("points").AsLong(0);
            pass.Premium = node.Field("premium").AsBool();
        }

        private static JsonValue WriteTournament(TournamentState tournament) => Object(
            ("index", JsonValue.Of(tournament.Index)),
            ("format", tournament.Format.Length > 0 ? JsonValue.Of(tournament.Format) : JsonValue.Null()),
            ("baseBosses", JsonValue.Of(tournament.BaseBosses)),
            ("baseKills", JsonValue.Of(tournament.BaseKills)),
            ("best", JsonValue.Of(tournament.Best)),
            ("unsent", JsonValue.Of(tournament.Unsent)));

        private static void ReadClanCamp(ContentNode node, ClanCampState camp)
        {
            camp.Level = node.Field("level").AsInt(0, 100);
            camp.FeastUntilUnixMs = (long)ReadCount(node.Field("feastUntilUnixMs"));
            camp.AnsweredUnixMs = (long)ReadCount(node.Field("answeredUnixMs"));
            camp.SeenUnixMs = (long)ReadCount(node.Field("seenUnixMs"));
        }

        private static void ReadTournament(ContentNode node, TournamentState tournament)
        {
            tournament.Index = node.Field("index").AsLong(-1);
            tournament.Format = node.HasField("format") && !node.Field("format").IsNull ? node.Field("format").AsString() : string.Empty;
            tournament.BaseBosses = node.Field("baseBosses").AsLong(0);
            tournament.BaseKills = node.Field("baseKills").AsLong(0);
            tournament.Best = node.Field("best").AsLong(0);
            tournament.Unsent = node.Field("unsent").AsBool();
        }

        private static JsonValue WriteBestiary(BestiaryState bestiary)
        {
            var enemies = new List<string>(bestiary.Kills.Keys);
            enemies.Sort(System.StringComparer.Ordinal);
            var kills = new JsonValue[enemies.Count];
            for (var i = 0; i < kills.Length; i++)
                kills[i] = Object(("enemy", JsonValue.Of(enemies[i])), ("kills", JsonValue.Of(bestiary.Kills[enemies[i]])));
            var opened = new List<string>(bestiary.Opened);
            opened.Sort(System.StringComparer.Ordinal);
            return Object(("kills", JsonValue.Array(kills)), ("opened", WriteIdList(opened)));
        }

        private static void ReadBestiary(ContentNode node, BestiaryState bestiary)
        {
            foreach (var item in node.Field("kills").Items())
                bestiary.Kills[item.Field("enemy").AsString()] = item.Field("kills").AsLong(0);
            foreach (var item in node.Field("opened").Items())
                bestiary.Opened.Add(item.AsString());
        }

        private static JsonValue WriteAchievements(AchievementState achievements)
        {
            var unlocked = new List<string>(achievements.Unlocked);
            unlocked.Sort(System.StringComparer.Ordinal);
            var totals = new List<JsonValue>();
            for (var i = 0; i < achievements.Totals.Length; i++)
            {
                if (achievements.Totals[i] > 0)
                    totals.Add(Object(("counter", JsonValue.Of(AchievementCatalog.IdOf((AchievementCounter)i))), ("total", JsonValue.Of(achievements.Totals[i]))));
            }
            return Object(
                ("unlocked", WriteIdList(unlocked)),
                ("unsent", WriteIdList(achievements.Unsent)),
                ("totals", JsonValue.Array(totals.ToArray())));
        }

        private static void ReadAchievements(ContentNode node, AchievementState achievements)
        {
            foreach (var item in node.Field("unlocked").Items())
                achievements.Unlocked.Add(item.AsString());
            foreach (var item in node.Field("unsent").Items())
            {
                var id = item.AsString();
                if (!achievements.Unsent.Contains(id))
                    achievements.Unsent.Add(id);
            }
            foreach (var item in node.Field("totals").Items())
            {
                if (AchievementCatalog.TryParse(item.Field("counter").AsString(), out var counter))
                    achievements.Totals[(int)counter] = item.Field("total").AsNumber(0, 1e18);
            }
        }

        private static JsonValue WriteQuests(QuestState quests) => Object(
            ("day", JsonValue.Of(quests.Day)),
            ("week", JsonValue.Of(quests.Week)),
            ("streak", JsonValue.Of(quests.Streak)),
            ("swapsLeft", JsonValue.Of(quests.SwapsLeft)),
            ("swaps", JsonValue.Of(quests.Swaps)),
            ("partyClaimBosses", JsonValue.Of(quests.PartyClaimBosses)),
            ("daily", WriteQuestList(quests.Daily)),
            ("weekly", WriteQuestList(quests.Weekly)));

        private static JsonValue WriteQuestList(List<QuestEntry> list)
        {
            var items = new JsonValue[list.Count];
            for (var i = 0; i < items.Length; i++)
            {
                var entry = list[i];
                items[i] = Object(
                    ("kind", JsonValue.Of(QuestRules.IdOf(entry.Kind))),
                    ("day", JsonValue.Of(entry.Day)),
                    ("target", JsonValue.Of(entry.Target)),
                    ("progress", JsonValue.Of(entry.Progress)),
                    ("claimed", JsonValue.Of(entry.Claimed)));
            }
            return JsonValue.Array(items);
        }

        private static void ReadQuests(ContentNode node, QuestState quests)
        {
            quests.Day = node.Field("day").AsLong();
            quests.Week = node.Field("week").AsLong();
            quests.Streak = node.Field("streak").AsInt(0);
            quests.SwapsLeft = node.Field("swapsLeft").AsInt(0);
            quests.Swaps = node.Field("swaps").AsInt(0);
            quests.PartyClaimBosses = node.HasField("partyClaimBosses") ? node.Field("partyClaimBosses").AsInt(0) : 0;
            ReadQuestList(node.Field("daily"), quests.Daily);
            ReadQuestList(node.Field("weekly"), quests.Weekly);
            quests.Primed = false;
        }

        private static void ReadQuestList(ContentNode node, List<QuestEntry> into)
        {
            into.Clear();
            foreach (var item in node.Items())
            {
                if (!QuestRules.TryParse(item.Field("kind").AsString(), out var kind))
                    continue;
                into.Add(new QuestEntry
                {
                    Kind = kind,
                    Day = item.Field("day").AsLong(),
                    Target = item.Field("target").AsNumber(0, 1e12),
                    Progress = item.Field("progress").AsNumber(0, 1e12),
                    Claimed = item.Field("claimed").AsBool()
                });
            }
        }

        private static JsonValue WriteDrops(DropState drops)
        {
            var (rng0, rng1) = drops.Rng.State;
            var gear = new JsonValue[drops.Gear.Count];
            for (var i = 0; i < gear.Length; i++)
                gear[i] = WriteGear(drops.Gear[i]);
            return Object(
                ("rng", JsonValue.Array(new[] { SaveValues.Of(rng0), SaveValues.Of(rng1) })),
                ("day", JsonValue.Of(drops.Day)),
                ("dailyItems", JsonValue.Of(drops.DailyItems)),
                ("dust", JsonValue.Of(drops.Dust)),
                ("shards", JsonValue.Of(drops.Shards)),
                ("ingots", JsonValue.Of(drops.Ingots)),
                ("essence", JsonValue.Of(drops.Essence)),
                ("craftSpent", JsonValue.Of(drops.CraftSpent)),
                ("shardCarry", JsonValue.Of(drops.ShardCarry)),
                ("shardsFound", JsonValue.Of(drops.ShardsFound)),
                ("dailyShards", JsonValue.Of(drops.DailyShards)),
                ("salvageUpTo", drops.SalvageUpTo == null ? JsonValue.Null() : JsonValue.Of(ItemRarityNames.Of(drops.SalvageUpTo.Value))),
                ("itemsFound", JsonValue.Of(drops.ItemsFound)),
                ("salvagedItems", JsonValue.Of(drops.SalvagedItems)),
                ("gear", JsonValue.Array(gear)));
        }

        private static JsonValue WriteGear(GearItem item)
        {
            var affixes = WriteAffixes(item.Affixes);
            return Object(
                ("slot", JsonValue.Of(ItemSlotNames.Of(item.Slot))),
                ("rarity", JsonValue.Of(ItemRarityNames.Of(item.Rarity))),
                ("itemLevel", JsonValue.Of(item.ItemLevel)),
                ("color", item.Color == null ? JsonValue.Null() : JsonValue.Of(item.Color)),
                ("base", item.Base == null ? JsonValue.Null() : JsonValue.Of(item.Base.Id)),
                ("set", item.Set == null ? JsonValue.Null() : JsonValue.Of(item.Set.Id)),
                ("unique", item.Unique == null ? JsonValue.Null() : JsonValue.Of(item.Unique.Id)),
                ("implicit", item.RolledImplicit == null ? JsonValue.Null() : JsonValue.Of(item.RolledImplicit.Id)),
                ("affixes", JsonValue.Array(affixes)),
                ("forge", WriteForge(item.Forge)),
                ("secondRing", JsonValue.Of(item.SecondRing)));
        }

        private static JsonValue WriteWorn(GearState gear)
        {
            var worn = new JsonValue[GearState.Slots];
            for (var i = 0; i < worn.Length; i++)
                worn[i] = gear.Worn[i] == null ? JsonValue.Null() : WriteGear(gear.Worn[i]!);
            return JsonValue.Array(worn);
        }

        private static void ReadWorn(ContentNode node, ItemCatalog items, GearState gear)
        {
            var slot = 0;
            foreach (var item in node.Items())
            {
                if (slot >= GearState.Slots)
                    return;
                var read = item.IsNull ? null : ReadGear(item, items);
                if (read != null && read.Wearable && GearState.Fits((GearSlot)slot, read.Slot))
                    gear.Worn[slot] = read;
                slot++;
            }
        }

        private static void ReadDrops(ContentNode node, DropState drops, ItemCatalog? items)
        {
            var rng = node.Field("rng").Items();
            if (rng.Count != 2)
                throw node.Field("rng").Error("expected two state words");
            var rng0 = SaveValues.ReadULong(rng[0]);
            var rng1 = SaveValues.ReadULong(rng[1]);
            if (rng0 == 0 && rng1 == 0)
                throw node.Field("rng").Error("state words must not both be zero");
            drops.Rng = Rng.FromState(rng0, rng1);
            drops.Day = node.Field("day").AsLong();
            drops.DailyItems = node.Field("dailyItems").AsInt(0);
            drops.Dust = node.Field("dust").AsNumber(0, 1e300);
            if (node.HasField("shards"))
            {
                drops.Shards = node.Field("shards").AsLong(0);
                drops.Ingots = node.HasField("ingots") ? node.Field("ingots").AsLong(0) : 0;
                drops.Essence = node.HasField("essence") ? node.Field("essence").AsLong(0) : 0;
                drops.CraftSpent = node.HasField("craftSpent") ? node.Field("craftSpent").AsLong(0) : 0;
                drops.ShardCarry = node.Field("shardCarry").AsNumber(0, 1);
                drops.ShardsFound = node.Field("shardsFound").AsLong(0);
                drops.DailyShards = node.Field("dailyShards").AsInt(0);
            }
            var salvage = node.Field("salvageUpTo");
            drops.SalvageUpTo = !salvage.IsNull && ItemRarityNames.ByName.TryGetValue(salvage.AsString(), out var limit) ? limit : (ItemRarity?)null;
            drops.ItemsFound = node.Field("itemsFound").AsLong(0);
            drops.SalvagedItems = node.Field("salvagedItems").AsLong(0);
            foreach (var item in node.Field("gear").Items())
            {
                var gear = ReadGear(item, items);
                if (gear != null)
                    drops.Gear.Add(gear);
            }
        }

        private static GearItem? ReadGear(ContentNode node, ItemCatalog? items)
        {
            if (!ItemSlotNames.ByName.TryGetValue(node.Field("slot").AsString(), out var slot) || slot == ItemSlot.Weapon)
                return null;
            var rarity = ItemRarityNames.ByName.TryGetValue(node.Field("rarity").AsString(), out var found) ? found : ItemRarity.Simple;
            var color = node.Field("color");
            var itemLevel = node.Field("itemLevel").AsInt(1);
            if (items != null && node.HasField("unique") && !node.Field("unique").IsNull && items.TryGetUnique(node.Field("unique").AsString(), out var unique) && unique.Slot == slot)
                return new GearItem(unique, itemLevel, ReadForge(node));
            if (items != null && node.HasField("base") && !node.Field("base").IsNull && items.TryGetBase(node.Field("base").AsString(), out var gearBase) && gearBase.Slot == slot)
                return new GearItem(gearBase, rarity, itemLevel, ReadAffixes(node, items), ReadSet(node, items, slot), ReadImplicit(node, items, slot), ReadForge(node), node.HasField("secondRing") && node.Field("secondRing").AsBool());
            return new GearItem(slot, rarity, itemLevel, color.IsNull ? null : color.AsString());
        }

        private static JsonValue WriteDungeons(DungeonState dungeons)
        {
            var keys = new JsonValue[dungeons.Keys.Count];
            for (var i = 0; i < keys.Length; i++)
                keys[i] = WriteKey(dungeons.Keys[i]);
            return Object(
                ("keys", JsonValue.Array(keys)),
                ("active", dungeons.Active == null ? JsonValue.Null() : WriteKey(dungeons.Active)),
                ("startStage", JsonValue.Of(dungeons.StartStage)),
                ("home", dungeons.Home == null ? JsonValue.Null() : WriteProgress(dungeons.Home)),
                ("completed", JsonValue.Of(dungeons.Completed)),
                ("failed", JsonValue.Of(dungeons.Failed)),
                ("bestTier", JsonValue.Of(dungeons.BestTier)));
        }

        private static JsonValue WriteKey(DungeonKey key)
        {
            var modifiers = new JsonValue[key.Modifiers.Count];
            for (var i = 0; i < modifiers.Length; i++)
                modifiers[i] = JsonValue.Of(key.Modifiers[i]);
            return Object(("tier", JsonValue.Of(key.Tier)), ("modifiers", JsonValue.Array(modifiers)));
        }

        private static void ReadDungeons(ContentNode node, DungeonState dungeons)
        {
            foreach (var item in node.Field("keys").Items())
                dungeons.Keys.Add(ReadKey(item));
            var active = node.Field("active");
            var home = node.Field("home");
            if (!active.IsNull && !home.IsNull)
            {
                dungeons.Active = ReadKey(active);
                dungeons.Home = ReadProgress(home);
                dungeons.StartStage = node.Field("startStage").AsLong(1);
            }
            dungeons.Completed = node.Field("completed").AsLong(0);
            dungeons.Failed = node.Field("failed").AsLong(0);
            dungeons.BestTier = node.Field("bestTier").AsInt(0);
        }

        private static DungeonKey ReadKey(ContentNode node)
        {
            var modifiers = new List<string>();
            foreach (var item in node.Field("modifiers").Items())
                modifiers.Add(item.AsString());
            return new DungeonKey(node.Field("tier").AsInt(1), modifiers);
        }

        private static JsonValue WriteStages(HashSet<long> stages)
        {
            var sorted = new List<long>(stages);
            sorted.Sort();
            var items = new JsonValue[sorted.Count];
            for (var i = 0; i < items.Length; i++)
                items[i] = JsonValue.Of(sorted[i]);
            return JsonValue.Array(items);
        }

        private static SetDef? ReadSet(ContentNode node, ItemCatalog items, ItemSlot slot) =>
            node.HasField("set") && !node.Field("set").IsNull && items.TryGetSet(node.Field("set").AsString(), out var set) && set.Has(slot) ? set : null;

        private static JsonValue[] LongItems(long[] values)
        {
            var items = new JsonValue[values.Length];
            for (var i = 0; i < values.Length; i++)
                items[i] = JsonValue.Of(values[i]);
            return items;
        }

        private static void ReadBiomeLeft(ContentNode stage, GameState state)
        {
            state.BiomeFresh = stage.HasField("biomeFresh") && stage.Field("biomeFresh").AsBool();
            if (!stage.HasField("biomeLeft"))
                return;
            var i = 0;
            foreach (var item in stage.Field("biomeLeft").Items())
            {
                if (i < state.BiomeLeftTicks.Length)
                    state.BiomeLeftTicks[i] = item.AsLong(long.MinValue);
                i++;
            }
        }

        private static JsonValue[] WriteCollection(HashSet<string> collection)
        {
            var keys = new List<string>(collection);
            keys.Sort(System.StringComparer.Ordinal);
            var items = new JsonValue[keys.Count];
            for (var i = 0; i < items.Length; i++)
                items[i] = JsonValue.Of(keys[i]);
            return items;
        }

        private static JsonValue[] WriteAffixes(IReadOnlyList<GearAffix> affixes)
        {
            var written = new JsonValue[affixes.Count];
            for (var i = 0; i < written.Length; i++)
            {
                var affix = affixes[i];
                written[i] = affix.Support == null
                    ? Object(("id", JsonValue.Of(affix.Def.Id)), ("tier", JsonValue.Of(affix.Tier)), ("value", JsonValue.Of(affix.Value)), ("roll", JsonValue.Of(affix.Roll)))
                    : Object(("id", JsonValue.Of(affix.Def.Id)), ("tier", JsonValue.Of(affix.Tier)), ("value", JsonValue.Of(affix.Value)), ("roll", JsonValue.Of(affix.Roll)),
                        ("support", JsonValue.Of(affix.Support.Id)));
            }
            return written;
        }

        private static GearImplicit? ReadImplicit(ContentNode node, ItemCatalog items, ItemSlot slot) =>
            node.HasField("implicit") && !node.Field("implicit").IsNull && items.TryGetImplicit(node.Field("implicit").AsString(), out var rolled) && rolled.Slot == slot ? rolled : null;

        private static IReadOnlyList<GearAffix> ReadAffixes(ContentNode node, ItemCatalog items)
        {
            var affixes = new List<GearAffix>();
            if (!node.HasField("affixes"))
                return affixes;
            foreach (var item in node.Field("affixes").Items())
            {
                if (!items.TryGetAffix(item.Field("id").AsString(), out var def))
                    continue;
                var support = SupportOf(item, def);
                if (def.Supports.Count > 0 && support == null)
                    continue;
                affixes.Add(new GearAffix(def, item.Field("tier").AsInt(1, items.Tiers), item.Field("value").AsNumber(-1e6, 1e6), support,
                    item.HasField("roll") ? item.Field("roll").AsNumber(0, 1) : 0));
            }
            return affixes;
        }

        private static GemDef? SupportOf(ContentNode item, AffixDef def)
        {
            if (!item.HasField("support"))
                return null;
            var id = item.Field("support").AsString();
            foreach (var support in def.Supports)
            {
                if (support.Id == id)
                    return support;
            }
            return null;
        }

        private static JsonValue WriteWeapons(WeaponState weapons)
        {
            var bag = new JsonValue[weapons.Bag.Count];
            for (var i = 0; i < bag.Length; i++)
                bag[i] = WriteWeapon(weapons.Bag[i]);
            return Object(
                ("starterGiven", JsonValue.Of(weapons.StarterGiven)),
                ("held", WriteWeapon(weapons.Held)),
                ("bag", JsonValue.Array(bag)));
        }

        private static JsonValue WriteWeapon(WeaponItem? weapon)
        {
            if (weapon == null)
                return JsonValue.Null();
            return Object(
                ("base", JsonValue.Of(weapon.Id)),
                ("rarity", JsonValue.Of(ItemRarityNames.Of(weapon.Rarity))),
                ("itemLevel", JsonValue.Of(weapon.ItemLevel)),
                ("affixes", JsonValue.Array(WriteAffixes(weapon.Affixes))),
                ("unique", weapon.Unique == null ? JsonValue.Null() : JsonValue.Of(weapon.Unique.Id)),
                ("set", weapon.Set == null ? JsonValue.Null() : JsonValue.Of(weapon.Set.Id)),
                ("forge", WriteForge(weapon.Forge)));
        }

        private static JsonValue WriteForge(ForgeMark forge) =>
            forge.Empty ? JsonValue.Null() : Object(("temper", JsonValue.Of(forge.Temper)), ("hone", JsonValue.Of(forge.Hone)), ("brand", OptionalText(forge.Brand)), ("slain", WriteIdList(forge.Slain)));

        private static ForgeMark? ReadForge(ContentNode node)
        {
            if (!node.HasField("forge") || node.Field("forge").IsNull)
                return null;
            var field = node.Field("forge");
            var forge = new ForgeMark { Temper = field.Field("temper").AsNumber(0, 1), Hone = field.HasField("hone") ? field.Field("hone").AsNumber(0, 1) : 0, Brand = ReadOptionalText(field, "brand") };
            foreach (var item in field.Field("slain").Items())
                forge.Slain.Add(item.AsString());
            return forge;
        }

        private static void ReadWeapons(ContentNode node, WeaponCatalog catalog, ItemCatalog? items, WeaponState weapons)
        {
            weapons.StarterGiven = node.Field("starterGiven").AsBool();
            weapons.Held = node.HasField("held") ? ReadWeapon(node.Field("held"), catalog, items) : null;
            foreach (var item in node.Field("bag").Items())
            {
                var weapon = ReadWeapon(item, catalog, items);
                if (weapon != null)
                    weapons.Bag.Add(weapon);
            }
        }

        private static WeaponItem? ReadWeapon(ContentNode node, WeaponCatalog catalog, ItemCatalog? items)
        {
            if (node.IsNull)
                return null;
            if (!node.HasField("base"))
                return catalog.TryGetBase(node.AsString(), out var oldBase) ? catalog.Starter(oldBase) : null;
            if (!catalog.TryGetBase(node.Field("base").AsString(), out var weaponBase))
                return null;
            var rarity = ItemRarityNames.ByName.TryGetValue(node.Field("rarity").AsString(), out var found) ? found : ItemRarity.Simple;
            var unique = node.HasField("unique") && !node.Field("unique").IsNull && catalog.TryGetUnique(node.Field("unique").AsString(), out var uniqueDef) && uniqueDef.Base == weaponBase ? uniqueDef : null;
            var set = items != null && node.HasField("set") && !node.Field("set").IsNull && items.TryGetSet(node.Field("set").AsString(), out var setDef) && setDef.Has(ItemSlot.Weapon) ? setDef : null;
            var itemLevel = node.HasField("itemLevel") ? node.Field("itemLevel").AsInt(1) : 1;
            var affixes = items != null ? ReadAffixes(node, items) : null;
            return new WeaponItem(weaponBase, rarity, itemLevel, affixes, unique, set, ReadForge(node));
        }

        private static JsonValue WriteRift(RiftState rift) =>
            Object(
                ("windowId", JsonValue.Of(rift.WindowId)),
                ("windowEndUnixMs", JsonValue.Of(rift.WindowEndUnixMs)),
                ("inside", JsonValue.Of(rift.Inside)),
                ("shards", SaveValues.Of(rift.Shards)),
                ("deepest", JsonValue.Of(rift.Deepest)),
                ("artifacts", JsonValue.Array(WriteArtifacts(rift.Artifacts))),
                ("otherWorld", rift.OtherWorld == null ? JsonValue.Null() : WriteProgress(rift.OtherWorld)));

        private const long MomentTicksLimit = 1_000_000_000_000;

        private static JsonValue OptionalText(string text) => text.Length == 0 ? JsonValue.Null() : JsonValue.Of(text);

        private static string ReadOptionalText(ContentNode node, string name) =>
            node.HasField(name) && !node.Field(name).IsNull ? node.Field(name).AsString() : string.Empty;

        private static JsonValue WriteProgress(StageProgress progress) =>
            Object(
                ("current", JsonValue.Of(progress.Stage)),
                ("highest", JsonValue.Of(progress.HighestStage)),
                ("kills", JsonValue.Of(progress.KillsOnStage)),
                ("farming", JsonValue.Of(progress.Farming)),
                ("farmKills", JsonValue.Of(progress.FarmKills)),
                ("bossFailsInARow", JsonValue.Of(progress.BossFailsInARow)),
                ("bossBestFraction", JsonValue.Of(progress.BossBestFraction)),
                ("biome", OptionalText(progress.Biome)),
                ("previousBiome", OptionalText(progress.PreviousBiome)),
                ("biomeSinceTick", JsonValue.Of(progress.BiomeSinceTick)));

        private static void ReadRift(ContentNode node, RiftState rift)
        {
            rift.WindowId = node.Field("windowId").AsLong(0);
            rift.WindowEndUnixMs = node.Field("windowEndUnixMs").AsLong(0);
            rift.Inside = node.Field("inside").AsBool();
            rift.Shards = SaveValues.ReadBigNumber(node.Field("shards"));
            rift.Deepest = node.Field("deepest").AsLong(1);
            ReadArtifacts(node.Field("artifacts"), rift.Artifacts);
            var other = node.Field("otherWorld");
            rift.OtherWorld = other.IsNull ? null : ReadProgress(other);
            if (rift.Inside && rift.OtherWorld == null)
                throw other.Error("must hold the home progress while inside the rift");
        }

        private static StageProgress ReadProgress(ContentNode node)
        {
            var progress = new StageProgress
            {
                Stage = node.Field("current").AsLong(1),
                HighestStage = node.Field("highest").AsLong(1),
                KillsOnStage = node.Field("kills").AsInt(0),
                Farming = node.Field("farming").AsBool(),
                FarmKills = node.Field("farmKills").AsInt(0),
                BossFailsInARow = node.Field("bossFailsInARow").AsInt(0),
                BossBestFraction = node.Field("bossBestFraction").AsNumber(0, 1),
                Biome = ReadOptionalText(node, "biome"),
                PreviousBiome = ReadOptionalText(node, "previousBiome"),
                BiomeSinceTick = node.HasField("biomeSinceTick") ? node.Field("biomeSinceTick").AsLong(long.MinValue) : 0
            };
            if (progress.HighestStage < progress.Stage)
                throw node.Field("highest").Error($"must not be lower than current ({progress.Stage})");
            return progress;
        }

        private static void ReadArtifacts(ContentNode node, Dictionary<string, int> artifacts)
        {
            foreach (var item in node.Items())
                artifacts[item.Field("id").AsString()] = item.Field("level").AsInt(1);
        }

        private static JsonValue WriteClassTree(ClassTreeAllocation tree)
        {
            var ids = new List<string>(tree.Ranks.Keys);
            ids.Sort(System.StringComparer.Ordinal);
            var ranks = new JsonValue[ids.Count];
            for (var i = 0; i < ranks.Length; i++)
                ranks[i] = Object(("id", JsonValue.Of(ids[i])), ("rank", JsonValue.Of(tree.Ranks[ids[i]])));
            var marks = new List<int>(tree.Majors.Keys);
            marks.Sort();
            var majors = new JsonValue[marks.Count];
            for (var i = 0; i < majors.Length; i++)
                majors[i] = Object(("at", JsonValue.Of(marks[i])), ("id", JsonValue.Of(tree.Majors[marks[i]])));
            var plan = new JsonValue[tree.Plan.Count];
            for (var i = 0; i < plan.Length; i++)
                plan[i] = JsonValue.Of(tree.Plan[i]);
            return Object(
                ("class", JsonValue.Of(tree.TreeId)),
                ("ranks", JsonValue.Array(ranks)),
                ("majors", JsonValue.Array(majors)),
                ("plan", JsonValue.Array(plan)));
        }

        private static JsonValue WriteCommonTrees(List<ClassTreeAllocation?> slots)
        {
            var values = new JsonValue[slots.Count];
            for (var i = 0; i < values.Length; i++)
                values[i] = slots[i] is { } tree ? WriteClassTree(tree) : JsonValue.Null();
            return JsonValue.Array(values);
        }

        private static List<ClassTreeAllocation?> ReadCommonTrees(ContentNode node)
        {
            var slots = new List<ClassTreeAllocation?>();
            foreach (var item in node.Items())
                slots.Add(item.IsNull ? null : ReadClassTree(item));
            return slots;
        }

        private static ClassTreeAllocation ReadClassTree(ContentNode node)
        {
            var tree = new ClassTreeAllocation(node.Field("class").AsString());
            foreach (var item in node.Field("ranks").Items())
                tree.Restore(item.Field("id").AsString(), item.Field("rank").AsInt(1, TreeTemplate.Ranks));
            foreach (var item in node.Field("majors").Items())
                tree.RestoreMajor(item.Field("at").AsInt(0, 100_000), item.Field("id").AsString());
            var plan = new List<string>();
            foreach (var item in node.Field("plan").Items())
                plan.Add(item.AsString());
            tree.RestorePlan(plan);
            return tree;
        }

        private static JsonValue WriteAscendancy(AscendancyState ascendancy) =>
            Object(
                ("subclass", ascendancy.Subclass == null ? JsonValue.Null() : JsonValue.Of(ascendancy.Subclass)),
                ("lastFreeChangeUnixMs", JsonValue.Of(ascendancy.LastFreeChangeUnixMs)),
                ("activities", JsonValue.Array(SortedStrings(ascendancy.CompletedActivities))),
                ("forks", JsonValue.Array(SortedStrings(ascendancy.ForkPicks))));

        private static AscendancyState ReadAscendancy(ContentNode node)
        {
            var subclass = node.Field("subclass");
            var activities = new List<string>();
            foreach (var item in node.Field("activities").Items())
                activities.Add(item.AsString());
            var forks = new List<string>();
            if (node.HasField("forks"))
            {
                foreach (var item in node.Field("forks").Items())
                    forks.Add(item.AsString());
            }
            var ascendancy = new AscendancyState { LastFreeChangeUnixMs = node.Field("lastFreeChangeUnixMs").AsLong() };
            ascendancy.Restore(subclass.IsNull ? null : subclass.AsString(), activities, forks);
            return ascendancy;
        }

        private static JsonValue[] SortedStrings(IReadOnlyCollection<string> values)
        {
            var sorted = new List<string>(values);
            sorted.Sort(System.StringComparer.Ordinal);
            var items = new JsonValue[sorted.Count];
            for (var i = 0; i < items.Length; i++)
                items[i] = JsonValue.Of(sorted[i]);
            return items;
        }

        private static ulong ReadCount(ContentNode node)
        {
            if (!ulong.TryParse(node.AsString(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var count))
                throw node.Error($"'{node.AsString()}' is not a whole count");
            return count;
        }

        private static JsonValue[] WritePresetSkills(BuildPreset preset)
        {
            var items = new JsonValue[preset.Skills.Count];
            for (var i = 0; i < items.Length; i++)
            {
                var skill = preset.Skills[i];
                var role = preset.RoleAt(i);
                items[i] = Object(("gem", JsonValue.Of(skill.Id)),
                    ("slot", role == null ? JsonValue.Null() : JsonValue.Of(RoleName(role.Value))),
                    ("supports", JsonValue.Array(WriteSupports(preset.SupportsAt(i)))));
            }
            return items;
        }

        private static JsonValue[] WriteArtifacts(Dictionary<string, int> artifacts)
        {
            var ids = new List<string>(artifacts.Keys);
            ids.Sort(System.StringComparer.Ordinal);
            var items = new JsonValue[ids.Count];
            for (var i = 0; i < items.Length; i++)
                items[i] = Object(("id", JsonValue.Of(ids[i])), ("level", JsonValue.Of(artifacts[ids[i]])));
            return items;
        }

        private static JsonValue[] WriteOwnedGems(GameState state)
        {
            var ids = new JsonValue[state.OwnedGems.Count];
            for (var i = 0; i < ids.Length; i++)
                ids[i] = JsonValue.Of(state.OwnedGems[i].Id);
            return ids;
        }

        private static JsonValue[] WriteTutorialDone(GameState state)
        {
            var ids = new List<string>(state.TutorialDone);
            ids.Sort(System.StringComparer.Ordinal);
            var values = new JsonValue[ids.Count];
            for (var i = 0; i < values.Length; i++)
                values[i] = JsonValue.Of(ids[i]);
            return values;
        }

        private static JsonValue[] WriteQueueDamage(GameState state)
        {
            var queue = new JsonValue[GameState.QueueDepth - 1];
            for (var i = 0; i < queue.Length; i++)
                queue[i] = SaveValues.Of(state.QueueDamage[i + 1]);
            return queue;
        }

        private static void ReadQueueDamage(ContentNode node, GameState state)
        {
            if (!node.HasField("queueDamage"))
                return;
            var position = 1;
            foreach (var item in node.Field("queueDamage").Items())
            {
                if (position < GameState.QueueDepth)
                    state.QueueDamage[position] = SaveValues.ReadBigNumber(item);
                position++;
            }
        }

        private static void ReadTutorial(ContentNode node, GameState state)
        {
            if (!node.HasField("tutorial"))
            {
                state.TutorialSkipped = true;
                return;
            }
            var tutorial = node.Field("tutorial");
            state.TutorialSkipped = tutorial.Field("skipped").AsBool();
            foreach (var id in tutorial.Field("done").Items())
                state.TutorialDone.Add(id.AsString());
        }

        private static JsonValue[] WriteSkills(GameState state)
        {
            var skills = new JsonValue[state.Skills.Count];
            for (var i = 0; i < skills.Length; i++)
            {
                var slot = state.Skills[i];
                skills[i] = Object(
                    ("gem", JsonValue.Of(slot.Skill.Id)),
                    ("cooldownTicks", JsonValue.Of(slot.CooldownTicks)),
                    ("activeTicks", JsonValue.Of(slot.ActiveTicks)),
                    ("hitsLeft", JsonValue.Of(slot.HitsLeft)),
                    ("lastCastTick", JsonValue.Of(slot.LastCastTick)),
                    ("slot", JsonValue.Of(RoleName(slot.Role))),
                    ("supports", JsonValue.Array(WriteSupports(slot.Supports))));
            }
            return skills;
        }

        private static JsonValue WriteGemProgress(GameState state)
        {
            var map = new Dictionary<string, JsonValue>(state.GemProgress.Count);
            foreach (var pair in state.GemProgress)
                map.Add(pair.Key, Object(("level", JsonValue.Of(pair.Value.Level)), ("experience", JsonValue.Of(pair.Value.Experience)),
                    ("rebirths", JsonValue.Of(pair.Value.Rebirths)), ("branches", WriteBranches(pair.Value.Branches))));
            return JsonValue.Object(map);
        }

        private static void ReadGemProgress(ContentNode node, GemCatalog gems, GameState state)
        {
            foreach (var id in node.FieldNames())
            {
                if (!gems.TryGet(id, out var gem))
                    continue;
                var item = node.Field(id);
                var progress = state.ProgressOf(gem);
                progress.Level = item.Field("level").AsInt(1, gems.Levels.Max);
                progress.Experience = item.Field("experience").AsNumber(0, 1e15);
                if (item.HasField("rebirths"))
                    progress.Rebirths = item.Field("rebirths").AsInt(0, 100);
                if (item.HasField("branches"))
                    ReadBranches(item.Field("branches"), gem, progress);
            }
        }

        private static JsonValue WriteBranches(int[] branches)
        {
            var items = new JsonValue[branches.Length];
            for (var i = 0; i < items.Length; i++)
                items[i] = JsonValue.Of(branches[i]);
            return JsonValue.Array(items);
        }

        private static void ReadBranches(ContentNode node, GemDef gem, GemProgress progress)
        {
            var items = node.Items();
            for (var tier = 0; tier < items.Count && tier < progress.Branches.Length; tier++)
            {
                var choice = items[tier].AsInt(SkillBranches.NoChoice, SkillBranchTier.OptionCount - 1);
                progress.Branches[tier] = tier < gem.Branches.Count ? choice : SkillBranches.NoChoice;
            }
        }

        private static JsonValue[] WriteSupports(GemDef?[] supports)
        {
            var used = System.Math.Min(supports.Length, SkillSlot.BuiltInSocket);
            while (used > 0 && supports[used - 1] == null)
                used--;
            var items = new JsonValue[used];
            for (var i = 0; i < used; i++)
                items[i] = supports[i] == null ? JsonValue.Null() : JsonValue.Of(supports[i]!.Id);
            return items;
        }

        private static void ReadSupports(ContentNode item, GemCatalog gems, GemDef?[] supports)
        {
            if (!item.HasField("supports"))
                return;
            var socket = 0;
            foreach (var id in item.Field("supports").Items())
            {
                if (socket >= supports.Length)
                    return;
                if (!id.IsNull && gems.TryGet(id.AsString(), out var support) && support.Kind == GemKind.Support)
                    supports[socket] = support;
                socket++;
            }
        }

        private static void ReadSkills(ContentNode node, GemCatalog gems, GameState state)
        {
            foreach (var item in node.Items())
            {
                if (!gems.TryGet(item.Field("gem").AsString(), out var skill) || skill.Skill == null)
                    continue;
                var slot = new SkillSlot(skill)
                {
                    CooldownTicks = item.Field("cooldownTicks").AsInt(0),
                    ActiveTicks = item.Field("activeTicks").AsInt(0),
                    HitsLeft = item.Field("hitsLeft").AsInt(0),
                    LastCastTick = item.HasField("lastCastTick") ? item.Field("lastCastTick").AsLong(-1) : -1,
                    Role = ReadRole(item, state.Skills.Count) ?? SkillLoadout.FreeRole(state.Skills, skill) ?? SkillSlotRole.Auto
                };
                ReadSupports(item, gems, slot.Supports);
                state.Skills.Add(slot);
            }
        }

        private static readonly Dictionary<string, SkillSlotRole> Roles = new Dictionary<string, SkillSlotRole>(System.StringComparer.Ordinal)
        {
            ["left"] = SkillSlotRole.Left,
            ["right"] = SkillSlotRole.Right,
            ["auto"] = SkillSlotRole.Auto,
            ["aura"] = SkillSlotRole.Aura
        };

        private static string RoleName(SkillSlotRole role) => role switch
        {
            SkillSlotRole.Left => "left",
            SkillSlotRole.Right => "right",
            SkillSlotRole.Aura => "aura",
            _ => "auto"
        };

        private static SkillSlotRole? ReadRole(ContentNode item, int position)
        {
            if (!item.HasField("slot") || item.Field("slot").IsNull)
                return SkillLoadout.RoleAtPosition(position);
            var name = item.Field("slot").AsString();
            if (!Roles.TryGetValue(name, out var role))
                throw item.Field("slot").Error($"unknown skill slot '{name}', expected left, right, auto or aura");
            return role;
        }

        private static EnemyDef? ReadEnemyDef(ContentNode id, EnemyCatalog enemies)
        {
            if (id.IsNull)
                return null;
            return enemies.TryGet(id.AsString(), out var enemy) ? enemy : null;
        }

        private static JsonValue Object(params (string Name, JsonValue Value)[] fields)
        {
            var map = new Dictionary<string, JsonValue>(fields.Length);
            foreach (var (name, value) in fields)
                map.Add(name, value);
            return JsonValue.Object(map);
        }
    }
}
