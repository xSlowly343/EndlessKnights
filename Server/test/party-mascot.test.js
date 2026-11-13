const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.PartyGrowthServerRules.mascot;
const now = Date.UTC(2026, 10, 14, 10);
const day = 86400000;
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

function mascotExperience(level) {
  let total = 0;
  for (let l = 1; l < level; l++)
    total += server.partyMascotNeed(l);
  return total;
}

test("mascot rules come from party.json: 12 beasts, 6 stages, 4 foods", () => {
  assert.strictEqual(rules.beasts.length, 12);
  assert.strictEqual(rules.stages.length, 6);
  assert.deepStrictEqual(plain(rules.foods.map((f) => f.id)), ["fish", "herb", "ore", "dish"]);
  assert.ok(rules.beasts.every((beast) => beast.branches.length === 2));
  assert.strictEqual(server.partyMascotStage(1, 0), "cub");
  assert.strictEqual(server.partyMascotStage(70, 0), "leader");
  assert.strictEqual(server.partyMascotStage(80, 10), "ancient");
  assert.strictEqual(server.partyMascotStage(80, 0), "leader");
});

test("the leader chooses the mascot on party level 5, a new beast only after 30 days and with half the experience", () => {
  const { nk, call, partyId } = party("ann", "bob");
  assert.throws(() => server.partyMascotChoose(nk, "ann", "wolf", now), /not open yet/);
  reachLevel(nk, partyId, 5);
  assert.throws(() => server.partyMascotChoose(nk, "bob", "wolf", now), /only the leader/);
  assert.throws(() => server.partyMascotChoose(nk, "ann", "dragon", now), /bad request/);
  let view = server.partyMascotChoose(nk, "ann", "wolf", now);
  assert.strictEqual(view.beast, "wolf");
  assert.strictEqual(view.level, 1);
  assert.strictEqual(view.stage, "cub");
  assert.ok(rules.tempers.indexOf(view.temper) >= 0, "the beast gets a temper");
  server.partyMascotChange(nk, partyId, (value) => {
    value.experience = 1000;
  });
  assert.throws(() => server.partyMascotChoose(nk, "ann", "bear", now + day), /once in 30 days/);
  view = server.partyMascotChoose(nk, "ann", "bear", now + 30 * day);
  assert.strictEqual(view.beast, "bear");
  assert.strictEqual(view.experience, 500);
  assert.strictEqual(call("bob", "partyMascotRpc").beast, "bear");
});

test("every member feeds the mascot up to 20 portions a day, the favourite food gives double", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 5);
  assert.throws(() => server.partyMascotFeed(nk, "ann", { food: "fish", amount: 1 }, now), /choose a mascot/);
  server.partyMascotChoose(nk, "ann", "wolf", now);
  let view = server.partyMascotFeed(nk, "ann", { food: "fish", amount: 3 }, now);
  assert.strictEqual(view.accepted.portions, 3);
  assert.strictEqual(view.accepted.favourite, true);
  assert.strictEqual(view.experience, 3 * 5 * 2);
  view = server.partyMascotFeed(nk, "ann", { food: "herb", amount: 30 }, now);
  assert.strictEqual(view.accepted.portions, 17, "the day limit cuts the rest");
  assert.strictEqual(view.feedLeft, 0);
  assert.throws(() => server.partyMascotFeed(nk, "ann", { food: "herb", amount: 1 }, now), /eaten enough/);
  view = server.partyMascotFeed(nk, "bob", { food: "ore", amount: 2 }, now);
  assert.strictEqual(view.accepted.portions, 2, "each member has their own limit");
  assert.strictEqual(server.partyMascotFeed(nk, "ann", { food: "dish", amount: 1 }, now + day).accepted.portions, 1, "a new day, a new limit");
  assert.throws(() => server.partyMascotFeed(nk, "ann", { food: "cake", amount: 1 }, now), /bad request/);
  assert.strictEqual(view.yourBond, 2 * rules.bond.feed);
});

test("petting raises the bond three times a day, hearts count every hundred", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 5);
  server.partyMascotChoose(nk, "ann", "deer", now);
  server.partyMascotPet(nk, "bob", now);
  server.partyMascotPet(nk, "bob", now);
  let view = server.partyMascotPet(nk, "bob", now);
  assert.strictEqual(view.yourBond, 30);
  assert.strictEqual(view.petsLeft, 0);
  assert.throws(() => server.partyMascotPet(nk, "bob", now), /wants to rest/);
  server.partyMascotChange(nk, partyId, (value) => {
    value.bonds.bob = 350;
  });
  view = server.partyMascotMine(nk, "bob", now);
  assert.strictEqual(view.yourHearts, 3);
  assert.deepStrictEqual(plain(view.bonds.map((b) => [b.name, b.hearts])), [["ann", 0], ["bob", 3]]);
});

test("battle minutes of any member feed the mascot and the bond", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 5);
  server.partyMascotChoose(nk, "ann", "boar", now);
  server.partyGrowthReport(nk, "bob", 900, now);
  const view = server.partyMascotMine(nk, "ann", now);
  assert.strictEqual(view.experience, 15);
  assert.strictEqual(view.bonds.filter((b) => b.userId === "bob")[0].bond, Math.floor(15 * rules.bond.perMinute));
});

test("from level 70 the party votes for one of two branches, more than half decides", () => {
  const { nk, partyId } = party("ann", "bob", "cid");
  reachLevel(nk, partyId, 5);
  server.partyMascotChoose(nk, "ann", "bear", now);
  assert.throws(() => server.partyMascotBranch(nk, "ann", "stone", now), /opens on level 70/);
  server.partyMascotChange(nk, partyId, (value) => {
    value.experience = mascotExperience(70);
  });
  assert.strictEqual(server.partyMascotMine(nk, "ann", now).branchOpen, true);
  assert.throws(() => server.partyMascotBranch(nk, "ann", "fire", now), /bad request/);
  let view = server.partyMascotBranch(nk, "ann", "stone", now);
  assert.strictEqual(view.branch, "");
  view = server.partyMascotBranch(nk, "bob", "honey", now);
  assert.strictEqual(view.branch, "");
  view = server.partyMascotBranch(nk, "cid", "stone", now);
  assert.strictEqual(view.branch, "stone");
  assert.throws(() => server.partyMascotBranch(nk, "bob", "honey", now), /already chosen/);
});

test("coats and gear open with the party, trials, colossus and the mascot level; the leader dresses the mascot", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 5);
  server.partyMascotChoose(nk, "ann", "owl", now);
  let view = server.partyMascotMine(nk, "ann", now);
  assert.deepStrictEqual(plain(view.coats), ["common"]);
  assert.deepStrictEqual(plain(view.gearOwned), []);
  assert.throws(() => server.partyMascotLook(nk, "ann", { coat: "rift" }, now), /not open yet/);
  server.partyMascotChange(nk, partyId, (value) => {
    value.experience = mascotExperience(25);
  });
  server.partyColossusChange(nk, partyId, (value) => {
    value.kills = 10;
    value.best = 10;
  });
  server.partyTrialChange(nk, partyId, (value) => {
    value.best = { choir: 10 };
  });
  reachLevel(nk, partyId, 40);
  view = server.partyMascotMine(nk, "ann", now);
  assert.deepStrictEqual(plain(view.coats), ["common", "banner", "shining", "rift"]);
  assert.deepStrictEqual(plain(view.gearOwned), ["blanket", "crest_collar", "colossus_fang"]);
  assert.throws(() => server.partyMascotLook(nk, "bob", { coat: "rift" }, now), /only the leader/);
  assert.throws(() => server.partyMascotLook(nk, "ann", { gear: { head: "blanket" } }, now), /not open yet/);
  view = server.partyMascotLook(nk, "ann", { coat: "rift", gear: { back: "blanket", trophy: "colossus_fang" } }, now);
  assert.strictEqual(view.coat, "rift");
  assert.deepStrictEqual(plain(view.gear), { back: "blanket", trophy: "colossus_fang" });
  view = server.partyMascotLook(nk, "ann", { gear: { back: "" } }, now);
  assert.deepStrictEqual(plain(view.gear), { trophy: "colossus_fang" });
});

test("the party tree feeds more, faster and pricier, and Ancient comes ten levels early", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 5);
  server.partyMascotChoose(nk, "ann", "wolf", now);
  server.partyTreasuryChange(nk, partyId, (value) => {
    value.levels = { "5": 2, "16": 3, "45": 5 };
  });
  let view = server.partyMascotFeed(nk, "ann", { food: "herb", amount: 21 }, now);
  assert.strictEqual(view.accepted.portions, 21, "Fed Beast gives one more portion a day");
  const expected = 21 * 3 * (1 + 0.2) * (1 + 0.1);
  assert.ok(Math.abs(view.accepted.experience - expected) < 1e-9, "Fed Beast A is doubled on level 3, Warm Beast A on level 2");
  server.partyMascotChange(nk, partyId, (value) => {
    value.experience = mascotExperience(80);
  });
  assert.strictEqual(server.partyMascotMine(nk, "ann", now).stage, "ancient");
  server.partyTreasuryChange(nk, partyId, (value) => {
    value.levels = { "50": 1 };
  });
  view = server.partyMascotFeed(nk, "bob", { food: "fish", amount: 1 }, now);
  assert.ok(Math.abs(view.accepted.experience - 10 / 2) < 1e-9, "Beast Soul makes feeding twice as dear");
});
