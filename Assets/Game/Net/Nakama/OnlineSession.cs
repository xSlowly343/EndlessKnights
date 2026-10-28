using System;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Net.Nakama
{
    public sealed class OnlineSession : IDisposable
    {
        private const long RefreshBeforeSeconds = 300;

        private readonly NakamaClient _client;
        private readonly Func<CancellationToken, Task<NakamaSession>> _login;
        private readonly SemaphoreSlim _busy = new SemaphoreSlim(1, 1);
        private NakamaSession _session;
        private OnlineParty? _online;
        private string _matchId = string.Empty;
        private volatile int _matchGeneration;

        private OnlineSession(NakamaClient client, Func<CancellationToken, Task<NakamaSession>> login, NakamaSession session, ulong playerNumber)
        {
            _client = client;
            _login = login;
            _session = session;
            Transport = new ReconnectingTransport(playerNumber);
        }

        public ReconnectingTransport Transport { get; }
        public ulong PlayerNumber => Transport.LocalId;
        public PartyView? Party { get; private set; }
        public bool InParty => Party != null;
        public bool Linked => _online != null && _online.Connected;
        public string UserId => _session.UserId;
        public int Reconnects { get; private set; }
        public int MatchGeneration => _matchGeneration;

        public static async Task<OnlineSession> StartAsync(NakamaClient client, Func<CancellationToken, Task<NakamaSession>> login, ulong playerNumber,
            CancellationToken cancel = default)
        {
            var session = new OnlineSession(client, login, await login(cancel).ConfigureAwait(false), playerNumber);
            await session.Locked(async () =>
            {
                session.Party = await session.Directory.MineAsync(cancel).ConfigureAwait(false);
                if (session.Party != null)
                    await session.LinkAsync(cancel).ConfigureAwait(false);
            }).ConfigureAwait(false);
            return session;
        }

        public PartyDirectory Directory => new PartyDirectory(_client, _session);

        public Task<PartyView> CreateAsync(string access, string? language, CancellationToken cancel = default) =>
            Enter(directory => directory.CreateAsync(access, null, language, null, cancel), cancel);

        public Task<PartyView> JoinByCodeAsync(string code, CancellationToken cancel = default) =>
            Enter(directory => directory.JoinByCodeAsync(code, cancel), cancel);

        public Task<PartyView> JoinOpenAsync(string partyId, CancellationToken cancel = default) =>
            Enter(directory => directory.JoinOpenAsync(partyId, cancel), cancel);

        public Task<PartyView> QuickAsync(string language, string role, int utcOffsetMinutes, CancellationToken cancel = default) =>
            Enter(directory => directory.QuickAsync(language, role, utcOffsetMinutes, cancel), cancel);

        public Task LeaveAsync(CancellationToken cancel = default) => Locked(async () =>
        {
            Unlink();
            if (Party != null)
                await Directory.LeaveAsync(cancel).ConfigureAwait(false);
            Party = null;
        });

        public Task RefreshPartyAsync(CancellationToken cancel = default) => Locked(async () =>
        {
            Party = await Directory.MineAsync(cancel).ConfigureAwait(false);
            if (Party == null)
                Unlink();
        });

        public Task KeepAliveAsync(long nowUnix, CancellationToken cancel = default) => Locked(async () =>
        {
            if (_session.ExpiresWithin(nowUnix, RefreshBeforeSeconds))
                _session = await Renew(cancel).ConfigureAwait(false);
            if (Party != null && !Linked)
            {
                Reconnects++;
                Party = await Directory.MineAsync(cancel).ConfigureAwait(false);
                if (Party != null)
                    await LinkAsync(cancel).ConfigureAwait(false);
                else
                    Unlink();
            }
        });

        public void Dispose()
        {
            Unlink();
            _busy.Dispose();
        }

        private async Task<PartyView> Enter(Func<PartyDirectory, Task<PartyView>> enter, CancellationToken cancel)
        {
            PartyView? entered = null;
            await Locked(async () =>
            {
                entered = await enter(Directory).ConfigureAwait(false);
                Party = entered;
                await LinkAsync(cancel).ConfigureAwait(false);
            }).ConfigureAwait(false);
            return entered!;
        }

        private async Task LinkAsync(CancellationToken cancel)
        {
            Unlink();
            var online = await OnlineParty.ConnectAsync(_client, _session, PlayerNumber, cancel).ConfigureAwait(false);
            if (online == null)
            {
                Party = null;
                return;
            }
            _online = online;
            Party = online.Party;
            if (online.Transport.MatchId != _matchId)
            {
                _matchId = online.Transport.MatchId;
                _matchGeneration++;
            }
            Transport.Use(online.Transport);
        }

        private void Unlink()
        {
            Transport.Use(null);
            _online?.Dispose();
            _online = null;
        }

        private async Task<NakamaSession> Renew(CancellationToken cancel)
        {
            try
            {
                return await _client.RefreshAsync(_session, cancel).ConfigureAwait(false);
            }
            catch (NakamaException)
            {
                return await _login(cancel).ConfigureAwait(false);
            }
        }

        private async Task Locked(Func<Task> work)
        {
            await _busy.WaitAsync().ConfigureAwait(false);
            try
            {
                await work().ConfigureAwait(false);
            }
            finally
            {
                _busy.Release();
            }
        }
    }
}
