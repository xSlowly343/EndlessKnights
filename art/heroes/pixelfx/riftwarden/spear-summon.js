import { Particles, P, Rng, fit, sparkle, clamp, WHITE } from "../../core/index.js";

const D = 0.35;

export const meta = {
  name: "Spear summon",
  desc: "Teal motes rush into the open hand and a new spear snaps into being with a glint along its length.",
  tags: ["summon", "spear", "spectral", "hand", "one-shot"],
  size: [70, 50],
  origin: [35, 25],
  duration: D,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: ["#0f2a2a", "#18504a", "#22806e", "#3fb894", "#8fe6c0", "#dcfff0", "#ffffff"], seed: 2 },
};

export function create(o) {
  const C = fit(o.palette, 7), R = new Rng(o.seed), ps = new Particles({ rng: R, ground: () => 40 });
  for (let i = 0; i < 16; i++) { const a = R.range(0, Math.PI * 2), d = R.range(10, 18); ps.spawn(P.STREAK, Math.cos(a) * d, Math.sin(a) * d, -Math.cos(a) * 80, -Math.sin(a) * 80, R.range(0.12, 0.2), C[4], 1, R.range(0, 0.08)); }
  let t = 0;
  return {
    update(dt) { t += dt; ps.update(dt); },
    draw(p) {
      const k = clamp(t / 0.2);
      p.glow(0, 0, 10 * k + 2, C[4], 0.5 * (1 - clamp((t - 0.2) / 0.15)));
      if (t > 0.18 && t < 0.28) { sparkle(p, 0, 0, 6, C[5], WHITE); p.line(-14, 0, 14, 0, C[5], 0.8); }
      ps.draw(p);
    },
  };
}
