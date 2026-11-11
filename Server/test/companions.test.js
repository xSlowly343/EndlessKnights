const test = require("node:test");
const assert = require("node:assert");
const { loadModule } = require("./load");
const { FakeNakama } = require("./fake-nakama");

const server = loadModule();
const rules = server.CompanionServerRules;
const plain = (o) => JSON.parse(JSON.stringify(o));
const hour = 3600000;
const now = 1800000000000;

function world(...names) {
  const nk = new FakeNakama();
  names.forEach((name, i) => nk.addUser(name, name, "7656119800000000" + i));
  const call = (user, rpc, request) => JSON.parse(server[rpc]({ userId: user, env: {} }, null, nk, request === undefined ? "" : JSON.stringify(request)));
  const created = call(names[0], "partyCreateRpc", {});
  for (const name of names.slice(1))
    call(name, "partyJoinRpc", { code: created.code });
  return { nk, call };
}

const friendship = (nk, user) => server.walletRead(nk, user).value.balances.friendship;
const claim = (nk, user, time) => plain(server.hideoutClaim(nk, user, time).items);

test("rules come from companions.json", () => {
  assert.strictEqual(rules.companions.camp_dog, "green_hills");
  assert.strictEqual(rules.petsPerDay, 3);
  assert.strictEqual(rules.farTripMs, 24 * hour);
  assert.ok(rules.shopIds.indexOf("egg_griffin") >= 0 && rules.shopIds.indexOf("camp_dog") < 0);
});

test("each member places one companion and sees the others", () => {
  const { nk } = world("ann", "bob");
  server.companionPlace(nk, "ann", "camp_dog", "Rex", 1, now);
  server.companionPlace(nk, "ann", "camp_crow", "", 0, now + 1);
  const view = plain(server.companionPlace(nk, "bob", "pet_owl", "Hoot", 0, now + 2));
  assert.deepStrictEqual(view.companions.pets.map((pet) => [pet.owner, pet.companion]), [["ann", "camp_crow"], ["bob", "pet_owl"]]);
  assert.throws(() => server.companionPlace(nk, "bob", "dragon", "", 0, now), /unknown companion/);
  assert.throws(() => server.companionPlace(nk, "bob", "pet_owl", "x".repeat(rules.nameLength + 1), 0, now), /bad name/);
  assert.throws(() => server.companionPlace(nk, "bob", "pet_owl", "", rules.stages, now), /bad request/);
  const gone = plain(server.companionPlace(nk, "bob", null, null, null, now + 3));
  assert.deepStrictEqual(gone.companions.pets.map((pet) => pet.owner), ["ann"]);
});

test("petting a guest gives a sign to the petter and a bond to the owner, three times a day", () => {
  const { nk } = world("ann", "bob");
  server.companionPlace(nk, "ann", "camp_dog", "Rex", 0, now);
  assert.throws(() => server.companionPet(nk, "ann", "ann", now), /friend's companion/);
  assert.throws(() => server.companionPet(nk, "ann", "bob", now), /no companion there/);
  for (let i = 0; i < 3; i++)
    assert.strictEqual(plain(server.companionPet(nk, "bob", "ann", now + i)).granted, 1);
  assert.throws(() => server.companionPet(nk, "bob", "ann", now + 10), /daily pet limit/);
  assert.strictEqual(friendship(nk, "bob"), 3);
  assert.deepStrictEqual(claim(nk, "ann", now + 11), { "companion_pet:camp_dog": 3 });
  assert.strictEqual(plain(server.companionPet(nk, "bob", "ann", now + 24 * hour)).granted, 1, "a new day opens the pets again");
});

test("a friend warms an egg once a day", () => {
  const { nk } = world("ann", "bob", "cid");
  server.companionWarm(nk, "bob", "ann", now);
  assert.throws(() => server.companionWarm(nk, "bob", "ann", now + 1), /warmed this egg today/);
  server.companionWarm(nk, "cid", "ann", now + 2);
  assert.throws(() => server.companionWarm(nk, "ann", "ann", now), /friend's egg/);
  assert.deepStrictEqual(claim(nk, "ann", now + 3), { companion_warm: 2 });
  server.companionWarm(nk, "bob", "ann", now + 24 * hour);
});

test("the far party trip needs two companions, takes a day and pays everyone", () => {
  const { nk } = world("ann", "bob", "cid");
  server.companionFarJoin(nk, "ann", "camp_dog", now);
  assert.throws(() => server.companionFarStart(nk, "ann", now), /needs more companions/);
  server.companionFarJoin(nk, "bob", "egg_griffin", now);
  server.companionFarJoin(nk, "cid", "pet_firefly", now);
  assert.throws(() => server.companionFarStart(nk, "nobody", now), /not in a party/);
  const started = plain(server.companionFarStart(nk, "bob", now + 1));
  assert.strictEqual(started.companions.far.running, true);
  assert.deepStrictEqual(started.companions.far.biomes, ["green_hills", "sky_islands"]);
  assert.throws(() => server.companionFarJoin(nk, "ann", "camp_crow", now + 2), /on the road/);
  assert.throws(() => server.companionFarClaim(nk, "ann", now + hour), /nothing to claim/);
  const back = now + 1 + rules.farTripMs;
  const got = plain(server.companionFarClaim(nk, "ann", back));
  const rare = Math.floor(3 * rules.rarePerCompanion * (1 + rules.rarePerBiome));
  assert.strictEqual(got.items.bone_hills, 2 * rules.bonesPerCompanion);
  assert.strictEqual(got.items.bone_sky, rules.bonesPerCompanion);
  assert.strictEqual(Object.keys(got.items).filter((id) => id.indexOf("fish_") === 0).reduce((sum, id) => sum + got.items[id], 0), rare);
  assert.strictEqual(got.granted, 5);
  assert.strictEqual(got.items.companion_soul, 5, "five souls of the underside each");
  assert.throws(() => server.companionFarClaim(nk, "ann", back), /already claimed/);
  assert.throws(() => server.companionFarJoin(nk, "ann", "camp_crow", back), /not claimed yet/);
  server.companionFarClaim(nk, "bob", back);
  server.companionFarClaim(nk, "cid", back);
  server.companionFarJoin(nk, "ann", "camp_crow", back);
  server.companionFarJoin(nk, "bob", "camp_dog", back);
  assert.throws(() => server.companionFarStart(nk, "ann", now + 2), /already went today/);
});

test("a party trip counts for the hideout and does not wait for those who left or stay away", () => {
  const { nk, call } = world("ann", "bob", "cid");
  server.companionFarJoin(nk, "ann", "camp_dog", now);
  server.companionFarJoin(nk, "bob", "camp_dog", now);
  server.companionFarJoin(nk, "cid", "camp_dog", now);
  server.companionFarStart(nk, "ann", now + 1);
  assert.strictEqual(server.hideoutRead(nk, server.partyGroupOfUser(nk, "ann").id).value.counters.partyTrips, 1);
  const back = now + 1 + rules.farTripMs;
  server.companionFarClaim(nk, "ann", back);
  call("cid", "partyLeaveRpc", {});
  assert.throws(() => server.companionFarJoin(nk, "ann", "camp_crow", back + 1), /not claimed yet/);
  server.companionFarJoin(nk, "ann", "camp_crow", back + rules.farTripMs);
  assert.strictEqual(claim(nk, "bob", back + rules.farTripMs + 1).bone_hills, 3 * rules.bonesPerCompanion);
});

test("a party trip holds at most four companions", () => {
  const { nk } = world("ann", "bob", "cid", "dan");
  for (const name of ["ann", "bob", "cid", "dan"])
    server.companionFarJoin(nk, name, "camp_dog", now);
  const view = plain(server.companionFarJoin(nk, "ann", null, now + 1));
  assert.strictEqual(view.companions.far.sent.length, 3);
});

test("two friends asleep by the fire give a sign a day to everyone there", () => {
  const { nk } = world("ann", "bob");
  server.companionPlace(nk, "ann", "camp_dog", "", 0, now);
  server.companionPlace(nk, "bob", "camp_crow", "", 0, now);
  server.hideoutPresence(nk, "ann", true, now + 1);
  server.hideoutPresence(nk, "bob", true, now + 2);
  server.hideoutPresence(nk, "ann", true, now + 3);
  assert.strictEqual(server.walletRead(nk, "ann").value.today.companion_pair, 1);
  assert.strictEqual(server.walletRead(nk, "bob").value.today.companion_pair, 1);
});

test("the companion shop takes friendship signs once per companion a week", () => {
  const { nk } = world("ann");
  assert.throws(() => server.companionShopBuy(nk, "ann", "egg_griffin", now), /not enough friendship/);
  server.walletRefund(nk, "ann", "friendship", 500, "test", now);
  const bought = plain(server.companionShopBuy(nk, "ann", "egg_griffin", now));
  assert.strictEqual(bought.friendship, 500 - rules.shopPrice);
  assert.throws(() => server.companionShopBuy(nk, "ann", "egg_griffin", now + 1), /already bought/);
  assert.throws(() => server.companionShopBuy(nk, "ann", "camp_dog", now), /not in the shop/);
  server.companionShopBuy(nk, "ann", "egg_griffin", now + rules.shopDays * 24 * hour);
});

test("a character companion costs more in the shop", () => {
  const { nk } = world("ann");
  assert.strictEqual(rules.shopPrices.rune_stoat, 300);
  server.walletRefund(nk, "ann", "friendship", 500, "test", now);
  const bought = plain(server.companionShopBuy(nk, "ann", "rune_stoat", now));
  assert.strictEqual(bought.friendship, 200);
});

test("a leaving member takes their companion away", () => {
  const { nk, call } = world("ann", "bob");
  server.companionPlace(nk, "bob", "camp_dog", "", 0, now);
  call("bob", "partyLeaveRpc", {});
  const view = plain(server.companionPlace(nk, "ann", "camp_crow", "", 0, now + 1));
  assert.deepStrictEqual(view.companions.pets.map((pet) => pet.owner), ["ann"]);
});
