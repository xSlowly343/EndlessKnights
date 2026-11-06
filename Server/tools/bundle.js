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
const clanChronicle = JSON.parse(fs.readFileSync(path.join(data, "clan_chronicle.json"), "utf8"));
const ascendancy = JSON.parse(fs.readFileSync(path.join(data, "ascendancy.json"), "utf8"));
const synergy = JSON.parse(fs.readFileSync(path.join(data, "synergy.json"), "utf8"));
const chronicle = JSON.parse(fs.readFileSync(path.join(data, "party_chronicle.json"), "utf8"));
const days = ["sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday"];

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
  maxWeakenedSpawns: party.weaken.maxEnemies,
  maxWeakenShare: party.weaken.maxShare,
  maxWeakenGold: party.weaken.maxGold,
  minBossTimerRate: party.timer.minBossRate,
  sharedBossTimerRate: party.timer.sharedRate,
  sharedTimerFighters: party.timer.sharedFighters,
  loopShareMax: party.timer.loopShareMax,
  worldSwitchSeconds: party.worldSwitchSeconds,
  repeatMaxSeconds: party.timer.repeatMaxSeconds,
  repeatCooldownSeconds: party.timer.repeatCooldownSeconds,
  minShare: party.contribution.minShare,
  activeInputSeconds: party.contribution.activeInputSeconds,
  restBankSeconds: party.contribution.restBankMinutes * 60,
  idlePortionShare: party.contribution.idlePortionShare,
  typingReserveShare: party.contribution.typingReserveShare,
  allyCreditMax: party.contribution.allyCreditMax,
  relaySenderSeconds: party.relay.senderSeconds,
  relayRecipientSeconds: party.relay.recipientSeconds,
  relayShare: party.relay.share,
  relayMaxBonus: party.relay.maxBonus,
  relayCooldownSeconds: party.relay.cooldownSeconds,
  allyResourceMax: party.buffs.allyResource.max,
  allyResourceCooldownSeconds: party.buffs.allyResource.cooldownSeconds,
  barsPerSecond: party.caps.barsPerSecond,
  burstBars: party.caps.burstBars,
  hitBurstSeconds: party.caps.hitBurstSeconds,
  flagsBeforeReport: party.caps.flagsBeforeReport,
  reactionCount: party.reactions.ids.length,
  reactionCooldownSeconds: party.reactions.cooldownSeconds,
  waveCount: stages.waves.count,
  waveFirstEnemies: stages.waves.firstEnemies,
  waveEnemiesPerWave: stages.waves.enemiesPerWave,
  waveStagesPerExtraEnemy: stages.waves.stagesPerExtraEnemy,
  waveMaxEnemies: stages.waves.maxEnemiesPerWave,
  waveMaxSteps: (stages.waves.maxSteps || []).map((step) => ({ from: step.from, max: step.max })),
  bossRetryAfterKills: stages.boss.retryAfterKills,
  bossTimerSeconds: stages.boss.timerSeconds,
  bossHpMultiplier: stages.boss.hpMultiplier
};

function statSum(modifiers, stat, kind) {
  return modifiers.filter((modifier) => modifier.stat === stat && modifier.kind === kind).reduce((sum, modifier) => sum + modifier.value, 0);
}

const partyRoles = Object.values(synergy.roles).filter((role, index, all) => all.indexOf(role) === index);

function roleMask(roles) {
  return (roles || []).reduce((mask, role) => {
    const index = partyRoles.indexOf(role);
    if (index < 0)
      throw new Error("rift.json: unknown role " + role);
    return mask | (1 << index);
  }, 0);
}

const riftRules = {
  roles: partyRoles,
  windows: rift.schedule.windows.map((window) => {
    const day = days.indexOf(window.day);
    if (day < 0)
      throw new Error("rift.json: unknown day " + window.day);
    return { day: day, hourUtc: window.hourUtc };
  }),
  durationMs: Math.round(rift.schedule.durationHours * 3600 * 1000),
  announceMs: Math.round(rift.schedule.announceMinutes * 60 * 1000),
  modifiers: rift.modifiers.map((modifier) => modifier.id),
  enemies: rift.modifiers.map((modifier) => ({
    unless: roleMask(modifier.unlessRoles),
    regen: statSum(modifier.modifiers, "enemyRegen", "flat"),
    shield: statSum(modifier.modifiers, "bossShield", "flat"),
    extraEnemies: statSum(modifier.modifiers, "stageEnemies", "increased")
  })),
  modifiersPerWindow: rift.modifiersPerWindow,
  partyJoinMs: Math.round(rift.party.joinMinutes * 60 * 1000),
  partyDurationMs: Math.round(rift.party.durationHours * 3600 * 1000),
  boardUpdateMs: Math.round(rift.board.updateMinutes * 60 * 1000),
  boardShown: rift.board.shown,
  milestones: {
    every: rift.milestones.every,
    steps: rift.milestones.steps.map((step) => ({ from: step.from, shards: step.shards || 0, friendship: step.friendship || 0 }))
  },
  boardPlaces: rift.board.places.map((place) => ({ upTo: place.upTo || 0, shards: place.shards, ingots: place.ingots, friendship: place.friendship || 0 }))
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
  classRoles: Object.values(party.search.classRoles),
  orchestra: chronicle.orchestra
};
for (const group of chronicle.orchestra)
  for (const role of group)
    if (party.search.roles.indexOf(role) < 0)
      throw new Error("party_chronicle.json: orchestra role '" + role + "' is not in party.json search.roles");

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
  requestDays: clan.requests.days,
  requestsPerClan: clan.requests.perClan,
  requestsPerPlayer: clan.requests.perPlayer,
  adjectives: clan.names.adjectives,
  nouns: clan.names.nouns,
  languages: party.search.languages
};

if (clanChronicle.version !== 1)
  throw new Error("clan_chronicle.json: version " + clanChronicle.version + " is not supported");
const clanChronicleRules = {
  maxRecords: clanChronicle.maxRecords,
  feats: clanChronicle.feats.map((feat) => ({ id: feat.id, kind: feat.kind, levels: feat.levels, title: feat.title || null }))
};

if (coopCurrency.version !== 1)
  throw new Error("coop_currency.json: version " + coopCurrency.version + " is not supported");

const helpRules = {
  seconds: party.help.seconds,
  answerMs: Math.round(party.help.answerSeconds * 1000),
  cooldownMs: Math.round(party.help.callCooldownSeconds * 1000),
  defaultAccept: party.help.defaultAccept
};

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
function curseClanShare(modifiers) {
  return modifiers.filter((modifier) => modifier.stat === "curseClanShare").reduce((all, modifier) => all + modifier.value, 0);
}

function traitModifiers(trait) {
  if (!trait)
    return [];
  const forks = trait.forks.flatMap((fork) => fork.options.reduce((best, option) => curseClanShare(option.modifiers) > curseClanShare(best) ? option.modifiers : best, []));
  return trait.levels.flatMap((level) => level.modifiers).concat(trait.party || [], forks);
}

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
  minSeals: raid.rewards.minSeals,
  covenActiveMs: Math.round(raid.coven.activeMinutes * 60000),
  covenMaxBonus: raid.coven.maxBonus,
  covenMaxShare: ascendancy.subclasses.reduce((sum, subclass) => sum + curseClanShare(traitModifiers(subclass.trait)), 0)
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

const clanWars = JSON.parse(fs.readFileSync(path.join(data, "clan_wars.json"), "utf8"));
if (clanWars.version !== 1)
  throw new Error("clan_wars.json: version " + clanWars.version + " is not supported");
for (const front of clanWars.fronts) {
  if (front.layer && !raid.layers.some((layer) => layer.id === front.layer))
    throw new Error("clan_wars.json: front " + front.id + " uses unknown raid layer " + front.layer);
  if (!front.layer && !(front.counter in challenges.capsPerSecond))
    throw new Error("clan_wars.json: front " + front.id + " uses unknown counter " + front.counter);
}
const clanWarRules = {
  minActive: clanWars.minActive,
  pairDay: clanWars.pairDay,
  warDay: clanWars.warDay,
  fronts: clanWars.fronts.map((front) => ({ id: front.id, counter: front.counter || "", layer: front.layer || "" })),
  perMemberSeconds: clanWars.perMemberSeconds,
  moralePerChallenge: clanWars.morale.perChallenge,
  moraleMax: clanWars.morale.max,
  sealsPerMember: clanWars.seals.perMember,
  sealsWin: clanWars.seals.win,
  sealsDraw: clanWars.seals.draw,
  sealsLoss: clanWars.seals.loss,
  ratingWin: clanWars.rating.win,
  ratingDraw: clanWars.rating.draw,
  ratingLoss: clanWars.rating.loss,
  leagues: clanWars.leagues,
  seasonWeeks: clanWars.seasonWeeks
};

if (clanTree.version !== 1)
  throw new Error("clan_tree.json: version " + clanTree.version + " is not supported");
const clanTreeRules = {
  minDonation: clanTree.minDonation,
  nodes: clanTree.nodes.map((node) => ({ id: node.id, cost: node.cost, requires: node.requires || [] }))
};

if (mentor.version !== 1)
  throw new Error("mentor.json: version " + mentor.version + " is not supported");
const mentorSide = (side) => ({ friendship: side.friendship || 0, shards: side.shards || 0, perk: side.perk || "", coopPoints: side.coopPoints || 0 });
const mentorRules = {
  studentDays: mentor.studentDays,
  mentorMinDays: mentor.mentorMinDays,
  maxMarks: mentor.maxMarks,
  maxMentorCoopPoints: mentor.maxMentorCoopPoints,
  rewards: mentor.rewards.map((reward) => ({ hours: reward.hours, mentor: mentorSide(reward.mentor), student: mentorSide(reward.student) }))
};

const coopHelp = JSON.parse(fs.readFileSync(path.join(data, "coop_help.json"), "utf8"));
if (coopHelp.version !== 1)
  throw new Error("coop_help.json: version " + coopHelp.version + " is not supported");
const coopHelpRules = {
  maxPoints: coopHelp.maxPoints,
  mentorMax: coopHelp.sources.mentor.max,
  bossesMax: coopHelp.sources.bosses.max,
  bossesPerPoint: coopHelp.sources.bosses.bossesPerPoint,
  riftMax: coopHelp.sources.rift.max,
  riftStages: coopHelp.sources.rift.stages,
  wanderersMax: coopHelp.sources.wanderers.max,
  ghostMax: coopHelp.sources.ghost.max,
  ghostBosses: coopHelp.sources.ghost.bosses,
  aurasMax: coopHelp.sources.auras.max,
  auraHours: coopHelp.sources.auras.hours
};

const ghostRules = Object.assign({}, party.ghost, { classes: Object.keys(party.search.classRoles).length });

const trials = JSON.parse(fs.readFileSync(path.join(data, "chronicle.json"), "utf8"));
if (trials.version !== 1)
  throw new Error("chronicle.json: version " + trials.version + " is not supported");
const weeklyRules = {
  trials: trials.trials.map((trial) => trial.id),
  count: trials.weekly.count,
  boardSize: trials.weekly.boardSize,
  maxFriends: trials.weekly.maxFriends,
  classes: Object.keys(party.search.classRoles).length,
  maxStage: 10000000
};

const cosmetics = JSON.parse(fs.readFileSync(path.join(data, "cosmetics.json"), "utf8"));
if (cosmetics.version !== 1)
  throw new Error("cosmetics.json: version " + cosmetics.version + " is not supported");
const cosmeticIds = new Set(cosmetics.items.map((item) => item.id));
const cosmeticRules = {
  items: cosmetics.items.map((item) => ({ id: item.id, slot: item.slot, price: item.price, source: item.source, clan: !!item.clan })),
  championTop10: "champion_top10",
  championTop100: "champion_top100",
  warVictors: "war_victors",
  warVictorsDays: 7,
  leagueBanners: Object.fromEntries(clanWars.leagues.map((league) => [league.id, "league_" + league.id]))
};
for (const id of [cosmeticRules.championTop10, cosmeticRules.championTop100, cosmeticRules.warVictors].concat(Object.values(cosmeticRules.leagueBanners)))
  if (!cosmeticIds.has(id))
    throw new Error("cosmetics.json: reward " + id + " is missing");

const seasonPass = JSON.parse(fs.readFileSync(path.join(data, "season_pass.json"), "utf8"));
if (seasonPass.version !== 1)
  throw new Error("season_pass.json: version " + seasonPass.version + " is not supported");
if (seasonPass.seasonWeeks % clanWars.seasonWeeks !== 0 || seasonPass.firstWeek % seasonPass.seasonWeeks !== 0)
  throw new Error("season_pass.json: seasons must start together with clan war seasons");
const passReward = (reward) => {
  if (reward.cosmetic) {
    const item = cosmetics.items.find((entry) => entry.id === reward.cosmetic);
    if (!item || item.source !== "pass")
      throw new Error("season_pass.json: " + reward.cosmetic + " is not a season pass cosmetic");
  }
  return { level: reward.level, cosmetic: reward.cosmetic || "", crystals: reward.crystals || 0 };
};
const seasonPassRules = {
  seasonWeeks: seasonPass.seasonWeeks,
  firstWeek: seasonPass.firstWeek,
  levels: seasonPass.levels,
  pointsPerLevel: seasonPass.pointsPerLevel,
  dailyCap: seasonPass.dailyCap,
  premiumPrice: seasonPass.premiumPrice,
  seasons: seasonPass.seasons.map((season) => ({ id: season.id, free: season.free.map(passReward), premium: season.premium.map(passReward) }))
};
const crystalChangeRules = { price: ascendancy.change.crystalCost, reasons: ["subclass_change", "class_change"] };

const seasonalEvents = JSON.parse(fs.readFileSync(path.join(data, "events.json"), "utf8"));
if (seasonalEvents.version !== 1)
  throw new Error("events.json: version " + seasonalEvents.version + " is not supported");
for (const event of seasonalEvents.events) {
  for (const item of event.shop) {
    const cosmetic = cosmetics.items.find((entry) => entry.id === item.cosmetic);
    if (!cosmetic || cosmetic.source !== "event")
      throw new Error("events.json: " + item.cosmetic + " is not an event cosmetic");
  }
  if (!cosmeticIds.has(event.clanEmblem))
    throw new Error("events.json: clan emblem " + event.clanEmblem + " is missing");
}
if (!cosmeticIds.has(seasonalEvents.boss.championFrame))
  throw new Error("events.json: champion frame " + seasonalEvents.boss.championFrame + " is missing");
const seasonalEventRules = {
  windowDays: seasonalEvents.windowDays,
  shopGraceDays: seasonalEvents.shopGraceDays,
  dailyTokenCap: seasonalEvents.dailyTokenCap,
  goals: seasonalEvents.goals.map((goal) => goal.id),
  goalTokens: seasonalEvents.goalTokens,
  goalPassPoints: seasonalEvents.goalPassPoints,
  allGoalsCrystals: seasonalEvents.allGoalsCrystals,
  clanGuestsPerMember: seasonalEvents.clanGoal.guestsPerMember,
  clanTarget: seasonalEvents.clanGoal.target,
  championTop: seasonalEvents.boss.championTop,
  championFrame: seasonalEvents.boss.championFrame,
  boardSize: 50,
  events: seasonalEvents.events.map((event) => ({ id: event.id, month: event.month, day: event.day, shop: event.shop, clanEmblem: event.clanEmblem }))
};

const townData = JSON.parse(fs.readFileSync(path.join(data, "town.json"), "utf8"));
if (townData.version !== 1)
  throw new Error("town.json: version " + townData.version + " is not supported");
const townRules = { giftsPerDay: townData.gifts.perDay, giftMaxAmount: townData.gifts.maxAmount, giftInbox: townData.gifts.inbox, giftItems: townData.gifts.items };

const code = fs.readFileSync(path.join(root, "build", "party.js"), "utf8");
fs.writeFileSync(path.join(root, "build", "index.js"), "var PartyServerRules = " + JSON.stringify(rules) + ";\nvar RiftServerRules = " + JSON.stringify(riftRules) + ";\nvar PartyDirectoryRules = " + JSON.stringify(directoryRules) + ";\nvar IntakeRules = " + JSON.stringify(intakeRules) + ";\nvar ChronicleServerRules = " + JSON.stringify(chronicleRules) + ";\nvar GhostServerRules = " + JSON.stringify(ghostRules) + ";\nvar ClanServerRules = " + JSON.stringify(clanRules) + ";\nvar WalletServerRules = " + JSON.stringify(walletRules) + ";\nvar RaidServerRules = " + JSON.stringify(raidRules) + ";\nvar ChallengeServerRules = " + JSON.stringify(challengeRules) + ";\nvar ClanTreeServerRules = " + JSON.stringify(clanTreeRules) + ";\nvar MentorServerRules = " + JSON.stringify(mentorRules) + ";\nvar HelpServerRules = " + JSON.stringify(helpRules) + ";\nvar ClanChronicleServerRules = " + JSON.stringify(clanChronicleRules) + ";\nvar CoopHelpServerRules = " + JSON.stringify(coopHelpRules) + ";\nvar WeeklyServerRules = " + JSON.stringify(weeklyRules) + ";\nvar ClanWarServerRules = " + JSON.stringify(clanWarRules) + ";\nvar CosmeticServerRules = " + JSON.stringify(cosmeticRules) + ";\nvar SeasonPassServerRules = " + JSON.stringify(seasonPassRules) + ";\nvar CrystalChangeServerRules = " + JSON.stringify(crystalChangeRules) + ";\nvar SeasonalEventServerRules = " + JSON.stringify(seasonalEventRules) + ";\nvar TownServerRules = " + JSON.stringify(townRules) + ";\n" + code);
