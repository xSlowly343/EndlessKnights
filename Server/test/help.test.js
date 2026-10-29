const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const help = server.HelpServerRules;

function presence(userId) {
  return { userId: userId, sessionId: "session-" + userId, username: userId, node: "riftwarden1" };
}

class Dispatcher {
  constructor() { this.kicked = []; }
  broadcastMessage() {}
  matchLabelUpdate() {}
  matchKick(presences) { for (const p of presences) this.kicked.push(p.userId); }
}

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  const fails = (user, rpc, request, message) => assert.throws(() => call(user, rpc, request), (error) => error.message === message, rpc + " should fail with " + message);
  return { nk, call, fails };
}

function liveMatch(nk, call, leader) {
  const party = call(leader, "partyCreateRpc", {});
  const matchId = call(leader, "partyConnectRpc").matchId;
  const ctx = { matchId: matchId, env: {} };
  const dispatcher = new Dispatcher();
  const match = { state: server.partyMatchInit(ctx, logger, nk, { seed: "9", partyId: party.id }).state };
  match.enter = (userId, number) => {
    const attempt = server.partyMatchJoinAttempt(ctx, logger, nk, dispatcher, 0, match.state, presence(userId), { playerNumber: number });
    if (!attempt.accept)
      return attempt.rejectMessage;
    match.state = server.partyMatchJoin(ctx, logger, nk, dispatcher, 0, attempt.state, [presence(userId)]).state;
    return "ok";
  };
  match.loop = (ticks) => {
    for (let i = 0; i < ticks; i++)
      match.state = server.partyMatchLoop(ctx, logger, nk, dispatcher, i, match.state, []).state;
  };
  nk.onSignal = (id, data) => {
    const reply = server.partyMatchSignal(ctx, logger, nk, dispatcher, 0, match.state, data);
    match.state = reply.state;
    return reply.data;
  };
  match.toBoss = () => {
    const encounter = match.state.host.encounter;
    while (!encounter.fightingBoss) {
      encounter.reportInput(0);
      encounter.applyDamage(0, encounter.enemyId, encounter.barLeft + 1);
      match.state.host.tick(Date.now());
    }
    match.state.host.outbox.length = 0;
    encounter.events.length = 0;
  };
  return { party, matchId, match, dispatcher };
}

test("only friends are called by default, and a player can refuse every call", () => {
  const { nk, call, fails } = world("ann", "bob", "cid");
  liveMatch(nk, call, "ann");
  assert.strictEqual(call("cid", "helpSettingsRpc").accept, help.defaultAccept);
  fails("ann", "helpCallRpc", { userId: "cid" }, "this player does not take calls from you");
  const clan = call("ann", "clanCreateRpc", {});
  call("cid", "clanJoinRpc", { code: clan.code });
  assert.strictEqual(server.helpTakesCalls(nk, "ann", "cid", Date.now()), true, "a clan mate is a friend");
  assert.strictEqual(call("cid", "helpSettingsRpc", { accept: "none" }).accept, "none");
  assert.strictEqual(server.helpTakesCalls(nk, "ann", "cid", Date.now()), false);
  fails("cid", "helpSettingsRpc", { accept: "maybe" }, "bad request");
  assert.strictEqual(call("bob", "helpSettingsRpc", { accept: "all" }).accept, "all");
  assert.strictEqual(server.helpTakesCalls(nk, "ann", "bob", Date.now()), true);
  call("bob", "partyBlockRpc", { userId: "ann" });
  assert.strictEqual(server.helpTakesCalls(nk, "ann", "bob", Date.now()), false, "a blocked caller is refused");
  fails("cid", "helpCallRpc", { userId: "ann" }, "not in a party");
  fails("ann", "helpCallRpc", { userId: "ann" }, "bad request");
});

test("a called friend joins the boss fight as a guest for a while and gets friendship for the boss", () => {
  const { nk, call, fails } = world("ann", "bob", "cid", "dan");
  call("cid", "helpSettingsRpc", { accept: "all" });
  call("dan", "helpSettingsRpc", { accept: "all" });
  const { matchId, match, dispatcher } = liveMatch(nk, call, "ann");
  call("bob", "partyJoinRpc", { code: call("ann", "partyMineRpc").party.code });
  assert.strictEqual(match.enter("ann", "11"), "ok");
  assert.strictEqual(match.enter("bob", "12"), "ok");
  fails("ann", "helpCallRpc", { userId: "cid" }, "no boss to help with");
  fails("ann", "helpCallRpc", { userId: "bob" }, "already in your party");

  match.toBoss();
  const until = call("ann", "helpCallRpc", { userId: "cid" }).until;
  assert.ok(until > Date.now());
  assert.strictEqual(nk.notifications.length, 1);
  assert.strictEqual(nk.notifications[0].userId, "cid");
  assert.strictEqual(nk.notifications[0].content.from, "ann");
  fails("ann", "helpCallRpc", { userId: "dan" }, "wait before calling again");
  assert.deepStrictEqual(JSON.parse(JSON.stringify(call("cid", "helpInvitesRpc").invites.map((i) => i.from))), ["ann"]);
  assert.strictEqual(match.enter("dan", "14"), "not a member of this party", "only the called friend gets in");

  const answer = call("cid", "helpAnswerRpc", { from: "ann" });
  assert.strictEqual(answer.matchId, matchId);
  assert.strictEqual(answer.seconds, help.seconds);
  fails("cid", "helpAnswerRpc", { from: "ann" }, "the call is over");
  assert.strictEqual(match.enter("cid", "13"), "ok");
  const host = match.state.host;
  const slot = host.encounter.slotOf("cid");
  assert.strictEqual(host.encounter.members[slot].guest, true);
  assert.strictEqual(host.encounter.fighters, 2, "the guest does not thicken the boss");
  const visit = nk.storageRead([{ collection: "party", key: "current", userId: "cid" }]);
  assert.strictEqual(visit.length, 0, "the guest keeps their own party record");

  host.encounter.reportInput(slot);
  host.encounter.applyDamage(slot, host.encounter.enemyId, 0.2);
  host.encounter.reportInput(0);
  host.encounter.applyDamage(0, host.encounter.enemyId, host.encounter.barLeft + 1);
  match.loop(1);
  server.partyFlushChronicle(nk, logger, match.state);
  assert.strictEqual(call("cid", "coopWalletRpc").balances.friendship, 2, "the guest earns friendship for the boss");
  assert.strictEqual(call("ann", "coopWalletRpc").ledger.filter((e) => e.source === "help_call").length, 0);

  match.loop(help.seconds * 10);
  assert.deepStrictEqual(dispatcher.kicked, ["cid"], "the guest is sent home when the time is up");
  assert.strictEqual(match.state.host.encounter.slotOf("cid"), -1);
  assert.strictEqual(match.enter("cid", "13"), "not a member of this party");
});

test("a guest leaves when the party goes to the rift and calls are refused there", () => {
  const { nk, call } = world("ann", "cid");
  call("cid", "helpSettingsRpc", { accept: "all" });
  const { match } = liveMatch(nk, call, "ann");
  match.enter("ann", "11");
  match.toBoss();
  call("ann", "helpCallRpc", { userId: "cid" });
  call("cid", "helpAnswerRpc", { from: "ann" });
  assert.strictEqual(match.enter("cid", "13"), "ok");
  match.state.host.encounter.switchWorld(1, Math.floor(Date.now() / 1000));
  match.state.host.tick(Date.now());
  assert.deepStrictEqual(JSON.parse(JSON.stringify(match.state.host.guestsLeft)), ["cid"]);
  assert.strictEqual(server.partyInvite(match.state, "cid", Date.now() + 1000), "no boss to help with");
});
