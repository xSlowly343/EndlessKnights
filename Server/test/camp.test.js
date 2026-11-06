const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const day = 86400000;
const now = Date.UTC(2026, 10, 6, 12);
const plain = (o) => JSON.parse(JSON.stringify(o));
const snapshot = { theme: "ice_peak", decor: ["decor_lantern", ""], monuments: { milestone: 2 }, title: "legends", trophies: ["trophy_dragon_koi"], brand: "hills_guardian" };

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  return nk;
}

test("a published camp is what visitors see, with today's acts left", () => {
  const nk = world("ann", "bob");
  server.campPublish(nk, "ann", snapshot, now);
  const visit = plain(server.campVisit(nk, "bob", { host: "ann" }, now));
  assert.strictEqual(visit.camp.theme, "ice_peak");
  assert.deepStrictEqual(visit.camp.monuments, { milestone: 2 });
  assert.deepStrictEqual(visit.left, ["gift", "water", "hammer"]);
  assert.throws(() => server.campVisit(nk, "ann", { host: "ann" }, now), /not your own camp/);
  assert.throws(() => server.campVisit(nk, "ann", { host: "bob" }, now), /no such camp/);
});

test("bad snapshots are refused", () => {
  const nk = world("ann");
  assert.throws(() => server.campPublish(nk, "ann", { theme: "x".repeat(100) }, now), /bad request/);
  assert.throws(() => server.campPublish(nk, "ann", { decor: new Array(40).fill("a") }, now), /bad request/);
  assert.throws(() => server.campPublish(nk, "ann", { monuments: { milestone: -1 } }, now), /bad request/);
});

test("each act once a day per host; the gift pays friendship to both", () => {
  const nk = world("ann", "bob");
  server.campPublish(nk, "ann", snapshot, now);
  const gift = plain(server.campGuest(nk, "bob", { host: "ann", act: "gift" }, now));
  assert.strictEqual(gift.friendship, 1);
  assert.deepStrictEqual(gift.left, ["water", "hammer"]);
  assert.strictEqual(server.walletRead(nk, "ann").value.balances.friendship, 1);
  assert.strictEqual(server.walletRead(nk, "bob").value.balances.friendship, 1);
  assert.throws(() => server.campGuest(nk, "bob", { host: "ann", act: "gift" }, now + 1000), /already done today/);
  server.campGuest(nk, "bob", { host: "ann", act: "water", bed: 2 }, now);
  assert.throws(() => server.campGuest(nk, "bob", { host: "ann", act: "dance" }, now), /bad request/);
  assert.deepStrictEqual(plain(server.campGuest(nk, "bob", { host: "ann", act: "gift" }, now + day)).left, ["water", "hammer"]);
});

test("the host claims what guests did once", () => {
  const nk = world("ann", "bob", "cid");
  server.campPublish(nk, "ann", snapshot, now);
  server.campGuest(nk, "bob", { host: "ann", act: "water", bed: 1 }, now);
  server.campGuest(nk, "cid", { host: "ann", act: "hammer" }, now + 5);
  const claimed = plain(server.campGuestsClaim(nk, "ann")).guests;
  assert.deepStrictEqual(claimed.map((a) => [a.fromName, a.act, a.bed]), [["bob", "water", 1], ["cid", "hammer", -1]]);
  assert.deepStrictEqual(plain(server.campGuestsClaim(nk, "ann")).guests, []);
});
