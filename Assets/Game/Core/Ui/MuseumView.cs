using System.Collections.Generic;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public sealed class CollectionLine
    {
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public int Collected { get; internal set; }
        public int Total { get; internal set; }
        public bool Complete { get; internal set; }
        public string Trophy { get; internal set; } = string.Empty;
        public bool OnShelf { get; internal set; }
        public string Title { get; internal set; } = string.Empty;
    }

    public static class MuseumView
    {
        public static List<CollectionLine> Collections(MuseumSystem museum, bool russian = true)
        {
            var lines = new List<CollectionLine>();
            foreach (var collection in museum.Rules.Collections)
            {
                lines.Add(new CollectionLine
                {
                    Id = collection.Id,
                    Name = russian ? collection.NameRu : collection.NameEn,
                    Collected = museum.Collected(collection),
                    Total = collection.Items.Count,
                    Complete = museum.Complete(collection.Id),
                    Trophy = collection.Trophy,
                    OnShelf = museum.State.Shelf.Contains(collection.Id),
                    Title = russian ? collection.TitleRu : collection.TitleEn
                });
            }
            return lines;
        }

        public static string Title(MuseumSystem museum, bool russian = true)
        {
            var collection = museum.Rules.Collection(museum.State.Title);
            return collection == null ? string.Empty : russian ? collection.TitleRu : collection.TitleEn;
        }
    }
}
