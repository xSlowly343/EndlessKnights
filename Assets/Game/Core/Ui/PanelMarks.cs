using System;
using System.Collections.Generic;
using Game.Core.Content.Tree;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public sealed class PanelMarks
    {
        private readonly bool[] _open = new bool[PanelSections.All.Count];
        private readonly bool[] _dots = new bool[PanelSections.All.Count];
        private PanelUnlocks _unlocks;
        private bool _refreshed;

        public PanelMarks(PanelUnlocks unlocks)
        {
            _unlocks = unlocks;
            for (var i = 0; i < _open.Length; i++)
                _open[i] = true;
        }

        public event Action<PanelSection>? Unlocked;

        public bool IsOpen(PanelSection section) => _open[(int)section];
        public bool HasDot(PanelSection section) => _dots[(int)section];
        public long UnlockStage(PanelSection section) => _unlocks.StageFor(section);
        public int UnreadMail { get; set; }

        public bool HasDotAmong(IReadOnlyList<PanelSection> sections)
        {
            for (var i = 0; i < sections.Count; i++)
            {
                if (HasDot(sections[i]))
                    return true;
            }
            return false;
        }

        public void UseUnlocks(PanelUnlocks unlocks) => _unlocks = unlocks;

        public bool Refresh(GameState state, AscendancyCatalog ascendancy)
        {
            var reached = System.Math.Max(state.RecordStage, state.HighestStage);
            var changed = false;
            foreach (var section in PanelSections.All)
            {
                var index = (int)section;
                var open = _unlocks.IsOpen(section, reached);
                var dot = open && NeedsAttention(section, state, ascendancy);
                var opened = open && !_open[index];
                changed |= open != _open[index] || dot != _dots[index];
                _open[index] = open;
                _dots[index] = dot;
                if (opened && _refreshed)
                    Unlocked?.Invoke(section);
            }
            _refreshed = true;
            return changed;
        }

        private bool NeedsAttention(PanelSection section, GameState state, AscendancyCatalog ascendancy) => section switch
        {
            PanelSection.Tree => state.TreePointsLeft > 0 || state.Ascendancy.PointsLeft(ascendancy) > 0,
            PanelSection.Mail => UnreadMail > 0,
            _ => false,
        };
    }
}
