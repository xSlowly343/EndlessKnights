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
    value.trialsDone = rules.trials.map((trial) => trial.id);
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
    value.trialsDone = rules.trials.map((trial) => trial.id);
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
    value.trialsDone = rules.trials.map((trial) => trial.id);
  });
  assert.strictEqual(server.partyMascotMine(nk, "ann", now).stage, "ancient");
  server.partyTreasuryChange(nk, partyId, (value) => {
    value.levels = { "50": 1 };
  });
  view = server.partyMascotFeed(nk, "bob", { food: "fish", amount: 1 }, now);
  assert.ok(Math.abs(view.accepted.experience - 10 / 2) < 1e-9, "Beast Soul makes feeding twice as dear");
});

function allTrials(nk, partyId, experience) {
  server.partyMascotChange(nk, partyId, (value) => {
    value.experience = experience;
    value.trialsDone = rules.trials.map((trial) => trial.id);
  });
}

test("a stage waits for its beast trial: the level stops at the border until the trial is met", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 5);
  server.partyMascotChoose(nk, "ann", "wolf", now);
  server.partyMascotChange(nk, partyId, (value) => {
    value.experience = mascotExperience(20);
  });
  let view = server.partyMascotMine(nk, "ann", now);
  assert.strictEqual(view.level, 14, "Youngling waits for ten pets");
  assert.strictEqual(view.held, "pets");
  assert.strictEqual(view.trials[0].count, 0);
  for (let i = 0; i < 3; i++)
    server.partyMascotPet(nk, "ann", now + i * day);
  for (let i = 0; i < 3; i++)
    server.partyMascotPet(nk, "bob", now + i * day);
  for (let i = 3; i < 7; i++)
    server.partyMascotPet(nk, "ann", now + i * day);
  view = server.partyMascotMine(nk, "ann", now + 7 * day);
  assert.strictEqual(view.trials[0].done, true);
  assert.strictEqual(view.level, 20);
  assert.strictEqual(view.stage, "youngling");
  server.partyMascotChange(nk, partyId, (value) => {
    value.experience = mascotExperience(35);
  });
  assert.strictEqual(server.partyMascotMine(nk, "ann", now).held, "favourite_days");
});

test("favourite food seven days in a row, bosses together and colossus kills open the next stages", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 5);
  server.partyMascotChoose(nk, "ann", "wolf", now);
  server.partyMascotChange(nk, partyId, (value) => {
    value.trialsDone = ["pets"];
  });
  for (let i = 0; i < 7; i++)
    server.partyMascotFeed(nk, "ann", { food: "fish", amount: 1 }, now + i * day);
  let view = server.partyMascotMine(nk, "ann", now + 7 * day);
  assert.strictEqual(view.trials[1].count, 7);
  server.partyGrowthBattle(nk, partyId, 40, 10);
  view = server.partyMascotMine(nk, "ann", now);
  assert.strictEqual(view.trials[2].count, 50);
});

test("rites cost a point per five levels and friendship from the treasury, reset once a week with a refund", () => {
  const { nk, call, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 5);
  server.partyMascotChoose(nk, "ann", "wolf", now);
  allTrials(nk, partyId, mascotExperience(10));
  server.partyTreasuryChange(nk, partyId, (value) => {
    value.treasury = 1000;
  });
  assert.throws(() => server.partyMascotRite(nk, "bob", "power_aura", now), /only the leader/);
  assert.throws(() => server.partyMascotRite(nk, "ann", "nope", now), /bad request/);
  let view = server.partyMascotRite(nk, "ann", "power_aura", now);
  assert.strictEqual(view.rites.power_aura, 1);
  assert.strictEqual(view.ritePoints, 1);
  assert.strictEqual(server.partyTreasuryRead(nk, partyId).value.treasury, 900);
  view = server.partyMascotRite(nk, "ann", "power_aura", now);
  assert.strictEqual(server.partyTreasuryRead(nk, partyId).value.treasury, 700, "the second level costs 200");
  assert.throws(() => server.partyMascotRite(nk, "ann", "home_bond", now), /no rite points/);
  view = call("ann", "partyMascotRiteResetRpc");
  assert.deepStrictEqual(plain(view.rites), {});
  assert.strictEqual(server.partyTreasuryRead(nk, partyId).value.treasury, 1000);
  server.partyMascotRite(nk, "ann", "home_bond", now);
  assert.throws(() => server.partyMascotRiteReset(nk, "ann", now + day), /once a week/);
});

test("the tree adds a rite point, a rite level and makes rites cheaper", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 5);
  server.partyMascotChoose(nk, "ann", "wolf", now);
  allTrials(nk, partyId, mascotExperience(20));
  server.partyTreasuryChange(nk, partyId, (value) => {
    value.treasury = 10000;
    value.levels = { "25": 1, "39": 5, "45": 1 };
  });
  const view = server.partyMascotMine(nk, "ann", now);
  assert.strictEqual(view.ritePoints, 5);
  assert.strictEqual(view.riteMaxRank, 4);
  server.partyMascotRite(nk, "ann", "power_aura", now);
  assert.strictEqual(server.partyTreasuryRead(nk, partyId).value.treasury, 10000 - 95);
});

test("actions of every member fill one call gauge, a full gauge calls the mascot for everyone once", () => {
  const { nk, call, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 5);
  server.partyMascotChoose(nk, "ann", "wolf", now);
  let view = server.partyMascotActions(nk, "ann", 1000, now);
  assert.strictEqual(view.gauge, 1000);
  view = server.partyMascotActions(nk, "bob", 1000, now);
  assert.strictEqual(view.accepted.calls, 1);
  assert.strictEqual(view.gauge, 500);
  assert.strictEqual(view.callPending, true);
  view = server.partyMascotActions(nk, "ann", 5000, now + 10000);
  assert.strictEqual(view.accepted.counted, 10 * rules.call.maxActionsPerSecond, "actions are capped by time");
  assert.strictEqual(call("ann", "partyMascotCallTakeRpc").accepted.call, true);
  assert.strictEqual(server.partyMascotCallTake(nk, "ann", now).accepted.call, false, "a call is taken once");
  assert.strictEqual(server.partyMascotMine(nk, "bob", now).callPending, true, "a member who was away gets it later");
});

test("the hungry beast gives double for the first portion of the day, the weekly favourite gives triple", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 5);
  server.partyMascotChoose(nk, "ann", "wolf", now);
  server.partyTreasuryChange(nk, partyId, (value) => {
    value.levels = { "5": 5, "16": 5 };
  });
  const favourite = server.partyMascotMine(nk, "ann", now).favourite;
  const food = rules.foods.filter((f) => f.id === favourite)[0];
  const other = rules.foods.filter((f) => f.id !== favourite)[0];
  const feed = 1 + 0.1 * 3;
  const grow = 1 + 0.05 * 3;
  let view = server.partyMascotFeed(nk, "ann", { food: other.id, amount: 1 }, now);
  assert.ok(Math.abs(view.accepted.experience - 2 * other.experience * feed * grow) < 1e-9, "hunger doubles the first portion");
  view = server.partyMascotFeed(nk, "ann", { food: favourite, amount: 1 }, now);
  assert.ok(Math.abs(view.accepted.experience - food.experience * 4 * feed * grow) < 1e-9, "favourite x2 and Fed Beast +2: x4");
});

test("the mascot hits the colossus once an hour, even when the party sleeps", () => {
  const { nk, partyId } = party("ann", "bob");
  reachLevel(nk, partyId, 10);
  server.partyMascotChoose(nk, "ann", "wolf", now);
  allTrials(nk, partyId, mascotExperience(100));
  server.PartyGrowthServerRules.colossus.mandatory = [{ upTo: 0, min: 0, max: 0 }];
  let view = server.partyColossusMine(nk, "ann", now);
  server.partyColossusReport(nk, "ann", { direct: [0, 0, 0, 0] }, now);
  view = server.partyColossusMine(nk, "ann", now + 5 * 3600000);
  const full = server.partyColossusBaseHp(1);
  assert.ok(Math.abs(view.hp - (full - 5 * 0.001 * full)) < 1e-6, "five hours, five hits of 0.1% at level 100");
});
