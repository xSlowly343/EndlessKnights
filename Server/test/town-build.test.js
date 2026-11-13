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
const vote = (nk, user, project, at) => plain(server.townBuildVote(nk, logger, user, project, at === undefined ? now : at));
function paced(nk, user, resource, amount) {
  const burst = Math.floor(rules.resources.find((entry) => entry.id === resource).dailyCap * rules.paceBurst);
  const steps = Math.ceil(amount / burst);
  for (let i = 0, left = amount; left > 0; i++, left -= burst)
    add(nk, user, { [resource]: Math.min(burst, left) }, now - (steps - i) * rules.paceFillMs * rules.paceBurst);
}

function addDay(nk, user, gains, from) {
  let view = null;
  const sends = Math.ceil(1 / rules.paceBurst);
  for (let i = 0; i < sends; i++)
    view = add(nk, user, gains, from + i * rules.paceFillMs * rules.paceBurst);
  return view;
}
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

const starts = rules.buildings.filter((building) => building.levels[0].afterBuilt === 0).map((building) => building.id);
const projects = [];
for (const building of rules.buildings)
  for (let level = 1; level <= building.levels.length; level++)
    projects.push(server.townBuildProjectId(building.id, level));
const needOf = (project) => {
  const found = server.townBuildProject(project);
  return found.building.levels[found.level - 1].needs;
};
const labour = rules.resources[0].id;
const wood = "wood";
const finish = (nk, ctx, at) => plain(JSON.parse(server.townBuildFinishRpc(ctx, logger, nk, "{}")));

test("rules come from guilds.json: every building level has resources to gather", () => {
  assert.ok(starts.length > 1, "the town starts from a tent and has several first buildings to choose from");
  assert.strictEqual(rules.voteMs, 14 * day, "the vote lasts two weeks");
  assert.ok(rules.openMs > 0);
  assert.ok(rules.resources.length > 0);
  assert.ok(rules.buildings.some((building) => building.id === "gardeners"), "the herbwitch's house is built by the town");
  assert.ok(rules.buildings.some((building) => building.id === "alchemists"), "the alchemist's shop is built by the town");
  for (const building of rules.buildings)
    for (const level of building.levels)
      assert.ok(Object.keys(level.needs).length > 0, building.id);
  assert.strictEqual(rules.stages[0], 0);
  assert.ok(rules.stageShards > 0);
});

test("projects are buildings and their levels", () => {
  assert.strictEqual(server.townBuildProjectId("smiths", 1), "smiths");
  assert.strictEqual(server.townBuildProjectId("smiths", 2), "smiths:2");
  assert.strictEqual(server.townBuildProject("smiths:2").level, 2);
  assert.strictEqual(server.townBuildProject("smiths").level, 1);
  assert.strictEqual(server.townBuildProject("smiths:1"), null);
  assert.strictEqual(server.townBuildProject("smiths:9"), null);
  assert.strictEqual(server.townBuildProject("smiths:x"), null);
  assert.strictEqual(server.townBuildProject("bakery"), null);
  assert.strictEqual(server.townBuildCandidates({ built: [] }).length, starts.length);
  assert.ok(!server.townBuildCandidates({ built: [] }).some((id) => id.indexOf(":") >= 0), "no upgrade before the buildings stand");
  const upgrades = server.townBuildCandidates({ built: projects.filter((id) => id.indexOf(":") < 0) }).filter((id) => id.indexOf(":") >= 0);
  assert.ok(upgrades.length > 0, "once the buildings stand the board offers upgrades");
  for (const id of upgrades)
    assert.ok(server.townBuildProject(id).level === 2, id);
  assert.deepStrictEqual(plain(server.townBuildCandidates({ built: projects })), []);
});

test("the town starts with a vote for the first buildings", () => {
  const { nk } = world(1);
  const view = state(nk, "p0");
  assert.strictEqual(view.phase, "vote");
  assert.strictEqual(view.round, 1);
  assert.strictEqual(view.voteEnds, now + rules.voteMs);
  assert.strictEqual(view.now, now);
  assert.deepStrictEqual(view.candidates, plain(starts.map((project) => ({ project: project, votes: 0 }))));
  assert.deepStrictEqual(view.built, []);
  assert.strictEqual(view.startBuilt, undefined);
  assert.strictEqual(view.project, "");
  assert.strictEqual(view.vote, "");
  assert.strictEqual(state(nk, "p0", now + day).voteEnds, now + rules.voteMs, "the timer does not restart");
});

test("a player has one vote and can move it until the vote ends", () => {
  const { nk } = world(3);
  const [first, second] = [starts[1], starts[2]];
  assert.strictEqual(vote(nk, "p0", first).vote, first);
  vote(nk, "p1", first);
  let view = vote(nk, "p2", second);
  assert.strictEqual(view.candidates.find((entry) => entry.project === first).votes, 2);
  assert.strictEqual(view.candidates.find((entry) => entry.project === second).votes, 1);
  view = vote(nk, "p0", second);
  assert.strictEqual(view.vote, second);
  assert.strictEqual(view.candidates.find((entry) => entry.project === first).votes, 1);
  assert.strictEqual(view.candidates.find((entry) => entry.project === second).votes, 2);
  assert.strictEqual(vote(nk, "p0", second).candidates.find((entry) => entry.project === second).votes, 2, "the same vote twice counts once");
  assert.strictEqual(state(nk, "p1").vote, first);
  assert.throws(() => vote(nk, "p0", first + ":2"), /bad request/, "an upgrade of a building that does not stand");
  assert.throws(() => vote(nk, "p0", 7), /bad request/);
  assert.throws(() => vote(nk, "p0", second, now + rules.voteMs), /the vote is closed/);
});

test("when the vote ends the sign names the winner and every resource it needs", () => {
  const { nk } = world(2);
  const winner = starts[2];
  vote(nk, "p0", winner);
  vote(nk, "p1", winner);
  const view = state(nk, "p0", now + rules.voteMs);
  assert.strictEqual(view.phase, "gather");
  assert.strictEqual(view.project, winner);
  assert.deepStrictEqual(view.candidates, []);
  const stands = (guild) => guild === "";
  for (const entry of view.resources) {
    const rule = rules.resources.find((known) => known.id === entry.id);
    assert.strictEqual(entry.need, stands(rule.guild) ? Math.round((needOf(winner)[entry.id] || 0) * rules.minPlayers * rules.gatherDays) : 0, entry.id);
  }
  assert.ok(resource(view, wood).need > 0, "wood is needed even if nobody cuts it yet: then the build waits");
});

test("a building never needs what only a hall that is not built yet can make", () => {
  const gated = rules.resources.filter((entry) => entry.guild !== "");
  assert.ok(gated.length > 0);
  for (const project of projects) {
    const before = plain(server.townBuildNeed({ built: [], days: {} }, project, 100));
    const after = plain(server.townBuildNeed({ built: projects, days: {} }, project, 100));
    for (const entry of gated) {
      assert.strictEqual(before[entry.id], undefined, project + " " + entry.id);
      if (needOf(project)[entry.id] > 0)
        assert.ok(after[entry.id] > 0, project + " " + entry.id);
    }
    assert.ok(Object.keys(before).length > 0, project);
  }
});

test("the need grows with the players who gathered in the last days", () => {
  const days = {};
  for (let i = 1; i <= rules.activeDays; i++)
    days[String(100 - i)] = 200;
  const project = starts[0];
  const need = plain(server.townBuildNeed({ built: projects, days: days }, project, 100));
  for (const id of Object.keys(needOf(project)))
    assert.strictEqual(need[id], needOf(project)[id] * 200 * rules.gatherDays);
  assert.strictEqual(server.townBuildPlayers({}, 100), rules.minPlayers);
});

test("a tie goes to the building listed first and an empty vote goes on until somebody votes", () => {
  const tied = world(2);
  vote(tied.nk, "p0", starts[3]);
  vote(tied.nk, "p1", starts[1]);
  assert.strictEqual(state(tied.nk, "p0", now + rules.voteMs).project, starts[1]);
  const empty = world(1);
  state(empty.nk, "p0");
  let view = state(empty.nk, "p0", now + rules.voteMs);
  assert.strictEqual(view.phase, "vote");
  assert.strictEqual(view.project, "");
  assert.strictEqual(view.round, 1);
  assert.strictEqual(view.voteEnds, now + 2 * rules.voteMs);
  view = state(empty.nk, "p0", now + 4.5 * rules.voteMs);
  assert.strictEqual(view.voteEnds, now + 5 * rules.voteMs, "a long silence is skipped in one step");
  vote(empty.nk, "p0", starts[2], now + 4.6 * rules.voteMs);
  view = state(empty.nk, "p0", now + 5 * rules.voteMs);
  assert.strictEqual(view.phase, "gather");
  assert.strictEqual(view.project, starts[2]);
});

test("what a player gathers counts for the town during the vote too, capped per day", () => {
  const { nk } = world(1);
  const cap = rules.resources[0].dailyCap;
  const id = rules.resources[0].id;
  const other = rules.resources[1].id;
  const hour = 3600000;
  const burst = Math.floor(cap * rules.paceBurst);
  const first = add(nk, "p0", { [id]: 1200, [other]: 3 });
  assert.strictEqual(first.phase, "vote");
  assert.deepStrictEqual(first.accepted, { [id]: 1200, [other]: 3 });
  assert.strictEqual(resource(first, id).stock, 1200);
  assert.strictEqual(resource(first, id).yours, 1200);
  assert.strictEqual(resource(first, id).sentToday, 1200);
  assert.strictEqual(resource(first, id).dailyCap, cap);
  assert.strictEqual(resource(first, id).room, burst - 1200);
  assert.strictEqual(resource(first, other).stock, 3);
  assert.deepStrictEqual(add(nk, "p0", { [id]: cap }).accepted, { [id]: burst - 1200 }, "one send carries at most a share of the day");
  let sent = burst;
  for (let step = 1; sent < cap; step++) {
    const paced = add(nk, "p0", { [id]: cap }, now + step * rules.paceFillMs * rules.paceBurst);
    assert.deepStrictEqual(paced.accepted, { [id]: Math.min(burst, cap - sent) }, "the share refills with time");
    sent += paced.accepted[id];
  }
  assert.deepStrictEqual(add(nk, "p0", { [id]: 10 }, now + 11 * hour).accepted, {}, "the day is full");
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

test("resources gathered in advance still leave the build two weeks long", () => {
  const { nk } = world(1);
  vote(nk, "p0", starts[0]);
  fill(nk, { resources: plain(rules.resources).map((entry) => ({ id: entry.id, need: 1e12 })) });
  const view = state(nk, "p0", now + rules.voteMs + 5000);
  assert.strictEqual(view.phase, "build");
  assert.strictEqual(view.project, starts[0]);
  assert.strictEqual(rules.minBuildMs, 14 * day, "a build takes at least two weeks");
  assert.strictEqual(view.opensAt, now + rules.voteMs + rules.minBuildMs);
  assert.deepStrictEqual(view.built, [], "not open before the countdown ends");
  assert.strictEqual(state(nk, "p0", now + rules.voteMs + rules.minBuildMs - 1000).phase, "build");
  assert.deepStrictEqual(state(nk, "p0", now + rules.voteMs + rules.minBuildMs).built, [starts[0]]);
});

test("the last delivery starts the countdown, then the building opens and a new vote begins", () => {
  const { nk } = world(1);
  vote(nk, "p0", starts[0]);
  const chosen = now + rules.voteMs;
  let view = state(nk, "p0", chosen);
  assert.strictEqual(view.phase, "gather");
  const id = labour;
  const value = stored(nk);
  for (const entry of view.resources)
    value.stock[entry.id] = entry.id === id ? entry.need - 5 : entry.need + 7;
  const version = nk.storageRead([{ collection: "town", key: "guilds", userId: server.PartySystemUser }])[0].version;
  nk.storageWrite([{ collection: "town", key: "guilds", userId: server.PartySystemUser, value: value, version: version, permissionRead: 0, permissionWrite: 0 }]);
  assert.strictEqual(state(nk, "p0", chosen + day).phase, "gather");
  view = add(nk, "p0", { [id]: 9 }, chosen + day);
  assert.strictEqual(view.phase, "build");
  assert.strictEqual(view.opensAt, chosen + rules.minBuildMs, "gathered early, it still opens two weeks after the vote");
  const late = world(1);
  vote(late.nk, "p0", starts[0]);
  fill(late.nk, state(late.nk, "p0", chosen), chosen + 20 * day);
  assert.strictEqual(state(late.nk, "p0", chosen + 20 * day).opensAt, chosen + 20 * day + rules.openMs, "gathered late, it opens after the countdown");
  assert.deepStrictEqual(plain(server.townMine(nk, "p0", chosen + day)).guilds, []);

  const opened = view.opensAt;
  view = state(nk, "p0", opened + 1000);
  assert.strictEqual(view.phase, "vote");
  assert.strictEqual(view.round, 2);
  assert.deepStrictEqual(view.built, [starts[0]]);
  assert.strictEqual(view.voteEnds, opened + rules.voteMs, "the next vote starts when the building opens");
  assert.ok(!view.candidates.some((entry) => entry.project === starts[0]));
  assert.strictEqual(resource(view, id).stock, 4, "what is left over stays for the next building");
  assert.strictEqual(resource(view, id).need, 0);
  assert.strictEqual(resource(view, id).yours, 0, "a new round counts your part from zero");
  assert.strictEqual(view.vote, "");
  assert.deepStrictEqual(plain(server.townMine(nk, "p0", opened + 1000)).guilds, [starts[0]]);
});

test("a town left alone finishes the building it voted for and then waits for the next vote", () => {
  const { nk } = world(1);
  vote(nk, "p0", starts[1]);
  fill(nk, { resources: rules.resources.map((entry) => ({ id: entry.id, need: 1e12 })) });
  const at = now + 1000 * day;
  const view = state(nk, "p0", at);
  assert.strictEqual(view.phase, "vote");
  assert.strictEqual(view.round, 2);
  assert.deepStrictEqual(view.built, [starts[1]]);
  assert.ok(view.voteEnds > at && view.voteEnds <= at + rules.voteMs);
});

test("builders earn points, the board shows the best of them and the town thanks them by mail when the building opens", () => {
  const { nk } = world(3);
  const letter = (user) => nk.storageRead([{ collection: "mail", key: "town-build-1", userId: user }])[0];
  const full = {};
  for (const entry of rules.resources.slice(0, 3))
    full[entry.id] = entry.dailyCap;
  vote(nk, "p0", starts[0]);
  server.consentSet(nk, logger, "p0", "town_top", true, now);
  let view = addDay(nk, "p0", full, now);
  assert.strictEqual(view.points, 3000, "a full day of one resource is 1000 points");
  assert.strictEqual(view.rank, 1);
  add(nk, "p1", { [rules.resources[0].id]: rules.resources[0].dailyCap / 10 });
  view = addDay(nk, "p0", full, now + day);
  assert.strictEqual(view.points, 6000);
  assert.deepStrictEqual(view.top, [{ rank: 1, name: "p0", points: 6000 }, { rank: 2, name: "", points: 100 }], "only those who agreed show their name");
  assert.strictEqual(state(nk, "p1", now + day).rank, 2);
  assert.strictEqual(state(nk, "p2", now + day).rank, 0);
  assert.strictEqual(letter("p0"), undefined, "nothing is sent while the building is not open");

  fill(nk, { resources: rules.resources.map((entry) => ({ id: entry.id, need: 1e12 })) });
  const opened = now + rules.voteMs + rules.minBuildMs + 1000;
  view = state(nk, "p0", opened);
  assert.strictEqual(view.round, 2);
  assert.strictEqual(view.points, 0);
  assert.deepStrictEqual(view.top, []);
  const tier = rules.rewards.filter((entry) => entry.points <= 6000).pop();
  const sent = plain(letter("p0").value);
  assert.strictEqual(sent.kind, "town");
  assert.strictEqual(sent.title, starts[0]);
  assert.strictEqual(sent.depth, 6000);
  assert.strictEqual(sent.place, 1);
  assert.ok(rules.stages.includes(1), "the first building changes the look of the town");
  assert.deepStrictEqual(sent.reward, { shards: tier.shards + rules.topShards + rules.stageShards, ingots: 0, friendship: tier.friendship });
  state(nk, "p1", opened);
  state(nk, "p2", opened);
  assert.strictEqual(letter("p1"), undefined, "below the first tier there is no letter");
  assert.strictEqual(letter("p2"), undefined);
  const version = letter("p0").version;
  add(nk, "p0", full, opened);
  state(nk, "p0", opened + day);
  assert.strictEqual(letter("p0").version, version, "one letter per building");
});

test("the game developer can open a gathered building before two weeks, players cannot", () => {
  const { nk } = world(2);
  nk.storageWrite([{ collection: "chat_roles", key: "p1", userId: server.PartySystemUser, value: { role: "developer" }, version: "*", permissionRead: 0, permissionWrite: 0 }]);
  vote(nk, "p0", starts[0]);
  const chosen = now + rules.voteMs;
  assert.strictEqual(state(nk, "p0", chosen).phase, "gather");
  assert.throws(() => server.townBuildFinish(nk, logger, "server", chosen), /nothing gathered/, "not before everything is gathered");
  fill(nk, { resources: rules.resources.map((entry) => ({ id: entry.id, need: 1e12 })) }, chosen);
  assert.throws(() => server.townBuildFinishRpc({ userId: "p0" }, logger, nk, "{}"), (error) => error.code === 7, "a player cannot");
  nk.storageWrite([{ collection: "chat_roles", key: "p0", userId: server.PartySystemUser, value: { role: "moderator" }, version: "*", permissionRead: 0, permissionWrite: 0 }]);
  assert.throws(() => server.townBuildFinishRpc({ userId: "p0" }, logger, nk, "{}"), (error) => error.code === 7, "a chat moderator cannot");
  const done = plain(server.townBuildFinish(nk, logger, "p1", chosen + day));
  assert.deepStrictEqual(done.built, [starts[0]]);
  assert.strictEqual(done.phase, "vote");
  assert.throws(() => server.townBuildFinishRpc({ userId: "p1" }, logger, nk, "{}"), /nothing gathered/, "a new vote has nothing to open");
});

test("there are more reward tiers than before and each one is worth more", () => {
  assert.ok(rules.rewards.length >= 8);
  for (let i = 1; i < rules.rewards.length; i++) {
    assert.ok(rules.rewards[i].points > rules.rewards[i - 1].points);
    assert.ok(rules.rewards[i].shards > rules.rewards[i - 1].shards);
  }
});

test("when every building and level is done the town stops counting and a record of the old format starts a vote", () => {
  const done = world(1);
  done.nk.storageWrite([{ collection: "town", key: "guilds", userId: server.PartySystemUser, value: { built: projects.slice(), project: projects.length, progress: 0, target: 0, days: {} }, version: "*", permissionRead: 0, permissionWrite: 0 }]);
  const view = add(done.nk, "p0", { [rules.resources[0].id]: 100 });
  assert.strictEqual(view.phase, "done");
  assert.strictEqual(view.project, "");
  assert.deepStrictEqual(view.accepted, {});
  assert.strictEqual(resource(view, rules.resources[0].id).sentToday, 0, "nothing is taken from the player");
  assert.deepStrictEqual(view.built, projects);

  const old = world(1);
  old.nk.storageWrite([{ collection: "town", key: "guilds", userId: server.PartySystemUser, value: { built: ["fishers"], project: 1, progress: 300, target: 50000, days: {} }, version: "*", permissionRead: 0, permissionWrite: 0 }]);
  old.nk.storageWrite([{ collection: "town", key: "build", userId: "p0", value: { day: 1, sent: 40 }, version: "*", permissionRead: 1, permissionWrite: 0 }]);
  const fresh = state(old.nk, "p0");
  assert.strictEqual(fresh.phase, "vote");
  assert.deepStrictEqual(fresh.built, ["fishers"]);
  assert.strictEqual(fresh.candidates.length, starts.length - 1);
  assert.ok(fresh.candidates.some((entry) => entry.project === "gardeners"), "an old town builds the herbwitch's house too");
});

test("a builder decides whether the top shows their name, and can change it later", () => {
  const { nk } = world(2);
  vote(nk, "p0", starts[0]);
  add(nk, "p0", { [rules.resources[0].id]: 100 });
  add(nk, "p1", { [rules.resources[0].id]: 50 });
  assert.deepStrictEqual(state(nk, "p0").top.map((entry) => entry.name), ["", ""], "nobody is named without consent");
  assert.deepStrictEqual(plain(server.consentSet(nk, logger, "p1", "town_top", true, now)).shown, ["town_top"]);
  assert.deepStrictEqual(state(nk, "p0").top, [{ rank: 1, name: "", points: 20 }, { rank: 2, name: "p1", points: 10 }]);
  server.consentSet(nk, logger, "p1", "town_top", false, now);
  assert.deepStrictEqual(state(nk, "p0").top.map((entry) => entry.name), ["", ""], "taking consent back hides the name again");
  assert.throws(() => server.consentSet(nk, logger, "p1", "everywhere", true, now), /bad request/);
  assert.throws(() => server.consentSet(nk, logger, "p1", "town_top", "yes", now), /bad request/);
});

test("the weekly rebuild counts everyone's labour, decorates the town and gives a frame to those who helped", () => {
  const { nk } = world(3);
  const week = server.TownWeekServerRules;
  const owns = (user, id) => (nk.storageRead([{ collection: "cosmetics", key: "owned", userId: user }])[0] || { value: { owned: [] } }).value.owned.includes(id);
  let view = state(nk, "p0");
  assert.strictEqual(view.week.week, server.weeklyWeekOf(now));
  assert.strictEqual(view.week.goal, Math.max(week.minGoal, Math.round(rules.minPlayers * week.perPlayer)));
  assert.strictEqual(view.week.done, false);
  const reward = view.week.frame;
  paced(nk, "p0", "labour", 4000);
  paced(nk, "p2", "labour", week.minLabour - 1);
  view = state(nk, "p0");
  assert.strictEqual(view.week.total, 4000 + week.minLabour - 1);
  assert.strictEqual(view.week.yours, 4000);
  const stored = nk.storageRead([{ collection: "town", key: "week", userId: server.PartySystemUser }])[0];
  nk.storageWrite([{ collection: "town", key: "week", userId: server.PartySystemUser, value: Object.assign({}, stored.value, { total: view.week.goal - 10 }), version: stored.version, permissionRead: 0, permissionWrite: 0 }]);
  view = add(nk, "p1", { labour: 10 });
  assert.strictEqual(view.week.done, true, "the goal is reached");
  assert.deepStrictEqual(view.week.decorations, [view.week.decor], "the town gets its decoration of the week");
  assert.ok(!owns("p1", reward), "ten actions are too few for the frame");
  assert.strictEqual(state(nk, "p0").week.framed, true);
  assert.ok(owns("p0", reward), "a helper gets the frame on the next visit");
  state(nk, "p2");
  assert.ok(!owns("p2", reward), "too little labour, no frame");
  add(nk, "p2", { labour: 1 });
  assert.ok(owns("p2", reward), "reaching the minimum later still gives the frame");
  const next = state(nk, "p0", now + 7 * day);
  assert.strictEqual(next.week.week, server.weeklyWeekOf(now) + 1);
  assert.strictEqual(next.week.total, 0);
  assert.strictEqual(next.week.done, false);
  assert.strictEqual(next.week.yours, 0);
  assert.notStrictEqual(next.week.frame, reward, "every week has its own reward");
  assert.deepStrictEqual(next.week.decorations, [view.week.decor], "decorations stay");
});
