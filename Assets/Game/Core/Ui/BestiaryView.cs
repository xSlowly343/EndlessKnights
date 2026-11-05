using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public sealed class BestiaryLine
    {
        public string Enemy { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public long Kills { get; internal set; }
        public long KillsToOpen { get; internal set; }
        public bool Open { get; internal set; }
        public string WeakSpot { get; internal set; } = string.Empty;
        public string Text { get; internal set; } = string.Empty;
    }

    public static class BestiaryView
    {
        public static List<BestiaryLine> Lines(BestiarySystem bestiary, EnemyCatalog enemies, bool russian = true)
        {
            var lines = new List<BestiaryLine>();
            foreach (var entry in bestiary.Catalog.All)
            {
                var type = enemies.TryGet(entry.Enemy, out var enemy) ? enemy.Type : EnemyType.Normal;
                var open = bestiary.IsOpen(entry.Enemy);
                lines.Add(new BestiaryLine
                {
                    Enemy = entry.Enemy,
                    Name = russian ? entry.NameRu : entry.NameEn,
                    Kills = bestiary.KillsOf(entry.Enemy),
                    KillsToOpen = bestiary.Catalog.KillsToOpen(type),
                    Open = open,
                    WeakSpot = open ? (russian ? entry.WeakSpotRu : entry.WeakSpotEn) : string.Empty,
                    Text = open ? (russian ? entry.TextRu : entry.TextEn) : string.Empty
                });
            }
            return lines;
        }
    }
}
