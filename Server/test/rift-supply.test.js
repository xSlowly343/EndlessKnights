const test = require("node:test");
const assert = require("node:assert");
const { loadModule, stockBag } = require("./load");
const { FakeNakama } = require("./fake-nakama");

const server = loadModule();
const plain = (o) => JSON.parse(JSON.stringify(o));
const base = Date.UTC(2026, 10, 9, 12);
const open = server.riftNext(server.RiftServerRules, base).startUnixMs + 60000;
const closed = server.riftNext(server.RiftServerRules, base).startUnixMs - 60000;

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  stockBag(server, nk, names);
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, { info() {}, warn() {}, error() {}, debug() {} }, nk, request === undefined ? "" : JSON.stringify(request)));
  const created = call(names[0], "partyCreateRpc", {});
  for (const name of names.slice(1, 3))
    call(name, "partyJoinRpc", { code: created.code });
  return nk;
}

test("a peaceful player sends one supply to a fighter of the party during the rift window", () => {
  const nk = world("ann", "bob", "cid", "eve");
  const dish = "dish:" + server.HideoutServerRules.dishes[0];
  assert.throws(() => server.riftSupplySend(nk, "ann", { to: "bob", item: dish }, closed), /rift is closed/);
  assert.throws(() => server.riftSupplySend(nk, "ann", { to: "bob", item: "dish:mud_pie" }, open), /bad request/);
  assert.throws(() => server.riftSupplySend(nk, "ann", { to: "ann", item: dish }, open), /not to yourself/);
  assert.throws(() => server.riftSupplySend(nk, "ann", { to: "eve", item: dish }, open), /only to the party and the clan/);
  const sent = plain(server.riftSupplySend(nk, "ann", { to: "bob", item: dish }, open));
  assert.strictEqual(sent.sentThisWindow, 1);
  assert.ok(sent.friendship > 0, "the cook earns friendship");
  const mine = plain(server.riftSupplyMine(nk, "bob", open));
  assert.strictEqual(mine.supply.item, dish);
  assert.strictEqual(mine.supply.fromName, "ann");
  assert.throws(() => server.riftSupplySend(nk, "cid", { to: "bob", item: "potion:" + server.HideoutServerRules.recipes[0] + ":fine" }, open), /already supplied this window/);
  assert.strictEqual(plain(server.riftSupplyMine(nk, "bob", closed)).supply, null, "outside the window there is no supply");
});

test("a sender has a few supplies per window, and a new window opens them again", () => {
  const nk = world("ann", "bob", "cid");
  const dish = "dish:" + server.HideoutServerRules.dishes[0];
  const window = server.riftWindowAt(server.RiftServerRules, open).id;
  nk.storageWrite([{ collection: "rift_supply", key: "sent", userId: "ann", value: { window: window, count: server.HideoutServerRules.supplySends }, version: "*", permissionRead: 1, permissionWrite: 0 }]);
  assert.throws(() => server.riftSupplySend(nk, "ann", { to: "bob", item: dish }, open), /no more supplies this window/);
  const later = server.riftNext(server.RiftServerRules, open + 86400000).startUnixMs + 60000;
  assert.strictEqual(plain(server.riftSupplySend(nk, "ann", { to: "bob", item: dish }, later)).sentThisWindow, 1);
});

test("the rear board lists the party first, then those still waiting for a supply", () => {
  const nk = world("ann", "bob", "cid");
  nk.addUser("dan", "dan", "76561198000000009");
  server.clanCreateFor(nk, "ann", { name: "Северный ветер", tag: "NW", language: "ru" }, open);
  server.clanAdd(nk, server.clanGroupOfUser(nk, "ann", open), "dan", open);
  const dish = "dish:" + server.HideoutServerRules.dishes[0];
  server.riftSupplySend(nk, "ann", { to: "bob", item: dish }, open);
  const rear = plain(server.riftRear(nk, "ann", open));
  assert.strictEqual(rear.sentThisWindow, 1);
  assert.strictEqual(rear.sendsPerWindow, server.HideoutServerRules.supplySends);
  assert.deepStrictEqual(rear.fighters.map((f) => [f.name, f.party, f.supplied]), [["cid", true, false], ["bob", true, true], ["dan", false, false]]);
  assert.strictEqual(plain(server.riftRear(nk, "bob", open)).mine.item, dish);
  const shut = plain(server.riftRear(nk, "ann", closed));
  assert.strictEqual(shut.window, 0);
  assert.ok(shut.fighters.every((f) => !f.supplied));
});

test("supplies to one's own party count for the chronicle feat and leave a record", () => {
  const nk = world("ann", "bob", "cid");
  const dish = "dish:" + server.HideoutServerRules.dishes[0];
  server.riftSupplySend(nk, "ann", { to: "bob", item: dish }, open);
  const group = server.partyGroupOfUser(nk, "ann");
  const value = server.chronicleRead(nk, group.id).value;
  assert.strictEqual(value.stats.suppliesTogether, 1);
  assert.strictEqual(value.records[value.records.length - 1].kind, "supply");
  assert.strictEqual(value.records[value.records.length - 1].name, "ann");
  const feat = server.ChronicleServerRules.feats.find((entry) => entry.id === "feeders");
  server.chronicleUpdate(nk, group.id, (chronicle) => {
    const delta = server.chronicleNoStats();
    delta.suppliesTogether = feat.levels[0] - 1;
    server.chronicleAdd(chronicle, delta, open, server.ChronicleServerRules);
  });
  assert.strictEqual(server.chronicleRead(nk, group.id).value.feats.feeders.level, 1, "five supplies make the party feeders");
});

test("the town board names its feeder, logger and crafter of the round, only with consent", () => {
  const nk = world("ann", "bob", "cid");
  const build = server.TownBuildServerRules;
  const logger = { info() {}, warn() {}, error() {}, debug() {} };
  server.townBuildState(nk, logger, "ann", open);
  const raw = build.resources.find((entry) => entry.kind === "raw").id;
  const craft = build.resources.find((entry) => entry.kind === "craft").id;
  server.consentSet(nk, logger, "cid", "town_top", true, open);
  server.townBuildAdd(nk, logger, "bob", { [raw]: 10 }, open);
  server.townBuildAdd(nk, logger, "cid", { [craft]: 2 }, open);
  server.riftSupplySend(nk, "cid", { to: "bob", item: "dish:" + server.HideoutServerRules.dishes[0] }, open);
  const roles = plain(server.townBuildState(nk, logger, "ann", open)).roles;
  assert.deepStrictEqual(roles, [{ role: "logger", name: "", amount: 10 }, { role: "feeder", name: "cid", amount: 1 }, { role: "crafter", name: "cid", amount: 2 }]);
});
