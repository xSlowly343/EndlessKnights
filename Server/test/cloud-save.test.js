const test = require("node:test");
const assert = require("node:assert");
const fs = require("fs");
const path = require("path");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const plain = (o) => JSON.parse(JSON.stringify(o));
const now = Date.UTC(2026, 10, 13, 12);
const hour = 3600000;
const vector = fs.readFileSync(path.join(__dirname, "..", "test-vectors", "cloud-save.json"), "utf8");
const riftVector = fs.readFileSync(path.join(__dirname, "..", "test-vectors", "cloud-save-rift.json"), "utf8");

function edit(change) {
  const root = JSON.parse(vector);
  change(root.payload.state);
  return JSON.stringify(root);
}

function world() {
  const nk = new FakeNakama();
  nk.addUser("ann", "ann", "76561198000000001", Math.floor((now - 30 * 86400000) / 1000));
  return nk;
}

const put = (nk, save, at, force) => plain(server.cloudPut(nk, logger, "ann", { save: save, force: force }, at));

test("the server reads what the game writes: record, play time, the bag, dishes, potions and finds", () => {
  const facts = plain(server.cloudFacts(vector));
  assert.strictEqual(facts.record, 321);
  assert.strictEqual(facts.playSeconds, 7200);
  assert.strictEqual(facts.bag.herb_windwort, 40);
  assert.strictEqual(facts.bag.ore_hills, 12);
  assert.strictEqual(facts.bag["dish:carp_pie"], 1);
  assert.strictEqual(facts.bag["dish:scout_ration"], 1);
  assert.strictEqual(Object.keys(facts.bag).filter((item) => item.startsWith("potion:") && item.endsWith(":fine")).length, 1);
  for (const id of ["fish_hills_roach", "knockbloom", "carp_pie", "find_old_coin"])
    assert.ok(facts.found.includes(id), id + " is found");
  assert.throws(() => server.cloudFacts("not json"), /not readable/);
});

test("a save goes to the cloud, comes back, keeps backups, and its bag is what can be spent", () => {
  const nk = world();
  assert.deepStrictEqual(plain(server.cloudGet(nk, "ann")), { save: null, server: null });
  const first = put(nk, vector, now);
  assert.strictEqual(first.status, "saved");
  assert.strictEqual(first.server.record, 321);
  assert.strictEqual(plain(server.cloudGet(nk, "ann")).save, vector);
  assert.strictEqual(server.inflowRead(nk, "ann").value.bag.herb_windwort, 40);
  const later = edit((state) => { state.inputStats.playSeconds = 7200 + 1800; state.stage.record = 400; });
  assert.strictEqual(put(nk, later, now + hour).status, "saved");
  assert.strictEqual(plain(server.cloudGet(nk, "ann", Math.floor((now + hour) / 1000) % server.CloudServerRules.backups)).save, vector, "the previous save is a backup");
  assert.strictEqual(plain(server.cloudGet(nk, "ann")).server.record, 400);
});

test("an older save does not overwrite a newer one unless the player says so", () => {
  const nk = world();
  put(nk, edit((state) => { state.inputStats.playSeconds = 20000; }), now);
  const older = put(nk, vector, now + hour);
  assert.strictEqual(older.status, "older");
  assert.strictEqual(older.server.playSeconds, 20000);
  assert.strictEqual(put(nk, vector, now + hour, true).status, "saved", "restoring an older save on purpose");
});

test("a doubtful save is kept but the server goes on trusting the last honest one", () => {
  const nk = world();
  put(nk, vector, now);
  const rich = edit((state) => { state.camp.bag.find((entry) => entry.item === "herb_windwort").count = 99999; state.inputStats.playSeconds = 7300; });
  const doubtful = put(nk, rich, now + 60000);
  assert.strictEqual(doubtful.status, "doubtful");
  assert.match(doubtful.server.reason, /herb_ than could be gathered/);
  assert.strictEqual(plain(server.cloudGet(nk, "ann")).save, rich, "the player's data is not lost");
  assert.strictEqual(server.inflowRead(nk, "ann").value.bag.herb_windwort, 40, "but spending still goes by the honest save");
  const fast = edit((state) => { state.inputStats.playSeconds = 7200 + 10 * 3600; });
  assert.match(put(nk, fast, now + 2 * hour).server.reason, /more play time than real time/);
  const leap = edit((state) => { state.inputStats.playSeconds = 7200 + 600; state.stage.record = 50000; });
  assert.match(put(nk, leap, now + 3 * hour, true).server.reason, /record grew too fast/);
});

test("too big or broken saves are refused", () => {
  const nk = world();
  assert.throws(() => put(nk, "x".repeat(server.CloudServerRules.maxBytes + 1), now), /too big/);
  assert.throws(() => put(nk, "{}", now), /not readable/);
  assert.throws(() => server.cloudPut(nk, logger, "ann", {}, now), /bad request/);
});

test("inside the Rift the server reads the shared record and peaceful bag, not the Rift hero", () => {
  const root = JSON.parse(riftVector);
  assert.strictEqual(root.payload.state.rift.inside, true, "the vector is made inside the Rift");
  assert.ok(root.payload.state.rift.otherHero, "the home hero waits in rift.otherHero");
  const inside = plain(server.cloudFacts(riftVector));
  const home = plain(server.cloudFacts(vector));
  assert.strictEqual(inside.record, 321);
  assert.strictEqual(inside.playSeconds, home.playSeconds);
  assert.deepStrictEqual(inside.bag, home.bag, "the peaceful bag is the same inside and at home");
  assert.deepStrictEqual(inside.found.slice().sort(), home.found.slice().sort());
  assert.ok(riftVector.length < server.CloudServerRules.maxBytes);
});

test("going in and out of the Rift does not make the bag grow on the server", () => {
  const nk = world();
  assert.strictEqual(put(nk, vector, now).status, "saved");
  const later = (text, seconds) => { const root = JSON.parse(text); root.payload.state.inputStats.playSeconds = 7200 + seconds; return JSON.stringify(root); };
  for (let i = 1; i <= 3; i++) {
    const inside = put(nk, later(riftVector, i * 1200), now + i * hour);
    assert.strictEqual(inside.status, "saved", "inside, round " + i);
    assert.strictEqual(inside.server.record, 321);
    const back = put(nk, later(vector, i * 1200 + 600), now + i * hour + hour / 2);
    assert.strictEqual(back.status, "saved", "back home, round " + i);
  }
  const ledger = server.inflowRead(nk, "ann").value;
  assert.strictEqual(ledger.bag.herb_windwort, 40);
  assert.strictEqual(ledger.bag.ore_hills, 12);
});
