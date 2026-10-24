const fs = require("fs");
const path = require("path");

const root = path.join(__dirname, "..");
const data = path.join(root, "..", "Assets", "StreamingAssets", "Data");
const party = JSON.parse(fs.readFileSync(path.join(data, "party.json"), "utf8"));
const stages = JSON.parse(fs.readFileSync(path.join(data, "stages.json"), "utf8"));
const rift = JSON.parse(fs.readFileSync(path.join(data, "rift.json"), "utf8"));
const days = ["sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday"];

const rules = {
  maxMembers: party.maxMembers,
  hpPerExtraPlayer: party.hpPerExtraPlayer,
  restAfterSeconds: party.presence.restAfterSeconds,
  disconnectGraceSeconds: party.presence.disconnectGraceSeconds,
  silenceSeconds: party.presence.silenceSeconds,
  presenceSeconds: party.packets.presenceSeconds,
  stateBroadcastSeconds: party.packets.stateBroadcastSeconds,
  maxMessagesPerSecond: party.packets.maxMessagesPerSecond,
  lateDamageSeconds: party.lateDamageSeconds,
  maxEnemyDamageTaken: party.maxEnemyDamageTaken,
  enemiesPerStage: stages.enemiesPerStage,
  bossRetryAfterKills: stages.boss.retryAfterKills,
  bossTimerSeconds: stages.boss.timerSeconds,
  bossHpMultiplier: stages.boss.hpMultiplier
};

const riftRules = {
  windows: rift.schedule.windows.map((window) => {
    const day = days.indexOf(window.day);
    if (day < 0)
      throw new Error("rift.json: unknown day " + window.day);
    return { day: day, hourUtc: window.hourUtc };
  }),
  durationMs: Math.round(rift.schedule.durationHours * 3600 * 1000),
  announceMs: Math.round(rift.schedule.announceMinutes * 60 * 1000),
  modifiers: rift.modifiers.map((modifier) => modifier.id),
  modifiersPerWindow: rift.modifiersPerWindow
};

const code = fs.readFileSync(path.join(root, "build", "party.js"), "utf8");
fs.writeFileSync(path.join(root, "build", "index.js"), "var PartyServerRules = " + JSON.stringify(rules) + ";\nvar RiftServerRules = " + JSON.stringify(riftRules) + ";\n" + code);
