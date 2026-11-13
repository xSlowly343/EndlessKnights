const crypto = require("crypto");

class FakeNakama {
  constructor() {
    this.groups = new Map();
    this.members = new Map();
    this.storage = new Map();
    this.users = new Map();
    this.matches = new Map();
    this.signals = [];
    this.notifications = [];
    this.onSignal = null;
    this.states = new Map();
    this.versions = 0;
    this.boards = new Map();
  }

  leaderboardCreate(id, authoritative, sortOrder, operator, resetSchedule, metadata) {
    if (!this.boards.has(id))
      this.boards.set(id, { id: id, authoritative: authoritative, sortOrder: sortOrder, operator: operator, records: new Map() });
  }

  leaderboardRecordWrite(id, ownerId, username, score, subscore, metadata) {
    const board = this.boards.get(id);
    if (!board)
      throw new Error("leaderboard not found");
    const old = board.records.get(ownerId);
    if (old && board.operator === "incr")
      score = old.score + score;
    const keep = old && board.operator === "best" && old.score >= score;
    const record = keep ? Object.assign({}, old, { username: username, numScore: old.numScore + 1 })
      : { leaderboardId: id, ownerId: ownerId, username: username, score: score, subscore: subscore || 0, numScore: old ? old.numScore + 1 : 1, metadata: metadata || {}, createTime: 0, updateTime: 0, expiryTime: 0, rank: 0 };
    board.records.set(ownerId, record);
    return this.ranked(board).find((r) => r.ownerId === ownerId);
  }

  leaderboardRecordDelete(id, ownerId) {
    const board = this.boards.get(id);
    if (board)
      board.records.delete(ownerId);
  }

  friendsList(userId, limit, state, cursor) {
    const friends = (this.friends && this.friends.get(userId)) || [];
    return { friends: friends.map((id) => ({ user: this.users.get(id), state: 0 })).filter((entry) => entry.user) };
  }

  addFriends(first, second) {
    this.friends = this.friends || new Map();
    for (const [a, b] of [[first, second], [second, first]]) {
      const list = this.friends.get(a) || [];
      if (list.indexOf(b) < 0)
        list.push(b);
      this.friends.set(a, list);
    }
  }

  leaderboardRecordsList(id, ownerIds, limit, cursor, overrideExpiry) {
    const board = this.boards.get(id);
    if (!board)
      throw new Error("leaderboard not found");
    const ranked = this.ranked(board);
    const owners = ownerIds || [];
    return { records: limit > 0 ? ranked.slice(0, limit) : [], ownerRecords: ranked.filter((r) => owners.indexOf(r.ownerId) >= 0) };
  }

  ranked(board) {
    const all = Array.from(board.records.values()).map((r) => Object.assign({}, r));
    all.sort((a, b) => b.score - a.score);
    all.forEach((r, i) => { r.rank = i + 1; });
    return all;
  }

  addUser(userId, name, steamId, createTime) {
    this.users.set(userId, { userId: userId, username: name, displayName: name, steamId: steamId || "", online: true, createTime: createTime === undefined ? Math.floor(Date.now() / 1000) : createTime });
  }

  uuidv4() {
    return crypto.randomUUID();
  }

  usersGetId(ids) {
    return ids.map((id) => this.users.get(id)).filter(Boolean);
  }

  groupCreate(userId, name, creatorId, lang, description, avatarUrl, open, metadata, limit) {
    for (const group of this.groups.values())
      if (group.name === name)
        throw new Error("group name in use");
    const group = { id: crypto.randomUUID(), name: name, langTag: lang || "", open: !!open, edgeCount: 1, maxCount: limit || 100, metadata: JSON.parse(JSON.stringify(metadata || {})) };
    this.groups.set(group.id, group);
    this.members.set(group.id, [userId]);
    this.states.set(group.id + "/" + userId, 0);
    return this.copy(group);
  }

  groupUpdate(groupId, userId, name, creatorId, lang, description, avatarUrl, open, metadata) {
    const group = this.groups.get(groupId);
    if (!group)
      throw new Error("group not found");
    if (lang !== null && lang !== undefined) group.langTag = lang;
    if (open !== null && open !== undefined) group.open = open;
    if (metadata !== null && metadata !== undefined) group.metadata = JSON.parse(JSON.stringify(metadata));
  }

  groupDelete(groupId) {
    this.groups.delete(groupId);
    this.members.delete(groupId);
  }

  groupsGetId(ids) {
    return ids.map((id) => this.groups.get(id)).filter(Boolean).map((group) => this.copy(group));
  }

  groupsList(name, langTag, open, members, limit) {
    const groups = [];
    for (const group of this.groups.values()) {
      if (langTag !== undefined && group.langTag !== langTag) continue;
      if (open !== undefined && group.open !== open) continue;
      groups.push(this.copy(group));
    }
    return { groups: groups.slice(0, limit || 100) };
  }

  groupUsersAdd(groupId, userIds) {
    const list = this.members.get(groupId);
    const group = this.groups.get(groupId);
    for (const userId of userIds) {
      if (list.indexOf(userId) >= 0) continue;
      if (list.length >= group.maxCount) throw new Error("group is full");
      list.push(userId);
      this.states.set(groupId + "/" + userId, 2);
    }
    group.edgeCount = list.length;
  }

  groupUsersKick(groupId, userIds) {
    const list = this.members.get(groupId);
    for (const userId of userIds) {
      const owners = list.filter((member) => this.states.get(groupId + "/" + member) === 0);
      if (owners.length === 1 && owners[0] === userId) throw new Error("cannot kick the last superadmin");
      const index = list.indexOf(userId);
      if (index >= 0) list.splice(index, 1);
    }
    this.groups.get(groupId).edgeCount = list.length;
  }

  groupUsersList(groupId) {
    return { groupUsers: (this.members.get(groupId) || []).map((userId) => ({ user: this.users.get(userId) || { userId: userId }, state: this.states.get(groupId + "/" + userId) })) };
  }

  groupUsersPromote(groupId, userIds) {
    for (const userId of userIds) {
      const key = groupId + "/" + userId;
      this.states.set(key, Math.max(0, this.states.get(key) - 1));
    }
  }

  userGroupsList(userId) {
    const userGroups = [];
    for (const [groupId, list] of this.members)
      if (list.indexOf(userId) >= 0)
        userGroups.push({ group: this.copy(this.groups.get(groupId)), state: 2 });
    return { userGroups: userGroups };
  }

  storageRead(keys) {
    return keys.map((key) => this.storage.get(this.key(key))).filter(Boolean).map((object) => JSON.parse(JSON.stringify(object)));
  }

  storageWrite(objects) {
    for (const object of objects) {
      const id = this.key(object);
      const existing = this.storage.get(id);
      if (object.version === "*" && existing) throw new Error("storage write rejected - version check failed");
      if (object.version && object.version !== "*" && (!existing || existing.version !== object.version)) throw new Error("storage write rejected - version check failed");
      this.storage.set(id, { collection: object.collection, key: object.key, userId: object.userId, value: JSON.parse(JSON.stringify(object.value)), version: String(++this.versions) });
    }
  }

  storageList(userId, collection, limit, cursor) {
    const all = [...this.storage.values()].filter((object) => object.collection === collection && (userId === undefined || object.userId === userId))
      .sort((a, b) => (a.key < b.key ? -1 : 1));
    const start = cursor ? Number(cursor) : 0;
    const page = all.slice(start, start + (limit || 100)).map((object) => JSON.parse(JSON.stringify(object)));
    return { objects: page, cursor: start + page.length < all.length ? String(start + page.length) : "" };
  }

  storageDelete(keys) {
    for (const key of keys) {
      const existing = this.storage.get(this.key(key));
      if (key.version && (!existing || existing.version !== key.version)) throw new Error("storage delete rejected - version check failed");
    }
    for (const key of keys) this.storage.delete(this.key(key));
  }

  matchCreate(module, params) {
    const id = crypto.randomUUID() + ".fake";
    this.matches.set(id, { matchId: id, size: 0, label: "", params: JSON.parse(JSON.stringify(params || {})) });
    return id;
  }

  matchGet(id) {
    const match = this.matches.get(id);
    return match ? Object.assign({}, match) : null;
  }

  matchSignal(id, data) {
    this.signals.push({ id: id, data: data });
    return this.onSignal ? this.onSignal(id, data) : "ok";
  }

  notificationSend(userId, subject, content, code, senderId, persistent) {
    this.notifications.push({ userId, subject, content: JSON.parse(JSON.stringify(content)), code, senderId, persistent });
  }

  key(object) {
    return object.collection + "/" + object.key + "/" + object.userId;
  }

  copy(group) {
    return JSON.parse(JSON.stringify(group));
  }
}

const logger = { info() {}, warn() {}, error() {} };

module.exports = { FakeNakama, logger };
