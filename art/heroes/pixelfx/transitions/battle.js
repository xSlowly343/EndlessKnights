export default function battle(fx) {
  const { clamp, seg, bump, ease, TAU, rng, cycle, burst, sparkle, WHITE } = fx;
  const K = fx.kit;
  const STEEL = K.ramp(["#1f2740", "#566a90", "#9fb4d6", "#eaf1fb"]);
  const GOLD = K.ramp(["#6a3f0e", "#b67c1c", "#f0b43c", "#ffd27a", "#fff3c4"]);
  const GRIP = K.ramp(["#3b2616", "#7a4a2a"]);
  const DUST = K.ramp(["#8d8370", "#b9ae96"]);
  const HOT = [WHITE, GOLD[4], GOLD[3], GOLD[2], GOLD[1]];
  const COOL = [WHITE, STEEL[3], STEEL[2], STEEL[1]];
  const BIG = { rows: 42, base: 11, reach: 16, blade: 2, grip: 4, guard: 6, guardHalf: 4, band: 7 };
  const LOOP = 8 / 12;
  const SMALL = { rows: 27, base: 6, reach: 9, blade: 1, grip: 2, guard: 3, guardHalf: 2, band: 5 };

  const axis = (g, row, flex) => g.base - (g.reach * row) / g.rows + flex * (row / g.rows) ** 2;

  function put(p, x, y, color, side) {
    p.px(side > 0 ? x : K.mirror(x), y, color);
  }

  function swordRow(p, g, side, row, part, flex, glint) {
    const y = -row, centre = Math.floor(axis(g, row, flex));
    const at = (dx, color) => put(p, centre + dx, y, color, side);
    if (part < 1) {
      at(-1, STEEL[0]); at(0, GOLD[2]); at(1, STEEL[0]);
      return;
    }
    if (part < g.grip) {
      at(-1, STEEL[0]); at(0, part % 2 < 1 ? GRIP[1] : GRIP[0]); at(1, STEEL[0]);
      return;
    }
    if (part < g.guard) {
      for (let dx = -g.guardHalf; dx <= g.guardHalf; dx++) at(dx, Math.abs(dx) === g.guardHalf ? STEEL[0] : part < g.grip + 1 && g.guard - g.grip > 1 ? GOLD[1] : GOLD[3]);
      return;
    }
    const left = g.rows - part;
    const lit = glint >= 0 && Math.abs(part - glint) < 2.5;
    if (left < 1) {
      at(0, STEEL[3]);
      return;
    }
    if (g.blade === 1 || left < 4) {
      at(-1, STEEL[0]); at(0, lit ? WHITE : side > 0 ? STEEL[3] : STEEL[2]); at(1, STEEL[0]);
      return;
    }
    const tones = side > 0 ? [STEEL[3], STEEL[2], STEEL[1]] : [STEEL[1], STEEL[2], STEEL[3]];
    at(-2, STEEL[0]);
    at(-1, lit ? WHITE : tones[0]);
    at(0, lit ? STEEL[3] : tones[1]);
    at(1, lit ? WHITE : tones[2]);
    at(2, STEEL[0]);
  }

  function sword(p, g, side, rise, flex, glint) {
    const sunk = (1 - rise) * g.rows;
    for (let row = 0; row <= g.rows; row++) {
      const part = row + sunk;
      if (part > g.rows) break;
      swordRow(p, g, side, row, part, flex, glint < 0 ? -1 : glint * g.rows);
    }
  }

  function gate(p, g, fill, phase, hot) {
    if (fill <= 0) return;
    const apex = Math.floor((g.base * g.rows) / g.reach);
    for (let row = 0; row < apex; row++) {
      const full = Math.floor(axis(g, row, 0)) - g.blade - 1;
      if (full < 0) continue;
      const open = Math.min(full + 1, Math.max(1, Math.round((full + 1) * Math.min(fill, 1)) + (fill > 1 ? 1 : 0)));
      for (let x = 0; x < open; x++) {
        const edge = x === open - 1;
        const wave = (((row / g.band + phase + 0.1 * Math.sin(x * 0.9 + phase * TAU)) % 1) + 1) % 1;
        let step = wave < 0.14 ? 4 : wave < 0.32 ? 3 : wave < 0.58 ? 2 : wave < 0.84 ? 1 : 2;
        if (x < 1 && step < 4) step++;
        if (edge && fill >= 1 && step > 1) step--;
        if (hot) step = Math.min(4, step + 1);
        const color = fill < 1 && edge ? WHITE : GOLD[step];
        p.px(x, -row, color);
        p.px(K.mirror(x), -row, color);
      }
    }
    const k = Math.min(fill, 1);
    p.glow(0, -g.rows * 0.3, g.rows * 0.42, GOLD[2], 0.2 * k + (hot ? 0.12 : 0));
    p.glow(0, -g.rows * 0.3, g.rows * 0.2, GOLD[4], 0.12 * k);
  }

  function stones(p, g, show) {
    if (show <= 0 || g.blade === 1) return;
    for (const side of [1, -1]) {
      put(p, g.base + 3, 0, STEEL[1], side);
      put(p, g.base + 2, 0, STEEL[0], side);
      put(p, g.base - 3, 0, STEEL[0], side);
      if (show > 0.5) put(p, g.base - 4, 0, STEEL[1], side);
    }
  }

  function idleSparks(p, g, time, period, seed, amount) {
    cycle(time, period, amount, seed, (r) => r.range(0.3, 0.55), (age, life, r) => {
      const side = r.sign(), row = r.range(0.25, 0.92) * g.rows;
      const x = side * (axis(g, row, 0) - g.blade - 1);
      K.spark(p, x, -row, side * r.range(4, 16), -r.range(14, 34), age, life, HOT, 55, 0.5);
    });
  }

  function portal(p, g, s) {
    stones(p, g, s.rise);
    gate(p, g, s.fill, s.phase, s.hot);
    if (s.rise > 0) {
      sword(p, g, 1, s.rise, s.flex, s.glint < 0 ? -1 : s.glint);
      sword(p, g, -1, s.rise, s.flex, s.glint < 0 ? -1 : (s.glint + 0.5) % 1.25);
    }
  }

  const crossing = (g) => -Math.floor((g.base * g.rows) / g.reach) - 1;

  function idleState(phase) {
    return { rise: 1, fill: 1, flex: 0, hot: false, phase, glint: phase * 1.25 };
  }

  function idle(p, phase) {
    portal(p, BIG, idleState(phase));
    idleSparks(p, BIG, phase * LOOP, LOOP, 11, 7);
    p.glow(0, crossing(BIG), 6, STEEL[3], 0.1 + 0.08 * K.pulse(phase));
  }

  function clang(p, g, at, time, seed, count) {
    const y = crossing(g), age = time - at;
    if (age < 0) return;
    const flash = 1 - seg(age, 0, 0.12);
    if (flash > 0) {
      sparkle(p, 0, y, (g.blade === 1 ? 3 : 6) * flash, WHITE, GOLD[3], 0.7 * flash);
      p.glow(0, y, g.rows * 0.5, GOLD[3], 0.35 * flash);
    }
    burst(count, seed, (r) => {
      const a = r.range(-Math.PI, 0), speed = r.range(30, 75) * (g.blade === 1 ? 0.6 : 1);
      K.spark(p, 0, y, Math.cos(a) * speed, Math.sin(a) * speed - 10, age, r.range(0.14, 0.3), r.chance(0.5) ? HOT : COOL, 190, 2);
    });
  }

  function baseDust(p, g, k, grow) {
    if (k <= 0) return;
    for (const side of [1, -1]) {
      K.puff(p, side * (g.base + 1 + grow * 2), -1 - grow * 2, 1.5 + grow * 2.2, DUST[1], 0.55 * k);
      K.puff(p, side * (g.base - 3 - grow), -1 - grow, 1 + grow * 1.6, DUST[0], 0.45 * k);
    }
  }

  function open(p, u, info) {
    if (u >= 1) return idle(p, 0);
    const time = u * info.seconds;
    const crack = seg(u, 0, 0.16), gone = 1 - seg(u, 0.3, 0.5);
    if (gone > 0) {
      const half = Math.round(7 * ease.outCubic(crack));
      for (let x = 0; x <= half; x++) {
        const color = x < 2 ? WHITE : x < half - 1 ? GOLD[3] : GOLD[1];
        p.dot(x, 0, color, gone);
        p.dot(K.mirror(x), 0, color, gone);
      }
      p.glow(0, 0, 4 + 8 * crack, GOLD[3], 0.45 * gone);
      const hop = bump(u, 0.04, 0.2) * 3;
      p.dot(BIG.base, -hop, STEEL[1], gone);
      p.dot(K.mirror(BIG.base), -hop * 0.7, STEEL[1], gone);
      burst(6, 31, (r) => K.spark(p, r.range(-5, 5), 0, r.range(-10, 10), -r.range(25, 55), time - r.range(0, 0.08), r.range(0.12, 0.22), HOT, 160));
    }
    const rise = ease.outBack(seg(u, 0.16, 0.5), 1.1);
    const hit = 0.5 * info.seconds;
    const ring = time > hit ? Math.sin((time - hit) * 70) * 1.6 * (1 - seg(u, 0.5, 0.8)) : 0;
    const fill = ease.outCubic(seg(u, 0.52, 0.92));
    portal(p, BIG, { rise, fill, flex: ring, hot: u > 0.5 && u < 0.66, phase: (u - 1) * info.seconds * 1.5, glint: u > 0.5 ? seg(u, 0.5, 1) * 1.25 - 1.25 : -1 });
    baseDust(p, BIG, bump(u, 0.16, 0.62), seg(u, 0.16, 0.62));
    clang(p, BIG, hit, time, 5, 12);
    if (fill > 0.4) idleSparks(p, BIG, (u - 1) * info.seconds, LOOP, 11, 7);
  }

  function close(p, u, info) {
    const time = u * info.seconds;
    const recoil = bump(u, 0, 0.24);
    const fill = u < 0.14 ? 1 + recoil * 0.5 : 1 - ease.inCubic(seg(u, 0.14, 0.5));
    const rise = 1 - ease.inBack(seg(u, 0.5, 0.9), 1.4);
    const shut = 0.5 * info.seconds;
    const shake = time > shut ? Math.sin((time - shut) * 80) * (1 - seg(u, 0.5, 0.7)) : 0;
    portal(p, BIG, { rise, fill: u < 0.5 ? Math.max(fill, 0.04) : 0, flex: recoil * 1.6 + shake, hot: u < 0.22, phase: -time * 2.4, glint: -1 });
    if (u >= 0.44 && u < 0.72) {
      const k = seg(u, 0.44, 0.72), top = crossing(BIG) + 2, mid = top / 2, half = (-top / 2) * (1 - ease.outQuad(k));
      for (let y = mid - half; y <= mid + half; y++) {
        p.px(0, y, WHITE);
        p.px(-1, y, GOLD[3]);
      }
      sparkle(p, 0, mid, 5 * (1 - k), WHITE, GOLD[3], 0.5 * (1 - k));
    }
    burst(10, 77, (r) => K.spark(p, r.range(-1, 1), -r.range(4, 24), r.range(-34, 34), -r.range(10, 50), time - shut + 0.02, r.range(0.14, 0.26), r.chance(0.6) ? HOT : COOL, 200, 1.5));
    const sink = seg(u, 0.5, 1);
    baseDust(p, BIG, bump(u, 0.5, 1.02) * (1 - seg(u, 0.86, 1)), sink);
    if (u > 0.86 && u < 1) {
      p.dot(BIG.base - 1, 0, GOLD[2], 1 - seg(u, 0.86, 1));
      p.dot(K.mirror(BIG.base - 1), 0, GOLD[2], 1 - seg(u, 0.86, 1));
    }
  }

  function stepIn(p, u, info) {
    const time = u * info.seconds;
    const ring = seg(u, 0, 0.45);
    K.floorRing(p, 0, -1, 3 + 9 * ease.outCubic(ring), 1 + 2 * ease.outCubic(ring), GOLD[3], GOLD[1], 1 - ring * ring);
    if (u < 0.3) p.glow(0, -14, 16, GOLD[3], 0.3 * (1 - seg(u, 0, 0.3)));
    burst(46, 301, (r) => {
      const born = r.range(0, 0.62) * info.seconds, shrink = 1 - 0.55 * (born / (0.62 * info.seconds));
      const a = r.range(0, TAU), d = Math.sqrt(r.next());
      const x = Math.cos(a) * d * 6.5 * shrink, y = (-15 + Math.sin(a) * d * 14) * shrink - 1;
      const pos = K.spark(p, x, y, r.range(-9, 9), -r.range(22, 60), time - born, r.range(0.12, 0.24), HOT, -150, 0, 0.045);
      if (pos && r.chance(0.3)) p.light(pos[0], pos[1], GOLD[3], 0.5);
    });
    burst(5, 302, (r) => {
      const born = r.range(0.05, 0.5) * info.seconds, age = time - born, life = 0.14;
      if (age < 0 || age > life) return;
      const x = r.range(-6, 6), top = -r.range(8, 22) - (age / life) * 12;
      for (let i = 0; i < 5; i++) p.dot(x, top + i, i < 2 ? GOLD[4] : GOLD[2], 1 - age / life);
    });
  }

  function arrive(p, u, info) {
    const time = u * info.seconds, land = 0.2 * info.seconds;
    const fall = ease.inQuad(seg(u, 0, 0.2));
    const stuck = seg(u, 0.2, 0.46);
    if (stuck < 1) {
      const tip = -36 * (1 - fall), shake = stuck > 0 ? Math.round(Math.sin(stuck * 16) * (1 - stuck)) : 0;
      const melt = Math.floor(ease.inQuad(stuck) * 20);
      for (let i = melt; i < 20; i++) {
        const y = tip - i;
        if (y > 0 || y < -39) continue;
        const x = 2 + (i > 8 ? shake : 0);
        if (i >= 15 && i < 17) {
          for (let dx = -3; dx <= 3; dx++) p.px(x + dx, y, Math.abs(dx) === 3 ? STEEL[0] : GOLD[3]);
          continue;
        }
        if (i >= 17) {
          p.px(x, y, i === 19 ? GOLD[2] : GRIP[1]);
          continue;
        }
        p.px(x - 1, y, i < 2 ? 0 : stuck > 0 && stuck < 0.3 ? WHITE : STEEL[3]);
        p.px(x, y, stuck > 0 && stuck < 0.3 ? WHITE : STEEL[2]);
        p.px(x + 1, y, i < 2 ? 0 : STEEL[1]);
      }
      if (stuck <= 0)
        for (let i = 0; i < 14; i++) {
          const y = tip - 20 - i;
          if (y < -39) break;
          p.dot(1, y, STEEL[3], 0.8 - i / 18);
          p.dot(3, y, STEEL[1], 0.6 - i / 20);
        }
    }
    const age = time - land;
    if (age < 0) return;
    const k = seg(u, 0.2, 1), flash = 1 - seg(age, 0, 0.1);
    if (flash > 0) {
      sparkle(p, 2, -1, 6 * flash, WHITE, GOLD[3], 0.7 * flash);
      p.glow(2, -2, 16, GOLD[4], 0.4 * flash);
    }
    const spread = ease.outQuart(k);
    K.floorRing(p, 0, -2, 2 + 12 * spread, 0.8 + 3.2 * spread, DUST[1], DUST[0], 1 - k * k);
    K.floorRing(p, 0, -1, 1 + 8 * spread, 0.4 + 1.8 * spread, GOLD[3], GOLD[1], (1 - k) * 0.8);
    burst(7, 410, (r, i) => {
      const side = i % 2 ? 1 : -1, x = side * (3 + 10 * spread * r.range(0.6, 1));
      K.puff(p, x, -2 - k * r.range(3, 8), 1.6 + k * r.range(2, 3.6), r.chance(0.5) ? DUST[1] : DUST[0], 0.6 * (1 - k) * (1 - k * 0.3));
    });
    burst(16, 411, (r) => {
      const a = r.range(-Math.PI * 0.92, -Math.PI * 0.08), speed = r.range(35, 95);
      K.spark(p, 2, -1, Math.cos(a) * speed, Math.sin(a) * speed, age, r.range(0.14, 0.3), r.chance(0.65) ? HOT : COOL, 260, 2.2);
    });
  }

  function ally(p, u, info, leaving) {
    const time = u * info.seconds, g = SMALL;
    const rise = u < 0.5 ? ease.outBack(seg(u, 0.05, 0.26), 2) : 1 - ease.inQuad(seg(u, 0.82, 1));
    const fill = u < 0.5 ? ease.outCubic(seg(u, 0.24, 0.42)) : 1 - ease.inCubic(seg(u, 0.66, 0.82));
    if (u < 0.2) {
      const w = Math.round(3 * seg(u, 0, 0.1));
      for (let x = 0; x <= w; x++) {
        p.dot(x, 0, x < 1 ? WHITE : GOLD[3], 1 - seg(u, 0.1, 0.2));
        p.dot(K.mirror(x), 0, x < 1 ? WHITE : GOLD[3], 1 - seg(u, 0.1, 0.2));
      }
    }
    portal(p, g, { rise, fill, flex: bump(u, 0.24, 0.36) * 0.9, hot: u > 0.24 && u < 0.36, phase: time * 2, glint: -1 });
    clang(p, g, 0.25 * info.seconds, time, 9, 5);
    const from = 0.36 * info.seconds, span = 0.4 * info.seconds;
    burst(9, leaving ? 501 : 502, (r) => {
      const born = from + r.range(0, 0.5) * span, life = r.range(0.4, 0.6) * span, age = time - born;
      if (age < 0 || age >= life) return;
      const k = age / life, bx = -r.range(3, 10), by = -r.range(3, 16), gx = r.range(-1, 1), gy = -r.range(5, 12);
      const e = leaving ? ease.inQuad(k) : ease.outQuad(k);
      const x = leaving ? bx + (gx - bx) * e : gx + (bx - gx) * e, y = (leaving ? by + (gy - by) * e : gy + (by - gy) * e) - Math.sin(k * Math.PI) * 3;
      p.px(x, y, k < 0.35 ? WHITE : k < 0.7 ? GOLD[3] : GOLD[2]);
    });
    if (!leaving) {
      const k = seg(u, 0.4, 0.8);
      if (k > 0 && k < 1) K.floorRing(p, -5, -1, 1 + 5 * ease.outCubic(k), 0.4 + 1.2 * k, DUST[1], DUST[0], 1 - k);
    }
    if (u > 0.78 && u < 1) {
      const k = seg(u, 0.78, 1);
      K.puff(p, g.base, -1 - k, 1 + k * 1.5, DUST[1], 0.5 * (1 - k));
      K.puff(p, -g.base, -1 - k, 1 + k * 1.5, DUST[1], 0.5 * (1 - k));
    }
  }

  return {
    open,
    idle,
    close,
    stepIn,
    arrive,
    allyOut: (p, u, info) => ally(p, u, info, true),
    allyIn: (p, u, info) => ally(p, u, info, false),
  };
}
