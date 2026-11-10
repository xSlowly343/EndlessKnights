import { Particles, Lights, P, Rng, fit, sparkle, flare, band, clamp, WHITE } from "../../core/index.js";

const FLY = 0.26, END = 1.1;

export const meta = {
  name: "Banner wave",
  desc: "The banner sweeps and a ribbon of golden light unfurls off it, rippling across to the target where it breaks into a crest of light; a ring of valour flares at the bearer's feet.",
  tags: ["holy", "banner", "wave", "aura", "valour", "one-shot"],
  size: [340, 130],
  origin: [30, 104],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: ["#2a1608", "#6b3c10", "#b8701a", "#f0b42e", "#ffe27a", "#fff6cf", "#ffffff"], tx: 150, height: 26, th: 16, seed: 5 },
};

export function create(o) {
  const C = fit(o.palette, 7), R = new Rng(o.seed), TX = o.tx, H = o.height, TH = o.th;
  const ps = new Particles({ rng: R, ground: () => 0, sparks: [WHITE, C[5], C[4], C[3]] }), lights = new Lights();
  lights.add(0, -10, 40, C[4], 0.7, 0.4);
  let t = 0, hit = false;
  return {
    update(dt) {
      t += dt;
      if (t < FLY && R.next() < 0.8) { const u = t / FLY, x = 10 + (TX - 10) * u; ps.spawn(P.STAR, x - 8, -(H + (TH - H) * u) + R.range(-6, 6), -20, R.range(-10, 10), R.range(0.3, 0.6), C[5]); }
      if (!hit && t >= FLY) { hit = true; lights.add(TX, -TH, 50, C[4], 1, 0.4); for (let i = 0; i < 16; i++) ps.spawn(P.SPARK, TX, -TH, R.range(-150, 150), R.range(-170, 40), R.range(0.2, 0.45)); }
      ps.update(dt); lights.update(dt);
    },
    draw(p) {
      if (t < 0.5) band(p, 0, 0, 6 + t * 50, 0.18, 1.5, clamp(1 - t / 0.5), [C[3], C[4], C[5]]);
      if (t < FLY) {
        const u = t / FLY, x = 10 + (TX - 10) * u, y = -(H + (TH - H) * u);
        const pts = [], top = [], bot = [];
        for (let i = 0; i <= 18; i++) { const v = i / 18, xx = x - v * 40, wv = Math.sin(v * 8 - t * 30) * 4 * v; top.push([xx, y - 7 + wv]); bot.push([xx, y + 7 + wv]); }
        p.poly([...top, ...bot.reverse()], C[3], 0.85);
        p.polyline(top, C[5]); p.polyline(bot.reverse(), C[2]);
        p.glow(x - 10, y, 16, C[4], 0.35);
        sparkle(p, x, y, 4, C[5], WHITE);
      } else {
        const s = t - FLY, k = clamp(1 - s / 0.35);
        if (k > 0) { flare(p, TX, -TH, 12 * k, 1, [C[3], C[5], WHITE]); p.poly([[TX, -TH - 12 * k], [TX + 8 * k, -TH], [TX, -TH + 12 * k], [TX - 8 * k, -TH]], C[4], k); }
      }
      ps.draw(p); lights.draw(p);
    },
  };
}
