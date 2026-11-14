using Game.Core.Sim;

namespace Game.Core.Ui
{
    public sealed class MilestoneShow
    {
        public const double GapSeconds = 0.3;
        public const int FramesPerSecond = 15;
        public const int ArtifactGroups = 4;

        private static readonly int KindCount = System.Enum.GetValues(typeof(MilestoneKind)).Length;
        private static readonly int[] Frames = { 12, 15, 14, 11, 11, 12, 12, 9, 9, 12, 12, 12, 12, 9, 9 };

        private readonly bool[] _waiting = new bool[KindCount];
        private readonly int[] _variants = new int[KindCount];
        private readonly long[] _values = new long[KindCount];
        private readonly long[] _order = new long[KindCount];
        private long _offers;
        private int _count;
        private double _freeAt = double.NegativeInfinity;

        public int Waiting => _count;

        public bool Busy(double now) => now < _freeAt;

        public static MilestoneSize SizeOf(MilestoneKind kind)
        {
            switch (kind)
            {
                case MilestoneKind.Prestige:
                    return MilestoneSize.Large;
                case MilestoneKind.Achievement:
                case MilestoneKind.Collection:
                case MilestoneKind.CampTier:
                case MilestoneKind.Quest:
                case MilestoneKind.Trial:
                    return MilestoneSize.Small;
                default:
                    return MilestoneSize.Medium;
            }
        }

        public static int FramesOf(MilestoneKind kind) => Frames[(int)kind];

        public static double SecondsOf(MilestoneKind kind) => Frames[(int)kind] / (double)FramesPerSecond;

        public static bool TryRead(in GameEvent gameEvent, out MilestoneCue cue)
        {
            switch (gameEvent.Kind)
            {
                case GameEventKind.LevelUp:
                    cue = new MilestoneCue(MilestoneKind.LevelUp, 0, gameEvent.Stage);
                    return true;
                case GameEventKind.Prestige:
                    cue = new MilestoneCue(MilestoneKind.Prestige, 0, gameEvent.Stage);
                    return true;
                case GameEventKind.ArtifactUnlocked:
                    cue = new MilestoneCue(MilestoneKind.ArtifactUnlocked, GroupOf(gameEvent.Stage), gameEvent.Stage);
                    return true;
                case GameEventKind.ArtifactAwakened:
                    cue = new MilestoneCue(MilestoneKind.ArtifactAwakened, GroupOf(gameEvent.Stage), (long)gameEvent.Amount.ToDouble());
                    return true;
                case GameEventKind.ArtifactEnchanted:
                    cue = new MilestoneCue(MilestoneKind.ArtifactEnchanted, GroupOf(gameEvent.Stage), gameEvent.Stage);
                    return true;
                case GameEventKind.RiftFeatureOpened:
                    cue = new MilestoneCue(MilestoneKind.RiftStep, 0, gameEvent.Stage);
                    return true;
                case GameEventKind.FeatureOpened:
                    cue = new MilestoneCue(MilestoneKind.FeatureOpened, 0, gameEvent.Stage);
                    return true;
                case GameEventKind.RiftGiftOffered:
                    cue = new MilestoneCue(MilestoneKind.GiftOffered, 0, gameEvent.Stage);
                    return true;
                case GameEventKind.RiftGiftChosen:
                    cue = new MilestoneCue(MilestoneKind.GiftChosen, 0, gameEvent.Stage);
                    return true;
                case GameEventKind.GateCleared:
                    cue = new MilestoneCue(MilestoneKind.GateCleared, 0, gameEvent.Stage);
                    return true;
                case GameEventKind.AchievementUnlocked:
                    cue = new MilestoneCue(MilestoneKind.Achievement, 0, gameEvent.Stage);
                    return true;
                case GameEventKind.CollectionCompleted:
                    cue = new MilestoneCue(MilestoneKind.Collection, 0, gameEvent.Stage);
                    return true;
                case GameEventKind.CampTierReached:
                    cue = new MilestoneCue(MilestoneKind.CampTier, 0, gameEvent.Stage);
                    return true;
                case GameEventKind.QuestClaimed:
                    cue = new MilestoneCue(MilestoneKind.Quest, 0, gameEvent.Stage);
                    return true;
                case GameEventKind.TrialCompleted:
                    cue = new MilestoneCue(MilestoneKind.Trial, 0, gameEvent.Stage);
                    return true;
                default:
                    cue = default;
                    return false;
            }
        }

        public bool Offer(in GameEvent gameEvent)
        {
            if (!TryRead(gameEvent, out var cue))
                return false;
            Offer(cue);
            return true;
        }

        public void Offer(MilestoneCue cue)
        {
            var at = (int)cue.Kind;
            if (!_waiting[at])
            {
                _waiting[at] = true;
                _order[at] = _offers++;
                _count++;
            }
            _variants[at] = cue.Variant;
            _values[at] = cue.Value;
        }

        public bool TryTake(double now, out MilestoneCue cue)
        {
            var prestige = (int)MilestoneKind.Prestige;
            if (_waiting[prestige])
            {
                cue = Take(prestige);
                _freeAt = System.Math.Max(_freeAt, now + cue.Seconds + GapSeconds);
                return true;
            }
            if (_count == 0 || now < _freeAt)
            {
                cue = default;
                return false;
            }
            cue = Take(Oldest());
            _freeAt = now + cue.Seconds + GapSeconds;
            return true;
        }

        public void Clear()
        {
            for (var i = 0; i < KindCount; i++)
                _waiting[i] = false;
            _count = 0;
        }

        private static int GroupOf(long group) => (int)System.Math.Min(ArtifactGroups - 1, System.Math.Max(0, group - 1));

        private int Oldest()
        {
            var oldest = -1;
            for (var i = 0; i < KindCount; i++)
            {
                if (_waiting[i] && (oldest < 0 || _order[i] < _order[oldest]))
                    oldest = i;
            }
            return oldest;
        }

        private MilestoneCue Take(int at)
        {
            _waiting[at] = false;
            _count--;
            return new MilestoneCue((MilestoneKind)at, _variants[at], _values[at]);
        }
    }
}
