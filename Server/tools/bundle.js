const fs = require("fs");
const path = require("path");

const root = path.join(__dirname, "..");
const data = path.join(root, "..", "Assets", "StreamingAssets", "Data");
const party = JSON.parse(fs.readFileSync(path.join(data, "party.json"), "utf8"));
const stages = JSON.parse(fs.readFileSync(path.join(data, "stages.json"), "utf8"));
const dungeons = JSON.parse(fs.readFileSync(path.join(data, "dungeons.json"), "utf8"));
const rift = JSON.parse(fs.readFileSync(path.join(data, "rift.json"), "utf8"));
const intake = JSON.parse(fs.readFileSync(path.join(root, "config", "intake.json"), "utf8"));
const cloud = JSON.parse(fs.readFileSync(path.join(root, "config", "cloud.json"), "utf8"));
if (cloud.version !== 1)
  throw new Error("config/cloud.json: version " + cloud.version + " is not supported");
const cloudRules = { maxBytes: cloud.maxBytes, backups: cloud.backups, playSlackSeconds: cloud.playSlackSeconds, playRate: cloud.playRate, stagesPerHour: cloud.stagesPerHour, stageSlack: cloud.stageSlack };
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
  stageBarsShare: party.caps.stageBarsShare || 0,
  pierceShare: party.pierce ? party.pierce.share : 0,
  pierceKillsPerTick: party.pierce ? party.pierce.maxKillsPerTick : 0,
  pierceCaps: {
    maxShare: party.pierce && party.pierce.maxShare !== undefined ? party.pierce.maxShare : 1,
    maxEnemySkip: party.pierce && party.pierce.maxEnemySkip !== undefined ? party.pierce.maxEnemySkip : 4,
    maxSplashShare: party.pierce && party.pierce.maxSplashShare !== undefined ? party.pierce.maxSplashShare : 1,
    maxSplashTargets: party.pierce && party.pierce.maxSplashTargets !== undefined ? party.pierce.maxSplashTargets : 3,
    maxBossPierce: party.pierce && party.pierce.maxBossPierce !== undefined ? party.pierce.maxBossPierce : 0.5,
    bossPierceHpShare: party.pierce && party.pierce.bossPierceHpShare !== undefined ? party.pierce.bossPierceHpShare : 0.5,
    maxStageCarry: party.pierce && party.pierce.maxStageCarry !== undefined ? party.pierce.maxStageCarry : 1
  },
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
  bossHpMultiplier: stages.boss.hpMultiplier,
  gates: (stages.gates || []).map((gate) => ({ stage: gate.stage, hpMultiplier: gate.hpMultiplier, timerSeconds: gate.timerSeconds })),
  dungeonTimerSeconds: dungeons.bossTimerSeconds,
  dungeonTimers: dungeons.modifiers.map((modifier) => modifier.bossTimer || 1)
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
  softenShare: rift.windowSet.softenShare || 0,
  keyed: rift.modifiers.map((modifier) => (rift.windowSet.keys || []).some((key) => key.modifier === modifier.id)),
  shop: ((rift.forge && rift.forge.shop) || []).map((offer) => ({ id: offer.id, price: offer.price, sparks: offer.sparks, perWindow: offer.perWindow })),
  shopCurrency: "",
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
  items: cosmetics.items.map((item) => ({ id: item.id, slot: item.slot, price: item.price, source: item.source, clan: !!item.clan, look: item.look || 0 })),
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

const social = JSON.parse(fs.readFileSync(path.join(data, "social.json"), "utf8"));
if (social.version !== 1)
  throw new Error("social.json: version " + social.version + " is not supported");
const socialRules = {
  nameMin: social.names.minLength,
  nameMax: social.names.maxLength,
  tickets: social.tickets.map((ticket) => ({ id: ticket.id, price: ticket.price })),
  roles: social.roles.map((role) => ({ id: role.id, muteHours: role.muteHours, grantsRoles: !!role.grantsRoles })),
  banned: social.banned,
  chatMax: social.chat.maxLength,
  chatDays: social.chat.days,
  pollSeconds: social.chat.pollSeconds,
  removedKept: social.chat.removedKept,
  channels: social.chat.channels.map((channel) => ({ id: channel.id, history: channel.history, perMinute: channel.perMinute, cooldownMs: Math.round(channel.cooldownSeconds * 1000) })),
  reportReasons: social.chat.reportReasons,
  autoMuteReporters: social.chat.autoMute.reporters,
  autoMuteDays: social.chat.autoMute.days,
  autoMuteHours: social.chat.autoMute.hours,
  maxMuteHours: social.chat.maxMuteHours
};
const townData = JSON.parse(fs.readFileSync(path.join(data, "town.json"), "utf8"));
if (townData.version !== 1)
  throw new Error("town.json: version " + townData.version + " is not supported");
const museumData = JSON.parse(fs.readFileSync(path.join(data, "museum.json"), "utf8"));
const museumIds = museumData.collections.map((collection) => collection.id).concat(...museumData.collections.map((collection) => collection.items));
const townRules = { museum: museumIds.filter((id, index) => museumIds.indexOf(id) === index), streetSeconds: townData.street.seconds, streetShown: townData.street.shown, giftsPerDay: townData.gifts.perDay, giftMaxAmount: townData.gifts.maxAmount, giftInbox: townData.gifts.inbox, giftItems: townData.gifts.items };
const inflowRules = { perDay: townData.inflow.perDay.map((entry) => ({ prefix: entry.prefix, amount: entry.amount })), other: townData.inflow.other };
if (inflowRules.perDay.some((entry) => !(entry.amount >= 1) || entry.prefix.length === 0) || !(inflowRules.other >= 1))
  throw new Error("town.json: inflow limits are out of range");
const marketData = townData.market;
const marketTreats = JSON.parse(fs.readFileSync(path.join(data, "companions.json"), "utf8")).treats.recipes;
const marketRules = {
  currency: marketData.currency,
  items: marketData.items,
  maxLots: marketData.maxLots,
  maxAmount: marketData.maxAmount,
  minPrice: marketData.minPrice,
  maxPrice: marketData.maxPrice,
  days: marketData.days,
  fee: marketData.fee,
  shown: marketData.shown,
  inbox: marketData.inbox
};
if (!coopCurrency.currencies.includes(marketRules.currency) || marketRules.currency === "crystals")
  throw new Error("town.json: market currency " + marketRules.currency + " is not a coop currency for trade");
for (const item of marketRules.items)
  if (!marketTreats.some((treat) => "treat_" + treat.id === item && !treat.delicacy))
    throw new Error("town.json: market item " + item + " is not a companion treat for sale");
if (!(marketRules.minPrice >= 1) || !(marketRules.maxPrice >= marketRules.minPrice) || !(marketRules.fee >= 0 && marketRules.fee < 1) || !(marketRules.maxLots >= 1) || !(marketRules.maxAmount >= 1) || !(marketRules.days > 0))
  throw new Error("town.json: market limits are out of range");

const chests = JSON.parse(fs.readFileSync(path.join(data, "chests.json"), "utf8"));
if (chests.version !== 1)
  throw new Error("chests.json: version " + chests.version + " is not supported");
if (chests.currency !== "crystals" || !walletRules.currencies.includes(chests.currency))
  throw new Error("chests.json: chests are sold for crystals only");
riftRules.shopCurrency = chests.currency;

const growth = party.growth;
if (!walletRules.currencies.includes(growth.treasury.currency))
  throw new Error("party.json: the party treasury currency is not a wallet currency");
if (!(growth.treasury.nodes >= 1) || !(growth.treasury.maxVotes >= 1) || !(growth.treasury.resetFeeShare >= 0 && growth.treasury.resetFeeShare < 1) || !(growth.treasury.resetVoteHours > 0))
  throw new Error("party.json: party treasury limits are out of range");
if (growth.tree.nodes.length !== growth.treasury.nodes)
  throw new Error("party.json: the party tree has " + growth.tree.nodes.length + " nodes, the treasury votes on " + growth.treasury.nodes);
function partyTrialTreeLines(growth, stat) {
  const found = [];
  growth.tree.nodes.forEach((node, index) => {
    [["a", 1], ["b", 3], ["c", 5]].forEach(([part, rank]) => {
      for (const line of node[part] || [])
        if (line.stat === stat && !line.waits)
          found.push({ node: index + 1, rank: rank, value: line.value });
    });
  });
  return found;
}

function partyTrialServerRules(growth) {
  const trials = growth.trials;
  return {
    perWeek: trials.perWeek,
    tries: trials.tries,
    minFighters: trials.minFighters,
    maxTier: trials.maxTier,
    ceilingBase: trials.ceilingBase,
    ceilingStep: trials.ceilingStep,
    firstClearSigns: trials.firstClearSigns,
    experiencePerTier: trials.experiencePerTier,
    minMs: trials.minSeconds * 1000,
    slackMs: trials.slackSeconds * 1000,
    list: trials.list.map((trial) => ({
      id: trial.id,
      marathon: trial.rule === "marathon",
      ms: trial.seconds * 1000,
      waves: trial.rule === "marathon" ? Math.floor(trial.seconds / trial.waveSeconds + 1e-9) : 0
    })),
    extraTries: partyTrialTreeLines(growth, "trialTries"),
    carry: partyTrialTreeLines(growth, "trialCarry")
  };
}

const partyGrowthRules = {
  maxLevel: growth.maxLevel,
  levelBase: growth.levelBase,
  levelStep: growth.levelStep,
  perActiveMinute: growth.experience.perActiveMinute,
  bossTogether: growth.experience.bossTogether,
  guardianTogether: growth.experience.guardianTogether,
  order: growth.experience.order,
  perBuildItem: growth.experience.perBuildItem,
  dailyMinutesPerMember: growth.experience.dailyMinutesPerMember,
  firstReportSeconds: growth.experience.firstReportSeconds,
  reportSlackSeconds: growth.experience.reportSlackSeconds,
  rejoinHours: growth.basket.rejoinHours,
  kickedRejoinHours: growth.basket.kickedRejoinHours,
  treasury: {
    currency: growth.treasury.currency,
    minDonation: growth.treasury.minDonation,
    nodes: growth.treasury.nodes,
    maxVotes: growth.treasury.maxVotes,
    freeResetMs: growth.treasury.freeResetDays * 86400000,
    resetFeeShare: growth.treasury.resetFeeShare,
    resetVoteMs: growth.treasury.resetVoteHours * 3600000
  },
  tree: {
    maxPoints: growth.tree.maxPoints,
    maxRank: growth.tree.maxRank,
    rows: growth.tree.rows,
    rowPrices: growth.tree.rowPrices,
    majorChangeMs: growth.tree.majorChangeHours * 3600000,
    nodes: growth.tree.nodes.map((node) => ({
      row: node.row,
      parent: node.parent || 0,
      keystone: !!node.keystone,
      prices: node.a.filter((line) => line.price !== undefined).map((line) => ({ basisPoints: Math.round(line.price * 10000), rows: line.rows }))
    })),
    majors: growth.tree.majors.map((slot) => ({ at: slot.at, options: slot.options.map((option) => option.id) }))
  },
  trials: partyTrialServerRules(growth),
  colossus: {
    hpBase: growth.colossus.hp.base,
    hpGrowth: growth.colossus.hp.growth,
    ceilingBase: growth.colossus.ceilingBase,
    ceilingStep: growth.colossus.ceilingStep,
    maxPointsPerSecond: growth.colossus.maxPointsPerSecond,
    maxOfflinePointsPerSecond: growth.colossus.maxOfflinePointsPerSecond,
    reportSeconds: growth.colossus.reportSeconds,
    experiencePerPercent: growth.colossus.experiencePerPercent,
    maxHardships: growth.colossus.maxHardships,
    mandatory: growth.colossus.mandatory.map((step) => ({ upTo: step.upTo || 0, min: step.min, max: step.max })),
    optionalMax: growth.colossus.optionalMax,
    optionalTrophyBonus: growth.colossus.optionalTrophyBonus,
    hardshipSoften: growth.colossus.hardshipSoften,
    logSize: growth.colossus.logSize,
    classes: Object.keys(party.search.classRoles).length,
    hardships: growth.colossus.hardships.map((hardship) => {
      const numbers = Object.assign({}, hardship);
      delete numbers.name;
      return numbers;
    }),
    soften: partyTrialTreeLines(growth, "colossusHardship"),
    switchOff: partyTrialTreeLines(growth, "colossusHardshipOff"),
    noHeal: partyTrialTreeLines(growth, "colossusNoHeal")
  },
  unlocks: growth.unlocks.map((unlock) => ({ level: unlock.level, kind: unlock.kind }))
};

const chestRules = {
  currency: chests.currency,
  chests: chests.chests.map((chest) => ({ id: chest.id, price: chest.price, ingots: chest.ingots || 0, essence: chest.essence || 0, perWeek: chest.perWeek }))
};

const tournaments = JSON.parse(fs.readFileSync(path.join(data, "tournaments.json"), "utf8"));
if (tournaments.version !== 1)
  throw new Error("tournaments.json: version " + tournaments.version + " is not supported");
const tournamentRules = {
  groupSize: tournaments.groupSize,
  startDays: tournaments.startDays,
  joinClosesMs: tournaments.joinClosesHours * 3600 * 1000,
  formats: tournaments.formats.map((format) => ({ id: format.id, maxScore: format.maxScore })),
  bands: tournaments.bands.map((band) => ({ from: band.from, scale: band.scale })),
  rewards: tournaments.rewards.map((reward) => ({ upTo: reward.upTo, ingots: reward.ingots || 0, essence: reward.essence || 0 })),
  maxRecord: 10000000,
  kept: 8
};

const clanCamp = JSON.parse(fs.readFileSync(path.join(data, "clan_camp.json"), "utf8"));
if (clanCamp.version !== 1)
  throw new Error("clan_camp.json: version " + clanCamp.version + " is not supported");
const cooking = JSON.parse(fs.readFileSync(path.join(data, "cooking.json"), "utf8"));
const clanCampRules = {
  maxGive: clanCamp.maxGive,
  dailyPointsPerMember: clanCamp.dailyPointsPerMember,
  materials: Object.fromEntries(clanCamp.materials.map((material) => [material.item, material.points])),
  levels: clanCamp.levels.map((level) => level.points),
  feastFromLevel: clanCamp.feast.fromLevel,
  feastDishesPerMember: clanCamp.feast.dishesPerMember,
  feastMinDishes: clanCamp.feast.minDishes,
  feastMaxDishes: clanCamp.feast.maxDishes,
  feastPerMemberWeekly: clanCamp.feast.perMemberWeekly,
  dishes: cooking.dishes.map((dish) => dish.id)
};

const guilds = JSON.parse(fs.readFileSync(path.join(data, "guilds.json"), "utf8"));
if (guilds.version !== 1)
  throw new Error("guilds.json: version " + guilds.version + " is not supported");
const campGuilds = JSON.parse(fs.readFileSync(path.join(data, "camp.json"), "utf8")).buildings.filter((building) => building.guild).map((building) => building.guild);
for (const guild of campGuilds)
  if (!guilds.build.buildings.some((building) => building.guild === guild))
    throw new Error("guilds.json: camp building guild " + guild + " is never built by the server");
for (const building of guilds.build.buildings)
  for (const level of building.levels)
    for (const resource of Object.keys(level.perPlayerDay))
      if (!guilds.build.resources.some((entry) => entry.id === resource))
        throw new Error("guilds.json: building " + building.id + " needs unknown resource " + resource);
const townBuildRules = {
  buildings: guilds.build.buildings.map((building) => ({ id: building.id, levels: building.levels.map((level) => ({ afterBuilt: level.afterBuilt, needs: level.perPlayerDay })) })),
  stages: guilds.build.stages.map((stage) => stage.built),
  stageShards: guilds.build.stageShards,
  voteMs: guilds.build.voteDays * 86400000,
  minBuildMs: guilds.build.buildDays * 86400000,
  gatherDays: guilds.build.gatherDays,
  openMs: guilds.build.openHours * 3600000,
  activeDays: guilds.build.activeDays,
  minPlayers: guilds.build.minPlayers,
  paceBurst: guilds.build.pace.burstShare,
  paceFillMs: guilds.build.pace.fillHours * 3600000,
  resources: guilds.build.resources.map((resource) => ({ id: resource.id, dailyCap: resource.dailyCap, guild: resource.guild || "", kind: resource.kind })),
  rewards: guilds.build.rewards.map((tier) => ({ id: tier.id, points: tier.points, shards: tier.shards, friendship: tier.friendship })),
  topSize: guilds.build.top.size,
  topShards: guilds.build.top.shards,
  keptDays: 8
};

const townWeekRules = guilds.weekly;
const cosmeticItems = JSON.parse(fs.readFileSync(path.join(data, "cosmetics.json"), "utf8")).items;
for (const reward of townWeekRules.rewards) {
  const frame = cosmeticItems.find((item) => item.id === reward.frame);
  if (!frame || frame.source !== "reward")
    throw new Error("guilds.json: weekly frame " + reward.frame + " is not a reward cosmetic");
}

const hideout = JSON.parse(fs.readFileSync(path.join(data, "party_hideout.json"), "utf8"));
const campLevelsData = JSON.parse(fs.readFileSync(path.join(data, "camp_levels.json"), "utf8"));
const campData = JSON.parse(fs.readFileSync(path.join(data, "camp.json"), "utf8"));
const campModulesData = JSON.parse(fs.readFileSync(path.join(data, "camp_modules.json"), "utf8"));
if (hideout.version !== 1)
  throw new Error("party_hideout.json: version " + hideout.version + " is not supported");
if (!(guilds.build.pace.burstShare > 0 && guilds.build.pace.burstShare <= 1) || !(guilds.build.pace.fillHours > 0))
  throw new Error("guilds.json: build pace is out of range");
const hideoutRules = {
  hereMs: hideout.hereSeconds * 1000,
  maxSitStepMs: hideout.maxSitStepSeconds * 1000,
  maxCaughtPerMark: hideout.maxCaughtPerMark,
  cornerDecor: hideout.cornerDecor,
  frozenMs: hideout.frozenDays * 86400000,
  decorIds: JSON.parse(fs.readFileSync(path.join(data, "decor.json"), "utf8")).items.map((item) => item.id),
  trophyHall: hideout.places.trophyHall,
  fireSignMs: hideout.fire.signEveryMinutes * 60000,
  stations: { forge: hideout.crafts.forge.building, cauldron: hideout.crafts.cauldron.building },
  brewsPerDay: hideout.crafts.brewsPerDay,
  pot: hideout.crafts.pot.building,
  dishesPerDay: hideout.crafts.pot.dishesPerDay,
  dishes: cooking.dishes.map((dish) => dish.id),
  supplySends: JSON.parse(fs.readFileSync(path.join(data, "peaceful.json"), "utf8")).riftSupply.sendsPerWindow,
  supplySource: JSON.parse(fs.readFileSync(path.join(data, "peaceful.json"), "utf8")).riftSupply.source,
  recipes: JSON.parse(fs.readFileSync(path.join(data, "alchemy.json"), "utf8")).recipes.map((recipe) => recipe.id),
  togetherBoost: hideout.fire.togetherBoost,
  bedsPerRow: hideout.sharedGarden.bedsPerRow,
  gardenRows: hideout.sharedGarden.rows,
  tendPerMark: hideout.sharedGarden.tendPerMark,
  plants: JSON.parse(fs.readFileSync(path.join(data, "garden.json"), "utf8")).plants.map((plant) => ({ id: plant.id, input: plant.input, starter: !!plant.starter,
    growSeconds: plant.growMinutes * 60, secondsPerUnit: plant.secondsPerUnit, herb: plant.herb, yield: plant.yield })),
  storagePrefixes: hideout.storage.prefixes,
  storageItems: hideout.storage.items,
  storageNever: hideout.storage.never,
  takePerDay: hideout.storage.takePerDay,
  journalSize: hideout.storage.journal,
  tiers: hideout.tiers.map((tier) => ({ members: tier.members, bosses: tier.bosses, orders: tier.orders, sitSeconds: Math.round(tier.sitHours * 3600), fireLevel: tier.fireLevel, cornerDecor: tier.cornerDecor || 0,
    needs: (tier.needs || []).map((need) => ({ counter: need.counter, target: need.target })), opens: tier.opens })),
  buildings: hideout.buildings.filter((building) => !building.personal).map((building) => ({ id: building.id, guild: building.guild || "", levels: building.levels.map((level) => ({ points: level.points, hours: level.hours })) })),
  levelTiers: hideout.levelTiers,
  materials: Object.fromEntries(clanCamp.materials.map((material) => [material.item, material.points])),
  maxGive: clanCamp.maxGive,
  orderDays: hideout.order.days,
  orderFromTier: hideout.order.fromTier,
  orderSource: hideout.order.source,
  workshop: Object.fromEntries(campLevelsData.buildings.map((building) => [building.building, {
    materials: building.materials, branches: building.branches.map((branch) => branch.id),
    guild: (campData.buildings.find((entry) => entry.id === building.building) || {}).guild || "" }])),
  modules: Object.fromEntries(campModulesData.modules.map((module) => [module.id, { building: module.building,
    materials: module.materials.map((material) => material.item), counts: module.materials.map((material) => material.count) }])),
  moduleSockets: campModulesData.sockets,
  street: campLevelsData.street,
  stationMaxLevel: campLevelsData.maxLevel,
  stationAmounts: campLevelsData.amounts,
  stationBranchLevel: campLevelsData.branchLevel,
  stationChangeAmounts: campLevelsData.changeAmounts,
  orderGoals: hideout.order.goals.map((goal) => ({ id: goal.id, fromTier: goal.fromTier, needs: goal.needs.map((need) => ({ counter: need.counter, target: need.target })) }))
};
if (!walletRules.sources.some((entry) => entry.id === hideoutRules.orderSource))
  throw new Error("coop_currency.json: the party order needs the " + hideoutRules.orderSource + " source");
for (const id of Object.keys(hideoutRules.modules))
  for (const item of hideoutRules.modules[id].materials)
    if (!hideoutRules.storagePrefixes.some((prefix) => item.startsWith(prefix)) && !hideoutRules.storageItems.includes(item))
      throw new Error("camp_modules.json: module " + id + " needs " + item + ", which the party storage does not take");
for (const id of Object.keys(hideoutRules.workshop))
  for (const item of hideoutRules.workshop[id].materials)
    if (!hideoutRules.storagePrefixes.some((prefix) => item.startsWith(prefix)) && !hideoutRules.storageItems.includes(item))
      throw new Error("camp_levels.json: station " + id + " needs " + item + ", which the party storage does not take");
for (const row of hideoutRules.gardenRows)
  if (!hideoutRules.buildings.some((building) => building.id === row))
    throw new Error("party_hideout.json: unknown garden row " + row);
for (const building of hideoutRules.buildings)
  if (building.guild && !guilds.guilds.some((guild) => guild.id === building.guild))
    throw new Error("party_hideout.json: unknown guild " + building.guild);

const companions = JSON.parse(fs.readFileSync(path.join(data, "companions.json"), "utf8"));
if (companions.version !== 1)
  throw new Error("companions.json: version " + companions.version + " is not supported");
const companionRules = {
  companions: Object.fromEntries(companions.companions.map((companion) => [companion.id, companion.biome])),
  shopIds: companions.companions.filter((companion) => companions.nest.shop.sources.indexOf(companion.source) >= 0 || companion.shopPrice > 0).map((companion) => companion.id),
  shopPrices: Object.fromEntries(companions.companions.filter((companion) => companion.shopPrice > 0).map((companion) => [companion.id, companion.shopPrice])),
  shopPrice: companions.nest.shop.price,
  shopDays: companions.nest.shop.days,
  stages: companions.stages.length,
  nameLength: companions.bond.nameLength,
  pairs: companions.joint.pairs.map((pair) => [pair.a, pair.b]),
  biomes: Object.fromEntries(companions.biomes.map((biome) => [biome.biome, { bone: biome.bone, fish: biome.fish }])),
  petsPerDay: companions.coop.petsPerDay,
  warmsPerFriendPerDay: companions.coop.warmsPerFriendPerDay,
  farTripMs: companions.coop.farTrip.hours * 3600000,
  farTripsPerDay: companions.coop.farTrip.perDay,
  farMin: companions.coop.farTrip.minCompanions,
  farMax: companions.coop.farTrip.maxCompanions,
  bonesPerCompanion: companions.coop.farTrip.bonesPerCompanion,
  rarePerCompanion: companions.coop.farTrip.rarePerCompanion,
  rarePerBiome: companions.coop.farTrip.rarePerBiome,
  soulsPerTrip: companions.coop.farTrip.souls,
  visitMs: companions.coop.visit.hours * 3600000,
  visitsPerDay: companions.coop.visit.perDay,
  visitSource: companions.coop.visit.source
};
for (const id of Object.keys(companionRules.companions))
  if (!companionRules.biomes[companionRules.companions[id]])
    throw new Error("companions.json: no biome entry for " + companionRules.companions[id]);
for (const source of ["companion_pet", "companion_trip", "companion_pair", companions.coop.visit.source])
  if (!walletRules.sources.some((entry) => entry.id === source))
    throw new Error("coop_currency.json: companions need the " + source + " source");

const fair = JSON.parse(fs.readFileSync(path.join(data, "fair.json"), "utf8"));
if (fair.version !== 1)
  throw new Error("fair.json: version " + fair.version + " is not supported");
const fairFishing = JSON.parse(fs.readFileSync(path.join(data, "fishing.json"), "utf8"));
const fairFish = {};
for (const spot of fairFishing.spots) {
  for (const fish of spot.fish)
    fairFish[fish.id] = { minCm: fish.minCm, maxCm: fish.maxCm };
  fairFish[spot.legend.id] = { minCm: spot.legend.cm, maxCm: spot.legend.cm };
}
for (const fish of fairFishing.riftFish)
  fairFish[fish.id] = { minCm: fish.minCm, maxCm: fish.maxCm };
const fairRules = {
  contests: fair.contests.map((contest) => contest.id),
  harvestPoints: fair.garden.harvestPoints, mutationPoints: fair.garden.mutationPoints,
  maxHarvestsPerDay: fair.garden.maxHarvestsPerDay, maxMutationsPerDay: fair.garden.maxMutationsPerDay,
  rewards: fair.rewards.map((reward) => ({ place: reward.place, friendship: reward.friendship })),
  takingPart: fair.takingPart, minEntrants: fair.minEntrants, top: fair.top, fish: fairFish
};
for (const id of fairRules.contests)
  if (id !== "big_fish" && id !== "best_garden")
    throw new Error("fair.json: unknown contest " + id);
if (!walletRules.sources.some((source) => source.id === "clan_fair"))
  throw new Error("coop_currency.json: the fair needs the clan_fair source");

const code = fs.readFileSync(path.join(root, "build", "party.js"), "utf8");
fs.writeFileSync(path.join(root, "build", "index.js"), "var PartyServerRules = " + JSON.stringify(rules) + ";\nvar RiftServerRules = " + JSON.stringify(riftRules) + ";\nvar PartyDirectoryRules = " + JSON.stringify(directoryRules) + ";\nvar IntakeRules = " + JSON.stringify(intakeRules) + ";\nvar ChronicleServerRules = " + JSON.stringify(chronicleRules) + ";\nvar GhostServerRules = " + JSON.stringify(ghostRules) + ";\nvar ClanServerRules = " + JSON.stringify(clanRules) + ";\nvar WalletServerRules = " + JSON.stringify(walletRules) + ";\nvar RaidServerRules = " + JSON.stringify(raidRules) + ";\nvar ChallengeServerRules = " + JSON.stringify(challengeRules) + ";\nvar ClanTreeServerRules = " + JSON.stringify(clanTreeRules) + ";\nvar MentorServerRules = " + JSON.stringify(mentorRules) + ";\nvar HelpServerRules = " + JSON.stringify(helpRules) + ";\nvar ClanChronicleServerRules = " + JSON.stringify(clanChronicleRules) + ";\nvar CoopHelpServerRules = " + JSON.stringify(coopHelpRules) + ";\nvar WeeklyServerRules = " + JSON.stringify(weeklyRules) + ";\nvar ClanWarServerRules = " + JSON.stringify(clanWarRules) + ";\nvar CosmeticServerRules = " + JSON.stringify(cosmeticRules) + ";\nvar SeasonPassServerRules = " + JSON.stringify(seasonPassRules) + ";\nvar CrystalChangeServerRules = " + JSON.stringify(crystalChangeRules) + ";\nvar SeasonalEventServerRules = " + JSON.stringify(seasonalEventRules) + ";\nvar TownServerRules = " + JSON.stringify(townRules) + ";\nvar InflowServerRules = " + JSON.stringify(inflowRules) + ";\nvar CloudServerRules = " + JSON.stringify(cloudRules) + ";\nvar MarketServerRules = " + JSON.stringify(marketRules) + ";\nvar ChestServerRules = " + JSON.stringify(chestRules) + ";\nvar TournamentServerRules = " + JSON.stringify(tournamentRules) + ";\nvar ClanCampServerRules = " + JSON.stringify(clanCampRules) + ";\nvar TownBuildServerRules = " + JSON.stringify(townBuildRules) + ";\nvar TownWeekServerRules = " + JSON.stringify({ perPlayer: townWeekRules.perPlayer, minGoal: townWeekRules.minGoal, minLabour: townWeekRules.minLabour, rewards: townWeekRules.rewards.map((reward) => ({ decor: reward.decor, frame: reward.frame })) }) + ";\nvar HideoutServerRules = " + JSON.stringify(hideoutRules) + ";\nvar FairServerRules = " + JSON.stringify(fairRules) + ";\n" + "var CompanionServerRules = " + JSON.stringify(companionRules) + ";\n" + "var SocialServerRules = " + JSON.stringify(socialRules) + ";\n" + "var PartyGrowthServerRules = " + JSON.stringify(partyGrowthRules) + ";\n" + code);
