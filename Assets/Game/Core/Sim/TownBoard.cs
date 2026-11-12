using System.Collections.Generic;

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
        public TownCandidate(string guild, long votes)
        {
            Guild = guild;
            Votes = votes;
        }

        public string Guild { get; }
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

    public sealed class TownBoard
    {
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

        public bool CanVote => Phase == TownBoardPhase.Vote;

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
