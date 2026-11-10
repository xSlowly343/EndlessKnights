import { Particles, Lights, P, Rng, fit, sparkle, band, clamp, WHITE } from "../../core/index.js";

const SHOOT = 0.18, WRAP = 0.5, END = 1.1;

export const meta = {
  name: "Thread bind",
  desc: "Three glowing threads whip from the spindle's tip to the target, coil round it in tightening loops, pull taut with a flash and snap into drifting fibres.",
  tags: ["thread", "bind", "projectile", "debuff", "one-shot"],
  size: [300, 120],
  origin: [20, 96],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#2a0f2e", "#5a1e5e", "#9a3a8e", "#d86ab8", "#f6a8dc", "#ffe0f4", "#ffffff"],
    tx: 150,
    height: 28,
    th: 16,
    seed: 7,
  },
};

export function create(o) {
  const C = fit(o.palette, 7), R = new Rng(o.seed), TX = o.tx, H = o.height, TH = o.th;
  const ps = new Particles({ rng: R, ground: () => 0, sparks: [WHITE, C[5], C[4], C[3]] }), lights = new Lights();
  const bends = [-9, 4, 12].map((b) => b + R.range(-3, 3));
  let t = 0, flashed = false;
  return {
    update(dt) {
      t += dt;
      if (!flashed && t >= WRAP) {
        flashed = true;
        lights.add(TX, -TH, 40, C[4], 1, 0.4);
        for (let i = 0; i < 18; i++) {
          const a = R.range(0, Math.PI * 2), sp = R.range(40, 140);
          ps.spawn(P.FLAKE, TX, -TH, Math.cos(a) * sp, Math.sin(a) * sp - 20, R.range(0.4, 0.8), C[5]);
        }
      }
      ps.update(dt); lights.update(dt);
    },
    draw(p) {
      const reach = clamp(t / SHOOT), fade = clamp(1 - (t - WRAP - 0.08) / 0.2);
      if (fade > 0) {
        bends.forEach((b, k) => {
          const pts = [];
          for (let i = 0; i <= 24; i++) {
            const u = (i / 24) * reach, x = 12 + (TX - 12) * u, y = -H + (H - TH) * u + Math.sin(u * Math.PI * 3 + k * 2) * Math.sin(u * Math.PI) * b * 0.45 * (1 - clamp((t - SHOOT) / 0.3));
            pts.push([x, y]);
          }
          p.polyline(pts, k === 1 ? WHITE : C[4], fade);
          const e = pts[pts.length - 1];
          if (reach < 1) sparkle(p, e[0], e[1], 3, C[5], WHITE);
        });
        if (t > SHOOT) {
          const w = clamp((t - SHOOT) / (WRAP - SHOOT));
          for (let k = 0; k < 3; k++) {
            const r = 16 - w * 8 + k * 2;
            p.ellipse(TX, -TH + (k - 1) * 6, r, r * 0.35, C[k === 1 ? 5 : 4], 0);
            band(p, TX, -TH + (k - 1) * 6, r, 0.35, 1, fade, [C[3], C[4], C[5]]);
          }
          p.glow(TX, -TH, 16, C[4], 0.4 * fade);
        }
      }
      if (t >= WRAP && t < WRAP + 0.1) sparkle(p, TX, -TH, 9, C[5], WHITE);
      ps.draw(p); lights.draw(p);
    },
  };
}
