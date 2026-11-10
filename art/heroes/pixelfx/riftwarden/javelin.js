import { Particles, Lights, P, Rng, fit, sparkle, flare, clamp, WHITE } from "../../core/index.js";

const SPEED = 560, END = 1.3;

export const meta = {
  name: "Javelin",
  desc: "A thrown spear flies flat and fast with wind streaks, punches into the target with a teal burst and stays stuck a moment, quivering, before it fades.",
  tags: ["spear", "javelin", "projectile", "thrown", "impact", "one-shot"],
  size: [360, 110],
  origin: [20, 90],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: ["#0f2a2a", "#18504a", "#22806e", "#3fb894", "#8fe6c0", "#dcfff0", "#ffffff"], shaft: ["#3a2214", "#6e4424", "#9a6a3a"], tx: 150, height: 30, th: 16, seed: 4 },
};

function spear(p, x, y, ang, C, S, a = 1) {
  const c = Math.cos(ang), s = Math.sin(ang), L = 30;
  const tx = x - c * L, ty = y - s * L;
  p.line(tx, ty + 1, x - c * 4, y - s * 4 + 1, S[0], a);
  p.line(tx, ty, x - c * 4, y - s * 4, S[2], a);
  p.poly([[x + c * 2, y + s * 2], [x - c * 4 - s * 2, y - s * 4 + c * 2], [x - c * 3, y - s * 3], [x - c * 4 + s * 2, y - s * 4 - c * 2]], C[5], a);
  p.px(x + c * 2, y + s * 2, WHITE);
  p.line(tx - c * 2 + s * 2, ty - s * 2 - c * 2, tx + c * 2 + s * 1, ty + s * 2 - c * 1, C[3], a);
}

export function create(o) {
  const C = fit(o.palette, 7), S = fit(o.shaft, 3), R = new Rng(o.seed), TX = o.tx, H = o.height, TH = o.th;
  const ps = new Particles({ rng: R, ground: () => 0, sparks: [WHITE, C[5], C[4], C[3]] }), lights = new Lights();
  const HIT = (TX - 30) / SPEED;
  const at = (t) => { const u = clamp(t / HIT); return [30 + (TX - 30) * u, -(H + (TH - H) * u) - Math.sin(u * Math.PI) * 4]; };
  const ang = Math.atan2(H - TH, TX - 8);
  let t = 0, hit = false;
  return {
    update(dt) {
      t += dt;
      if (!hit && t >= HIT) { hit = true; lights.add(TX, -TH, 44, C[4], 1, 0.35); for (let i = 0; i < 18; i++) { const a = Math.PI + R.range(-0.8, 0.8), sp = R.range(60, 200); ps.spawn(P.SPARK, TX, -TH, Math.cos(a) * sp, Math.sin(a) * sp - 30, R.range(0.2, 0.45)); } }
      ps.update(dt); lights.update(dt);
    },
    draw(p) {
      if (t < HIT) {
        const [x, y] = at(t);
        for (let k = 0; k < 3; k++) p.line(x - 36 - k * 6, y + (k - 1) * 3, x - 30 - k * 6 - 10, y + (k - 1) * 3, C[4], 0.5);
        spear(p, x, y, ang, C, S);
      } else {
        const s = t - HIT, fade = clamp(1 - (s - 0.45) / 0.3), q = Math.sin(s * 60) * clamp(1 - s / 0.3) * 0.08;
        if (fade > 0) spear(p, TX + 6, -TH, ang + q, C, S, fade);
        if (s < 0.12) flare(p, TX, -TH, 10 * (1 - s / 0.12), 1, [C[4], C[5], WHITE]);
        if (s < 0.05) sparkle(p, TX, -TH, 7, C[5], WHITE);
      }
      ps.draw(p); lights.draw(p);
    },
  };
}
