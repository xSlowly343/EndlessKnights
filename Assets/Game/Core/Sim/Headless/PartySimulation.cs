using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Party;
using Game.Core.Party.Transport;
using Game.Core.Stats;

namespace Game.Core.Sim.Headless
{
    public sealed class PartySimulation
    {
        private const ulong HostId = 1;
        private const ulong FirstMemberId = 100;
        private const double MinLatencyMs = 50;
        private const double MaxLatencyMs = 150;

        private readonly GameData _data;
        private readonly BotProfile _profile;
        private readonly IReadOnlyList<HeroClass> _classes;
        private readonly ulong _seed;
        private readonly double _loss;

        public PartySimulation(GameData data, BotProfile profile, IReadOnlyList<HeroClass> classes, ulong seed = 1, double loss = 0)
        {
            _data = data;
            _profile = profile;
            _classes = classes;
            _seed = seed;
            _loss = loss;
        }

        public PartySimulationResult Run(double seconds)
        {
            var network = new LoopbackNetwork(_seed, MinLatencyMs, MaxLatencyMs, _loss, 4096);
            var host = new PartyHost(new PartyEncounter(_data.Party, _data.Stages, _seed), network.Connect(HostId), _data.Party);
            var loops = new GameLoop[_classes.Count];
            var clients = new PartyClient[_classes.Count];
            var kills = new long[_classes.Count];
            var fails = new int[_classes.Count];
            for (var i = 0; i < loops.Length; i++)
            {
                var memberSeed = _seed + (ulong)i * 7919;
                loops[i] = new GameLoop(_data, new BotInput(_profile, memberSeed ^ 0x5EEDUL), new GameState(memberSeed) { Class = _classes[i] }, new StatSheet());
                clients[i] = new PartyClient(loops[i], network.Connect(FirstMemberId + (ulong)i), HostId, _data.Party);
                clients[i].Join();
            }

            var totalTicks = (long)System.Math.Round(seconds * GameLoop.TicksPerSecond);
            for (var tick = 0L; tick < totalTicks; tick++)
            {
                network.Advance(GameLoop.TickSeconds * 1000);
                for (var i = 0; i < loops.Length; i++)
                {
                    loops[i].Tick();
                    kills[i] += loops[i].Events.CountOf(GameEventKind.EnemyKilled);
                    fails[i] += loops[i].Events.CountOf(GameEventKind.BossFailed);
                    loops[i].Events.Clear();
                }
                host.Tick();
            }

            var members = new List<PartyMemberResult>();
            for (var i = 0; i < loops.Length; i++)
            {
                var state = loops[i].State;
                var share = host.Kills > 0 ? clients[i].PartyRewards / (double)host.Kills : 0;
                members.Add(new PartyMemberResult(_classes[i], state.Stage, state.HighestStage, kills[i], fails[i], state.RunGoldEarned, share));
            }
            return new PartySimulationResult(seconds, host.Kills, members);
        }
    }
}
