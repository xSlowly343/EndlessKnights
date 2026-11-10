import { Particles, Lights, P, Rng, fit, sparkle, flare, band, clamp, WHITE } from "../../core/index.js";

const DROP = 0.22, END = 1.2;
const CROWN = [
  "k..k..k..k",
  "kYkkYkkYkk",
  "kYYkYYkYYk",
  "kYYYYYYYYk",
  "kYRYYBYYRk",
  "kYYYYYYYYk",
  "kyyyyyyyyk",
  "kkkkkkkkkk",
];

export const meta = {
  name: "Crown seal",
  desc: "A royal gesture: a golden crown of light drops onto the target and stamps down like a seal with a flash, a ring of gold over the floor and a burst of glints; a ring of command flares at the caster's feet.",
  tags: ["holy", "royal", "crown", "seal", "impact", "one-shot"],
  size: [340, 180],
  origin: [30, 150],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: ["#2a1608", "#6b3c10", "#b8701a", "#f0b42e", "#ffe27a", "#fff6cf", "#ffffff"], tx: 150, th: 16, seed: 5 },
};

export function create(o) {
  const C = fit(o.palette, 7), R = new Rng(o.seed), TX = o.tx, TH = o.th;
  const ps = new Particles({ rng: R, ground: () => 0, sparks: [WHITE, C[5], C[4], C[3]] }), lights = new Lights();
  const pal = { k: C[1], Y: C[4], y: C[3], R: 0xe03040, B: 0x4060e0 };
  lights.add(0, -10, 40, C[4], 0.7, 0.4);
  let t = 0, hit = false;
  return {
    update(dt) {
      t += dt;
      if (!hit && t >= DROP) {
        hit = true; lights.add(TX, -TH, 60, C[4], 1, 0.45);
        for (let i = 0; i < 24; i++) { const a = R.range(0, Math.PI * 2), sp = R.range(60, 200); ps.spawn(P.SPARK, TX, -TH, Math.cos(a) * sp, Math.sin(a) * sp - 40, R.range(0.2, 0.5)); }
        for (let i = 0; i < 8; i++) ps.spawn(P.STAR, TX + R.range(-14, 14), -TH + R.range(-14, 8), 0, R.range(-20, -6), R.range(0.4, 0.8), C[5]);
      }
      ps.update(dt); lights.update(dt);
    },
    draw(p) {
      if (t < 0.5) band(p, 0, 0, 6 + t * 50, 0.18, 1.5, clamp(1 - t / 0.5), [C[3], C[4], C[5]]);
      const u = clamp(t / DROP), y = -TH - 18 - (1 - u * u) * 60, fade = clamp(1 - (t - DROP - 0.25) / 0.25);
      if (fade > 0) {
        p.glow(TX, y + 4, 18, C[4], 0.45 * fade);
        p.sprite(CROWN, pal, TX - 10, y - 8, { scale: 2, a: fade });
        if (u < 1) for (let k = 1; k < 4; k++) p.line(TX - 8 + k * 4, y - 10 - k * 6, TX - 8 + k * 4, y - 16 - k * 6, C[5], 0.6);
      }
      if (t >= DROP) {
        const s = t - DROP, k = clamp(1 - s / 0.4);
        if (s < 0.12) flare(p, TX, -TH, 14, 1, [C[3], C[5], WHITE]);
        band(p, TX, 0, 6 + s * 80, 0.18, 1.6, k, [C[3], C[4], C[5], C[6]]);
        p.ring(TX, -TH, 4 + s * 60, 1, C[5], k);
      }
      ps.draw(p); lights.draw(p);
    },
  };
}
