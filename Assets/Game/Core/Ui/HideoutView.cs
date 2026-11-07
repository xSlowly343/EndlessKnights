using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
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
            return lines;
        }

        public static string AtTheFire(HideoutState state) =>
            state.Here.Count == 0 ? Texts.Get("hideout.fire.empty") : Texts.Format("hideout.fire", string.Join(", ", state.Here));
    }
}
