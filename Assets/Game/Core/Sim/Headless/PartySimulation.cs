using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Synergy;
using Game.Core.Input;
using Game.Core.Math;
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
        private readonly double _damageMultiplier;

        public PartySimulation(GameData data, BotProfile profile, IReadOnlyList<HeroClass> classes, ulong seed = 1, double loss = 0, double damageMultiplier = 1)
        {
            _damageMultiplier = damageMultiplier;
            _data = data;
            _profile = profile;
            _classes = classes;
            _seed = seed;
            _loss = loss;
        }

        public IReadOnlyDictionary<HeroClass, IReadOnlyList<GemDef>>? Kits { get; set; }
        public SynergyRules? Synergy { get; set; }
        public int IdleMembers { get; set; }
        public double IdleAfterSeconds { get; set; }
        public double SampleSeconds { get; set; }
        public int HeroLevel { get; set; }

        public PartySimulationResult Run(double seconds)
        {
            var synergy = Synergy ?? _data.Synergy;
            var network = new LoopbackNetwork(_seed, MinLatencyMs, MaxLatencyMs, _loss, 4096);
            var host = new PartyHost(new PartyEncounter(_data.Party, _data.Stages, _seed), network.Connect(HostId), _data.Party, _data.Rift, synergy);
            var loops = new GameLoop[_classes.Count];
            var clients = new PartyClient[_classes.Count];
            var kills = new long[_classes.Count];
            var fails = new int[_classes.Count];
            var damage = new BigNumber[_classes.Count];
            var uptime = new SynergyUptime[_classes.Count];
            var inputs = new IdleAfterInput?[_classes.Count];
            var samples = new List<PartySample>();
            for (var i = 0; i < loops.Length; i++)
            {
                var memberSeed = _seed + (ulong)i * 7919;
                var hero = new StatSheet();
                if (_damageMultiplier != 1)
                    hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, _damageMultiplier - 1));
                var state = new GameState(memberSeed) { Class = _classes[i] };
                if (HeroLevel > 0)
                    state.HeroLevel = HeroLevel;
                if (Kits != null && Kits.TryGetValue(_classes[i], out var kit))
                    state.OwnedGems.AddRange(kit);
                IInputSource input = new BotInput(_profile, memberSeed ^ 0x5EEDUL);
                if (i >= loops.Length - IdleMembers)
                {
                    inputs[i] = new IdleAfterInput(input, IdleAfterSeconds);
                    input = inputs[i]!;
                }
                loops[i] = new GameLoop(_data, input, state, hero);
                if (Synergy != null)
                    loops[i].Synergy.UseRules(Synergy);
                clients[i] = new PartyClient(loops[i], network.Connect(FirstMemberId + (ulong)i), HostId, _data.Party);
                clients[i].Join();
                var bot = new PlayerBot(_data, loops[i], double.PositiveInfinity, inParty: true);
                bot.EquipStartingSkills();
                if (HeroLevel > 0)
                    bot.SpendTreePoints();
                uptime[i] = new SynergyUptime(synergy);
            }

            var totalTicks = (long)System.Math.Round(seconds * GameLoop.TicksPerSecond);
            for (var tick = 0L; tick < totalTicks; tick++)
            {
                network.Advance(GameLoop.TickSeconds * 1000);
                for (var i = 0; i < loops.Length; i++)
                {
                    if (inputs[i] == null || !inputs[i]!.Idle)
                        CastEverything(loops[i]);
                    loops[i].Tick();
                    damage[i] += loops[i].Combat.LastTickDamage * BigNumber.FromDouble(clients[i].DamageTaken);
                    uptime[i].Count(loops[i].Synergy);
                    kills[i] += loops[i].Events.CountOf(GameEventKind.EnemyKilled);
                    fails[i] += loops[i].Events.CountOf(GameEventKind.BossFailed);
                    loops[i].Events.Clear();
                }
                host.Tick();
                if (SampleSeconds > 0 && (tick + 1) % (long)System.Math.Round(SampleSeconds * GameLoop.TicksPerSecond) == 0)
                    samples.Add(Sample((tick + 1) * GameLoop.TickSeconds, host, clients));
            }

            var members = new List<PartyMemberResult>();
            for (var i = 0; i < loops.Length; i++)
            {
                var state = loops[i].State;
                var share = host.Kills > 0 ? clients[i].PartyRewards / (double)host.Kills : 0;
                members.Add(new PartyMemberResult(_classes[i], state.Stage, state.HighestStage, kills[i], fails[i], state.RunGoldEarned, share, clients[i].MissedKills)
                {
                    Damage = damage[i],
                    SharesDealt = clients[i].SharesDealt,
                    Synergy = uptime[i]
                });
            }
            return new PartySimulationResult(seconds, host.Kills, members) { Samples = samples };
        }

        private static PartySample Sample(double seconds, PartyHost host, PartyClient[] clients)
        {
            var rewards = new long[clients.Length];
            var shares = new double[clients.Length];
            var banks = new long[clients.Length];
            for (var i = 0; i < clients.Length; i++)
            {
                var slot = clients[i].OwnSlot;
                rewards[i] = clients[i].PartyRewards;
                shares[i] = host.SharesOf(slot);
                banks[i] = slot >= 0 ? host.Encounter.Member(slot).RestBankTicks / GameLoop.TicksPerSecond : 0;
            }
            return new PartySample(seconds, host.Kills, rewards, shares, banks);
        }

        private static void CastEverything(GameLoop loop)
        {
            for (var slot = 0; slot < loop.State.Skills.Count; slot++)
                loop.RequestCast(slot);
        }
    }
}
