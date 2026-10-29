using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Sim.Headless;
using Game.Core.Stats;
using Game.Core.Ui;
using Game.Net.Nakama;

namespace Game.Sim
{
    public static class BotCommand
    {
        public const string Usage =
            "sim bot --server https://host --key <server key> (--create [--open] | --code K7M-Q2X | --find | --quick | --pair [--rift] | --ghost | --clan new|open|quick|K7M-Q2X [--count 1] | --raid [--count 3]) [--seconds 60] [--kps 6] [--class viking] [--stay] [--chronicle] [--report cheating]";

        private const ulong BotSteamBase = 76561190000000000UL;

        public static int Run(GameData data, IReadOnlyList<string> args)
        {
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < args.Count; i++)
            {
                var name = args[i];
                var flag = name == "--create" || name == "--open" || name == "--pair" || name == "--rift" || name == "--chronicle" || name == "--ghost" || name == "--stay" || name == "--find" || name == "--quick" || name == "--raid";
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
                if (options.ContainsKey("--raid"))
                    return RunRaid(data, client, profile, seconds, (int)Number(options, "--count", 3)).GetAwaiter().GetResult();
                if (options.TryGetValue("--clan", out var clan))
                    return RunClan(client, clan, (int)Number(options, "--count", 1)).GetAwaiter().GetResult();
                if (options.ContainsKey("--ghost"))
                    return RunGhost(data, client, profile, heroClass, seconds).GetAwaiter().GetResult();
                if (options.ContainsKey("--pair"))
                    return RunPair(data, client, profile, seconds, options.ContainsKey("--rift")).GetAwaiter().GetResult();
                var stay = options.ContainsKey("--stay");
                if (options.TryGetValue("--code", out var code))
                    return RunOne(data, client, profile, heroClass, seconds, bot => bot.JoinAsync(code), stay, options.TryGetValue("--report", out var codeReason) ? codeReason : null,
                        options.ContainsKey("--chronicle")).GetAwaiter().GetResult();
                options.TryGetValue("--report", out var reason);
                if (options.ContainsKey("--create"))
                    return RunOne(data, client, profile, heroClass, seconds, bot => bot.CreateAsync(options.ContainsKey("--open")), stay, reason, options.ContainsKey("--chronicle")).GetAwaiter().GetResult();
                if (options.ContainsKey("--find"))
                    return RunOne(data, client, profile, heroClass, seconds, bot => bot.FindAsync(Console.Out), stay, reason).GetAwaiter().GetResult();
                if (options.ContainsKey("--quick"))
                    return RunOne(data, client, profile, heroClass, seconds, bot => bot.QuickAsync(), stay, reason).GetAwaiter().GetResult();
                return Fail("choose --create, --code, --find, --quick or --pair");
            }
            catch (NakamaException error)
            {
                return Fail("server: " + error.Message);
            }
        }

        private static async Task<int> RunOne(GameData data, NakamaClient client, BotProfile profile, HeroClass heroClass, double seconds, Func<Bot, Task> enter, bool stay,
            string? reportReason, bool chronicle = false)
        {
            var bot = await Bot.LoginAsync(data, client, profile, heroClass, 1).ConfigureAwait(false);
            await enter(bot).ConfigureAwait(false);
            Console.WriteLine("party code " + PartySearchRules.FormatCode(bot.Party!.Code) + ", members " + bot.Party.Members.Count);
            await bot.ConnectAsync().ConfigureAwait(false);
            Play(new[] { bot }, seconds);
            bot.Report(Console.Out);
            if (chronicle)
                await bot.ShowChronicleAsync(Console.Out).ConfigureAwait(false);
            if (reportReason != null)
                await bot.ReportOtherAsync(reportReason, Console.Out).ConfigureAwait(false);
            await bot.FinishAsync(stay).ConfigureAwait(false);
            return bot.Client.Status == PartyLink.Linked ? 0 : 1;
        }

        private static async Task<int> RunClan(NakamaClient client, string how, int count)
        {
            ClanView? clan = null;
            for (var i = 0; i < count; i++)
            {
                var device = "bot-" + Guid.NewGuid().ToString("N");
                var online = await OnlineSession.StartAsync(client, cancel => client.AuthenticateDeviceAsync(device, cancel), BotSteamBase + (ulong)i).ConfigureAwait(false);
                var clans = online.Clans;
                clan = how switch
                {
                    "new" => await clans.CreateAsync("invite", null, null, null).ConfigureAwait(false),
                    "open" => await clans.CreateAsync("open", null, null, null).ConfigureAwait(false),
                    "quick" => await clans.QuickAsync("ru").ConfigureAwait(false),
                    _ => await clans.JoinByCodeAsync(how).ConfigureAwait(false)
                };
                if (clan == null)
                    return Fail("no open clan to join");
                if (how == "new" || how == "open")
                    how = clan.Code;
            }
            Console.WriteLine(ClanText.Name(clan!.Adjective, clan.Noun) + " [" + ClanText.Tag(clan.Adjective, clan.Noun) + "] · " + ClanText.Members(clan.Members.Count, clan.MaxMembers)
                + " · " + ClanText.ActiveNow(clan.ActiveNow) + " · " + ClanText.Code(clan.Code));
            return 0;
        }

        private static async Task<int> RunRaid(GameData data, NakamaClient client, BotProfile profile, double seconds, int count)
        {
            var bots = new List<Bot>();
            string? code = null;
            for (var i = 0; i < count; i++)
            {
                var bot = await Bot.LoginAsync(data, client, profile, (HeroClass)(i % ((int)HeroClass.Captain + 1)), i + 1).ConfigureAwait(false);
                code = await bot.EnterClanAsync(code).ConfigureAwait(false);
                bots.Add(bot);
            }
            var pushTicks = (long)System.Math.Round(data.Raid.PushSeconds * GameLoop.TicksPerSecond);
            for (var played = 0L; played < seconds * GameLoop.TicksPerSecond; played += pushTicks)
            {
                Play(bots, System.Math.Min(data.Raid.PushSeconds, seconds - (double)played / GameLoop.TicksPerSecond));
                foreach (var bot in bots)
                    await bot.PushRaidAsync().ConfigureAwait(false);
            }
            var raid = await bots[0].PushRaidAsync().ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Console.WriteLine(RaidText.Boss(raid.Boss) + " · " + RaidText.EndsIn(raid.EndsAtUnixMs, now) + " · " + RaidText.Hp(raid.Left) + ", clan " + ClanText.Code(code!));
            foreach (var layer in raid.Layers)
                Console.WriteLine("  " + RaidText.Layer(layer.Id, layer.Left, layer.Hp) + " (" + RaidText.Input(layer.Id) + "), "
                    + (layer.Hp - layer.Left).ToString("0.##", CultureInfo.InvariantCulture) + " of " + layer.Hp.ToString("0", CultureInfo.InvariantCulture));
            foreach (var row in raid.Top)
                Console.WriteLine("  " + row.Place + ". " + row.Name + " " + (row.Share * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%");
            await bots[0].FinishAsync(true).ConfigureAwait(false);
            return raid.Players == count ? 0 : 1;
        }

        private static async Task<int> RunGhost(GameData data, NakamaClient client, BotProfile profile, HeroClass heroClass, double seconds)
        {
            var bot = await Bot.LoginAsync(data, client, profile, heroClass, 1).ConfigureAwait(false);
            Play(new[] { bot }, System.Math.Min(20, seconds / 2));
            var activity = await bot.ShareGhostAsync().ConfigureAwait(false);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "shared own ghost: class {0}, pace {1:0.###} enemies/s, activity {2:P0}",
                HeroClassNames.Of(heroClass), bot.Loop.Ghost.OwnShareRate, activity));
            var ghost = await bot.TakeGhostAsync(data).ConfigureAwait(false);
            Console.WriteLine(ghost == null ? "no ghost to take" : string.Format(CultureInfo.InvariantCulture, "ghost {0} ({1}), pace {2:0.###}, activity {3:P0}",
                ghost.Value.Name, HeroClassNames.Of(ghost.Value.HeroClass), ghost.Value.ShareRate, ghost.Value.Activity));
            Play(new[] { bot }, seconds);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "kills {0}, stage {1}, ghost damage {2}, bosses with the ghost {3}",
                bot.Kills, bot.Loop.State.Stage, NumberFormatter.Format(bot.Loop.Ghost.Damage, NumberFormatSettings.Default), bot.Loop.Ghost.BossesHelped));
            if (ghost != null)
                Console.WriteLine("thanked the ghost's owner: " + await bot.ThankGhostAsync(ghost.Value.UserId).ConfigureAwait(false) + " friendship tokens");
            await bot.ShowWalletAsync(Console.Out).ConfigureAwait(false);
            await bot.FinishAsync(true).ConfigureAwait(false);
            return 0;
        }

        private static async Task<int> RunPair(GameData data, NakamaClient client, BotProfile profile, double seconds, bool rift)
        {
            var leader = await Bot.LoginAsync(data, client, profile, HeroClass.Viking, 1).ConfigureAwait(false);
            var friend = await Bot.LoginAsync(data, client, profile, HeroClass.Voodoo, 2).ConfigureAwait(false);
            await leader.CreateAsync(false).ConfigureAwait(false);
            await friend.JoinAsync(leader.Party!.Code.ToLowerInvariant()).ConfigureAwait(false);
            Console.WriteLine("party code " + PartySearchRules.FormatCode(leader.Party.Code) + ", members " + friend.Party!.Members.Count);
            await leader.ConnectAsync().ConfigureAwait(false);
            await friend.ConnectAsync().ConfigureAwait(false);
            leader.WantsRift = rift;
            Play(new[] { leader, friend }, seconds);
            leader.Report(Console.Out);
            friend.Report(Console.Out);
            var linked = leader.Client.Status == PartyLink.Linked && friend.Client.Status == PartyLink.Linked;
            var sameEnemy = leader.Client.EnemyId == friend.Client.EnemyId;
            var reactionsArrive = leader.ReactionsHeard > 0 && friend.ReactionsHeard > 0;
            Console.WriteLine("both linked " + linked + ", same enemy " + sameEnemy + ", reactions arrive " + reactionsArrive);
            await leader.ShowWalletAsync(Console.Out).ConfigureAwait(false);
            await friend.ShowWalletAsync(Console.Out).ConfigureAwait(false);
            var bothInRift = leader.Client.World == PartyWorld.Rift && friend.Client.World == PartyWorld.Rift;
            if (rift)
                Console.WriteLine("both in the rift " + bothInRift);
            if (bothInRift)
                await leader.ShowRiftBoardAsync(Console.Out).ConfigureAwait(false);
            await friend.FinishAsync(false).ConfigureAwait(false);
            await leader.FinishAsync(false).ConfigureAwait(false);
            return linked && reactionsArrive && leader.Client.PartyRewards > 0 && friend.Client.PartyRewards > 0 && (!rift || bothInRift) ? 0 : 1;
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
            private const int KeepAliveTicks = 50;
            private const int ReactionTicks = 100;
            private const int RiftTicks = 50;

            private readonly OnlineSession _online;
            private readonly GameLoop _loop;
            private readonly PartySearchRules _search;
            private readonly string _language;
            private Task _keepAlive = Task.CompletedTask;
            private long _ticks;
            private int _matchGeneration;
            private readonly int[] _heard = new int[PartyRules.MaxSlots];

            private Bot(GameData data, OnlineSession online, GameLoop loop)
            {
                _online = online;
                _loop = loop;
                _search = data.Party.Search;
                _language = _search.Languages[0];
                Client = new PartyClient(loop, online.Transport, NakamaPartyTransport.HostId, data.Party);
            }

            public PartyView? Party => _online.Party;
            public PartyClient Client { get; }
            public GameLoop Loop => _loop;
            public long Kills { get; private set; }
            public int ReactionsSent { get; private set; }
            public bool WantsRift { get; set; }
            public int ReactionsHeard { get; private set; }
            public BigNumber AllyDamage { get; private set; }

            public static async Task<Bot> LoginAsync(GameData data, NakamaClient client, BotProfile profile, HeroClass heroClass, int index)
            {
                var device = "bot-" + Guid.NewGuid().ToString("N");
                var playerNumber = BotSteamBase + (ulong)(Math.Abs(BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0)) % 1000000000);
                var online = await OnlineSession.StartAsync(client, cancel => client.AuthenticateDeviceAsync(device, cancel), playerNumber).ConfigureAwait(false);
                var seed = (ulong)Environment.TickCount64 * 31 + (ulong)index;
                var loop = new GameLoop(data, new BotInput(profile, seed), new GameState(seed) { Class = heroClass }, new StatSheet());
                new PlayerBot(data, loop, double.PositiveInfinity).EquipStartingSkills();
                return new Bot(data, online, loop);
            }

            public async Task CreateAsync(bool open) => await _online.CreateAsync(open ? "open" : "invite", null).ConfigureAwait(false);

            public async Task JoinAsync(string code) => await _online.JoinByCodeAsync(code).ConfigureAwait(false);

            public async Task QuickAsync() =>
                await _online.QuickAsync(_language, _search.RoleOf(_loop.State.Class), 0).ConfigureAwait(false);

            public async Task FindAsync(System.IO.TextWriter output)
            {
                var found = await _online.ListAsync(_language, _search.RoleOf(_loop.State.Class), null, null, 0).ConfigureAwait(false);
                output.WriteLine("open parties " + found.Count);
                foreach (var card in found)
                {
                    var row = PartySearchRow.From(card.Id, card.Title, card.Language, card.Wanted, card.Members, card.MaxMembers, card.ActiveNow, card.UsuallyOnline, card.WantsYou);
                    output.WriteLine("  " + row.Title + " · " + row.Details + " · " + row.Seats + (row.Tags.Length > 0 ? " · " + row.Tags : string.Empty));
                }
                if (found.Count == 0)
                    throw new NakamaException(0, "no open party to join");
                await _online.JoinOpenAsync(found[0].Id).ConfigureAwait(false);
            }

            public Task<double> ShareGhostAsync()
            {
                var synergy = _loop.Data.Synergy;
                return _online.Ghosts.ShareAsync(_loop.State.Class, _loop.Ghost.OwnShareRate, synergy.IdsOf(_loop.Synergy.OwnMarks), _loop.Ghost.TakeActiveSeconds());
            }

            public async Task<GhostSnapshot?> TakeGhostAsync(GameData data)
            {
                var ghost = await _online.Ghosts.PickAsync(data.Synergy).ConfigureAwait(false);
                if (ghost != null)
                    _loop.Ghost.Use(ghost.Value);
                return ghost;
            }

            public async Task<string> EnterClanAsync(string? code)
            {
                var clan = code == null ? await _online.Clans.CreateAsync("invite", null, null, null).ConfigureAwait(false) : await _online.Clans.JoinByCodeAsync(code).ConfigureAwait(false);
                return clan.Code;
            }

            public Task<RaidView> PushRaidAsync() => _online.PushRaidAsync(_loop.Data.Raid, _loop.Raid.Take());

            public async Task<long> ThankGhostAsync(string ownerId)
            {
                var bosses = _loop.Ghost.TakeThanks();
                return bosses > 0 ? await _online.Ghosts.ThankAsync(ownerId, bosses).ConfigureAwait(false) : 0;
            }

            public async Task ShowWalletAsync(System.IO.TextWriter output)
            {
                var wallet = await _online.WalletAsync().ConfigureAwait(false);
                output.WriteLine("wallet: " + CoopWalletText.Balance(CoopCurrencyRules.Friendship, wallet.Friendship) + ", " + CoopWalletText.Balance(CoopCurrencyRules.Seals, wallet.Seals));
                foreach (var entry in wallet.Ledger)
                    output.WriteLine("  " + CoopWalletText.Entry(entry.Currency, entry.Amount, entry.Source));
            }

            public async Task ShowRiftBoardAsync(System.IO.TextWriter output)
            {
                var board = await _online.Directory.RiftBoardAsync(Client.RiftId).ConfigureAwait(false);
                output.WriteLine("rift " + board.RiftId + ", ends " + DateTimeOffset.FromUnixTimeMilliseconds(board.EndUnixMs).ToString("u", CultureInfo.InvariantCulture) +
                    ", " + RiftBoardText.Own(board.OwnPlace, board.Parties, board.OwnDepth));
                foreach (var row in board.Top)
                    output.WriteLine("  " + RiftBoardText.Row(row.Place, row.Title, row.Depth, row.Yours));
            }

            public async Task ShowChronicleAsync(System.IO.TextWriter output)
            {
                var chronicle = await _online.Directory.ChronicleAsync().ConfigureAwait(false);
                output.WriteLine(PartyChronicleText.Banner(chronicle.Banner) + ", bosses together " + chronicle.BossesTogether + ", hours " +
                    chronicle.HoursTogether.ToString("0.0", CultureInfo.InvariantCulture));
                foreach (var feat in chronicle.Feats)
                    output.WriteLine("  " + PartyChronicleText.Feat(feat.Id) + ": " + (feat.Level > 0 ? PartyChronicleText.Earned(feat.AtUnixMs, 0) + ", " : string.Empty) +
                        PartyChronicleText.Goal(feat.Kind, feat.Tracked, feat.Level, feat.Levels, feat.Progress));
                foreach (var record in chronicle.Records)
                    output.WriteLine("  " + PartyChronicleText.Record(record.Kind, record.AtUnixMs, record.Name, record.Feat, 0));
            }

            public async Task ReportOtherAsync(string reason, System.IO.TextWriter output)
            {
                for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
                {
                    if (slot == Client.OwnSlot || Client.Roster.Player(slot) == 0)
                        continue;
                    var reported = await _online.ReportAsync(slot, reason).ConfigureAwait(false);
                    var blocked = await _online.BlockAsync(slot).ConfigureAwait(false);
                    output.WriteLine("slot " + slot + ": reported " + reported + ", blocked " + blocked);
                    return;
                }
                output.WriteLine("nobody else in the roster to report");
            }

            public Task ConnectAsync()
            {
                if (!_online.Linked)
                    throw new NakamaException(0, "the bot is not in a party");
                _matchGeneration = _online.MatchGeneration;
                Client.Join();
                return Task.CompletedTask;
            }

            public void Tick()
            {
                if (_online.MatchGeneration != _matchGeneration)
                {
                    _matchGeneration = _online.MatchGeneration;
                    Client.Rejoin();
                }
                for (var slot = 0; slot < _loop.State.Skills.Count; slot++)
                    _loop.RequestCast(slot);
                _loop.Tick();
                if (_ticks % ReactionTicks == ReactionTicks / 2 && Client.SendReaction(ReactionsSent % Client.Reactions.Ids.Count))
                    ReactionsSent++;
                if (WantsRift && _ticks % RiftTicks == 0 && Client.World == PartyWorld.Home && !Client.WorldSwitching)
                    Client.ProposeWorld(PartyWorld.Rift, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                for (var slot = 0; slot < _heard.Length; slot++)
                {
                    AllyDamage += Client.TakeAllyDamage(slot);
                    var serial = Client.ReactionSerial(slot);
                    if (serial != _heard[slot] && slot != Client.OwnSlot)
                        ReactionsHeard++;
                    _heard[slot] = serial;
                }
                Kills += _loop.Events.CountOf(GameEventKind.EnemyKilled);
                _loop.Events.Clear();
                if (++_ticks % KeepAliveTicks == 0 && _keepAlive.IsCompleted)
                    _keepAlive = Task.Run(() => _online.KeepAliveAsync(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            }

            public void Report(System.IO.TextWriter output)
            {
                var own = Client.OwnSlot;
                output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{0} {1}: link {2}, slot {3}, paid kills {4}, unpaid {5}, share {6:P0}, stage {7}, enemy {8}, reconnects {9}, reactions sent {10}, heard {11}, ally damage {12}, boss kills {13}, last boss loop {14:P0}, timer saved {15:0.0}s, world {16}",
                    HeroClassNames.Of(_loop.State.Class), _online.PlayerNumber, Client.Status, own, Client.PartyRewards, Client.UnpaidKills,
                    own >= 0 ? Client.Contribution(own) : 0, _loop.State.Stage, Client.EnemyId, _online.Reconnects, ReactionsSent, ReactionsHeard,
                    NumberFormatter.Format(AllyDamage, NumberFormatSettings.Default), Client.BossKillsSeen, Client.LastBossKill.Loop, Client.LastBossKill.SavedSeconds, Client.World));
            }

            public async Task FinishAsync(bool stay)
            {
                await _keepAlive.ConfigureAwait(false);
                if (!stay)
                    await _online.LeaveAsync().ConfigureAwait(false);
                _online.Dispose();
            }
        }
    }
}
