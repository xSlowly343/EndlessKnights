using System.Collections.Generic;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public sealed class CompanionCard
    {
        public string Companion { get; internal set; } = string.Empty;
        public string Biome { get; internal set; } = string.Empty;
        public int Level { get; internal set; }
        public int MaxLevel { get; internal set; }
        public double Experience { get; internal set; }
        public double Need { get; internal set; }
        public double LootBonus { get; internal set; }
        public double ShorterCut { get; internal set; }
        public double FoodLeft { get; internal set; }
        public string Favourite { get; internal set; } = string.Empty;
        public string Stage { get; internal set; } = string.Empty;
        public string NextStage { get; internal set; } = string.Empty;
        public int NextLevel { get; internal set; }
        public CompanionResult Evolve { get; internal set; }
        public int Hearts { get; internal set; }
        public double Bond { get; internal set; }
        public double BondNext { get; internal set; }
        public string Name { get; internal set; } = string.Empty;
        public string Accessory { get; internal set; } = string.Empty;
        public bool CanRename { get; internal set; }
        public int Gestures { get; internal set; }
        public bool FireSeat { get; internal set; }
        public bool CanChooseAccessory { get; internal set; }
        public bool BestFriend { get; internal set; }
        public int StoryLetters { get; internal set; }
        public int Stars { get; internal set; }
        public int OwnSparks { get; internal set; }
        public string Coat { get; internal set; } = string.Empty;
        public string Rarity { get; internal set; } = string.Empty;
        public string Form { get; internal set; } = string.Empty;
    }

    public sealed class AlbumCard
    {
        public string Companion { get; internal set; } = string.Empty;
        public bool Known { get; internal set; }
        public string Source { get; internal set; } = string.Empty;
        public string Biome { get; internal set; } = string.Empty;
        public string Rarity { get; internal set; } = string.Empty;
        public int Stars { get; internal set; }
        public int MaxStars { get; internal set; }
        public bool Paid { get; internal set; }
        public CompanionCard? Card { get; internal set; }
    }

    public sealed class CompanionAlbum
    {
        public IReadOnlyList<AlbumCard> Cards { get; internal set; } = System.Array.Empty<AlbumCard>();
        public int Stars { get; internal set; }
        public int FreeStars { get; internal set; }
        public int MaxFreeStars { get; internal set; }
        public int TripSlots { get; internal set; }
        public int NestBonusSlots { get; internal set; }
        public double FoodExperience { get; internal set; }
        public bool Title { get; internal set; }
        public int NextStars { get; internal set; }
        public string NextBonus { get; internal set; } = string.Empty;
    }

    public static class CompanionView
    {
        public static CompanionAlbum Album(CompanionSystem companions)
        {
            var rules = companions.Rules;
            var cards = new List<AlbumCard>();
            foreach (var def in rules.Companions)
            {
                var known = companions.Known(def.Id);
                var paid = false;
                foreach (var source in rules.Album.PaidSources)
                    paid |= source == def.Source;
                cards.Add(new AlbumCard
                {
                    Companion = def.Id,
                    Known = known,
                    Source = def.Source,
                    Biome = def.Biome,
                    Rarity = def.Rarity,
                    Stars = companions.Stars(def.Id),
                    MaxStars = rules.Stages.Count + 1 + rules.Forms.Forms.Count,
                    Paid = paid,
                    Card = known ? Card(companions, def.Id) : null
                });
            }
            var next = companions.NextAlbumBonus();
            return new CompanionAlbum
            {
                Cards = cards,
                Stars = companions.AlbumStars(),
                FreeStars = companions.AlbumStars(true),
                MaxFreeStars = companions.MaxAlbumStars(true),
                TripSlots = companions.TripSlots,
                NestBonusSlots = companions.NestBonusSlots,
                FoodExperience = companions.AlbumBonus(Content.AlbumBonusKind.FoodExperience),
                Title = companions.AlbumTitle,
                NextStars = next?.Stars ?? 0,
                NextBonus = next == null ? string.Empty : NextBonusId(next.Kind)
            };
        }

        private static string NextBonusId(Content.AlbumBonusKind kind) => kind switch
        {
            Content.AlbumBonusKind.TripSlot => "tripSlot",
            Content.AlbumBonusKind.FoodExperience => "foodExperience",
            Content.AlbumBonusKind.NestSlot => "nestSlot",
            _ => "title"
        };

        public static CompanionCard Card(CompanionSystem companions, string companion)
        {
            var rules = companions.Rules;
            var def = rules.Companion(companion);
            var level = companions.LevelOf(companion);
            var known = companions.State.Progress.TryGetValue(companion, out var progress) && progress.FavouriteKnown;
            var hearts = companions.Hearts(companion);
            var bond = rules.Bond;
            return new CompanionCard
            {
                Companion = companion,
                Biome = def?.Biome ?? string.Empty,
                Level = level,
                MaxLevel = companions.MaxLevelOf(companion),
                Experience = progress?.Experience ?? 0,
                Need = level >= companions.MaxLevelOf(companion) ? 0 : rules.ExperienceFor(level + 1),
                Stars = companions.StarsOf(companion),
                OwnSparks = progress?.OwnSparks ?? 0,
                Coat = progress?.Coat ?? string.Empty,
                Rarity = def?.Rarity ?? string.Empty,
                Form = progress?.Form ?? string.Empty,
                LootBonus = companions.LootBonus(companion),
                ShorterCut = companions.ShorterCut(companion),
                FoodLeft = companions.FoodLeft(companion),
                Favourite = known ? def?.Favourite ?? string.Empty : string.Empty,
                Stage = companions.Stage(companion).Id,
                NextStage = companions.NextStage(companion)?.Id ?? string.Empty,
                NextLevel = companions.NextStage(companion)?.Level ?? 0,
                Evolve = companions.CanEvolve(companion),
                Hearts = hearts,
                Bond = progress?.Bond ?? 0,
                BondNext = hearts >= bond.MaxHearts ? 0 : bond.PointsTo(hearts + 1),
                Name = progress?.Name ?? string.Empty,
                Accessory = progress?.Accessory ?? string.Empty,
                CanRename = hearts >= bond.RenameHearts,
                Gestures = (hearts >= bond.GestureHearts ? 1 : 0) + (hearts >= bond.SecondGestureHearts ? 1 : 0),
                FireSeat = hearts >= bond.FireSeatHearts,
                CanChooseAccessory = hearts >= bond.AccessoryHearts,
                BestFriend = hearts >= bond.BestFriendHearts,
                StoryLetters = progress?.Story ?? 0
            };
        }

        public static string PairScene(CompanionSystem companions, string a, string b, bool russian = true)
        {
            var index = companions.PairIndex(a, b);
            if (index < 0)
                return string.Empty;
            var pair = companions.Rules.Pairs[index];
            return russian ? pair.SceneRu : pair.SceneEn;
        }

        public static List<CompanionCard> All(CompanionSystem companions, ExpeditionSystem expeditions)
        {
            var cards = new List<CompanionCard>();
            foreach (var companion in expeditions.Companions())
                cards.Add(Card(companions, companion));
            return cards;
        }
    }
}
