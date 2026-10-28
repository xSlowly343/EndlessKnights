using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Core.Content;
using Game.Core.Party;
using Game.Core.Settings;
using Game.Core.Sim;
using Game.Core.Ui;
using Game.Net.Nakama;
using Game.Presentation.Battle;
using Game.Presentation.Ui;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class OnlinePartyDriver : MonoBehaviour
    {
        private const float KeepAliveSeconds = 10f;
        private const float SearchRefreshSeconds = 30f;
        private const float AlliesRefreshSeconds = 0.1f;

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
        private PartySearchFilters _filters = null!;
        private bool _searchShown;
        private bool _searching;
        private float _sinceSearch;
        private NotificationDriver? _notifications;
        private readonly int[] _heardReactions = new int[PartyRules.MaxSlots];
        private readonly StripAlly[] _allies = new StripAlly[StripAllies.MaxShown];
        private readonly EnemyBadge[] _badges = new EnemyBadge[Game.Core.Party.Wire.ShownEffect.MaxShown];
        private readonly BossShare[] _bossShares = new BossShare[PartyRules.MaxSlots];
        private int _bossKillsSeen;
        private BattleView? _battle;
        private float _sinceAllies;

        public static OnlinePartyDriver Attach(GameObject host, GameLoop loop, GameData data, PanelCanvas panels, GameSettings settings, NotificationDriver? notifications,
            BattleView? battle)
        {
            var driver = host.AddComponent<OnlinePartyDriver>();
            driver._notifications = notifications;
            driver._battle = battle;
            driver._loop = loop;
            driver._data = data;
            driver._panels = panels;
            driver._settings = settings;
            panels.Party.ActionRequested += driver.OnAction;
            panels.Party.MemberActionRequested += driver.OnMemberAction;
            panels.Party.UseMemberOptions(PartyMemberMenu.Options(data.Party.Search));
            var reactions = new List<string>();
            foreach (var id in data.Party.Reactions.Ids)
                reactions.Add(PartyReactions.Label(id));
            panels.Party.UseReactions(reactions);
            panels.Party.ReactionRequested += driver.React;
            panels.Party.RiftRequested += driver.MoveWorld;
            driver._filters = new PartySearchFilters(data.Party.Search, settings.Language);
            panels.PartySearch.UseFilters(driver._filters);
            panels.PartySearch.QuickRequested += () => driver.OnAction(PartyAction.QuickMatch);
            panels.PartySearch.RefreshRequested += driver.Search;
            panels.PartySearch.JoinRequested += driver.JoinOpen;
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
            WatchSearch();
            if (_client == null)
                return;
            _sinceKeepAlive += Time.unscaledDeltaTime;
            if (_session == null || _party == null)
            {
                RetryStart();
                return;
            }
            WatchReactions();
            ShowAllies();
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

        private void React(int reaction)
        {
            if (_party == null || !_party.InParty)
            {
                _panels.ShowNotice(PartyActions.Failed("not in a party"));
                return;
            }
            if (!_party.SendReaction(reaction))
                _panels.ShowNotice(PartyReactions.Wait(_data.Party.Reactions));
        }

        private void ShowAllies()
        {
            if (_battle == null)
                return;
            _sinceAllies += Time.unscaledDeltaTime;
            if (_sinceAllies < AlliesRefreshSeconds)
                return;
            _sinceAllies = 0f;
            var party = _party!;
            var count = StripAllies.Fill(party, _allies);
            _battle.Allies.Show(_allies, count);
            for (var i = 0; i < count; i++)
            {
                var damage = party.TakeAllyDamage(_allies[i].Slot);
                if (damage.Sign > 0)
                    _battle.ShowAllyHit(damage, PartyColors.Of(_allies[i].ColorIndex));
            }
            _battle.PartyMarks.ShowBadges(_badges, PartyStripMarks.FillBadges(party, _data.Gems, _badges));
            if (party.BossKillsSeen == _bossKillsSeen)
                return;
            _bossKillsSeen = party.BossKillsSeen;
            if (!party.InParty)
                return;
            var kill = party.LastBossKill;
            var shares = PartyStripMarks.FillBossShares(kill, party.Roster, party.OwnSlot, _bossShares);
            _battle.PartyMarks.ShowBossShares(PartyStripMarks.BossTitle(kill, party.OwnSlot), _bossShares, shares);
        }

        private void MoveWorld(PartyRiftAction action)
        {
            PartyRiftButton.Perform(action, _party, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            _panels.Party.Refresh(_loop);
        }

        private void WatchReactions()
        {
            var party = _party!;
            if (!party.InParty)
                return;
            for (var slot = 0; slot < _heardReactions.Length; slot++)
            {
                var serial = party.ReactionSerial(slot);
                if (serial == _heardReactions[slot])
                    continue;
                _heardReactions[slot] = serial;
                var reaction = party.ReactionOf(slot);
                if (slot == party.OwnSlot || reaction == PartyClient.NoReaction)
                    continue;
                Announce(slot, PartyReactions.Label(party.Reactions, reaction));
            }
        }

        private void Announce(int slot, string reaction)
        {
            foreach (var row in PartyRoster.From(_loop.State, _party!))
            {
                if (row.Slot != slot || row.IsFree)
                    continue;
                var text = PartyReactions.Said(row.Name, reaction);
                if (_notifications != null)
                    _notifications.Notify(text);
                else
                    _panels.ShowNotice(text);
                return;
            }
        }

        private void WatchSearch()
        {
            var shown = _panels.PartySearch.IsShown;
            var opened = shown && !_searchShown;
            _searchShown = shown;
            if (!shown)
                return;
            _sinceSearch += Time.unscaledDeltaTime;
            if (opened || _sinceSearch >= SearchRefreshSeconds)
                Search();
        }

        private void Search()
        {
            _sinceSearch = 0f;
            if (_client == null)
            {
                _panels.PartySearch.ShowStatus(PartySearchTexts.Offline);
                return;
            }
            if (_session == null)
            {
                _panels.PartySearch.ShowStatus(PartyActions.Failed(string.Empty));
                return;
            }
            if (_session.InParty)
            {
                _panels.PartySearch.ShowStatus(PartySearchTexts.InParty);
                return;
            }
            if (_searching)
                return;
            _searching = true;
            _panels.PartySearch.ShowStatus(PartySearchTexts.Loading);
            var session = _session;
            var language = _filters.Language;
            var wanted = _filters.Wanted;
            var slot = _filters.Slot;
            var role = _data.Party.Search.RoleOf(_loop.State.Class);
            var offset = UtcOffsetMinutes();
            Task.Run(async () =>
            {
                try
                {
                    var cards = await session.ListAsync(language, role, wanted, slot, offset).ConfigureAwait(false);
                    var rows = new List<PartySearchRow>(cards.Count);
                    foreach (var card in cards)
                        rows.Add(PartySearchRow.From(card.Id, card.Title, card.Language, card.Wanted, card.Members, card.MaxMembers, card.ActiveNow, card.UsuallyOnline, card.WantsYou));
                    _mainThread.Enqueue(() => _panels.PartySearch.ShowRows(rows));
                }
                catch (NakamaException error)
                {
                    Debug.LogWarning("Party search: " + error.Message);
                    _mainThread.Enqueue(() => _panels.PartySearch.ShowStatus(PartyActions.Failed(error.Message)));
                }
                finally
                {
                    _mainThread.Enqueue(() => _searching = false);
                }
            });
        }

        private void RefreshSearch()
        {
            if (_panels.PartySearch.IsShown)
                Search();
        }

        private void JoinOpen(string partyId)
        {
            if (_session == null || _busy)
            {
                _panels.ShowNotice(_busy ? PartyActions.Working : PartyActions.Offline);
                return;
            }
            var session = _session;
            Enter(() => session.JoinOpenAsync(partyId));
        }

        private void OnMemberAction(int slot, PartyMemberOption option)
        {
            if (_session == null || _busy)
            {
                _panels.ShowNotice(_busy ? PartyActions.Working : PartyActions.Offline);
                return;
            }
            var session = _session;
            Run(async () =>
            {
                var done = option.IsBlock
                    ? await session.BlockAsync(slot).ConfigureAwait(false)
                    : await session.ReportAsync(slot, option.ReportReason!).ConfigureAwait(false);
                var notice = !done ? PartyMemberMenu.NotFound : option.IsBlock ? PartyMemberMenu.Blocked : PartyMemberMenu.Reported;
                _mainThread.Enqueue(() => _panels.ShowNotice(notice));
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
                            RefreshSearch();
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
                    RefreshSearch();
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
