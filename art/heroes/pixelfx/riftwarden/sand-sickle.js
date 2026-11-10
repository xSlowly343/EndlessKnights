import { Particles, Lights, P, Rng, fit, sparkle, flare, clamp, WHITE } from "../../core/index.js";

const OUT = 0.26, BACK = 0.58, END = 1.2;

export const meta = {
  name: "Sand sickle",
  desc: "A golden sickle of packed sand spins out to the target, bites it, curves back past it again on the return and dissolves into drifting sand at the thrower's hand.",
  tags: ["sand", "boomerang", "projectile", "sickle", "spin", "one-shot"],
  size: [340, 120],
  origin: [20, 96],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: ["#2a1608", "#6b3c10", "#b8701a", "#f0b42e", "#ffe27a", "#fff6cf", "#ffffff"], tx: 150, height: 22, seed: 6 },
};

export function create(o) {
  const C = fit(o.palette, 7), R = new Rng(o.seed), TX = o.tx, H = o.height;
  const ps = new Particles({ rng: R, ground: () => 0, sparks: [WHITE, C[5], C[4], C[3]] }), lights = new Lights();
  const pos = (t) => {
    if (t < OUT) { const u = t / OUT; return [10 + (TX - 10) * (1 - (1 - u) * (1 - u)), -H - Math.sin(u * Math.PI) * 8]; }
    const u = clamp((t - OUT) / (BACK - OUT)); return [TX - (TX - 10) * u * u, -H + Math.sin(u * Math.PI) * 10];
  };
  let t = 0, h1 = false;
  return {
    update(dt) {
      t += dt;
      if (t < BACK) { const [x, y] = pos(t); if (R.next() < 0.9) ps.spawn(P.DUST, x, y, R.range(-20, 20), R.range(-10, 10), R.range(0.3, 0.6), C[3]); }
      if (!h1 && t >= OUT) { h1 = true; lights.add(TX, -H, 40, C[4], 1, 0.3); for (let i = 0; i < 14; i++) ps.spawn(P.SPARK, TX, -H, R.range(-140, 140), R.range(-140, 60), R.range(0.15, 0.35)); }
      if (Math.abs(t - BACK) < 0.02) for (let i = 0; i < 10; i++) ps.spawn(P.DUST, 10, -H, R.range(-30, 30), R.range(-30, 10), R.range(0.4, 0.8), C[4]);
      ps.update(dt); lights.update(dt);
    },
    draw(p) {
      if (t < BACK) {
        const [x, y] = pos(t), a = t * 26, r = 9;
        p.arc(x, y, r, (v) => 1 + 3 * Math.sin(Math.PI * v), a, a + Math.PI * 1.25, (v) => (v > 0.4 && v < 0.65 ? WHITE : C[4]), 1);
        p.arc(x, y, r - 3, 1, a, a + Math.PI * 1.1, C[2], 0.8);
        p.glow(x, y, 14, C[3], 0.35);
      }
      if (t >= OUT && t < OUT + 0.1) flare(p, TX, -H, 9, 1, [C[3], C[5], WHITE]);
      ps.draw(p); lights.draw(p);
    },
  };
}
