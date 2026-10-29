interface MentorRewardSide {
    friendship: number;
    uncutGems: number;
    perk: string;
    coopPoints: number;
}

interface MentorRules {
    studentDays: number;
    mentorMinDays: number;
    maxMarks: number;
    maxMentorCoopPoints: number;
    rewards: { hours: number; mentor: MentorRewardSide; student: MentorRewardSide }[];
}

interface MentorProfile {
    ready: boolean;
    studentId: string;
    studentUntil: number;
    mentorId: string;
    seconds: number;
    level: number;
    marks: string[];
    perks: string[];
    coopPoints: number;
    graduates: number;
}

declare const MentorServerRules: MentorRules;

const MentorCollection = "mentor";
const MentorKey = "profile";
const MentorWriteAttempts = 3;
const MentorHourSeconds = 3600;
const MentorMarkPattern = /^[a-z0-9_]{1,64}$/;

function mentorEmpty(): MentorProfile {
    return { ready: false, studentId: "", studentUntil: 0, mentorId: "", seconds: 0, level: 0, marks: [], perks: [], coopPoints: 0, graduates: 0 };
}

function mentorBornAt(nk: nkruntime.Nakama, userIds: string[]): { [userId: string]: number } {
    const born: { [userId: string]: number } = {};
    for (const user of userIds.length > 0 ? nk.usersGetId(userIds) : [])
        born[user.userId] = (Number(user.createTime) || 0) * 1000;
    return born;
}

function mentorIsStudent(bornAtMs: number, nowUnixMs: number): boolean {
    return bornAtMs > 0 && nowUnixMs - bornAtMs < MentorServerRules.studentDays * PartyDayMs;
}

function mentorOldEnough(bornAtMs: number, nowUnixMs: number): boolean {
    return bornAtMs > 0 && nowUnixMs - bornAtMs >= MentorServerRules.mentorMinDays * PartyDayMs;
}

function mentorRead(nk: nkruntime.Nakama, userIds: string[]): { [userId: string]: { value: MentorProfile; version: string } } {
    const profiles: { [userId: string]: { value: MentorProfile; version: string } } = {};
    for (const userId of userIds)
        profiles[userId] = { value: mentorEmpty(), version: "*" };
    if (userIds.length === 0)
        return profiles;
    for (const object of nk.storageRead(userIds.map((userId) => ({ collection: MentorCollection, key: MentorKey, userId: userId }))))
        profiles[object.userId] = { value: Object.assign(mentorEmpty(), object.value), version: object.version };
    return profiles;
}

function mentorChange<T>(nk: nkruntime.Nakama, userId: string, change: (profile: MentorProfile) => T): T {
    for (let attempt = 1; attempt <= MentorWriteAttempts; attempt++) {
        const stored = mentorRead(nk, [userId])[userId];
        const result = change(stored.value);
        try {
            nk.storageWrite([{ collection: MentorCollection, key: MentorKey, userId: userId, value: stored.value as any, version: stored.version, permissionRead: 1, permissionWrite: 0 }]);
            return result;
        } catch (error) {
            continue;
        }
    }
    throw new Error("mentor is busy, try again");
}

function mentorFree(profile: MentorProfile, studentId: string, nowUnixMs: number): boolean {
    return profile.ready && (profile.studentId === "" || profile.studentId === studentId || profile.studentUntil <= nowUnixMs);
}

function mentorRoles(nk: nkruntime.Nakama, userIds: string[], nowUnixMs: number): { students: string[]; mentors: string[]; profiles: { [userId: string]: MentorProfile } } {
    const born = mentorBornAt(nk, userIds);
    const stored = mentorRead(nk, userIds);
    const profiles: { [userId: string]: MentorProfile } = {};
    const students: string[] = [];
    const mentors: string[] = [];
    for (const userId of userIds) {
        profiles[userId] = stored[userId].value;
        if (mentorIsStudent(born[userId] || 0, nowUnixMs))
            students.push(userId);
        else if (profiles[userId].ready && mentorOldEnough(born[userId] || 0, nowUnixMs))
            mentors.push(userId);
    }
    return { students: students, mentors: mentors, profiles: profiles };
}

function mentorAvailable(roles: { mentors: string[]; profiles: { [userId: string]: MentorProfile } }, nowUnixMs: number): string[] {
    return roles.mentors.filter((userId) => mentorFree(roles.profiles[userId], "", nowUnixMs));
}

function mentorStudentUntil(nk: nkruntime.Nakama, studentId: string): number {
    return (mentorBornAt(nk, [studentId])[studentId] || 0) + MentorServerRules.studentDays * PartyDayMs;
}

function mentorTogether(nk: nkruntime.Nakama, logger: nkruntime.Logger, pairs: { [pair: string]: number }, nowUnixMs: number): number {
    let counted = 0;
    for (const pair of Object.keys(pairs)) {
        const ids = pair.split("|");
        if (ids.length !== 2 || !(pairs[pair] > 0))
            continue;
        try {
            if (mentorCount(nk, logger, ids, pairs[pair], nowUnixMs))
                counted += pairs[pair];
        } catch (error) {
            logger.warn("mentor hours were not counted for %s: %s", pair, String(error));
        }
    }
    return counted;
}

function mentorCount(nk: nkruntime.Nakama, logger: nkruntime.Logger, ids: string[], seconds: number, nowUnixMs: number): boolean {
    const roles = mentorRoles(nk, ids, nowUnixMs);
    if (roles.students.length !== 1 || roles.mentors.length !== 1)
        return false;
    const studentId = roles.students[0];
    const mentorId = roles.mentors[0];
    if (!mentorFree(roles.profiles[mentorId], studentId, nowUnixMs))
        return false;
    const until = mentorStudentUntil(nk, studentId);
    mentorChange(nk, mentorId, (profile) => {
        if (profile.studentId !== studentId) {
            profile.studentId = studentId;
            profile.studentUntil = until;
        }
    });
    const reached = mentorChange(nk, studentId, (profile) => {
        profile.mentorId = mentorId;
        profile.seconds += seconds;
        const levels: number[] = [];
        while (profile.level < MentorServerRules.rewards.length && profile.seconds >= MentorServerRules.rewards[profile.level].hours * MentorHourSeconds) {
            levels.push(profile.level);
            profile.level++;
        }
        return levels;
    });
    for (const level of reached)
        mentorReward(nk, logger, mentorId, studentId, level, nowUnixMs);
    return true;
}

function mentorReward(nk: nkruntime.Nakama, logger: nkruntime.Logger, mentorId: string, studentId: string, level: number, nowUnixMs: number): void {
    const reward = MentorServerRules.rewards[level];
    mentorGive(nk, mentorId, reward.mentor, true, nowUnixMs);
    mentorGive(nk, studentId, reward.student, false, nowUnixMs);
    if (reward.mentor.perk === "mentor_cloak")
        mentorChange(nk, mentorId, (profile) => { profile.graduates++; });
    if (reward.student.uncutGems > 0)
        mailSend(nk, studentId, {
            id: "mentor-" + reward.hours, kind: "mentor", at: nowUnixMs, place: 0, of: 0, depth: reward.hours, title: "",
            reward: { shards: 0, uncutGems: reward.student.uncutGems, ingots: 0 }, claimed: false, claimedAt: 0
        });
    logger.info("mentor %s and student %s reached %d hours together", mentorId, studentId, reward.hours);
}

function mentorGive(nk: nkruntime.Nakama, userId: string, side: MentorRewardSide, isMentor: boolean, nowUnixMs: number): void {
    if (side.friendship > 0)
        walletGrant(nk, userId, "mentor_hours", side.friendship, nowUnixMs);
    if (!side.perk && !(side.coopPoints > 0))
        return;
    mentorChange(nk, userId, (profile) => {
        if (side.perk && profile.perks.indexOf(side.perk) < 0)
            profile.perks.push(side.perk);
        if (side.coopPoints > 0)
            profile.coopPoints = isMentor ? Math.min(MentorServerRules.maxMentorCoopPoints, profile.coopPoints + side.coopPoints) : profile.coopPoints + side.coopPoints;
    });
}

function mentorView(nk: nkruntime.Nakama, userId: string, nowUnixMs: number): { [key: string]: any } {
    const born = mentorBornAt(nk, [userId])[userId] || 0;
    const profile = mentorRead(nk, [userId])[userId].value;
    const student = mentorIsStudent(born, nowUnixMs);
    const hasStudent = profile.studentId !== "" && profile.studentUntil > nowUnixMs;
    const other = student ? profile.mentorId : hasStudent ? profile.studentId : "";
    const otherProfile = other ? mentorRead(nk, [other])[other].value : null;
    return {
        role: student ? "student" : profile.ready && mentorOldEnough(born, nowUnixMs) ? "mentor" : "none",
        canMentor: mentorOldEnough(born, nowUnixMs), ready: profile.ready,
        studentUntil: student ? born + MentorServerRules.studentDays * PartyDayMs : hasStudent ? profile.studentUntil : 0,
        partner: other ? { userId: other, name: chronicleNameOf(nk, other) } : null,
        seconds: student ? profile.seconds : otherProfile ? otherProfile.seconds : 0,
        level: student ? profile.level : otherProfile ? otherProfile.level : 0,
        marks: student ? profile.marks : otherProfile ? otherProfile.marks : [],
        perks: profile.perks, coopPoints: profile.coopPoints, graduates: profile.graduates
    };
}

function mentorReadyRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const ready = partyRequest(payload).ready;
    if (typeof ready !== "boolean")
        throw new Error("bad request");
    const now = Date.now();
    if (ready && !mentorOldEnough(mentorBornAt(nk, [userId])[userId] || 0, now))
        throw new Error("too new to mentor");
    mentorChange(nk, userId, (profile) => { profile.ready = ready; });
    return JSON.stringify(mentorView(nk, userId, now));
}

function mentorStatusRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    return JSON.stringify(mentorView(nk, partyCaller(ctx), Date.now()));
}

function mentorMarksRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
    const userId = partyCaller(ctx);
    const marks = partyRequest(payload).marks;
    if (!Array.isArray(marks) || marks.length > MentorServerRules.maxMarks || marks.some((mark) => typeof mark !== "string" || !MentorMarkPattern.test(mark)))
        throw new Error("bad request");
    const now = Date.now();
    const profile = mentorRead(nk, [userId])[userId].value;
    if (!profile.studentId || profile.studentUntil <= now)
        throw new Error("no student");
    const unique: string[] = [];
    for (const mark of marks as string[])
        if (unique.indexOf(mark) < 0)
            unique.push(mark);
    mentorChange(nk, profile.studentId, (student) => { student.marks = unique; });
    return JSON.stringify(mentorView(nk, userId, now));
}
