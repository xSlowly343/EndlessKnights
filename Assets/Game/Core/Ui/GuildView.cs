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

    public sealed class TownBoardLine
    {
        public string Guild { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public long Votes { get; internal set; }
        public bool Yours { get; internal set; }
        public string Text { get; internal set; } = string.Empty;
    }

    public sealed class TownQuestLine
    {
        public string Resource { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public long Stock { get; internal set; }
        public long Need { get; internal set; }
        public long Yours { get; internal set; }
        public long LeftToday { get; internal set; }
        public bool Done { get; internal set; }
        public string Text { get; internal set; } = string.Empty;
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

        public static string Sign(GuildRules rules, TownBoard board, double secondsSinceReply)
        {
            switch (board.Phase)
            {
                case TownBoardPhase.Vote:
                    return Texts.Format("guild.sign.vote", TimeLeft(board.SecondsLeft(secondsSinceReply)));
                case TownBoardPhase.Gather:
                    return Texts.Format("guild.sign.gather", Name(rules, board.Project));
                case TownBoardPhase.Opening:
                    return Texts.Format("guild.sign.opening", Name(rules, board.Project), TimeLeft(board.SecondsLeft(secondsSinceReply)));
                default:
                    return Texts.Get("guild.project.done");
            }
        }

        public static List<TownBoardLine> Board(GuildRules rules, TownBoard board)
        {
            var lines = new List<TownBoardLine>(board.Candidates.Count);
            foreach (var candidate in board.Candidates)
            {
                var yours = candidate.Guild == board.Vote;
                lines.Add(new TownBoardLine
                {
                    Guild = candidate.Guild,
                    Name = Name(rules, candidate.Guild),
                    Votes = candidate.Votes,
                    Yours = yours,
                    Text = Texts.Format(yours ? "guild.board.yours" : "guild.board.candidate", Name(rules, candidate.Guild), candidate.Votes)
                });
            }
            return lines;
        }

        public static List<TownQuestLine> Quest(GuildRules rules, TownBoard board)
        {
            var lines = new List<TownQuestLine>(board.Stocks.Count);
            if (board.Phase == TownBoardPhase.Done)
                return lines;
            foreach (var stock in board.Stocks)
            {
                if (stock.Need == 0 && board.Phase != TownBoardPhase.Vote)
                    continue;
                var resource = rules.Resource(stock.Resource);
                var name = resource == null ? stock.Resource : Texts.Pick(resource.NameRu, resource.NameEn);
                lines.Add(new TownQuestLine
                {
                    Resource = stock.Resource,
                    Name = name,
                    Stock = stock.Stock,
                    Need = stock.Need,
                    Yours = stock.Yours,
                    LeftToday = stock.LeftToday,
                    Done = stock.Need > 0 && stock.Gathered,
                    Text = stock.Need > 0 ? Texts.Format("guild.quest.need", name, System.Math.Min(stock.Stock, stock.Need), stock.Need, stock.Yours)
                        : Texts.Format("guild.quest.stock", name, stock.Stock, stock.Yours)
                });
            }
            return lines;
        }

        public static string QuestTitle(GuildRules rules, TownBoard board) =>
            board.Phase == TownBoardPhase.Vote ? Texts.Get("guild.quest.early")
            : board.Phase == TownBoardPhase.Gather ? Texts.Format("guild.quest.title", Name(rules, board.Project))
            : board.Phase == TownBoardPhase.Opening ? Texts.Get("guild.quest.next")
            : string.Empty;

        public static string TimeLeft(double seconds)
        {
            var hours = (long)System.Math.Floor(seconds / 3600);
            if (hours < 24)
                return OfflineReport.Duration(seconds);
            return hours % 24 == 0 ? Texts.Format("unit.days", hours / 24) : Texts.Format("unit.daysHours", hours / 24, hours % 24);
        }

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
