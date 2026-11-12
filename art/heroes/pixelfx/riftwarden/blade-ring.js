import { Particles, Bits, Lights, P, B, Rng, fit, clamp, seg, ease, hex, flare, sparkle, WHITE } from "../../core/index.js";
import { sword } from "./blade-volley.js";

const END = 0.8, N = 6, STAB = 0.2, STEP = 0.018, BREAK = 0.5, FLOOR = 16, LEN = 13;

export const meta = {
  name: "Blade ring",
  desc: "Six swords blink in over the target in a crescent, a ring of light turning behind; they draw back, drive in one by one, meet in a white cross and break into splinters.",
  tags: ["steel", "impact", "one-shot"],
  size: [108, 88],
  origin: [62, 48],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#141a2c", "#3a4a6e", "#6e86b4", "#a8c0e4", "#dcecff", "#ffffff"],
    hilt: ["#7a4a0c", "#f0b424", "#fff0a0"],
    seed: 142,
  },
};

export function create(o) {
  const S = fit(o.palette, 6), G = o.hilt.map(hex), R = new Rng(o.seed);
  const ps = new Particles({ rng: R, ground: () => FLOOR, sparks: [WHITE, S[4], S[2]] }), bits = new Bits({ ground: () => FLOOR }), lights = new Lights();
  let t = 0, stabbed = 0, broke = false;
  lights.add(0, 0, 30, S[3], 0.5, 0.2);
  const angle = (i) => ((300 - i * 33) * Math.PI) / 180;
  const tipAt = (i) => {
    const s = STAB + i * STEP;
    return t < s ? 18 + 5 * ease.outCubic(seg(t, 0.09, s)) - 4 * (1 - seg(t, 0, 0.09)) : 23 - 21 * ease.outQuart(seg(t, s, s + 0.045)) + 1.5 * Math.sin(seg(t, s + 0.045, s + 0.14) * Math.PI);
  };
  return {
    update(dt) {
      t += dt;
      while (stabbed < N && t >= STAB + stabbed * STEP + 0.03) {
        const a = angle(stabbed);
        lights.add(Math.cos(a) * 4, Math.sin(a) * 4, 18, S[4], 0.7, 0.14);
        for (let k = 0; k < 4; k++) {
          const b = a + Math.PI + R.range(-0.9, 0.9), sp = R.range(80, 200);
          ps.spawn(P.SPARK, 0, 0, Math.cos(b) * sp, Math.sin(b) * sp, R.range(0.15, 0.35));
        }
        stabbed++;
      }
      if (!broke && t >= BREAK) {
        broke = true;
        lights.add(0, 0, 36, S[4], 0.8, 0.24);
        for (let i = 0; i < N; i++) {
          const a = angle(i), ca = Math.cos(a), sa = Math.sin(a);
          for (let k = 0; k < 4; k++) {
            const d = 3 + k * 4, sp = R.range(40, 110);
            bits.add({ kind: B.SHARD, x: ca * d, y: sa * d, vx: ca * sp + R.range(-30, 30), vy: sa * sp - R.range(20, 90), a: a, va: R.range(-18, 18), s: R.range(3.5, 6.5), c: k === 3 ? G[1] : S[3], drag: 2, bounce: 0.5, life: R.range(0.22, 0.3) });
          }
          ps.spawn(P.STAR, ca * 16, sa * 16, ca * 20, sa * 20, R.range(0.14, 0.26), S[4], R.range(2, 3.4), R.range(0, 0.08));
          ps.spawn(P.MOTE, ca * 8, sa * 8, ca * R.range(90, 150), sa * R.range(90, 150), R.range(0.1, 0.16), S[4], 1);
        }
      }
      ps.update(dt);
      bits.update(dt);
      lights.update(dt);
    },
    drawBack(p) {
      if (t >= END) return;
      const k = seg(t, 0, 0.12), hold = 1 - seg(t, BREAK - 0.04, BREAK + 0.06);
      if (hold > 0) {
        const r = 20 * ease.outBack(k, 1.4);
        p.glowRing(0, 0, r, 5, S[2], 0.5 * hold);
        p.ring(0, 0, r, 1, S[3], (v, g) => (Math.sin(g * N + t * 14) > -0.2 ? hold : 0));
        p.ring(0, 0, r - 3, 1, S[1], (v, g) => (Math.sin(g * 4 - t * 9) > 0 ? hold * 0.8 : 0));
        for (let i = 0; i < N; i++) if (Math.sin(angle(i)) < 0 && t < BREAK) one(p, i);
      }
    },
    draw(p) {
      if (t >= END) return;
      if (t < BREAK) for (let i = 0; i < N; i++) if (Math.sin(angle(i)) >= 0) one(p, i);
      const meet = STAB + N * STEP * 0.5 + 0.04, f = clamp(1 - (t - meet) / 0.14);
      if (t >= meet && f > 0) {
        flare(p, 0, 0, 22 * f + 4, f, [S[1], S[2], S[3], S[4], S[5]]);
        p.ring(0, 0, 6 + 20 * (1 - f * f), 1.5, f > 0.5 ? WHITE : S[4], f);
        p.circle(0, 0, 6 * f, WHITE, 1);
      }
      if (t >= BREAK && t < BREAK + 0.1) {
        const b = 1 - (t - BREAK) / 0.1;
        sparkle(p, 0, 0, 16 * b, S[4], WHITE);
        p.ring(0, 0, 8 + 18 * (1 - b), 2, WHITE, b);
      }
      bits.draw(p);
      ps.draw(p, "front");
      lights.draw(p);
    },
  };
  function one(p, i) {
    const a = angle(i), tip = tipAt(i), born = i * 0.008, k = seg(t, born, born + 0.06);
    if (k <= 0) return;
    const x = Math.cos(a) * tip, y = Math.sin(a) * tip, s = STAB + i * STEP;
    if (t >= s && t < s + 0.07) p.line(Math.cos(a) * 26, Math.sin(a) * 26, x, y, WHITE, (u) => u * (1 - (t - s) / 0.07));
    sword(p, x, y, a + Math.PI, LEN, S, G, { a: k, white: k < 1 || (t > BREAK - 0.035), roll: i % 2 ? 1 : -1 });
    if (k < 1) sparkle(p, Math.cos(a) * (tip + 7), Math.sin(a) * (tip + 7), 5 * (1 - k), S[4], WHITE);
    else if (t < STAB) p.light(Math.cos(a) * (tip + LEN), Math.sin(a) * (tip + LEN), S[4], 0.8);
  }
}
