export default function hideout(fx) {
  const { seg, bump, ease, TAU, cycle, burst, sparkle, WHITE } = fx;
  const K = fx.kit;
  const WOOD = K.ramp(["#3b2212", "#7a4a2a", "#a8693a", "#d08e4e"]);
  const OUT = K.ramp(["#22140b"])[0];
  const WARM = K.ramp(["#9c4a14", "#d9862a", "#f0b43c", "#ffd878", "#fff4c8"]);
  const NIGHT = K.ramp(["#2a1c12", "#3a2616"]);
  const SMOKE = K.ramp(["#5a4a40", "#8a7868"]);
  const DUST = K.ramp(["#8d7a62", "#b9a68a"]);
  const EMBER = [WHITE, WARM[4], WARM[3], WARM[2], WARM[1], WARM[0]];
  const FIRE = [WARM[0], WARM[1], WARM[2], WARM[3], WARM[4], WHITE];
  const LOOP = 8 / 12;
  const BIG = { half: 9, post: 3, wall: 31, beam: 4, over: 1, lamp: 8, lampHalf: 2, rows: 43, planks: 5 };
  const SMALL = { half: 4, post: 1, wall: 18, beam: 2, over: 1, lamp: 5, lampHalf: 1, rows: 25, planks: 3 };

  function lantern(g, x, up, s) {
    const m = K.index(x - (up >= 2 ? s.sway : 0));
    if (g.lampHalf === 1) {
      if (m > 1) return 0;
      if (up === 0 || up === g.lamp - 1) return m === 0 ? OUT : 0;
      if (up === 1 || up === g.lamp - 2) return OUT;
      return m === 1 ? OUT : s.lamp > 0 ? WARM[3] : WOOD[0];
    }
    if (up < 2) return m === 0 ? OUT : 0;
    if (up === 2 || up === g.lamp - 2) return m <= g.lampHalf ? OUT : 0;
    if (up === g.lamp - 1) return m === 0 ? OUT : 0;
    if (m > g.lampHalf) return 0;
    if (m === g.lampHalf) return OUT;
    return s.lamp > 0 ? (m === 0 ? (s.lamp > 1 ? WHITE : WARM[4]) : WARM[2]) : WOOD[0];
  }

  function leaf(g, pos, wide, row) {
    const full = g.half * 2;
    if (wide <= 2) return pos === 0 ? WOOD[0] : WOOD[1];
    if (pos === wide - 1) return OUT;
    const k = Math.floor((pos / wide) * full);
    if (g.post > 1) {
      if (k >= full - 5 && k <= full - 4 && row >= 13 && row <= 15) return WARM[3];
      if (row === 5 || row === 24) return WOOD[0];
      if (row === 6 || row === 25) return WOOD[3];
    }
    if (k % g.planks === g.planks - 1) return WOOD[0];
    return row > g.wall - 4 ? WOOD[1] : WOOD[2];
  }

  function hearth(g, x, row, s) {
    if (s.lit <= 0) return x % 5 === 0 && row % 6 === 2 ? NIGHT[1] : NIGHT[0];
    const d = Math.hypot((x + 0.5) / g.half, (row - g.wall * 0.22) / (g.wall * 0.9)) + 0.05 * Math.sin(s.phase * TAU + row * 0.5) + (s.hot ? -0.18 : 0);
    if (s.lit < 1 && d > s.lit) return d > s.lit + 0.2 ? NIGHT[0] : WARM[0];
    return d < 0.3 ? WARM[4] : d < 0.55 ? WARM[3] : d < 0.82 ? WARM[2] : WARM[1];
  }

  function sampler(g, s) {
    const beamTop = g.wall + g.beam;
    return (x, row) => {
      const m = K.index(x);
      if (row > beamTop) return s.lampOn ? lantern(g, x, row - beamTop - 1, s) : 0;
      if (row >= g.wall) {
        const reach = g.half + g.post + g.over;
        if (m > reach) return 0;
        if (row === beamTop || m === reach) return OUT;
        if (row === g.wall) return WOOD[0];
        return row === beamTop - 1 ? WOOD[3] : m % 6 === 5 ? WOOD[0] : WOOD[2];
      }
      if (m >= g.half) {
        if (m > g.half + g.post) return 0;
        if (m === g.half + g.post) return OUT;
        if (m === g.half) return s.lit > 0.5 && s.open > 0.5 ? WARM[2] : WOOD[3];
        return row % 8 === 7 ? WOOD[0] : WOOD[1];
      }
      const pos = x + g.half, wide = Math.max(2, Math.round(g.half * 2 * (1 - s.open)));
      if (pos < wide) return leaf(g, pos, wide, row);
      return hearth(g, x, row, s);
    };
  }

  function portal(p, g, s) {
    const span = g.half + g.post + g.over;
    if ((s.scale ?? 1) < 0.06) return;
    K.scaled(p, sampler(g, s), -span - 1, span, g.rows, s.scale ?? 1, s.shake ?? 0);
    if ((s.scale ?? 1) < 1) return;
    if (s.lit > 0.5 && s.open > 0.6) {
      K.flame(p, 0, -1, g.half * 0.5, g.wall * 0.3, s.phase, FIRE, 3);
      if (g.post > 1) {
        K.flame(p, -4, -1, 2, 5, s.phase, FIRE, 7, 2);
        K.flame(p, 4, -1, 2, 6, s.phase, FIRE, 12, 2);
        for (let x = -5; x <= 4; x++) p.px(x, 0, x % 3 === 0 ? WOOD[0] : OUT);
      }
      p.glow(0, -g.wall * 0.4, g.wall * 0.75, WARM[2], 0.22 * s.open + (s.hot ? 0.2 : 0));
    }
    if (s.lamp > 0 && s.lampOn) p.glow(s.sway, -(g.wall + g.beam + g.lamp * 0.55), g.lamp + 2, WARM[3], 0.3 + 0.25 * (s.lamp - 1));
  }

  function embers(p, g, time, amount, seed) {
    cycle(time, LOOP, amount, seed, (r) => r.range(0.4, 0.62), (age, life, r) => {
      K.spark(p, r.range(-g.half + 2, g.half - 2), -r.range(2, g.wall * 0.35), r.range(-9, 9), -r.range(16, 34), age, life, EMBER, -18, 0.8, 0);
    });
  }

  function idleState(phase) {
    return { open: 1, lit: 1, lamp: 1 + 0.4 * K.pulse(phase), lampOn: true, sway: Math.round(Math.sin(phase * TAU) * 1.2), phase, hot: false };
  }

  function idle(p, phase) {
    portal(p, BIG, idleState(phase));
    embers(p, BIG, phase * LOOP, 7, 21);
  }

  function baseDust(p, g, k, grow) {
    if (k <= 0) return;
    for (const side of [1, -1]) {
      K.puff(p, side * (g.half + g.post + 1 + grow * 2), -1 - grow * 2, 1.4 + grow * 2, DUST[1], 0.55 * k);
      K.puff(p, side * (g.half - 2 - grow * 2), -1 - grow, 1.2 + grow * 1.5, DUST[0], 0.4 * k);
    }
  }

  function open(p, u, info) {
    if (u >= 1) return idle(p, 0);
    const time = u * info.seconds;
    const seed = 1 - seg(u, 0.1, 0.22);
    if (seed > 0) {
      const w = Math.round(5 * ease.outCubic(seg(u, 0, 0.12)));
      for (let x = 0; x <= w; x++) {
        p.dot(x, 0, x < 2 ? WHITE : WARM[3], seed);
        p.dot(K.mirror(x), 0, x < 2 ? WHITE : WARM[3], seed);
      }
      p.glow(0, 0, 5 + 6 * seg(u, 0, 0.12), WARM[3], 0.5 * seed);
    }
    const scale = u < 0.1 ? 0 : ease.outBack(seg(u, 0.1, 0.42), 2.4);
    const lampOn = u > 0.4;
    const push = bump(u, 0.46, 0.6);
    const swing = ease.outBack(seg(u, 0.58, 0.88), 1.3);
    const lit = u > 0.44 ? 1 : 0;
    portal(p, BIG, {
      scale: Math.max(scale, 0.02), open: Math.min(1, swing), lit, lamp: u > 0.44 ? (u < 0.56 ? 2 : 1.2) : 0, lampOn,
      sway: u > 0.6 ? Math.round(Math.sin((u - 0.6) * 22) * 2 * (1 - seg(u, 0.6, 1))) : 0, phase: (u - 1) * info.seconds * 1.5, hot: u > 0.6 && u < 0.78,
      shake: push > 0.5 ? 1 : 0,
    });
    if (u > 0.44 && u < 0.6) {
      const k = seg(u, 0.44, 0.6);
      sparkle(p, 0, -(BIG.wall + BIG.beam + 4), 5 * (1 - k), WHITE, WARM[3], 0.6 * (1 - k));
      for (let row = 1; row < BIG.wall - 1; row += 1) p.dot(BIG.half - 1, -row, row % 3 ? WARM[4] : WHITE, 0.4 + 0.6 * k);
      for (let x = -BIG.half + 1; x < BIG.half; x++) p.dot(x, 0, WARM[4], k);
    }
    baseDust(p, BIG, bump(u, 0.1, 0.5), seg(u, 0.1, 0.5));
    burst(12, 44, (r) => K.spark(p, r.range(-5, 5), -r.range(2, 14), r.range(-45, 45), -r.range(15, 60), time - 0.62 * info.seconds, r.range(0.14, 0.26), EMBER, 150, 2));
    if (swing > 0.6) embers(p, BIG, (u - 1) * info.seconds, 7, 21);
  }

  function close(p, u, info) {
    const time = u * info.seconds;
    const slam = ease.inCubic(seg(u, 0.1, 0.36));
    const shut = u >= 0.36;
    const shock = u >= 0.36 && u < 0.56 ? (Math.floor((u - 0.36) * 28) % 2 ? 1 : -1) : 0;
    const fold = 1 - ease.inBack(seg(u, 0.58, 0.94), 1.8);
    const lampLife = 1 - seg(u, 0.4, 0.56);
    portal(p, BIG, {
      scale: Math.max(fold, 0), open: 1 - slam, lit: shut ? 0 : 1, lamp: lampLife > 0 ? 1 + bump(u, 0, 0.2) : 0, lampOn: true,
      sway: Math.round(Math.sin(u * 30) * (u < 0.36 ? 1 : 3 * (1 - seg(u, 0.36, 0.8)))), phase: time * 1.5, hot: u < 0.14, shake: shock,
    });
    if (shut && u < 0.52) {
      const k = seg(u, 0.36, 0.52);
      for (let row = 1; row < BIG.wall - 1; row++) p.dot(BIG.half - 1 + shock, -row, WARM[4], 1 - k);
      for (let x = -BIG.half + 1; x < BIG.half; x++) p.dot(x + shock, 0, WARM[3], 1 - k);
      burst(8, 61, (r) => K.spark(p, r.range(-12, 12), -BIG.wall + 1, r.range(-8, 8), r.range(5, 25), time - 0.36 * info.seconds, r.range(0.14, 0.26), [DUST[1], DUST[0]], 240, 0, 0));
    }
    if (u > 0.5 && u < 0.92) {
      const k = seg(u, 0.5, 0.92), top = -(BIG.wall + BIG.beam + 5) * Math.max(fold, 0.2);
      K.puff(p, Math.sin(k * 5) * 2, top - 2 - k * 7, 1.5 + k * 2.5, SMOKE[1], 0.5 * (1 - k));
      K.puff(p, 1 + Math.sin(k * 7) * 2, top - k * 4, 1 + k * 2, SMOKE[0], 0.45 * (1 - k));
    }
    baseDust(p, BIG, bump(u, 0.58, 1.02) * (1 - seg(u, 0.88, 1)), seg(u, 0.58, 1));
    burst(6, 62, (r) => K.spark(p, r.range(-9, 9), -r.range(0, 4), r.range(-20, 20), -r.range(20, 50), time - 0.86 * info.seconds, r.range(0.05, 0.12), EMBER, 160));
  }

  function stepIn(p, u, info) {
    const time = u * info.seconds;
    const glow = 1 - seg(u, 0.1, 0.6);
    if (glow > 0) p.glow(2, -15, 18, WARM[2], 0.32 * glow);
    burst(34, 71, (r) => {
      const born = r.range(0, 0.55) * info.seconds, life = r.range(0.14, 0.26), age = time - born;
      if (age < 0 || age >= life) return;
      const k = age / life, x0 = r.range(-7, 7), y0 = -r.range(2, 29);
      const x = x0 + (11 - x0) * ease.inQuad(k), y = y0 + (-14 - y0) * ease.inQuad(k) * 0.6 - Math.sin(k * Math.PI) * 2;
      p.dot(x, y, k < 0.3 ? WARM[4] : k < 0.65 ? WARM[3] : WARM[1], 1 - k * 0.5);
      if (k > 0.15) p.dot(x - 1, y, WARM[0], 0.7 - k * 0.4);
    });
    const dark = seg(u, 0.5, 1);
    if (dark > 0) {
      burst(4, 72, (r, i) => {
        const x = r.range(-4, 6), rise = dark * r.range(8, 16);
        K.puff(p, x + Math.sin(dark * 4 + i) * 2, -10 - i * 4 - rise, 1.5 + dark * 2.6, i % 2 ? SMOKE[1] : SMOKE[0], 0.55 * Math.sin(dark * Math.PI));
      });
      const wick = 1 - seg(u, 0.5, 0.8);
      if (wick > 0) {
        p.dot(0, -1, WARM[3], wick);
        p.dot(-1, -1, WARM[1], wick);
        p.dot(1, -1, WARM[1], wick);
      }
    }
  }

  function arrive(p, u, info) {
    const time = u * info.seconds;
    const wide = Math.round(7 * ease.outBack(seg(u, 0, 0.3), 2)), fade = 1 - seg(u, 0.45, 0.85);
    if (fade > 0 && wide >= 0) {
      for (let row = 0; row <= 30; row++) {
        for (let x = 0; x <= wide; x++) {
          const edge = x === wide || row === 30;
          const color = edge ? (u < 0.3 ? WHITE : WARM[4]) : u < 0.14 ? WARM[4] : 0;
          if (!color) continue;
          p.dot(x, -row, color, fade);
          p.dot(K.mirror(x), -row, color, fade);
        }
      }
      p.glow(0, -14, 20, WARM[3], 0.4 * fade);
    }
    burst(20, 81, (r) => {
      const born = r.range(0.12, 0.4) * info.seconds;
      K.spark(p, r.range(-5, 5), -r.range(1, 10), r.range(-42, 42), -r.range(20, 80), time - born, r.range(0.16, 0.3), EMBER, 300, 1.2);
    });
    const k = seg(u, 0.25, 1);
    if (k > 0) K.floorRing(p, 0, -1, 3 + 10 * ease.outCubic(k), 0.8 + 2 * k, WARM[2], WARM[0], (1 - k) * 0.9);
  }

  function ally(p, u, info, leaving) {
    const time = u * info.seconds, g = SMALL;
    const scale = u < 0.5 ? ease.outBack(seg(u, 0.04, 0.24), 2.2) : 1 - ease.inQuad(seg(u, 0.84, 1));
    const swing = u < 0.5 ? ease.outCubic(seg(u, 0.26, 0.42)) : 1 - ease.inCubic(seg(u, 0.66, 0.8));
    if (u < 0.12) p.dot(0, 0, WHITE, 1);
    portal(p, g, {
      scale: Math.max(scale, 0.02), open: swing, lit: u > 0.24 && u < 0.8 ? 1 : 0, lamp: u > 0.2 && u < 0.84 ? 1 : 0, lampOn: u > 0.2 || u > 0.5,
      sway: u > 0.66 && u < 0.84 ? (Math.floor(u * 40) % 2 ? 1 : -1) : 0, phase: time * 2, hot: u > 0.3 && u < 0.42,
    });
    const from = 0.36 * info.seconds, span = 0.38 * info.seconds;
    burst(8, leaving ? 91 : 92, (r) => {
      const born = from + r.range(0, 0.5) * span, life = r.range(0.4, 0.6) * span, age = time - born;
      if (age < 0 || age >= life) return;
      const k = age / life, bx = -r.range(4, 10), by = -r.range(3, 15), gx = r.range(-2, 2), gy = -r.range(3, 10);
      const e = leaving ? ease.inQuad(k) : ease.outQuad(k);
      const x = leaving ? bx + (gx - bx) * e : gx + (bx - gx) * e, y = (leaving ? by + (gy - by) * e : gy + (by - gy) * e) - Math.sin(k * Math.PI) * 2;
      p.px(x, y, k < 0.4 ? WARM[4] : WARM[2]);
    });
    if (u > 0.82 && u < 1) {
      const k = seg(u, 0.82, 1);
      K.puff(p, 0, -6 - k * 6, 1.2 + k * 2, SMOKE[1], 0.5 * (1 - k));
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
