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
        public int HallLevel { get; internal set; }
        public int HallMaxLevel { get; internal set; }
        public string Status { get; internal set; } = string.Empty;
    }

    public sealed class TownBuildingLine
    {
        public string Id { get; internal set; } = string.Empty;
        public string Guild { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public string Master { get; internal set; } = string.Empty;
        public string District { get; internal set; } = string.Empty;
        public int Level { get; internal set; }
        public int MaxLevel { get; internal set; }
        public string Text { get; internal set; } = string.Empty;
    }

    public sealed class GuildPerkLine
    {
        public int Level { get; internal set; }
        public int Hall { get; internal set; }
        public bool Open { get; internal set; }
        public string Text { get; internal set; } = string.Empty;
    }

    public sealed class TownStageLine
    {
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public int Built { get; internal set; }
        public int Index { get; internal set; }
        public int Count { get; internal set; }
        public string Text { get; internal set; } = string.Empty;
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
        public string Project { get; internal set; } = string.Empty;
        public int Level { get; internal set; }
        public string Name { get; internal set; } = string.Empty;
        public long Votes { get; internal set; }
        public bool Yours { get; internal set; }
        public string Text { get; internal set; } = string.Empty;
    }

    public sealed class TownSiteLine
    {
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public string Resource { get; internal set; } = string.Empty;
        public bool Open { get; internal set; }
        public int FromStage { get; internal set; }
        public string Text { get; internal set; } = string.Empty;
    }

    public sealed class TownBuilderLine
    {
        public int Rank { get; internal set; }
        public string Name { get; internal set; } = string.Empty;
        public long Points { get; internal set; }
        public bool Yours { get; internal set; }
        public bool Hidden { get; internal set; }
        public string Text { get; internal set; } = string.Empty;
    }

    public sealed class TownQuestLine
    {
        public TownResourceKind Kind { get; internal set; }
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
                    var hall = rules.Building(guild.Id);
                    line.Guilds.Add(new GuildLine
                    {
                        Id = guild.Id,
                        Name = Texts.Pick(guild.NameRu, guild.NameEn),
                        Master = MasterName(guild.Master),
                        Level = level,
                        Built = built,
                        HallLevel = guilds.HallLevel(guild.Id),
                        HallMaxLevel = hall == null ? 0 : hall.MaxLevel,
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
                    return Texts.Format("guild.sign.gather", ProjectName(rules, board.Project));
                case TownBoardPhase.Opening:
                    return Texts.Format("guild.sign.opening", ProjectName(rules, board.Project), TimeLeft(board.SecondsLeft(secondsSinceReply)));
                default:
                    return Texts.Get("guild.project.done");
            }
        }

        public static List<TownBoardLine> Board(GuildRules rules, TownBoard board)
        {
            var lines = new List<TownBoardLine>(board.Candidates.Count);
            foreach (var candidate in board.Candidates)
            {
                var yours = candidate.Project == board.Vote;
                rules.ProjectOf(candidate.Project, out var level);
                var name = ProjectName(rules, candidate.Project);
                lines.Add(new TownBoardLine
                {
                    Project = candidate.Project,
                    Level = level,
                    Name = name,
                    Votes = candidate.Votes,
                    Yours = yours,
                    Text = Texts.Format(yours ? "guild.board.yours" : "guild.board.candidate", name, candidate.Votes)
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
                if (stock.Need == 0 && (board.Phase != TownBoardPhase.Vote || stock.Stock == 0))
                    continue;
                var resource = rules.Resource(stock.Resource);
                var name = resource == null ? stock.Resource : Texts.Pick(resource.NameRu, resource.NameEn);
                lines.Add(new TownQuestLine
                {
                    Kind = resource == null ? TownResourceKind.Produce : resource.Kind,
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

        public static string KindName(TownResourceKind kind) => Texts.Get(kind switch
        {
            TownResourceKind.Labour => "guild.kind.labour",
            TownResourceKind.Raw => "guild.kind.raw",
            TownResourceKind.Craft => "guild.kind.craft",
            _ => "guild.kind.produce"
        });

        public static List<TownSiteLine> Sites(GuildSystem guilds)
        {
            var rules = guilds.Rules;
            var lines = new List<TownSiteLine>(rules.Sites.Count);
            foreach (var site in rules.Sites)
            {
                var open = guilds.SiteOpen(site);
                var name = Texts.Pick(site.NameRu, site.NameEn);
                var resource = rules.Resource(site.Resource);
                var resourceName = resource == null ? site.Resource : Texts.Pick(resource.NameRu, resource.NameEn);
                lines.Add(new TownSiteLine
                {
                    Id = site.Id,
                    Name = name,
                    Resource = resourceName,
                    Open = open,
                    FromStage = site.FromStage,
                    Text = open ? Texts.Format("guild.site.open", name, resourceName) : Texts.Format("guild.site.closed", name, resourceName, site.FromStage)
                });
            }
            return lines;
        }

        public static string SiteOpened(GuildRules rules, long index)
        {
            if (index < 0 || index >= rules.Sites.Count)
                return string.Empty;
            var site = rules.Sites[(int)index];
            var resource = rules.Resource(site.Resource);
            return Texts.Format("guild.site.opened", Texts.Pick(site.NameRu, site.NameEn), resource == null ? site.Resource : Texts.Pick(resource.NameRu, resource.NameEn));
        }

        public static string Reward(GuildRules rules, TownBoard board)
        {
            if (board.Phase == TownBoardPhase.Done)
                return string.Empty;
            var reached = rules.TierAt(board.Points);
            var next = rules.TierAfter(board.Points);
            if (reached == null)
                return next == null ? string.Empty : Texts.Format("guild.reward.none", board.Points, Texts.Pick(next.NameRu, next.NameEn), next.Points - board.Points);
            var name = Texts.Pick(reached.NameRu, reached.NameEn);
            return next == null ? Texts.Format("guild.reward.last", board.Points, name)
                : Texts.Format("guild.reward.tier", board.Points, name, Texts.Pick(next.NameRu, next.NameEn), next.Points - board.Points);
        }

        public static string RewardOf(TownRewardTier tier) => tier.Friendship > 0
            ? Texts.Format("guild.reward.priceBoth", Texts.Pick(tier.NameRu, tier.NameEn), tier.Points, tier.Shards, tier.Friendship)
            : Texts.Format("guild.reward.price", Texts.Pick(tier.NameRu, tier.NameEn), tier.Points, tier.Shards);

        public static List<string> Roles(TownBoard board)
        {
            var lines = new List<string>(board.Roles.Count);
            foreach (var role in board.Roles)
            {
                var name = role.Name.Length > 0 ? role.Name : Texts.Get("town.top.hidden");
                lines.Add(Texts.Format("town.role." + role.Role, name, role.Amount));
            }
            return lines;
        }

        public static List<TownBuilderLine> Top(TownBoard board)
        {
            var lines = new List<TownBuilderLine>(board.Top.Count);
            foreach (var builder in board.Top)
            {
                var yours = builder.Rank == board.Rank;
                var name = builder.Name.Length > 0 ? builder.Name : Texts.Get("town.top.hidden");
                lines.Add(new TownBuilderLine
                {
                    Rank = builder.Rank,
                    Name = name,
                    Points = builder.Points,
                    Yours = yours,
                    Hidden = builder.Name.Length == 0,
                    Text = Texts.Format(yours ? "guild.top.yours" : "guild.top.line", builder.Rank, name, builder.Points)
                });
            }
            return lines;
        }

        public static string QuestTitle(GuildRules rules, TownBoard board) =>
            board.Phase == TownBoardPhase.Vote ? Texts.Get("guild.quest.early")
            : board.Phase == TownBoardPhase.Gather ? Texts.Format("guild.quest.title", ProjectName(rules, board.Project))
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

        public static string ProjectName(GuildRules rules, string project)
        {
            var building = rules.ProjectOf(project, out var level);
            if (building == null)
                return project;
            var name = Texts.Pick(building.NameRu, building.NameEn);
            return level <= 1 ? name : Texts.Format("guild.project.level", name, level);
        }

        public static List<TownBuildingLine> Buildings(GuildRules rules, ICollection<string> built)
        {
            var lines = new List<TownBuildingLine>(rules.Buildings.Count);
            foreach (var building in rules.Buildings)
            {
                var level = rules.LevelOf(building, built);
                var name = Texts.Pick(building.NameRu, building.NameEn);
                lines.Add(new TownBuildingLine
                {
                    Id = building.Id,
                    Guild = building.Guild,
                    Name = name,
                    Master = MasterName(building.Master),
                    District = building.District,
                    Level = level,
                    MaxLevel = building.MaxLevel,
                    Text = level == 0 ? Texts.Format("guild.hall.none", name) : Texts.Format("guild.hall.level", name, level, building.MaxLevel)
                });
            }
            return lines;
        }

        public static TownStageLine Stage(GuildRules rules, int built)
        {
            var stage = rules.StageAt(built);
            var next = rules.StageAfter(built);
            var name = Texts.Pick(stage.NameRu, stage.NameEn);
            var index = 0;
            while (index < rules.Stages.Count && rules.Stages[index] != stage)
                index++;
            return new TownStageLine
            {
                Id = stage.Id,
                Name = name,
                Built = built,
                Index = index,
                Count = rules.Stages.Count,
                Text = next == null ? Texts.Format("guild.stage.last", name, built)
                    : Texts.Format("guild.stage.next", name, built, Texts.Pick(next.NameRu, next.NameEn), next.Built - built)
            };
        }

        public static List<GuildPerkLine> Perks(GuildSystem guilds, string guildId)
        {
            var guild = guilds.Rules.Guild(guildId);
            var lines = new List<GuildPerkLine>();
            if (guild == null)
                return lines;
            foreach (var perk in guild.Perks)
            {
                var effect = Texts.Format("perk." + MuseumRules.NameOf(perk.Perk), Percent(perk.Value));
                lines.Add(new GuildPerkLine
                {
                    Level = perk.Level,
                    Hall = perk.Hall,
                    Open = guilds.PerkOpen(guild, perk),
                    Text = perk.Level > 0 ? Texts.Format("guild.perk.level", perk.Level, effect) : Texts.Format("guild.perk.hall", perk.Hall, effect)
                });
            }
            return lines;
        }

        public static string ForecastBonus(GuildRules rules) =>
            Texts.Format("guild.forecast.bonus", rules.Observatory.FishWeight.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture), Percent(rules.Observatory.PlantGrow), Percent(rules.Observatory.PlantMutation));

        private static string Percent(double value) => (value * 100).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

        public static string TrialOffer(GuildRules rules, string building)
        {
            var def = rules.Building(building);
            return def == null ? string.Empty : Texts.Format("guild.trial.offer", MasterName(def.Master), TimeLeft(rules.Trial.Seconds));
        }

        public static string TrialStatus(MasterTrial trial) =>
            trial.Active ? Texts.Format("guild.trial.running", Texts.Get("class." + HeroClassNames.Of(trial.Master)), TimeLeft(trial.SecondsLeft), trial.Kills, Game.Core.Math.NumberFormatter.Format(trial.Damage, Game.Core.Math.NumberDisplay.Settings)) : string.Empty;

        public static string TrialResult(MasterTrialResult result) => result switch
        {
            MasterTrialResult.NotInTown => Texts.Get("guild.trial.notInTown"),
            MasterTrialResult.NotBuilt => Texts.Get("guild.trial.notBuilt"),
            MasterTrialResult.Busy => Texts.Get("guild.trial.busy"),
            MasterTrialResult.NoMaster => Texts.Get("guild.trial.noMaster"),
            _ => string.Empty
        };

        public static string TrialEnded(long kills) => Texts.Format("guild.trial.ended", kills);

        public static string Week(GuildRules rules, TownWeek week)
        {
            if (week.Goal <= 0)
                return string.Empty;
            var decor = DecorName(rules, week.Decor);
            if (week.Done)
                return Texts.Format(week.Framed ? "guild.week.doneFramed" : "guild.week.done", decor, week.Total, week.Yours, week.MinLabour);
            return Texts.Format(week.Yours >= week.MinLabour ? "guild.week.enough" : "guild.week.gather", System.Math.Min(week.Total, week.Goal), week.Goal, week.Yours, week.MinLabour, decor);
        }

        public static List<string> Decorations(GuildRules rules, TownWeek week)
        {
            var names = new List<string>(week.Decorations.Count);
            foreach (var id in week.Decorations)
                names.Add(DecorName(rules, id));
            return names;
        }

        private static string DecorName(GuildRules rules, string id)
        {
            var decor = rules.Decor(id);
            return decor == null ? id : Texts.Pick(decor.NameRu, decor.NameEn);
        }

        public static string MasterSays(GuildRules rules, TownBoard board, long day)
        {
            if (board.Phase != TownBoardPhase.Gather && board.Phase != TownBoardPhase.Opening)
                return string.Empty;
            var building = rules.ProjectOf(board.Project, out _);
            if (building == null)
                return string.Empty;
            var line = building.Lines[(int)(((day % building.Lines.Count) + building.Lines.Count) % building.Lines.Count)];
            return Texts.Format("guild.master.says", MasterName(building.Master), Texts.Pick(line.Ru, line.En));
        }

        private static string MasterName(string id) => Texts.Get("class." + id);
    }
}
