using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public sealed class CampLevelLine
    {
        public string Building { get; internal set; } = string.Empty;
        public int Level { get; internal set; }
        public string Status { get; internal set; } = string.Empty;
        public string Cost { get; internal set; } = string.Empty;
        public bool CanUpgrade { get; internal set; }
    }

    public static class CampLevelView
    {
        public static List<CampLevelLine> Lines(CampLevelSystem levels, CampSystem camp, GuildRules guilds)
        {
            var lines = new List<CampLevelLine>();
            var rules = levels.Rules;
            foreach (var def in rules.Buildings)
            {
                var level = levels.Level(def.Building);
                var result = levels.Check(def.Building);
                lines.Add(new CampLevelLine
                {
                    Building = def.Building,
                    Level = level,
                    CanUpgrade = result == CampLevelResult.Done,
                    Cost = level == 0 || level >= rules.MaxLevel ? string.Empty : Cost(def, rules.AmountsFor(level + 1), camp),
                    Status = result switch
                    {
                        CampLevelResult.NotOpen => Texts.Get("campLevel.notOpen"),
                        CampLevelResult.TopLevel => Texts.Format("campLevel.top", level),
                        CampLevelResult.GuildTooLow => Texts.Format("campLevel.guild", GuildName(guilds, def.Guild), rules.GuildLevelFor(level + 1)),
                        CampLevelResult.NotEnough => Texts.Format("campLevel.level", level, rules.MaxLevel),
                        _ => Texts.Format("campLevel.ready", level + 1)
                    }
                });
            }
            return lines;
        }

        private static string Cost(CampLevelDef def, IReadOnlyList<int> amounts, CampSystem camp)
        {
            var parts = new List<string>(def.Materials.Count);
            for (var i = 0; i < def.Materials.Count; i++)
                parts.Add(Texts.Format("campLevel.cost", def.Materials[i], camp.Count(def.Materials[i]), amounts[i]));
            return string.Join(" · ", parts);
        }

        private static string GuildName(GuildRules guilds, string id)
        {
            var guild = guilds.Guild(id);
            return guild == null ? id : Texts.Pick(guild.NameRu, guild.NameEn);
        }
    }
}
