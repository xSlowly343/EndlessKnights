using Game.Core.Text;

namespace Game.Core.Ui
{
    public readonly struct PartySearchRow
    {
        private const string Separator = " · ";

        private PartySearchRow(string partyId, string title, string details, string seats, bool activeNow, bool wantsYou)
        {
            PartyId = partyId;
            Title = title;
            Details = details;
            Seats = seats;
            ActiveNow = activeNow;
            WantsYou = wantsYou;
        }

        public string PartyId { get; }
        public string Title { get; }
        public string Details { get; }
        public string Seats { get; }
        public bool ActiveNow { get; }
        public bool WantsYou { get; }

        public string Tags
        {
            get
            {
                var tags = ActiveNow ? Texts.Get("party.search.activeNow") : string.Empty;
                if (WantsYou)
                    tags += (tags.Length > 0 ? Separator : string.Empty) + Texts.Get("party.search.wantsYou");
                return tags;
            }
        }

        public static PartySearchRow From(string partyId, string titleId, string language, string wanted, int members, int maxMembers,
            bool activeNow, string? usuallyOnline, bool wantsYou)
        {
            var title = titleId.Length > 0 && Texts.Has("party.title." + titleId) ? PartyCardText.Title(titleId) : Texts.Get("party.search.untitled");
            var details = Texts.Has("settings.language." + language) ? PartyCardText.Language(language) : language.ToUpperInvariant();
            if (!string.IsNullOrEmpty(usuallyOnline) && Texts.Has("party.time." + usuallyOnline))
                details += Separator + PartyCardText.UsuallyOnline(usuallyOnline!);
            if (wanted.Length > 0 && Texts.Has("party.role." + wanted))
                details += Separator + PartyCardText.Wanted(wanted);
            var seats = Texts.Format("party.search.seats", members, maxMembers);
            return new PartySearchRow(partyId, title, details, seats, activeNow, wantsYou);
        }
    }
}
