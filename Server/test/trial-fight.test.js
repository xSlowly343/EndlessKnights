const test = require("node:test");
const assert = require("node:assert");
const { loadModule, readVectors } = require("./load");

const server = loadModule();
const vectors = readVectors("party-trial.json");
const rules = server.PartyGrowthServerRules.trials.fight;

function hitAt(tick, fighter) {
  return {
    keys: ((tick * 7 + fighter * 3) % 11) * 0.000025,
    mouse: ((tick * 5 + fighter) % 7) * 0.000017,
    skills: ((tick * 3 + fighter * 5) % 13) * 0.0000125,
    minions: ((tick + fighter * 2) % 5) * 0.0000085,
    typing: (tick + fighter) % 17 !== 0,
    armorPen: 0,
    shred: 0
  };
}

function close(actual, expected, label) {
  const scale = Math.max(1, Math.abs(expected));
  assert.ok(Math.abs(actual - expected) <= 1e-12 * scale, label + ": " + actual + " vs " + expected);
}

test("the fight rules carry every trial and complication", () => {
  assert.strictEqual(rules.list.length, 8);
  assert.strictEqual(rules.complications.length, 5);
  assert.strictEqual(rules.list[7].waves, 30);
  assert.ok(rules.defense.k > 0);
});

test("the trial id packs like the core", () => {
  const id = server.partyTrialIdPack(7, 35, true);
  assert.strictEqual(server.partyTrialIdIndex(id), 7);
  assert.strictEqual(server.partyTrialIdTier(id), 35);
  assert.strictEqual(server.partyTrialIdNight(id), true);
  assert.strictEqual(id, 7 + 1 + 35 * 256 + 16777216);
});

for (const scenario of vectors.scenarios) {
  test("trial " + rules.list[scenario.trial].id + " tier " + scenario.tier + " fights like the core", () => {
    const fight = new server.PartyTrialFight();
    fight.start(rules, scenario.trial, scenario.tier, vectors.fighters, scenario.power, scenario.night);
    let next = 0;
    while (fight.running()) {
      const hits = [];
      for (let f = 0; f < vectors.fighters; f++)
        hits.push(hitAt(fight.tick, f));
      fight.advance(rules, hits, vectors.fighters);
      if (fight.tick % vectors.every !== 0 && fight.running())
        continue;
      const check = scenario.checks[next++];
      const label = "tick " + check.tick;
      assert.strictEqual(fight.tick, check.tick, label);
      assert.strictEqual(fight.outcome, check.outcome, label + " outcome");
      close(fight.hpLeft, check.hpLeft, label + " hp left");
      close(fight.hpFull, check.hpFull, label + " hp full");
      for (let kind = 0; kind < 4; kind++)
        close(fight.shields[kind], check.shields[kind], label + " shield " + kind);
      close(fight.skillShieldLeft, check.skillShield, label + " skill shield");
      assert.strictEqual(fight.holder, check.holder, label + " holder");
      assert.strictEqual(fight.resisted, check.resisted, label + " resisted");
      assert.strictEqual(fight.chain, check.chain, label + " chain");
      assert.strictEqual(fight.wave, check.wave, label + " wave");
      assert.strictEqual(fight.wavesCleared, check.wavesCleared, label + " waves cleared");
      close(fight.armor, check.armor, label + " armor");
    }
    assert.strictEqual(next, scenario.checks.length);
    assert.strictEqual(fight.resultTier(rules), scenario.result);
  });
}

test("the trial state encodes like the core", () => {
  const state = vectors.state;
  const encounter = new server.PartyEncounter(server.PartyServerRules, "42");
  encounter.join("ann");
  encounter.join("bob");
  const index = rules.list.map((t) => t.id).indexOf(state.trial);
  assert.ok(encounter.proposeWorld(3, server.partyTrialIdPack(index, state.tier, state.night)));
  for (let i = 0; i < server.PartyServerRules.worldSwitchSeconds * 10; i++)
    encounter.advance();
  encounter.reportInput(0);
  encounter.reportInput(1);
  encounter.addTrialDamage(0, state.keys, 0, 0, 0);
  encounter.advance();
  const bytes = new Uint8Array(server.partyEncodeTrialState(encounter));
  const hex = Array.from(bytes).map((b) => b.toString(16).padStart(2, "0")).join("");
  assert.strictEqual(hex, state.hex);
});
