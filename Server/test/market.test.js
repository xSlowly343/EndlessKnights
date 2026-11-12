const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama } = require("./fake-nakama");

const server = loadModule();
const rules = server.MarketServerRules;
const day = 86400000;
const now = Date.UTC(2026, 10, 12, 12);
const food = rules.items[0];
const plain = (o) => JSON.parse(JSON.stringify(o));

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  return nk;
}

function rich(nk, user, amount) {
  server.walletRefund(nk, user, rules.currency, amount, "test", now);
}

function balance(nk, user) {
  return server.walletRead(nk, user).value.balances[rules.currency];
}

test("the market trades companion treats for a coop currency that is not crystals", () => {
  assert.notStrictEqual(rules.currency, "crystals");
  assert.ok(rules.items.length > 0);
  assert.ok(rules.items.every((item) => item.startsWith("treat_") && !item.endsWith("_delicacy")));
});

test("a lot needs a treat, an amount and a price within the limits", () => {
  const nk = world("ann");
  assert.throws(() => server.marketSell(nk, "ann", { item: "herb_windwort", amount: 1, price: 1, nonce: "a1" }, now), /not for sale/);
  assert.throws(() => server.marketSell(nk, "ann", { item: "treat_bloom_delicacy", amount: 1, price: 1, nonce: "a1" }, now), /not for sale/);
  assert.throws(() => server.marketSell(nk, "ann", { item: food, amount: 0, price: 1, nonce: "a1" }, now), /bad amount/);
  assert.throws(() => server.marketSell(nk, "ann", { item: food, amount: rules.maxAmount + 1, price: 1, nonce: "a1" }, now), /bad amount/);
  assert.throws(() => server.marketSell(nk, "ann", { item: food, amount: 1, price: rules.minPrice - 1, nonce: "a1" }, now), /bad price/);
  assert.throws(() => server.marketSell(nk, "ann", { item: food, amount: 1, price: rules.maxPrice + 1, nonce: "a1" }, now), /bad price/);
  assert.throws(() => server.marketSell(nk, "ann", { item: food, amount: 1.5, price: 1, nonce: "a1" }, now), /bad request/);
  assert.throws(() => server.marketSell(nk, "ann", { item: food, amount: 1, price: 1, nonce: "A 1" }, now), /bad request/);
  const view = plain(server.marketSell(nk, "ann", { item: food, amount: 3, price: 4, nonce: "a1" }, now));
  assert.strictEqual(view.mine.length, 1);
  assert.strictEqual(view.listed.sellerName, "ann");
  assert.strictEqual(view.listed.until, now + rules.days * day);
  assert.deepStrictEqual(view.lots, [], "your own lots are not on the counter");
});

test("the same request sent twice lists one lot", () => {
  const nk = world("ann");
  server.marketSell(nk, "ann", { item: food, amount: 3, price: 4, nonce: "a1" }, now);
  const again = plain(server.marketSell(nk, "ann", { item: food, amount: 3, price: 4, nonce: "a1" }, now + 5));
  assert.strictEqual(again.mine.length, 1);
  assert.strictEqual(again.listed.at, now);
});

test("a seller holds a limited number of lots", () => {
  const nk = world("ann");
  for (let i = 0; i < rules.maxLots; i++)
    server.marketSell(nk, "ann", { item: food, amount: 1, price: 2, nonce: "n" + i }, now + i);
  assert.throws(() => server.marketSell(nk, "ann", { item: food, amount: 1, price: 2, nonce: "extra" }, now + 99), /too many lots/);
});

test("buying moves the price to the seller less the fee and the treat to the buyer's inbox", () => {
  const nk = world("ann", "bob");
  rich(nk, "bob", 100);
  const lot = plain(server.marketSell(nk, "ann", { item: food, amount: 5, price: 4, nonce: "a1" }, now)).listed;
  const counter = plain(server.marketView(nk, "bob", undefined, now + 1, {}));
  assert.strictEqual(counter.lots.length, 1);
  assert.strictEqual(counter.balance, 100);
  assert.throws(() => server.marketBuy(nk, "ann", { lot: lot.id }, now + 2), /your own lot/);

  const view = plain(server.marketBuy(nk, "bob", { lot: lot.id }, now + 2));

  assert.strictEqual(view.paid, 20);
  assert.strictEqual(view.balance, 80);
  assert.strictEqual(balance(nk, "ann"), 20 - Math.ceil(20 * rules.fee));
  assert.deepStrictEqual(view.lots, []);
  assert.strictEqual(view.inbox.length, 1);
  assert.strictEqual(view.inbox[0].item, food);
  assert.strictEqual(view.inbox[0].amount, 5);
  assert.strictEqual(view.inbox[0].reason, "bought");
  assert.throws(() => server.marketBuy(nk, "bob", { lot: lot.id }, now + 3), /lot is gone/);
  assert.strictEqual(balance(nk, "bob"), 80, "a lot that is gone costs nothing");
});

test("a buyer without the money gets nothing and the lot stays", () => {
  const nk = world("ann", "bob");
  rich(nk, "bob", 3);
  const lot = plain(server.marketSell(nk, "ann", { item: food, amount: 2, price: 2, nonce: "a1" }, now)).listed;
  assert.throws(() => server.marketBuy(nk, "bob", { lot: lot.id }, now + 1), /not enough/);
  assert.strictEqual(balance(nk, "bob"), 3);
  assert.strictEqual(plain(server.marketView(nk, "bob", undefined, now + 2, {})).lots.length, 1);
});

test("claiming empties the inbox once", () => {
  const nk = world("ann", "bob");
  rich(nk, "bob", 10);
  const lot = plain(server.marketSell(nk, "ann", { item: food, amount: 1, price: 2, nonce: "a1" }, now)).listed;
  const bought = plain(server.marketBuy(nk, "bob", { lot: lot.id }, now + 1)).bought;
  assert.strictEqual(plain(server.marketClaim(nk, "bob", undefined, now + 2)).inbox.length, 1, "nothing is dropped until the client confirms");
  assert.deepStrictEqual(plain(server.marketClaim(nk, "bob", [bought.id], now + 3)).inbox, []);
});

test("a cancelled lot returns to its seller and an expired one leaves the counter", () => {
  const nk = world("ann", "bob");
  rich(nk, "bob", 100);
  const lot = plain(server.marketSell(nk, "ann", { item: food, amount: 4, price: 2, nonce: "a1" }, now)).listed;
  const late = now + rules.days * day;
  assert.deepStrictEqual(plain(server.marketView(nk, "bob", undefined, late, {})).lots, []);
  assert.throws(() => server.marketBuy(nk, "bob", { lot: lot.id }, late), /lot is gone/);
  assert.throws(() => server.marketCancel(nk, "bob", { lot: lot.id }, late), /not your lot/);
  assert.strictEqual(plain(server.marketView(nk, "ann", undefined, late, {})).mine.length, 1, "the seller still sees an expired lot");

  const view = plain(server.marketCancel(nk, "ann", { lot: lot.id }, late + 1));

  assert.deepStrictEqual(view.mine, []);
  assert.strictEqual(view.returned.reason, "returned");
  assert.strictEqual(view.inbox[0].amount, 4);
  assert.throws(() => server.marketCancel(nk, "ann", { lot: lot.id }, late + 2), /lot is gone/);
});

test("the counter shows the cheapest lots first and filters by treat", () => {
  const nk = world("ann", "bob", "cat");
  const other = rules.items[1];
  server.marketSell(nk, "ann", { item: food, amount: 1, price: 9, nonce: "a1" }, now);
  server.marketSell(nk, "bob", { item: food, amount: 1, price: 3, nonce: "b1" }, now + 1);
  server.marketSell(nk, "bob", { item: other, amount: 1, price: 1, nonce: "b2" }, now + 2);
  const all = plain(server.marketView(nk, "cat", undefined, now + 3, {}));
  assert.deepStrictEqual(all.lots.map((lot) => lot.price), [1, 3, 9]);
  const filtered = plain(server.marketView(nk, "cat", food, now + 3, {}));
  assert.deepStrictEqual(filtered.lots.map((lot) => lot.price), [3, 9]);
});
