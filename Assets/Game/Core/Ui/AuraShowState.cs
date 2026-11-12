using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Party;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public struct AuraMark
    {
        public GemDef Gem;
        public AuraTheme Theme;
        public GemColor Color;
        public bool Timed;
        public double SecondsLeft;
        public bool Warning;
        public bool Lit;
    }

    public struct FadedAuraMark
    {
        public AuraTheme Theme;
        public GemColor Color;
        public int Index;
        public int CountBefore;
    }

    public struct PartyAuraArc
    {
        public bool Present;
        public GemColor Color;
        public int Auras;
        public bool Faded;
        public bool Fresh;
    }

    public sealed class AuraShowState
    {
        public const int MaxMarks = SkillLoadout.MaxSlots;

        private readonly AuraShowRules _rules;
        private readonly AuraMark[] _marks = new AuraMark[MaxMarks];
        private readonly AuraMark[] _before = new AuraMark[MaxMarks];
        private readonly FadedAuraMark[] _faded = new FadedAuraMark[MaxMarks];
        private readonly PartyAuraArc[] _arcs = new PartyAuraArc[PartyRules.MaxSlots];
        private readonly int[,] _allyBefore = new int[PartyRules.MaxSlots, PartyBuffRules.MaxAuras];
        private readonly int[] _allyBeforeCount = new int[PartyRules.MaxSlots];
        private readonly int[] _allyNow = new int[PartyBuffRules.MaxAuras];
        private int _countBefore;
        private bool _started;

        public AuraShowState(AuraShowRules rules)
        {
            _rules = rules;
            Sigil = string.Empty;
        }

        public int Count { get; private set; }
        public int FadedCount { get; private set; }
        public string Sigil { get; private set; }
        public GemColor SigilColor { get; private set; }
        public bool HasSigil => Count > 0;
        public bool AnyTimed { get; private set; }
        public GemColor TimedColor { get; private set; }

        public ref readonly AuraMark Mark(int index) => ref _marks[index];

        public ref readonly FadedAuraMark Faded(int index) => ref _faded[index];

        public ref readonly PartyAuraArc Arc(int slot) => ref _arcs[slot];

        public void Restart() => _started = false;

        public void Update(IReadOnlyList<SkillSlot> skills, PartyBuffs? party)
        {
            var events = _started;
            KeepBefore();
            FillMarks(skills, events);
            FillFaded(events);
            FillSigil();
            FillArcs(party, events);
            _started = true;
        }

        private void KeepBefore()
        {
            _countBefore = Count;
            for (var i = 0; i < Count; i++)
                _before[i] = _marks[i];
        }

        private void FillMarks(IReadOnlyList<SkillSlot> skills, bool events)
        {
            Count = 0;
            AnyTimed = false;
            for (var i = 0; i < skills.Count && Count < MaxMarks; i++)
            {
                var slot = skills[i];
                if (!slot.AuraApplied || !(slot.Def.Effect is AuraEffect))
                    continue;
                var timed = !slot.Def.Reserves && slot.ActiveTicks > 0;
                var secondsLeft = timed ? slot.ActiveTicks * GameLoop.TickSeconds : 0d;
                _marks[Count++] = new AuraMark
                {
                    Gem = slot.Skill,
                    Theme = _rules.ThemeOf(slot.Skill),
                    Color = slot.Skill.Color,
                    Timed = timed,
                    SecondsLeft = secondsLeft,
                    Warning = timed && secondsLeft <= _rules.WarnSeconds,
                    Lit = events && IndexBefore(slot.Skill) < 0
                };
                if (!timed)
                    continue;
                AnyTimed = true;
                TimedColor = slot.Skill.Color;
            }
        }

        private void FillFaded(bool events)
        {
            FadedCount = 0;
            if (!events)
                return;
            for (var i = 0; i < _countBefore; i++)
            {
                if (IndexNow(_before[i].Gem) >= 0)
                    continue;
                _faded[FadedCount++] = new FadedAuraMark { Theme = _before[i].Theme, Color = _before[i].Color, Index = i, CountBefore = _countBefore };
            }
        }

        private void FillSigil()
        {
            if (Count == 0)
            {
                Sigil = string.Empty;
                return;
            }
            for (var i = 0; i < Count; i++)
            {
                var sigil = _rules.SigilOf(_marks[i].Gem);
                if (sigil == AuraShowRules.CommonSigil)
                    continue;
                Sigil = sigil;
                SigilColor = _marks[i].Color;
                return;
            }
            Sigil = AuraShowRules.CommonSigil;
            SigilColor = _marks[0].Color;
        }

        private void FillArcs(PartyBuffs? party, bool events)
        {
            for (var slot = 0; slot < _arcs.Length; slot++)
            {
                var ally = party?.Ally(slot);
                if (ally == null || !ally.Present)
                {
                    _arcs[slot] = default;
                    _allyBeforeCount[slot] = 0;
                    continue;
                }
                FillArc(slot, ally, events);
            }
        }

        private void FillArc(int slot, AllyOffer ally, bool events)
        {
            var arc = new PartyAuraArc();
            var suppressed = 0;
            var count = 0;
            for (var i = 0; i < ally.Auras.Length; i++)
            {
                var aura = ally.Auras[i];
                if (!aura.Active || !PartyBuffs.Shared(aura.Gem!))
                    continue;
                if (arc.Auras == 0)
                    arc.Color = aura.Gem!.Color;
                arc.Auras++;
                if (aura.Suppressed)
                    suppressed++;
                if (events && !SeenBefore(slot, aura.Gem!.NetId))
                    arc.Fresh = true;
                _allyNow[count++] = aura.Gem!.NetId;
            }
            for (var i = 0; i < count; i++)
                _allyBefore[slot, i] = _allyNow[i];
            _allyBeforeCount[slot] = count;
            arc.Present = arc.Auras > 0;
            arc.Faded = arc.Present && suppressed == arc.Auras;
            _arcs[slot] = arc;
        }

        private bool SeenBefore(int slot, int netId)
        {
            for (var i = 0; i < _allyBeforeCount[slot]; i++)
            {
                if (_allyBefore[slot, i] == netId)
                    return true;
            }
            return false;
        }

        private int IndexBefore(GemDef gem)
        {
            for (var i = 0; i < _countBefore; i++)
            {
                if (ReferenceEquals(_before[i].Gem, gem))
                    return i;
            }
            return -1;
        }

        private int IndexNow(GemDef gem)
        {
            for (var i = 0; i < Count; i++)
            {
                if (ReferenceEquals(_marks[i].Gem, gem))
                    return i;
            }
            return -1;
        }
    }
}
