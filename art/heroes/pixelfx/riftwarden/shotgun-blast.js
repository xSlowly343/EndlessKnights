import { Particles, Lights, P, Rng, fit, sparkle, flare, clamp, WHITE } from "../../core/index.js";

const END = 1.4;

export const meta = {
  name: "Shotgun blast",
  desc: "A wide forward fan of flame from the muzzle, a white-hot core, pellets flung ahead and a short cloud of smoke rolling forward; nothing flies back at the shooter.",
  tags: ["gun", "shotgun", "muzzle", "flash", "smoke", "directional", "one-shot"],
  size: [170, 90],
  origin: [20, 40],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff"],
    seed: 5,
  },
};

export function create(o) {
  const F = fit(o.palette, 9), R = new Rng(o.seed);
  const ps = new Particles({ rng: R, ground: () => 40, sparks: [F[8], F[7], F[5], F[3]] }), lights = new Lights();
  lights.add(10, 0, 46, F[5], 1, 0.25);
  for (let i = 0; i < 26; i++) {
    const a = R.range(-0.32, 0.32), sp = R.range(260, 520);
    ps.spawn(P.SPARK, 4, R.range(-1, 1), Math.cos(a) * sp, Math.sin(a) * sp, R.range(0.12, 0.3));
  }
  for (let i = 0; i < 12; i++) {
    const a = R.range(-0.4, 0.4), sp = R.range(60, 140);
    ps.spawn(P.FIRE, 6, R.range(-2, 2), Math.cos(a) * sp, Math.sin(a) * sp - 10, R.range(0.15, 0.35), 0, 1);
  }
  for (let i = 0; i < 9; i++) {
    const a = R.range(-0.35, 0.2), sp = R.range(30, 90);
    ps.spawn(P.SMOKE, 10 + R.range(0, 10), R.range(-3, 3), Math.cos(a) * sp, Math.sin(a) * sp - 12, R.range(0.7, 1.2), 0, R.range(1, 1.6), R.range(0.02, 0.1));
  }
  let t = 0;
  return {
    update(dt) { t += dt; ps.update(dt); lights.update(dt); },
    draw(p) {
      const k = clamp(1 - t / 0.09);
      if (k > 0) {
        for (let i = 0; i < 7; i++) {
          const a = -0.42 + i * 0.14, L = (16 + (i % 2 ? 8 : 18)) * (0.6 + 0.4 * k);
          p.beam(2, 0, 2 + Math.cos(a) * L, Math.sin(a) * L, 3 * k + 1, i % 2 ? F[5] : F[6]);
        }
        p.circle(4, 0, 5 * k + 1, F[8]);
        flare(p, 4, 0, 10 * k, 1, [F[5], F[7], WHITE]);
        p.glow(10, 0, 30, F[5], 0.6 * k);
      }
      if (t < 0.05) sparkle(p, 3, 0, 6, F[7], WHITE);
      ps.draw(p); lights.draw(p);
    },
  };
}
