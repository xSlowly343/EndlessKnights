using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public sealed class HideoutBuildingLine
    {
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public int Level { get; internal set; }
        public string Status { get; internal set; } = string.Empty;
        public bool CanQueue { get; internal set; }
        public int Wants { get; internal set; }
        public bool IWant { get; internal set; }
    }

    public static class HideoutView
    {
        public static string TierName(HideoutSystem hideout) =>
            hideout.State.Tier <= 0 ? Texts.Get("hideout.none") : Texts.Pick(hideout.Rules.Tiers[hideout.State.Tier - 1].NameRu, hideout.Rules.Tiers[hideout.State.Tier - 1].NameEn);

        public static List<string> Buildings(HideoutSystem hideout)
        {
            var names = new List<string>();
            foreach (var id in hideout.Open())
            {
                var building = hideout.Rules.Building(id);
                names.Add(building == null ? id : Texts.Pick(building.NameRu, building.NameEn));
            }
            return names;
        }

        public static List<string> NextTier(HideoutSystem hideout)
        {
            var lines = new List<string>();
            var tier = hideout.NextTier;
            if (tier == null)
                return lines;
            var state = hideout.State;
            lines.Add(Texts.Format("hideout.next", Texts.Pick(tier.NameRu, tier.NameEn)));
            if (tier.Members > 1)
                lines.Add(Texts.Format("hideout.need.members", System.Math.Min(state.Members, tier.Members), tier.Members));
            if (tier.Bosses > 0)
                lines.Add(Texts.Format("hideout.need.bosses", System.Math.Min(state.Bosses, tier.Bosses), tier.Bosses));
            if (tier.SitHours > 0)
                lines.Add(Texts.Format("hideout.need.sit", System.Math.Min(state.SitSeconds / 3600.0, tier.SitHours).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture), tier.SitHours));
            if (tier.Orders > 0)
                lines.Add(Texts.Format("hideout.need.orders", System.Math.Min(state.Orders, tier.Orders), tier.Orders));
            if (tier.FireLevel > 0)
                lines.Add(Texts.Format("hideout.need.fire", tier.FireLevel));
            foreach (var need in tier.Needs)
                lines.Add(Texts.Format("hideout.need." + need.Counter, need.Target));
            return lines;
        }

        public static List<HideoutBuildingLine> Plan(HideoutSystem hideout)
        {
            var lines = new List<HideoutBuildingLine>();
            var state = hideout.State;
            foreach (var id in hideout.Open())
            {
                var building = hideout.Rules.Building(id);
                if (building == null || building.Personal || lines.Exists(line => line.Id == id))
                    continue;
                var level = hideout.LevelOf(id);
                state.Wants.TryGetValue(id, out var wants);
                lines.Add(new HideoutBuildingLine
                {
                    Id = id,
                    Name = Texts.Pick(building.NameRu, building.NameEn),
                    Level = level,
                    Status = state.Project == id ? Texts.Get("hideout.building.underWay")
                        : level >= building.Levels.Count ? Texts.Get("hideout.building.top")
                        : state.CanQueue.Contains(id) ? Texts.Format("hideout.building.cost", building.Levels[level].Points, building.Levels[level].Hours)
                        : Texts.Get("hideout.building.waits"),
                    CanQueue = state.Leader && state.Project.Length == 0 && state.CanQueue.Contains(id),
                    Wants = wants,
                    IWant = state.MyWants.Contains(id)
                });
            }
            return lines;
        }

        public static string Project(HideoutSystem hideout)
        {
            var state = hideout.State;
            if (state.Project.Length == 0)
                return Texts.Get(state.Leader ? "hideout.project.pick" : "hideout.project.none");
            var building = hideout.Rules.Building(state.Project);
            var name = building == null ? state.Project : Texts.Pick(building.NameRu, building.NameEn);
            return Texts.Format("hideout.project", name, state.ProjectLevel) + " " + Texts.Format("hideout.project.progress", state.ProjectPoints, state.ProjectTarget, System.Math.Min(state.HoursTogether, state.ProjectHours), state.ProjectHours);
        }

        public static string MyCorner(HideoutSystem hideout) =>
            hideout.CornerOpen ? Texts.Format("hideout.corner", hideout.Corner.Count, hideout.CornerDecor) : Texts.Get(hideout.State.Tier > 0 ? "hideout.corner.full" : "hideout.corner.none");

        public static List<string> Corners(HideoutState state)
        {
            var lines = new List<string>();
            foreach (var corner in state.Corners)
            {
                var hero = corner.Path.Length > 0 ? corner.Hero + " · " + corner.Path : corner.Hero;
                lines.Add(Texts.Format("hideout.corner.of", corner.Name, hero, corner.Decor.Count));
            }
            return lines;
        }

        public static string AtTheFire(HideoutState state) =>
            state.Here.Count == 0 ? Texts.Get("hideout.fire.empty") : Texts.Format("hideout.fire", string.Join(", ", state.Here));
    }
}
