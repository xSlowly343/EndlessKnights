using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Json;
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
                    ("maxHp", SaveValues.Of(state.EnemyMaxHp)))),
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
                ("inputStats", Object(
                    ("keys", JsonValue.Of(state.TotalInput.Keys.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                    ("leftClicks", JsonValue.Of(state.TotalInput.LeftClicks.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                    ("rightClicks", JsonValue.Of(state.TotalInput.RightClicks.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                    ("middleClicks", JsonValue.Of(state.TotalInput.MiddleClicks.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                    ("mouseMeters", JsonValue.Of(state.TotalInput.MouseMeters)))),
                ("skills", JsonValue.Array(WriteSkills(state))),
                ("ownedGems", JsonValue.Array(WriteOwnedGems(state))),
                ("tree", WriteTree(state.Tree)),
                ("ascendancy", WriteAscendancy(state.Ascendancy)),
                ("presets", Object(
                    ("active", JsonValue.Of(state.ActivePreset)),
                    ("otherTree", WriteTree(state.OtherPreset.Tree)),
                    ("otherSkills", JsonValue.Array(WritePresetSkills(state.OtherPreset))))),
                ("chainDamage", SaveValues.Of(state.ChainDamage)),
                ("tutorial", Object(
                    ("skipped", JsonValue.Of(state.TutorialSkipped)),
                    ("done", JsonValue.Array(WriteTutorialDone(state))))),
                ("carry", Object(
                    ("keys", JsonValue.Of(state.KeyCarry)),
                    ("mousePx", JsonValue.Of(state.MouseCarryPx)))));
        }

        public static GameState Read(ContentNode node, EnemyCatalog enemies, GemCatalog gems, WeaponCatalog weapons)
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
            ReadSkills(node.Field("skills"), gems, state);
            foreach (var id in node.Field("ownedGems").Items())
            {
                if (gems.TryGet(id.AsString(), out var gem))
                    state.OwnedGems.Add(gem);
            }
            state.Tree = ReadTree(node.Field("tree"));
            state.Ascendancy = ReadAscendancy(node.Field("ascendancy"));
            ReadTutorial(node, state);
            var input = node.Field("inputStats");
            state.TotalInput.Keys = ReadCount(input.Field("keys"));
            state.TotalInput.LeftClicks = ReadCount(input.Field("leftClicks"));
            state.TotalInput.RightClicks = ReadCount(input.Field("rightClicks"));
            state.TotalInput.MiddleClicks = ReadCount(input.Field("middleClicks"));
            state.TotalInput.MouseMeters = input.Field("mouseMeters").AsNumber(0, 1e15);
            var presets = node.Field("presets");
            state.ActivePreset = presets.Field("active").AsInt(0, 1);
            state.OtherPreset = new BuildPreset(ReadTree(presets.Field("otherTree")));
            foreach (var item in presets.Field("otherSkills").Items())
            {
                if (!gems.TryGet(item.Field("gem").AsString(), out var skill) || skill.Skill == null)
                    continue;
                var triggerNode = item.Field("trigger");
                GemDef? trigger = null;
                if (!triggerNode.IsNull && gems.TryGet(triggerNode.AsString(), out var found) && found.Trigger != null)
                    trigger = found;
                state.OtherPreset.Skills.Add((skill, trigger));
            }
            ReadArtifacts(node.Field("artifacts"), state.Artifacts);
            if (node.HasField("rift"))
                ReadRift(node.Field("rift"), state.Rift);
            if (node.HasField("weapons"))
                ReadWeapons(node.Field("weapons"), weapons, state.Weapons);
            return state;
        }

        private static JsonValue WriteWeapons(WeaponState weapons)
        {
            var bag = new JsonValue[weapons.Bag.Count];
            for (var i = 0; i < bag.Length; i++)
                bag[i] = JsonValue.Of(weapons.Bag[i].Id);
            return Object(
                ("starterGiven", JsonValue.Of(weapons.StarterGiven)),
                ("right", weapons.Right == null ? JsonValue.Null() : JsonValue.Of(weapons.Right.Id)),
                ("left", weapons.Left == null ? JsonValue.Null() : JsonValue.Of(weapons.Left.Id)),
                ("bag", JsonValue.Array(bag)));
        }

        private static void ReadWeapons(ContentNode node, WeaponCatalog catalog, WeaponState weapons)
        {
            weapons.StarterGiven = node.Field("starterGiven").AsBool();
            weapons.Right = ReadWeapon(node.Field("right"), catalog);
            weapons.Left = ReadWeapon(node.Field("left"), catalog);
            foreach (var item in node.Field("bag").Items())
            {
                var weapon = ReadWeapon(item, catalog);
                if (weapon != null)
                    weapons.Bag.Add(weapon);
            }
        }

        private static WeaponBase? ReadWeapon(ContentNode node, WeaponCatalog catalog) =>
            !node.IsNull && catalog.TryGetBase(node.AsString(), out var weapon) ? weapon : null;

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

        private static JsonValue WriteTree(TreeAllocation tree)
        {
            var ids = new List<string>(tree.Allocated);
            ids.Sort(System.StringComparer.Ordinal);
            var nodes = new JsonValue[ids.Count];
            for (var i = 0; i < nodes.Length; i++)
            {
                var id = ids[i];
                var fields = new Dictionary<string, JsonValue> { ["id"] = JsonValue.Of(id) };
                if (tree.AttributeChoices.TryGetValue(id, out var attribute))
                    fields["attribute"] = JsonValue.Of(attribute);
                if (tree.MasteryChoices.TryGetValue(id, out var option))
                    fields["option"] = JsonValue.Of(option);
                nodes[i] = JsonValue.Object(fields);
            }
            return Object(
                ("startClass", JsonValue.Of(tree.StartClass)),
                ("pointsEarned", JsonValue.Of(tree.PointsEarned)),
                ("lastFreeResetUnixMs", JsonValue.Of(tree.LastFreeResetUnixMs)),
                ("allocated", JsonValue.Array(nodes)));
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

        private static TreeAllocation ReadTree(ContentNode node)
        {
            var tree = new TreeAllocation(node.Field("startClass").AsString())
            {
                PointsEarned = node.Field("pointsEarned").AsInt(0),
                LastFreeResetUnixMs = node.Field("lastFreeResetUnixMs").AsLong()
            };
            foreach (var item in node.Field("allocated").Items())
            {
                tree.Restore(
                    item.Field("id").AsString(),
                    item.HasField("attribute") ? item.Field("attribute").AsString() : null,
                    item.HasField("option") ? item.Field("option").AsInt(0) : (int?)null);
            }
            return tree;
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
                items[i] = Object(("gem", JsonValue.Of(skill.Id)), ("trigger", trigger == null ? JsonValue.Null() : JsonValue.Of(trigger.Id)));
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
                    ("hitsLeft", JsonValue.Of(slot.HitsLeft)));
            }
            return skills;
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
                state.Skills.Add(new SkillSlot(skill, trigger)
                {
                    CooldownTicks = item.Field("cooldownTicks").AsInt(0),
                    ActiveTicks = item.Field("activeTicks").AsInt(0),
                    HitsLeft = item.Field("hitsLeft").AsInt(0)
                });
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
