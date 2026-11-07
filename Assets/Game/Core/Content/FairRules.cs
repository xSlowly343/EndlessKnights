using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class FairContest
    {
        public FairContest(string id, string nameRu, string nameEn)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
    }

    public sealed class FairRules
    {
        public const string FileName = "fair.json";
        public const int SupportedVersion = 1;
        public const string BigFish = "big_fish";
        public const string BestGarden = "best_garden";

        public IReadOnlyList<FairContest> Contests { get; private set; } = Array.Empty<FairContest>();
        public int MaxHarvestsPerDay { get; private set; }
        public int MaxMutationsPerDay { get; private set; }

        public FairContest? Contest(string id)
        {
            foreach (var contest in Contests)
            {
                if (contest.Id == id)
                    return contest;
            }
            return null;
        }

        public static FairRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var contests = new List<FairContest>();
            foreach (var node in root.Field("contests").Items())
            {
                var id = node.Field("id").AsString();
                if (id != BigFish && id != BestGarden)
                    throw node.Field("id").Error($"unknown contest '{id}'");
                var name = node.Field("name");
                contests.Add(new FairContest(id, name.Field("ru").AsString(), name.Field("en").AsString()));
            }
            var garden = root.Field("garden");
            return new FairRules
            {
                Contests = contests,
                MaxHarvestsPerDay = garden.Field("maxHarvestsPerDay").AsInt(1, 100_000),
                MaxMutationsPerDay = garden.Field("maxMutationsPerDay").AsInt(1, 100_000)
            };
        }
    }
}
