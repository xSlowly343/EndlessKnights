using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content.Gems;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public sealed class SkillWindowModel
    {
        private readonly GameLoop _loop;
        private readonly SkillWindowTier[] _tiers = new SkillWindowTier[SkillBranchTier.Count];
        private readonly List<SkillWindowSupport> _supports = new List<SkillWindowSupport>();

        public SkillWindowModel(GameLoop loop)
        {
            _loop = loop;
        }

        public int Row { get; private set; } = SkillLoadout.NoSlot;
        public GemDef? Skill { get; private set; }
        public int Level { get; private set; }
        public int MaxLevel => _loop.Data.Gems.Levels.Max;
        public double Experience { get; private set; }
        public double ExperienceToNext { get; private set; }
        public bool IsAura => Skill != null && SkillLoadout.IsAura(Skill);
        public GemDef? NextForm { get; private set; }
        public int NextFormLevel { get; private set; }
        public int TierCount { get; private set; }
        public int Sockets { get; private set; }
        public int MaxSockets => _loop.Book.MaxSockets;
        public IReadOnlyList<SkillWindowSupport> Supports => _supports;

        public ref readonly SkillWindowTier Tier(int tier) => ref _tiers[tier];

        public void Show(int row)
        {
            Row = row;
            Refresh();
        }

        public void Refresh()
        {
            var skills = _loop.State.Skills;
            Skill = Row >= 0 && Row < skills.Count ? skills[Row].Skill : null;
            TierCount = 0;
            NextForm = null;
            NextFormLevel = 0;
            _supports.Clear();
            if (Skill == null)
                return;
            if (_loop.Book.OfHeroClass(Skill))
                NextForm = _loop.Book.NextFormOf(Skill);
            NextFormLevel = NextForm == null ? 0 : _loop.Book.UnlockLevel(NextForm);
            var progress = _loop.State.ProgressOf(Skill);
            Level = progress.Level;
            Experience = progress.Experience;
            ExperienceToNext = Level >= MaxLevel ? 0 : _loop.Data.Gems.Levels.ToNext(Level);
            Sockets = _loop.Book.SocketsAt(Row);
            FillTiers(progress);
            FillSupports(skills[Row]);
        }

        public BranchResult Choose(int tier, int option)
        {
            if (Skill == null)
                return BranchResult.NoSuchBranch;
            var result = _loop.ChooseBranch(Skill, tier, option);
            Refresh();
            return result;
        }

        public BookResult SetSupport(int socket, GemDef? support)
        {
            var result = _loop.Book.SetSupport(Row, socket, support);
            Refresh();
            return result;
        }

        private void FillTiers(GemProgress progress)
        {
            var branches = Skill!.Branches;
            TierCount = branches.Count;
            for (var tier = 0; tier < branches.Count; tier++)
            {
                _tiers[tier] = new SkillWindowTier
                {
                    Def = branches[tier],
                    Open = progress.Level >= branches[tier].Level,
                    Chosen = progress.Branches[tier],
                    ChangeCost = _loop.BranchChangeCost(Skill, tier)
                };
            }
        }

        private void FillSupports(SkillSlot slot)
        {
            var book = _loop.Book;
            foreach (var support in _loop.Data.Gems.All)
            {
                if (support.Support == null || !support.CanSupport(slot.Skill))
                    continue;
                var socket = System.Array.IndexOf(slot.Supports, support);
                var offer = socket >= 0 ? BookResult.Done
                    : !book.SupportOpen(support) ? BookResult.LevelTooLow
                    : BookResult.Done;
                _supports.Add(new SkillWindowSupport { Gem = support, Offer = offer, Socket = socket });
            }
        }
    }
}
