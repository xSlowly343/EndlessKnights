const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const party = loadModule();
const rules = party.PartyDirectoryRules;

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(party[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  const fails = (user, rpc, request, message) => assert.throws(() => call(user, rpc, request), (error) => error.message === message, rpc + " should fail with " + message);
  return { nk, call, fails };
}

test("a party gets a six letter code without look-alike letters and its creator leads it", () => {
  const { call } = world("ann");
  const view = call("ann", "partyCreateRpc", { title: "night_shift", language: "ru" });
  assert.match(view.code, new RegExp("^[" + party.PartyCodeAlphabet + "]{" + rules.codeLength + "}$"));
  assert.strictEqual(view.leader, "ann");
  assert.strictEqual(view.access, "invite");
  assert.deepStrictEqual(view.members.map((m) => m.userId), ["ann"]);
  assert.strictEqual(view.members[0].steamId, "76561198000000000");
  assert.strictEqual(call("ann", "partyMineRpc").party.id, view.id);
});

test("friends join by code, written in any case and with a dash", () => {
  const { call, fails } = world("ann", "bob", "cid", "dan", "eve");
  const view = call("ann", "partyCreateRpc", {});
  const typed = view.code.slice(0, 3).toLowerCase() + "-" + view.code.slice(3);
  for (const friend of ["bob", "cid", "dan"])
    call(friend, "partyJoinRpc", { code: typed });
  assert.strictEqual(call("ann", "partyMineRpc").party.members.length, 4);
  fails("eve", "partyJoinRpc", { code: view.code }, "party is full");
  fails("eve", "partyJoinRpc", { code: "ZZZZZZ" }, "unknown code");
});

test("one party at a time", () => {
  const { call, fails } = world("ann", "bob");
  const first = call("ann", "partyCreateRpc", {});
  call("bob", "partyCreateRpc", {});
  fails("bob", "partyJoinRpc", { code: first.code }, "already in a party");
  fails("ann", "partyCreateRpc", {}, "already in a party");
});

test("the leader updates the card, promotes and kicks; others cannot", () => {
  const { nk, call, fails } = world("ann", "bob", "cid");
  const view = call("ann", "partyCreateRpc", {});
  call("bob", "partyJoinRpc", { code: view.code });
  call("cid", "partyJoinRpc", { code: view.code });
  fails("bob", "partyUpdateRpc", { access: "open" }, "only the leader can do that");
  fails("ann", "partyUpdateRpc", { wanted: "healer" }, "bad request");
  assert.strictEqual(call("ann", "partyUpdateRpc", { access: "open", wanted: "buffer", title: "lunch_raid" }).wanted, "buffer");
  assert.strictEqual(nk.groupsGetId([view.id])[0].open, true);
  assert.strictEqual(call("ann", "partyPromoteRpc", { userId: "bob" }).leader, "bob");
  fails("ann", "partyKickRpc", { userId: "cid" }, "only the leader can do that");
  const after = call("bob", "partyKickRpc", { userId: "cid" });
  assert.deepStrictEqual(after.members.map((m) => m.userId), ["ann", "bob"]);
  assert.strictEqual(call("cid", "partyMineRpc").party, null);
});

test("when the leader leaves the next member leads, and the last one closes the party and frees the code", () => {
  const { nk, call } = world("ann", "bob");
  const view = call("ann", "partyCreateRpc", {});
  call("bob", "partyJoinRpc", { code: view.code });
  const match = call("ann", "partyConnectRpc").matchId;
  call("ann", "partyLeaveRpc");
  assert.strictEqual(call("bob", "partyMineRpc").party.leader, "bob");
  assert.deepStrictEqual(nk.signals, [{ id: match, data: "leave:ann" }]);
  call("bob", "partyLeaveRpc");
  assert.strictEqual(nk.groupsGetId([view.id]).length, 0);
  assert.strictEqual(nk.storageRead([{ collection: "party_codes", key: view.code, userId: party.PartySystemUser }]).length, 0);
});

test("every member connects to the same match while it lives", () => {
  const { nk, call } = world("ann", "bob");
  const view = call("ann", "partyCreateRpc", {});
  call("bob", "partyJoinRpc", { code: view.code });
  const first = call("ann", "partyConnectRpc").matchId;
  assert.strictEqual(call("bob", "partyConnectRpc").matchId, first);
  assert.deepStrictEqual(nk.matches.get(first).params, { partyId: view.id });
  nk.matches.delete(first);
  const second = call("bob", "partyConnectRpc").matchId;
  assert.notStrictEqual(second, first);
  assert.strictEqual(call("ann", "partyMineRpc").party.matchId, second);
});

test("only open parties with a free slot are listed, those fighting now and wanting your role first", () => {
  const { nk, call } = world("ann", "bob", "cid", "dan", "eve", "fay", "gus");
  const closed = call("ann", "partyCreateRpc", { language: "ru" });
  const plain = call("bob", "partyCreateRpc", { access: "open", language: "ru", wanted: "any" });
  const wantsBuffer = call("cid", "partyCreateRpc", { access: "open", language: "ru", wanted: "buffer" });
  const fighting = call("dan", "partyCreateRpc", { access: "open", language: "ru" });
  const english = call("eve", "partyCreateRpc", { access: "open", language: "en" });
  nk.matches.get(call("dan", "partyConnectRpc").matchId).size = 1;
  const listed = call("gus", "partyListRpc", { language: "ru", role: "buffer" }).parties;
  assert.deepStrictEqual(listed.map((card) => card.id), [fighting.id, wantsBuffer.id, plain.id]);
  assert.strictEqual(listed[0].activeNow, true);
  assert.strictEqual(listed[1].wantsYou, true);
  assert.ok(listed.every((card) => card.id !== closed.id && card.id !== english.id));
  assert.strictEqual("code" in listed[0], false, "codes are not shown in the search");
  assert.deepStrictEqual(call("gus", "partyListRpc", { wanted: "buffer" }).parties.map((card) => card.id), [wantsBuffer.id]);
});

test("an open party is joined by id from the search, a closed one is not", () => {
  const { call, fails } = world("ann", "bob", "cid");
  const open = call("ann", "partyCreateRpc", { access: "open" });
  const closed = call("bob", "partyCreateRpc", {});
  assert.strictEqual(call("cid", "partyJoinRpc", { partyId: open.id }).members.length, 2);
  call("cid", "partyLeaveRpc");
  fails("cid", "partyJoinRpc", { partyId: closed.id }, "party not found");
});

test("blocked players do not meet through the search, in either direction", () => {
  const { call } = world("ann", "bob", "cid");
  const open = call("ann", "partyCreateRpc", { access: "open" });
  call("bob", "partyBlockRpc", { userId: "ann" });
  assert.deepStrictEqual(call("bob", "partyListRpc", {}).parties, []);
  call("ann", "partyBlockRpc", { userId: "cid" });
  assert.deepStrictEqual(call("cid", "partyListRpc", {}).parties, []);
  assert.deepStrictEqual(call("ann", "partyUnblockRpc", { userId: "cid" }).blocked, []);
  assert.strictEqual(call("cid", "partyListRpc", {}).parties[0].id, open.id);
});

test("quick match joins the best open party or opens a new one for the next seekers", () => {
  const { call } = world("ann", "bob", "cid", "dan");
  const first = call("ann", "partyQuickRpc", { language: "en", role: "damage" });
  assert.strictEqual(first.access, "open");
  assert.strictEqual(first.language, "en");
  const second = call("bob", "partyQuickRpc", { language: "en", role: "buffer" });
  assert.strictEqual(second.id, first.id);
  const russian = call("cid", "partyQuickRpc", { language: "ru", role: "buffer" });
  assert.notStrictEqual(russian.id, first.id);
  assert.strictEqual(call("bob", "partyQuickRpc", { language: "ru" }).id, first.id, "a player in a party stays there");
});

test("members of open parties who stay away too long drop out", () => {
  const { nk, call } = world("ann", "bob", "cid");
  const open = call("ann", "partyCreateRpc", { access: "open" });
  call("bob", "partyJoinRpc", { partyId: open.id });
  const old = Date.now() - (rules.inactiveDays + 1) * 86400000;
  nk.storageWrite([{ collection: "party", key: "current", userId: "bob", value: { partyId: open.id, seen: old } }]);
  const listed = call("cid", "partyListRpc", {}).parties;
  assert.strictEqual(listed[0].members, 1);
  assert.strictEqual(call("bob", "partyMineRpc").party, null);
});

test("the usual time is shown in the viewer's own time zone", () => {
  const { nk, call } = world("ann", "bob");
  const open = call("ann", "partyCreateRpc", { access: "open" });
  const group = nk.groups.get(open.id);
  group.metadata.hours = group.metadata.hours.map((_, hour) => (hour === 17 ? 5 : 0));
  assert.strictEqual(call("bob", "partyListRpc", { utcOffsetMinutes: 0 }).parties[0].usuallyOnline, "day");
  assert.strictEqual(call("bob", "partyListRpc", { utcOffsetMinutes: 180 }).parties[0].usuallyOnline, "evening");
  assert.strictEqual(call("bob", "partyListRpc", { utcOffsetMinutes: 180, slot: "morning" }).parties.length, 0);
});

test("reports are stored for the server owner and need a known reason", () => {
  const { nk, call, fails } = world("ann", "bob");
  call("ann", "partyReportRpc", { userId: "bob", reason: "cheating" });
  const stored = nk.storageRead([{ collection: "party_reports", key: "bob:ann", userId: party.PartySystemUser }]);
  assert.strictEqual(stored[0].value.reason, "cheating");
  fails("ann", "partyReportRpc", { userId: "bob", reason: "rude" }, "bad request");
});

test("client group calls cannot touch party groups and cannot list groups", () => {
  const { nk, call } = world("ann");
  const view = call("ann", "partyCreateRpc", {});
  const ctx = { userId: "ann", env: {} };
  assert.strictEqual(party.partyBeforeGroupChange(ctx, logger, nk, { groupId: view.id }), null);
  assert.deepStrictEqual(party.partyBeforeGroupChange(ctx, logger, nk, { groupId: "other" }), { groupId: "other" });
  assert.strictEqual(party.partyBeforeCreateGroup(ctx, logger, nk, { name: "party-x" }), null);
  assert.strictEqual(party.partyBeforeListGroups(ctx, logger, nk, {}), null);
});

test("only party members may enter the party match", () => {
  const { nk, call } = world("ann", "bob");
  const view = call("ann", "partyCreateRpc", {});
  const ctx = { matchId: "m", env: {} };
  let state = JSON.parse(JSON.stringify(party.partyMatchInit(ctx, logger, nk, { partyId: view.id }).state));
  const presence = (userId) => ({ userId: userId, sessionId: "s" + userId, username: userId, node: "n" });
  assert.strictEqual(party.partyMatchJoinAttempt(ctx, logger, nk, null, 0, state, presence("bob"), { playerNumber: "12" }).accept, false);
  const attempt = party.partyMatchJoinAttempt(ctx, logger, nk, null, 0, state, presence("ann"), { playerNumber: "11" });
  assert.strictEqual(attempt.accept, true);
  party.partyMatchJoin(ctx, logger, nk, { matchLabelUpdate() {} }, 0, JSON.parse(JSON.stringify(attempt.state)), [presence("ann")]);
  const seen = nk.storageRead([{ collection: "party", key: "current", userId: "ann" }])[0].value;
  assert.strictEqual(seen.partyId, view.id);
  assert.ok(nk.groups.get(view.id).metadata.hours.some((count) => count > 0));
});

test("members come from the group itself, so a lost metadata write cannot hide anyone", () => {
  const { nk, call } = world("ann", "bob", "cid");
  const view = call("ann", "partyCreateRpc", {});
  nk.groupUsersAdd(view.id, ["bob"]);
  assert.deepStrictEqual(call("ann", "partyMineRpc").party.members.map((m) => m.userId), ["ann", "bob"]);
  call("cid", "partyJoinRpc", { code: view.code });
  nk.groupUsersKick(view.id, ["cid"]);
  assert.strictEqual(call("cid", "partyMineRpc").party, null);
  assert.strictEqual(call("bob", "partyMineRpc").party.members.length, 2);
});

test("a group left without members is dissolved when it is next seen", () => {
  const { nk, call } = world("ann", "bob");
  const open = call("ann", "partyCreateRpc", { access: "open" });
  nk.members.set(open.id, []);
  assert.deepStrictEqual(call("bob", "partyListRpc", {}).parties, []);
  assert.strictEqual(nk.groups.has(open.id), false);
  assert.strictEqual(nk.storageRead([{ collection: "party_codes", key: open.code, userId: party.PartySystemUser }]).length, 0);
});
