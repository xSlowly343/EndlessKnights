import { Particles, Bits, Lights, P, B, Rng, fit, clamp, seg, ease, band, crystal, sparkle, WHITE } from "../../core/index.js";

const END = 0.86, BREAK = 0.46;

export const meta = {
  name: "Glacier bloom",
  desc: "Ice spikes burst up in a fan one after another, frost racing over the floor; a glint sweeps them, they flash white and shatter into chunks, splinters and snow.",
  tags: ["ice", "impact", "ground", "one-shot"],
  size: [120, 76],
  origin: [60, 62],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#0c1c3a", "#1c4a80", "#3a86c8", "#72c0ee", "#b4e6ff", "#e8f8ff", "#ffffff"],
    seed: 122,
  },
};

const SPIKES = [
  { x: -24, h: 13, w: 6, lean: -8, at: 0.09, back: true }, { x: 20, h: 14, w: 6, lean: 6, at: 0.1, back: true },
  { x: -12, h: 27, w: 8, lean: -7, at: 0.05, back: true }, { x: 10, h: 30, w: 9, lean: 5, at: 0.06, back: true },
  { x: 0, h: 44, w: 12, lean: 1, at: 0.02 }, { x: -18, h: 18, w: 7, lean: -9, at: 0.07 }, { x: 15, h: 19, w: 7, lean: 7, at: 0.08 },
  { x: -7, h: 22, w: 7, lean: -4, at: 0.04 }, { x: 6, h: 17, w: 6, lean: 4, at: 0.05 },
];

export function create(o) {
  const C = fit(o.palette, 7), R = new Rng(o.seed), floor = () => 0;
  const ps = new Particles({ rng: R, ground: floor, sparks: [C[6], C[4], C[3]], haze: C[3] }), bits = new Bits({ ground: floor }), lights = new Lights();
  lights.add(0, -6, 40, C[4], 0.8, 0.3);
  for (let i = 0; i < 12; i++) ps.spawn(P.VAPOUR, R.range(-26, 26), R.range(-6, 0), R.range(-50, 50), R.range(-22, -4), R.range(0.3, 0.6), C[4], R.range(4, 7), R.range(0, 0.1));
  for (let i = 0; i < 16; i++) {
    const a = -Math.PI / 2 + R.range(-1.3, 1.3), sp = R.range(80, 220);
    ps.spawn(P.SPARK, 0, -3, Math.cos(a) * sp, Math.sin(a) * sp, R.range(0.2, 0.45));
  }
  let t = 0, broke = false;
  const spike = (p, s) => {
    const k = ease.outBack(seg(t, s.at, s.at + 0.11), 2.2);
    if (k <= 0) return;
    crystal(p, s.x, 1, s.h * k, s.w * (0.7 + 0.3 * k), s.lean * k, C, {
      white: (t > s.at && t < s.at + 0.035) || (t > BREAK - 0.04 && t <= BREAK),
      glint: t > 0.24 && t < 0.4 ? seg(t, 0.24, 0.4) * 1.2 - 0.1 + s.x * 0.004 : -1,
    });
  };
  return {
    update(dt) {
      t += dt;
      if (!broke && t >= BREAK) {
        broke = true;
        lights.add(0, -18, 42, C[5], 0.7, 0.26);
        for (const s of SPIKES) {
          for (let j = 0; j < Math.ceil(s.h / 6); j++) {
            const u = (j + 0.5) / Math.ceil(s.h / 6), x = s.x + s.lean * u, y = -s.h * u, out = Math.sign(s.x || R.range(-1, 1));
            bits.add({ kind: j % 3 === 2 ? B.SHARD : B.CHUNK, x, y, vx: out * R.range(20, 80) + R.range(-30, 20), vy: R.range(-190, -40), a: R.range(0, 6), va: R.range(-14, 14),
              s: j % 3 === 2 ? R.range(3, 6) : R.range(2.2, 4.4) * (1 - u * 0.4), c: C[4], ramp: [C[0], C[2], C[3], C[4], C[6]], bounce: 0.4, life: R.range(0.3, 0.42) });
            ps.spawn(P.FLAKE, x, y, R.range(-60, 60), R.range(-70, 10), R.range(0.25, 0.4), R.chance(0.5) ? WHITE : C[4], R.range(1, 2));
          }
          ps.spawn(P.VAPOUR, s.x + s.lean * 0.5, -s.h * 0.5, R.range(-20, 20), R.range(-20, 0), R.range(0.25, 0.38), C[5], R.range(5, 8));
        }
        for (let i = 0; i < 9; i++) ps.spawn(P.STAR, R.range(-28, 26), R.range(-44, -4), 0, 8, R.range(0.2, 0.36), C[5], R.range(1.5, 3.2), R.range(0, 0.1));
      }
      ps.update(dt);
      bits.update(dt);
      lights.update(dt);
    },
    drawBack(p) {
      if (t >= END) return;
      const k = seg(t, 0, 0.34), hold = 1 - seg(t, BREAK, 0.8);
      band(p, 0, 1, 6 + 30 * ease.outQuart(k), 0.2, 2.5, hold * (0.5 + 0.5 * (1 - k)), [C[1], C[2], C[4], C[6]], (g) => Math.sin(g) < 0, t < 0.05);
      ps.draw(p, "back");
      if (t < BREAK) for (const s of SPIKES) if (s.back) spike(p, s);
    },
    draw(p) {
      if (t >= END) return;
      const k = seg(t, 0, 0.34), hold = 1 - seg(t, BREAK, 0.8);
      band(p, 0, 1, 6 + 30 * ease.outQuart(k), 0.2, 2.5, hold * (0.5 + 0.5 * (1 - k)), [C[1], C[2], C[4], C[6]], (g) => Math.sin(g) >= 0, t < 0.05);
      for (let i = 0; i < 9; i++) {
        const a = (i / 9) * Math.PI * 2 + 0.3, L = (8 + (i % 3) * 5) * ease.outCubic(seg(t, 0.03, 0.22));
        p.line(Math.cos(a) * 5, 1 + Math.sin(a) * 1.2, Math.cos(a) * (5 + L), 1 + Math.sin(a) * (1.2 + L * 0.2), C[5], hold * 0.9);
      }
      if (t < BREAK) {
        p.glow(0, -18, 36, C[3], 0.4);
        for (const s of SPIKES) if (!s.back) spike(p, s);
        if (t > 0.2 && t < 0.42) sparkle(p, 2, -44 + Math.sin(t * 40), 5 * Math.sin(seg(t, 0.2, 0.42) * Math.PI), C[5], WHITE);
      } else if (t < BREAK + 0.08) {
        const f = 1 - (t - BREAK) / 0.08;
        sparkle(p, 0, -22, 18 * f, C[5], WHITE);
        p.ring(0, -18, 10 + 18 * (1 - f), 2, WHITE, f);
      }
      bits.draw(p);
      ps.draw(p, "front");
      lights.draw(p);
    },
  };
}
