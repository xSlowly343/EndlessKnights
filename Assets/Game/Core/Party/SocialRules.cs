using System.Collections.Generic;
using System.Text;
using Game.Core.Content;

namespace Game.Core.Party
{
    public enum SocialNameProblem
    {
        None,
        TooShort,
        TooLong,
        BadSymbols,
        Banned
    }

    public sealed class SocialChannelRule
    {
        public SocialChannelRule(string id, int history, int perMinute, double cooldownSeconds)
        {
            Id = id;
            History = history;
            PerMinute = perMinute;
            CooldownSeconds = cooldownSeconds;
        }

        public string Id { get; }
        public int History { get; }
        public int PerMinute { get; }
        public double CooldownSeconds { get; }
    }

    public sealed class SocialTicketRule
    {
        public SocialTicketRule(string id, int price)
        {
            Id = id;
            Price = price;
        }

        public string Id { get; }
        public int Price { get; }
    }

    public sealed class SocialRoleRule
    {
        public SocialRoleRule(string id, int muteHours, bool grantsRoles)
        {
            Id = id;
            MuteHours = muteHours;
            GrantsRoles = grantsRoles;
        }

        public string Id { get; }
        public int MuteHours { get; }
        public bool GrantsRoles { get; }
    }

    public sealed class SocialRules
    {
        public const string FileName = "social.json";
        public const int SupportedVersion = 1;
        public const string World = "world";
        public const string Party = "party";
        public const string Clan = "clan";
        public const string PartyRenameTicket = "party_rename";
        public const string ClanRenameTicket = "clan_rename";
        public const string OwnTitleMark = "=";

        private const string Digits = "0o1i3e4a5s7t@a$sёе";
        private const string Twins = "aаbвcсeеhнkкmмoоpрtтxхyу";

        private readonly IReadOnlyList<string> _banned;

        private SocialRules(int nameMin, int nameMax, IReadOnlyList<string> banned, int chatMax, int chatDays, double pollSeconds,
            IReadOnlyList<SocialChannelRule> channels, IReadOnlyList<string> reportReasons, IReadOnlyList<SocialTicketRule> tickets, IReadOnlyList<SocialRoleRule> roles)
        {
            NameMin = nameMin;
            NameMax = nameMax;
            _banned = banned;
            ChatMax = chatMax;
            ChatDays = chatDays;
            PollSeconds = pollSeconds;
            Channels = channels;
            ReportReasons = reportReasons;
            Tickets = tickets;
            Roles = roles;
        }

        public int NameMin { get; }
        public int NameMax { get; }
        public int ChatMax { get; }
        public int ChatDays { get; }
        public double PollSeconds { get; }
        public IReadOnlyList<SocialChannelRule> Channels { get; }
        public IReadOnlyList<string> ReportReasons { get; }
        public IReadOnlyList<SocialTicketRule> Tickets { get; }
        public IReadOnlyList<SocialRoleRule> Roles { get; }

        public static SocialRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var names = root.Field("names");
            var nameMin = names.Field("minLength").AsInt(1, 64);
            var nameMax = names.Field("maxLength").AsInt(nameMin, 64);
            var chat = root.Field("chat");
            var channels = new List<SocialChannelRule>();
            foreach (var node in chat.Field("channels").Items())
            {
                var id = node.Field("id").AsString();
                if (id != World && id != Party && id != Clan)
                    throw node.Field("id").Error($"unknown channel \"{id}\"");
                if (Find(channels, id) != null)
                    throw node.Field("id").Error($"channel \"{id}\" is repeated");
                channels.Add(new SocialChannelRule(id, node.Field("history").AsInt(1, 1000), node.Field("perMinute").AsInt(1, 600), node.Field("cooldownSeconds").AsNumber(0, 600)));
            }
            if (channels.Count != 3)
                throw chat.Field("channels").Error("must hold world, party and clan");
            var autoMute = chat.Field("autoMute");
            autoMute.Field("reporters").AsInt(1, 1000);
            autoMute.Field("days").AsInt(1, 365);
            var maxMuteHours = chat.Field("maxMuteHours").AsInt(1, 100000);
            autoMute.Field("hours").AsInt(1, maxMuteHours);
            chat.Field("removedKept").AsInt(1, 1000);
            var tickets = new List<SocialTicketRule>();
            foreach (var node in root.Field("tickets").Items())
            {
                var id = node.Field("id").AsString();
                if (id != PartyRenameTicket && id != ClanRenameTicket)
                    throw node.Field("id").Error($"unknown ticket \"{id}\"");
                tickets.Add(new SocialTicketRule(id, node.Field("price").AsInt(1, 1000000)));
            }
            if (tickets.Count != 2 || tickets[0].Id == tickets[1].Id)
                throw root.Field("tickets").Error("must hold party_rename and clan_rename");
            var roles = new List<SocialRoleRule>();
            foreach (var node in root.Field("roles").Items())
                roles.Add(new SocialRoleRule(Word(node.Field("id")), node.Field("muteHours").AsInt(0, maxMuteHours), node.Field("grantsRoles").AsBool()));
            var banned = new List<string>();
            foreach (var node in root.Field("banned").Items())
            {
                var word = node.AsString();
                if (word.Length < 3 || Fold(word, false) != word)
                    throw node.Error($"\"{word}\" must be three or more lower-case letters");
                banned.Add(word);
            }
            var reasons = new List<string>();
            foreach (var node in chat.Field("reportReasons").Items())
                reasons.Add(Word(node));
            if (reasons.Count == 0)
                throw chat.Field("reportReasons").Error("must not be empty");
            return new SocialRules(nameMin, nameMax, banned, chat.Field("maxLength").AsInt(1, 2000), chat.Field("days").AsInt(1, 365),
                chat.Field("pollSeconds").AsNumber(1, 600), channels, reasons, tickets, roles);
        }

        public SocialChannelRule Channel(string id) => Find(Channels, id) ?? throw new KeyNotFoundException(id);

        public int TicketPrice(string id)
        {
            foreach (var ticket in Tickets)
            {
                if (ticket.Id == id)
                    return ticket.Price;
            }
            throw new KeyNotFoundException(id);
        }

        public bool IsRole(string id)
        {
            foreach (var role in Roles)
            {
                if (role.Id == id)
                    return true;
            }
            return false;
        }

        public static string Tidy(string text)
        {
            var tidy = new StringBuilder(text.Length);
            var gap = false;
            foreach (var c in text)
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c) || (c >= 0x200b && c <= 0x200f) || (c >= 0x2028 && c <= 0x202e) || c == 0xfeff)
                {
                    gap = tidy.Length > 0;
                    continue;
                }
                if (gap)
                    tidy.Append(' ');
                gap = false;
                tidy.Append(c);
            }
            return tidy.ToString();
        }

        public SocialNameProblem CheckName(string name)
        {
            var tidy = Tidy(name);
            if (tidy.Length < NameMin)
                return SocialNameProblem.TooShort;
            if (tidy.Length > NameMax)
                return SocialNameProblem.TooLong;
            if (!HasNameShape(tidy))
                return SocialNameProblem.BadSymbols;
            return IsBanned(tidy) ? SocialNameProblem.Banned : SocialNameProblem.None;
        }

        public bool IsBanned(string text)
        {
            var plain = Fold(text, false);
            var twin = Fold(text, true);
            foreach (var word in _banned)
            {
                if (plain.Contains(word) || twin.Contains(word))
                    return true;
            }
            return false;
        }

        public bool FitsChat(string text)
        {
            var tidy = Tidy(text);
            return tidy.Length > 0 && tidy.Length <= ChatMax;
        }

        private static bool HasNameShape(string name)
        {
            var afterLetter = false;
            foreach (var c in name)
            {
                if (IsNameSymbol(c))
                {
                    afterLetter = true;
                    continue;
                }
                if (!afterLetter || (c != ' ' && c != '\'' && c != '-'))
                    return false;
                afterLetter = false;
            }
            return afterLetter;
        }

        private static bool IsNameSymbol(char c) =>
            (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || (c >= 0xc0 && c <= 0x24f) || (c >= 0x400 && c <= 0x4ff);

        private static string Fold(string text, bool twins)
        {
            var folded = new StringBuilder(text.Length);
            foreach (var symbol in text.ToLowerInvariant())
            {
                var c = Swap(Digits, symbol);
                if (twins)
                    c = Swap(Twins, c);
                if ((c >= 'a' && c <= 'z') || (c >= 0xdf && c <= 0x24f) || (c >= 0x400 && c <= 0x4ff))
                    folded.Append(c);
            }
            return folded.ToString();
        }

        private static char Swap(string pairs, char c)
        {
            for (var i = 0; i < pairs.Length; i += 2)
            {
                if (pairs[i] == c)
                    return pairs[i + 1];
            }
            return c;
        }

        private static SocialChannelRule? Find(IReadOnlyList<SocialChannelRule> channels, string id)
        {
            foreach (var channel in channels)
            {
                if (channel.Id == id)
                    return channel;
            }
            return null;
        }

        private static string Word(ContentNode node)
        {
            var id = node.AsString();
            if (id.Length == 0)
                throw node.Error("must not be empty");
            foreach (var c in id)
            {
                if (!(c >= 'a' && c <= 'z') && c != '_')
                    throw node.Error($"\"{id}\" may contain only a-z and _");
            }
            return id;
        }
    }
}
