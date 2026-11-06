const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.TownServerRules;
const day = 86400000;
const now = Date.UTC(2026, 10, 6, 12);
const plain = (o) => JSON.parse(JSON.stringify(o));

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
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
  const claimed = plain(server.townGiftClaim(nk, "bob", now));
  assert.strictEqual(claimed.claimed.length, rules.giftInbox);
  assert.deepStrictEqual(claimed.inbox, []);
  assert.deepStrictEqual(plain(server.townGiftClaim(nk, "bob", now)).claimed, []);
});

test("built guilds come from the town record of the server", () => {
  const { nk } = world("ann");
  nk.storageWrite([{ collection: "town", key: "guilds", userId: server.PartySystemUser, value: { built: ["gardeners"] }, version: "*", permissionRead: 0, permissionWrite: 0 }]);
  assert.deepStrictEqual(plain(server.townMine(nk, "ann", now)).guilds, ["gardeners"]);
});
