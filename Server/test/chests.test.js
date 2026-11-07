const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.ChestServerRules;
const day = 86400000;
const plain = (o) => JSON.parse(JSON.stringify(o));
const monday = server.seasonPassWeekStart(3000);
const now = monday + 2 * day;

function world() {
  const nk = new FakeNakama();
  nk.addUser("ann", "ann", "76561198000000001");
  const call = (rpc, request) => JSON.parse(server[rpc]({ userId: "ann", env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  return { nk, call };
}

const give = (nk, crystals) => server.walletGrant(nk, "ann", "crystal_purchase", crystals, now);
const chest = (id) => rules.chests.find((entry) => entry.id === id);
const letters = (nk) => plain(server.mailLetters(nk, "ann"));

test("every chest holds a fixed amount of ingots or essence and is sold for crystals", () => {
  assert.strictEqual(rules.currency, "crystals");
  assert.ok(rules.chests.length > 0);
  for (const entry of rules.chests) {
    assert.ok(entry.price > 0, entry.id);
    assert.ok(entry.ingots + entry.essence > 0, entry.id);
    assert.ok(entry.perWeek >= 1, entry.id);
  }
});

test("a chest takes crystals and arrives as a letter with its exact contents", () => {
  const { nk } = world();
  const smith = chest("smith");
  give(nk, smith.price + 7);
  const view = plain(server.chestBuy(nk, logger, "ann", "smith", now));
  assert.strictEqual(view.crystals, 7);
  assert.strictEqual(view.bought.smith, 1);
  const mail = letters(nk);
  assert.strictEqual(mail.length, 1);
  assert.strictEqual(mail[0].id, view.letter);
  assert.strictEqual(mail[0].kind, "chest");
  assert.strictEqual(mail[0].title, "smith");
  assert.deepStrictEqual(mail[0].reward, { shards: 0, ingots: smith.ingots, essence: smith.essence });
});

test("without enough crystals nothing is spent and no letter comes", () => {
  const { nk } = world();
  give(nk, chest("master").price - 1);
  assert.throws(() => server.chestBuy(nk, logger, "ann", "master", now), /not enough crystals/);
  assert.strictEqual(letters(nk).length, 0);
  assert.strictEqual(plain(server.chestView(nk, "ann", now, "")).bought.master, undefined);
});

test("the weekly limit stops a chest until monday and then opens again", () => {
  const { nk } = world();
  const small = chest("smith_small");
  give(nk, small.price * (small.perWeek + 1));
  for (let i = 0; i < small.perWeek; i++)
    server.chestBuy(nk, logger, "ann", "smith_small", now + i);
  assert.throws(() => server.chestBuy(nk, logger, "ann", "smith_small", now + 10), /weekly limit/);
  assert.strictEqual(plain(server.chestView(nk, "ann", now, "")).crystals, small.price, "the refused chest cost nothing");
  const next = plain(server.chestBuy(nk, logger, "ann", "smith_small", monday + 7 * day + 1));
  assert.strictEqual(next.bought.smith_small, 1);
  assert.strictEqual(next.crystals, 0);
  assert.strictEqual(new Set(letters(nk).map((letter) => letter.id)).size, small.perWeek + 1, "every chest is its own letter");
});

test("unknown chests and bad requests are refused through the rpc", () => {
  const { nk, call } = world();
  give(nk, 5000);
  assert.throws(() => call("chestBuyRpc", { id: "gold_mountain" }), /no such chest/);
  assert.throws(() => call("chestBuyRpc", { id: 5 }), /bad request/);
  const shop = call("chestShopRpc");
  assert.strictEqual(shop.crystals, 5000);
  assert.deepStrictEqual(shop.bought, {});
});

test("a claimed chest letter hands over essence too", () => {
  const { nk, call } = world();
  give(nk, chest("jeweler").price);
  const bought = call("chestBuyRpc", { id: "jeweler" });
  const claimed = call("mailClaimRpc", { id: bought.letter });
  assert.strictEqual(claimed.letter.reward.essence, chest("jeweler").essence);
  assert.throws(() => call("mailClaimRpc", { id: bought.letter }), /already claimed/);
});
