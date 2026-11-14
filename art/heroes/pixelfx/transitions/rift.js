export default function rift(fx) {
  const { seg, bump, ease, TAU, rng, rand, noise, cycle, burst, sparkle, fly, WHITE } = fx;
  const K = fx.kit;
  const DARK = K.ramp(["#0a0614", "#150b2a", "#24124a"]);
  const VOID = K.ramp(["#2e1870", "#4a2a9a", "#8f5bff", "#b79cff"]);
  const EDGE = K.ramp(["#e8daff"])[0];
  const SHARD = [EDGE, VOID[3], VOID[2], VOID[1]];
  const SPARK = [WHITE, EDGE, VOID[3], VOID[2], VOID[1]];
  const LOOP = 1;
  const FRAMES = 12;
  const BIG = { rows: 55, mid: 27.5, half: 28, wide: 12, sway: 3.2, bolts: 3, reach: 15, shards: 11, far: 33 };
  const SMALL = { rows: 31, mid: 15.5, half: 16, wide: 5.4, sway: 1.4, bolts: 1, reach: 6, shards: 3, far: 11 };

  const centre = (g, row) => noise(row * 0.16, 7) * g.sway + noise(row * 0.5, 9) * g.sway * 0.35;

  function span(g, row, s) {
    const away = Math.abs(row + 0.5 - g.mid) / (g.half * Math.max(0.02, s.len));
    if (away > 1) return null;
    const pinch = 1 - away ** 2.4;
    const lens = Math.sin((Math.PI * (row + 3)) / (g.rows + 6)) ** 0.6;
    const wobble = Math.sin(s.phase * TAU + row * 0.8) * 0.7;
    const left = g.wide * lens * (0.72 + 0.5 * noise(row * 0.42, 21)) + wobble;
    const right = g.wide * lens * (0.72 + 0.5 * noise(row * 0.42, 43)) - wobble;
    const mid = centre(g, row) + (s.shake ?? 0);
    return [mid - Math.max(0, left) * s.open * pinch, mid + Math.max(0, right) * s.open * pinch, mid];
  }

  function inner(x, row, s) {
    if (s.hot) return (x + row) % 3 ? WHITE : EDGE;
    if (rand(x + 90, row, 5) < 0.03) return (Math.floor(s.phase * 6) + x + row) % 3 === 0 ? EDGE : VOID[2];
    const n = Math.sin(row * 0.33 + s.phase * TAU + x * 0.55) + Math.sin(row * 0.14 - s.phase * TAU * 2 + x * 0.9);
    return n > 1.55 ? VOID[1] : n > 1.05 ? VOID[0] : n > 0.4 ? DARK[2] : n > -0.5 ? DARK[1] : DARK[0];
  }

  function tear(p, g, s) {
    if (s.len <= 0) return;
    for (let row = 0; row <= g.rows; row++) {
      const cut = span(g, row, s);
      if (!cut) continue;
      const [left, right, mid] = cut, y = -row;
      if (right - left < 1.2) {
        p.px(mid, y, s.dim ? VOID[2] : row % 5 === 0 ? EDGE : WHITE);
        if (s.thick && row % 2) p.px(mid + 1, y, EDGE);
        continue;
      }
      const x0 = Math.floor(left), x1 = Math.floor(right);
      for (let x = x0; x <= x1; x++) {
        const d = Math.min(x - x0, x1 - x);
        p.px(x, y, d < 1 ? (s.hot || s.rim ? WHITE : EDGE) : d < 2 ? VOID[2] : d < 3 && g.wide > 6 ? VOID[1] : inner(x, row, s));
      }
      if (g.wide > 6) {
        if ((row + Math.floor(s.phase * FRAMES)) % 3 === 0) p.px(x0 - 1, y, VOID[2]);
        if ((row + Math.floor(s.phase * FRAMES)) % 3 === 1) p.px(x1 + 1, y, VOID[2]);
      }
    }
    const glow = Math.min(1, s.open + 0.2) * Math.min(1, s.len);
    for (let row = 4; row < g.rows; row += g.wide > 6 ? 8 : 6) p.glow(centre(g, row), -row, g.wide * 1.6 + 4, VOID[2], (0.13 + (s.hot ? 0.2 : 0)) * glow);
  }

  function bolt(p, x0, y0, x1, y1, r, bright) {
    let px = x0, py = y0;
    const parts = 5;
    for (let i = 1; i <= parts; i++) {
      const t = i / parts, wild = i === parts ? 0 : 1;
      const x = x0 + (x1 - x0) * t + r.range(-2.2, 2.2) * wild, y = y0 + (y1 - y0) * t + r.range(-2.6, 2.6) * wild;
      p.line(px, py, x, y, bright ? (i < 3 ? WHITE : EDGE) : VOID[2], bright ? 1 : 0.75);
      if (bright && i === 3 && r.chance(0.6)) p.line(x, y, x + r.range(-4, 4), y + r.range(-5, 5), VOID[3]);
      px = x; py = y;
    }
    if (bright) {
      p.glowLine(x0, y0, x1, y1, 5, VOID[3], 0.4);
      p.px(x1, y1, WHITE);
    }
  }

  function bolts(p, g, s, frame, power = 1) {
    for (let age = 0; age < 2; age++) {
      const f = (((frame - age) % FRAMES) + FRAMES) % FRAMES;
      for (let i = 0; i < g.bolts; i++) {
        const r = rng(f, i, 311);
        if (!r.chance(0.8 * power)) continue;
        const row = Math.floor(r.range(0.12, 0.9) * g.rows), cut = span(g, row, s);
        if (!cut) continue;
        const side = r.sign(), x0 = side > 0 ? cut[1] : cut[0];
        bolt(p, x0, -row, x0 + side * r.range(0.5, 1) * g.reach * power, -row + r.range(-9, 9) * (g.rows / 55), r, age === 0);
      }
    }
  }

  function chip(p, x, y, turn, tone, size = 2) {
    const q = ((Math.floor(turn) % 4) + 4) % 4;
    p.px(x, y, tone[0]);
    if (size < 2) return;
    p.px(x + (q === 0 || q === 3 ? 1 : -1), y, tone[1]);
    p.px(x, y + (q < 2 ? 1 : -1), tone[2]);
    if (size > 2) p.px(x + (q === 0 || q === 3 ? 1 : -1), y + (q < 2 ? -1 : 1), tone[2]);
  }

  function inflow(p, g, s, time) {
    cycle(time, LOOP, g.shards, 131, (r) => r.range(0.45, 0.8), (age, life, r, i) => {
      const k = age / life, side = i % 2 ? 1 : -1, row = r.range(0.1, 0.92) * g.rows;
      const cut = span(g, Math.floor(row), s);
      if (!cut) return;
      const goal = side > 0 ? cut[1] + 1 : cut[0] - 1, start = side * g.far * r.range(0.8, 1);
      const x = start + (goal - start) * ease.inCubic(k), y = -row - Math.sin(k * Math.PI) * r.range(-3, 3);
      if (k > 0.94) {
        p.px(x, y, WHITE);
        p.glow(x, y, 4, EDGE, 0.4);
        return;
      }
      chip(p, x, y, age * 14 + i, SHARD, g.wide > 6 ? (i % 3 === 0 ? 3 : 2) : 1);
      if (k > 0.5) p.dot(x + side * 2, y, VOID[1], 0.7);
    });
  }

  function idleState(phase) {
    return { open: 1, len: 1, phase, hot: false };
  }

  function idle(p, phase) {
    const s = idleState(phase);
    tear(p, BIG, s);
    bolts(p, BIG, s, Math.round(phase * FRAMES));
    inflow(p, BIG, s, phase * LOOP);
  }

  function outburst(p, g, time, at, count, seed, speed) {
    burst(count, seed, (r, i) => {
      const age = time - at, life = r.range(0.2, 0.36);
      if (age < 0 || age >= life) return;
      const side = i % 2 ? 1 : -1, row = r.range(0.1, 0.9) * g.rows;
      const [x, y] = fly(centre(g, row) + side * 3, -row, side * r.range(0.4, 1) * speed, r.range(-50, 30), age, 120, 5);
      if (y > 0) return;
      chip(p, x, y, age * 20 + i, SHARD, g.wide > 6 ? (i % 3 === 0 ? 3 : 2) : 1);
    });
  }

  function open(p, u, info) {
    if (u >= 1) return idle(p, 0);
    const time = u * info.seconds, g = BIG, frame = Math.round(u * (info.frames - 1));
    if (u < 0.2) {
      const k = seg(u, 0, 0.2);
      sparkle(p, centre(g, 27), -27, 2 + 3 * bump(u, 0, 0.2), WHITE, VOID[3], 0.6);
      p.glow(0, -27, 8 + 6 * k, VOID[2], 0.35);
    }
    const len = ease.outCubic(seg(u, 0.08, 0.4));
    const strain = u > 0.38 && u < 0.52;
    const rip = seg(u, 0.52, 0.74);
    const width = rip <= 0 ? 0 : 1.28 * ease.outBack(rip, 2.6) - 0.28 * seg(u, 0.74, 1) * 0 ;
    const settle = u > 0.74 ? 1 + 0.28 * (1 - ease.outCubic(seg(u, 0.74, 1))) : Math.min(width, 1.28);
    const s = {
      open: rip <= 0 ? 0 : settle, len: rip > 0 ? 1 : len * 0.94, phase: (u - 1) * info.seconds, hot: u > 0.52 && u < 0.62, thick: strain,
      shake: strain ? (frame % 2 ? 1 : -1) : 0, rim: u > 0.6 && u < 0.72,
    };
    tear(p, g, s);
    if (u > 0.2 && rip <= 0) {
      burst(5, 141, (r, i) => {
        const row = Math.floor(27 + (i - 2) * 9 * len), dir = r.sign();
        if (Math.abs(row - 27) > 27 * len) return;
        const x = centre(g, row);
        p.line(x, -row, x + dir * r.range(2, 5), -row - r.range(-3, 3), i % 2 ? EDGE : VOID[3]);
      });
      if (strain) {
        bolts(p, g, { ...s, open: 0.12, len: 1 }, frame, 0.6);
        p.glow(0, -27, 26, VOID[2], 0.3);
      }
    }
    if (u > 0.5 && u < 0.7) p.glow(0, -27, 40, EDGE, 0.5 * (1 - seg(u, 0.5, 0.7)));
    outburst(p, g, time, 0.53 * info.seconds, 22, 143, 190);
    if (rip > 0.3) bolts(p, g, s, frame + FRAMES - (info.frames - 1), u < 0.8 ? 1.25 : 1);
    if (u > 0.72) inflow(p, g, s, (u - 1) * info.seconds);
  }

  function close(p, u, info) {
    const time = u * info.seconds, g = BIG, frame = Math.round(u * (info.frames - 1));
    const swell = bump(u, 0, 0.22);
    const shut = ease.inCubic(seg(u, 0.18, 0.58));
    const s = {
      open: u < 0.18 ? 1 + 0.22 * swell : 1 - shut, len: 1 - ease.inQuad(seg(u, 0.56, 0.8)), phase: time, hot: u > 0.5 && u < 0.6, rim: u < 0.16,
      thick: u > 0.56 && u < 0.66, shake: u > 0.56 && u < 0.7 ? (frame % 2 ? 1 : -1) : 0, dim: u > 0.72,
    };
    if (u < 0.8) tear(p, g, s);
    if (u < 0.5) bolts(p, g, s, frame, 1 + swell * 0.4);
    if (u > 0.74 && u < 0.94) {
      const k = seg(u, 0.74, 0.94);
      sparkle(p, centre(g, 27), -27, 6 * (1 - k), WHITE, VOID[3], 0.7 * (1 - k));
    }
    if (u > 0.52 && u < 0.72) p.glow(0, -27, 34, EDGE, 0.4 * (1 - seg(u, 0.52, 0.72)));
    burst(20, 151, (r, i) => {
      const age = time - (0.56 + r.range(0, 0.12)) * info.seconds, life = r.range(0.12, 0.2);
      if (age < 0 || age >= life) return;
      const row = r.range(0.06, 0.94) * g.rows, [x, y] = fly(centre(g, row), -row, r.range(-46, 46), r.range(-40, 10), age, 520, 1);
      if (y > 0) return;
      chip(p, x, y, age * 22 + i, age / life > 0.6 ? [VOID[2], VOID[1], VOID[1]] : SHARD, i % 3 === 0 ? 3 : 2);
    });
  }

  function cracks(p, k, fade) {
    burst(4, 161, (r, i) => {
      let x = r.range(-5, 5), y = -30 + i * 2;
      const steps = Math.round(9 * k);
      for (let j = 0; j < steps; j++) {
        const nx = x + r.range(-3, 3), ny = y + r.range(2, 4);
        if (ny > -1) break;
        p.line(x, y, nx, ny, j % 3 === 0 ? WHITE : EDGE, fade);
        x = nx; y = ny;
      }
    });
  }

  function stepIn(p, u, info) {
    const time = u * info.seconds;
    cracks(p, seg(u, 0.02, 0.3), 1 - seg(u, 0.36, 0.56));
    burst(9, 163, (r, i) => {
      const at = r.range(0.1, 0.5), k = seg(u, at, at + 0.4);
      if (k <= 0 || k >= 1) return;
      const row = 3 + i * 3.1, x0 = r.range(-6, 2), reach = 4 + 16 * ease.outCubic(k), from = reach * ease.inCubic(k) * 0.9;
      for (let d = from; d < reach; d++) p.dot(x0 + d, -row, d > reach - 2 ? WHITE : d > reach - 6 ? EDGE : d > reach - 10 ? VOID[2] : VOID[1], 1);
    });
    burst(20, 165, (r, i) => {
      const born = r.range(0.14, 0.62) * info.seconds, life = r.range(0.14, 0.24), age = time - born;
      if (age < 0 || age >= life) return;
      const k = age / life, x0 = r.range(-7, 7), y0 = -r.range(2, 30);
      const x = x0 + (17 - x0) * ease.inQuad(k), y = y0 + (-20 - y0) * k * 0.5 - Math.sin(k * Math.PI) * r.range(-4, 4);
      chip(p, x, y, age * 24 + i, k > 0.7 ? [VOID[2], VOID[1], VOID[1]] : SHARD, i % 3 === 0 ? 3 : 2);
    });
    if (u > 0.3 && u < 0.7) p.glow(2, -16, 18, VOID[2], 0.3 * Math.sin(seg(u, 0.3, 0.7) * Math.PI));
    burst(8, 167, (r) => K.spark(p, r.range(8, 16), -r.range(6, 28), r.range(20, 50), r.range(-20, 20), time - r.range(0.6, 0.86) * info.seconds, r.range(0.05, 0.1), SPARK, 0, 0, 0.03));
  }

  function arrive(p, u, info) {
    const time = u * info.seconds;
    burst(22, 171, (r, i) => {
      const born = r.range(0, 0.4) * info.seconds, life = r.range(0.16, 0.26), age = time - born;
      if (age < 0 || age >= life) return;
      const k = age / life, x1 = r.range(-6, 6), y1 = -r.range(2, 30), x0 = r.range(10, 19), y0 = y1 + r.range(-10, 10);
      const e = ease.outCubic(k), x = x0 + (x1 - x0) * e, y = Math.min(-1, y0 + (y1 - y0) * e);
      chip(p, x, y, age * 24 + i, SHARD, i % 3 === 0 ? 3 : 2);
      if (k > 0.9) p.glow(x, y, 4, EDGE, 0.35);
    });
    const lock = seg(u, 0.5, 0.74);
    if (lock > 0 && u < 0.9) {
      const fade = 1 - seg(u, 0.74, 0.9), rx = 8 - 1.5 * lock, ry = 15;
      for (let i = 0; i < 44; i++) {
        const a = (i / 44) * TAU;
        if (lock < 1 && (i * 7) % 44 > lock * 44) continue;
        p.dot(Math.cos(a) * rx, -16 + Math.sin(a) * ry, i % 4 === 0 ? WHITE : EDGE, fade);
      }
      p.glow(0, -16, 20, EDGE, 0.4 * fade * lock);
    }
    cracks(p, 1 - seg(u, 0.3, 0.7), 1 - seg(u, 0.4, 0.74));
    const ring = seg(u, 0.6, 1);
    if (ring > 0) K.floorRing(p, 0, -1, 3 + 12 * ease.outCubic(ring), 0.8 + 2.4 * ease.outCubic(ring), VOID[3], VOID[1], 1 - ring);
    burst(10, 173, (r) => K.spark(p, r.range(-6, 6), -r.range(4, 28), r.range(-30, 30), r.range(-50, 10), time - 0.74 * info.seconds, r.range(0.08, 0.16), SPARK, 100, 1));
  }

  function ally(p, u, info, leaving) {
    const time = u * info.seconds, g = SMALL, up = u < 0.5, frame = Math.round(u * (info.frames - 1));
    if (u < 0.12) sparkle(p, 0, -15, 2, WHITE, VOID[3], 0.4);
    const s = {
      open: up ? 1.2 * ease.outBack(seg(u, 0.22, 0.4), 2.4) - 0.2 * seg(u, 0.36, 0.5) : 1 - ease.inCubic(seg(u, 0.62, 0.8)), len: up ? ease.outCubic(seg(u, 0.04, 0.22)) : 1 - ease.inQuad(seg(u, 0.78, 0.96)),
      phase: time, hot: u > 0.22 && u < 0.32, dim: u > 0.86,
    };
    if (u < 0.98) tear(p, g, s);
    if (u > 0.3 && u < 0.68) {
      bolts(p, g, s, frame);
      inflow(p, g, s, time);
    }
    const from = 0.36 * info.seconds, span2 = 0.36 * info.seconds;
    burst(7, leaving ? 181 : 182, (r, i) => {
      const born = from + r.range(0, 0.5) * span2, life = r.range(0.4, 0.6) * span2, age = time - born;
      if (age < 0 || age >= life) return;
      const k = age / life, bx = -r.range(5, 10), by = -r.range(3, 18), gx = r.range(-1, 1), gy = -r.range(8, 22);
      const e = leaving ? ease.inQuad(k) : ease.outQuad(k);
      p.px(leaving ? bx + (gx - bx) * e : gx + (bx - gx) * e, leaving ? by + (gy - by) * e : gy + (by - gy) * e, i % 2 ? EDGE : VOID[3]);
    });
  }

  function colossus(p, cx, base, wide, tall, crown, tone, rim, facing) {
    for (let x = -wide; x <= wide; x++) {
      const t = x / wide, shoulder = tall * 0.62 * Math.sqrt(Math.max(0, 1 - t * t));
      const head = Math.abs(t) < 0.34 ? tall * (0.38 * Math.sqrt(1 - (t / 0.34) ** 2)) : 0;
      const spike = crown && Math.abs(t) < 0.34 && Math.round(x) % 3 === 0 ? 3 : 0;
      const horn = !crown && Math.abs(Math.abs(t) - 0.3) < 0.07 ? 5 : 0;
      const height = Math.round(shoulder + head + spike + horn);
      for (let h = 0; h < height + 30; h++) {
        const y = base - height + h;
        if (y > 29) break;
        const lit = h === 0 || (h < 14 && (facing > 0 ? x === wide : x === -wide)) || (h === 1 && (facing > 0 ? x > 0 : x < 0));
        p.px(cx + x, y, lit ? rim : tone);
      }
    }
  }

  function rune(p, x, y) {
    const mark = (dx, dy, color) => {
      p.px(x + dx, y + dy, color);
      p.px(x - 1 - dx, y + dy, color);
    };
    for (let dy = -8; dy <= 8; dy++) mark(0, dy, WHITE);
    for (let i = 1; i <= 4; i++) {
      mark(i, -4 - i, EDGE);
      mark(i, -1 - i, EDGE);
      mark(i, 4 + i * 0, i < 4 ? 0 : 0);
    }
    for (let i = 1; i <= 3; i++) {
      mark(i, 2 + i, EDGE);
      mark(i, 8 - i, EDGE);
    }
    mark(4, 5, EDGE);
    p.glow(x, y, 16, VOID[3], 0.55);
    p.glow(x, y, 7, EDGE, 0.35);
  }

  function loading(p, phase, info) {
    const time = phase * info.seconds;
    p.shade(-80, -30, 80, 30, (x, y) => {
      const dx = x, dy = y * 1.7, d = Math.hypot(dx, dy), a = Math.atan2(dy, dx);
      const v = ((((a / TAU) * 3 + Math.log(d * 0.12 + 1) * 1.5 - phase) % 1) + 1) % 1;
      const arm = v < 0.1 ? 2 : v < 0.3 ? 1 : 0;
      if (d < 9) return d > 7.4 ? VOID[2] : DARK[0];
      if (d < 20) return arm === 2 ? VOID[2] : arm === 1 ? VOID[1] : VOID[0];
      if (d < 40) return arm === 2 ? VOID[1] : arm === 1 ? VOID[0] : DARK[2];
      if (d < 70) return arm === 2 ? VOID[0] : arm === 1 ? DARK[2] : DARK[1];
      return arm === 2 ? DARK[2] : arm === 1 ? DARK[1] : DARK[0];
    });
    for (let i = 0; i < 46; i++) {
      const x = Math.floor(rand(i, 1, 201) * 160) - 80, y = Math.floor(rand(i, 2, 201) * 60) - 30;
      if (Math.hypot(x, y * 1.7) < 24) continue;
      const beat = (Math.floor(phase * 8) + i) % 8;
      p.px(x, y, beat === 0 ? WHITE : beat < 3 ? EDGE : VOID[1]);
    }
    const drift = Math.sin(phase * TAU), bob = Math.cos(phase * TAU);
    colossus(p, -64 + Math.round(drift * 3), 12 + Math.round(bob * 1.5), 15, 30, false, DARK[0], VOID[0], 1);
    colossus(p, 63 - Math.round(drift * 3), 6 - Math.round(bob * 1.5), 13, 36, true, DARK[0], VOID[0], -1);
    const blink = phase > 0.42 && phase < 0.5;
    if (!blink) {
      const lx = -64 + Math.round(drift * 3), ly = 12 + Math.round(bob * 1.5) - 24;
      p.px(lx - 2, ly, VOID[3]); p.px(lx + 2, ly, VOID[3]);
      p.glow(lx, ly, 5, VOID[2], 0.4);
    }
    const rx = 63 - Math.round(drift * 3), ry = 6 - Math.round(bob * 1.5) - 27;
    p.px(rx - 2, ry, EDGE); p.px(rx + 1, ry, EDGE);
    p.glow(rx, ry, 5, VOID[2], 0.4);
    cycle(time, info.seconds, 16, 203, (r) => r.range(1, 1.7), (age, life, r, i) => {
      const k = age / life, a = r.range(0, TAU) + k * 3.4, d = 74 * (1 - ease.inQuad(k)) + 9;
      const x = Math.cos(a) * d, y = (Math.sin(a) * d) / 1.7;
      if (Math.abs(y) > 29 || Math.abs(x) > 79) return;
      chip(p, x, y, age * 7 + i, k > 0.8 ? [WHITE, EDGE, EDGE] : SHARD, i % 3 === 0 ? 3 : 2);
    });
    p.glow(0, 0, 44, VOID[1], 0.3);
    rune(p, 0, 0);
  }

  return {
    open,
    idle,
    close,
    stepIn,
    arrive,
    loading,
    allyOut: (p, u, info) => ally(p, u, info, true),
    allyIn: (p, u, info) => ally(p, u, info, false),
  };
}
