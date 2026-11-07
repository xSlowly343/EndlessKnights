using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public sealed class GuildLine
    {
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public string Master { get; internal set; } = string.Empty;
        public int Level { get; internal set; }
        public bool Built { get; internal set; }
        public string Status { get; internal set; } = string.Empty;
    }

    public sealed class DistrictLine
    {
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public bool Open { get; internal set; }
        public string Status { get; internal set; } = string.Empty;
        public List<GuildLine> Guilds { get; } = new List<GuildLine>();
    }

    public static class GuildView
    {
        public static List<DistrictLine> Districts(GuildSystem guilds)
        {
            var rules = guilds.Rules;
            var lines = new List<DistrictLine>(rules.Districts.Count);
            foreach (var district in rules.Districts)
            {
                var open = guilds.DistrictOpen(district);
                var line = new DistrictLine
                {
                    Id = district.Id,
                    Name = Texts.Pick(district.NameRu, district.NameEn),
                    Open = open,
                    Status = open ? string.Empty : Texts.Get("guild.district.closed")
                };
                foreach (var guild in rules.Guilds)
                {
                    if (guild.District != district.Id)
                        continue;
                    var built = guilds.Built(guild.Id);
                    var level = guilds.Level(guild.Id);
                    line.Guilds.Add(new GuildLine
                    {
                        Id = guild.Id,
                        Name = Texts.Pick(guild.NameRu, guild.NameEn),
                        Master = MasterName(guild.Master),
                        Level = level,
                        Built = built,
                        Status = built ? Texts.Format("guild.level", level, rules.MaxLevel) : Texts.Get("guild.building")
                    });
                }
                lines.Add(line);
            }
            return lines;
        }

        public static string Project(GuildRules rules, string guild, long progress, long target) =>
            guild.Length == 0 ? Texts.Get("guild.project.done")
            : Texts.Format("guild.project", Name(rules, guild), progress, target);

        public static string Forecast(ObservatoryForecast forecast, FishingRules fishing, GardenRules garden)
        {
            var fish = forecast.Fish.Length > 0 ? fishing.Fish(forecast.Fish) : null;
            var plant = forecast.Plant.Length > 0 ? garden.Plant(forecast.Plant) : null;
            return Texts.Format("guild.forecast", fish == null ? "—" : Texts.Pick(fish.NameRu, fish.NameEn), plant == null ? "—" : Texts.Pick(plant.NameRu, plant.NameEn));
        }

        private static string Name(GuildRules rules, string guild)
        {
            var def = rules.Guild(guild);
            return def == null ? guild : Texts.Pick(def.NameRu, def.NameEn);
        }

        private static string MasterName(string id) => Texts.Get("class." + id);
    }
}
