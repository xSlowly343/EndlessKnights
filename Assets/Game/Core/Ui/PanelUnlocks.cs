using System.Collections.Generic;
using System.Linq;
using Game.Core.Content;

namespace Game.Core.Ui
{
    public sealed class PanelUnlocks
    {
        public const string FileName = "panel.json";
        public const int SupportedVersion = 1;
        public const long MaxStage = 1_000_000;

        private readonly long[] _stages;

        private PanelUnlocks(long[] stages)
        {
            _stages = stages;
        }

        public static PanelUnlocks AllOpen => new PanelUnlocks(FirstStage());

        public long StageFor(PanelSection section) => _stages[(int)section];

        public bool IsOpen(PanelSection section, long recordStage) => recordStage >= StageFor(section);

        public static PanelUnlocks Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var stages = FirstStage();
            var listed = new HashSet<PanelSection>();
            foreach (var item in root.Field("unlocks").Items())
            {
                var name = item.Field("section");
                if (!PanelSections.TryParse(name.AsString(), out var section))
                    throw name.Error($"unknown section '{name.AsString()}', expected one of: {string.Join(", ", PanelSections.All.Select(PanelSections.Key))}");
                if (!listed.Add(section))
                    throw name.Error($"section '{name.AsString()}' is listed twice");
                var stage = item.Field("stage").AsLong(1, MaxStage);
                if (section == PanelSections.DefaultBar[0] && stage > 1)
                    throw item.Field("stage").Error($"'{name.AsString()}' opens with the menu button and must be open from stage 1");
                stages[(int)section] = stage;
            }
            return new PanelUnlocks(stages);
        }

        private static long[] FirstStage() => Enumerable.Repeat(1L, PanelSections.All.Count).ToArray();
    }
}
