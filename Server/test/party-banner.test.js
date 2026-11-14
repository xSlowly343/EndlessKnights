const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const plain = (o) => JSON.parse(JSON.stringify(o));

function party(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  const view = call(names[0], "partyCreateRpc", {});
  for (const name of names.slice(1))
    call(name, "partyJoinRpc", { code: view.code });
  return { nk, call, partyId: view.id };
}

function reachLevel(nk, partyId, level) {
  let total = 0;
  for (let l = 1; l < level; l++)
    total += server.partyGrowthNeed(l);
  const have = server.partyGrowthRead(nk, partyId).value.experience;
  server.partyGrowthAdd(nk, partyId, total - have);
}

test("27 crests: tiers 10, 15, 20 of each trial and tiers 25, 50, 100 of the colossus", () => {
  const all = plain(server.partyBannerAll());
  assert.strictEqual(all.length, 27);
  assert.ok(all.includes("choir_10") && all.includes("marathon_20") && all.includes("colossus_100"));
});

test("the banner opens on party level 40 with one place, more places on 50, 65, 75, 85 and 100", () => {
  const { nk, call, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 39);
  assert.strictEqual(call("ann", "partyBannerRpc").opened, false);
  assert.strictEqual(server.partyBannerSlots(40), 1);
  assert.strictEqual(server.partyBannerSlots(64), 2);
  assert.strictEqual(server.partyBannerSlots(100), 6);
  reachLevel(nk, partyId, 50);
  const view = call("bob", "partyBannerRpc");
  assert.strictEqual(view.opened, true);
  assert.deepStrictEqual(plain(view.slots), ["", ""]);
});

test("crests are earned by trials and the colossus, the leader places them, one crest in one place", () => {
  const { nk, call, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 50);
  assert.throws(() => server.partyBannerSet(nk, "ann", { slot: 0, crest: "choir_10" }), /not earned/);
  server.partyTrialChange(nk, partyId, (value) => {
    value.best = { choir: 15, pulse: 10 };
  });
  server.partyColossusChange(nk, partyId, (value) => {
    value.best = 25;
  });
  let view = server.partyBannerMine(nk, "bob");
  assert.deepStrictEqual(plain(view.earned), ["choir_10", "choir_15", "pulse_10", "colossus_25"]);
  assert.throws(() => server.partyBannerSet(nk, "bob", { slot: 0, crest: "choir_15" }), /only the leader/);
  assert.throws(() => server.partyBannerSet(nk, "ann", { slot: 2, crest: "choir_15" }), /not open yet/);
  assert.throws(() => server.partyBannerSet(nk, "ann", { slot: 0, crest: "dragon_10" }), /bad request/);
  server.partyBannerSet(nk, "ann", { slot: 0, crest: "choir_15" });
  view = call("ann", "partyBannerSetRpc", { slot: 1, crest: "colossus_25" });
  assert.deepStrictEqual(plain(view.slots), ["choir_15", "colossus_25"]);
  view = server.partyBannerSet(nk, "ann", { slot: 1, crest: "choir_15" });
  assert.deepStrictEqual(plain(view.slots), ["", "choir_15"], "a crest moves, it is not doubled");
  view = server.partyBannerSet(nk, "ann", { slot: 1, crest: "" });
  assert.deepStrictEqual(plain(view.slots), ["", ""]);
});
