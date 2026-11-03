using System.Collections.Generic;
using Game.Core.Sim;
using Game.Core.Sim.Rift;

namespace Game.Core.Mail
{
    public sealed class MailBox
    {
        private readonly List<MailLetter> _letters = new List<MailLetter>();
        private readonly HashSet<string> _seen = new HashSet<string>();

        public IReadOnlyList<MailLetter> Letters => _letters;
        public int Unread { get; private set; }

        public IReadOnlyList<MailLetter> Replace(IReadOnlyList<MailLetter> letters)
        {
            var arrived = new List<MailLetter>();
            _letters.Clear();
            Unread = 0;
            foreach (var letter in letters)
            {
                _letters.Add(letter);
                if (letter.Claimed)
                    continue;
                Unread++;
                if (_seen.Add(letter.Id))
                    arrived.Add(letter);
            }
            return arrived;
        }

        public MailLetter? Find(string id)
        {
            foreach (var letter in _letters)
            {
                if (letter.Id == id)
                    return letter;
            }
            return null;
        }

        public bool Take(MailLetter claimed, GameState state)
        {
            var local = Find(claimed.Id);
            if (local != null && local.Claimed)
                return false;
            RiftRewards.Apply(state, claimed.Reward);
            if (local == null)
                return true;
            local.MarkClaimed();
            Unread = System.Math.Max(0, Unread - 1);
            return true;
        }
    }
}
