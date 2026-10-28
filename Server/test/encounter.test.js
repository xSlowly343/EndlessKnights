const test = require("node:test");
const assert = require("node:assert");
const { loadModule, readVectors } = require("./load");

const party = loadModule();
const vectors = readVectors("party-encounter.json");

function close(actual, expected, where) {
  const tolerance = 1e-12 * Math.max(1, Math.abs(expected));
  assert.ok(Math.abs(actual - expected) <= tolerance, `${where}: ${actual} is not ${expected}`);
}

function run(encounter, step) {
  switch (step.op) {
    case "join": return encounter.join(step.player);
    case "leave": return encounter.leave(step.slot);
    case "input": return encounter.reportInput(step.slot);
    case "peaceful": return encounter.setPeaceful(step.slot, step.value);
    case "connected": return encounter.setConnected(step.slot, step.value);
    case "damage": return encounter.applyDamage(step.slot, step.enemyId, step.share);
    case "effect": return encounter.applyEnemyEffect(step.id, step.damageTaken, step.seconds, -1, step.timerSlow);
    case "repeat": return encounter.repeatRecent(step.slot, step.seconds);
    case "requestBoss": return encounter.requestBossFight();
    case "proposeWorld": return encounter.proposeWorld(step.world, step.riftId);
    case "cancelWorld": return encounter.cancelWorld();
    case "switchWorld": return encounter.switchWorld(step.world, step.riftId);
    case "advance":
      for (let i = 0; i < step.ticks; i++)
        encounter.advance();
      return undefined;
    default: throw new Error("unknown op " + step.op);
  }
}

function compare(encounter, expect, where) {
  for (const key of ["tick", "enemyId", "enemyKind", "killsOnStage", "farming", "farmKills", "bossTicksLeft", "fighters", "world", "riftId", "pendingWorld", "switchTicksLeft"])
    assert.strictEqual(encounter[key], expect[key], `${where}: ${key}`);
  assert.strictEqual(encounter.enemySeed, expect.enemySeed, `${where}: enemySeed`);
  close(encounter.barFull, expect.barFull, `${where}: barFull`);
  close(encounter.barLeft, expect.barLeft, `${where}: barLeft`);
  close(encounter.damageTakenMultiplier(), expect.damageTaken, `${where}: damageTaken`);
  close(encounter.bossTimerRate(), expect.bossTimerRate, `${where}: bossTimerRate`);
  close(encounter.loopShare, expect.loopShare, `${where}: loopShare`);
  close(encounter.savedBossTicks, expect.savedBossTicks, `${where}: savedBossTicks`);
  expect.members.forEach((member, slot) => {
    const actual = encounter.members[slot];
    if (member === null) {
      assert.strictEqual(actual.joined, false, `${where}: slot ${slot} should be empty`);
      return;
    }
    assert.strictEqual(actual.playerId, member.player, `${where}: slot ${slot} player`);
    assert.strictEqual(actual.weight, member.weight, `${where}: slot ${slot} weight`);
    assert.strictEqual(encounter.presence(slot), member.presence, `${where}: slot ${slot} presence`);
    assert.strictEqual(encounter.typing(slot), member.typing, `${where}: slot ${slot} typing`);
    close(actual.enemyShare, member.share, `${where}: slot ${slot} share`);
    assert.strictEqual(actual.restBankTicks, member.restBank, `${where}: slot ${slot} rest bank`);
  });
  assert.strictEqual(encounter.events.length, expect.events.length, `${where}: event count`);
  expect.events.forEach((event, i) => {
    const actual = encounter.events[i];
    assert.strictEqual(actual.kind, event.kind, `${where}: event ${i} kind`);
    assert.strictEqual(actual.tick, event.tick, `${where}: event ${i} tick`);
    assert.strictEqual(actual.enemyId, event.enemyId, `${where}: event ${i} enemy`);
    assert.strictEqual(actual.enemyKind, event.enemyKind, `${where}: event ${i} enemy kind`);
    assert.strictEqual(actual.rewarded, event.rewarded, `${where}: event ${i} rewarded`);
    event.shares.forEach((share, slot) => close(actual.shares[slot], share, `${where}: event ${i} share ${slot}`));
    close(actual.loop, event.loop, `${where}: event ${i} loop`);
    close(actual.saved, event.saved, `${where}: event ${i} saved`);
  });
  encounter.events.length = 0;
}

test("the rules built into the server match the core", () => {
  for (const key of Object.keys(vectors.rules))
    assert.strictEqual(party.PartyServerRules[key], vectors.rules[key], key);
});

for (const scenario of vectors.scenarios) {
  test(scenario.name, () => {
    const encounter = new party.PartyEncounter(vectors.rules, scenario.seed);
    scenario.steps.forEach((step, index) => {
      const where = `${scenario.name}, step ${index} (${step.op})`;
      const result = run(encounter, step);
      if (typeof step.result === "boolean")
        assert.strictEqual(result, step.result, `${where}: result`);
      else if (step.op === "damage" || step.op === "join" || step.op === "repeat")
        close(result, step.result, `${where}: result`);
      compare(encounter, step.expect, where);
    });
  });
}
