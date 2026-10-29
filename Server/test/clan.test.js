const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.ClanServerRules;
const day = 86400000;

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  const fails = (user, rpc, request, message) => assert.throws(() => call(user, rpc, request), (error) => error.message === message, rpc + " should fail with " + message);
  const lastSeen = (user, daysAgo) => {
    const stored = nk.storageRead([{ collection: "clan", key: "current", userId: user }])[0];
    nk.storageWrite([{ collection: "clan", key: "current", userId: user, value: { clanId: stored.value.clanId, seen: Date.now() - daysAgo * day } }]);
  };
  return { nk, call, fails, lastSeen };
}

function crowd(count) {
  const names = [];
  for (let i = 0; i < count; i++)
    names.push("p" + i);
  return names;
}

test("rules come from clan.json: thirty members, names from two word lists", () => {
  assert.strictEqual(rules.maxMembers, 30);
  assert.ok(rules.adjectives.length > 0 && rules.nouns.length > 0);
  assert.deepStrictEqual(Array.from(rules.languages), Array.from(server.PartyDirectoryRules.languages));
});

test("a clan gets a name from the lists and a code, and its founder leads it", () => {
  const { call, fails } = world("ann");
  const view = call("ann", "clanCreateRpc", { adjective: "lilac", noun: "lanterns", language: "ru" });
  assert.match(view.code, new RegExp("^[" + server.PartyCodeAlphabet + "]{" + rules.codeLength + "}$"));
  assert.strictEqual(view.adjective, "lilac");
  assert.strictEqual(view.noun, "lanterns");
  assert.strictEqual(view.access, "invite");
  assert.strictEqual(view.leader, "ann");
  assert.strictEqual(view.maxMembers, 30);
  assert.deepStrictEqual(view.members.map((m) => [m.userId, m.role]), [["ann", "leader"]]);
  assert.strictEqual(view.activeNow, 1);
  assert.strictEqual(call("ann", "clanMineRpc").clan.id, view.id);
  fails("ann", "clanCreateRpc", {}, "already in a clan");
  fails("ann", "clanUpdateRpc", { noun: "pirates" }, "bad request");
});

test("a clan and a party live side by side", () => {
  const { call } = world("ann", "bob");
  const party = call("ann", "partyCreateRpc", {});
  const clan = call("ann", "clanCreateRpc", {});
  call("bob", "partyJoinRpc", { code: party.code });
  call("bob", "clanJoinRpc", { code: clan.code.toLowerCase() });
  assert.strictEqual(call("bob", "partyMineRpc").party.id, party.id);
  assert.strictEqual(call("bob", "clanMineRpc").clan.id, clan.id);
  call("bob", "clanLeaveRpc");
  assert.strictEqual(call("bob", "partyMineRpc").party.id, party.id);
  assert.strictEqual(call("bob", "clanMineRpc").clan, null);
});

test("thirty fit and the thirty-first is turned away", () => {
  const names = crowd(31);
  const { call, fails } = world(...names);
  const view = call("p0", "clanCreateRpc", {});
  for (let i = 1; i < 30; i++)
    call(names[i], "clanJoinRpc", { code: view.code });
  assert.strictEqual(call("p0", "clanMineRpc").clan.members.length, 30);
  fails("p30", "clanJoinRpc", { code: view.code }, "clan is full");
  fails("p30", "clanJoinRpc", { code: "ZZZZZZ" }, "unknown code");
});

test("the leader names officers up to the limit; officers kick members but not each other", () => {
  const names = crowd(rules.maxOfficers + 3);
  const { call, fails } = world(...names);
  const view = call("p0", "clanCreateRpc", {});
  for (let i = 1; i < names.length; i++)
    call(names[i], "clanJoinRpc", { code: view.code });
  for (let i = 1; i <= rules.maxOfficers; i++)
    call("p0", "clanPromoteRpc", { userId: names[i] });
  fails("p0", "clanPromoteRpc", { userId: names[rules.maxOfficers + 1] }, "too many officers");
  fails("p1", "clanPromoteRpc", { userId: names[rules.maxOfficers + 1] }, "only the leader can do that");
  fails("p1", "clanKickRpc", { userId: "p2" }, "only the leader can do that");
  fails("p1", "clanKickRpc", { userId: "p0" }, "only the leader can do that");
  const last = names[names.length - 1];
  const after = call("p1", "clanKickRpc", { userId: last });
  assert.ok(after.members.every((m) => m.userId !== last));
  assert.strictEqual(call(last, "clanMineRpc").clan, null);
  const roles = call("p0", "clanDemoteRpc", { userId: "p1" }).members.filter((m) => m.role === "officer").map((m) => m.userId);
  assert.strictEqual(roles.indexOf("p1"), -1);
  fails("p1", "clanKickRpc", { userId: names[rules.maxOfficers + 1] }, "only the leader can do that");
});

test("the leader hands the clan over and stays as an officer", () => {
  const { call, fails } = world("ann", "bob", "cid");
  const view = call("ann", "clanCreateRpc", {});
  call("bob", "clanJoinRpc", { code: view.code });
  call("cid", "clanJoinRpc", { code: view.code });
  const after = call("ann", "clanLeadRpc", { userId: "bob" });
  assert.strictEqual(after.leader, "bob");
  assert.deepStrictEqual(after.members.map((m) => [m.userId, m.role]).slice(0, 2), [["bob", "leader"], ["ann", "officer"]]);
  fails("ann", "clanUpdateRpc", { access: "open" }, "only the leader can do that");
  assert.strictEqual(call("bob", "clanUpdateRpc", { access: "open", adjective: "golden" }).adjective, "golden");
});

test("when the leader leaves an officer leads, and the last one out closes the clan and frees the code", () => {
  const { nk, call } = world("ann", "bob", "cid");
  const view = call("ann", "clanCreateRpc", {});
  call("bob", "clanJoinRpc", { code: view.code });
  call("cid", "clanJoinRpc", { code: view.code });
  call("ann", "clanPromoteRpc", { userId: "cid" });
  call("ann", "clanLeaveRpc");
  assert.strictEqual(call("bob", "clanMineRpc").clan.leader, "cid");
  call("bob", "clanLeaveRpc");
  call("cid", "clanLeaveRpc");
  assert.strictEqual(nk.groupsGetId([view.id]).length, 0);
  assert.strictEqual(nk.storageRead([{ collection: "clan_codes", key: view.code, userId: server.PartySystemUser }]).length, 0);
});

test("a leader away for two weeks passes the clan to the most recent officer", () => {
  const { call, lastSeen } = world("ann", "bob", "cid", "dan");
  const view = call("ann", "clanCreateRpc", {});
  for (const name of ["bob", "cid", "dan"])
    call(name, "clanJoinRpc", { code: view.code });
  call("ann", "clanPromoteRpc", { userId: "bob" });
  call("ann", "clanPromoteRpc", { userId: "cid" });
  lastSeen("ann", rules.leaderAwayDays + 1);
  lastSeen("bob", 3);
  lastSeen("cid", 1);
  const after = call("dan", "clanMineRpc").clan;
  assert.strictEqual(after.leader, "cid");
  assert.deepStrictEqual(after.members.filter((m) => m.role === "officer").map((m) => m.userId).sort(), ["ann", "bob"]);
});

test("open clans are listed by who is around, without codes, and quick join picks one", () => {
  const { call, lastSeen } = world("ann", "bob", "cid", "dan", "eve");
  const busy = call("ann", "clanCreateRpc", { access: "open", language: "ru" });
  call("bob", "clanJoinRpc", { clanId: busy.id });
  const quiet = call("cid", "clanCreateRpc", { access: "open", language: "ru" });
  lastSeen("cid", 1);
  call("dan", "clanCreateRpc", { access: "invite", language: "ru" });
  const list = call("eve", "clanListRpc", { language: "ru" }).clans;
  assert.deepStrictEqual(list.map((c) => c.id), [busy.id, quiet.id]);
  assert.strictEqual(list[0].activeNow, 2);
  assert.strictEqual(list[0].code, undefined);
  assert.strictEqual(call("eve", "clanListRpc", { language: "en" }).clans.length, 0);
  assert.strictEqual(call("eve", "clanQuickRpc", { language: "en" }).clan, null);
  assert.strictEqual(call("eve", "clanQuickRpc", { language: "ru" }).clan.id, busy.id);
  assert.strictEqual(call("eve", "clanQuickRpc", { language: "ru" }).clan.id, busy.id);
});

test("a closed clan cannot be joined by id, and blocked players do not see each other's clan", () => {
  const { call, fails } = world("ann", "bob", "cid");
  const closed = call("ann", "clanCreateRpc", { access: "invite" });
  fails("bob", "clanJoinRpc", { clanId: closed.id }, "clan not found");
  call("ann", "clanUpdateRpc", { access: "open" });
  call("ann", "partyBlockRpc", { userId: "cid" });
  assert.strictEqual(call("cid", "clanListRpc", {}).clans.length, 0);
  assert.strictEqual(call("bob", "clanListRpc", {}).clans.length, 1);
});

test("members gone for a month drop out of open clans", () => {
  const { call, lastSeen } = world("ann", "bob", "cid");
  const view = call("ann", "clanCreateRpc", { access: "open" });
  call("bob", "clanJoinRpc", { clanId: view.id });
  lastSeen("bob", rules.inactiveDays + 1);
  assert.strictEqual(call("cid", "clanListRpc", {}).clans[0].members, 1);
  assert.strictEqual(call("bob", "clanMineRpc").clan, null);
});

test("clients cannot create or touch clan groups directly", () => {
  const { nk, call } = world("ann");
  const clan = call("ann", "clanCreateRpc", {});
  assert.strictEqual(server.partyBeforeCreateGroup({}, logger, nk, { name: "clan-x" }), null);
  assert.strictEqual(server.partyBeforeGroupChange({ userId: "ann", env: {} }, logger, nk, { groupId: clan.id }), null);
});
