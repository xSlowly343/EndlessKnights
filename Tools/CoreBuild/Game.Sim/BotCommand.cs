using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Sim.Headless;
using Game.Core.Stats;
using Game.Net.Nakama;

namespace Game.Sim
{
    public static class BotCommand
    {
        public const string Usage =
            "sim bot --server https://host --key <server key> (--create [--open] | --code K7M-Q2X | --pair) [--seconds 60] [--kps 6] [--class viking] [--stay]";

        private const ulong BotSteamBase = 76561190000000000UL;

        public static int Run(GameData data, IReadOnlyList<string> args)
        {
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < args.Count; i++)
            {
                var name = args[i];
                var flag = name == "--create" || name == "--open" || name == "--pair" || name == "--stay";
                if (flag)
                    options[name] = "yes";
                else if (i + 1 < args.Count)
                    options[name] = args[++i];
                else
                    return Fail("option " + name + " needs a value");
            }
            if (!options.TryGetValue("--server", out var server) || !options.TryGetValue("--key", out var key))
                return Fail("--server and --key are required");
            var seconds = Number(options, "--seconds", 60);
            var profile = new BotProfile(Number(options, "--kps", 6));
            var heroClass = options.TryGetValue("--class", out var className) && HeroClassNames.ByName.TryGetValue(className, out var named) ? named : HeroClass.Viking;
            var client = new NakamaClient(server, key);
            try
            {
                if (options.ContainsKey("--pair"))
                    return RunPair(data, client, profile, seconds).GetAwaiter().GetResult();
                var stay = options.ContainsKey("--stay");
                if (options.TryGetValue("--code", out var code))
                    return RunOne(data, client, profile, heroClass, seconds, bot => bot.JoinAsync(code), stay).GetAwaiter().GetResult();
                if (options.ContainsKey("--create"))
                    return RunOne(data, client, profile, heroClass, seconds, bot => bot.CreateAsync(options.ContainsKey("--open")), stay).GetAwaiter().GetResult();
                return Fail("choose --create, --code or --pair");
            }
            catch (NakamaException error)
            {
                return Fail("server: " + error.Message);
            }
        }

        private static async Task<int> RunOne(GameData data, NakamaClient client, BotProfile profile, HeroClass heroClass, double seconds, Func<Bot, Task> enter, bool stay)
        {
            var bot = await Bot.LoginAsync(data, client, profile, heroClass, 1).ConfigureAwait(false);
            await enter(bot).ConfigureAwait(false);
            Console.WriteLine("party code " + PartySearchRules.FormatCode(bot.Party!.Code) + ", members " + bot.Party.Members.Count);
            await bot.ConnectAsync().ConfigureAwait(false);
            Play(new[] { bot }, seconds);
            bot.Report(Console.Out);
            await bot.FinishAsync(stay).ConfigureAwait(false);
            return bot.Client!.Status == PartyLink.Linked ? 0 : 1;
        }

        private static async Task<int> RunPair(GameData data, NakamaClient client, BotProfile profile, double seconds)
        {
            var leader = await Bot.LoginAsync(data, client, profile, HeroClass.Viking, 1).ConfigureAwait(false);
            var friend = await Bot.LoginAsync(data, client, profile, HeroClass.Voodoo, 2).ConfigureAwait(false);
            await leader.CreateAsync(false).ConfigureAwait(false);
            await friend.JoinAsync(leader.Party!.Code.ToLowerInvariant()).ConfigureAwait(false);
            Console.WriteLine("party code " + PartySearchRules.FormatCode(leader.Party.Code) + ", members " + friend.Party!.Members.Count);
            await leader.ConnectAsync().ConfigureAwait(false);
            await friend.ConnectAsync().ConfigureAwait(false);
            Play(new[] { leader, friend }, seconds);
            leader.Report(Console.Out);
            friend.Report(Console.Out);
            var linked = leader.Client!.Status == PartyLink.Linked && friend.Client!.Status == PartyLink.Linked;
            var sameEnemy = leader.Client.EnemyId == friend.Client!.EnemyId;
            Console.WriteLine("both linked " + linked + ", same enemy " + sameEnemy);
            await friend.FinishAsync(false).ConfigureAwait(false);
            await leader.FinishAsync(false).ConfigureAwait(false);
            return linked && leader.Client.PartyRewards > 0 && friend.Client.PartyRewards > 0 ? 0 : 1;
        }

        private static void Play(IReadOnlyList<Bot> bots, double seconds)
        {
            var ticks = (long)Math.Round(seconds * GameLoop.TicksPerSecond);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (var tick = 0L; tick < ticks; tick++)
            {
                foreach (var bot in bots)
                    bot.Tick();
                var due = (tick + 1) * 1000 / GameLoop.TicksPerSecond;
                var wait = due - clock.ElapsedMilliseconds;
                if (wait > 0)
                    Thread.Sleep((int)wait);
            }
        }

        private static double Number(Dictionary<string, string> options, string name, double fallback) =>
            options.TryGetValue(name, out var text) ? double.Parse(text, CultureInfo.InvariantCulture) : fallback;

        private static int Fail(string message)
        {
            Console.Error.WriteLine(message);
            Console.Error.WriteLine(Usage);
            return 2;
        }

        private sealed class Bot
        {
            private readonly GameData _data;
            private readonly NakamaClient _client;
            private readonly NakamaSession _session;
            private readonly PartyDirectory _directory;
            private readonly GameLoop _loop;
            private readonly ulong _playerNumber;
            private OnlineParty? _online;

            private Bot(GameData data, NakamaClient client, NakamaSession session, GameLoop loop, ulong playerNumber)
            {
                _data = data;
                _client = client;
                _session = session;
                _directory = new PartyDirectory(client, session);
                _loop = loop;
                _playerNumber = playerNumber;
            }

            public PartyView? Party { get; private set; }
            public PartyClient? Client { get; private set; }
            public long Kills { get; private set; }

            public static async Task<Bot> LoginAsync(GameData data, NakamaClient client, BotProfile profile, HeroClass heroClass, int index)
            {
                var session = await client.AuthenticateDeviceAsync("bot-" + Guid.NewGuid().ToString("N")).ConfigureAwait(false);
                var seed = (ulong)Environment.TickCount64 * 31 + (ulong)index;
                var loop = new GameLoop(data, new BotInput(profile, seed), new GameState(seed) { Class = heroClass }, new StatSheet());
                new PlayerBot(data, loop, double.PositiveInfinity).EquipStartingSkills();
                var playerNumber = BotSteamBase + (ulong)(Math.Abs(BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0)) % 1000000000);
                return new Bot(data, client, session, loop, playerNumber);
            }

            public async Task CreateAsync(bool open) => Party = await _directory.CreateAsync(open ? "open" : "invite", null, null, null).ConfigureAwait(false);

            public async Task JoinAsync(string code) => Party = await _directory.JoinByCodeAsync(code).ConfigureAwait(false);

            public async Task ConnectAsync()
            {
                _online = await OnlineParty.ConnectAsync(_client, _session, _playerNumber).ConfigureAwait(false)
                    ?? throw new NakamaException(0, "the bot is not in a party");
                Client = new PartyClient(_loop, _online.Transport, NakamaPartyTransport.HostId, _data.Party);
                Client.Join();
            }

            public void Tick()
            {
                for (var slot = 0; slot < _loop.State.Skills.Count; slot++)
                    _loop.RequestCast(slot);
                _loop.Tick();
                Kills += _loop.Events.CountOf(GameEventKind.EnemyKilled);
                _loop.Events.Clear();
            }

            public void Report(System.IO.TextWriter output)
            {
                var client = Client!;
                var own = client.OwnSlot;
                output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{0} {1}: link {2}, slot {3}, paid kills {4}, unpaid {5}, share {6:P0}, stage {7}, enemy {8}",
                    HeroClassNames.Of(_loop.State.Class), _playerNumber, client.Status, own, client.PartyRewards, client.UnpaidKills,
                    own >= 0 ? client.Contribution(own) : 0, _loop.State.Stage, client.EnemyId));
            }

            public async Task FinishAsync(bool stay)
            {
                _online?.Dispose();
                if (!stay)
                    await _directory.LeaveAsync().ConfigureAwait(false);
            }
        }
    }
}
