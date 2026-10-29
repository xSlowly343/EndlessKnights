const fs = require("fs");
const path = require("path");

const root = path.join(__dirname, "..");
const data = path.join(root, "..", "Assets", "StreamingAssets", "Data");
const party = JSON.parse(fs.readFileSync(path.join(data, "party.json"), "utf8"));
const stages = JSON.parse(fs.readFileSync(path.join(data, "stages.json"), "utf8"));
const rift = JSON.parse(fs.readFileSync(path.join(data, "rift.json"), "utf8"));
const intake = JSON.parse(fs.readFileSync(path.join(root, "config", "intake.json"), "utf8"));
const coopCurrency = JSON.parse(fs.readFileSync(path.join(data, "coop_currency.json"), "utf8"));
const mentor = JSON.parse(fs.readFileSync(path.join(data, "mentor.json"), "utf8"));
const clanTree = JSON.parse(fs.readFileSync(path.join(data, "clan_tree.json"), "utf8"));
const challenges = JSON.parse(fs.readFileSync(path.join(data, "clan_challenges.json"), "utf8"));
const raid = JSON.parse(fs.readFileSync(path.join(data, "raid.json"), "utf8"));
const clan = JSON.parse(fs.readFileSync(path.join(data, "clan.json"), "utf8"));
const chronicle = JSON.parse(fs.readFileSync(path.join(data, "party_chronicle.json"), "utf8"));
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
  biomeLength: stages.biomeLength,
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
  modifiersPerWindow: rift.modifiersPerWindow,
  partyJoinMs: Math.round(rift.party.joinMinutes * 60 * 1000),
  partyDurationMs: Math.round(rift.party.durationHours * 3600 * 1000),
  boardUpdateMs: Math.round(rift.board.updateMinutes * 60 * 1000),
  boardShown: rift.board.shown,
  boardPlaces: rift.board.places.map((place) => ({ upTo: place.upTo || 0, shards: place.shards, uncutGems: place.uncutGems, ingots: place.ingots }))
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

if (chronicle.version !== 1)
  throw new Error("party_chronicle.json: version " + chronicle.version + " is not supported");
const chronicleRules = {
  flushSeconds: chronicle.flushSeconds,
  maxRecords: chronicle.maxRecords,
  night: chronicle.night,
  feats: chronicle.feats.map((feat) => ({
    id: feat.id, kind: feat.kind, levels: feat.levels, banner: feat.banner || [], title: feat.title || null, tracked: feat.tracked !== false
  })),
  classRoles: Object.values(party.search.classRoles)
};

if (clan.version !== 1)
  throw new Error("clan.json: version " + clan.version + " is not supported");
const clanRules = {
  maxMembers: clan.maxMembers,
  maxOfficers: clan.maxOfficers,
  codeLength: clan.codeLength,
  listLimit: clan.listLimit,
  activeMinutes: clan.activeMinutes,
  inactiveDays: clan.inactiveDays,
  leaderAwayDays: clan.leaderAwayDays,
  adjectives: clan.names.adjectives,
  nouns: clan.names.nouns,
  languages: party.search.languages
};

if (coopCurrency.version !== 1)
  throw new Error("coop_currency.json: version " + coopCurrency.version + " is not supported");
const walletRules = {
  currencies: coopCurrency.currencies,
  dayUtcOffsetMinutes: coopCurrency.dayUtcOffsetMinutes,
  ledgerSize: coopCurrency.ledgerSize,
  sources: coopCurrency.sources.map((source) => ({
    id: source.id, currency: source.currency, amount: source.amount, dailyCap: source.dailyCap, perFriendDailyCap: source.perFriendDailyCap || 0
  }))
};

if (raid.version !== 1)
  throw new Error("raid.json: version " + raid.version + " is not supported");
if (days.indexOf(raid.week.startDay) < 0)
  throw new Error("raid.json: unknown day " + raid.week.startDay);
const raidRules = {
  boss: raid.boss,
  weekStartDay: days.indexOf(raid.week.startDay),
  weekUtcOffsetMinutes: raid.week.utcOffsetMinutes,
  pushSeconds: raid.pushSeconds,
  maxSharesPerSecond: raid.maxSharesPerSecond,
  minMembers: raid.minMembers,
  spillover: raid.spillover,
  layers: raid.layers.map((layer) => ({ id: layer.id, input: layer.input, perMember: layer.perMember })),
  sealsPerMember: raid.rewards.sealsPerMember,
  killBonus: raid.rewards.killBonus,
  minSeals: raid.rewards.minSeals
};

if (challenges.version !== 1)
  throw new Error("clan_challenges.json: version " + challenges.version + " is not supported");
const challengeRules = {
  perDay: challenges.perDay,
  dayUtcOffsetMinutes: challenges.dayUtcOffsetMinutes,
  minMembers: challenges.minMembers,
  capsPerSecond: challenges.capsPerSecond,
  challenges: challenges.challenges.map((challenge) => ({ id: challenge.id, counter: challenge.counter, perMember: challenge.perMember, seals: challenge.seals }))
};

if (clanTree.version !== 1)
  throw new Error("clan_tree.json: version " + clanTree.version + " is not supported");
const clanTreeRules = {
  minDonation: clanTree.minDonation,
  nodes: clanTree.nodes.map((node) => ({ id: node.id, cost: node.cost, requires: node.requires || [] }))
};

if (mentor.version !== 1)
  throw new Error("mentor.json: version " + mentor.version + " is not supported");
const mentorSide = (side) => ({ friendship: side.friendship || 0, uncutGems: side.uncutGems || 0, perk: side.perk || "", coopPoints: side.coopPoints || 0 });
const mentorRules = {
  studentDays: mentor.studentDays,
  mentorMinDays: mentor.mentorMinDays,
  maxMarks: mentor.maxMarks,
  maxMentorCoopPoints: mentor.maxMentorCoopPoints,
  rewards: mentor.rewards.map((reward) => ({ hours: reward.hours, mentor: mentorSide(reward.mentor), student: mentorSide(reward.student) }))
};

const ghostRules = Object.assign({}, party.ghost, { classes: Object.keys(party.search.classRoles).length });

const code = fs.readFileSync(path.join(root, "build", "party.js"), "utf8");
fs.writeFileSync(path.join(root, "build", "index.js"), "var PartyServerRules = " + JSON.stringify(rules) + ";\nvar RiftServerRules = " + JSON.stringify(riftRules) + ";\nvar PartyDirectoryRules = " + JSON.stringify(directoryRules) + ";\nvar IntakeRules = " + JSON.stringify(intakeRules) + ";\nvar ChronicleServerRules = " + JSON.stringify(chronicleRules) + ";\nvar GhostServerRules = " + JSON.stringify(ghostRules) + ";\nvar ClanServerRules = " + JSON.stringify(clanRules) + ";\nvar WalletServerRules = " + JSON.stringify(walletRules) + ";\nvar RaidServerRules = " + JSON.stringify(raidRules) + ";\nvar ChallengeServerRules = " + JSON.stringify(challengeRules) + ";\nvar ClanTreeServerRules = " + JSON.stringify(clanTreeRules) + ";\nvar MentorServerRules = " + JSON.stringify(mentorRules) + ";\n" + code);
