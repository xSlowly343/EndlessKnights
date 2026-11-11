const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.SocialServerRules;
const day = 86400000;

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  const fails = (user, rpc, request, message) => assert.throws(() => call(user, rpc, request), (error) => error.message === message, rpc + " should fail with " + message);
  return { nk, call, fails };
}

function at(time, run) {
  const real = Date.now;
  Date.now = () => time;
  try {
    return run();
  } finally {
    Date.now = real;
  }
}

function crowd(count) {
  const names = [];
  for (let i = 0; i < count; i++)
    names.push("p" + i);
  return names;
}

test("rules come from social.json", () => {
  assert.strictEqual(rules.nameMin, 3);
  assert.strictEqual(rules.nameMax, 20);
  assert.deepStrictEqual(Array.from(rules.channels.map((channel) => channel.id)), ["world", "party", "clan"]);
  assert.ok(rules.banned.length > 0);
});

test("a leader gives the party its own name, and the search card shows it", () => {
  const { call, fails } = world("ann", "bob");
  const party = call("ann", "partyCreateRpc", { access: "open", title: "night_shift", name: "  Ночные   совы " });
  assert.strictEqual(party.name, "Ночные совы");
  assert.strictEqual(party.title, "night_shift");
  assert.strictEqual(party.renameFree, false);
  assert.strictEqual(call("bob", "partyListRpc", {}).parties[0].title, "=Ночные совы");
  call("bob", "partyJoinRpc", { code: party.code });
  fails("bob", "partyUpdateRpc", { name: "Bob rules" }, "only the leader can do that");
});

test("a name is checked for length, symbols and banned words", () => {
  const { call, fails } = world("ann");
  call("ann", "partyCreateRpc", {});
  fails("ann", "partyUpdateRpc", { name: "ab" }, "bad name");
  fails("ann", "partyUpdateRpc", { name: "a".repeat(rules.nameMax + 1) }, "bad name");
  fails("ann", "partyUpdateRpc", { name: "<b>owls</b>" }, "bad name");
  fails("ann", "partyUpdateRpc", { name: "owls!!!" }, "bad name");
  fails("ann", "partyUpdateRpc", { name: 12 }, "bad request");
  fails("ann", "partyUpdateRpc", { name: "Fuck owls" }, "name not allowed");
  fails("ann", "partyUpdateRpc", { name: "F u c k owls" }, "name not allowed");
  fails("ann", "partyUpdateRpc", { name: "Fuсk0ff sh1t" }, "name not allowed");
  fails("ann", "partyUpdateRpc", { name: "Xyй вам" }, "name not allowed");
  assert.strictEqual(call("ann", "partyUpdateRpc", { name: "O'Neil-7 owls" }).name, "O'Neil-7 owls");
});

test("the first name is free, every next one takes a ticket bought for crystals", () => {
  const { nk, call, fails } = world("ann", "bob");
  const now = Date.now();
  const created = call("ann", "partyCreateRpc", { access: "open", title: "late_owls" });
  assert.strictEqual(created.renameFree, true);
  assert.strictEqual(call("ann", "partyUpdateRpc", { name: "First name" }).renameFree, false);
  fails("ann", "partyUpdateRpc", { name: "Second name" }, "no ticket");
  assert.strictEqual(call("ann", "partyMineRpc").party.name, "First name");
  assert.strictEqual(call("ann", "partyUpdateRpc", { name: "First name", wanted: "any" }).name, "First name");
  fails("ann", "ticketBuyRpc", { id: "party_rename" }, "not enough crystals");
  fails("ann", "ticketBuyRpc", { id: "gold_rename" }, "bad request");
  const price = rules.tickets.filter((t) => t.id === "party_rename")[0].price;
  server.walletGrant(nk, "ann", "crystal_purchase", price * 2, now);
  const bought = call("ann", "ticketBuyRpc", { id: "party_rename" });
  assert.deepStrictEqual(bought.tickets.map((t) => [t.id, t.owned]), [["party_rename", 1], ["clan_rename", 0]]);
  assert.strictEqual(call("ann", "coopWalletRpc").balances.crystals, price);
  fails("ann", "partyUpdateRpc", { name: "Fuck owls" }, "name not allowed");
  assert.strictEqual(call("ann", "ticketMineRpc").tickets[0].owned, 1);
  assert.strictEqual(call("ann", "partyUpdateRpc", { name: "Second name" }).name, "Second name");
  assert.strictEqual(call("ann", "ticketMineRpc").tickets[0].owned, 0);
  assert.strictEqual(call("ann", "partyUpdateRpc", { name: "" }).name, "");
  assert.strictEqual(call("bob", "partyListRpc", {}).parties[0].title, "late_owls");
  fails("ann", "partyUpdateRpc", { name: "Second name" }, "no ticket");
});

test("a clan gets its own name the same way, with its own ticket", () => {
  const { nk, call, fails } = world("ann", "bob");
  const clan = call("ann", "clanCreateRpc", { access: "open", adjective: "lilac", noun: "lanterns", name: "Орден Кромки" });
  assert.strictEqual(clan.name, "Орден Кромки");
  assert.strictEqual(clan.adjective, "lilac");
  assert.strictEqual(call("bob", "clanListRpc", {}).clans[0].name, "Орден Кромки");
  fails("ann", "clanUpdateRpc", { name: "Пиздец" }, "name not allowed");
  fails("ann", "clanUpdateRpc", { name: "Another" }, "no ticket");
  server.walletGrant(nk, "ann", "crystal_purchase", 5000, Date.now());
  call("ann", "ticketBuyRpc", { id: "party_rename" });
  fails("ann", "clanUpdateRpc", { name: "Another" }, "no ticket");
  call("ann", "ticketBuyRpc", { id: "clan_rename" });
  assert.strictEqual(call("ann", "clanUpdateRpc", { name: "Another" }).name, "Another");
  call("bob", "clanJoinRpc", { code: clan.code });
  fails("bob", "clanUpdateRpc", { name: "Bobs" }, "only the leader can do that");
});

test("party chat: members talk, outsiders do not, history is read after a number", () => {
  const { call, fails } = world("ann", "bob", "eve");
  const party = call("ann", "partyCreateRpc", {});
  call("bob", "partyJoinRpc", { code: party.code });
  const now = Date.now();
  const first = at(now, () => call("ann", "chatSendRpc", { channel: "party", text: "  привет,\n отряд " }));
  assert.strictEqual(first.seq, 1);
  assert.deepStrictEqual(first.messages.map((m) => [m.seq, m.from, m.name, m.text]), [[1, "ann", "ann", "привет, отряд"]]);
  assert.strictEqual(first.canModerate, true);
  at(now + 2000, () => call("bob", "chatSendRpc", { channel: "party", text: "hi" }));
  const read = at(now + 3000, () => call("ann", "chatReadRpc", { channel: "party", after: 1 }));
  assert.deepStrictEqual(read.messages.map((m) => m.text), ["hi"]);
  assert.strictEqual(call("bob", "chatReadRpc", { channel: "party" }).canModerate, false);
  fails("eve", "chatSendRpc", { channel: "party", text: "let me in" }, "not in a party");
  fails("eve", "chatReadRpc", { channel: "clan" }, "not in a clan");
  fails("ann", "chatSendRpc", { channel: "nowhere", text: "x" }, "bad request");
  fails("ann", "chatSendRpc", { channel: "party", text: "   " }, "bad request");
  fails("ann", "chatSendRpc", { channel: "party", text: "x".repeat(rules.chatMax + 1) }, "bad request");
});

test("chat hides banned words, keeps a short history and forgets old lines", () => {
  const { call } = world("ann");
  call("ann", "partyCreateRpc", {});
  const channel = rules.channels.filter((c) => c.id === "party")[0];
  const now = Date.now();
  const masked = at(now, () => call("ann", "chatSendRpc", { channel: "party", text: "what the fuck is this" }));
  assert.strictEqual(masked.messages[0].text, "what the *** is this");
  for (let i = 1; i <= channel.history + 5; i++)
    at(now + i * 61000, () => call("ann", "chatSendRpc", { channel: "party", text: "line " + i }));
  const all = at(now + (channel.history + 6) * 61000, () => call("ann", "chatReadRpc", { channel: "party" }));
  assert.strictEqual(all.messages.length, channel.history);
  assert.strictEqual(all.messages[all.messages.length - 1].text, "line " + (channel.history + 5));
  const later = at(now + (rules.chatDays + 1) * day, () => call("ann", "chatReadRpc", { channel: "party" }));
  assert.strictEqual(later.messages.length, 0);
});

test("chat slows down a player who writes too fast", () => {
  const { call, fails } = world("ann");
  const channel = rules.channels.filter((c) => c.id === "world")[0];
  const now = Date.now();
  at(now, () => call("ann", "chatSendRpc", { channel: "world", text: "one" }));
  at(now + channel.cooldownMs - 1, () => fails("ann", "chatSendRpc", { channel: "world", text: "two" }, "too fast"));
  for (let i = 1; i < channel.perMinute; i++)
    at(now + i * channel.cooldownMs, () => call("ann", "chatSendRpc", { channel: "world", text: "more " + i }));
  at(now + channel.perMinute * channel.cooldownMs, () => fails("ann", "chatSendRpc", { channel: "world", text: "over" }, "too fast"));
  at(now + 61000 + channel.perMinute * channel.cooldownMs, () => call("ann", "chatSendRpc", { channel: "world", text: "again" }));
});

test("world chat is split by language and hides players you blocked", () => {
  const { call } = world("ann", "bob", "eve");
  call("ann", "chatSendRpc", { channel: "world", language: "ru", text: "всем привет" });
  call("eve", "chatSendRpc", { channel: "world", language: "ru", text: "купите золото" });
  assert.strictEqual(call("bob", "chatReadRpc", { channel: "world", language: "en" }).messages.length, 0);
  assert.strictEqual(call("bob", "chatReadRpc", { channel: "world", language: "ru" }).messages.length, 2);
  call("bob", "partyBlockRpc", { userId: "eve" });
  assert.deepStrictEqual(call("bob", "chatReadRpc", { channel: "world", language: "ru" }).messages.map((m) => m.from), ["ann"]);
});

test("the author and the room's leaders delete a line, the others cannot", () => {
  const { call, fails } = world("ann", "bob", "cat");
  const clan = call("ann", "clanCreateRpc", {});
  call("bob", "clanJoinRpc", { code: clan.code });
  call("cat", "clanJoinRpc", { code: clan.code });
  call("ann", "clanPromoteRpc", { userId: "bob" });
  const now = Date.now();
  at(now - 60000, () => call("cat", "chatSendRpc", { channel: "clan", text: "one" }));
  call("ann", "chatSendRpc", { channel: "clan", text: "two" });
  call("cat", "chatSendRpc", { channel: "clan", text: "three" });
  fails("cat", "chatDeleteRpc", { channel: "clan", seq: 2 }, "not yours");
  fails("cat", "chatDeleteRpc", { channel: "clan", seq: 9 }, "no such message");
  const afterOfficer = call("bob", "chatDeleteRpc", { channel: "clan", seq: 1 });
  assert.deepStrictEqual(Array.from(afterOfficer.removed), [1]);
  const afterAuthor = call("cat", "chatDeleteRpc", { channel: "clan", seq: 3 });
  assert.deepStrictEqual(afterAuthor.messages.map((m) => m.text), ["two"]);
  assert.deepStrictEqual(Array.from(call("ann", "chatReadRpc", { channel: "clan", after: 3 }).removed), [1, 3]);
});

test("reports are kept for the operator, and enough of them mute a player in the world chat", () => {
  const names = crowd(rules.autoMuteReporters);
  const { nk, call, fails } = world("eve", ...names);
  const sent = call("eve", "chatSendRpc", { channel: "world", text: "buy gold at my site" });
  fails("eve", "chatReportRpc", { channel: "world", seq: sent.seq, reason: "spam" }, "bad request");
  fails("p0", "chatReportRpc", { channel: "world", seq: sent.seq, reason: "boring" }, "bad request");
  fails("p0", "chatReportRpc", { channel: "world", seq: 99, reason: "spam" }, "no such message");
  names.forEach((name, i) => assert.strictEqual(call(name, "chatReportRpc", { channel: "world", seq: sent.seq, reason: "spam" }).muted, i === names.length - 1));
  fails("eve", "chatSendRpc", { channel: "world", text: "still here" }, "muted");
  assert.ok(call("eve", "chatReadRpc", { channel: "world" }).mutedUntil > Date.now());
  call("eve", "partyCreateRpc", {});
  call("eve", "chatSendRpc", { channel: "party", text: "my own party hears me" });
  const board = JSON.parse(server.chatModerationRpc({ env: {} }, logger, nk, ""));
  assert.strictEqual(board.reports.length, names.length);
  assert.strictEqual(board.reports[0].text, "buy gold at my site");
  assert.deepStrictEqual(board.mutes.map((m) => [m.userId, m.scope, m.reason]), [["eve", "world", "reports"]]);
  assert.throws(() => server.chatModerationRpc({ userId: "p0", env: {} }, logger, nk, ""), (error) => error.code === 7);
});

test("the operator mutes, unmutes and removes lines; players cannot", () => {
  const { nk, call, fails } = world("ann", "eve");
  const operator = (rpc, request) => JSON.parse(server[rpc]({ env: {} }, logger, nk, JSON.stringify(request)));
  assert.throws(() => server.chatMuteRpc({ userId: "eve", env: {} }, logger, nk, JSON.stringify({ userId: "ann" })), (error) => error.code === 7);
  assert.throws(() => server.chatModerationRpc({ userId: "eve", env: {} }, logger, nk, ""), (error) => error.code === 7);
  const now = Date.now();
  const sent = at(now, () => call("eve", "chatSendRpc", { channel: "world", text: "rude line" }));
  assert.strictEqual(operator("chatRemoveRpc", { room: sent.room, seq: sent.seq }).removed, true);
  assert.strictEqual(operator("chatRemoveRpc", { room: sent.room, seq: sent.seq }).removed, false);
  assert.strictEqual(call("ann", "chatReadRpc", { channel: "world" }).messages.length, 0);
  assert.ok(operator("chatMuteRpc", { userId: "eve", hours: 48 }).until > now + 47 * 3600000);
  call("eve", "partyCreateRpc", {});
  at(now + 60000, () => fails("eve", "chatSendRpc", { channel: "party", text: "hello" }, "muted"));
  assert.strictEqual(operator("chatMuteRpc", { userId: "eve", hours: 0 }).until, 0);
  at(now + 120000, () => call("eve", "chatSendRpc", { channel: "party", text: "hello" }));
});

test("a chat goes away together with its party", () => {
  const { nk, call } = world("ann");
  const party = call("ann", "partyCreateRpc", {});
  call("ann", "chatSendRpc", { channel: "party", text: "anyone?" });
  assert.strictEqual(nk.storageRead([{ collection: "chat", key: party.id, userId: server.PartySystemUser }]).length, 1);
  call("ann", "partyLeaveRpc");
  assert.strictEqual(nk.storageRead([{ collection: "chat", key: party.id, userId: server.PartySystemUser }]).length, 0);
});

test("developers and moderators carry a badge and keep order from inside the game", () => {
  const { nk, call, fails } = world("dev", "mod", "eve", "ann");
  const operator = (rpc, request) => JSON.parse(server[rpc]({ env: {} }, logger, nk, JSON.stringify(request)));
  const staffFails = (user, rpc, request) => assert.throws(() => call(user, rpc, request), (error) => error.code === 7);
  assert.deepStrictEqual(Array.from(rules.roles.map((role) => role.id)), ["developer", "moderator"]);
  operator("chatRoleRpc", { userId: "dev", role: "developer" });
  assert.strictEqual(call("dev", "chatRoleRpc", { userId: "mod", role: "moderator" }).role, "moderator");
  staffFails("mod", "chatRoleRpc", { userId: "eve", role: "moderator" });
  staffFails("eve", "chatRoleRpc", { userId: "eve", role: "developer" });
  assert.throws(() => call("dev", "chatRoleRpc", { userId: "eve", role: "king" }), (error) => error.message === "bad request");
  const now = Date.now();
  const line = at(now, () => call("dev", "chatSendRpc", { channel: "world", text: "patch is out" }));
  assert.strictEqual(line.messages[0].role, "developer");
  assert.strictEqual(line.role, "developer");
  const rude = at(now, () => call("eve", "chatSendRpc", { channel: "world", text: "rude line" }));
  assert.strictEqual(rude.messages[1].role, "");
  assert.strictEqual(rude.canModerate, false);
  const view = call("mod", "chatReadRpc", { channel: "world" });
  assert.strictEqual(view.canModerate, true);
  assert.deepStrictEqual(call("mod", "chatDeleteRpc", { channel: "world", seq: rude.seq }).messages.map((m) => m.from), ["dev"]);
  const muted = call("mod", "chatMuteRpc", { userId: "eve", hours: 9999, scope: "world" });
  const limit = rules.roles.filter((role) => role.id === "moderator")[0].muteHours;
  assert.ok(muted.until <= Date.now() + limit * 3600000 && muted.until > Date.now() + (limit - 1) * 3600000);
  at(now + 60000, () => fails("eve", "chatSendRpc", { channel: "world", text: "again" }, "muted"));
  staffFails("mod", "chatMuteRpc", { userId: "dev", hours: 1 });
  staffFails("eve", "chatMuteRpc", { userId: "ann", hours: 1 });
  assert.strictEqual(call("mod", "chatModerationRpc", {}).staff.length, 2);
  operator("chatRoleRpc", { userId: "mod", role: "" });
  staffFails("mod", "chatMuteRpc", { userId: "eve", hours: 1 });
});
