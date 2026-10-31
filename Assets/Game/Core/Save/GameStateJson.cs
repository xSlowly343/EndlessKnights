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
                    ("comboTowardStrike", JsonValue.Of(state.ComboTowardStrike)),
                    ("killsTowardRise", JsonValue.Of(state.KillsTowardRise)),
                    ("keysTowardShield", JsonValue.Of(state.KeysTowardShield)),
                    ("keysTowardMark", JsonValue.Of(state.KeysTowardMark)),
                    ("keysTowardBlind", JsonValue.Of(state.KeysTowardBlind)),
                    ("keyHits", JsonValue.Of(state.KeyHits)),
                    ("comboIdleTicks", JsonValue.Of(state.ComboIdleTicks)))),
                ("stage", Object(
                    ("current", JsonValue.Of(state.Stage)),
                    ("highest", JsonValue.Of(state.HighestStage)),
                    ("record", JsonValue.Of(state.RecordStage)),
                    ("kills", JsonValue.Of(state.KillsOnStage)),
                    ("bossTicksLeft", JsonValue.Of(state.BossTicksLeft)),
                    ("farming", JsonValue.Of(state.Farming)),
                    ("farmKills", JsonValue.Of(state.FarmKills)),
                    ("bossFailsInARow", JsonValue.Of(state.BossFailsInARow)),
                    ("bossBestFraction", JsonValue.Of(state.BossBestFraction)))),
                ("enemy", Object(
                    ("id", state.Enemy == null ? JsonValue.Null() : JsonValue.Of(state.Enemy.Id)),
                    ("type", JsonValue.Of(SaveValues.NameOf(state.EnemyType, EnemyTypes))),
                    ("hp", SaveValues.Of(state.EnemyHp)),
                    ("maxHp", SaveValues.Of(state.EnemyMaxHp)),
                    ("wanderer", JsonValue.Of(state.WandererBoss)),
                    ("goldenTicks", JsonValue.Of(state.GoldenTicks)),
                    ("goldenNormalHp", SaveValues.Of(state.GoldenNormalHp)))),
                ("gold", Object(
                    ("wallet", SaveValues.Of(state.Gold)),
                    ("runEarned", SaveValues.Of(state.RunGoldEarned)))),
                ("recentDps", SaveValues.Of(state.RecentDps)),
                ("prestige", Object(
                    ("echo", SaveValues.Of(state.Echo)),
                    ("echoEarned", SaveValues.Of(state.EchoEarned)),
                    ("count", JsonValue.Of(state.Prestiges)))),
                ("artifacts", JsonValue.Array(WriteArtifacts(state.Artifacts))),
                ("rift", WriteRift(state.Rift)),
                ("weapons", WriteWeapons(state.Weapons)),
                ("drops", WriteDrops(state.Drops)),
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
                ("setCollection", JsonValue.Array(WriteCollection(state))),
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
                ("gemProgress", WriteGemProgress(state)),
                ("boundGems", WriteBoundGems(state)),
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
                ComboTowardStrike = hero.HasField("comboTowardStrike") ? hero.Field("comboTowardStrike").AsInt(0, 1_000_000) : 0,
                KillsTowardRise = hero.HasField("killsTowardRise") ? hero.Field("killsTowardRise").AsInt(0, 1_000_000) : 0,
                KeysTowardShield = hero.HasField("keysTowardShield") ? hero.Field("keysTowardShield").AsInt(0, 1_000_000) : 0,
                KeysTowardMark = hero.HasField("keysTowardMark") ? hero.Field("keysTowardMark").AsInt(0, 1_000_000) : 0,
                KeysTowardBlind = hero.HasField("keysTowardBlind") ? hero.Field("keysTowardBlind").AsInt(0, 1_000_000) : 0,
                KeyHits = hero.Field("keyHits").AsLong(0),
                ComboIdleTicks = hero.Field("comboIdleTicks").AsLong(0),
                Stage = stage.Field("current").AsLong(1),
                HighestStage = stage.Field("highest").AsLong(1),
                RecordStage = stage.Field("record").AsLong(1),
                KillsOnStage = stage.Field("kills").AsInt(0),
                BossTicksLeft = stage.Field("bossTicksLeft").AsInt(0),
                Farming = stage.Field("farming").AsBool(),
                FarmKills = stage.Field("farmKills").AsInt(0),
                BossFailsInARow = stage.Field("bossFailsInARow").AsInt(0),
                BossBestFraction = stage.HasField("bossBestFraction") ? stage.Field("bossBestFraction").AsNumber(0, 1) : 0,
                Gold = SaveValues.ReadBigNumber(gold.Field("wallet")),
                RunGoldEarned = SaveValues.ReadBigNumber(gold.Field("runEarned")),
                RecentDps = SaveValues.ReadBigNumber(node.Field("recentDps")),
                Echo = SaveValues.ReadBigNumber(node.Field("prestige").Field("echo")),
                EchoEarned = SaveValues.ReadBigNumber(node.Field("prestige").Field("echoEarned")),
                Prestiges = node.Field("prestige").Field("count").AsInt(0),
                ChainDamage = SaveValues.ReadBigNumber(node.Field("chainDamage")),
                KeyCarry = carry.Field("keys").AsNumber(0, 1e6),
                MouseCarryPx = carry.Field("mousePx").AsNumber(0, 1e9)
            };
            if (state.HighestStage < state.Stage)
                throw stage.Field("highest").Error($"must not be lower than current ({state.Stage})");

            state.SpawnEnemy(ReadEnemyDef(enemy.Field("id"), enemies), SaveValues.ReadEnum(enemy.Field("type"), EnemyTypes),
                SaveValues.ReadBigNumber(enemy.Field("maxHp")));
            state.EnemyHp = SaveValues.ReadBigNumber(enemy.Field("hp"));
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
                var triggerNode = item.Field("trigger");
                GemDef? trigger = null;
                if (!triggerNode.IsNull && gems.TryGet(triggerNode.AsString(), out var found) && found.Trigger != null)
                    trigger = found;
                state.OtherPreset.Skills.Add((skill, trigger));
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
            if (node.HasField("gemProgress"))
                ReadGemProgress(node.Field("gemProgress"), gems, state);
            else
                state.Drops.Rng = new Rng(rng0 ^ rng1 ^ DropState.SeedSalt);
            if (node.HasField("boundGems"))
                ReadBoundGems(node.Field("boundGems"), gems, state);
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
                ("completed", JsonValue.Array(completed)));
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
                ("shardCarry", JsonValue.Of(drops.ShardCarry)),
                ("shardsFound", JsonValue.Of(drops.ShardsFound)),
                ("dailyShards", JsonValue.Of(drops.DailyShards)),
                ("uncutGems", JsonValue.Of(drops.UncutGems)),
                ("uncutFound", JsonValue.Of(drops.UncutFound)),
                ("salvageUpTo", drops.SalvageUpTo == null ? JsonValue.Null() : JsonValue.Of(ItemRarityNames.Of(drops.SalvageUpTo.Value))),
                ("itemsFound", JsonValue.Of(drops.ItemsFound)),
                ("salvagedItems", JsonValue.Of(drops.SalvagedItems)),
                ("gear", JsonValue.Array(gear)));
        }

        private static JsonValue WriteGear(GearItem item)
        {
            var affixes = new JsonValue[item.Affixes.Count];
            for (var i = 0; i < affixes.Length; i++)
                affixes[i] = Object(("id", JsonValue.Of(item.Affixes[i].Def.Id)), ("tier", JsonValue.Of(item.Affixes[i].Tier)), ("value", JsonValue.Of(item.Affixes[i].Value)));
            return Object(
                ("slot", JsonValue.Of(ItemSlotNames.Of(item.Slot))),
                ("rarity", JsonValue.Of(ItemRarityNames.Of(item.Rarity))),
                ("itemLevel", JsonValue.Of(item.ItemLevel)),
                ("color", item.Color == null ? JsonValue.Null() : JsonValue.Of(item.Color)),
                ("base", item.Base == null ? JsonValue.Null() : JsonValue.Of(item.Base.Id)),
                ("set", item.Set == null ? JsonValue.Null() : JsonValue.Of(item.Set.Id)),
                ("unique", item.Unique == null ? JsonValue.Null() : JsonValue.Of(item.Unique.Id)),
                ("affixes", JsonValue.Array(affixes)));
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
                drops.ShardCarry = node.Field("shardCarry").AsNumber(0, 1);
                drops.ShardsFound = node.Field("shardsFound").AsLong(0);
                drops.DailyShards = node.Field("dailyShards").AsInt(0);
                drops.UncutGems = node.Field("uncutGems").AsInt(0);
                drops.UncutFound = node.Field("uncutFound").AsLong(0);
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
                return new GearItem(unique, itemLevel);
            if (items != null && node.HasField("base") && !node.Field("base").IsNull && items.TryGetBase(node.Field("base").AsString(), out var gearBase) && gearBase.Slot == slot)
                return new GearItem(gearBase, rarity, itemLevel, ReadAffixes(node, items), ReadSet(node, items, slot));
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

        private static JsonValue[] WriteCollection(GameState state)
        {
            var keys = new List<string>(state.SetCollection);
            keys.Sort(System.StringComparer.Ordinal);
            var items = new JsonValue[keys.Count];
            for (var i = 0; i < items.Length; i++)
                items[i] = JsonValue.Of(keys[i]);
            return items;
        }

        private static IReadOnlyList<GearAffix> ReadAffixes(ContentNode node, ItemCatalog items)
        {
            var affixes = new List<GearAffix>();
            if (!node.HasField("affixes"))
                return affixes;
            foreach (var item in node.Field("affixes").Items())
            {
                if (items.TryGetAffix(item.Field("id").AsString(), out var def))
                    affixes.Add(new GearAffix(def, item.Field("tier").AsInt(1, items.Tiers), item.Field("value").AsNumber(-1e6, 1e6)));
            }
            return affixes;
        }

        private static JsonValue WriteWeapons(WeaponState weapons)
        {
            var bag = new JsonValue[weapons.Bag.Count];
            for (var i = 0; i < bag.Length; i++)
                bag[i] = WriteWeapon(weapons.Bag[i]);
            return Object(
                ("starterGiven", JsonValue.Of(weapons.StarterGiven)),
                ("right", WriteWeapon(weapons.Right)),
                ("left", WriteWeapon(weapons.Left)),
                ("bag", JsonValue.Array(bag)));
        }

        private static JsonValue WriteWeapon(WeaponItem? weapon)
        {
            if (weapon == null)
                return JsonValue.Null();
            var skills = new JsonValue[weapon.Skills.Count];
            for (var i = 0; i < skills.Length; i++)
                skills[i] = JsonValue.Of(weapon.Skills[i]);
            return Object(
                ("base", JsonValue.Of(weapon.Id)),
                ("rarity", JsonValue.Of(ItemRarityNames.Of(weapon.Rarity))),
                ("skills", JsonValue.Array(skills)),
                ("unique", weapon.Unique == null ? JsonValue.Null() : JsonValue.Of(weapon.Unique.Id)),
                ("set", weapon.Set == null ? JsonValue.Null() : JsonValue.Of(weapon.Set.Id)));
        }

        private static void ReadWeapons(ContentNode node, WeaponCatalog catalog, ItemCatalog? items, WeaponState weapons)
        {
            weapons.StarterGiven = node.Field("starterGiven").AsBool();
            weapons.Right = ReadWeapon(node.Field("right"), catalog, items);
            weapons.Left = ReadWeapon(node.Field("left"), catalog, items);
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
            var skills = new List<string>();
            foreach (var item in node.Field("skills").Items())
                skills.Add(item.AsString());
            var unique = node.HasField("unique") && !node.Field("unique").IsNull && catalog.TryGetUnique(node.Field("unique").AsString(), out var uniqueDef) && uniqueDef.Base == weaponBase ? uniqueDef : null;
            var set = items != null && node.HasField("set") && !node.Field("set").IsNull && items.TryGetSet(node.Field("set").AsString(), out var setDef) && setDef.Has(ItemSlot.Weapon) ? setDef : null;
            return new WeaponItem(weaponBase, rarity, skills, unique, set);
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

        private static JsonValue WriteProgress(StageProgress progress) =>
            Object(
                ("current", JsonValue.Of(progress.Stage)),
                ("highest", JsonValue.Of(progress.HighestStage)),
                ("kills", JsonValue.Of(progress.KillsOnStage)),
                ("farming", JsonValue.Of(progress.Farming)),
                ("farmKills", JsonValue.Of(progress.FarmKills)),
                ("bossFailsInARow", JsonValue.Of(progress.BossFailsInARow)),
                ("bossBestFraction", JsonValue.Of(progress.BossBestFraction)));

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
                BossBestFraction = node.Field("bossBestFraction").AsNumber(0, 1)
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
                ("allocated", JsonValue.Array(SortedStrings(ascendancy.Allocated))),
                ("activities", JsonValue.Array(SortedStrings(ascendancy.CompletedActivities))));

        private static AscendancyState ReadAscendancy(ContentNode node)
        {
            var subclass = node.Field("subclass");
            var allocated = new List<string>();
            foreach (var item in node.Field("allocated").Items())
                allocated.Add(item.AsString());
            var activities = new List<string>();
            foreach (var item in node.Field("activities").Items())
                activities.Add(item.AsString());
            var ascendancy = new AscendancyState { LastFreeChangeUnixMs = node.Field("lastFreeChangeUnixMs").AsLong() };
            ascendancy.Restore(subclass.IsNull ? null : subclass.AsString(), allocated, activities);
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
                var (skill, trigger) = preset.Skills[i];
                items[i] = Object(("gem", JsonValue.Of(skill.Id)), ("trigger", trigger == null ? JsonValue.Null() : JsonValue.Of(trigger.Id)),
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
                    ("trigger", slot.Trigger == null ? JsonValue.Null() : JsonValue.Of(slot.Trigger.Id)),
                    ("cooldownTicks", JsonValue.Of(slot.CooldownTicks)),
                    ("activeTicks", JsonValue.Of(slot.ActiveTicks)),
                    ("hitsLeft", JsonValue.Of(slot.HitsLeft)),
                    ("supports", JsonValue.Array(WriteSupports(slot.Supports))));
            }
            return skills;
        }

        private static JsonValue WriteGemProgress(GameState state)
        {
            var map = new Dictionary<string, JsonValue>(state.GemProgress.Count);
            foreach (var pair in state.GemProgress)
                map.Add(pair.Key, Object(("level", JsonValue.Of(pair.Value.Level)), ("experience", JsonValue.Of(pair.Value.Experience)),
                    ("rebirths", JsonValue.Of(pair.Value.Rebirths))));
            return JsonValue.Object(map);
        }

        private static JsonValue WriteBoundGems(GameState state)
        {
            var map = new Dictionary<string, JsonValue>(state.BoundGems.Count);
            foreach (var pair in state.BoundGems)
            {
                if (pair.Value > 0)
                    map.Add(pair.Key, JsonValue.Of(pair.Value));
            }
            return JsonValue.Object(map);
        }

        private static void ReadBoundGems(ContentNode node, GemCatalog gems, GameState state)
        {
            foreach (var id in node.FieldNames())
            {
                if (gems.TryGet(id, out _))
                    state.BoundGems[id] = node.Field(id).AsInt(0, 100000);
            }
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
            }
        }

        private static JsonValue[] WriteSupports(GemDef?[] supports)
        {
            var used = supports.Length;
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
                var triggerNode = item.Field("trigger");
                GemDef? trigger = null;
                if (!triggerNode.IsNull && gems.TryGet(triggerNode.AsString(), out var found) && found.Trigger != null)
                    trigger = found;
                var slot = new SkillSlot(skill, trigger)
                {
                    CooldownTicks = item.Field("cooldownTicks").AsInt(0),
                    ActiveTicks = item.Field("activeTicks").AsInt(0),
                    HitsLeft = item.Field("hitsLeft").AsInt(0)
                };
                ReadSupports(item, gems, slot.Supports);
                state.Skills.Add(slot);
            }
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
