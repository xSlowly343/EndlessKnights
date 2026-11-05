using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class BestiaryEntry
    {
        public BestiaryEntry(string enemy, string nameRu, string nameEn, string weakSpotRu, string weakSpotEn, string textRu, string textEn)
        {
            Enemy = enemy;
            NameRu = nameRu;
            NameEn = nameEn;
            WeakSpotRu = weakSpotRu;
            WeakSpotEn = weakSpotEn;
            TextRu = textRu;
            TextEn = textEn;
        }

        public string Enemy { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public string WeakSpotRu { get; }
        public string WeakSpotEn { get; }
        public string TextRu { get; }
        public string TextEn { get; }
    }

    public sealed class BestiaryCatalog
    {
        public const string FileName = "bestiary.json";
        public const int SupportedVersion = 1;

        private readonly Dictionary<string, BestiaryEntry> _byEnemy;

        private BestiaryCatalog(List<BestiaryEntry> all, long normal, long boss, long guardian)
        {
            All = all;
            NormalKills = normal;
            BossKills = boss;
            GuardianKills = guardian;
            _byEnemy = new Dictionary<string, BestiaryEntry>(StringComparer.Ordinal);
            foreach (var entry in all)
                _byEnemy.Add(entry.Enemy, entry);
        }

        public IReadOnlyList<BestiaryEntry> All { get; }
        public long NormalKills { get; }
        public long BossKills { get; }
        public long GuardianKills { get; }

        public bool TryGet(string enemy, out BestiaryEntry entry) => _byEnemy.TryGetValue(enemy, out entry!);

        public long KillsToOpen(EnemyType type) => type == EnemyType.Guardian ? GuardianKills : type == EnemyType.Normal ? NormalKills : BossKills;

        public static BestiaryCatalog Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var all = new List<BestiaryEntry>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Field("entries").Items())
            {
                var enemy = node.Field("enemy").AsString();
                if (!ids.Add(enemy))
                    throw node.Field("enemy").Error($"enemy '{enemy}' is listed twice");
                var name = node.Field("name");
                var weak = node.Field("weakSpot");
                var text = node.Field("text");
                all.Add(new BestiaryEntry(enemy, name.Field("ru").AsString(), name.Field("en").AsString(), weak.Field("ru").AsString(), weak.Field("en").AsString(),
                    text.Field("ru").AsString(), text.Field("en").AsString()));
            }
            var unlock = root.Field("unlock");
            return new BestiaryCatalog(all, unlock.Field("normal").AsLong(1), unlock.Field("boss").AsLong(1), unlock.Field("guardian").AsLong(1));
        }
    }
}
