import { Particles, Lights, P, Rng, fit, sparkle, flare, clamp, ease, WHITE } from "../../core/index.js";

const END = 0.6;

export const meta = {
  name: "Void burst",
  desc: "A flying crescent breaks on its target: a white flare, a vertical gash that splits open, a ring, sparks thrown back and forward, and vapour that thins away.",
  tags: ["melee", "slash", "impact", "burst", "directional", "one-shot"],
  size: [112, 96],
  origin: [56, 48],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#06281c", "#0c6a48", "#20ca7b", "#6ffd92", "#d0f7d6", "#f2fef4", "#ffffff"],
    radius: 15,
    floor: 16,
    seed: 5,
  },
};

export function create(o) {
  const C = fit(o.palette, 7), R = new Rng(o.seed), RAD = o.radius;
  const ps = new Particles({ rng: R, ground: () => o.floor, sparks: [C[5], C[4], C[3], C[2]], haze: C[2] }), lights = new Lights();
  lights.add(0, 0, 30, C[3], 0.45, 0.3);
  for (let i = 0; i < 22; i++) {
    const back = i % 3 === 0, a = back ? Math.PI + R.range(-0.9, 0.9) : R.range(-1.1, 1.1), sp = R.range(60, back ? 150 : 240);
    ps.spawn(P.SPARK, R.range(-2, 2), R.range(-RAD, RAD) * 0.7, Math.cos(a) * sp, Math.sin(a) * sp - 30, R.range(0.18, 0.5));
  }
  for (let i = 0; i < 7; i++) ps.spawn(P.MOTE, R.range(-4, 6), R.range(-RAD, RAD), R.range(20, 90), R.range(-40, 20), R.range(0.25, 0.55), C[4]);
  for (let i = 0; i < 4; i++) ps.spawn(P.VAPOUR, R.range(-5, 7), R.range(-8, 8), R.range(8, 30), R.range(-16, 2), R.range(0.35, 0.6), C[2], R.range(4, 7));
  for (let i = 0; i < 5; i++) ps.spawn(P.STAR, R.range(-16, 18), R.range(-18, 14), R.range(-6, 6), R.range(-14, -4), R.range(0.25, 0.4), C[5], 1, R.range(0.05, 0.18));
  let t = 0;
  return {
    update(dt) { t += dt; ps.update(dt); lights.update(dt); },
    draw(p) {
      const f = clamp(1 - t / 0.22), g = clamp(1 - t / 0.34);
      if (g > 0) {
        const open = ease.outQuart(clamp(t / 0.2)), h = RAD * (1.15 + 0.35 * open), w = 5 * g;
        p.glow(0, 0, 18, C[2], 0.35 * g);
        p.arc(-RAD + 2 + open * 5, 0, RAD + open * 3, (v) => 1 + w * Math.sin(Math.PI * v), -1.25, 1.25, (v) => (Math.abs(v - 0.5) < 0.2 * g ? WHITE : Math.abs(v - 0.5) < 0.3 ? C[4] : C[3]), 0.35 + 0.65 * g);
        p.line(1, -h, 1, h, (v) => (Math.abs(v - 0.5) < 0.3 * g ? WHITE : C[4]), (v) => g * Math.sin(Math.PI * v));
        p.glowLine(1, -h, 1, h, C[3], 0.5 * g);
      }
      if (f > 0) {
        flare(p, 0, 0, 15 * f, 0.6, [C[2], C[3], C[5], WHITE]);
        sparkle(p, 0, 0, 9 * f, C[5], WHITE);
        p.ring(0, 0, 3 + ease.outQuart(1 - f) * 20, f > 0.5 ? C[5] : C[3], 0.85 * f);
      }
      ps.draw(p); lights.draw(p);
    },
  };
}
