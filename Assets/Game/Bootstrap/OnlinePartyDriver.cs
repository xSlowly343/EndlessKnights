using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Game.Core.Content;
using Game.Core.Party;
using Game.Core.Settings;
using Game.Core.Sim;
using Game.Core.Ui;
using Game.Net.Nakama;
using Game.Presentation.Ui;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class OnlinePartyDriver : MonoBehaviour
    {
        private const float KeepAliveSeconds = 10f;

        private readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
        private GameLoop _loop = null!;
        private GameData _data = null!;
        private PanelCanvas _panels = null!;
        private GameSettings _settings = null!;
        private NakamaClient? _client;
        private OnlineIdentity? _identity;
        private OnlineSession? _session;
        private PartyClient? _party;
        private Task _keepAlive = Task.CompletedTask;
        private float _sinceKeepAlive;
        private int _matchGeneration;
        private bool _busy;

        public static OnlinePartyDriver Attach(GameObject host, GameLoop loop, GameData data, PanelCanvas panels, GameSettings settings)
        {
            var driver = host.AddComponent<OnlinePartyDriver>();
            driver._loop = loop;
            driver._data = data;
            driver._panels = panels;
            driver._settings = settings;
            panels.Party.ActionRequested += driver.OnAction;
            var config = NetworkFile.Load();
            if (!config.HasParty)
                return driver;
            driver._client = new NakamaClient(config.ServerUrl, config.ServerKey);
            driver._identity = OnlineIdentity.LoadOrCreate(SaveDriver.SaveDirectory);
            driver.Run(driver.StartSession, silent: true);
            return driver;
        }

        private async Task StartSession()
        {
            var client = _client!;
            var identity = _identity!;
            var session = await OnlineSession.StartAsync(client, cancel => client.AuthenticateDeviceAsync(identity.DeviceId, cancel), identity.PlayerNumber).ConfigureAwait(false);
            _mainThread.Enqueue(() => Bind(session));
        }

        private void Bind(OnlineSession session)
        {
            _session = session;
            _party = new PartyClient(_loop, session.Transport, NakamaPartyTransport.HostId, _data.Party);
            _panels.Party.UseClient(_party);
            _matchGeneration = session.MatchGeneration;
            if (session.Linked)
                _party.Join();
            ShowCode();
        }

        private void Update()
        {
            while (_mainThread.TryDequeue(out var action))
                action();
            if (_client == null)
                return;
            _sinceKeepAlive += Time.unscaledDeltaTime;
            if (_session == null || _party == null)
            {
                RetryStart();
                return;
            }
            if (_session.MatchGeneration != _matchGeneration)
            {
                _matchGeneration = _session.MatchGeneration;
                _party.Rejoin();
                ShowCode();
            }
            if (_sinceKeepAlive < KeepAliveSeconds || !_keepAlive.IsCompleted)
                return;
            _sinceKeepAlive = 0f;
            var session = _session;
            _keepAlive = Task.Run(async () =>
            {
                try
                {
                    await session.KeepAliveAsync(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
                }
                catch (NakamaException error)
                {
                    Debug.LogWarning("Party: " + error.Message);
                }
            });
        }

        private void RetryStart()
        {
            if (_busy || _sinceKeepAlive < KeepAliveSeconds)
                return;
            _sinceKeepAlive = 0f;
            Run(StartSession, silent: true);
        }

        private void OnAction(PartyAction action)
        {
            if (_client == null)
            {
                _panels.ShowNotice(action == PartyAction.Links ? PartyRoster.NotYet : PartyActions.Offline);
                return;
            }
            if (_session == null || _busy)
            {
                _panels.ShowNotice(_busy ? PartyActions.Working : PartyActions.Offline);
                return;
            }
            switch (action)
            {
                case PartyAction.Create:
                    Enter(() => _session.CreateAsync("invite", Language()));
                    break;
                case PartyAction.JoinByCode:
                    if (!PartyActions.TryReadCode(GUIUtility.systemCopyBuffer, _data.Party.Search.CodeLength, out var code))
                    {
                        _panels.ShowNotice(PartyActions.NoCode);
                        return;
                    }
                    Enter(() => _session.JoinByCodeAsync(code));
                    break;
                case PartyAction.QuickMatch:
                    Enter(() => _session.QuickAsync(Language(), _data.Party.Search.RoleOf(_loop.State.Class), UtcOffsetMinutes()));
                    break;
                case PartyAction.CopyCode:
                    if (_session.Party != null)
                    {
                        GUIUtility.systemCopyBuffer = PartySearchRules.FormatCode(_session.Party.Code);
                        _panels.ShowNotice(PartyActions.CodeCopied(_session.Party.Code));
                    }
                    break;
                case PartyAction.Leave:
                    var leaving = _session;
                    Run(async () =>
                    {
                        await leaving.LeaveAsync().ConfigureAwait(false);
                        _mainThread.Enqueue(() =>
                        {
                            _party?.Leave();
                            ShowCode();
                            _panels.ShowNotice(PartyActions.LeftParty);
                        });
                    });
                    break;
                default:
                    _panels.ShowNotice(PartyRoster.NotYet);
                    break;
            }
        }

        private void Enter(Func<Task<PartyView>> enter)
        {
            Run(async () =>
            {
                var party = await enter().ConfigureAwait(false);
                _mainThread.Enqueue(() =>
                {
                    _matchGeneration = _session!.MatchGeneration;
                    _party?.Rejoin();
                    ShowCode();
                    _panels.ShowNotice(PartyActions.Joined(party.Members.Count, party.MaxMembers));
                });
            });
        }

        private void Run(Func<Task> work, bool silent = false)
        {
            _busy = true;
            if (!silent)
                _panels.ShowNotice(PartyActions.Working);
            Task.Run(async () =>
            {
                try
                {
                    await work().ConfigureAwait(false);
                }
                catch (NakamaException error)
                {
                    Debug.LogWarning("Party: " + error.Message);
                    if (!silent)
                        _mainThread.Enqueue(() => _panels.ShowNotice(PartyActions.Failed(error.Message)));
                }
                finally
                {
                    _mainThread.Enqueue(() => _busy = false);
                }
            });
        }

        private void ShowCode() => _panels.Party.UseCode(_session?.Party?.Code ?? string.Empty);

        private string Language()
        {
            var languages = _data.Party.Search.Languages;
            for (var i = 0; i < languages.Count; i++)
            {
                if (languages[i] == _settings.Language)
                    return languages[i];
            }
            return languages[0];
        }

        private static int UtcOffsetMinutes() => (int)Math.Round(TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalMinutes);

        private void OnApplicationQuit() => _session?.Dispose();
    }
}
