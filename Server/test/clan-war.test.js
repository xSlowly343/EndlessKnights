const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.ClanWarServerRules;
const day = 86400000;
const monday = server.raidWeekStart(server.raidWeekOf(Date.UTC(2026, 10, 4, 12)));
const at = (week, weekday, hours) => monday + week * 7 * day + weekday * day + (hours || 12) * 3600000;
const plain = (o) => JSON.parse(JSON.stringify(o));
const caps = server.ChallengeServerRules.capsPerSecond;

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  return { nk, call };
}

function clanOf(w, names) {
  const clan = w.call(names[0], "clanCreateRpc", {});
  for (const name of names.slice(1))
    w.call(name, "clanJoinRpc", { code: clan.code });
  return clan;
}

const group = (w, user, now) => server.clanOf(w.nk, user, now);
const note = (w, user, now, counts, shield, finished) => server.clanWarNote(w.nk, logger, group(w, user, now), user, counts || {}, shield || 0, finished || 0, now);
const view = (w, user, now) => plain(server.clanWarView(w.nk, logger, group(w, user, now), user, now));
const keys = (seconds) => ({ keys: caps.keys * seconds });

test("rules come from clan_wars.json: five fronts, war on Saturday, one cap for every front", () => {
  assert.deepStrictEqual(plain(rules.fronts.map((f) => f.id)), ["keys", "mouse", "clicks", "bosses", "skills"]);
  assert.strictEqual(rules.warDay, 5);
  assert.strictEqual(rules.pairDay, 4);
  assert.strictEqual(rules.minActive, 3);
  const units = plain(server.clanWarUnits({ keys: caps.keys * 10, mouseMeters: caps.mouseMeters * 10, clicks: caps.clicks * 10, bosses: caps.bosses * 10 }, server.RaidServerRules.maxSharesPerSecond * 10));
  for (const front of rules.fronts)
    assert.ok(Math.abs(units[front.id] - 10) < 1e-9, front.id + " counts seconds of input at the server cap");
});

test("a member's share of a front stops at the daily cap", () => {
  const score = server.clanWarEmptyScore("c", 1);
  server.clanWarAdd(score, "ann", { keys: rules.perMemberSeconds - 10 });
  server.clanWarAdd(score, "ann", { keys: 500 });
  server.clanWarAdd(score, "bob", { keys: 20 });
  assert.strictEqual(score.members.ann.keys, rules.perMemberSeconds);
  assert.strictEqual(score.fronts.keys, rules.perMemberSeconds + 20);
});

test("a week of war: gather, pair on Friday, fight on Saturday, seals and rating on Sunday", () => {
  const w = world("ann", "bob", "cid", "dan", "eve", "fay", "gus", "hal");
  const red = clanOf(w, ["ann", "bob", "cid"]);
  const blue = clanOf(w, ["dan", "eve", "fay"]);
  clanOf(w, ["gus", "hal"]);
  for (const user of ["ann", "bob", "cid", "dan", "eve", "fay", "gus", "hal"])
    note(w, user, at(0, 1), keys(1));
  note(w, "ann", at(0, 2), keys(1), 0, 3);
  assert.strictEqual(view(w, "ann", at(0, 2)).phase, "gather");
  assert.ok(Math.abs(view(w, "ann", at(0, 2)).morale - 0.03) < 1e-9);

  const friday = view(w, "ann", at(0, 4));
  assert.strictEqual(friday.phase, "paired");
  assert.strictEqual(friday.opponent.clanId, blue.id);
  assert.strictEqual(view(w, "gus", at(0, 4)).opponent, null, "a clan with fewer than three active members sits the war out");

  const saturday = at(0, 5);
  note(w, "ann", saturday, keys(1000));
  note(w, "bob", saturday, keys(1000), server.RaidServerRules.maxSharesPerSecond * 100);
  note(w, "dan", saturday, { mouseMeters: caps.mouseMeters * 500, clicks: caps.clicks * 500, bosses: caps.bosses * 500 });
  note(w, "eve", saturday, keys(100));
  const war = view(w, "ann", saturday);
  assert.strictEqual(war.phase, "war");
  assert.ok(Math.abs(war.fronts.ours.keys - 2000 * 1.03) < 1e-6, "morale lifts every front");
  assert.strictEqual(war.fronts.mine.keys, 1000);
  note(w, "dan", at(0, 4), keys(100000));
  assert.strictEqual(view(w, "dan", saturday).fronts.ours.keys, 100, "Friday input does not count");

  const sunday = view(w, "ann", at(0, 6));
  assert.strictEqual(sunday.phase, "results");
  assert.strictEqual(sunday.last.outcome, "loss", "blue takes mouse, clicks and bosses, red only keys and skills");
  assert.strictEqual(sunday.last.points, 2);
  assert.strictEqual(sunday.last.theirPoints, 3);
  assert.strictEqual(sunday.last.seals, Math.round(rules.sealsPerMember * 2 * rules.sealsLoss));
  const blueSunday = view(w, "dan", at(0, 6, 20));
  assert.strictEqual(blueSunday.last.outcome, "win");
  assert.strictEqual(blueSunday.rating, rules.ratingWin);
  assert.strictEqual(server.clanTreeRead(w.nk, blue.id).value.treasury, Math.round(rules.sealsPerMember * 2 * rules.sealsWin));
  assert.strictEqual(server.clanTreeRead(w.nk, red.id).value.log[0].kind, "war");
  const records = server.clanChronicleRead(w.nk, blue.id).value.records;
  assert.ok(records.some((r) => r.kind === "war_win"));
  assert.strictEqual(server.clanChronicleRead(w.nk, blue.id).value.stats.warsWon, 1);
  view(w, "dan", at(1, 0));
  assert.strictEqual(server.clanTreeRead(w.nk, blue.id).value.treasury, Math.round(rules.sealsPerMember * 2 * rules.sealsWin), "seals are paid once");
});

test("the odd clan meets a shadow of last week, or rests with a draw when there is none", () => {
  const w = world("a1", "a2", "a3", "b1", "b2", "b3", "c1", "c2", "c3");
  clanOf(w, ["a1", "a2", "a3"]);
  clanOf(w, ["b1", "b2", "b3"]);
  const third = clanOf(w, ["c1", "c2", "c3"]);
  for (const user of ["a1", "a2", "a3", "b1", "b2", "b3", "c1", "c2", "c3"])
    note(w, user, at(0, 0), keys(1));
  const pairs = plain(server.clanWarPairsOf(w.nk, logger, server.raidWeekOf(at(0, 4)), at(0, 4)));
  const odd = pairs.pairs.find((p) => p.shadow !== null);
  assert.ok(odd, "three clans give one pair and one rest");
  note(w, odd.a === third.id ? "c1" : "a1", at(0, 5), keys(10));
  const oddUser = odd.a === third.id ? "c1" : odd.a === group(w, "a1", at(0, 5)).id ? "a1" : "b1";
  const rest = view(w, oddUser, at(0, 6));
  assert.strictEqual(rest.opponent.bye, true);
  assert.strictEqual(rest.last.outcome, "draw");

  for (const user of ["a1", "a2", "a3", "b1", "b2", "b3", "c1", "c2", "c3"])
    note(w, user, at(1, 0), keys(1));
  const next = plain(server.clanWarPairsOf(w.nk, logger, server.raidWeekOf(at(1, 4)), at(1, 4)));
  const shadowPair = next.pairs.find((p) => p.shadow !== null);
  assert.ok(shadowPair.b, "next week the odd clan fights the numbers of a clan from last week");
  assert.ok(Object.keys(shadowPair.shadow).length > 0);
});

test("only the leader or an officer opts the clan out, and only before Friday", () => {
  const w = world("ann", "bob", "cid");
  clanOf(w, ["ann", "bob", "cid"]);
  assert.throws(() => server.clanWarJoin(w.nk, logger, "bob", false, at(0, 1)), /only the leader or an officer/);
  const out = plain(server.clanWarJoin(w.nk, logger, "ann", false, at(0, 1)));
  assert.strictEqual(out.joined, false);
  for (const user of ["ann", "bob", "cid"])
    note(w, user, at(0, 2), keys(1));
  assert.throws(() => server.clanWarJoin(w.nk, logger, "ann", true, at(0, 4)), /pairs are already made/);
  assert.strictEqual(server.clanWarPairsOf(w.nk, logger, server.raidWeekOf(at(0, 4)), at(0, 4)).pairs.length, 0);
});

test("leagues follow the rating and a new season drops a clan one league", () => {
  assert.strictEqual(server.clanWarLeague(0), "bronze");
  assert.strictEqual(server.clanWarLeague(12), "silver");
  assert.strictEqual(server.clanWarLeague(40), "gold");
  const turned = plain(server.clanWarSeasonTurn({ rating: 40, season: 3, lastWeek: 10, wins: 7 }, 4));
  assert.strictEqual(turned.rating, 10);
  assert.strictEqual(turned.season, 4);
  assert.strictEqual(plain(server.clanWarSeasonTurn({ rating: 40, season: 4, lastWeek: 10, wins: 7 }, 4)).rating, 40);
});
