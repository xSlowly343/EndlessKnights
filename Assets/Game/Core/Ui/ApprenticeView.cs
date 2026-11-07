using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public sealed class ApprenticeLine
    {
        public string Building { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public bool Hired { get; internal set; }
        public bool CanHire { get; internal set; }
        public string Status { get; internal set; } = string.Empty;
    }

    public static class ApprenticeView
    {
        public static List<ApprenticeLine> Lines(ApprenticeSystem apprentices, GuildRules guilds)
        {
            var lines = new List<ApprenticeLine>();
            var rules = apprentices.Rules;
            foreach (var def in rules.Apprentices)
            {
                var hired = apprentices.At(def.Building);
                var check = apprentices.Check(def.Building);
                lines.Add(new ApprenticeLine
                {
                    Building = def.Building,
                    Name = Texts.Pick(def.NameRu, def.NameEn),
                    Hired = hired != null,
                    CanHire = check == ApprenticeResult.Done,
                    Status = hired != null ? Working(apprentices, hired) : check switch
                    {
                        ApprenticeResult.NotOpen => Texts.Get("apprentice.notOpen"),
                        ApprenticeResult.GuildTooLow => Texts.Format("apprentice.guild", GuildName(guilds, def.Guild), rules.MinGuildLevel),
                        _ => Texts.Get("apprentice.free")
                    }
                });
            }
            return lines;
        }

        private static string Working(ApprenticeSystem apprentices, ApprenticeState apprentice)
        {
            var trait = apprentices.Rules.Trait(apprentice.Trait);
            var level = Texts.Format("apprentice.level", apprentices.Level(apprentice), apprentices.Rules.LevelHours.Count, apprentices.Pending(apprentice));
            return trait == null ? level : level + " · " + Texts.Pick(trait.NameRu, trait.NameEn);
        }

        private static string GuildName(GuildRules guilds, string id)
        {
            var guild = guilds.Guild(id);
            return guild == null ? id : Texts.Pick(guild.NameRu, guild.NameEn);
        }
    }
}
