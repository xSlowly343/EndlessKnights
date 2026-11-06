using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public sealed class EventGoalLine
    {
        public string Id { get; internal set; } = string.Empty;
        public EventGoalKind Kind { get; internal set; }
        public long Progress { get; internal set; }
        public long Target { get; internal set; }
        public bool Done { get; internal set; }
    }

    public sealed class EventShopLine
    {
        public string Cosmetic { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public long Price { get; internal set; }
        public bool Owned { get; internal set; }
    }

    public static class EventView
    {
        public static string Title(EventSystem events, bool russian = true)
        {
            var window = events.ShopWindow;
            return window == null ? string.Empty : russian ? window.Value.Event.NameRu : window.Value.Event.NameEn;
        }

        public static List<EventGoalLine> Goals(EventSystem events)
        {
            var lines = new List<EventGoalLine>();
            if (!events.Active)
                return lines;
            foreach (var goal in events.Rules.Goals)
            {
                lines.Add(new EventGoalLine
                {
                    Id = goal.Id,
                    Kind = goal.Kind,
                    Progress = System.Math.Min(goal.Target, events.Progress(goal.Kind)),
                    Target = goal.Target,
                    Done = events.State.GoalsDone.Contains(goal.Id)
                });
            }
            return lines;
        }

        public static List<EventShopLine> Shop(EventSystem events, CosmeticSystem cosmetics, bool russian = true)
        {
            var lines = new List<EventShopLine>();
            var window = events.ShopWindow;
            if (window == null)
                return lines;
            foreach (var item in window.Value.Event.Shop)
            {
                var name = cosmetics.Catalog.TryGet(item.Cosmetic, out var cosmetic) ? (russian ? cosmetic.NameRu : cosmetic.NameEn) : item.Cosmetic;
                lines.Add(new EventShopLine { Cosmetic = item.Cosmetic, Name = name, Price = item.Price, Owned = cosmetics.Owns(item.Cosmetic) });
            }
            return lines;
        }
    }
}
