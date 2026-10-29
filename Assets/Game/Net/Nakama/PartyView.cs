using System.Collections.Generic;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class PartyView
    {
        private PartyView(JsonValue json)
        {
            Id = NakamaJson.Text(json, "id");
            Code = NakamaJson.Text(json, "code");
            Access = NakamaJson.Text(json, "access");
            Title = NakamaJson.Text(json, "title");
            Language = NakamaJson.Text(json, "language");
            Wanted = NakamaJson.Text(json, "wanted");
            Leader = NakamaJson.Text(json, "leader");
            MaxMembers = (int)NakamaJson.Number(json, "maxMembers");
            MatchId = NakamaJson.Text(json, "matchId");
            var members = new List<PartyMemberView>();
            foreach (var member in NakamaJson.Items(json, "members"))
                members.Add(new PartyMemberView(NakamaJson.Text(member, "userId"), NakamaJson.Text(member, "name"), NakamaJson.Text(member, "steamId"), NakamaJson.Flag(member, "leader"), SlotOf(member))
                {
                    Student = NakamaJson.Flag(member, "student"),
                    Mentor = NakamaJson.Flag(member, "mentor")
                });
            Members = members;
        }

        public string Id { get; }
        public string Code { get; }
        public string Access { get; }
        public string Title { get; }
        public string Language { get; }
        public string Wanted { get; }
        public string Leader { get; }
        public int MaxMembers { get; }
        public string MatchId { get; }
        public IReadOnlyList<PartyMemberView> Members { get; }
        public bool IsOpen => Access == "open";

        public static PartyView? From(JsonValue? json) => json == null || json.Kind != JsonKind.Object ? null : new PartyView(json);

        public string? UserIdAtSlot(int slot)
        {
            if (slot < 0)
                return null;
            foreach (var member in Members)
            {
                if (member.Slot == slot)
                    return member.UserId;
            }
            return null;
        }

        private static int SlotOf(JsonValue member)
        {
            var slot = NakamaJson.Field(member, "slot");
            return slot != null && slot.Kind == JsonKind.Number ? (int)slot.Number : -1;
        }
    }

    public sealed class PartyMemberView
    {
        public PartyMemberView(string userId, string name, string steamId, bool leader, int slot)
        {
            UserId = userId;
            Name = name;
            SteamId = steamId;
            Leader = leader;
            Slot = slot;
        }

        public string UserId { get; }
        public string Name { get; }
        public string SteamId { get; }
        public bool Leader { get; }
        public int Slot { get; }
        public bool Student { get; set; }
        public bool Mentor { get; set; }
    }

    public sealed class PartyCard
    {
        public PartyCard(JsonValue json)
        {
            Id = NakamaJson.Text(json, "id");
            Title = NakamaJson.Text(json, "title");
            Language = NakamaJson.Text(json, "language");
            Wanted = NakamaJson.Text(json, "wanted");
            Members = (int)NakamaJson.Number(json, "members");
            MaxMembers = (int)NakamaJson.Number(json, "maxMembers");
            ActiveNow = NakamaJson.Flag(json, "activeNow");
            UsuallyOnline = NakamaJson.Text(json, "usuallyOnline");
            WantsYou = NakamaJson.Flag(json, "wantsYou");
            HasMentor = NakamaJson.Flag(json, "hasMentor");
            HasStudent = NakamaJson.Flag(json, "hasStudent");
        }

        public string Id { get; }
        public string Title { get; }
        public string Language { get; }
        public string Wanted { get; }
        public int Members { get; }
        public int MaxMembers { get; }
        public bool ActiveNow { get; }
        public string UsuallyOnline { get; }
        public bool WantsYou { get; }
        public bool HasMentor { get; }
        public bool HasStudent { get; }
    }
}
