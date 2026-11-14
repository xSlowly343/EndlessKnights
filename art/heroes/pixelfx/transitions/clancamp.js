export default function clancamp(fx) {
  const { seg, bump, ease, TAU, cycle, burst, sparkle, WHITE } = fx;
  const K = fx.kit;
  const WOOD = K.ramp(["#2a1710", "#5a3420", "#8a5632", "#b47a48"]);
  const RED = K.ramp(["#7a1f1c", "#b8362e", "#e0574a", "#ff8a6e"]);
  const CLOTH = K.ramp(["#8f7c58", "#b5a27a", "#d9c7a0", "#f2e6c8"]);
  const VEIL = K.ramp(["#2a0f10", "#561616", "#8a2620"]);
  const HEAT = K.ramp(["#a8322a", "#e0574a", "#f08a3c", "#ffd27a", "#fff3c4"]);
  const SMOKE = K.ramp(["#4a3a3a", "#7a6a66"]);
  const DUST = K.ramp(["#8a7460", "#b49c84"]);
  const EMBER = [WHITE, HEAT[4], HEAT[3], HEAT[2], HEAT[1], HEAT[0]];
  const LOOP = 8 / 12;
  const BIG = { pole: 10, thick: 3, top: 38, cap: 3, rope: 35, half: 6, drop: 16, veil: 34, fireHalf: 5, fireTall: 15, bands: [8, 20, 30], face: 25, mark: 3 };
  const SMALL = { pole: 5, thick: 2, top: 20, cap: 2, rope: 19, half: 3, drop: 8, veil: 18, fireHalf: 2.5, fireTall: 8, bands: [6, 13], face: -9, mark: 1 };

  function poleCell(g, k, part) {
    const last = g.thick - 1;
    if (part > g.top + g.cap) return k === 0 || k === last ? RED[1] : 0;
    if (part > g.top) return k < 0 || k > last ? RED[0] : k === 0 ? RED[3] : RED[2];
    if (k < 0 || k > last) return g.thick > 2 ? WOOD[0] : 0;
    if (g.bands.some((b) => part === b || part === b + 1)) return k === 0 ? RED[3] : RED[2];
    if (g.face > 0) {
      if (part === g.face + 2 && k === 1) return HEAT[3];
      if (part === g.face && k >= 1) return WOOD[0];
      if (part === g.face - 2 || part === g.face + 4) return WOOD[1];
    }
    return k === 0 ? WOOD[3] : k === last ? WOOD[1] : WOOD[2];
  }

  function pole(p, g, side, rise, shake) {
    if (rise <= 0) return;
    const full = g.top + g.cap + 2, sunk = Math.round((1 - rise) * full);
    for (let row = 0; row + sunk <= full; row++) {
      for (let k = -1; k <= g.thick; k++) {
        const color = poleCell(g, side > 0 ? k : g.thick - 1 - k, row + sunk);
        if (!color) continue;
        const x = side > 0 ? g.pole + k : -g.pole - g.thick + k;
        p.px(x + shake, -row, color);
      }
    }
    if (g.face > 0 && rise >= 1) p.glow(side * (g.pole + 1.5), -(g.face + 2), 4, HEAT[3], 0.3);
  }

  function veil(p, g, s) {
    const top = Math.floor(g.veil * Math.min(1, s.veil));
    for (let row = 0; row < top; row++) {
      for (let x = -g.pole + 1; x < g.pole - 1; x++) {
        const t = row / g.veil + 0.06 * Math.sin(x * 1.3 + s.phase * TAU) + 0.04 * Math.sin(row * 0.7 - s.phase * TAU * 2);
        const edge = row === top - 1 && s.veil < 1;
        p.px(x, -row, edge ? HEAT[1] : t < 0.3 ? VEIL[2] : t < 0.62 ? VEIL[1] : VEIL[0]);
      }
    }
  }

  function fire(p, g, s) {
    if (s.fire <= 0) return;
    const big = g.thick > 2;
    if (big) {
      K.flame(p, -4, -1, g.fireHalf * 0.5, g.fireTall * 0.55 * s.fire, s.phase, HEAT, 5, 2);
      K.flame(p, 4, -1, g.fireHalf * 0.5, g.fireTall * 0.6 * s.fire, s.phase, HEAT, 9, 2);
    }
    K.flame(p, 0, -1, g.fireHalf * Math.min(1.25, 0.6 + s.fire * 0.4), g.fireTall * s.fire, s.phase, HEAT, 2);
    if (big) {
      for (let x = -6; x <= 5; x++) p.px(x, 0, x % 4 === 0 ? WOOD[3] : WOOD[1]);
      for (let x = -4; x <= 3; x++) p.px(x, -1, (x + 8) % 3 === 0 ? WOOD[0] : WOOD[2]);
    } else {
      for (let x = -3; x <= 2; x++) p.px(x, 0, WOOD[1]);
    }
    p.glow(0, -g.fireTall * 0.4, g.fireTall * 1.3, HEAT[2], 0.22 * Math.min(1.6, s.fire) + (s.hot ? 0.2 : 0));
  }

  function banner(p, g, s) {
    if (s.rope > 0) {
      const reach = Math.round((g.pole - 1) * s.rope);
      for (let m = g.pole - 1; m >= g.pole - 1 - reach && m >= 0; m--) {
        const sag = m < g.pole / 2 ? 1 : 0;
        p.px(m, -(g.rope - sag), WOOD[0]);
        p.px(K.mirror(m), -(g.rope - sag), WOOD[0]);
      }
    }
    if (s.unroll <= 0) return;
    const length = Math.max(2, Math.round(g.drop * Math.min(s.unroll, 1.15))), rolled = s.unroll < 0.96;
    for (let k = 0; k <= length; k++) {
      const row = g.rope - 1 - k, depth = k / g.drop;
      const swing = Math.sin(s.phase * TAU - k * 0.55) * s.wave * depth;
      const shift = Math.round(swing);
      const fold = Math.cos(s.phase * TAU - k * 0.55) * s.wave > 0.55 && k > 2;
      for (let m = 0; m < g.half; m++) {
        const tail = length - k;
        if (!rolled && tail < 3 && m < 3 - tail && g.thick > 2) continue;
        if (!rolled && g.thick === 2 && tail < 1 && m < 1) continue;
        for (const x of [m, K.mirror(m)]) {
          let color = fold ? CLOTH[1] : x < 0 ? CLOTH[3] : CLOTH[2];
          if (m === g.half - 1) color = RED[2];
          if (k === 0) color = RED[1];
          const dy = k - Math.round(g.drop * 0.5), reachOf = Math.abs(dy) + m;
          if (!rolled && reachOf <= g.mark) color = reachOf === 0 && g.mark > 1 ? HEAT[3] : RED[2];
          if (rolled && k >= length - 1) color = k === length ? CLOTH[0] : CLOTH[1];
          p.px(x + shift, -row, color);
        }
      }
    }
  }

  function portal(p, g, s) {
    veil(p, g, s);
    fire(p, g, s);
    pole(p, g, -1, s.left, s.shake ?? 0);
    pole(p, g, 1, s.right, s.shake ?? 0);
    banner(p, g, s);
  }

  function embers(p, g, time, amount, seed) {
    cycle(time, LOOP, amount, seed, (r) => r.range(0.4, 0.64), (age, life, r) => {
      K.spark(p, r.range(-g.fireHalf, g.fireHalf), -r.range(3, g.fireTall), r.range(-12, 12), -r.range(20, 42), age, life, EMBER, -20, 0.6, 0);
    });
  }

  function idleState(phase) {
    return { left: 1, right: 1, rope: 1, unroll: 1, wave: 1.3, veil: 1, fire: 1, phase, hot: false };
  }

  function idle(p, phase) {
    portal(p, BIG, idleState(phase));
    embers(p, BIG, phase * LOOP, 8, 51);
  }

  function slamDust(p, x, k) {
    if (k <= 0 || k >= 1) return;
    K.puff(p, x - 2 - k * 3, -1 - k * 2, 1.4 + k * 2.2, DUST[1], 0.55 * (1 - k));
    K.puff(p, x + 2 + k * 3, -1 - k * 2, 1.4 + k * 2.2, DUST[0], 0.5 * (1 - k));
  }

  function open(p, u, info) {
    if (u >= 1) return idle(p, 0);
    const time = u * info.seconds, g = BIG;
    if (u < 0.16) {
      const k = seg(u, 0, 0.16);
      sparkle(p, 0, -1, 3 * (1 - k), WHITE, HEAT[3], 0.5);
      burst(6, 53, (r) => K.spark(p, 0, -1, r.range(-50, 50), -r.range(20, 70), time, r.range(0.05, 0.1), EMBER, 300));
    }
    const left = ease.outBack(seg(u, 0.1, 0.3), 1.6), right = ease.outBack(seg(u, 0.2, 0.4), 1.6);
    const shake = (u > 0.24 && u < 0.3) || (u > 0.34 && u < 0.4) ? 1 : 0;
    const whoosh = seg(u, 0.56, 0.74), settle = seg(u, 0.74, 1);
    const blaze = u < 0.1 ? 0 : u < 0.56 ? 0.22 : whoosh < 1 ? 0.22 + 1.4 * ease.outCubic(whoosh) : 1.62 - 0.62 * ease.outCubic(settle);
    portal(p, g, {
      left: Math.min(left, 1.04), right: Math.min(right, 1.04), shake, rope: seg(u, 0.38, 0.48), unroll: ease.outBack(seg(u, 0.46, 0.74), 2.2),
      wave: 1.3 + 2 * (1 - seg(u, 0.5, 1)), veil: ease.outCubic(seg(u, 0.56, 0.8)), fire: blaze, phase: (u - 1) * info.seconds * 1.5, hot: u > 0.6 && u < 0.78,
    });
    slamDust(p, -g.pole - 1, seg(u, 0.2, 0.5));
    slamDust(p, g.pole + 1, seg(u, 0.3, 0.6));
    burst(14, 55, (r) => K.spark(p, r.range(-4, 4), -r.range(2, 10), r.range(-40, 40), -r.range(40, 110), time - 0.6 * info.seconds, r.range(0.14, 0.28), EMBER, 120, 1.5));
    if (u > 0.74) embers(p, g, (u - 1) * info.seconds, 8, 51);
  }

  function close(p, u, info) {
    const time = u * info.seconds, g = BIG;
    const flare = bump(u, 0, 0.26);
    const blaze = u < 0.22 ? 1 + 0.6 * flare : Math.max(0, 1 - ease.outCubic(seg(u, 0.22, 0.5)) * 1.05);
    const left = 1 - ease.inBack(seg(u, 0.6, 0.92), 1.5), right = 1 - ease.inBack(seg(u, 0.52, 0.84), 1.5);
    portal(p, g, {
      left: Math.max(left, 0), right: Math.max(right, 0), rope: 1 - seg(u, 0.46, 0.54), unroll: 1 - ease.inCubic(seg(u, 0.2, 0.48)), wave: 1.3 + 2.4 * flare,
      veil: 1 - ease.inCubic(seg(u, 0.2, 0.46)), fire: blaze, phase: time * 2, hot: u < 0.2, shake: 0,
    });
    burst(12, 57, (r) => K.spark(p, r.range(-5, 5), -r.range(2, 12), r.range(-36, 36), -r.range(30, 90), time - 0.04, r.range(0.14, 0.28), EMBER, 100, 1.5));
    const k = seg(u, 0.4, 1);
    if (k > 0 && k < 1) {
      const fade = Math.sin(k * Math.PI) * (1 - seg(u, 0.88, 1));
      K.puff(p, Math.sin(k * 4) * 2, -4 - k * 16, 2 + k * 3.4, SMOKE[1], 0.5 * fade);
      K.puff(p, 2 - Math.sin(k * 6) * 2, -2 - k * 9, 1.6 + k * 2.6, SMOKE[0], 0.5 * fade);
      for (let x = -3; x <= 2; x++) p.dot(x, 0, x % 2 ? HEAT[1] : HEAT[0], (1 - k) * (1 - seg(u, 0.8, 1)));
    }
    slamDust(p, g.pole + 1, seg(u, 0.56, 0.96));
    slamDust(p, -g.pole - 1, seg(u, 0.64, 1.02));
  }

  function stepIn(p, u, info) {
    const time = u * info.seconds;
    const env = u < 0.3 ? ease.outCubic(seg(u, 0, 0.3)) : 1 - ease.inQuad(seg(u, 0.5, 0.92));
    if (env > 0.04) {
      const phase = time * 2.5;
      K.flame(p, -6, -1, 3.2, 17 * env, phase, HEAT, 4, 1);
      K.flame(p, 6, -1, 3.2, 19 * env, phase, HEAT, 8, 1);
      K.flame(p, -2, -1, 4, 28 * env, phase, HEAT, 1, 1);
      K.flame(p, 3, -1, 3.6, 24 * env, phase, HEAT, 6, 1);
      p.glow(0, -12, 20, HEAT[2], 0.3 * env);
    }
    K.floorRing(p, 0, -1, 4 + 8 * seg(u, 0, 0.4), 1 + 1.6 * seg(u, 0, 0.4), HEAT[3], HEAT[0], 1 - seg(u, 0.2, 0.5));
    burst(26, 61, (r) => {
      const born = r.range(0.1, 0.8) * info.seconds;
      K.spark(p, r.range(-8, 8), -r.range(4, 26), r.range(-10, 10), -r.range(24, 60), time - born, r.range(0.12, 0.2), EMBER, -40, 0.5, 0.04);
    });
  }

  function arrive(p, u, info) {
    const time = u * info.seconds;
    const snap = seg(u, 0, 0.55);
    if (snap < 1) {
      const reach = Math.round(22 * ease.outQuart(seg(u, 0, 0.22))), gone = Math.round(22 * ease.inQuad(seg(u, 0.26, 0.55)));
      for (let k = gone; k < reach; k++) {
        const x = -11 + k, wave = Math.sin(k * 0.6 - time * 34) * (1.5 + 2.5 * (1 - snap)) * (k / 22);
        const half = 3 - (k > 18 ? (k - 18) * 0.7 : 0);
        for (let dy = -half; dy <= half; dy++) {
          const edge = Math.abs(dy) >= half - 0.5;
          p.px(x, -30 + wave + dy, edge ? RED[2] : dy < 0 ? CLOTH[3] : Math.cos(k * 0.6 - time * 34) > 0.4 ? CLOTH[1] : CLOTH[2]);
        }
      }
      if (u > 0.08 && u < 0.3) sparkle(p, -11 + reach, -30, 3 * (1 - seg(u, 0.08, 0.3)), WHITE, CLOTH[3], 0.3);
    }
    const k = seg(u, 0.1, 0.9);
    if (k > 0) K.floorRing(p, 0, -1, 2 + 11 * ease.outCubic(k), 0.6 + 2.4 * ease.outCubic(k), HEAT[1], HEAT[0], 1 - k);
    if (u > 0.08 && u < 0.4) p.glow(0, -3, 14, HEAT[2], 0.4 * (1 - seg(u, 0.08, 0.4)));
    burst(24, 63, (r) => {
      const born = r.range(0.08, 0.36) * info.seconds;
      K.spark(p, r.range(-10, 10), -r.range(0, 3), r.range(-14, 14), -r.range(40, 120), time - born, r.range(0.16, 0.3), EMBER, 90, 2.4, 0.04);
    });
  }

  function ally(p, u, info, leaving) {
    const time = u * info.seconds, g = SMALL;
    const up = u < 0.5;
    const left = up ? ease.outBack(seg(u, 0.03, 0.2), 1.5) : 1 - ease.inQuad(seg(u, 0.84, 1));
    const right = up ? ease.outBack(seg(u, 0.1, 0.27), 1.5) : 1 - ease.inQuad(seg(u, 0.8, 0.96));
    if (u < 0.1) p.dot(0, 0, WHITE, 1);
    portal(p, g, {
      left: Math.min(left, 1), right: Math.min(right, 1), rope: up ? seg(u, 0.22, 0.3) : 1 - seg(u, 0.76, 0.82), unroll: up ? ease.outBack(seg(u, 0.26, 0.42), 2) : 1 - ease.inCubic(seg(u, 0.64, 0.8)),
      wave: 1, veil: up ? seg(u, 0.28, 0.42) : 1 - seg(u, 0.64, 0.78), fire: up ? 1.3 * seg(u, 0.26, 0.4) : 1 - seg(u, 0.62, 0.8), phase: time * 2, hot: u > 0.3 && u < 0.42,
    });
    const from = 0.36 * info.seconds, span = 0.38 * info.seconds;
    burst(8, leaving ? 65 : 66, (r) => {
      const born = from + r.range(0, 0.5) * span, life = r.range(0.4, 0.6) * span, age = time - born;
      if (age < 0 || age >= life) return;
      const k = age / life, bx = -r.range(5, 10), by = -r.range(3, 15), gx = r.range(-2, 2), gy = -r.range(2, 8);
      const e = leaving ? ease.inQuad(k) : ease.outQuad(k);
      const x = leaving ? bx + (gx - bx) * e : gx + (bx - gx) * e, y = (leaving ? by + (gy - by) * e : gy + (by - gy) * e) - Math.sin(k * Math.PI) * 2;
      p.px(x, y, k < 0.4 ? HEAT[4] : HEAT[2]);
    });
    if (u > 0.78 && u < 1) {
      const k = seg(u, 0.78, 1);
      K.puff(p, 0, -3 - k * 7, 1.2 + k * 2, SMOKE[1], 0.5 * (1 - k));
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
