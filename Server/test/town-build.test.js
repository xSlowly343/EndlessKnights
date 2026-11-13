const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.TownBuildServerRules;
const plain = (o) => JSON.parse(JSON.stringify(o));
const day = 86400000;
const now = server.raidWeekStart(3000) + 12 * 3600000;

function world(count) {
  const nk = new FakeNakama();
  const names = [];
  for (let i = 0; i < count; i++) {
    names.push("p" + i);
    nk.addUser("p" + i, "p" + i, "7656119800000" + String(1000 + i));
  }
  return { nk, names };
}

const state = (nk, user, at) => plain(server.townBuildState(nk, logger, user, at === undefined ? now : at));
const add = (nk, user, gains, at) => plain(server.townBuildAdd(nk, logger, user, gains, at === undefined ? now : at));
const vote = (nk, user, guild, at) => plain(server.townBuildVote(nk, logger, user, guild, at === undefined ? now : at));
const resource = (view, id) => view.resources.find((entry) => entry.id === id);
const stored = (nk) => nk.storageRead([{ collection: "town", key: "guilds", userId: server.PartySystemUser }])[0].value;

function fill(nk, view, at) {
  const value = stored(nk);
  for (const entry of view.resources)
    value.stock[entry.id] = Math.max(value.stock[entry.id] || 0, entry.need);
  const version = nk.storageRead([{ collection: "town", key: "guilds", userId: server.PartySystemUser }])[0].version;
  nk.storageWrite([{ collection: "town", key: "guilds", userId: server.PartySystemUser, value: value, version: version, permissionRead: 0, permissionWrite: 0 }]);
  return state(nk, "p0", at);
}

test("rules come from guilds.json: every building on the board has resources to gather", () => {
  assert.ok(rules.order.length > 0);
  assert.strictEqual(rules.voteMs, 14 * day, "the vote lasts two weeks");
  assert.ok(rules.openMs > 0);
  assert.ok(rules.resources.length > 0);
  for (const guild of rules.startBuilt)
    assert.ok(!rules.order.includes(guild));
  for (const guild of rules.order)
    assert.ok(Object.keys(rules.needs[guild]).length > 0, guild);
});

test("the town starts with a vote for every building that is not built", () => {
  const { nk } = world(1);
  const view = state(nk, "p0");
  assert.strictEqual(view.phase, "vote");
  assert.strictEqual(view.round, 1);
  assert.strictEqual(view.voteEnds, now + rules.voteMs);
  assert.strictEqual(view.now, now);
  assert.deepStrictEqual(view.candidates, plain(rules.order).map((guild) => ({ guild: guild, votes: 0 })));
  assert.deepStrictEqual(view.built, []);
  assert.deepStrictEqual(view.startBuilt, plain(rules.startBuilt));
  assert.strictEqual(view.project, "");
  assert.strictEqual(view.vote, "");
  assert.strictEqual(state(nk, "p0", now + day).voteEnds, now + rules.voteMs, "the timer does not restart");
});

test("a player has one vote and can move it until the vote ends", () => {
  const { nk } = world(3);
  const [first, second] = [rules.order[1], rules.order[2]];
  assert.strictEqual(vote(nk, "p0", first).vote, first);
  vote(nk, "p1", first);
  let view = vote(nk, "p2", second);
  assert.strictEqual(view.candidates.find((entry) => entry.guild === first).votes, 2);
  assert.strictEqual(view.candidates.find((entry) => entry.guild === second).votes, 1);
  view = vote(nk, "p0", second);
  assert.strictEqual(view.vote, second);
  assert.strictEqual(view.candidates.find((entry) => entry.guild === first).votes, 1);
  assert.strictEqual(view.candidates.find((entry) => entry.guild === second).votes, 2);
  assert.strictEqual(vote(nk, "p0", second).candidates.find((entry) => entry.guild === second).votes, 2, "the same vote twice counts once");
  assert.strictEqual(state(nk, "p1").vote, first);
  assert.throws(() => vote(nk, "p0", rules.startBuilt[0]), /bad request/);
  assert.throws(() => vote(nk, "p0", 7), /bad request/);
  assert.throws(() => vote(nk, "p0", second, now + rules.voteMs), /the vote is closed/);
});

test("when the vote ends the sign names the winner and the resources it needs", () => {
  const { nk } = world(2);
  const winner = rules.order[2];
  vote(nk, "p0", winner);
  vote(nk, "p1", winner);
  const view = state(nk, "p0", now + rules.voteMs);
  assert.strictEqual(view.phase, "gather");
  assert.strictEqual(view.project, winner);
  assert.deepStrictEqual(view.candidates, []);
  const stands = (guild) => guild === "" || rules.startBuilt.includes(guild);
  for (const entry of view.resources) {
    const rule = rules.resources.find((known) => known.id === entry.id);
    assert.strictEqual(entry.need, stands(rule.guild) ? Math.round((rules.needs[winner][entry.id] || 0) * rules.minPlayers * rules.gatherDays) : 0, entry.id);
  }
});

test("a hall never needs what only a hall that is not built yet can make", () => {
  const gated = rules.resources.filter((entry) => entry.guild !== "" && !rules.startBuilt.includes(entry.guild));
  assert.ok(gated.length > 0);
  for (const guild of rules.order) {
    const before = plain(server.townBuildNeed({ built: [], days: {} }, guild, 100));
    const after = plain(server.townBuildNeed({ built: plain(rules.order), days: {} }, guild, 100));
    for (const entry of gated) {
      assert.strictEqual(before[entry.id], undefined, guild + " " + entry.id);
      if (rules.needs[guild][entry.id] > 0)
        assert.ok(after[entry.id] > 0, guild + " " + entry.id);
    }
    assert.ok(Object.keys(before).length > 0, guild);
  }
});

test("a tie goes to the building listed first and an empty vote goes on until somebody votes", () => {
  const tied = world(2);
  vote(tied.nk, "p0", rules.order[3]);
  vote(tied.nk, "p1", rules.order[1]);
  assert.strictEqual(state(tied.nk, "p0", now + rules.voteMs).project, rules.order[1]);
  const empty = world(1);
  state(empty.nk, "p0");
  let view = state(empty.nk, "p0", now + rules.voteMs);
  assert.strictEqual(view.phase, "vote");
  assert.strictEqual(view.project, "");
  assert.strictEqual(view.round, 1);
  assert.strictEqual(view.voteEnds, now + 2 * rules.voteMs);
  view = state(empty.nk, "p0", now + 4.5 * rules.voteMs);
  assert.strictEqual(view.voteEnds, now + 5 * rules.voteMs, "a long silence is skipped in one step");
  vote(empty.nk, "p0", rules.order[2], now + 4.6 * rules.voteMs);
  view = state(empty.nk, "p0", now + 5 * rules.voteMs);
  assert.strictEqual(view.phase, "gather");
  assert.strictEqual(view.project, rules.order[2]);
});

test("what a player gathers counts for the town during the vote too, capped per day", () => {
  const { nk } = world(1);
  const cap = rules.resources[0].dailyCap;
  const id = rules.resources[0].id;
  const other = rules.resources[1].id;
  const first = add(nk, "p0", { [id]: 1200, [other]: 3 });
  assert.strictEqual(first.phase, "vote");
  assert.deepStrictEqual(first.accepted, { [id]: 1200, [other]: 3 });
  assert.strictEqual(resource(first, id).stock, 1200);
  assert.strictEqual(resource(first, id).yours, 1200);
  assert.strictEqual(resource(first, id).sentToday, 1200);
  assert.strictEqual(resource(first, id).dailyCap, cap);
  assert.strictEqual(resource(first, other).stock, 3);
  const capped = add(nk, "p0", { [id]: cap });
  assert.deepStrictEqual(capped.accepted, { [id]: cap - 1200 });
  assert.deepStrictEqual(add(nk, "p0", { [id]: 10 }).accepted, {}, "the day is full");
  const next = add(nk, "p0", { [id]: 10 }, now + day);
  assert.deepStrictEqual(next.accepted, { [id]: 10 }, "the next day opens again");
  assert.strictEqual(resource(next, id).stock, cap + 10);
  assert.strictEqual(resource(next, id).yours, cap + 10);
  assert.throws(() => add(nk, "p0", {}), /bad request/);
  assert.throws(() => add(nk, "p0", { [id]: 0 }), /bad request/);
  assert.throws(() => add(nk, "p0", { [id]: 2.5 }), /bad request/);
  assert.throws(() => add(nk, "p0", { gold: 5 }), /bad request/);
  assert.throws(() => add(nk, "p0", 5), /bad request/);
});

test("resources gathered in advance start the countdown the moment the vote ends", () => {
  const { nk } = world(1);
  vote(nk, "p0", rules.order[0]);
  fill(nk, { resources: plain(rules.resources).map((entry) => ({ id: entry.id, need: 1e12 })) });
  const view = state(nk, "p0", now + rules.voteMs + 5000);
  assert.strictEqual(view.phase, "build");
  assert.strictEqual(view.project, rules.order[0]);
  assert.strictEqual(view.opensAt, now + rules.voteMs + rules.openMs);
  assert.deepStrictEqual(view.built, [], "not open before the countdown ends");
});

test("the last delivery starts the countdown, then the hall opens and a new vote begins", () => {
  const { nk } = world(1);
  vote(nk, "p0", rules.order[0]);
  const chosen = now + rules.voteMs;
  let view = state(nk, "p0", chosen);
  assert.strictEqual(view.phase, "gather");
  const id = rules.resources[0].id;
  const value = stored(nk);
  for (const entry of view.resources)
    value.stock[entry.id] = entry.id === id ? entry.need - 5 : entry.need + 7;
  const version = nk.storageRead([{ collection: "town", key: "guilds", userId: server.PartySystemUser }])[0].version;
  nk.storageWrite([{ collection: "town", key: "guilds", userId: server.PartySystemUser, value: value, version: version, permissionRead: 0, permissionWrite: 0 }]);
  assert.strictEqual(state(nk, "p0", chosen + day).phase, "gather");
  view = add(nk, "p0", { [id]: 9 }, chosen + day);
  assert.strictEqual(view.phase, "build");
  assert.strictEqual(view.opensAt, chosen + day + rules.openMs);
  assert.deepStrictEqual(plain(server.townMine(nk, "p0", chosen + day)).guilds, []);

  const opened = view.opensAt;
  view = state(nk, "p0", opened + 1000);
  assert.strictEqual(view.phase, "vote");
  assert.strictEqual(view.round, 2);
  assert.deepStrictEqual(view.built, [rules.order[0]]);
  assert.strictEqual(view.voteEnds, opened + rules.voteMs, "the next vote starts when the hall opens");
  assert.ok(!view.candidates.some((entry) => entry.guild === rules.order[0]));
  assert.strictEqual(resource(view, id).stock, 4, "what is left over stays for the next hall");
  assert.strictEqual(resource(view, id).need, 0);
  assert.strictEqual(resource(view, id).yours, 0, "a new round counts your part from zero");
  assert.strictEqual(view.vote, "");
  assert.deepStrictEqual(plain(server.townMine(nk, "p0", opened + 1000)).guilds, [rules.order[0]]);
});

test("the need grows with the players who gathered in the last days", () => {
  const days = {};
  for (let i = 1; i <= rules.activeDays; i++)
    days[String(100 - i)] = 200;
  const guild = rules.order[0];
  const need = plain(server.townBuildNeed({ built: plain(rules.order), days: days }, guild, 100));
  for (const id of Object.keys(rules.needs[guild]))
    assert.strictEqual(need[id], rules.needs[guild][id] * 200 * rules.gatherDays);
  assert.strictEqual(server.townBuildPlayers({}, 100), rules.minPlayers);
});

test("a town left alone finishes the hall it voted for and then waits for the next vote", () => {
  const { nk } = world(1);
  vote(nk, "p0", rules.order[1]);
  fill(nk, { resources: rules.resources.map((entry) => ({ id: entry.id, need: 1e12 })) });
  const at = now + 1000 * day;
  const view = state(nk, "p0", at);
  assert.strictEqual(view.phase, "vote");
  assert.strictEqual(view.round, 2);
  assert.deepStrictEqual(view.built, [rules.order[1]]);
  assert.ok(view.voteEnds > at && view.voteEnds <= at + rules.voteMs);
});

test("builders earn points, the board shows the best of them and the town thanks them by mail when the hall opens", () => {
  const { nk } = world(3);
  const letter = (user) => nk.storageRead([{ collection: "mail", key: "town-build-1", userId: user }])[0];
  const full = {};
  for (const entry of rules.resources.slice(0, 3))
    full[entry.id] = entry.dailyCap;
  vote(nk, "p0", rules.order[0]);
  let view = add(nk, "p0", full);
  assert.strictEqual(view.points, 3000, "a full day of one resource is 1000 points");
  assert.strictEqual(view.rank, 1);
  add(nk, "p1", { [rules.resources[0].id]: rules.resources[0].dailyCap / 2 });
  view = add(nk, "p0", full, now + day);
  assert.strictEqual(view.points, 6000);
  assert.deepStrictEqual(view.top, [{ rank: 1, name: "p0", points: 6000 }, { rank: 2, name: "p1", points: 500 }]);
  assert.strictEqual(state(nk, "p1", now + day).rank, 2);
  assert.strictEqual(state(nk, "p2", now + day).rank, 0);
  assert.strictEqual(letter("p0"), undefined, "nothing is sent while the hall is not open");

  fill(nk, { resources: rules.resources.map((entry) => ({ id: entry.id, need: 1e12 })) });
  const opened = now + rules.voteMs + rules.openMs + 1000;
  view = state(nk, "p0", opened);
  assert.strictEqual(view.round, 2);
  assert.strictEqual(view.points, 0);
  assert.deepStrictEqual(view.top, []);
  const tier = rules.rewards.filter((entry) => entry.points <= 6000).pop();
  const sent = plain(letter("p0").value);
  assert.strictEqual(sent.kind, "town");
  assert.strictEqual(sent.title, rules.order[0]);
  assert.strictEqual(sent.depth, 6000);
  assert.strictEqual(sent.place, 1);
  assert.deepStrictEqual(sent.reward, { shards: tier.shards + rules.topShards, ingots: 0, friendship: tier.friendship });
  state(nk, "p1", opened);
  state(nk, "p2", opened);
  assert.strictEqual(letter("p1"), undefined, "below the first tier there is no letter");
  assert.strictEqual(letter("p2"), undefined);
  const version = letter("p0").version;
  add(nk, "p0", full, opened);
  state(nk, "p0", opened + day);
  assert.strictEqual(letter("p0").version, version, "one letter per hall");
});

test("when every hall is built the town stops counting and a record of the old format starts a vote", () => {
  const done = world(1);
  done.nk.storageWrite([{ collection: "town", key: "guilds", userId: server.PartySystemUser, value: { built: rules.order.slice(), project: rules.order.length, progress: 0, target: 0, days: {} }, version: "*", permissionRead: 0, permissionWrite: 0 }]);
  const view = add(done.nk, "p0", { [rules.resources[0].id]: 100 });
  assert.strictEqual(view.phase, "done");
  assert.strictEqual(view.project, "");
  assert.deepStrictEqual(view.accepted, {});
  assert.strictEqual(resource(view, rules.resources[0].id).sentToday, 0, "nothing is taken from the player");
  assert.deepStrictEqual(view.built, plain(rules.order));

  const old = world(1);
  old.nk.storageWrite([{ collection: "town", key: "guilds", userId: server.PartySystemUser, value: { built: [rules.order[0]], project: 1, progress: 300, target: 50000, days: {} }, version: "*", permissionRead: 0, permissionWrite: 0 }]);
  old.nk.storageWrite([{ collection: "town", key: "build", userId: "p0", value: { day: 1, sent: 40 }, version: "*", permissionRead: 1, permissionWrite: 0 }]);
  const fresh = state(old.nk, "p0");
  assert.strictEqual(fresh.phase, "vote");
  assert.deepStrictEqual(fresh.built, [rules.order[0]]);
  assert.strictEqual(fresh.candidates.length, rules.order.length - 1);
});
