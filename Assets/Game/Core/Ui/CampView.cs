using System.Collections.Generic;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public sealed class CampStreetLine
    {
        public string Building { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public int Slots { get; internal set; }
        public bool WaitsForGuild { get; internal set; }
        public string OpensAtTier { get; internal set; } = string.Empty;
    }

    public static class CampView
    {
        public static string TierName(CampSystem camp, bool russian = true) => russian ? camp.Tier.NameRu : camp.Tier.NameEn;

        public static List<CampStreetLine> Street(CampSystem camp, bool russian = true)
        {
            var lines = new List<CampStreetLine>();
            foreach (var building in camp.Rules.Buildings)
            {
                var slots = camp.Slots(building.Id);
                var opens = string.Empty;
                if (slots == 0)
                {
                    foreach (var tier in camp.Rules.Tiers)
                    {
                        if (tier.SlotsOf(building.Id) > 0)
                        {
                            opens = russian ? tier.NameRu : tier.NameEn;
                            break;
                        }
                    }
                }
                lines.Add(new CampStreetLine
                {
                    Building = building.Id,
                    Name = russian ? building.NameRu : building.NameEn,
                    Slots = slots,
                    WaitsForGuild = slots > 0 && camp.WaitsForGuild(building.Id),
                    OpensAtTier = opens
                });
            }
            return lines;
        }
    }
}
