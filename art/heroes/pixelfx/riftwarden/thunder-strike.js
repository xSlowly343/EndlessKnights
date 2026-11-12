import { Particles, Lights, P, Rng, rng, fit, clamp, seg, ease, bolt, forks, band, hex, sparkle, WHITE } from "../../core/index.js";
import { strike } from "./thunder-orb.js";

const END = 0.86, TOP = -36, HITS = [0.07, 0.21, 0.33];

export const meta = {
  name: "Thunder strike",
  desc: "A thunderhead boils up and answers three times: a forked white trunk, a thinner re-strike, a flicker; rings run along the floor, arcs crawl, sparks hop away.",
  tags: ["lightning", "impact", "ground", "sky", "one-shot"],
  size: [108, 74],
  origin: [54, 62],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#0a0820", "#2a2470", "#5a54d8", "#8c9cff", "#c4d8ff", "#f0f6ff", "#ffffff"],
    cloud: ["#12101e", "#262238", "#3e3a58", "#6a6690"],
    spark: "#fff27a",
    seed: 132,
  },
};

const PUFFS = [[-19, 2, 7], [-9, -2, 9], [3, -3, 10], [14, -1, 8], [21, 3, 6], [-3, 4, 8], [8, 4, 7], [-13, 5, 6]];

export function create(o) {
  const C = fit(o.palette, 7), K = o.cloud.map(hex), Y = hex(o.spark), R = new Rng(o.seed);
  const ps = new Particles({ rng: R, ground: () => 0, sparks: [WHITE, Y, C[3]] }), lights = new Lights();
  let t = 0, done = 0;
  const lit = () => Math.max(...HITS.map((h) => (t >= h ? clamp(1 - (t - h) / 0.12) : 0)));
  return {
    update(dt) {
      t += dt;
      while (done < HITS.length && t >= HITS[done]) {
        const power = 1 - done * 0.3, x = [0, 5, -4][done];
        lights.add(x, -8, 38 * power, C[4], 0.9 * power, 0.22);
        lights.add(x * 0.5, TOP + 6, 24, C[3], 0.6 * power, 0.2);
        for (let i = 0; i < 16 * power; i++) {
          const a = -Math.PI / 2 + R.range(-1.4, 1.2) - 0.15, sp = R.range(80, 200) * power;
          ps.spawn(P.SPARK, x, -2, Math.cos(a) * sp, Math.sin(a) * sp, R.range(0.2, 0.5));
        }
        for (let i = 0; i < 5; i++) ps.spawn(P.STAR, x + R.range(-22, 20), R.range(-26, -4), 0, 0, R.range(0.12, 0.3), i % 2 ? Y : C[5], R.range(1.5, 3), R.range(0, 0.1));
        done++;
      }
      ps.update(dt);
      lights.update(dt);
    },
    drawBack(p) {
      if (t >= END) return;
      const swell = ease.outBack(seg(t, 0, 0.1), 1.5), thin = 1 - seg(t, 0.46, 0.76), flash = lit();
      for (const [x, y, r] of PUFFS) {
        const rr = r * swell * (0.75 + 0.25 * thin), px = x * (0.5 + 0.5 * swell) + Math.sin(t * 5 + x) * 1.2, py = TOP + y - (1 - thin) * 5;
        p.disc(px, py, rr + 1.5, K[0], 0.7 * thin);
        p.disc(px, py, rr, flash > 0.5 ? K[3] : K[1], 0.85 * thin);
        p.disc(px + 1, py + rr * 0.35, rr * 0.62, flash > 0.3 ? C[4] : K[2], (0.5 + 0.5 * flash) * thin);
      }
      if (flash > 0) p.glow(0, TOP + 4, 28, C[3], 0.6 * flash);
      for (let i = 0; i < HITS.length; i++) {
        const k = seg(t, HITS[i], HITS[i] + 0.26);
        if (t >= HITS[i] && k < 1) band(p, 0, 1, 5 + (26 - i * 5) * ease.outQuart(k), 0.2, 2.5, 1 - k, [C[1], C[2], C[4], C[6]], (g) => Math.sin(g) < 0, k < 0.2);
      }
    },
    draw(p) {
      if (t >= END) return;
      for (let i = 0; i < HITS.length; i++) {
        const s = t - HITS[i], k = seg(t, HITS[i], HITS[i] + 0.26);
        if (s < 0) continue;
        if (k < 1) band(p, 0, 1, 5 + (26 - i * 5) * ease.outQuart(k), 0.2, 2.5, 1 - k, [C[1], C[2], C[4], C[6]], (g) => Math.sin(g) >= 0, k < 0.2);
        if (s >= 0.12) continue;
        const Rb = rng(o.seed, i, Math.floor(s * 30) > 1 ? 1 : 0), x = [0, 5, -4][i], age = s / 0.12;
        const path = bolt(Rb, x * 0.4 + [-4, 6, 0][i], TOP + 6, x, 0, { jag: 0.2, depth: 5 });
        p.glowLine(x * 0.4, TOP + 6, x, 0, 9 - i * 2, C[3], 0.7 * (1 - age));
        strike(p, path, C, (i === 0 ? 3.4 : i === 1 ? 2.2 : 1.4) * (1 - age * 0.6), 1 - age * 0.4);
        for (const f of forks(Rb, path, { count: 4 - i, len: 20 - i * 5, spread: 0.8 })) p.polyline(f, age < 0.4 ? WHITE : C[4], 1 - age * 0.7);
        if (s < 0.07) {
          sparkle(p, x, -1, (15 - i * 4) * (1 - s / 0.07), C[5], WHITE);
          p.ellipse(x, 0, (13 - i * 3) * (1 - s / 0.14), 3, WHITE, 1);
        }
      }
      const crawl = seg(t, HITS[0], 0.62);
      if (crawl > 0 && crawl < 1) {
        const Rc = rng(o.seed, 77, Math.floor(t * 30));
        for (let i = 0; i < 4; i++) {
          const dir = i % 2 ? 1 : -1, x0 = dir * (4 + crawl * 16 + i * 2), L = Rc.range(5, 10);
          const path = bolt(Rc, x0, 0, x0 + dir * L, Rc.range(-7, -1), { jag: 0.45, depth: 2 });
          p.polyline(path, i < 2 ? C[5] : C[4], 1 - crawl * 0.6);
          p.dot(x0 + dir * L, -3, Y, 1 - crawl);
        }
        p.ellipse(0, 1, 16, 2.2, C[2], 0.5 * (1 - crawl));
      }
      ps.draw(p, "front");
      lights.draw(p);
    },
  };
}
