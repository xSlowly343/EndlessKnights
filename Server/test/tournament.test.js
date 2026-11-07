const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.TournamentServerRules;
const day = 86400000;
const hour = 3600000;
const plain = (o) => JSON.parse(JSON.stringify(o));
const monday = server.seasonPassWeekStart(3000);

function world(count) {
  const nk = new FakeNakama();
  const names = [];
  for (let i = 0; i < count; i++) {
    const name = "p" + i;
    names.push(name);
    nk.addUser(name, name, "7656119800000" + String(1000 + i));
  }
  return { nk, names };
}

const join = (nk, user, record, at) => plain(server.tournamentJoin(nk, user, record, at));
const submit = (nk, user, index, score, at) => plain(server.tournamentSubmit(nk, user, { index: index, score: score }, at));
const mail = (nk, user) => plain(server.mailLetters(nk, user));

test("tournaments start on the listed days and follow each other without gaps", () => {
  const first = server.tournamentIndexOf(monday + hour);
  assert.strictEqual(server.tournamentStartsAt(first), monday + rules.startDays[0] * day);
  assert.strictEqual(server.tournamentIndexOf(monday - 1), first - 1, "sunday night belongs to the last tournament of the previous week");
  for (let i = first - 2; i < first + 6; i++) {
    assert.strictEqual(server.tournamentEndsAt(i), server.tournamentStartsAt(i + 1));
    assert.strictEqual(server.tournamentIndexOf(server.tournamentStartsAt(i)), i);
    assert.strictEqual(server.tournamentIndexOf(server.tournamentEndsAt(i) - 1), i);
  }
  assert.strictEqual(server.tournamentStartsAt(first + rules.startDays.length), monday + 7 * day);
  const formats = new Set();
  for (let i = first; i < first + rules.formats.length; i++)
    formats.add(server.tournamentFormat(i).id);
  assert.strictEqual(formats.size, rules.formats.length, "every format comes in turn");
});

test("players are split into groups by record band and group size", () => {
  const { nk, names } = world(rules.groupSize + 2);
  const at = monday + hour;
  const views = names.map((name, i) => join(nk, name, i === 0 ? 60000 : 3, at));
  assert.strictEqual(views[0].band, rules.bands.length - 1);
  assert.strictEqual(views[0].group, 0, "a high record has its own band");
  assert.strictEqual(views[1].band, 0);
  assert.strictEqual(views[1].group, 0);
  assert.strictEqual(views[rules.groupSize].group, 0);
  assert.strictEqual(views[rules.groupSize + 1].group, 1, "the group is full, the next player opens a new one");
  assert.deepStrictEqual(join(nk, names[1], 3, at + 1), plain(server.tournamentView(nk, names[1], at + 1, [])), "joining twice changes nothing");
});

test("scores are kept as the best one and checked against the format", () => {
  const { nk } = world(2);
  const at = monday + hour;
  const index = join(nk, "p0", 10, at).index;
  const format = server.tournamentFormat(index);
  assert.strictEqual(submit(nk, "p0", index, 40, at).mine.score, 40);
  assert.strictEqual(submit(nk, "p0", index, 25, at).mine.score, 40);
  assert.throws(() => submit(nk, "p0", index, format.maxScore + 1, at), /bad score/);
  assert.throws(() => submit(nk, "p0", index, 1.5, at), /bad score/);
  assert.throws(() => submit(nk, "p0", index - 1, 10, at), /not this tournament/);
  assert.throws(() => submit(nk, "p1", index, 10, at), /not joined/);
  assert.throws(() => server.tournamentJoin(nk, "p1", 0, at), /bad record/);
});

test("joining closes some hours before the end", () => {
  const { nk } = world(1);
  const index = server.tournamentIndexOf(monday + hour);
  const late = server.tournamentEndsAt(index) - rules.joinClosesMs;
  assert.throws(() => server.tournamentJoin(nk, "p0", 10, late), /joining is closed/);
  assert.strictEqual(join(nk, "p0", 10, late - 1).joined, true);
});

test("after the end places are paid by mail once, scaled by the band", () => {
  const { nk, names } = world(4);
  const at = monday + hour;
  const index = server.tournamentIndexOf(at);
  names.forEach((name) => join(nk, name, name === "p3" ? 60000 : 50, at));
  submit(nk, "p0", index, 30, at);
  submit(nk, "p1", index, 50, at);
  submit(nk, "p3", index, 5, at);
  assert.deepStrictEqual(plain(server.tournamentSettle(nk, logger, "p1", at + hour)), [], "nothing before the end");

  const after = server.tournamentEndsAt(index) + 1;
  assert.deepStrictEqual(plain(server.tournamentSettle(nk, logger, "p1", after)), ["tournament-" + index]);
  assert.deepStrictEqual(plain(server.tournamentSettle(nk, logger, "p1", after + 1)), [], "paid once");
  const first = mail(nk, "p1")[0];
  assert.strictEqual(first.kind, "tournament");
  assert.strictEqual(first.place, 1);
  assert.strictEqual(first.of, 2);
  assert.strictEqual(first.depth, 50);
  assert.strictEqual(first.title, server.tournamentFormat(index).id);
  assert.strictEqual(first.reward.ingots, rules.rewards[0].ingots);
  assert.strictEqual(first.reward.essence, rules.rewards[0].essence);

  server.tournamentSettle(nk, logger, "p0", after);
  assert.strictEqual(mail(nk, "p0")[0].place, 2);
  assert.strictEqual(server.tournamentSettle(nk, logger, "p2", after).length, 0, "no score, no reward");
  assert.strictEqual(mail(nk, "p2").length, 0);

  server.tournamentSettle(nk, logger, "p3", after);
  const top = rules.bands[rules.bands.length - 1].scale;
  assert.strictEqual(mail(nk, "p3")[0].reward.ingots, Math.round(rules.rewards[0].ingots * top), "alone in the top band and paid by its scale");
});

test("the mail list pays finished tournaments too", () => {
  const { nk } = world(1);
  const at = monday + hour;
  const index = server.tournamentIndexOf(at);
  join(nk, "p0", 10, at);
  submit(nk, "p0", index, 7, at);
  const realNow = Date.now;
  server.Date.now = () => server.tournamentEndsAt(index) + 1;
  try {
    const list = JSON.parse(server.mailListRpc({ userId: "p0", env: {} }, logger, nk, ""));
    assert.strictEqual(list.letters.filter((letter) => letter.kind === "tournament").length, 1);
  } finally {
    server.Date.now = realNow;
  }
});
