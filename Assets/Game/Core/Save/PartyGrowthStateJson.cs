using System.Collections.Generic;
using System.Globalization;
using Game.Core.Content;
using Game.Core.Content.Json;
using Game.Core.Party;

namespace Game.Core.Save
{
    public static class PartyGrowthStateJson
    {
        public static JsonValue Write(PartyGrowthSave saved)
        {
            var tree = new List<int>(saved.TreeLevels.Keys);
            tree.Sort();
            var nodes = new JsonValue[tree.Count];
            for (var i = 0; i < nodes.Length; i++)
                nodes[i] = JsonValue.Object(new Dictionary<string, JsonValue> { ["node"] = JsonValue.Of(tree[i]), ["level"] = JsonValue.Of(saved.TreeLevels[tree[i]]) });
            var best = new JsonValue[saved.TrialBest.Count];
            for (var i = 0; i < best.Length; i++)
                best[i] = JsonValue.Of(saved.TrialBest[i]);
            var map = new Dictionary<string, JsonValue>
            {
                ["inParty"] = JsonValue.Of(saved.InParty),
                ["joinedUnixMs"] = Time(saved.JoinedUnixMs),
                ["members"] = JsonValue.Of(saved.Members),
                ["tree"] = JsonValue.Array(nodes),
                ["majors"] = Texts(saved.TreeMajors),
                ["trialBest"] = JsonValue.Array(best),
                ["relics"] = Levels(saved.RelicLevels),
                ["mascotLevel"] = JsonValue.Of(saved.MascotLevel),
                ["rites"] = Levels(saved.RiteLevels),
                ["crests"] = Texts(saved.Crests),
                ["partySets"] = Levels(saved.PartySetCounts),
                ["answeredUnixMs"] = Time(saved.AnsweredUnixMs),
                ["seenUnixMs"] = Time(saved.SeenUnixMs)
            };
            if (saved.MascotBeast.Length > 0)
                map["mascot"] = JsonValue.Of(saved.MascotBeast);
            return JsonValue.Object(map);
        }

        public static void Read(ContentNode node, PartyGrowthSave saved)
        {
            saved.Clear();
            saved.InParty = node.Field("inParty").AsBool();
            saved.JoinedUnixMs = ReadTime(node.Field("joinedUnixMs"));
            saved.Members = node.Field("members").AsInt(0, 1000);
            foreach (var item in node.Field("tree").Items())
                saved.TreeLevels[item.Field("node").AsInt(1, 10_000)] = item.Field("level").AsInt(0, 1000);
            ReadTexts(node.Field("majors"), saved.TreeMajors);
            foreach (var item in node.Field("trialBest").Items())
                saved.TrialBest.Add(item.AsInt(0, 100_000));
            ReadLevels(node.Field("relics"), saved.RelicLevels);
            saved.MascotBeast = node.HasField("mascot") ? node.Field("mascot").AsString() : string.Empty;
            saved.MascotLevel = node.Field("mascotLevel").AsInt(0, 100_000);
            ReadLevels(node.Field("rites"), saved.RiteLevels);
            ReadTexts(node.Field("crests"), saved.Crests);
            ReadLevels(node.Field("partySets"), saved.PartySetCounts);
            saved.AnsweredUnixMs = ReadTime(node.Field("answeredUnixMs"));
            saved.SeenUnixMs = ReadTime(node.Field("seenUnixMs"));
        }

        private static JsonValue Time(long unixMs) => JsonValue.Of(unixMs.ToString(CultureInfo.InvariantCulture));

        private static long ReadTime(ContentNode node)
        {
            if (!long.TryParse(node.AsString(), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                throw node.Error("expected a time in milliseconds");
            return value;
        }

        private static JsonValue Texts(List<string> texts)
        {
            var items = new JsonValue[texts.Count];
            for (var i = 0; i < items.Length; i++)
                items[i] = JsonValue.Of(texts[i]);
            return JsonValue.Array(items);
        }

        private static void ReadTexts(ContentNode node, List<string> texts)
        {
            foreach (var item in node.Items())
                texts.Add(item.AsString());
        }

        private static JsonValue Levels(Dictionary<string, int> levels)
        {
            var ids = new List<string>(levels.Keys);
            ids.Sort(System.StringComparer.Ordinal);
            var items = new JsonValue[ids.Count];
            for (var i = 0; i < items.Length; i++)
                items[i] = JsonValue.Object(new Dictionary<string, JsonValue> { ["id"] = JsonValue.Of(ids[i]), ["level"] = JsonValue.Of(levels[ids[i]]) });
            return JsonValue.Array(items);
        }

        private static void ReadLevels(ContentNode node, Dictionary<string, int> levels)
        {
            foreach (var item in node.Items())
                levels[item.Field("id").AsString()] = item.Field("level").AsInt(0, 1_000_000);
        }
    }
}
