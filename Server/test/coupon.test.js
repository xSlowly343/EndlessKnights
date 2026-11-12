const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama, logger } = require("./fake-nakama");

const server = loadModule();
const now = Date.UTC(2026, 10, 11, 12, 0);
const hour = 3600000;
const plain = (o) => JSON.parse(JSON.stringify(o));

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  return nk;
}

test("a coupon sends its gift by mail once per player", () => {
  const nk = world("ann", "bob");
  assert.strictEqual(server.couponPut(nk, "rift-2026", { reward: { shards: 500, ingots: 3 } }), "RIFT2026");
  const first = plain(server.couponRedeem(nk, "ann", " rift 2026 ", now));
  assert.strictEqual(first.status, "done");
  assert.deepStrictEqual(first.letter.reward, { shards: 500, ingots: 3, friendship: 0, essence: 0 });
  const mail = plain(server.mailLetters(nk, "ann"));
  assert.strictEqual(mail.length, 1);
  assert.strictEqual(mail[0].kind, "coupon");
  assert.strictEqual(mail[0].title, "RIFT2026");
  assert.strictEqual(plain(server.couponRedeem(nk, "ann", "RIFT2026", now)).status, "used");
  assert.strictEqual(plain(server.couponRedeem(nk, "bob", "RIFT2026", now)).status, "done");
  assert.strictEqual(plain(server.mailLetters(nk, "ann")).length, 1);
});

test("unknown, early, expired and spent coupons give nothing", () => {
  const nk = world("ann", "bob");
  server.couponPut(nk, "SOON", { reward: { shards: 1 }, from: now + hour });
  server.couponPut(nk, "GONE", { reward: { shards: 1 }, until: now });
  server.couponPut(nk, "ONE1", { reward: { shards: 1 }, limit: 1 });
  assert.strictEqual(plain(server.couponRedeem(nk, "ann", "NOPE", now)).status, "unknown");
  assert.strictEqual(plain(server.couponRedeem(nk, "ann", "x", now)).status, "unknown");
  assert.strictEqual(plain(server.couponRedeem(nk, "ann", 5, now)).status, "unknown");
  assert.strictEqual(plain(server.couponRedeem(nk, "ann", "SOON", now)).status, "early");
  assert.strictEqual(plain(server.couponRedeem(nk, "ann", "GONE", now)).status, "expired");
  assert.strictEqual(plain(server.couponRedeem(nk, "ann", "ONE1", now)).status, "done");
  assert.strictEqual(plain(server.couponRedeem(nk, "bob", "ONE1", now)).status, "spent");
  assert.strictEqual(plain(server.mailLetters(nk, "ann")).length, 1);
  assert.strictEqual(plain(server.mailLetters(nk, "bob")).length, 0);
  assert.throws(() => server.couponPut(nk, "ONE1", { reward: { shards: 9 } }), /version check/);
});

test("guessing codes is cut off after ten tries an hour", () => {
  const nk = world("ann");
  server.couponPut(nk, "REAL", { reward: { essence: 2 } });
  for (let i = 0; i < 10; i++)
    assert.strictEqual(plain(server.couponRedeem(nk, "ann", "GUESS" + i, now)).status, "unknown");
  assert.strictEqual(plain(server.couponRedeem(nk, "ann", "REAL", now)).status, "tooMany");
  assert.strictEqual(plain(server.couponRedeem(nk, "ann", "REAL", now + hour)).status, "done");
});

test("players cannot add coupons, the server key can", () => {
  const nk = world("ann");
  assert.throws(() => server.couponPutRpc({ userId: "ann", env: {} }, logger, nk, JSON.stringify({ code: "FREE", reward: { shards: 1 } })), /only the server key/);
  assert.strictEqual(JSON.parse(server.couponPutRpc({ env: {} }, logger, nk, JSON.stringify({ code: "free", reward: { shards: 1 } }))).code, "FREE");
});

test("the mailbox shows a hundred letters, unclaimed first", () => {
  const nk = world("ann");
  for (let i = 0; i < 130; i++)
    server.mailSend(nk, "ann", { id: "l" + i, kind: "coupon", at: now + i, place: 0, of: 0, depth: 0, title: "", reward: { shards: 1, ingots: 0 }, claimed: i >= 20, claimedAt: 0 });
  const mail = plain(server.mailLetters(nk, "ann"));
  assert.strictEqual(mail.length, 100);
  assert.strictEqual(mail.filter((l) => !l.claimed).length, 20);
  assert.strictEqual(mail[0].id, "l19");
});
