export default function town(fx) {
  const { seg, bump, ease, TAU, cycle, burst, sparkle, fly, WHITE } = fx;
  const K = fx.kit;
  const STONE = K.ramp(["#2b2f3d", "#5b6070", "#8a8f9e", "#b9bfcc"]);
  const TEAL = K.ramp(["#184a60", "#2c7a96", "#4fa8c2", "#8fd6e2", "#dcf7f8"]);
  const COB = K.ramp(["#3f5360", "#6f8794", "#9db3bd"]);
  const WARM = K.ramp(["#b9801f", "#f0b43c", "#ffe9a8"]);
  const DUST = K.ramp(["#7d8494", "#a9b0bf"]);
  const MOTE = [WHITE, TEAL[4], TEAL[3], TEAL[2], TEAL[1]];
  const LOOP = 8 / 12;
  const BIG = { inner: 8, outer: 13, wall: 26, R: 13.5, r: 8, road: 8, pole: 10, poleTop: 45, flag: 7, flagRow: 41, rows: 45, course: 5, roofs: [1, 2, 4, 2, 3, 5, 3, 1], lamp: 5 };
  const SMALL = { inner: 4, outer: 6, wall: 12, R: 7, r: 4.2, road: 2, pole: 5, poleTop: 23, flag: 4, flagRow: 21, rows: 23, course: 4, roofs: [1, 2, 1, 2], lamp: 0 };

  function inside(g, m, row) {
    if (m >= g.inner) return false;
    return row < g.wall || Math.hypot(m + 0.5, row - g.wall + 0.5) < g.r;
  }

  function view(g, x, m, row, s) {
    if (s.fill <= 0 || row > s.fill * (g.wall + g.r)) return 0;
    if (row + 1 > s.fill * (g.wall + g.r) && s.fill < 1) return WHITE;
    if (row < g.road) {
      const half = g.inner - 1 - row * 0.5;
      if (m > half) return TEAL[0];
      if (g.road < 4) return row ? COB[2] : COB[1];
      return (x + 40 + (row >> 1) * 2) % 4 === 0 ? COB[0] : row % 2 ? COB[2] : COB[1];
    }
    const up = row - g.road;
    if (up < g.roofs[m % g.roofs.length]) return g.lamp && up === 1 && m % 3 === 2 && s.twinkle !== m ? WARM[2] : TEAL[1];
    const t = up / (g.wall + g.r - g.road);
    let step = t < 0.14 ? 4 : t < 0.32 ? 3 : t < 0.6 ? 2 : t < 0.84 ? 1 : 0;
    const span = g.wall + g.r - g.road, sweep = (((up - s.phase * span) % span) + span) % span;
    if (sweep < 1.5 && step < 4) step++;
    if (s.hot && step < 4) step++;
    return TEAL[step];
  }

  function sampler(g, s) {
    return (x, row) => {
      const m = K.index(x);
      if (inside(g, m, row)) return view(g, x, m, row, s);
      if (row < g.wall) {
        if (row < 2 && g.lamp) return m >= g.inner - 1 && m <= g.outer + 1 ? (m === g.inner - 1 || m === g.outer + 1 || row === 0 ? STONE[0] : STONE[2]) : 0;
        if (m < g.inner || m > g.outer) return 0;
        if (m === g.outer) return STONE[0];
        if (row % g.course === g.course - 1) return STONE[1];
        const lit = x >= 0 ? m === g.inner : m === g.outer - 1;
        const joint = (Math.floor(row / g.course) % 2 ? g.inner + 2 : g.inner + 3) === m && g.lamp;
        return joint ? STONE[1] : lit ? STONE[3] : x >= 0 && m === g.outer - 1 ? STONE[1] : STONE[2];
      }
      const d = Math.hypot(m + 0.5, row - g.wall + 0.5);
      if (g.lamp && m <= 1 && row - g.wall >= g.r - 1 && row - g.wall <= g.R + 0.6) return row - g.wall > g.R - 0.4 || m === 1 ? STONE[0] : STONE[3];
      if (d > g.R || d < g.r) return 0;
      if (d > g.R - 0.9) return STONE[0];
      if (d < g.r + 0.9) return STONE[1];
      const a = Math.atan2(row - g.wall + 0.5, x + 0.5);
      if (g.lamp && ((a * 7) / Math.PI) % 1 < 0.14) return STONE[1];
      return a > Math.PI * 0.55 ? STONE[3] : STONE[2];
    };
  }

  function flags(p, g, s, off) {
    if (s.flag <= 0) return;
    for (const side of [1, -1]) {
      const put = (m, row, color) => {
        const y = -row + off(row);
        if (y <= 0) p.px(side > 0 ? m : -m - 1, y, color);
      };
      const base = Math.floor(g.wall + Math.sqrt(Math.max(0, g.R * g.R - (g.pole + 0.5) ** 2)));
      for (let row = base; row <= g.poleTop; row++) put(g.pole, row, row === g.poleTop ? WARM[1] : STONE[0]);
      const len = Math.round(g.flag * Math.min(s.flag, 1.25));
      for (let k = 1; k <= len; k++) {
        const wave = Math.sin(s.phase * TAU - k * 0.9 + side) * 1.3 * (k / g.flag) - s.droop * k * 0.7;
        const half = (g.flag > 4 ? 3 : 2) * (1 - k / (g.flag + 1));
        const mid = g.flagRow + wave;
        for (let row = Math.round(mid - half); row <= Math.round(mid + half); row++)
          put(g.pole + k, row, row === Math.round(mid + half) && half > 0.8 ? TEAL[3] : row === Math.round(mid - half) && half > 0.8 ? TEAL[1] : TEAL[2]);
      }
    }
  }

  function lamps(p, g, s, off) {
    if (!g.lamp || s.lamp <= 0) return;
    for (const side of [1, -1]) {
      const swing = Math.round(Math.sin(s.phase * TAU + (side > 0 ? 0 : 1.3)) * 1.4 * s.swing);
      const top = g.wall + Math.floor(Math.sqrt(g.r * g.r - (g.lamp + 0.5) ** 2)) - 1;
      const x = side > 0 ? g.lamp : -g.lamp - 1, dy = off(top);
      for (let i = 0; i < 4; i++) p.px(x + Math.round((swing * i) / 4), -(top - i) + dy, STONE[0]);
      const lx = x + swing, ly = -(top - 5) + dy;
      if (ly > 0) continue;
      p.px(lx, ly - 1, STONE[0]);
      p.px(lx - 1, ly, WARM[1]);
      p.px(lx, ly, s.lamp > 1 ? WHITE : WARM[2]);
      p.px(lx + 1, ly, WARM[1]);
      p.px(lx, ly + 1, WARM[0]);
      p.glow(lx + 0.5, ly + 0.5, 6, WARM[1], 0.3 * Math.min(1.5, s.lamp));
    }
  }

  function portal(p, g, s) {
    const sample = sampler(g, s), off = s.off ?? (() => 0), lift = s.lift ?? 0;
    const shifted = (row) => off(row) - lift;
    for (let row = 0; row <= g.rows; row++) {
      if (row > s.build * (g.rows + 1)) break;
      const y = -row + shifted(row);
      if (y > 0) continue;
      const dx = s.jitter ? s.jitter(row) : 0;
      for (let x = -g.outer - 5; x <= g.outer + 4; x++) {
        const color = sample(x, row);
        if (color) p.px(x + dx, y, color);
      }
    }
    flags(p, g, s, shifted);
    lamps(p, g, s, shifted);
    if (s.fill > 0.5 && s.build >= 1) p.glow(0, -g.wall * 0.6, g.wall * 0.8, TEAL[2], 0.2 + (s.hot ? 0.2 : 0));
  }

  function motes(p, g, time, amount, seed) {
    cycle(time, LOOP, amount, seed, (r) => r.range(0.4, 0.62), (age, life, r) => {
      K.spark(p, r.range(-g.inner + 1, g.inner - 1), -r.range(3, g.wall), r.range(-6, 6), -r.range(10, 24), age, life, MOTE, 0, 0, 0);
    });
  }

  function idleState(phase) {
    return { build: 1, fill: 1, flag: 1, droop: 0, lamp: 1 + 0.3 * K.pulse(phase, 2), swing: 1, phase, hot: false, twinkle: Math.floor(phase * 4) * 3 + 2 };
  }

  function idle(p, phase) {
    portal(p, BIG, idleState(phase));
    motes(p, BIG, phase * LOOP, 6, 15);
  }

  function dust(p, g, k, grow) {
    if (k <= 0) return;
    for (const side of [1, -1]) {
      K.puff(p, side * (g.outer + 1 + grow * 2), -1 - grow * 2, 1.5 + grow * 2.2, DUST[1], 0.55 * k);
      K.puff(p, side * (g.inner - 2 - grow * 2), -1 - grow * 1.5, 1.2 + grow * 2, DUST[0], 0.45 * k);
      K.puff(p, side * (g.inner + 2), -2 - grow * 4, 1 + grow * 2.4, DUST[1], 0.35 * k);
    }
  }

  function open(p, u, info) {
    if (u >= 1) return idle(p, 0);
    const time = u * info.seconds, g = BIG;
    const seed = 1 - seg(u, 0.08, 0.2);
    if (seed > 0) {
      sparkle(p, 0, -1, 3 * bump(u, -0.05, 0.2), WHITE, TEAL[3], 0.5 * seed);
      p.dot(g.inner + 2, -bump(u, 0.02, 0.18) * 3, STONE[2], seed);
      p.dot(-g.inner - 3, -bump(u, 0.04, 0.2) * 2, STONE[2], seed);
    }
    const build = seg(u, 0.08, 0.56), top = build * (g.rows + 1);
    const drop = (row) => {
      const course = Math.floor(row / g.course) * g.course;
      const k = seg(top - course, 0, g.course * 1.6);
      return -Math.round(7 * (1 - k) * (1 - k));
    };
    portal(p, g, {
      build: Math.min(1, build * 1.12), off: drop, fill: ease.outCubic(seg(u, 0.6, 0.9)), flag: ease.outBack(seg(u, 0.56, 0.8), 3), droop: 0,
      lamp: u > 0.62 ? (u < 0.74 ? 2 : 1.2) : 0, swing: 1 + 2 * (1 - seg(u, 0.62, 1)), phase: (u - 1) * info.seconds * 1.5, hot: u > 0.62 && u < 0.8, twinkle: 2,
    });
    dust(p, g, bump(u, 0.08, 0.66), seg(u, 0.08, 0.66));
    if (u > 0.54 && u < 0.7) sparkle(p, 0, -(g.wall + g.R), 5 * (1 - seg(u, 0.54, 0.7)), WHITE, TEAL[3], 0.5);
    burst(12, 23, (r) => K.spark(p, r.range(-6, 6), -r.range(2, 10), r.range(-30, 30), -r.range(30, 80), time - 0.62 * info.seconds, r.range(0.14, 0.28), MOTE, 60, 1.5));
    if (u > 0.8) motes(p, g, (u - 1) * info.seconds, 6, 15);
  }

  function close(p, u, info) {
    const time = u * info.seconds, g = BIG;
    const hop = bump(u, 0, 0.2);
    const start = 0.38 * info.seconds;
    const fall = (row) => {
      const course = Math.floor(row / g.course), courses = Math.floor(g.rows / g.course);
      const begin = start + ((courses - course) / courses) * 0.1;
      const age = time - begin;
      return age <= 0 ? 0 : Math.round(1500 * age * age + 30 * age);
    };
    const jitter = (row) => {
      const course = Math.floor(row / g.course), age = time - start;
      return age <= 0 ? 0 : Math.round(Math.sin(course * 2.4) * age * 34);
    };
    portal(p, g, {
      build: 1, off: fall, jitter, lift: Math.round(hop * 1.6), fill: 1 - ease.inCubic(seg(u, 0.12, 0.4)), flag: 1 - ease.inQuad(seg(u, 0.14, 0.4)), droop: seg(u, 0.05, 0.3),
      lamp: u < 0.34 ? 1 + hop : 0, swing: 2.5, phase: time * 2, hot: u < 0.16, twinkle: 2,
    });
    const k = seg(u, 0.44, 1);
    dust(p, g, Math.sin(k * Math.PI) * (1 - seg(u, 0.9, 1)), k);
    if (k > 0 && k < 1) {
      K.puff(p, 0, -3 - k * 5, 2 + k * 4, DUST[0], 0.4 * Math.sin(k * Math.PI) * (1 - seg(u, 0.9, 1)));
      burst(10, 29, (r) => K.spark(p, r.range(-12, 12), -r.range(0, 3), r.range(-30, 30), -r.range(30, 70), time - (0.55 + r.range(0, 0.2)) * info.seconds, r.range(0.08, 0.16), [STONE[3], STONE[2], STONE[1]], 400, 0, 0));
    }
  }

  function stepIn(p, u, info) {
    const time = u * info.seconds;
    for (let n = 0; n < 7; n++) {
      const at = 0.06 + n * 0.1, k = seg(u, at, at + 0.34);
      if (k <= 0 || k >= 1) continue;
      const row = 1 + Math.round(n * 2.6), half = Math.round(7 - n * 0.95), alpha = Math.sin(k * Math.PI);
      for (let x = 0; x <= half; x++) {
        const joint = (x + n) % 3 === 2;
        const top = k < 0.4 ? TEAL[4] : TEAL[3], low = joint ? TEAL[1] : TEAL[2];
        for (const cx of [x, K.mirror(x)]) {
          p.dot(cx, -row - 1, joint ? low : top, alpha);
          if (n < 5) p.dot(cx, -row, low, alpha);
        }
      }
      if (n === 6 && k < 0.6) sparkle(p, 0, -row - 4, 4 * (1 - k / 0.6), WHITE, TEAL[3], 0.5);
    }
    burst(22, 33, (r) => {
      const born = r.range(0, 0.6) * info.seconds, lane = born / (0.6 * info.seconds);
      K.spark(p, r.range(-7, 7) * (1 - lane * 0.7), -2 - lane * 16 - r.range(0, 6), r.range(-6, 6), -r.range(16, 40), time - born, r.range(0.12, 0.22), MOTE, 0, 0, 0.04);
    });
    const puff = seg(u, 0, 0.5);
    if (puff < 1) {
      K.puff(p, -4 - puff * 3, -1 - puff * 2, 1.2 + puff * 2, DUST[1], 0.5 * (1 - puff));
      K.puff(p, 4 + puff * 3, -1 - puff * 2, 1.2 + puff * 2, DUST[1], 0.5 * (1 - puff));
    }
  }

  function arrive(p, u, info) {
    const time = u * info.seconds;
    const k = seg(u, 0, 0.7);
    K.floorRing(p, 0, -1, 2 + 11 * ease.outCubic(k), 0.6 + 2.4 * ease.outCubic(k), TEAL[3], TEAL[1], 1 - k * k);
    if (u < 0.25) {
      sparkle(p, 0, -2, 5 * (1 - seg(u, 0, 0.25)), WHITE, TEAL[3], 0.6);
      p.glow(0, -8, 16, TEAL[3], 0.35 * (1 - seg(u, 0, 0.25)));
    }
    burst(8, 37, (r, i) => {
      const side = i % 2 ? 1 : -1, born = r.range(0, 0.12) * info.seconds, age = time - born, life = r.range(0.26, 0.36);
      if (age < 0 || age >= life) return;
      const [x, y] = fly(side * r.range(1, 4), -2, side * r.range(14, 46), -r.range(120, 190), age, 520, 1.5);
      const flip = Math.floor(age * 30 + i) % 2, fade = 1 - seg(age / life, 0.7, 1) * 0.8;
      p.dot(x, y, TEAL[3], fade);
      p.dot(x + side, y, TEAL[2], fade);
      p.dot(x + side * 2, y + (flip ? 1 : 0), TEAL[2], fade);
      p.dot(x, y + 1, TEAL[1], fade);
      p.dot(x + side, y + (flip ? 1 : -1), flip ? TEAL[1] : TEAL[4], fade);
      p.dot(x - side, y - 1, STONE[1], fade);
      p.dot(x - side, y, STONE[1], fade);
    });
    burst(12, 38, (r) => K.spark(p, r.range(-8, 8), -r.range(0, 6), r.range(-20, 20), -r.range(30, 90), time - r.range(0, 0.1), r.range(0.14, 0.3), MOTE, 120, 1.5));
  }

  function ally(p, u, info, leaving) {
    const time = u * info.seconds, g = SMALL;
    const build = u < 0.5 ? seg(u, 0.03, 0.26) : 1;
    const top = build * (g.rows + 1);
    const drop = (row) => (u < 0.5 ? -Math.round(4 * (1 - seg(top - Math.floor(row / g.course) * g.course, 0, 6)) ** 2) : Math.max(0, Math.round(700 * Math.max(0, time - (0.8 + (1 - row / g.rows) * 0.06) * info.seconds) ** 2)));
    if (u < 0.1) p.dot(0, 0, WHITE, 1);
    if (u < 0.99)
      portal(p, g, {
        build: Math.min(1, build * 1.1), off: drop, fill: u < 0.5 ? ease.outCubic(seg(u, 0.26, 0.42)) : 1 - ease.inCubic(seg(u, 0.64, 0.8)),
        flag: u < 0.5 ? ease.outBack(seg(u, 0.22, 0.4), 3) : 1 - seg(u, 0.66, 0.8), droop: 0, lamp: 0, swing: 0, phase: time * 2, hot: u > 0.3 && u < 0.42, twinkle: -1,
      });
    const from = 0.36 * info.seconds, span = 0.38 * info.seconds;
    burst(8, leaving ? 41 : 42, (r) => {
      const born = from + r.range(0, 0.5) * span, life = r.range(0.4, 0.6) * span, age = time - born;
      if (age < 0 || age >= life) return;
      const k = age / life, bx = -r.range(4, 10), by = -r.range(3, 15), gx = r.range(-2, 2), gy = -r.range(3, 9);
      const e = leaving ? ease.inQuad(k) : ease.outQuad(k);
      const x = leaving ? bx + (gx - bx) * e : gx + (bx - gx) * e, y = (leaving ? by + (gy - by) * e : gy + (by - gy) * e) - Math.sin(k * Math.PI) * 2;
      p.px(x, y, k < 0.4 ? TEAL[4] : TEAL[3]);
    });
    if (u > 0.8 && u < 1) {
      const k = seg(u, 0.8, 1);
      K.puff(p, -g.outer, -1 - k * 2, 1 + k * 2, DUST[1], 0.5 * (1 - k));
      K.puff(p, g.outer, -1 - k * 2, 1 + k * 2, DUST[1], 0.5 * (1 - k));
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
