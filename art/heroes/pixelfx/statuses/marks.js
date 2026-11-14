export default function marks(fx) {
  const { clamp, seg, bump, TAU, cycle, burst, WHITE } = fx;
  const K = fx.kit;
  const FIRE = K.ramp(["#7a2412", "#c8502a", "#ff7a3c", "#ffb04a", "#ffe08a"]);
  const STAR = K.ramp(["#8a6a1a", "#f0e04c", "#fff7b0"]);
  const HAZE = K.ramp(["#141a2c", "#2a3350", "#4a5578", "#c9d2e6"]);
  const HEX = K.ramp(["#2a1648", "#4a2a7a", "#b07cff", "#e0ccff"]);
  const GOLD = K.ramp(["#6a3f0e", "#f0b43c", "#ffd27a"]);
  const BLOOD = K.ramp(["#7a1f1a", "#e0574a", "#ffb0a0"]);
  const LOOP = 8 / 12;

  function burnTop(tier) {
    const tongues = [2, 3, 5][tier - 1], tall = [6, 9, 12][tier - 1];
    return (p, phase, info, g) => {
      const s = g.s, span = g.w * [0.3, 0.5, 0.72][tier - 1];
      for (let i = 0; i < tongues; i++) {
        const x = tongues === 1 ? 0 : -span / 2 + (span * i) / (tongues - 1);
        const middle = 1 - Math.abs(i - (tongues - 1) / 2) / Math.max(1, tongues / 2) * 0.35;
        K.flame(p, x, 3 * s, (2.2 + (i % 2) * 0.6) * s, tall * s * middle, phase, FIRE, i * 7 + tier, 1);
      }
      cycle(phase * LOOP, LOOP, (2 + tier * 2) * s, 211 + tier, (r) => r.range(0.3, 0.55), (age, life, r) => {
        K.spark(p, r.range(-span / 2, span / 2), -r.range(2, tall * 0.6) * s, r.range(-6, 6) * s, -r.range(16, 30) * s, age, life, [FIRE[4], FIRE[3], FIRE[2], FIRE[1]], -8, 0.6);
      });
      p.glow(0, -tall * s * 0.3, tall * s * 0.6, FIRE[2], 0.18 + 0.08 * K.pulse(phase, 2));
    };
  }

  function stun(p, phase, info, g) {
    const s = g.s, rx = 10 * s, ry = 3 * s, cy = -6 * s;
    for (let i = 0; i < 20 * s; i++) {
      const a = (i / (20 * s)) * TAU;
      if ((i + Math.floor(phase * 8)) % 4 === 0) p.px(Math.cos(a) * rx, cy + Math.sin(a) * ry, STAR[0]);
    }
    const stars = [0, 1, 2].map((i) => {
      const a = (phase + i / 3) * TAU;
      return { x: Math.cos(a) * rx, y: cy + Math.sin(a) * ry, near: Math.sin(a) > 0, spin: (i + Math.floor(phase * 8)) % 2 };
    });
    for (const star of stars.filter((q) => !q.near)) {
      p.px(star.x, star.y, STAR[1]);
      if (s > 1) for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) p.px(star.x + dx, star.y + dy, STAR[0]);
    }
    for (const star of stars.filter((q) => q.near)) {
      const arm = s + (star.spin ? 1 : 0);
      for (let d = 1; d <= arm; d++) for (const [dx, dy] of star.spin ? [[1, 0], [-1, 0], [0, 1], [0, -1]] : [[1, 1], [-1, 1], [1, -1], [-1, -1]]) p.px(star.x + dx * d, star.y + dy * d, d === arm ? STAR[0] : STAR[1]);
      if (!star.spin) for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) p.px(star.x + dx, star.y + dy, STAR[1]);
      p.px(star.x, star.y, STAR[2]);
    }
  }

  function blind(p, phase, info, g) {
    const s = g.s, half = 10 * s, tall = 3 * s;
    for (let x = -half; x < half; x++) {
      const taper = 1 - (Math.abs(x + 0.5) / half) ** 3;
      const upper = Math.round(tall * taper + Math.sin((x / (4 * s) + phase) * TAU) * 0.9 * s);
      const lower = Math.round(tall * taper + Math.sin((x / (5 * s) - phase) * TAU + 1) * 0.9 * s);
      for (let y = -upper; y <= lower; y++) {
        const drift = Math.sin((x / (7 * s) + y / (3 * s) + phase * 2) * TAU);
        p.px(x, y, y === lower ? HAZE[0] : drift > 0.55 ? HAZE[2] : HAZE[1]);
      }
    }
    for (const [cx, shift] of [[-5 * s, 0], [4 * s, 0.5]]) {
      const lift = K.pulse(phase, 1, shift) > 0.5 ? 0 : -1;
      for (let d = -s; d <= s; d++) {
        p.px(cx + d, lift + d, HAZE[3]);
        p.px(cx + d, lift - d, HAZE[3]);
      }
    }
  }

  function curse(p, phase, info, g) {
    const s = g.s, cy = -8 * s, wide = 6 * s;
    const lid = 0.45 + 0.55 * K.pulse(phase, 1), tall = Math.max(1, Math.round(3.4 * s * lid));
    for (let x = -wide; x <= wide; x++) {
      const arch = Math.round(tall * Math.sqrt(Math.max(0, 1 - (x / wide) ** 2)));
      for (let y = -arch; y <= arch; y++) {
        const rim = Math.abs(y) === arch;
        p.px(x, cy + y, rim ? HEX[2] : Math.abs(x) <= s && Math.abs(y) < arch ? HEX[0] : HEX[1]);
      }
      p.px(x, cy + arch + 1, HEX[0]);
    }
    if (tall > s) p.px(-1, cy - 1, HEX[3]);
    p.px(-wide - 1, cy, HEX[2]);
    p.px(wide + 1, cy, HEX[2]);
    for (let i = 0; i < 3; i++) {
      const x = (i - 1) * 3 * s, drop = ((phase + i / 3) % 1) * 4 * s;
      p.px(x, cy + tall + 2 + drop, drop < 2 * s ? HEX[2] : HEX[1]);
    }
    for (let i = 0; i < 3; i++) {
      const a = (phase + i / 3) * TAU;
      p.px(Math.cos(a) * (wide + 1), cy - 5 * s + Math.sin(a) * s - (i === 0 ? 1 : 0), HEX[3]);
    }
    p.glow(0, cy, wide * 1.2, HEX[2], 0.12 + 0.14 * lid);
  }

  function diamond(p, radius, color, s) {
    const r = Math.round(radius);
    for (let i = 0; i <= r; i++)
      for (const [dx, dy] of [[1, 1], [-1, 1], [1, -1], [-1, -1]])
        for (let w = 0; w < s; w++) p.px(dx > 0 ? i + w : -i - 1 - w, dy > 0 ? r - i : -(r - i) - 1, color);
  }

  function markCore(p, s, beat, turn) {
    diamond(p, 6 * s + 1, GOLD[0], s);
    diamond(p, 6 * s, GOLD[1], s);
    for (let i = 0; i < 4; i++) {
      const a = (i / 4 + turn / 4) * TAU, c = Math.cos(a), n = Math.sin(a);
      for (let d = 8 * s; d <= 10 * s; d++) p.px(c * d - (c < -0.5 ? 1 : 0), n * d - (n < -0.5 ? 1 : 0), d === 10 * s ? GOLD[0] : GOLD[2]);
    }
    const dot = beat > 0.5 ? s : s - 1;
    for (let x = -dot - 1; x <= dot; x++) for (let y = -dot - 1; y <= dot; y++) p.px(x, y, Math.abs(x + 0.5) + Math.abs(y + 0.5) <= dot + 0.5 ? (beat > 0.75 ? BLOOD[2] : BLOOD[1]) : BLOOD[0]);
  }

  function mark(p, phase, info, g) {
    markCore(p, g.s, K.pulse(phase, 2), phase);
    p.glow(0, 0, 7 * g.s, GOLD[1], 0.08 + 0.08 * K.pulse(phase, 2));
  }

  function markOn(p, u, info, g) {
    const s = g.s;
    if (u >= 1) return mark(p, 0, info, g);
    const close = seg(u, 0, 0.7), radius = 11 * s - 5 * s * close * close;
    diamond(p, radius + 1, GOLD[0], s);
    diamond(p, radius, close > 0.8 ? WHITE : GOLD[2], s);
    const snap = bump(u, 0.55, 1);
    if (snap > 0) {
      fx.sparkle(p, 0, 0, 5 * s * snap, WHITE, GOLD[2], 0.8);
      p.glow(0, 0, 9 * s, GOLD[1], 0.3 * snap);
    }
    burst(6 * s, 307, (r) => {
      const a = r.range(0, TAU), k = clamp(u * 1.3 - 0.5);
      if (k <= 0) return;
      p.px(Math.cos(a) * (6 + k * 5) * s, Math.sin(a) * (6 + k * 5) * s, k < 0.6 ? GOLD[2] : GOLD[1]);
    });
  }

  return { burnTop1: burnTop(1), burnTop2: burnTop(2), burnTop3: burnTop(3), stun, blind, curse, mark, markOn };
}
