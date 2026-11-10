import { Particles, Lights, P, Rng, fit, sparkle, flare, clamp, WHITE } from "../../core/index.js";

const FLY = 0.24, END = 1.0;

export const meta = {
  name: "Slash wave",
  desc: "A swing tears a crescent off the blade and hurls it forward; it thins as it flies, leaves a dotted wake and bursts on the target in a flare and sparks.",
  tags: ["melee", "slash", "projectile", "wave", "directional", "one-shot"],
  size: [260, 110],
  origin: [20, 90],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#212f6b", "#2c58aa", "#3a8bd8", "#62c8f4", "#b8c9e8", "#e8f1ff", "#ffffff"],
    reach: 160,
    height: 26,
    radius: 21,
    seed: 2,
  },
};

export function create(o) {
  const C = fit(o.palette, 7), R = new Rng(o.seed), X1 = o.reach, H = o.height, RAD = o.radius;
  const ps = new Particles({ rng: R, ground: () => 0, sparks: [WHITE, C[5], C[4], C[3]] }), lights = new Lights();
  const x0 = 12, pos = (t) => x0 + (X1 - x0) * clamp(t / FLY) ** 0.85;
  let t = 0, hit = false;
  return {
    update(dt) {
      t += dt;
      if (t < FLY && R.next() < 0.7) ps.spawn(P.MOTE, pos(t) - 4, -H + R.range(-RAD, RAD) * 0.8, -40, R.range(-10, 10), R.range(0.15, 0.3), C[4]);
      if (!hit && t >= FLY) {
        hit = true;
        lights.add(X1, -H, 44, C[4], 1, 0.4);
        for (let i = 0; i < 24; i++) {
          const a = R.range(-Math.PI * 0.75, Math.PI * 0.75), sp = R.range(70, 230);
          ps.spawn(P.SPARK, X1, -H, Math.cos(a) * sp, Math.sin(a) * sp - 40, R.range(0.2, 0.5));
        }
        for (let i = 0; i < 3; i++) ps.spawn(P.VAPOUR, X1 + R.range(-6, 6), -H + R.range(-6, 6), R.range(10, 30), R.range(-14, 4), R.range(0.4, 0.7), C[2]);
      }
      ps.update(dt); lights.update(dt);
    },
    draw(p) {
      if (t < FLY) {
        const u = t / FLY, x = pos(t), r = RAD * (1 - 0.25 * u), sw = 0.55 + 0.15 * u;
        const a0 = -Math.PI * sw, a1 = Math.PI * sw;
        p.glow(x - 4, -H, r + 8, C[3], 0.35);
        p.arc(x - r, -H, r, (v) => 1 + 6 * Math.sin(Math.PI * v), a0, a1, (v) => (v > 0.4 && v < 0.6 ? WHITE : Math.abs(v - 0.5) < 0.3 ? C[5] : C[4]), 1);
        p.arc(x - r - 3, -H, r - 1, 1, a0 * 0.8, a1 * 0.8, C[3], 0.8);
        for (let k = 1; k < 4; k++) p.arc(x - r - k * 7, -H, r - k, 1, a0 * 0.6, a1 * 0.6, C[2 + (k === 1)], 0.6 - k * 0.15);
        sparkle(p, x, -H, 3, C[5], WHITE);
      } else {
        const s = t - FLY, f = clamp(1 - s / 0.25);
        if (f > 0) {
          flare(p, X1, -H, 12 * f, 1, [C[4], C[5], WHITE]);
          sparkle(p, X1, -H, 8 * f, C[5], WHITE);
          p.glow(X1, -H, 20, C[4], 0.5 * f);
          p.ring(X1, -H, 4 + s * 60, C[5], 0.7 * f);
        }
      }
      ps.draw(p); lights.draw(p);
    },
  };
}
