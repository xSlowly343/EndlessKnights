import { Particles, Lights, P, Rng, fit, sparkle, flare, clamp, WHITE } from "../../core/index.js";

const SPEED = 640, END = 1.3;

export const meta = {
  name: "Ghost spear",
  desc: "A spectral teal spear leaves the hand, pierces the target clean through with a burst, flies on trailing light, and a hunter's mark spins above the target.",
  tags: ["spear", "projectile", "pierce", "mark", "spectral", "one-shot"],
  size: [360, 110],
  origin: [20, 90],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: ["#0f2a2a", "#18504a", "#22806e", "#3fb894", "#8fe6c0", "#dcfff0", "#ffffff"], tx: 150, height: 26, th: 16, seed: 4 },
};

export function create(o) {
  const C = fit(o.palette, 7), R = new Rng(o.seed), TX = o.tx, H = o.height, TH = o.th;
  const ps = new Particles({ rng: R, ground: () => 0, sparks: [WHITE, C[5], C[4], C[3]] }), lights = new Lights();
  const HIT = (TX - 10) / SPEED, yAt = (x) => -(H + (TH - H) * clamp((x - 10) / (TX - 10)));
  let t = 0, hit = false;
  return {
    update(dt) {
      t += dt;
      const x = 10 + t * SPEED;
      if (x < TX + 120 && R.next() < 0.8) ps.spawn(P.MOTE, x - 10, yAt(x) + R.range(-1, 1), -60, R.range(-8, 8), R.range(0.2, 0.4), C[4]);
      if (!hit && t >= HIT) {
        hit = true;
        lights.add(TX, -TH, 50, C[4], 1, 0.4);
        for (let i = 0; i < 20; i++) { const a = R.range(-0.7, 0.7), sp = R.range(80, 220); ps.spawn(P.SPARK, TX, -TH, Math.cos(a) * sp, Math.sin(a) * sp - 30, R.range(0.2, 0.5)); }
      }
      ps.update(dt); lights.update(dt);
    },
    draw(p) {
      const x = 10 + t * SPEED;
      if (x < TX + 140) {
        const y = yAt(x), L = 26;
        p.glowLine(x - L, y, x, y, 5, C[3], 0.4);
        p.line(x - L, y, x - 4, y, (u) => (u > 0.6 ? C[5] : C[3]));
        p.poly([[x, y], [x - 5, y - 2], [x - 4, y], [x - 5, y + 2]], WHITE);
        p.line(x - L - 10, y, x - L, y, C[2], 0.6);
      }
      if (t >= HIT) {
        const s = t - HIT, k = clamp(1 - s / 0.7);
        if (s < 0.12) flare(p, TX, -TH, 10 * (1 - s / 0.12), 1, [C[4], C[5], WHITE]);
        if (k > 0) {
          const my = -TH - 22 - Math.sin(s * 8) * 1.5, r = 5, a = s * 6;
          p.poly([[TX + Math.cos(a) * r, my], [TX, my - r], [TX - Math.cos(a) * r, my], [TX, my + r]], C[4], k);
          p.ring(TX, my, r + 2, 1, C[5], k);
          sparkle(p, TX, my, 3, C[5], WHITE);
        }
      }
      ps.draw(p); lights.draw(p);
    },
  };
}
