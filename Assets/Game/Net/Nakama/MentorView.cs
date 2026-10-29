using System.Collections.Generic;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class MentorView
    {
        public const string StudentRole = "student";
        public const string MentorRole = "mentor";

        private MentorView(JsonValue json)
        {
            Role = NakamaJson.Text(json, "role");
            CanMentor = NakamaJson.Flag(json, "canMentor");
            Ready = NakamaJson.Flag(json, "ready");
            StudentUntilUnixMs = (long)NakamaJson.Number(json, "studentUntil");
            var partner = NakamaJson.Field(json, "partner");
            PartnerId = partner == null ? string.Empty : NakamaJson.Text(partner, "userId");
            PartnerName = partner == null ? string.Empty : NakamaJson.Text(partner, "name");
            Seconds = NakamaJson.Number(json, "seconds");
            Level = (int)NakamaJson.Number(json, "level");
            Marks = Texts(json, "marks");
            Perks = Texts(json, "perks");
            CoopPoints = (int)NakamaJson.Number(json, "coopPoints");
            Graduates = (int)NakamaJson.Number(json, "graduates");
        }

        public string Role { get; }
        public bool IsStudent => Role == StudentRole;
        public bool IsMentor => Role == MentorRole;
        public bool CanMentor { get; }
        public bool Ready { get; }
        public long StudentUntilUnixMs { get; }
        public string PartnerId { get; }
        public string PartnerName { get; }
        public bool HasPartner => PartnerId.Length > 0;
        public double Seconds { get; }
        public int Level { get; }
        public IReadOnlyList<string> Marks { get; }
        public IReadOnlyList<string> Perks { get; }
        public int CoopPoints { get; }
        public int Graduates { get; }

        public static MentorView From(JsonValue json) => new MentorView(json);

        private static IReadOnlyList<string> Texts(JsonValue json, string name)
        {
            var list = new List<string>();
            foreach (var item in NakamaJson.Items(json, name))
                list.Add(item.Text);
            return list;
        }
    }
}
