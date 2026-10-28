const crypto = require("crypto");

class FakeNakama {
  constructor() {
    this.groups = new Map();
    this.members = new Map();
    this.storage = new Map();
    this.users = new Map();
    this.matches = new Map();
    this.signals = [];
    this.states = new Map();
    this.versions = 0;
  }

  addUser(userId, name, steamId) {
    this.users.set(userId, { userId: userId, username: name, displayName: name, steamId: steamId || "", online: true });
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

  storageDelete(keys) {
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
    return "ok";
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
