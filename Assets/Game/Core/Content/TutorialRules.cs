using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class TutorialRules
    {
        public const string FileName = "tutorial.json";
        public const int SupportedVersion = 1;
        public const int MaxTextLength = 60;

        private static readonly Dictionary<string, TutorialShow> ShowNames = new Dictionary<string, TutorialShow>(StringComparer.Ordinal)
        {
            ["start"] = TutorialShow.Start,
            ["kills"] = TutorialShow.Kills,
            ["bossStarted"] = TutorialShow.BossStarted,
            ["bossFailed"] = TutorialShow.BossFailed,
            ["levelUp"] = TutorialShow.LevelUp,
            ["upgradeAffordable"] = TutorialShow.UpgradeAffordable
        };

        private static readonly Dictionary<string, TutorialDone> DoneNames = new Dictionary<string, TutorialDone>(StringComparer.Ordinal)
        {
            ["keys"] = TutorialDone.Keys,
            ["mousePx"] = TutorialDone.MousePx,
            ["skillCast"] = TutorialDone.SkillCast,
            ["bossEnded"] = TutorialDone.BossEnded,
            ["seconds"] = TutorialDone.Seconds,
            ["treeNode"] = TutorialDone.TreeNode,
            ["strikeUpgrade"] = TutorialDone.StrikeUpgrade
        };

        private TutorialRules(IReadOnlyList<TutorialStep> steps)
        {
            Steps = steps;
        }

        public IReadOnlyList<TutorialStep> Steps { get; }

        public static TutorialRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var steps = new List<TutorialStep>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Field("steps").Items())
            {
                var id = item.Field("id").AsString();
                if (!ids.Add(id))
                    throw item.Field("id").Error($"step '{id}' is listed twice");
                var text = item.Field("text").AsString();
                if (text.Length == 0 || text.Length > MaxTextLength)
                    throw item.Field("text").Error($"must be 1 to {MaxTextLength} characters to fit the strip");
                var show = item.Field("show");
                var done = item.Field("done");
                steps.Add(new TutorialStep(
                    id,
                    text,
                    Read(show.Field("when"), ShowNames),
                    show.HasField("count") ? show.Field("count").AsInt(1, 10000) : 1,
                    Read(done.Field("when"), DoneNames),
                    done.HasField("count") ? done.Field("count").AsNumber(1, 1e9) : 1,
                    item.Field("maxSeconds").AsNumber(1, 3600)));
            }
            return new TutorialRules(steps);
        }

        private static T Read<T>(ContentNode node, Dictionary<string, T> names)
        {
            var name = node.AsString();
            if (names.TryGetValue(name, out var value))
                return value;
            throw node.Error($"unknown value '{name}', expected one of: {string.Join(", ", names.Keys)}");
        }
    }
}
