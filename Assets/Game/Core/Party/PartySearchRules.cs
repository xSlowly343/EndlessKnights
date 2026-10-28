using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Sim;

namespace Game.Core.Party
{
    public sealed class PartySearchRules
    {
        public const string AnyRole = "any";
        public const string CodeAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

        private readonly Dictionary<HeroClass, string> _classRoles;

        private PartySearchRules(int codeLength, int inactiveDays, int listLimit, IReadOnlyList<string> titles, IReadOnlyList<string> roles,
            IReadOnlyList<string> languages, IReadOnlyList<string> reportReasons, IReadOnlyList<(string Id, int FromHour)> timeSlots,
            Dictionary<HeroClass, string> classRoles)
        {
            TimeSlots = timeSlots;
            CodeLength = codeLength;
            InactiveDays = inactiveDays;
            ListLimit = listLimit;
            Titles = titles;
            Roles = roles;
            Languages = languages;
            ReportReasons = reportReasons;
            _classRoles = classRoles;
        }

        public int CodeLength { get; }
        public int InactiveDays { get; }
        public int ListLimit { get; }
        public IReadOnlyList<string> Titles { get; }
        public IReadOnlyList<string> Roles { get; }
        public IReadOnlyList<string> Languages { get; }
        public IReadOnlyList<string> ReportReasons { get; }
        public IReadOnlyList<(string Id, int FromHour)> TimeSlots { get; }

        public string TimeSlotAt(int localHour)
        {
            var slot = TimeSlots[TimeSlots.Count - 1].Id;
            for (var i = 0; i < TimeSlots.Count; i++)
            {
                if (TimeSlots[i].FromHour <= localHour)
                    slot = TimeSlots[i].Id;
            }
            return slot;
        }

        public string RoleOf(HeroClass heroClass) => _classRoles[heroClass];

        public static string FormatCode(string code) => code.Length <= 3 ? code : code.Substring(0, code.Length / 2) + "-" + code.Substring(code.Length / 2);

        public static PartySearchRules Read(ContentNode node)
        {
            var roles = Ids(node.Field("roles"));
            if (!Contains(roles, AnyRole))
                throw node.Field("roles").Error($"must contain \"{AnyRole}\"");
            var classRoles = new Dictionary<HeroClass, string>();
            var classNode = node.Field("classRoles");
            foreach (var name in classNode.FieldNames())
            {
                if (!HeroClassNames.ByName.TryGetValue(name, out var heroClass))
                    throw classNode.Field(name).Error($"unknown class \"{name}\"");
                var role = classNode.Field(name).AsString();
                if (role == AnyRole || !Contains(roles, role))
                    throw classNode.Field(name).Error($"role \"{role}\" is not one of roles (and cannot be \"{AnyRole}\")");
                classRoles[heroClass] = role;
            }
            foreach (var pair in HeroClassNames.ByName)
            {
                if (!classRoles.ContainsKey(pair.Value))
                    throw classNode.Error($"class \"{pair.Key}\" has no role");
            }
            return new PartySearchRules(
                node.Field("codeLength").AsInt(4, 10),
                node.Field("inactiveDays").AsInt(1, 365),
                node.Field("listLimit").AsInt(1, 100),
                Ids(node.Field("titles")),
                roles,
                Ids(node.Field("languages")),
                Ids(node.Field("reportReasons")),
                Slots(node.Field("timeSlots")),
                classRoles);
        }

        private static IReadOnlyList<(string Id, int FromHour)> Slots(ContentNode node)
        {
            var slots = new List<(string Id, int FromHour)>();
            foreach (var item in node.Items())
            {
                var hour = item.Field("fromHour").AsInt(0, 23);
                if (slots.Count == 0 && hour != 0)
                    throw item.Field("fromHour").Error("the first slot must start at hour 0");
                if (slots.Count > 0 && hour <= slots[slots.Count - 1].FromHour)
                    throw item.Field("fromHour").Error("slots must go in increasing hours");
                slots.Add((item.Field("id").AsString(), hour));
            }
            if (slots.Count == 0)
                throw node.Error("must not be empty");
            return slots;
        }

        private static IReadOnlyList<string> Ids(ContentNode node)
        {
            var ids = new List<string>();
            foreach (var item in node.Items())
            {
                var id = item.AsString();
                if (id.Length == 0 || Contains(ids, id))
                    throw item.Error($"\"{id}\" is empty or repeated");
                foreach (var c in id)
                {
                    if (!(c >= 'a' && c <= 'z') && c != '_')
                        throw item.Error($"\"{id}\" may contain only a-z and _");
                }
                ids.Add(id);
            }
            if (ids.Count == 0)
                throw node.Error("must not be empty");
            return ids;
        }

        private static bool Contains(IReadOnlyList<string> ids, string id)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                    return true;
            }
            return false;
        }
    }
}
