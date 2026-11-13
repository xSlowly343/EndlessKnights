using Game.Core.Party;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public sealed class AllyPortals
    {
        public const int PortalTicks = 15;

        private readonly int[] _slot;
        private readonly StripPlace[] _place;
        private readonly bool[] _known;
        private readonly StripPlace[] _to;
        private readonly bool[] _leaving;
        private readonly int[] _ticksLeft;

        public AllyPortals(int capacity)
        {
            _slot = new int[capacity];
            _place = new StripPlace[capacity];
            _known = new bool[capacity];
            _to = new StripPlace[capacity];
            _leaving = new bool[capacity];
            _ticksLeft = new int[capacity];
        }

        public int Capacity => _slot.Length;

        public static bool PlaceOf(StripAlly ally, out StripPlace place)
        {
            place = ally.Presence switch
            {
                PartyPresence.Peaceful => ally.Pastime.Known ? ally.Pastime.Place : StripPlace.Hideout,
                _ => StripPlace.Home
            };
            return ally.Presence != PartyPresence.Offline;
        }

        public void Observe(int index, StripAlly ally, StripPlace own)
        {
            if (index < 0 || index >= Capacity)
                return;
            if (!PlaceOf(ally, out var place))
            {
                _known[index] = false;
                _ticksLeft[index] = 0;
                return;
            }
            if (_known[index] && _slot[index] == ally.Slot && _place[index] != place)
            {
                _leaving[index] = place != own;
                _to[index] = _leaving[index] ? place : _place[index];
                _ticksLeft[index] = PortalTicks;
            }
            _slot[index] = ally.Slot;
            _place[index] = place;
            _known[index] = true;
        }

        public void Forget(int index)
        {
            if (index < 0 || index >= Capacity)
                return;
            _known[index] = false;
            _ticksLeft[index] = 0;
        }

        public void Tick()
        {
            for (var i = 0; i < _ticksLeft.Length; i++)
            {
                if (_ticksLeft[i] > 0)
                    _ticksLeft[i]--;
            }
        }

        public bool Active(int index) => index >= 0 && index < Capacity && _ticksLeft[index] > 0;

        public StripPlace Destination(int index) => _to[index];

        public bool Leaving(int index) => _leaving[index];

        public double Progress(int index) => Active(index) ? 1 - (double)_ticksLeft[index] / PortalTicks : 1;
    }
}
