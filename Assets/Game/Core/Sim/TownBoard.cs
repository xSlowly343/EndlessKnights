using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum TownBoardPhase
    {
        Vote,
        Gather,
        Opening,
        Done
    }

    public readonly struct TownCandidate
    {
        public TownCandidate(string project, long votes)
        {
            Project = project;
            Votes = votes;
        }

        public string Project { get; }
        public long Votes { get; }
    }

    public readonly struct TownStock
    {
        public TownStock(string resource, long stock, long need, long yours, long sentToday, long dailyCap)
        {
            Resource = resource;
            Stock = stock;
            Need = need;
            Yours = yours;
            SentToday = sentToday;
            DailyCap = dailyCap;
        }

        public string Resource { get; }
        public long Stock { get; }
        public long Need { get; }
        public long Yours { get; }
        public long SentToday { get; }
        public long DailyCap { get; }
        public long LeftToday => System.Math.Max(0, DailyCap - SentToday);
        public bool Gathered => Stock >= Need;
    }

    public readonly struct TownRole
    {
        public TownRole(string role, string name, long amount)
        {
            Role = role;
            Name = name;
            Amount = amount;
        }

        public string Role { get; }
        public string Name { get; }
        public long Amount { get; }
    }

    public readonly struct TownBuilder
    {
        public TownBuilder(int rank, string name, long points)
        {
            Rank = rank;
            Name = name;
            Points = points;
        }

        public int Rank { get; }
        public string Name { get; }
        public long Points { get; }
    }

    public sealed class TownWeek
    {
        public TownWeek(long week, long goal, long total, bool done, long yours, long minLabour, bool framed, string decor, IReadOnlyList<string> decorations)
        {
            Week = week;
            Goal = goal;
            Total = total;
            Done = done;
            Yours = yours;
            MinLabour = minLabour;
            Framed = framed;
            Decor = decor;
            Decorations = decorations;
        }

        public static TownWeek None { get; } = new TownWeek(0, 0, 0, false, 0, 0, false, string.Empty, new string[0]);

        public long Week { get; }
        public long Goal { get; }
        public long Total { get; }
        public bool Done { get; }
        public long Yours { get; }
        public long MinLabour { get; }
        public bool Framed { get; }
        public string Decor { get; }
        public IReadOnlyList<string> Decorations { get; }
    }

    public sealed class TownBoard
    {
        private static readonly TownBuilder[] NoBuilders = new TownBuilder[0];

        public TownBoard(TownBoardPhase phase, int round, long voteEndsUnixMs, long opensAtUnixMs, long serverUnixMs, string project, string vote,
            IReadOnlyList<string> built, IReadOnlyList<TownCandidate> candidates, IReadOnlyList<TownStock> stocks)
        {
            Phase = phase;
            Round = round;
            VoteEndsUnixMs = voteEndsUnixMs;
            OpensAtUnixMs = opensAtUnixMs;
            ServerUnixMs = serverUnixMs;
            Project = project;
            Vote = vote;
            Built = built;
            Candidates = candidates;
            Stocks = stocks;
        }

        public TownBoardPhase Phase { get; }
        public int Round { get; }
        public long VoteEndsUnixMs { get; }
        public long OpensAtUnixMs { get; }
        public long ServerUnixMs { get; }
        public string Project { get; }
        public string Vote { get; }
        public IReadOnlyList<string> Built { get; }
        public IReadOnlyList<TownCandidate> Candidates { get; }
        public IReadOnlyList<TownStock> Stocks { get; }
        public long Points { get; private set; }
        public int Rank { get; private set; }
        public IReadOnlyList<TownBuilder> Top { get; private set; } = NoBuilders;
        public TownWeek Week { get; private set; } = TownWeek.None;

        public TownBoard WithWeek(TownWeek week)
        {
            Week = week;
            return this;
        }

        public IReadOnlyList<TownRole> Roles { get; private set; } = System.Array.Empty<TownRole>();

        public TownBoard WithRoles(IReadOnlyList<TownRole> roles)
        {
            Roles = roles;
            return this;
        }

        public TownBoard WithBuilders(long points, int rank, IReadOnlyList<TownBuilder> top)
        {
            Points = points;
            Rank = rank;
            Top = top;
            return this;
        }

        public bool CanVote => Phase == TownBoardPhase.Vote;

        public int BuiltCount(GuildRules rules) => rules.CountProjects(Built);

        public bool Gathered
        {
            get
            {
                foreach (var stock in Stocks)
                {
                    if (!stock.Gathered)
                        return false;
                }
                return true;
            }
        }

        public double SecondsLeft(double secondsSinceReply)
        {
            var until = Phase == TownBoardPhase.Vote ? VoteEndsUnixMs : Phase == TownBoardPhase.Opening ? OpensAtUnixMs : ServerUnixMs;
            return System.Math.Max(0, (until - ServerUnixMs) / 1000.0 - System.Math.Max(0, secondsSinceReply));
        }

        public long LeftToday(string resource)
        {
            foreach (var stock in Stocks)
            {
                if (stock.Resource == resource)
                    return stock.LeftToday;
            }
            return 0;
        }

        public static TownBoardPhase PhaseOf(string name) =>
            name == "vote" ? TownBoardPhase.Vote : name == "gather" ? TownBoardPhase.Gather : name == "build" ? TownBoardPhase.Opening : TownBoardPhase.Done;
    }
}
