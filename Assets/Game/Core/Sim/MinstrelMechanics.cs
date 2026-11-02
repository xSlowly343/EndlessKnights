using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class MinstrelMechanics
    {
        public const int Fanfare = 0;
        public const int March = 1;
        public const int Hymn = 2;

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private MinstrelRules _rules;

        public MinstrelMechanics(MinstrelRules rules, GameState state, StatSheet hero, CombatSystem combat)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _combat = combat;
        }

        public void UseRules(MinstrelRules rules) => _rules = rules;

        private MinstrelState S => _state.Minstrel;
        private bool On => _state.Is(HeroClass.Minstrel);
        private bool Flag(StatId stat) => _hero.Value(stat, 0) >= 1;
        private double Positive(StatId stat) => System.Math.Max(0, _hero.Value(stat, 0));

        public bool Verse => On && VersePower > 0;
        public bool Song => On && SongPower > 0;
        public bool Lullaby => On && SleepPower > 0;

        public double VersePower => Positive(MinstrelStats.VersePower);
        public double SongPower => Positive(MinstrelStats.SongPower);
        public double SleepPower => Positive(MinstrelStats.SleepPower);

        public int LineHits => System.Math.Max(_rules.LineHitsMin, (int)System.Math.Round(_rules.LineHits + _hero.Value(MinstrelStats.LineHits, 0)));

        public int VerseTicks => Ticks(System.Math.Max(_rules.VerseSecondsMin, _rules.VerseSeconds + _hero.Value(MinstrelStats.VerseSeconds, 0)));

        public int VerseMax => System.Math.Max(1, (int)System.Math.Round(_rules.VerseMax + _hero.Value(MinstrelStats.VerseMax, 0)
            + (Flag(MinstrelStats.ChorusChoir) ? System.Math.Max(0, _state.PartyMembers - 1) : 0)));

        public double ChorusMultiplier => System.Math.Max(1, _rules.ChorusMultiplier + _hero.Value(MinstrelStats.ChorusBoost, 0));

        public bool InChorus => Verse && S.ChorusTicks > 0;

        public int SongHits => System.Math.Max(_rules.SongHitsMin, (int)System.Math.Round(_rules.SongHits + _hero.Value(MinstrelStats.SongHits, 0)));

        public bool Solo => _state.PartyMembers <= 1;

        public bool AllSongs => Song && S.SongAllTicks > 0;

        public int NoteHits => System.Math.Max(_rules.NoteHitsMin, (int)System.Math.Round(_rules.NoteHits + _hero.Value(MinstrelStats.NoteHits, 0)));

        public int SleepNotes => System.Math.Max(_rules.SleepNotesMin, (int)System.Math.Round(_rules.SleepNotes + _hero.Value(MinstrelStats.SleepNotes, 0)));

        public int SleepTicks => Ticks(System.Math.Max(_rules.SleepSecondsMin, _rules.SleepSeconds + _hero.Value(MinstrelStats.SleepSeconds, 0)));

        public bool Climax => On && (InChorus || AllSongs || (Lullaby && S.Asleep) || S.HymnTicks > 0);

        public double VerseBonus => Verse ? S.Verses * VersePower * (InChorus ? ChorusMultiplier : 1) : 0;

        public double HymnBonus => On && S.HymnTicks > 0 ? S.HymnPower * (InChorus ? ChorusMultiplier : 1) : 0;

        public double SelfSong
        {
            get
            {
                if (!Song)
                    return 0;
                var power = SongPower * (1 - System.Math.Min(1, Positive(MinstrelStats.SongSelfPenalty)));
                if (Solo && Flag(MinstrelStats.SongSolo))
                    power *= _rules.SongSoloMultiplier;
                return AllSongs ? power * _rules.SongAllMultiplier : power;
            }
        }

        public double PartySong
        {
            get
            {
                if (!Song)
                    return 0;
                var power = SongPower * (1 - System.Math.Min(1, Positive(MinstrelStats.SongPartyPenalty)));
                return AllSongs ? power * _rules.SongAllMultiplier : power;
            }
        }

        public bool Sings(int melody)
        {
            if (!Song)
                return false;
            if (AllSongs || S.Melody == melody)
                return true;
            return Flag(MinstrelStats.SongDuet) && (S.Melody + 1) % MinstrelState.MelodyCount == melody;
        }

        public double StrikeMultiplier()
        {
            if (!On)
                return 1;
            var multiplier = S.SoloTicks > 0 ? _rules.SoloMultiplier : 1;
            return Sings(Fanfare) ? multiplier * (1 + SelfSong) : multiplier;
        }

        public double DamageTaken(DamageSource source)
        {
            if (!On)
                return 1;
            var multiplier = 1 + VerseBonus + HymnBonus;
            if (source == DamageSource.Skill && Sings(Hymn))
                multiplier *= 1 + SelfSong;
            if (Lullaby && S.Asleep && Flag(MinstrelStats.SleepLight))
                multiplier *= SleepPower;
            return multiplier;
        }

        public bool Dream(BigNumber damage)
        {
            if (!Lullaby || !S.Asleep || Flag(MinstrelStats.SleepLight))
                return false;
            S.Dream += damage * SleepPower;
            return true;
        }

        public void AfterStrike()
        {
            if (!On)
                return;
            if (Verse && ++S.LineHits >= LineHits)
            {
                S.LineHits = 0;
                AddLines(1);
            }
            if (Song && !Flag(MinstrelStats.SongStill) && ++S.SongHits >= SongHits)
            {
                S.SongHits = 0;
                ChangeMelody();
            }
            if (Lullaby && ++S.NoteHits >= NoteHits)
            {
                S.NoteHits = 0;
                AddNotes(1);
            }
        }

        public void OnSkillCast(GemDef skill)
        {
            if (!On || !(skill.Skill?.Effect is StrikeEffect strike))
                return;
            if (Lullaby && Flag(MinstrelStats.SleepByCast))
                AddNotes(1);
            if (strike.ClimaxHit > 0 && Climax)
                TraitHit(strike.ClimaxHit);
            if (strike.Finale > 0)
                Finale(strike.Finale);
            if (strike.Lines > 0 && Verse)
                AddLines(strike.Lines);
            if (strike.Tune && Song)
                ChangeMelody();
            if (strike.Notes > 0 && Lullaby)
                AddNotes(strike.Notes);
            if (strike.HymnSeconds > 0 && strike.HymnPower > 0)
            {
                S.HymnTicks = System.Math.Max(S.HymnTicks, Ticks(strike.HymnSeconds));
                S.HymnPower = System.Math.Max(S.HymnPower, strike.HymnPower);
            }
            if (strike.SoloSeconds > 0)
                S.SoloTicks = System.Math.Max(S.SoloTicks, Ticks(strike.SoloSeconds));
        }

        public void Tick()
        {
            if (!On)
                return;
            if (S.SpawnPending)
                OnNewEnemy();
            TickVerses();
            TickSong();
            TickSleep();
            if (S.HymnTicks > 0 && --S.HymnTicks == 0)
                S.HymnPower = 0;
            if (S.SoloTicks > 0)
                S.SoloTicks--;
            _state.TraitPartyDamage = HymnBonus;
            S.PartyVerse = VerseBonus;
            S.PartySong = PartySong;
            S.PartySleep = Lullaby && S.Asleep ? System.Math.Max(0, SleepPower - 1) : 0;
            S.Chorus = InChorus;
        }

        private void OnNewEnemy()
        {
            S.SpawnPending = false;
            S.EncoreUsed = false;
            S.SleepTicks = 0;
            S.Dream = BigNumber.Zero;
            if (Flag(MinstrelStats.SleepForget))
            {
                S.Notes = 0;
                S.NoteHits = 0;
            }
            if (Lullaby && _state.FightingBoss && Flag(MinstrelStats.SleepOnBoss))
                FallAsleep(Ticks(_rules.SleepBossSeconds));
        }

        private void AddLines(int lines)
        {
            S.Lines += lines;
            while (S.Lines >= _rules.LinesPerVerse)
            {
                S.Lines -= _rules.LinesPerVerse;
                AddVerse();
            }
        }

        private void AddVerse()
        {
            S.Verses = System.Math.Min(VerseMax, S.Verses + 1);
            S.VerseTicks = VerseTicks;
            if (InChorus && Flag(MinstrelStats.ChorusEndless))
                S.ChorusTicks += Ticks(_rules.EndlessSeconds);
            if (InChorus || ++S.Row < _rules.ChorusVerses)
                return;
            StartChorus();
        }

        private void StartChorus()
        {
            S.Row = 0;
            S.Choruses++;
            S.ChorusTicks = Ticks(_rules.ChorusSeconds);
            if (!Flag(MinstrelStats.ChorusGift))
                return;
            _state.PendingResource += _rules.ChorusGift;
            _state.TraitPartyResourceGift = System.Math.Max(_state.TraitPartyResourceGift, _rules.ChorusGift);
        }

        private void TickVerses()
        {
            if (!Verse)
            {
                S.Verses = 0;
                S.Lines = 0;
                S.Row = 0;
                S.ChorusTicks = 0;
                return;
            }
            if (S.ChorusTicks > 0 && --S.ChorusTicks == 0)
                EndChorus();
            if (S.VerseTicks <= 0 || --S.VerseTicks > 0)
                return;
            S.Verses = 0;
            S.Row = 0;
        }

        private void EndChorus()
        {
            if (Flag(MinstrelStats.ChorusEncore) && !S.EncoreUsed)
            {
                S.EncoreUsed = true;
                S.ChorusTicks = Ticks(_rules.ChorusSeconds);
                return;
            }
            if (!Flag(MinstrelStats.ChorusSpend))
                return;
            S.Verses = 0;
            S.Row = 0;
            S.VerseTicks = 0;
        }

        private void Finale(double perVerse)
        {
            var verses = Verse ? S.Verses : 0;
            TraitHit(perVerse * System.Math.Max(1, verses));
            if (verses <= 0)
                return;
            S.Verses = 0;
            S.Row = 0;
            S.VerseTicks = 0;
        }

        private void ChangeMelody()
        {
            S.Melody = (S.Melody + 1) % MinstrelState.MelodyCount;
            S.MelodyChanges++;
            var hit = Positive(MinstrelStats.SongChangeHit);
            if (hit > 0)
                TraitHit(hit);
            if (!Flag(MinstrelStats.SongGift))
                return;
            _state.PendingResource += _rules.SongGift;
            _state.TraitPartyResourceGift = System.Math.Max(_state.TraitPartyResourceGift, _rules.SongGift);
        }

        private void TickSong()
        {
            if (!Song)
            {
                S.SongAllTicks = 0;
                S.SongAllWaitTicks = 0;
                S.MarchCarry = 0;
                return;
            }
            if (S.SongAllTicks > 0)
                S.SongAllTicks--;
            if (Flag(MinstrelStats.SongAll) && ++S.SongAllWaitTicks >= Ticks(_rules.SongAllEverySeconds))
            {
                S.SongAllWaitTicks = 0;
                S.SongAllTicks = Ticks(_rules.SongAllSeconds);
            }
            if (Sings(March))
                MarchCooldowns(SelfSong);
        }

        private void MarchCooldowns(double speed)
        {
            S.MarchCarry += speed;
            if (S.MarchCarry < 1)
                return;
            var ticks = (int)S.MarchCarry;
            S.MarchCarry -= ticks;
            for (var i = 0; i < _state.Skills.Count; i++)
            {
                var slot = _state.Skills[i];
                if (slot.CooldownTicks > 1)
                    slot.CooldownTicks = System.Math.Max(1, slot.CooldownTicks - ticks);
            }
        }

        private void AddNotes(int notes)
        {
            if (Flag(MinstrelStats.SleepOnBoss) && !_state.FightingBoss)
                return;
            S.Notes += notes;
            if (S.Asleep || S.Notes < SleepNotes)
                return;
            S.Notes = 0;
            FallAsleep(SleepTicks);
        }

        private void FallAsleep(int ticks)
        {
            if (!_state.EnemyAlive || ticks <= 0)
                return;
            S.SleepTicks = ticks;
            S.Sleeps++;
        }

        private void TickSleep()
        {
            if (!Lullaby)
            {
                S.Notes = 0;
                S.SleepTicks = 0;
                S.Dream = BigNumber.Zero;
                return;
            }
            if (S.SleepTicks <= 0 || --S.SleepTicks > 0)
                return;
            Wake();
        }

        private void Wake()
        {
            var dream = S.Dream;
            S.Dream = BigNumber.Zero;
            _combat.DirectHit(dream);
        }

        private void TraitHit(double multiplier)
        {
            if (multiplier <= 0 || !_state.EnemyAlive)
                return;
            var supporting = _combat.Supporting;
            _combat.Supporting = null;
            _combat.SkillHit(_combat.BaseKeyDamage, multiplier * _state.FocusMultiplier, StatTags.Keyboard);
            _combat.Supporting = supporting;
        }

        private static int Ticks(double seconds) => (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
    }
}
