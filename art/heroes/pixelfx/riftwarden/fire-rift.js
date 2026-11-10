import { Particles, Lights, P, PF, Rng, fit, flare, clamp, bolt, WHITE } from "../../core/index.js";

const RUN = 0.24, END = 1.6;

export const meta = {
  name: "Fire rift",
  desc: "The axe splits the floor: a glowing crack races to the target spitting embers, and a pillar of flame bursts up under it, then the crack cools to a dark scar.",
  tags: ["fire", "ground", "crack", "pillar", "melee", "one-shot"],
  size: [320, 150],
  origin: [30, 120],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: ["#2a0c10", "#5c1414", "#a3241a", "#e0481c", "#ff8a2a", "#ffc23d", "#fff0a0"], tx: 150, seed: 3 },
};

export function create(o) {
  const C = fit(o.palette, 7), R = new Rng(o.seed), TX = o.tx;
  const ps = new Particles({ rng: R, ground: () => 0, sparks: [WHITE, C[6], C[5], C[3]] }), lights = new Lights();
  const crack = bolt(R, 8, 1, TX, 1, { jag: 0.05, depth: 6 });
  let t = 0, boom = false;
  return {
    update(dt) {
      t += dt;
      if (t < RUN) {
        const x = 8 + (TX - 8) * (t / RUN);
        for (let i = 0; i < 2; i++) ps.spawn(P.EMBER, x + R.range(-3, 3), -1, R.range(-20, 20), R.range(-60, -20), R.range(0.3, 0.6), C[5]);
        if (R.next() < 0.5) ps.spawn(P.FIRE, x, -2, R.range(-10, 10), R.range(-40, -20), R.range(0.2, 0.35), 0, 0.7);
      }
      if (!boom && t >= RUN) {
        boom = true;
        lights.add(TX, -20, 60, C[4], 1, 0.6);
        for (let i = 0; i < 22; i++) ps.spawn(P.SPARK, TX, -2, R.range(-120, 120), R.range(-260, -90), R.range(0.3, 0.7));
      }
      if (t >= RUN && t < RUN + 0.55) {
        for (let i = 0; i < 4; i++) ps.spawn(P.FIRE, TX + R.range(-7, 7), -2, R.range(-14, 14), R.range(-180, -110), R.range(0.25, 0.5), 0, R.range(1, 1.8), 0, PF.SMOKES);
      }
      ps.update(dt); lights.update(dt);
    },
    draw(p) {
      const reach = clamp(t / RUN), cool = clamp((t - RUN - 0.3) / (END - RUN - 0.4));
      const n = Math.max(2, Math.round(crack.length * reach));
      const pts = crack.slice(0, n);
      const hot = cool < 0.5 ? (cool < 0.2 ? C[6] : C[4]) : C[1], gone = clamp((END - t) / 0.35);
      p.polyline(pts.map(([x, y]) => [x, y + 1]), C[0], (1 - cool * 0.5) * gone);
      p.polyline(pts, hot, (1 - cool * 0.7) * gone);
      if (reach < 1) { const [hx, hy] = pts[pts.length - 1]; flare(p, hx, hy - 1, 5, 0.8, [C[4], C[6], WHITE]); }
      if (cool < 0.6) p.glowLine(8, 0, pts[pts.length - 1][0], 0, 5, C[3], 0.3 * (1 - cool));
      if (t >= RUN && t < RUN + 0.6) {
        const k = clamp(1 - (t - RUN) / 0.6);
        p.beam(TX, 0, TX, -46 * k - 6, 10 * k + 2, (u, v) => (v < 0.3 ? C[6] : C[4]), 0.85);
        p.glow(TX, -20, 30, C[4], 0.6 * k);
      }
      ps.draw(p); lights.draw(p);
    },
  };
}
