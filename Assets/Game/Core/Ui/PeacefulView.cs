using System.Collections.Generic;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public sealed class PeacefulChoiceLine
    {
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public bool On { get; internal set; }
    }

    public static class PeacefulView
    {
        public static List<PeacefulChoiceLine> Activities(PeacefulSystem peaceful, bool russian = true)
        {
            var lines = new List<PeacefulChoiceLine>();
            var current = peaceful.Activity;
            foreach (var activity in peaceful.Rules.Activities)
                lines.Add(new PeacefulChoiceLine { Id = activity.Id, Name = russian ? activity.NameRu : activity.NameEn, On = activity.Id == current });
            return lines;
        }

        public static List<PeacefulChoiceLine> Presets(PeacefulSystem peaceful, bool russian = true)
        {
            var lines = new List<PeacefulChoiceLine>();
            foreach (var preset in peaceful.Rules.Presets)
                lines.Add(new PeacefulChoiceLine { Id = preset.Id, Name = russian ? preset.NameRu : preset.NameEn, On = peaceful.State.Presets.Contains(preset.Id) });
            return lines;
        }
    }
}
