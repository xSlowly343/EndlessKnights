import { Particles, Lights, P, Rng, fit, sparkle, flare, band, bolt, forks, clamp, WHITE } from "../../core/index.js";

const CHARGE = 0.14, STRIKE = CHARGE, FLICKER = 0.26, END = 1.3;

export const meta = {
  name: "Storm bolt",
  desc: "Sparks crawl into the staff's crystal, then a forked bolt cracks down from it onto the target, restrikes twice, splashes arcs along the floor and leaves a smoking scorch.",
  tags: ["lightning", "spell", "staff", "target", "impact", "one-shot"],
  size: [250, 160],
  origin: [36, 46],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#0d1030", "#1b2470", "#2f47b8", "#4f7ff0", "#86b8ff", "#c4e2ff", "#ffffff"],
    tx: 92,
    ty: 89,
    seed: 4,
  },
};

export function create(o) {
  const C = fit(o.palette, 7), R = new Rng(o.seed), TX = o.tx, TY = o.ty;
  const ps = new Particles({ rng: R, ground: () => TY, sparks: [WHITE, C[5], C[4], C[3]] }), lights = new Lights();
  const paths = [0, 1, 2].map(() => {
    const main = bolt(R, 0, 0, TX, TY, { jag: 0.2, depth: 5 });
    return { main, br: forks(R, main, { count: 4, len: 22, jag: 0.35, spread: 1.0 }) };
  });
  const arcs = [-1, 1, -1, 1].map((d, i) => bolt(R, TX, TY, TX + d * (10 + i * 7), TY + 1 + (i % 2), { jag: 0.4, depth: 3 }));
  let t = 0, fired = false;
  return {
    update(dt) {
      t += dt;
      if (t < CHARGE && R.next() < 0.6) {
        const a = R.range(0, Math.PI * 2), d = R.range(8, 14);
        ps.spawn(P.STREAK, Math.cos(a) * d, Math.sin(a) * d, -Math.cos(a) * 90, -Math.sin(a) * 90, 0.12, C[5]);
      }
      if (!fired && t >= STRIKE) {
        fired = true;
        lights.add(TX, TY - 6, 60, C[4], 1, 0.5);
        lights.add(0, 0, 26, C[5], 0.9, 0.25);
        for (let i = 0; i < 26; i++) {
          const a = -Math.PI * R.range(0.05, 0.95), sp = R.range(90, 260);
          ps.spawn(P.SPARK, TX, TY - 1, Math.cos(a) * sp, Math.sin(a) * sp, R.range(0.25, 0.6));
        }
        for (let i = 0; i < 6; i++) ps.spawn(P.VAPOUR, TX + R.range(-8, 8), TY - 2, R.range(-12, 12), R.range(-30, -14), R.range(0.6, 1.0), C[1]);
      }
      ps.update(dt); lights.update(dt);
    },
    draw(p) {
      const c = clamp(t / CHARGE);
      if (t < STRIKE + 0.05) { p.glow(0, 0, 4 + 10 * c, C[4], 0.5 * c); sparkle(p, 0, 0, 2 + 4 * c, C[5], WHITE); }
      const s = t - STRIKE;
      if (s >= 0 && s < FLICKER) {
        const k = Math.min(2, Math.floor(s / (FLICKER / 3))), path = paths[k], on = (s % (FLICKER / 3)) < (FLICKER / 3) * 0.7;
        if (on) {
          for (const b of path.br) { p.polyline(b, C[4]); }
          for (let i = 1; i < path.main.length; i++) {
            const [ax, ay] = path.main[i - 1], [bx, by] = path.main[i];
            p.glowLine(ax, ay, bx, by, 6, C[3], 0.35);
            p.beam(ax, ay, bx, by, 3, C[5]);
          }
          p.polyline(path.main, WHITE);
          flare(p, TX, TY - 2, 10 - k * 3, 0.9, [C[4], C[5], WHITE]);
          sparkle(p, 0, 0, 6, C[5], WHITE);
        }
        for (const a of arcs) if ((Math.floor(s * 40) + arcs.indexOf(a)) % 2 === 0) p.polyline(a, C[5]);
      }
      if (s >= 0) {
        const f = clamp(1 - s / 0.9);
        band(p, TX, TY, 6 + s * 40, 0.18, 1.2, f * 0.8, [C[3], C[4], C[5]]);
        if (s < 0.9) p.glow(TX, TY, 18, C[3], 0.35 * f);
        const sc = clamp(1 - s / (END - STRIKE));
        if (sc > 0) p.ellipse(TX, TY + 0.5, 7 * sc + 2, 1.6, C[0], 0.8 * sc);
      }
      ps.draw(p); lights.draw(p);
    },
  };
}
