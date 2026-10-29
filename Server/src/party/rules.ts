interface PartyEncounterRules {
    maxMembers: number;
    hpPerExtraPlayer: number;
    restAfterSeconds: number;
    disconnectGraceSeconds: number;
    lateDamageSeconds: number;
    maxEnemyDamageTaken: number;
    maxWeakenedSpawns: number;
    maxWeakenShare: number;
    maxWeakenGold: number;
    minBossTimerRate: number;
    worldSwitchSeconds: number;
    waveCount: number;
    waveFirstEnemies: number;
    waveEnemiesPerWave: number;
    waveStagesPerExtraEnemy: number;
    waveMaxEnemies: number;
    bossRetryAfterKills: number;
    bossTimerSeconds: number;
    bossHpMultiplier: number;
    minShare: number;
    activeInputSeconds: number;
    restBankSeconds: number;
    idlePortionShare: number;
    typingReserveShare: number;
    allyCreditMax: number;
    relaySenderSeconds: number;
    relayRecipientSeconds: number;
}

const PartyTicksPerSecond = 10;
const PartyMaxSlots = 4;
const PartyNoSlot = -1;
const PartyNever = -9007199254740992;
const PartyMaxRepeatSeconds = 10;
const PartyRecentTicks = PartyMaxRepeatSeconds * PartyTicksPerSecond + 1;

const enum PartyEnemyKind { Normal = 0, Boss = 1 }
const enum PartyEventKind { EnemySpawned = 0, BossStarted = 1, EnemyKilled = 2, StageCleared = 3, BossFailed = 4, WorldPending = 5, WorldCancelled = 6, WorldChanged = 7, GuestLeft = 8 }
const enum PartyWorld { Home = 0, Rift = 1 }
const PartyNoWorld = 255;
const enum PartyPresence { Fighting = 0, Resting = 1, Peaceful = 2, Offline = 3 }

function partyHpMultiplier(rules: PartyEncounterRules, fighters: number): number {
    return Math.max(1, 1 + rules.hpPerExtraPlayer * (Math.max(1, fighters) - 1));
}

function partyEnemiesOnStage(rules: PartyEncounterRules, stage: number): number {
    const extra = Math.min(rules.waveMaxEnemies, Math.floor((Math.max(1, stage) - 1) / rules.waveStagesPerExtraEnemy));
    let total = 0;
    for (let wave = 0; wave < rules.waveCount; wave++)
        total += Math.min(rules.waveMaxEnemies, rules.waveFirstEnemies + wave * rules.waveEnemiesPerWave + extra);
    return total;
}

function partyTicks(seconds: number): number {
    const scaled = seconds * PartyTicksPerSecond;
    const floor = Math.floor(scaled);
    const fraction = scaled - floor;
    if (fraction > 0.5)
        return floor + 1;
    if (fraction < 0.5)
        return floor;
    return floor % 2 === 0 ? floor : floor + 1;
}
