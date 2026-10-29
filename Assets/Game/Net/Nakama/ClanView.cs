using System.Collections.Generic;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class ClanView
    {
        private ClanView(JsonValue json)
        {
            Id = NakamaJson.Text(json, "id");
            Code = NakamaJson.Text(json, "code");
            Access = NakamaJson.Text(json, "access");
            Adjective = NakamaJson.Text(json, "adjective");
            Noun = NakamaJson.Text(json, "noun");
            Language = NakamaJson.Text(json, "language");
            Leader = NakamaJson.Text(json, "leader");
            MaxMembers = (int)NakamaJson.Number(json, "maxMembers");
            MaxOfficers = (int)NakamaJson.Number(json, "maxOfficers");
            ActiveNow = (int)NakamaJson.Number(json, "activeNow");
            var members = new List<ClanMemberView>();
            foreach (var member in NakamaJson.Items(json, "members"))
                members.Add(new ClanMemberView(member));
            Members = members;
        }

        public string Id { get; }
        public string Code { get; }
        public string Access { get; }
        public string Adjective { get; }
        public string Noun { get; }
        public string Language { get; }
        public string Leader { get; }
        public int MaxMembers { get; }
        public int MaxOfficers { get; }
        public int ActiveNow { get; }
        public IReadOnlyList<ClanMemberView> Members { get; }
        public bool IsOpen => Access == "open";

        public static ClanView? From(JsonValue? json) => json == null || json.Kind != JsonKind.Object ? null : new ClanView(json);

        public string RoleOf(string userId)
        {
            foreach (var member in Members)
            {
                if (member.UserId == userId)
                    return member.Role;
            }
            return string.Empty;
        }
    }

    public sealed class ClanMemberView
    {
        public ClanMemberView(JsonValue json)
        {
            UserId = NakamaJson.Text(json, "userId");
            Name = NakamaJson.Text(json, "name");
            SteamId = NakamaJson.Text(json, "steamId");
            Role = NakamaJson.Text(json, "role");
            SeenUnixMs = (long)NakamaJson.Number(json, "seen");
        }

        public string UserId { get; }
        public string Name { get; }
        public string SteamId { get; }
        public string Role { get; }
        public long SeenUnixMs { get; }
    }

    public sealed class ClanCard
    {
        public ClanCard(JsonValue json)
        {
            Id = NakamaJson.Text(json, "id");
            Adjective = NakamaJson.Text(json, "adjective");
            Noun = NakamaJson.Text(json, "noun");
            Language = NakamaJson.Text(json, "language");
            Members = (int)NakamaJson.Number(json, "members");
            MaxMembers = (int)NakamaJson.Number(json, "maxMembers");
            ActiveNow = (int)NakamaJson.Number(json, "activeNow");
        }

        public string Id { get; }
        public string Adjective { get; }
        public string Noun { get; }
        public string Language { get; }
        public int Members { get; }
        public int MaxMembers { get; }
        public int ActiveNow { get; }
    }
}
