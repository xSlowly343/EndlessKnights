const fs = require("fs");
const path = require("path");

const root = path.join(__dirname, "..");
const data = path.join(root, "..", "Assets", "StreamingAssets", "Data");
const party = JSON.parse(fs.readFileSync(path.join(data, "party.json"), "utf8"));
const stages = JSON.parse(fs.readFileSync(path.join(data, "stages.json"), "utf8"));
const rift = JSON.parse(fs.readFileSync(path.join(data, "rift.json"), "utf8"));
const intake = JSON.parse(fs.readFileSync(path.join(root, "config", "intake.json"), "utf8"));
const days = ["sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday"];

function enemiesOnFirstStage(waves) {
  let total = 0;
  for (let wave = 0; wave < waves.count; wave++)
    total += Math.min(waves.maxEnemiesPerWave, waves.firstEnemies + wave * waves.enemiesPerWave);
  return total;
}

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
  minBossTimerRate: party.timer.minBossRate,
  worldSwitchSeconds: party.worldSwitchSeconds,
  repeatMaxSeconds: party.timer.repeatMaxSeconds,
  repeatCooldownSeconds: party.timer.repeatCooldownSeconds,
  minShare: party.contribution.minShare,
  activeInputSeconds: party.contribution.activeInputSeconds,
  restBankSeconds: party.contribution.restBankMinutes * 60,
  idlePortionShare: party.contribution.idlePortionShare,
  typingReserveShare: party.contribution.typingReserveShare,
  barsPerSecond: party.caps.barsPerSecond,
  burstBars: party.caps.burstBars,
  hitBurstSeconds: party.caps.hitBurstSeconds,
  flagsBeforeReport: party.caps.flagsBeforeReport,
  reactionCount: party.reactions.ids.length,
  reactionCooldownSeconds: party.reactions.cooldownSeconds,
  enemiesPerStage: enemiesOnFirstStage(stages.waves),
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

const directoryRules = {
  maxMembers: party.maxMembers,
  codeLength: party.search.codeLength,
  inactiveDays: party.search.inactiveDays,
  listLimit: party.search.listLimit,
  titles: party.search.titles,
  roles: party.search.roles,
  languages: party.search.languages,
  reportReasons: party.search.reportReasons,
  timeSlots: party.search.timeSlots
};

if (intake.version !== 1)
  throw new Error("config/intake.json: version " + intake.version + " is not supported");
const intakeRules = {
  telemetry: intake.telemetry,
  errors: intake.errors,
  summary: intake.summary,
  characters: Object.keys(party.search.classRoles)
};

const code = fs.readFileSync(path.join(root, "build", "party.js"), "utf8");
fs.writeFileSync(path.join(root, "build", "index.js"), "var PartyServerRules = " + JSON.stringify(rules) + ";\nvar RiftServerRules = " + JSON.stringify(riftRules) + ";\nvar PartyDirectoryRules = " + JSON.stringify(directoryRules) + ";\nvar IntakeRules = " + JSON.stringify(intakeRules) + ";\n" + code);
