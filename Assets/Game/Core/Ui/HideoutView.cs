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

        public static List<string> SharedGarden(HideoutSystem hideout, GardenRules garden)
        {
            var lines = new List<string>();
            var state = hideout.State;
            if (state.Beds.Count == 0)
            {
                lines.Add(Texts.Get("hideout.garden.none"));
                return lines;
            }
            if (state.Together)
                lines.Add(Texts.Get("hideout.garden.together"));
            for (var i = 0; i < state.Beds.Count; i++)
            {
                var bed = state.Beds[i];
                var plant = bed.Empty ? null : garden.Plant(bed.Plant);
                var name = plant == null ? bed.Plant : Texts.Pick(plant.NameRu, plant.NameEn);
                lines.Add(bed.Empty ? Texts.Format("hideout.bed.empty", i + 1)
                    : bed.Ready ? Texts.Format("hideout.bed.ready", i + 1, name)
                    : Texts.Format("hideout.bed.growing", i + 1, name, (int)System.Math.Floor(bed.Share * 100)));
            }
            if (state.Owed.Count > 0)
                lines.Add(Texts.Get("hideout.garden.owed"));
            return lines;
        }

        public static List<string> StorageLines(HideoutState state)
        {
            var lines = new List<string>();
            if (!state.StorageBuilt)
            {
                lines.Add(Texts.Get("hideout.storage.none"));
                return lines;
            }
            lines.Add(Texts.Format("hideout.storage.limit", state.TakenToday, state.TakePerDay));
            var items = new List<string>(state.Storage.Keys);
            items.Sort(System.StringComparer.Ordinal);
            foreach (var item in items)
                lines.Add(Texts.Format("hideout.storage.item", item, state.Storage[item]));
            return lines;
        }

        public static List<string> JournalLines(HideoutState state)
        {
            var lines = new List<string>();
            foreach (var entry in state.Journal)
                lines.Add(Texts.Format(entry.Amount > 0 ? "hideout.journal.put" : "hideout.journal.took", entry.Name, entry.Item, System.Math.Abs(entry.Amount)));
            return lines;
        }

        public static string Crafting(HideoutSystem hideout)
        {
            var station = hideout.Station;
            if (station.Length == 0)
                return string.Empty;
            var state = station == HideoutSystem.ForgeStation ? hideout.State.Forge : hideout.State.Cauldron;
            if (!state.Built)
                return Texts.Get("hideout.craft." + station + ".none");
            return state.Together ? Texts.Format("hideout.craft." + station + ".together", string.Join(", ", state.Partners)) : Texts.Get("hideout.craft.alone");
        }

        public static string Masterpiece(Game.Core.Content.Items.ForgeMark mark) =>
            mark.Masters.Count == 0 ? string.Empty : Texts.Format("hideout.masterpiece", string.Join(", ", mark.Masters));

        public static List<string> Dummy(HideoutState state, Game.Core.Content.Synergy.SynergyRules synergy, string ownClass)
        {
            var lines = new List<string>();
            if (!state.DummyBuilt)
            {
                lines.Add(Texts.Get("hideout.dummy.none"));
                return lines;
            }
            var mask = RoleMaskOf(synergy, ownClass);
            foreach (var corner in state.Corners)
                mask |= RoleMaskOf(synergy, corner.Hero);
            var missing = new List<string>();
            for (var index = 0; index < synergy.Roles.Count; index++)
            {
                var role = PartyCardText.Role(synergy.Roles[index]);
                if ((mask & (1 << index)) == 0)
                {
                    missing.Add(role);
                    continue;
                }
                var buffs = new List<string>();
                foreach (var modifier in synergy.RoleBuffs[index])
                    buffs.Add(Game.Core.Stats.ModifierText.Describe(modifier));
                lines.Add(Texts.Format("hideout.dummy.role", role, string.Join(", ", buffs)));
            }
            if (lines.Count == 0)
                lines.Add(Texts.Get("hideout.dummy.empty"));
            if (missing.Count > 0)
                lines.Add(Texts.Format("hideout.dummy.missing", string.Join(", ", missing)));
            return lines;
        }

        private static int RoleMaskOf(Game.Core.Content.Synergy.SynergyRules synergy, string heroName) =>
            HeroClassNames.ByName.TryGetValue(heroName, out var heroClass) ? synergy.RoleMaskOf(heroClass) : 0;

        public static List<string> Order(HideoutState state)
        {
            var lines = new List<string>();
            var order = state.Order;
            if (order.Id.Length == 0)
            {
                lines.Add(Texts.Get("hideout.order.none"));
                return lines;
            }
            lines.Add(Texts.Format(order.Done ? "hideout.order.done" : "hideout.order.title", Texts.Get("hideout.order." + order.Id), order.DaysLeft));
            foreach (var need in order.Needs)
                lines.Add(Texts.Format("hideout.order.need", Texts.Get("hideout.order.counter." + need.Counter), need.Have, need.Target));
            if (order.CanRedraw)
                lines.Add(Texts.Get("hideout.order.redraw"));
            return lines;
        }

        public static List<string> TrophyHall(HideoutState state)
        {
            var lines = new List<string>();
            var trophies = state.Trophies;
            if (!trophies.Built)
            {
                lines.Add(Texts.Get("hideout.trophies.none"));
                return lines;
            }
            lines.Add(Texts.Format("hideout.trophies.guardians", trophies.Guardians, trophies.Bosses));
            lines.Add(Texts.Format("hideout.trophies.rift", trophies.RiftDepth, trophies.Hours));
            foreach (var feat in trophies.Feats)
                lines.Add(Texts.Format("hideout.trophies.feat", PartyChronicleText.Feat(feat.Id), feat.Level));
            return lines;
        }

        public static string Pier(HideoutState state) =>
            state.PierRareBoost > 0 ? Texts.Format("hideout.pier", (int)System.Math.Round(state.PierRareBoost * 100)) : Texts.Get("hideout.pier.none");

        public static string AtTheFire(HideoutState state) =>
            state.Here.Count == 0 ? Texts.Get("hideout.fire.empty") : Texts.Format("hideout.fire", string.Join(", ", state.Here));
    }
}
