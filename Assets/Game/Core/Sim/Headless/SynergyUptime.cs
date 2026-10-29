using Game.Core.Content.Synergy;
using Game.Core.Party;

namespace Game.Core.Sim.Headless
{
    public sealed class SynergyUptime
    {
        private readonly long[] _responses;
        private readonly long[] _reactions;
        private readonly long[] _constellations;
        private readonly long[] _ownMarks;
        private readonly long[] _allyMarks;

        public SynergyUptime(SynergyRules rules)
        {
            Rules = rules;
            _responses = new long[rules.Responses.Count];
            _reactions = new long[rules.Reactions.Count];
            _constellations = new long[rules.Constellations.Count];
            _ownMarks = new long[rules.Marks.Count];
            _allyMarks = new long[rules.Marks.Count];
        }

        public SynergyRules Rules { get; }
        public long Ticks { get; private set; }
        public long AnyResponseTicks { get; private set; }

        public double Response(int index) => Share(_responses[index]);
        public double Reaction(int index) => Share(_reactions[index]);
        public double Constellation(int index) => Share(_constellations[index]);
        public double OwnMark(int mark) => Share(_ownMarks[mark]);
        public double AllyMark(int mark) => Share(_allyMarks[mark]);
        public double AnyResponse => Share(AnyResponseTicks);

        public void Count(SynergySystem synergy)
        {
            Ticks++;
            var any = false;
            for (var i = 0; i < _responses.Length; i++)
            {
                if (!synergy.ResponseActive(i))
                    continue;
                _responses[i]++;
                any = true;
            }
            if (any)
                AnyResponseTicks++;
            for (var i = 0; i < _reactions.Length; i++)
            {
                if (synergy.ReactionActive(i))
                    _reactions[i]++;
            }
            for (var i = 0; i < _constellations.Length; i++)
            {
                if (synergy.ConstellationActive(i))
                    _constellations[i]++;
            }
            for (var mark = 0; mark < _ownMarks.Length; mark++)
            {
                if ((synergy.OwnMarks & (1 << mark)) != 0)
                    _ownMarks[mark]++;
                if ((synergy.AllyMarks & (1 << mark)) != 0)
                    _allyMarks[mark]++;
            }
        }

        private double Share(long ticks) => Ticks > 0 ? ticks / (double)Ticks : 0;
    }
}
