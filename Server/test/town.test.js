const test = require("node:test");
const assert = require("node:assert");
const { loadModule, stockBag } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.TownServerRules;
const day = 86400000;
const now = Date.UTC(2026, 10, 6, 12);
const plain = (o) => JSON.parse(JSON.stringify(o));

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  names.forEach((name, i) => names.slice(i + 1).forEach((other) => nk.addFriends(name, other)));
  stockBag(server, nk, names);
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  return { nk, call };
}

test("a fresh town has no guild buildings, no gifts and the daily limit", () => {
  const { call } = world("ann");
  const mine = call("ann", "townMineRpc");
  assert.deepStrictEqual(mine.guilds, []);
  assert.deepStrictEqual(mine.inbox, []);
  assert.strictEqual(mine.sentToday, 0);
  assert.strictEqual(mine.giftsPerDay, rules.giftsPerDay);
});

test("gifts carry only harvest, within the amount and to another player", () => {
  const { nk } = world("ann", "bob");
  assert.throws(() => server.townGiftSend(nk, "ann", { to: "bob", item: "gem_fireball", amount: 1 }, now), /not giftable/);
  assert.throws(() => server.townGiftSend(nk, "ann", { to: "bob", item: "herb_windwort", amount: rules.giftMaxAmount + 1 }, now), /bad amount/);
  assert.throws(() => server.townGiftSend(nk, "ann", { to: "bob", item: "herb_windwort", amount: 0 }, now), /bad amount/);
  assert.throws(() => server.townGiftSend(nk, "ann", { to: "ann", item: "herb_windwort", amount: 1 }, now), /not to yourself/);
  assert.throws(() => server.townGiftSend(nk, "ann", { to: "nobody", item: "herb_windwort", amount: 1 }, now), /no such player/);
  assert.throws(() => server.townGiftSend(nk, "ann", { to: "bob", item: "herb_windwort" }, now), /bad request/);
  const sent = plain(server.townGiftSend(nk, "ann", { to: "bob", item: "herb_windwort", amount: 3 }, now));
  assert.strictEqual(sent.sentToday, 1);
  assert.strictEqual(sent.sent.fromName, "ann");
  const inbox = plain(server.townMine(nk, "bob", now)).inbox;
  assert.strictEqual(inbox.length, 1);
  assert.strictEqual(inbox[0].item, "herb_windwort");
  assert.strictEqual(inbox[0].amount, 3);
});

test("the daily limit resets on the next day", () => {
  const { nk } = world("ann", "bob");
  for (let i = 0; i < rules.giftsPerDay; i++)
    server.townGiftSend(nk, "ann", { to: "bob", item: "herb_knockbloom", amount: 1 }, now + i);
  assert.throws(() => server.townGiftSend(nk, "ann", { to: "bob", item: "herb_knockbloom", amount: 1 }, now + 100), /daily limit/);
  assert.strictEqual(plain(server.townGiftSend(nk, "ann", { to: "bob", item: "herb_knockbloom", amount: 1 }, now + day)).sentToday, 1);
});

test("a full inbox refuses more gifts and claiming empties it once", () => {
  const { nk } = world("bob", ...Array.from({ length: Math.ceil(rules.giftInbox / rules.giftsPerDay) + 1 }, (_, i) => "friend" + i));
  let sent = 0;
  for (let friend = 0; sent < rules.giftInbox; friend++)
    for (let i = 0; i < rules.giftsPerDay && sent < rules.giftInbox; i++, sent++)
      server.townGiftSend(nk, "friend" + friend, { to: "bob", item: "herb_hushbell", amount: 2 }, now + sent);
  assert.throws(() => server.townGiftSend(nk, "friend" + (Math.ceil(rules.giftInbox / rules.giftsPerDay)), { to: "bob", item: "herb_hushbell", amount: 1 }, now + 1000), /inbox is full/);
  const claimed = plain(server.townGiftClaim(nk, "bob", undefined, now));
  assert.strictEqual(claimed.claimed.length, rules.giftInbox);
  assert.strictEqual(plain(server.townGiftClaim(nk, "bob", undefined, now)).claimed.length, rules.giftInbox, "gifts wait until the client confirms them");
  const left = plain(server.townGiftClaim(nk, "bob", claimed.claimed.slice(1).map((gift) => gift.id), now));
  assert.deepStrictEqual(left.claimed.map((gift) => gift.id), [claimed.claimed[0].id]);
  assert.deepStrictEqual(plain(server.townGiftClaim(nk, "bob", [claimed.claimed[0].id, 7, "stranger"], now)).inbox, []);
});

test("built guilds come from the town record of the server", () => {
  const { nk } = world("ann");
  nk.storageWrite([{ collection: "town", key: "guilds", userId: server.PartySystemUser, value: { built: ["gardeners"] }, version: "*", permissionRead: 0, permissionWrite: 0 }]);
  assert.deepStrictEqual(plain(server.townMine(nk, "ann", now)).guilds, ["gardeners"]);
});

test("gifts go only to friends, the clan and the party", () => {
  const nk = new FakeNakama();
  for (const name of ["ann", "bob", "cid", "dan"])
    nk.addUser(name, name, "76561198000000" + name.length + name.charCodeAt(0));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  stockBag(server, nk, ["ann", "bob", "cid", "dan"]);
  const gift = (from, to) => server.townGiftSend(nk, from, { to: to, item: "herb_windwort", amount: 1 }, now);
  assert.throws(() => gift("ann", "bob"), /only to friends, clan and party/);
  nk.addFriends("ann", "bob");
  assert.strictEqual(plain(gift("ann", "bob")).sentToday, 1, "a friend");
  const created = call("ann", "partyCreateRpc", {});
  call("cid", "partyJoinRpc", { code: created.code });
  assert.strictEqual(plain(gift("cid", "ann")).sentToday, 1, "the party");
  server.clanCreateFor(nk, "dan", { name: "Северный ветер", tag: "NW", language: "ru" }, now);
  assert.throws(() => gift("dan", "bob"), /only to friends, clan and party/);
  server.clanAdd(nk, server.clanGroupOfUser(nk, "dan", now), "bob", now);
  assert.strictEqual(plain(gift("dan", "bob")).sentToday, 1, "the clan");
});

test("a gift leaves only what the last cloud save shows, and received gifts can be passed on", () => {
  const { nk } = world("ann", "bob", "cid");
  server.inflowBaseline(nk, "ann", { herb_windwort: 4 });
  server.townGiftSend(nk, "ann", { to: "bob", item: "herb_windwort", amount: 4 }, now);
  assert.throws(() => server.townGiftSend(nk, "ann", { to: "bob", item: "herb_windwort", amount: 1 }, now), /not in your bag/);
  assert.strictEqual(plain(server.townMine(nk, "ann", now)).sentToday, 1, "a refused gift does not use up the daily count");
  server.inflowBaseline(nk, "bob", {});
  const inbox = plain(server.townGiftClaim(nk, "bob", undefined, now)).claimed;
  assert.strictEqual(server.inflowRead(nk, "bob").value.held.herb_windwort, undefined, "nothing is held until the gift is confirmed");
  server.townGiftClaim(nk, "bob", inbox.map((entry) => entry.id), now);
  assert.strictEqual(plain(server.townMine(nk, "bob", now)).available.herb_windwort, 4);
  server.townGiftSend(nk, "bob", { to: "cid", item: "herb_windwort", amount: 4 }, now);
  assert.strictEqual(plain(server.townMine(nk, "bob", now)).available.herb_windwort, 0, "a received gift can be passed on");
});
