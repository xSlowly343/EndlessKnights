using System.Collections.Generic;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public sealed class DecorSpotLine
    {
        public int Spot { get; internal set; }
        public string Decor { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
    }

    public sealed class DecorItemLine
    {
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public bool Owned { get; internal set; }
        public bool CanCraft { get; internal set; }
        public bool Placed { get; internal set; }
    }

    public sealed class MonumentLine
    {
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public int Level { get; internal set; }
        public double Value { get; internal set; }
        public double Next { get; internal set; }
    }

    public sealed class CampSnapshot
    {
        public string Theme { get; internal set; } = string.Empty;
        public IReadOnlyList<string> Decor { get; internal set; } = System.Array.Empty<string>();
        public IReadOnlyList<(string Id, int Level)> Monuments { get; internal set; } = System.Array.Empty<(string, int)>();
        public string Title { get; internal set; } = string.Empty;
        public IReadOnlyList<string> Trophies { get; internal set; } = System.Array.Empty<string>();
        public string Brand { get; internal set; } = string.Empty;
    }

    public static class DecorView
    {
        public static List<DecorSpotLine> Street(DecorSystem decor, bool russian = true)
        {
            var lines = new List<DecorSpotLine>();
            for (var spot = 0; spot < decor.Spots; spot++)
            {
                var id = spot < decor.State.Placed.Count ? decor.State.Placed[spot] : string.Empty;
                var item = decor.Rules.Item(id);
                lines.Add(new DecorSpotLine { Spot = spot, Decor = id, Name = item == null ? string.Empty : russian ? item.NameRu : item.NameEn });
            }
            return lines;
        }

        public static List<DecorItemLine> Items(DecorSystem decor, bool russian = true)
        {
            var lines = new List<DecorItemLine>();
            foreach (var item in decor.Rules.Items)
            {
                var owned = decor.Owns(item.Id);
                lines.Add(new DecorItemLine
                {
                    Id = item.Id,
                    Name = russian ? item.NameRu : item.NameEn,
                    Owned = owned,
                    CanCraft = !owned && decor.CanCraft(item),
                    Placed = decor.State.Placed.Contains(item.Id)
                });
            }
            return lines;
        }

        public static List<MonumentLine> Monuments(DecorSystem decor, bool russian = true)
        {
            var lines = new List<MonumentLine>();
            foreach (var monument in decor.Rules.Monuments)
            {
                var level = decor.MonumentLevel(monument);
                lines.Add(new MonumentLine
                {
                    Id = monument.Id,
                    Name = russian ? monument.NameRu : monument.NameEn,
                    Level = level,
                    Value = decor.ValueOf(monument.Input),
                    Next = level < monument.Steps.Count ? monument.Steps[level] : 0
                });
            }
            return lines;
        }

        public static CampSnapshot Snapshot(DecorSystem decor, MuseumSystem museum, GameState state)
        {
            var placed = new List<string>();
            for (var spot = 0; spot < decor.Spots && spot < decor.State.Placed.Count; spot++)
                placed.Add(decor.State.Placed[spot]);
            var monuments = new List<(string, int)>();
            foreach (var monument in decor.Rules.Monuments)
                monuments.Add((monument.Id, decor.MonumentLevel(monument)));
            var trophies = new List<string>();
            foreach (var id in museum.State.Shelf)
            {
                var collection = museum.Rules.Collection(id);
                if (collection != null)
                    trophies.Add(collection.Trophy);
            }
            return new CampSnapshot
            {
                Theme = decor.State.Theme,
                Decor = placed,
                Monuments = monuments,
                Title = museum.State.Title,
                Trophies = trophies,
                Brand = state.Weapons.Held?.Forge.Brand ?? string.Empty
            };
        }
    }
}
