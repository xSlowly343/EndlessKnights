const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama } = require("./fake-nakama");

const server = loadModule();
const rules = server.RiftServerRules;
const plain = (o) => JSON.parse(JSON.stringify(o));
const window = server.riftNext(rules, Date.UTC(2026, 10, 13, 12));
const riftId = window.id;
const inside = window.startUnixMs + 3600000;

function world(crystals) {
  const nk = new FakeNakama();
  nk.addUser("ann", "ann", "76561198000000001");
  if (crystals > 0)
    server.walletGrant(nk, "ann", "crystal_purchase", crystals, inside);
  return nk;
}

const offer = (id) => rules.shop.find((entry) => entry.id === id);
const buy = (nk, id, at, rift) => plain(server.riftShopBuy(nk, "ann", rift === undefined ? riftId : rift, id, at === undefined ? inside : at));

test("the Rift shop sells sparks for crystals and counts them per window", () => {
  assert.ok(rules.shop.length > 0);
  assert.strictEqual(rules.shopCurrency, "crystals");
  const nk = world(5000);
  const first = buy(nk, "sparks_bag");
  assert.strictEqual(first.riftId, riftId);
  assert.strictEqual(first.bought.sparks_bag, 1);
  assert.strictEqual(first.sparks, offer("sparks_bag").sparks);
  assert.strictEqual(first.crystals, 5000 - offer("sparks_bag").price);
  const second = buy(nk, "sparks_handful");
  assert.strictEqual(second.sparks, offer("sparks_bag").sparks + offer("sparks_handful").sparks, "the total bought in this window");
  assert.deepStrictEqual(plain(server.riftShopView(nk, "ann", riftId)), second);
});

test("the window limit holds and a new window starts from zero", () => {
  const nk = world(20000);
  const chest = offer("sparks_chest");
  for (let i = 0; i < chest.perWindow; i++)
    buy(nk, "sparks_chest");
  assert.throws(() => buy(nk, "sparks_chest"), /window limit/);
  assert.throws(() => buy(nk, "sparks_chest", inside, riftId + 60), /window limit/, "a party rift in the same window does not reset the limit");
  const next = server.riftNext(rules, window.endUnixMs + 1000);
  const fresh = buy(nk, "sparks_chest", next.startUnixMs + 1000, next.id);
  assert.strictEqual(fresh.bought.sparks_chest, 1);
  assert.strictEqual(fresh.sparks, chest.sparks);
});

test("the Rift shop is closed outside an open window and without crystals", () => {
  const nk = world(5000);
  assert.throws(() => buy(nk, "sparks_bag", window.startUnixMs - 1000), /closed/);
  assert.throws(() => buy(nk, "sparks_bag", server.riftPartyEnd(riftId) + window.endUnixMs), /closed/);
  assert.throws(() => buy(nk, "sparks_bag", inside, riftId - 86400), /closed/, "an id outside every window");
  const party = buy(nk, "sparks_handful", inside, riftId + 60);
  assert.strictEqual(party.sparks, offer("sparks_handful").sparks, "a party rift gets its own sparks");
  assert.strictEqual(party.bought.sparks_handful, 1, "but the limit is shared by the whole window");
  assert.throws(() => buy(nk, "not_an_offer"), /no such offer/);
  const poor = world(0);
  assert.throws(() => buy(poor, "sparks_bag"));
  assert.deepStrictEqual(plain(server.riftShopView(poor, "ann", riftId)).bought, {}, "a failed purchase leaves nothing");
});
