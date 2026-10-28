using Game.Core.Party;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public sealed class PartySearchFilters
    {
        private const int Any = -1;

        private readonly PartySearchRules _rules;
        private int _language;
        private int _slot = Any;
        private int _wanted = Any;

        public PartySearchFilters(PartySearchRules rules, string language)
        {
            _rules = rules;
            _language = System.Math.Max(0, IndexOf(rules.Languages, language));
        }

        public string Language => _rules.Languages[_language];
        public string? Slot => _slot == Any ? null : _rules.TimeSlots[_slot].Id;
        public string? Wanted => _wanted == Any ? null : _rules.Roles[_wanted];
        public int Version { get; private set; }

        public string LanguageLabel => Texts.Format("party.search.language", PartyCardText.Language(Language));
        public string SlotLabel => Texts.Format("party.search.when", Slot == null ? Texts.Get("party.search.anyTime") : Texts.Get("party.time." + Slot));
        public string WantedLabel => Texts.Format("party.search.wantedFilter", Wanted == null ? Texts.Get("party.search.allParties") : PartyCardText.Role(Wanted));

        public void NextLanguage()
        {
            _language = (_language + 1) % _rules.Languages.Count;
            Version++;
        }

        public void NextSlot()
        {
            _slot = Cycle(_slot, _rules.TimeSlots.Count);
            Version++;
        }

        public void NextWanted()
        {
            _wanted = Cycle(_wanted, _rules.Roles.Count);
            Version++;
        }

        private static int Cycle(int index, int count) => index + 1 >= count ? Any : index + 1;

        private static int IndexOf(System.Collections.Generic.IReadOnlyList<string> values, string value)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                    return i;
            }
            return -1;
        }
    }
}
