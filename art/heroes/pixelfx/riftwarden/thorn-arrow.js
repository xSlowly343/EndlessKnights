import { Particles, Lights, P, Rng, fit, sparkle, clamp, WHITE } from "../../core/index.js";

const FLY = 0.2, END = 1.8;

export const meta = {
  name: "Thorn arrow",
  desc: "A living vine arrow with a thorn tip flies true; on impact vines burst from the ground and coil up round the target, thorny and leafy, then wither away.",
  tags: ["nature", "arrow", "projectile", "vine", "root", "one-shot"],
  size: [320, 120],
  origin: [20, 96],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: ["#0e2010", "#1e4420", "#2e6e2a", "#4ea040", "#8cd060", "#d8f4a0", "#ffffff"], tx: 150, height: 24, th: 16, seed: 5 },
};

export function create(o) {
  const C = fit(o.palette, 7), R = new Rng(o.seed), TX = o.tx, H = o.height, TH = o.th;
  const ps = new Particles({ rng: R, ground: () => 0 }), lights = new Lights();
  const vines = [-1, 1, -1, 1].map((s, i) => ({ s, off: (i - 1.5) * 4, ph: R.range(0, 6.28) }));
  let t = 0, hit = false;
  return {
    update(dt) {
      t += dt;
      if (!hit && t >= FLY) { hit = true; lights.add(TX, -TH, 34, C[4], 0.8, 0.4); for (let i = 0; i < 12; i++) ps.spawn(P.FLAKE, TX + R.range(-8, 8), -TH + R.range(-8, 4), R.range(-50, 50), R.range(-60, 0), R.range(0.6, 1.0), i % 2 ? C[4] : C[3], 2); }
      ps.update(dt); lights.update(dt);
    },
    draw(p) {
      if (t < FLY) {
        const u = t / FLY, x = 10 + (TX - 10) * u, y = -(H + (TH - H) * u);
        p.line(x - 18, y, x - 3, y, C[3]);
        p.line(x - 18, y - 1, x - 14, y - 2, C[4]); p.line(x - 18, y + 1, x - 14, y + 2, C[4]);
        p.poly([[x, y], [x - 4, y - 2], [x - 4, y + 2]], C[5]);
        p.px(x, y, WHITE);
      }
      if (t >= FLY) {
        const s = t - FLY, grow = clamp(s / 0.25), wither = clamp((s - 1.0) / 0.5), k = grow * (1 - wither);
        for (const v of vines) {
          const pts = [];
          for (let i = 0; i <= 20; i++) {
            const u = (i / 20) * k, y = -u * (TH + 14), x = TX + v.off + Math.sin(u * 10 + v.ph) * 7 * v.s * (1 - u * 0.3);
            pts.push([x, y]);
          }
          p.polyline(pts, C[3], 1 - wither);
          p.polyline(pts.map(([x, y]) => [x + 1, y]), C[2], 1 - wither);
          pts.forEach(([x, y], i) => { if (i % 5 === 3) { p.px(x + v.s * 2, y - 1, C[5]); p.px(x - v.s, y, C[4]); } });
        }
        if (s < 0.06) sparkle(p, TX, -TH, 7, C[5], WHITE);
      }
      ps.draw(p); lights.draw(p);
    },
  };
}
