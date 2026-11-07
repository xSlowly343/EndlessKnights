using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class CompanionFamily
    {
        public CompanionFamily(string id, string nameRu, string nameEn, IReadOnlyList<string> members, IReadOnlyList<MenagerieLine> collected, IReadOnlyList<MenagerieLine> adults)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Members = members;
            Collected = collected;
            Adults = adults;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<string> Members { get; }
        public IReadOnlyList<MenagerieLine> Collected { get; }
        public IReadOnlyList<MenagerieLine> Adults { get; }
    }

    public sealed class CompanionFamilyRules
    {
        public IReadOnlyList<CompanionFamily> Families { get; private set; } = Array.Empty<CompanionFamily>();
        public int AdultStage { get; private set; } = 2;
        public int TreePoints { get; private set; } = 1;

        public CompanionFamily? FamilyOf(string companion)
        {
            for (var i = 0; i < Families.Count; i++)
            {
                var members = Families[i].Members;
                for (var j = 0; j < members.Count; j++)
                {
                    if (members[j] == companion)
                        return Families[i];
                }
            }
            return null;
        }

        internal static CompanionFamilyRules Load(ContentNode node)
        {
            var families = new List<CompanionFamily>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in node.Field("list").Items())
            {
                var members = new List<string>();
                foreach (var member in item.Field("members").Items())
                {
                    var id = member.AsString();
                    if (!seen.Add(id))
                        throw member.Error($"companion '{id}' is in two families");
                    members.Add(id);
                }
                var name = item.Field("name");
                families.Add(new CompanionFamily(item.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), members,
                    MenagerieTreeRules.Lines(item.Field("collected")), MenagerieTreeRules.Lines(item.Field("adults"))));
            }
            return new CompanionFamilyRules
            {
                Families = families,
                AdultStage = node.Field("adultStage").AsInt(0, 10),
                TreePoints = node.Field("treePoints").AsInt(0, 100)
            };
        }
    }
}
