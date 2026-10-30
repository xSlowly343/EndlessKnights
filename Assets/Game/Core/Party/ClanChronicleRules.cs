using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Party
{
    public sealed class ClanChronicleRules
    {
        public const string FileName = "clan_chronicle.json";
        public const int SupportedVersion = 1;

        public static readonly IReadOnlyList<string> Kinds = new[] { "raidsKilled", "challengesDone", "treeNodes", "sealsDonated", "membersMax", "covenRaids" };

        private ClanChronicleRules(int maxRecords, IReadOnlyList<PartyFeat> feats)
        {
            MaxRecords = maxRecords;
            Feats = feats;
        }

        public int MaxRecords { get; }
        public IReadOnlyList<PartyFeat> Feats { get; }

        public static ClanChronicleRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var feats = new List<PartyFeat>();
            var ids = new HashSet<string>();
            foreach (var item in root.Field("feats").Items())
                feats.Add(PartyChronicleRules.ReadFeat(item, ids, Kinds, System.Array.Empty<string>()));
            return new ClanChronicleRules(root.Field("maxRecords").AsInt(1, 500), feats);
        }
    }
}
