const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.HideoutServerRules;
const plain = (o) => JSON.parse(JSON.stringify(o));
const minute = 60000;
const now = 1800000000000;

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  return { nk, call };
}

function party(call, names) {
  const created = call(names[0], "partyCreateRpc", {});
  for (const name of names.slice(1))
    call(name, "partyJoinRpc", { code: created.code });
  return created;
}

const at = (nk, user, here, time) => plain(server.hideoutPresence(nk, user, here, time));

function bosses(nk, user, count) {
  const group = server.partyGroupOfUser(nk, user);
  server.chronicleUpdate(nk, group.id, (value) => { value.stats.bossesTogether = count; });
}

test("rules come from party_hideout.json with nine tiers", () => {
  assert.strictEqual(rules.tiers.length, 9);
  assert.strictEqual(rules.tiers[1].sitSeconds, 2 * 3600);
});

test("a party of one has no hideout, two open the rest stop", () => {
  const { nk, call } = world("ann", "bob");
  assert.throws(() => at(nk, "ann", true, now), /not in a party/);
  party(call, ["ann"]);
  assert.strictEqual(at(nk, "ann", true, now).tier, 0);
  call("bob", "partyJoinRpc", { code: call("ann", "partyMineRpc").party.code });
  assert.strictEqual(at(nk, "ann", true, now + 1).tier, 1);
});

test("two at the fire see each other and sit together, one alone does not", () => {
  const { nk, call } = world("ann", "bob");
  party(call, ["ann", "bob"]);
  at(nk, "ann", true, now);
  assert.strictEqual(at(nk, "ann", true, now + minute).sitSeconds, 0, "alone at the fire");
  const both = at(nk, "bob", true, now + 2 * minute);
  assert.deepStrictEqual(both.here.map((entry) => entry.name).sort(), ["ann", "bob"]);
  const later = at(nk, "ann", true, now + 3 * minute);
  assert.strictEqual(later.sitSeconds, 60);
  const gap = at(nk, "bob", true, now + 30 * minute);
  assert.ok(gap.sitSeconds <= 60 + rules.maxSitStepMs / 1000, "a long gap counts no more than one step");
  at(nk, "ann", true, now + 30.5 * minute);
  const left = at(nk, "bob", false, now + 31 * minute);
  assert.deepStrictEqual(left.here.map((entry) => entry.name), ["ann"]);
});

test("a player who stops pinging leaves the fire on their own", () => {
  const { nk, call } = world("ann", "bob");
  party(call, ["ann", "bob"]);
  at(nk, "ann", true, now);
  const view = at(nk, "bob", true, now + rules.hereMs + 1);
  assert.deepStrictEqual(view.here.map((entry) => entry.name), ["bob"]);
});

test("the yard needs bosses together and two hours by the fire", () => {
  const { nk, call } = world("ann", "bob");
  party(call, ["ann", "bob"]);
  bosses(nk, "ann", rules.tiers[1].bosses);
  assert.strictEqual(at(nk, "ann", true, now).tier, 1, "bosses alone are not enough");
  let time = now;
  at(nk, "bob", true, time);
  while (time < now + 2 * 3600 * 1000 + 2 * minute) {
    time += minute;
    at(nk, time % (2 * minute) === 0 ? "ann" : "bob", true, time);
  }
  const view = at(nk, "ann", true, time + minute);
  assert.ok(view.sitSeconds >= rules.tiers[1].sitSeconds);
  assert.strictEqual(view.tier, 2);
  bosses(nk, "ann", rules.tiers[2].bosses);
  assert.strictEqual(at(nk, "ann", true, time + 2 * minute).tier, 2, "the homestead waits for party orders and a second-level fire");
});

test("the hideout is read and pinged through the rpc", () => {
  const { call } = world("ann", "bob");
  party(call, ["ann", "bob"]);
  assert.strictEqual(call("ann", "hideoutPresenceRpc", { here: true }).here.length, 1);
  assert.strictEqual(call("bob", "hideoutViewRpc").here.length, 1);
  assert.throws(() => call("ann", "hideoutPresenceRpc", { here: "yes" }), /bad request/);
});

function towns(nk, built) {
  nk.storageWrite([{ collection: "town", key: "guilds", userId: server.PartySystemUser, value: { built: built }, version: "*", permissionRead: 0, permissionWrite: 0 }]);
}

function hours(nk, user, count) {
  const group = server.partyGroupOfUser(nk, user);
  server.chronicleUpdate(nk, group.id, (value) => { value.stats.ticksTogether = Math.round(count * server.chronicleTicksPerHour()); });
}

const herb = Object.keys(rules.materials).find((item) => rules.materials[item] === 1);
const building = (id) => rules.buildings.find((entry) => entry.id === id);
const queue = (nk, user, id) => plain(server.hideoutQueue(nk, user, id, now));
const give = (nk, user, amount) => plain(server.hideoutGive(nk, user, herb, amount, now));

function fill(nk, user, points) {
  let view = null;
  for (let left = points; left > 0; left -= rules.maxGive)
    view = give(nk, user, Math.min(rules.maxGive, left));
  return view;
}

test("the leader queues an open building and everyone builds it with materials", () => {
  const { nk, call } = world("ann", "bob");
  party(call, ["ann", "bob"]);
  const first = plain(server.hideoutPresence(nk, "ann", null, now));
  assert.strictEqual(first.tier, 1);
  assert.ok(first.canQueue.includes("bonfire"));
  assert.throws(() => queue(nk, "bob", "bonfire"), /only the leader/);
  assert.throws(() => queue(nk, "ann", "pier"), /no place for it yet/);
  assert.throws(() => give(nk, "bob", 5), /nothing is being built/);
  const queued = queue(nk, "ann", "bonfire");
  assert.strictEqual(queued.project.building, "bonfire");
  assert.strictEqual(queued.project.target, building("bonfire").levels[0].points);
  const half = give(nk, "bob", 10);
  assert.strictEqual(half.project.points, 10);
  assert.strictEqual(half.project.yours, 10);
  assert.throws(() => queue(nk, "ann", "board"), /under way/);
  const done = fill(nk, "ann", building("bonfire").levels[0].points);
  assert.strictEqual(done.buildings.bonfire, 1);
  assert.strictEqual(done.fireLevel, 1);
  assert.strictEqual(done.project, null);
  assert.throws(() => server.hideoutGive(nk, "ann", "gold_bar", 1, now), /not a building material/);
});

test("a building also waits for hours of battle together", () => {
  const { nk, call } = world("ann", "bob");
  party(call, ["ann", "bob"]);
  server.hideoutPresence(nk, "ann", null, now);
  queue(nk, "ann", "chronicle_table");
  const cost = building("chronicle_table").levels[0];
  const waiting = fill(nk, "ann", cost.points);
  assert.strictEqual(waiting.project.points, cost.points, "points are there but hours are not");
  hours(nk, "ann", cost.hours);
  assert.strictEqual(plain(server.hideoutPresence(nk, "ann", null, now)).buildings.chronicle_table, 1);
});

test("higher levels need a higher tier and guild buildings wait for the town", () => {
  const { nk, call } = world("ann", "bob");
  party(call, ["ann", "bob"]);
  server.hideoutPresence(nk, "ann", null, now);
  queue(nk, "ann", "bonfire");
  fill(nk, "ann", building("bonfire").levels[0].points);
  assert.throws(() => queue(nk, "ann", "bonfire"), /needs a higher tier/);
  const group = server.partyGroupOfUser(nk, "ann");
  server.hideoutChange(nk, group.id, (value) => { value.tier = 6; });
  assert.throws(() => queue(nk, "ann", "pier"), /guild hall is not built/);
  towns(nk, ["fishers"]);
  assert.strictEqual(queue(nk, "ann", "pier").project.building, "pier");
});

test("the homestead opens with a second-level fire and a party order, and wants are counted", () => {
  const { nk, call } = world("ann", "bob");
  party(call, ["ann", "bob"]);
  const group = server.partyGroupOfUser(nk, "ann");
  bosses(nk, "ann", rules.tiers[2].bosses);
  server.hideoutChange(nk, group.id, (value) => { value.tier = 2; value.buildings.bonfire = 2; value.sitSeconds = 99999; });
  assert.strictEqual(plain(server.hideoutPresence(nk, "ann", null, now)).tier, 2, "the party order is still missing");
  server.hideoutChange(nk, group.id, (value) => { value.orders = 1; });
  assert.strictEqual(plain(server.hideoutPresence(nk, "ann", null, now)).tier, 3);
  plain(server.hideoutWant(nk, "bob", "garden", true, now));
  const view = plain(server.hideoutWant(nk, "ann", "garden", true, now));
  assert.strictEqual(view.wants.garden, 2);
  assert.deepStrictEqual(view.mine, ["garden"]);
  assert.strictEqual(plain(server.hideoutWant(nk, "ann", "garden", false, now)).wants.garden, 1);
  assert.throws(() => server.hideoutWant(nk, "ann", "moat", true, now), /bad request/);
});

test("activity needs hold a tier until they are met", () => {
  const { nk, call } = world("ann", "bob");
  party(call, ["ann", "bob"]);
  const group = server.partyGroupOfUser(nk, "ann");
  bosses(nk, "ann", rules.tiers[3].bosses);
  server.hideoutChange(nk, group.id, (value) => { value.tier = 3; value.orders = rules.tiers[3].orders; });
  assert.strictEqual(plain(server.hideoutPresence(nk, "ann", null, now)).tier, 3, "the manor waits for harvests from the shared bed");
  server.hideoutChange(nk, group.id, (value) => { value.counters.gardenHarvests = 20; });
  assert.strictEqual(plain(server.hideoutPresence(nk, "ann", null, now)).tier, 4);
});

const fish = (nk, user, on, caught, time) => plain(server.hideoutPresence(nk, user, false, time, on, caught));

test("two who fish at the same time fish together and count their catch", () => {
  const { nk, call } = world("ann", "bob");
  party(call, ["ann", "bob"]);
  const alone = fish(nk, "ann", true, 3, now);
  assert.strictEqual(alone.fishingTogether, false);
  assert.strictEqual(alone.fishTogether, 0, "a catch alone is not counted");
  const both = fish(nk, "bob", true, 2, now + minute);
  assert.strictEqual(both.fishingTogether, true);
  assert.deepStrictEqual(both.fishers.map((entry) => entry.name).sort(), ["ann", "bob"]);
  assert.strictEqual(both.fishTogether, 2);
  const capped = fish(nk, "ann", true, 100, now + 1.5 * minute);
  assert.strictEqual(capped.fishTogether, 2 + rules.maxCaughtPerMark);
  assert.deepStrictEqual(capped.here, [], "fishing at home is not sitting by the hideout fire");
  const late = fish(nk, "ann", true, 1, now + minute + rules.hereMs + 1);
  assert.strictEqual(late.fishingTogether, false, "bob stopped pinging");
  const stop = fish(nk, "bob", false, 0, now + minute + rules.hereMs + 2);
  assert.deepStrictEqual(stop.fishers.map((entry) => entry.name), ["ann"]);
});

test("the presence rpc takes the fishing mark and checks it", () => {
  const { nk, call } = world("ann", "bob");
  party(call, ["ann", "bob"]);
  call("ann", "hideoutPresenceRpc", { here: false, fishing: true, caught: 1 });
  const view = call("bob", "hideoutPresenceRpc", { here: true, fishing: true, caught: 1 });
  assert.strictEqual(view.fishingTogether, true);
  assert.strictEqual(view.fishTogether, 1);
  assert.throws(() => call("ann", "hideoutPresenceRpc", { here: true, fishing: "yes" }), /bad request/);
  assert.throws(() => call("ann", "hideoutPresenceRpc", { here: true, fishing: true, caught: -1 }), /bad request/);
  assert.strictEqual(call("ann", "hideoutPresenceRpc", { here: true }).fishingTogether, true, "a mark without fishing keeps the old one");
});

const corner = (call, user, decor, hero, path) => call(user, "hideoutCornerRpc", { hero: hero || "Huscarl", path: path || "", decor: decor });

test("each member gets a corner while the tier has room and fills it with decor", () => {
  const { nk, call } = world("ann", "bob", "cid");
  party(call, ["ann", "bob", "cid"]);
  const view = corner(call, "ann", ["decor_lantern", "decor_bench"], "Huscarl", "berserker");
  assert.strictEqual(view.myCorner, true);
  assert.strictEqual(view.cornerDecor, rules.cornerDecor);
  assert.deepStrictEqual(plain(view.corners.map((entry) => entry.name)), ["ann", "bob"], "the rest stop has two corners");
  assert.deepStrictEqual(plain(view.corners[0].decor), ["decor_lantern", "decor_bench"]);
  assert.strictEqual(view.corners[0].path, "berserker");
  assert.strictEqual(at(nk, "cid", true, now).myCorner, false);
  assert.throws(() => corner(call, "cid", []), /no free corner/);
  assert.throws(() => corner(call, "ann", ["decor_lantern", "decor_lantern"]), /unknown decor/);
  assert.throws(() => corner(call, "ann", ["sword_of_doom"]), /unknown decor/);
  assert.throws(() => corner(call, "ann", ["decor_lantern", "decor_bench", "decor_flower_pot", "decor_fish_rack"]), /the corner is full|unknown decor/);
  assert.throws(() => call("ann", "hideoutCornerRpc", { hero: 5, decor: [] }), /bad request/);
});

test("the stronghold gives five places in a corner", () => {
  const stronghold = rules.tiers.findIndex((tier) => tier.cornerDecor > rules.cornerDecor) + 1;
  assert.strictEqual(server.hideoutCornerDecor(stronghold - 1), rules.cornerDecor);
  assert.strictEqual(server.hideoutCornerDecor(stronghold), 5);
});

test("a member who leaves empties the corner and the next one takes it", () => {
  const { nk, call } = world("ann", "bob", "cid");
  party(call, ["ann", "bob", "cid"]);
  corner(call, "bob", ["decor_bench"]);
  call("bob", "partyLeaveRpc");
  const view = at(nk, "cid", true, now);
  assert.strictEqual(view.myCorner, true);
  assert.deepStrictEqual(plain(view.corners.map((entry) => entry.name)), ["ann", "cid"]);
  assert.deepStrictEqual(plain(view.corners[1].decor), [], "the new corner starts empty");
});

test("a dissolved party with a hideout rests for thirty days and returns by the old code", () => {
  const { nk, call } = world("ann", "bob", "eve");
  const created = party(call, ["ann", "bob"]);
  bosses(nk, "ann", 12);
  corner(call, "ann", ["decor_lantern"]);
  call("ann", "partyLeaveRpc");
  call("bob", "partyLeaveRpc");
  assert.strictEqual(nk.groupsGetId([created.id]).length, 0);
  assert.throws(() => call("eve", "partyJoinRpc", { code: created.code }), /rests until a former member returns/);
  const back = call("ann", "partyJoinRpc", { code: created.code });
  assert.notStrictEqual(back.id, created.id);
  assert.strictEqual(back.code, created.code);
  const view = at(nk, "ann", true, now);
  assert.strictEqual(view.tier, 1, "the tier came back too");
  assert.strictEqual(view.bosses, 12, "the chronicle came back with the hideout");
  call("bob", "partyJoinRpc", { code: created.code });
  const both = at(nk, "bob", true, now + 1);
  assert.strictEqual(both.tier, 1);
  assert.deepStrictEqual(plain(both.corners.map((entry) => entry.decor.length)), [0, 0], "corners start empty after the rest");
});

test("after thirty days the rested hideout goes to the archive and the code is free", () => {
  const { nk, call } = world("ann", "bob");
  const created = party(call, ["ann", "bob"]);
  at(nk, "ann", true, now);
  call("ann", "partyLeaveRpc");
  call("bob", "partyLeaveRpc");
  const frozen = nk.storageRead([{ collection: "party_codes", key: created.code, userId: server.PartySystemUser }])[0].value;
  assert.throws(() => server.partyThaw(nk, "ann", created.code, frozen.frozenAt + rules.frozenMs + 1), /unknown code/);
  assert.strictEqual(nk.storageRead([{ collection: "party_codes", key: created.code, userId: server.PartySystemUser }]).length, 0);
  assert.strictEqual(nk.storageRead([{ collection: "party_hideout", key: created.id, userId: server.PartySystemUser }]).length, 0);
});

test("a party that never had a hideout closes at once as before", () => {
  const { nk, call } = world("ann");
  const created = party(call, ["ann"]);
  call("ann", "partyLeaveRpc");
  assert.strictEqual(nk.storageRead([{ collection: "party_codes", key: created.code, userId: server.PartySystemUser }]).length, 0);
  assert.throws(() => call("ann", "partyJoinRpc", { code: created.code }), /unknown code/);
});


function built(nk, user, buildings) {
  const group = server.partyGroupOfUser(nk, user);
  server.hideoutChange(nk, group.id, (value) => { value.tier = Math.max(value.tier, 3); Object.assign(value.buildings, buildings); });
}

const friendship = (nk, user) => server.walletRead(nk, user).value.balances.friendship;

test("an hour by the fire together gives a sign of friendship, with a daily cap", () => {
  const { nk, call } = world("ann", "bob");
  party(call, ["ann", "bob"]);
  let time = now;
  at(nk, "ann", true, time);
  at(nk, "bob", true, time);
  const before = friendship(nk, "ann");
  for (let i = 0; i < 61; i++) {
    time += minute;
    at(nk, i % 2 === 0 ? "ann" : "bob", true, time);
  }
  assert.strictEqual(friendship(nk, "ann") - before, 1);
  assert.strictEqual(friendship(nk, "bob") - before, 1);
  for (let i = 0; i < 6 * 60; i++) {
    time += minute;
    at(nk, i % 2 === 0 ? "ann" : "bob", true, time);
  }
  assert.strictEqual(friendship(nk, "ann") - before, 4, "four signs a day at most");
});

test("the shared garden waits for the gardeners and grows from everyone's input", () => {
  const { nk, call } = world("ann", "bob", "cid");
  party(call, ["ann", "bob", "cid"]);
  const plant = rules.plants.find((entry) => entry.input === "keys" && entry.starter);
  assert.deepStrictEqual(plain(at(nk, "ann", true, now).commons.garden), [], "no garden building, no beds");
  assert.throws(() => server.hideoutPlant(nk, "ann", 0, plant.id, now), /no such bed/);
  const group = server.partyGroupOfUser(nk, "ann");
  server.hideoutChange(nk, group.id, (value) => { value.tier = 3; });
  assert.strictEqual(building("garden").guild, "gardeners", "the garden waits for the gardeners' hall like every guild building");
  assert.strictEqual(rules.startBuilt, undefined, "the herbwitch's house is built by the town, not given from the start");
  nk.storageWrite([{ collection: "town", key: "guilds", userId: server.PartySystemUser, value: { built: ["gardeners"] }, version: "*", permissionRead: 0, permissionWrite: 0 }]);
  built(nk, "ann", { garden: 1 });
  const planted = plain(server.hideoutPlant(nk, "ann", 0, plant.id, now));
  assert.strictEqual(planted.commons.garden.length, rules.bedsPerRow);
  assert.strictEqual(planted.commons.garden[0].plant, plant.id);
  assert.throws(() => server.hideoutPlant(nk, "bob", 0, plant.id, now), /the bed is taken/);
  assert.throws(() => server.hideoutHarvest(nk, "bob", 0, now), /not ready/);
  const keys = Math.ceil(plant.growSeconds / plant.secondsPerUnit / 2);
  let time = now;
  let view = null;
  for (let left = keys; left > 0; left -= rules.tendPerMark.keys) {
    time += 1000;
    view = plain(server.hideoutPresence(nk, "bob", true, time, null, 0, { keys: Math.min(left, rules.tendPerMark.keys), clicks: 0, mouse: 0, idle: 0 }));
  }
  assert.strictEqual(view.commons.garden[0].ready, false, "bob alone grows it halfway");
  assert.strictEqual(view.commons.garden[0].tended, true);
  for (let left = keys; left > 0; left -= rules.tendPerMark.keys) {
    time += 1000;
    view = plain(server.hideoutPresence(nk, "bob", true, time, null, 0, { keys: Math.min(left, rules.tendPerMark.keys), clicks: 0, mouse: 0, idle: 0 }));
  }
  assert.strictEqual(view.commons.garden[0].ready, true);
  const harvested = plain(server.hideoutHarvest(nk, "ann", 0, time));
  assert.strictEqual(harvested.commons.owed[plant.herb], plant.yield);
  assert.strictEqual(harvested.commons.garden[0].plant, "");
  const claim = plain(call("bob", "hideoutClaimRpc"));
  assert.strictEqual(claim.items[plant.herb], plant.yield, "the one who tended shares the harvest");
  assert.strictEqual(plain(call("bob", "hideoutClaimRpc")).items[plant.herb], plant.yield, "a claim comes again until it is confirmed");
  assert.strictEqual(plain(call("bob", "hideoutClaimRpc", { ack: claim.seq - 1 })).seq, claim.seq, "a stale confirmation changes nothing");
  assert.deepStrictEqual(plain(call("bob", "hideoutClaimRpc", { ack: claim.seq }).items), {}, "a confirmed claim is taken once");
  assert.deepStrictEqual(plain(call("cid", "hideoutClaimRpc").items), {}, "who did not tend gets nothing");
  assert.strictEqual(server.hideoutRead(nk, group.id).value.counters.gardenHarvests, 1);
});

test("the garden grows faster when two sit by the fire, and a second row adds beds", () => {
  const { nk, call } = world("ann", "bob");
  party(call, ["ann", "bob"]);
  built(nk, "ann", { garden: 1, garden_row: 1 });
  const plant = rules.plants.find((entry) => entry.input === "idle" && entry.starter);
  server.hideoutPlant(nk, "ann", 5, plant.id, now);
  at(nk, "ann", true, now);
  at(nk, "bob", true, now);
  const view = plain(server.hideoutPresence(nk, "ann", true, now + 1000, null, 0, { keys: 0, clicks: 0, mouse: 0, idle: 100 }));
  assert.strictEqual(view.commons.together, true);
  assert.strictEqual(view.commons.garden.length, 2 * rules.bedsPerRow);
  const grown = view.commons.garden[5].share * plant.growSeconds;
  assert.ok(Math.abs(grown - (1 + 100 * plant.secondsPerUnit) * (1 + rules.togetherBoost)) < 1e-6, "time and tending both get the boost");
  assert.throws(() => server.hideoutPresence(nk, "ann", true, now, null, 0, server.hideoutReadTend({ keys: -1 })), /bad request/);
  assert.strictEqual(server.hideoutReadTend({ keys: 1e9 }).keys, rules.tendPerMark.keys, "a mark carries no more than the cap");
});

test("the storage takes peaceful goods only, limits what is taken out and keeps a journal", () => {
  const { nk, call } = world("ann", "bob");
  party(call, ["ann", "bob"]);
  assert.throws(() => call("ann", "hideoutStorePutRpc", { item: "herb_knockbloom", amount: 5 }), /storage is not built/);
  built(nk, "ann", { storage: 1 });
  for (const item of ["sword_of_kings", "gem_anvil", "seed_colossus", 5])
    assert.throws(() => call("ann", "hideoutStorePutRpc", { item: item, amount: 1 }), /does not go into the storage/);
  assert.throws(() => call("ann", "hideoutStorePutRpc", { item: "herb_knockbloom", amount: 0 }), /bad amount/);
  call("ann", "hideoutStorePutRpc", { item: "herb_knockbloom", amount: 30 });
  call("ann", "hideoutStorePutRpc", { item: "ember_coal", amount: 2 });
  const took = call("bob", "hideoutStoreTakeRpc", { item: "herb_knockbloom", amount: rules.takePerDay });
  assert.strictEqual(took.commons.storage.herb_knockbloom, 30 - rules.takePerDay);
  assert.strictEqual(took.commons.takenToday, rules.takePerDay);
  assert.throws(() => call("bob", "hideoutStoreTakeRpc", { item: "ember_coal", amount: 1 }), /daily take limit/);
  assert.throws(() => call("ann", "hideoutStoreTakeRpc", { item: "ember_coal", amount: 3 }), /not enough/);
  const view = call("ann", "hideoutStoreTakeRpc", { item: "ember_coal", amount: 2 });
  assert.strictEqual(view.commons.storage.ember_coal, undefined);
  assert.strictEqual(view.commons.owed.ember_coal, 2, "taken goods wait for a confirmed claim");
  const taken = plain(call("ann", "hideoutClaimRpc"));
  assert.deepStrictEqual(taken.items, { ember_coal: 2 });
  assert.deepStrictEqual(plain(call("ann", "hideoutClaimRpc", { ack: taken.seq })).hideout.commons.owed, {});
  assert.deepStrictEqual(view.commons.journal.map((entry) => entry.name + ":" + entry.item + ":" + entry.amount),
    ["ann:ember_coal:-2", "bob:herb_knockbloom:-" + rules.takePerDay, "ann:ember_coal:2", "ann:herb_knockbloom:30"]);
  const tomorrow = plain(server.hideoutStore(nk, "bob", "herb_knockbloom", 1, false, now + 2 * 86400000));
  assert.strictEqual(tomorrow.commons.takenToday, 1, "the limit resets the next day");
});

const craft = (nk, user, station, time) => plain(server.hideoutPresence(nk, user, true, time, null, 0, null, station));

test("two at the same station of a built workshop see each other and brew for each other", () => {
  const { nk, call } = world("ann", "bob", "cid");
  party(call, ["ann", "bob", "cid"]);
  const recipe = server.HideoutServerRules.recipes[0];
  craft(nk, "ann", "cauldron", now);
  assert.deepStrictEqual(craft(nk, "bob", "cauldron", now + 1).crafts.cauldron, [], "nothing to share before the cauldron is built");
  built(nk, "ann", { shared_cauldron: 1, shared_forge: 1 });
  const view = craft(nk, "bob", "cauldron", now + 2);
  assert.deepStrictEqual(view.crafts.cauldron, ["ann"]);
  assert.deepStrictEqual(craft(nk, "cid", "forge", now + 3).crafts.forge, [], "alone at the forge");
  assert.deepStrictEqual(view.crafts.forge, []);
  assert.throws(() => server.hideoutBrewed(nk, "cid", recipe, false, now + 4), /nobody brews with you/);
  assert.throws(() => server.hideoutBrewed(nk, "ann", "poison", false, now + 4), /bad request/);
  server.hideoutBrewed(nk, "ann", recipe, true, now + 4);
  const claim = plain(call("bob", "hideoutClaimRpc"));
  assert.strictEqual(claim.items["potion:" + recipe + ":fine"], 1, "the partner gets a potion of the same brew");
  assert.deepStrictEqual(plain(call("ann", "hideoutClaimRpc").items), {}, "the brewer keeps their own potion");
  assert.deepStrictEqual(plain(call("cid", "hideoutClaimRpc").items), {});
  for (let i = 1; i < server.HideoutServerRules.brewsPerDay; i++)
    server.hideoutBrewed(nk, "ann", recipe, false, now + 4 + i);
  assert.throws(() => server.hideoutBrewed(nk, "ann", recipe, false, now + 100), /daily brew limit/);
  const left = plain(server.hideoutPresence(nk, "bob", true, now + 200));
  assert.deepStrictEqual(left.crafts.cauldron, [], "a mark without a station leaves it");
  assert.throws(() => call("bob", "hideoutPresenceRpc", { here: true, craft: "anvil" }), /bad request/);
});

test("the trophy hall shows the guardians, the rift and the feats of the chronicle once it is built", () => {
  const { nk, call } = world("ann", "bob");
  party(call, ["ann", "bob"]);
  const group = server.partyGroupOfUser(nk, "ann");
  server.chronicleUpdate(nk, group.id, (value) => {
    value.stats.guardiansTogether = 4;
    value.stats.bossesTogether = 1200;
    value.stats.riftDepth = 31;
    value.feats.old_guard = { level: 2, at: now };
    value.feats.first_steps = { level: 0, at: now };
  });
  assert.strictEqual(at(nk, "ann", true, now).trophies, null, "no hall, no trophies");
  built(nk, "ann", { trophy_hall: 1 });
  const hall = at(nk, "ann", true, now + 1).trophies;
  assert.strictEqual(hall.guardians, 4);
  assert.strictEqual(hall.bosses, 1200);
  assert.strictEqual(hall.riftDepth, 31);
  assert.deepStrictEqual(plain(hall.feats), [{ id: "old_guard", level: 2 }]);
});

function yard(nk, call) {
  party(call, ["ann", "bob"]);
  bosses(nk, "ann", rules.tiers[1].bosses);
  const group = server.partyGroupOfUser(nk, "ann");
  server.hideoutChange(nk, group.id, (value) => { value.tier = 1; value.sitSeconds = rules.tiers[1].sitSeconds; });
  return group;
}

test("the yard gets a weekly party order, and filling it pays everyone and counts for the tiers", () => {
  const { nk, call } = world("ann", "bob");
  const group = yard(nk, call);
  const first = at(nk, "ann", true, now);
  assert.strictEqual(first.tier, 2);
  assert.ok(first.order && first.order.done === false && first.order.needs.length === 2);
  assert.ok(first.order.needs.every((need) => need.have === 0), "progress starts at the moment the order appears");
  assert.ok(first.order.daysLeft >= 1 && first.order.daysLeft <= rules.orderDays);
  const goal = rules.orderGoals.filter((entry) => entry.id === first.order.id)[0];
  const fill = (need) => {
    if (need.counter === "bosses")
      server.chronicleUpdate(nk, group.id, (value) => { value.stats.bossesTogether += need.target; });
    else if (need.counter === "guardians")
      server.chronicleUpdate(nk, group.id, (value) => { value.stats.guardiansTogether += need.target; });
    else if (need.counter === "sitMinutes")
      server.hideoutChange(nk, group.id, (value) => { value.sitSeconds += need.target * 60; });
    else
      server.hideoutChange(nk, group.id, (value) => { value.counters[need.counter] = (value.counters[need.counter] || 0) + need.target; });
  };
  fill(goal.needs[0]);
  const half = at(nk, "ann", true, now + 1);
  assert.strictEqual(half.order.done, false);
  assert.strictEqual(half.order.needs[0].have, goal.needs[0].target);
  fill(goal.needs[1]);
  const done = at(nk, "bob", true, now + 2);
  assert.strictEqual(done.order.done, true);
  assert.strictEqual(done.orders, 1);
  const source = server.WalletServerRules.sources.filter((entry) => entry.id === rules.orderSource)[0];
  assert.strictEqual(server.walletRead(nk, "ann").value.balances.friendship, source.amount);
  assert.strictEqual(server.walletRead(nk, "bob").value.balances.friendship, source.amount);
  assert.strictEqual(at(nk, "ann", true, now + 3).orders, 1, "one order a week");
  const next = at(nk, "ann", true, now + rules.orderDays * 86400000);
  assert.strictEqual(next.order.done, false);
  assert.ok(next.order.needs.every((need) => need.have === 0));
});

test("the leader changes the weekly order once", () => {
  const { nk, call } = world("ann", "bob");
  yard(nk, call);
  const first = at(nk, "ann", true, now);
  assert.strictEqual(first.order.canRedraw, true);
  assert.strictEqual(at(nk, "bob", true, now).order.canRedraw, false);
  assert.throws(() => server.hideoutOrderRedraw(nk, "bob", now + 1), /only the leader/);
  const second = plain(server.hideoutOrderRedraw(nk, "ann", now + 1));
  assert.notStrictEqual(second.order.id, first.order.id);
  assert.strictEqual(second.order.canRedraw, false);
  assert.throws(() => server.hideoutOrderRedraw(nk, "ann", now + 2), /cannot be changed/);
});

test("there is no party order before the yard", () => {
  const { nk, call } = world("ann", "bob");
  party(call, ["ann", "bob"]);
  assert.strictEqual(at(nk, "ann", true, now).order, null);
});
