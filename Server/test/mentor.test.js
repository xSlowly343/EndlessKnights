const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const rules = server.MentorServerRules;
const day = 86400;
const hour = 3600;
const plain = (o) => JSON.parse(JSON.stringify(o));
const nowSeconds = () => Math.floor(Date.now() / 1000);

function world(people) {
  const nk = new FakeNakama();
  Object.keys(people).forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i, nowSeconds() - people[name] * day));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, logger, nk, request === undefined ? "" : JSON.stringify(request)));
  const fails = (user, rpc, request, message) => assert.throws(() => call(user, rpc, request), (error) => error.message === message, rpc + " should fail with " + message);
  return { nk, call, fails };
}

const together = (nk, a, b, seconds) => server.mentorTogether(nk, logger, { [[a, b].sort().join("|")]: seconds }, Date.now());

test("the first week is the student's, a month makes a mentor who said so", () => {
  const { call, fails } = world({ kid: 2, vet: 40, mid: 10 });
  assert.strictEqual(call("kid", "mentorStatusRpc").role, "student");
  assert.ok(call("kid", "mentorStatusRpc").studentUntil > Date.now());
  fails("mid", "mentorReadyRpc", { ready: true }, "too new to mentor");
  assert.strictEqual(call("vet", "mentorStatusRpc").role, "none");
  assert.strictEqual(call("vet", "mentorStatusRpc").canMentor, true);
  assert.strictEqual(call("vet", "mentorReadyRpc", { ready: true }).role, "mentor");
  fails("vet", "mentorReadyRpc", { ready: "yes" }, "bad request");
});

test("party search shows mentors to students and quick match brings a student to a mentor", () => {
  const { call } = world({ kid: 1, vet: 60, other: 60, host: 60 });
  call("vet", "mentorReadyRpc", { ready: true });
  const plainParty = call("other", "partyCreateRpc", { access: "open", language: "ru" });
  const mentorParty = call("host", "partyCreateRpc", { access: "open", language: "ru" });
  call("vet", "partyJoinRpc", { partyId: mentorParty.id });
  const wanted = call("kid", "partyListRpc", { language: "ru", mentor: "want" }).parties;
  assert.deepStrictEqual(plain(wanted.map((p) => p.id)), [mentorParty.id]);
  assert.strictEqual(wanted[0].hasMentor, true);
  assert.strictEqual(call("vet", "partyListRpc", { language: "ru", mentor: "offer" }).parties.length, 0);
  const joined = call("kid", "partyQuickRpc", { language: "ru", role: "damage" });
  assert.strictEqual(joined.id, mentorParty.id);
  const flags = Object.fromEntries(joined.members.map((m) => [m.userId, [m.student, m.mentor]]));
  assert.deepStrictEqual(plain(flags), { host: [false, false], vet: [false, true], kid: [true, false] });
  assert.ok(plainParty.id);
});

test("an hour together pairs them and pays both; later hours pay on", () => {
  const { nk, call } = world({ kid: 1, vet: 60 });
  call("vet", "mentorReadyRpc", { ready: true });
  together(nk, "kid", "vet", hour - 1);
  assert.strictEqual(call("kid", "mentorStatusRpc").level, 0);
  assert.strictEqual(call("kid", "mentorStatusRpc").partner.userId, "vet", "the first fight together pairs them");
  assert.strictEqual(call("vet", "mentorStatusRpc").partner.userId, "kid");
  together(nk, "kid", "vet", 1);
  const student = call("kid", "mentorStatusRpc");
  assert.strictEqual(student.level, 1);
  assert.strictEqual(server.walletRead(nk, "vet").value.balances.friendship, rules.rewards[0].mentor.friendship);
  const letters = call("kid", "mailListRpc").letters;
  assert.deepStrictEqual(plain(letters.map((l) => [l.kind, l.reward.uncutGems])), [["mentor", rules.rewards[0].student.uncutGems]]);
  together(nk, "kid", "vet", 20 * hour);
  assert.strictEqual(call("kid", "mentorStatusRpc").level, rules.rewards.length);
  const mentor = call("vet", "mentorStatusRpc");
  assert.deepStrictEqual(plain(mentor.perks), ["mentor_cloak"]);
  assert.strictEqual(mentor.graduates, 1);
  assert.strictEqual(mentor.coopPoints, 1);
  assert.deepStrictEqual(plain(call("kid", "mentorStatusRpc").perks), ["early_book_slot", "graduate_frame"]);
});

test("a mentor keeps one student for that student's week; two veterans or two students earn nothing", () => {
  const { nk, call } = world({ kid: 1, kid2: 2, vet: 60, vet2: 90 });
  call("vet", "mentorReadyRpc", { ready: true });
  call("vet2", "mentorReadyRpc", { ready: true });
  together(nk, "kid", "vet", 60);
  together(nk, "kid2", "vet", 2 * hour);
  assert.strictEqual(call("kid2", "mentorStatusRpc").seconds, 0, "vet is busy with kid this week");
  together(nk, "kid2", "vet2", 60);
  assert.strictEqual(call("kid2", "mentorStatusRpc").partner.userId, "vet2");
  together(nk, "vet", "vet2", 5 * hour);
  together(nk, "kid", "kid2", 5 * hour);
  assert.strictEqual(call("kid", "mentorStatusRpc").seconds, 60);
  assert.strictEqual(server.walletRead(nk, "vet2").value.balances.friendship, 0);
});

test("the mentor marks up to ten tree nodes for the student", () => {
  const { nk, call, fails } = world({ kid: 1, vet: 60 });
  call("vet", "mentorReadyRpc", { ready: true });
  fails("vet", "mentorMarksRpc", { marks: ["a"] }, "no student");
  together(nk, "kid", "vet", 60);
  fails("vet", "mentorMarksRpc", { marks: new Array(rules.maxMarks + 1).fill("n").map((n, i) => n + i) }, "bad request");
  fails("vet", "mentorMarksRpc", { marks: ["<b>"] }, "bad request");
  call("vet", "mentorMarksRpc", { marks: ["str_12", "dex_40", "str_12"] });
  assert.deepStrictEqual(plain(call("kid", "mentorStatusRpc").marks), ["str_12", "dex_40"]);
  assert.deepStrictEqual(plain(call("vet", "mentorStatusRpc").marks), ["str_12", "dex_40"]);
});

test("the host counts seconds for every pair fighting together", () => {
  const host = new server.PartyHost(server.PartyServerRules, "5");
  host.join("user-b", "12");
  host.join("user-a", "11");
  host.join("user-c", "13");
  for (let i = 0; i < 20; i++) {
    host.encounter.reportInput(0);
    host.encounter.reportInput(1);
    host.tick();
  }
  const pairs = plain(host.takePairTicks());
  assert.ok(pairs["user-a|user-b"] > 0);
  assert.strictEqual(pairs["user-a|user-c"], pairs["user-a|user-b"], "a member who just joined counts as fighting at first");
  assert.deepStrictEqual(plain(host.takePairTicks()), {});
});
