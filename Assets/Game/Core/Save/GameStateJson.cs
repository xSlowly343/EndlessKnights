using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Json;
using Game.Core.Math;
using Game.Core.Sim;

namespace Game.Core.Save
{
    public static class GameStateJson
    {
        private static readonly Dictionary<string, HeroClass> Classes = new Dictionary<string, HeroClass>
        {
            ["typist"] = HeroClass.Typist,
            ["bard"] = HeroClass.Bard
        };

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
                    ("class", JsonValue.Of(SaveValues.NameOf(state.Class, Classes))),
                    ("focus", JsonValue.Of(state.Focus)),
                    ("focusMultiplier", JsonValue.Of(state.FocusMultiplier)),
                    ("energy", JsonValue.Of(state.Energy)),
                    ("idleTicks", JsonValue.Of(state.IdleTicks)),
                    ("macroRhythmTicks", JsonValue.Of(state.MacroRhythmTicks)),
                    ("combo", JsonValue.Of(state.Combo)),
                    ("comboIdleTicks", JsonValue.Of(state.ComboIdleTicks)),
                    ("notes", JsonValue.Of(state.Notes)))),
                ("stage", Object(
                    ("current", JsonValue.Of(state.Stage)),
                    ("highest", JsonValue.Of(state.HighestStage)),
                    ("kills", JsonValue.Of(state.KillsOnStage)),
                    ("bossTicksLeft", JsonValue.Of(state.BossTicksLeft)),
                    ("farming", JsonValue.Of(state.Farming)),
                    ("farmKills", JsonValue.Of(state.FarmKills)),
                    ("bossFailsInARow", JsonValue.Of(state.BossFailsInARow)))),
                ("enemy", Object(
                    ("id", state.Enemy == null ? JsonValue.Null() : JsonValue.Of(state.Enemy.Id)),
                    ("type", JsonValue.Of(SaveValues.NameOf(state.EnemyType, EnemyTypes))),
                    ("hp", SaveValues.Of(state.EnemyHp)),
                    ("maxHp", SaveValues.Of(state.EnemyMaxHp)))),
                ("gold", Object(
                    ("wallet", SaveValues.Of(state.Gold)),
                    ("runEarned", SaveValues.Of(state.RunGoldEarned)))),
                ("recentDps", SaveValues.Of(state.RecentDps)),
                ("carry", Object(
                    ("keys", JsonValue.Of(state.KeyCarry)),
                    ("clicks", JsonValue.Of(state.ClickCarry)),
                    ("mousePx", JsonValue.Of(state.MouseCarryPx)))));
        }

        public static GameState Read(ContentNode node, EnemyCatalog enemies)
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
                Class = SaveValues.ReadEnum(hero.Field("class"), Classes),
                Focus = hero.Field("focus").AsNumber(0, 1e6),
                FocusMultiplier = hero.Field("focusMultiplier").AsNumber(0, 1e6),
                Energy = hero.Field("energy").AsNumber(0, 1e9),
                IdleTicks = hero.Field("idleTicks").AsLong(0),
                MacroRhythmTicks = hero.Field("macroRhythmTicks").AsLong(0),
                Combo = hero.Field("combo").AsInt(0),
                ComboIdleTicks = hero.Field("comboIdleTicks").AsLong(0),
                Notes = hero.Field("notes").AsInt(0),
                Stage = stage.Field("current").AsLong(1),
                HighestStage = stage.Field("highest").AsLong(1),
                KillsOnStage = stage.Field("kills").AsInt(0),
                BossTicksLeft = stage.Field("bossTicksLeft").AsInt(0),
                Farming = stage.Field("farming").AsBool(),
                FarmKills = stage.Field("farmKills").AsInt(0),
                BossFailsInARow = stage.Field("bossFailsInARow").AsInt(0),
                Gold = SaveValues.ReadBigNumber(gold.Field("wallet")),
                RunGoldEarned = SaveValues.ReadBigNumber(gold.Field("runEarned")),
                RecentDps = SaveValues.ReadBigNumber(node.Field("recentDps")),
                KeyCarry = carry.Field("keys").AsNumber(0, 1e6),
                ClickCarry = carry.Field("clicks").AsNumber(0, 1e6),
                MouseCarryPx = carry.Field("mousePx").AsNumber(0, 1e9)
            };
            if (state.HighestStage < state.Stage)
                throw stage.Field("highest").Error($"must not be lower than current ({state.Stage})");

            state.SpawnEnemy(ReadEnemyDef(enemy.Field("id"), enemies), SaveValues.ReadEnum(enemy.Field("type"), EnemyTypes),
                SaveValues.ReadBigNumber(enemy.Field("maxHp")));
            state.EnemyHp = SaveValues.ReadBigNumber(enemy.Field("hp"));
            return state;
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
