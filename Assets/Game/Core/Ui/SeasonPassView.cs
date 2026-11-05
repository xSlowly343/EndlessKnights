using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public sealed class SeasonPassRewardLine
    {
        public string Cosmetic { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public long Crystals { get; internal set; }
        public bool Got { get; internal set; }
        public bool Locked { get; internal set; }
    }

    public sealed class SeasonPassLevelLine
    {
        public int Level { get; internal set; }
        public bool Reached { get; internal set; }
        public SeasonPassRewardLine? Free { get; internal set; }
        public SeasonPassRewardLine? Premium { get; internal set; }
    }

    public static class SeasonPassView
    {
        public static string Title(SeasonPassSystem pass, bool russian = true)
        {
            var season = pass.Season;
            return season == null ? string.Empty : russian ? season.NameRu : season.NameEn;
        }

        public static List<SeasonPassLevelLine> Lines(SeasonPassSystem pass, CosmeticCatalog cosmetics, bool russian = true)
        {
            var lines = new List<SeasonPassLevelLine>();
            var season = pass.Season;
            if (season == null)
                return lines;
            for (var level = 1; level <= pass.Rules.Levels; level++)
            {
                var free = season.RewardAt(level, false);
                var premium = season.RewardAt(level, true);
                if (free == null && premium == null)
                    continue;
                var reached = pass.Reached(level);
                lines.Add(new SeasonPassLevelLine
                {
                    Level = level,
                    Reached = reached,
                    Free = free == null ? null : Reward(free.Value, cosmetics, russian, reached, false),
                    Premium = premium == null ? null : Reward(premium.Value, cosmetics, russian, reached && pass.State.Premium, !pass.State.Premium)
                });
            }
            return lines;
        }

        private static SeasonPassRewardLine Reward(SeasonPassReward reward, CosmeticCatalog cosmetics, bool russian, bool got, bool locked)
        {
            var name = string.Empty;
            if (reward.Cosmetic.Length > 0 && cosmetics.TryGet(reward.Cosmetic, out var item))
                name = russian ? item.NameRu : item.NameEn;
            return new SeasonPassRewardLine { Cosmetic = reward.Cosmetic, Name = name, Crystals = reward.Crystals, Got = got, Locked = locked };
        }
    }
}
