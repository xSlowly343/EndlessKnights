const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.ClanCampServerRules;
const plain = (o) => JSON.parse(JSON.stringify(o));
const day = 86400000;
const now = server.raidWeekStart(3000) + day;
const herb = Object.keys(rules.materials).find((item) => rules.materials[item] === 1);
const dish = rules.dishes[0];

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  const clan = call(names[0], "clanCreateRpc", {});
  for (const name of names.slice(1))
    call(name, "clanJoinRpc", { code: clan.code });
  return { nk, call };
}

const give = (nk, user, item, amount, at) => plain(server.clanCampGive(nk, user, item, amount, at === undefined ? now : at));

function build(nk, users, points) {
  let at = now;
  let given = 0;
  while (given < points) {
    for (const user of users) {
      const left = rules.dailyPointsPerMember - (given % rules.dailyPointsPerMember);
      give(nk, user, herb, Math.min(rules.maxGive, left), at);
    }
    given += rules.maxGive * users.length;
    at += day;
  }
}

test("rules come from clan_camp.json and cooking.json", () => {
  assert.ok(herb, "a one-point material exists");
  assert.ok(rules.levels.length > rules.feastFromLevel);
  assert.ok(rules.dishes.length > 0);
});

test("members give materials for points and the camp grows levels", () => {
  const { nk } = world("ann", "bob");
  const first = give(nk, "ann", herb, 10);
  assert.strictEqual(first.points, 10);
  assert.strictEqual(first.yours, 10);
  assert.strictEqual(first.today, 10);
  assert.strictEqual(first.level, 0);
  assert.strictEqual(first.next, rules.levels[0]);
  assert.strictEqual(first.log[0].kind, "gave");
  assert.strictEqual(first.log[0].name, "ann");
  assert.throws(() => give(nk, "ann", "gold_bar", 1), /not a camp material/);
  assert.throws(() => give(nk, "ann", herb, rules.maxGive + 1), /bad amount/);
  assert.throws(() => give(nk, "ann", herb, 0), /bad amount/);
  assert.throws(() => server.clanCampGive(nk, "zed", herb, 1, now), /./);

  build(nk, ["ann", "bob"], rules.levels[0]);
  assert.ok(plain(server.clanCampView(nk, server.clanOf(nk, "ann", now), server.clanCampRead(nk, server.clanOf(nk, "ann", now).id, now + 30 * day).value, "ann")).level >= 1);
});

test("one member gives at most the daily cap, the next day opens again", () => {
  const { nk } = world("ann");
  let total = 0;
  while (total + rules.maxGive <= rules.dailyPointsPerMember) {
    give(nk, "ann", herb, rules.maxGive);
    total += rules.maxGive;
  }
  assert.throws(() => give(nk, "ann", herb, rules.dailyPointsPerMember - total + 1), /daily limit/);
  assert.strictEqual(give(nk, "ann", herb, 1, now + day).today, 1);
});

test("the feast needs the table, fills with dishes and is laid until the raid week ends", () => {
  const { nk } = world("ann", "bob");
  assert.throws(() => server.clanFeastBring(nk, "ann", dish, now), /feast table is not built/);
  build(nk, ["ann", "bob"], rules.levels[rules.feastFromLevel - 1]);
  const at = now + 40 * day;
  assert.throws(() => server.clanFeastBring(nk, "ann", "stone_soup", at), /not a dish/);
  const target = server.clanCampFeastTarget(2);
  assert.strictEqual(target, rules.feastMinDishes);
  let view = null;
  const bringers = ["ann", "bob"];
  for (let i = 0; i < target; i++) {
    const user = bringers[i % 2];
    view = plain(server.clanFeastBring(nk, user, dish, at + i));
  }
  assert.strictEqual(view.feast.dishes, target);
  assert.strictEqual(view.feast.laid, true);
  assert.strictEqual(view.feast.until, server.raidWeekStart(server.raidWeekOf(at) + 1));
  assert.strictEqual(view.log[0].kind, "feast");
  assert.throws(() => server.clanFeastBring(nk, "ann", dish, at + 100), /already laid/);

  const nextWeek = server.raidWeekStart(server.raidWeekOf(at) + 1) + 1;
  const fresh = plain(server.clanFeastBring(nk, "ann", dish, nextWeek));
  assert.strictEqual(fresh.feast.dishes, 1, "a new raid week starts a new feast");
  assert.strictEqual(fresh.feast.laid, false);
});

test("one member brings a limited number of dishes a week", () => {
  const { nk } = world("ann", "bob", "eve", "max", "kim", "lea");
  build(nk, ["ann", "bob", "eve", "max", "kim", "lea"], rules.levels[rules.feastFromLevel - 1]);
  const at = now + 40 * day;
  for (let i = 0; i < rules.feastPerMemberWeekly; i++)
    server.clanFeastBring(nk, "ann", dish, at + i);
  assert.throws(() => server.clanFeastBring(nk, "ann", dish, at + 10), /weekly limit/);
});

test("the camp is read through the rpc", () => {
  const { call } = world("ann");
  const view = call("ann", "clanCampRpc");
  assert.strictEqual(view.points, 0);
  assert.strictEqual(view.feast.open, false);
  assert.throws(() => call("ann", "clanCampGiveRpc", { item: herb, amount: "5" }), /bad amount/);
});
