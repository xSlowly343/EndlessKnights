const test = require("node:test");
const assert = require("node:assert");
const { loadModule, readVectors } = require("./load");
const fs = require("fs");
const path = require("path");
const cloudSave = fs.readFileSync(path.join(__dirname, "..", "test-vectors", "cloud-save.json"), "utf8");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const plain = (o) => JSON.parse(JSON.stringify(o));
const now = Date.UTC(2026, 10, 13, 12);

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  return nk;
}

test("the first on the server to find something is the discoverer, named only with consent", () => {
  const nk = world("ann", "bob");
  const [fish, dish, collection] = ["fish_hills_roach", "carp_pie", "fish_hills"];
  for (const user of ["ann", "bob"])
    assert.strictEqual(plain(server.cloudPut(nk, logger, user, { save: cloudSave }, now)).status, "saved");
  assert.throws(() => server.museumFound(nk, "ann", ["unicorn"], now), /bad request/);
  assert.throws(() => server.museumFound(nk, "ann", [], now), /bad request/);
  assert.throws(() => server.museumFound(nk, "ann", [collection], now), /not found by the last cloud save/, "the hills are not complete in the save");
  const first = plain(server.museumFound(nk, "ann", [fish, dish], now));
  assert.deepStrictEqual(first.won, [fish, dish]);
  const second = plain(server.museumFound(nk, "bob", [fish], now + 1000));
  assert.deepStrictEqual(second.won, [], "the second finder is not first");
  assert.deepStrictEqual(second.reported, [fish]);
  const plaque = second.firsts.find((entry) => entry.id === fish);
  assert.strictEqual(plaque.name, "", "no name without consent");
  assert.strictEqual(plaque.mine, false);
  server.consentSet(nk, logger, "ann", "museum", true, now);
  const shown = plain(server.museumFirsts(nk, "ann"));
  assert.strictEqual(shown.find((entry) => entry.id === fish).name, "ann");
  assert.strictEqual(shown.find((entry) => entry.id === fish).mine, true);
});

test("the street shows friends, the clan and the party who are in the town now", () => {
  const nk = world("ann", "bob", "cid", "eve");
  nk.addFriends("ann", "bob");
  nk.addFriends("ann", "cid");
  server.townStreet(nk, "bob", true, "hideout", now);
  server.townStreet(nk, "cid", true, "town", now - server.TownServerRules.streetSeconds * 1000 - 1);
  server.townStreet(nk, "eve", true, "town", now);
  assert.deepStrictEqual(plain(server.townStreet(nk, "ann", true, "town", now)).walkers.map((w) => [w.name, w.place, w.ties.join()]), [["bob", "hideout", "friend"]], "cid left long ago, eve is a stranger");
  server.townStreet(nk, "bob", false, null, now + 1);
  assert.deepStrictEqual(plain(server.townStreet(nk, "ann", true, "town", now + 2)).walkers, []);
  assert.throws(() => server.townStreet(nk, "ann", "yes", "town", now), /bad request/);
});
