import { Particles, Lights, P, Rng, fit, sparkle, flare, clamp, bolt, WHITE } from "../../core/index.js";

const FLY = 0.24, END = 1.0;
const NOTE = ["..kk", "..kK", "..k.", "..k.", "kkk.", "kkk."];

export const meta = {
  name: "Power chord",
  desc: "A slammed chord rips out of the guitar as stacked sound arcs with music notes and crackling sparks; they crash into the target with a flash, harder on the third strike.",
  tags: ["sound", "music", "guitar", "wave", "lightning", "one-shot"],
  size: [340, 130],
  origin: [20, 100],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: ["#1a0a2a", "#3a1a6a", "#7a3ac8", "#c070ff", "#ffb0f0", "#fff0ff", "#ffffff"], tx: 150, height: 22, power: 1, seed: 3 },
};

export function create(o) {
  const C = fit(o.palette, 7), R = new Rng(o.seed), TX = o.tx, H = o.height, K = o.power;
  const ps = new Particles({ rng: R, ground: () => 0, sparks: [WHITE, C[5], C[4], C[3]] }), lights = new Lights();
  const notes = [0, 1, 2].map((i) => ({ dy: R.range(-14, 10), dt: i * 0.04, wob: R.range(0, 6) }));
  let t = 0, hit = false, arcs = null;
  return {
    update(dt) {
      t += dt;
      if (!hit && t >= FLY) {
        hit = true; lights.add(TX, -H, 50 * K, C[4], 1, 0.4);
        for (let i = 0; i < 18 * K; i++) { const a = R.range(0, Math.PI * 2), sp = R.range(60, 200) * K; ps.spawn(P.SPARK, TX, -H, Math.cos(a) * sp, Math.sin(a) * sp, R.range(0.2, 0.45)); }
        arcs = [0, 1, 2].map(() => bolt(R, TX, -H, TX + R.range(-20, 20) * K, -H + R.range(-24, 24) * K, { jag: 0.4, depth: 3 }));
      }
      ps.update(dt); lights.update(dt);
    },
    draw(p) {
      if (t < FLY) {
        const u = t / FLY, x = 12 + (TX - 12) * u;
        for (let k = 0; k < 3; k++) {
          const r = (8 + k * 5) * (0.7 + 0.5 * K);
          p.arc(x - k * 6 - r, -H, r, 1 + (k === 0 ? 1 : 0), -0.9, 0.9, k === 0 ? WHITE : C[4 - k], 1 - k * 0.25);
        }
        p.glow(x - 6, -H, 14 * K, C[3], 0.35);
        for (const n of notes) {
          const nu = clamp((t - n.dt) / FLY); if (nu <= 0) continue;
          const nx = 12 + (TX - 20) * nu, ny = -H + n.dy + Math.sin(nu * 10 + n.wob) * 3;
          NOTE.forEach((row, yy) => [...row].forEach((ch, xx) => { if (ch !== ".") p.px(nx + xx, ny + yy - 6, ch === "K" ? WHITE : C[4]); }));
        }
        if (R.next() < 0.5) { const b = bolt(R, x - 10, -H, x + 4, -H + R.range(-8, 8), { jag: 0.4, depth: 2 }); p.polyline(b, C[5], 0.8); }
      } else {
        const s = t - FLY, k = clamp(1 - s / 0.3);
        if (k > 0) {
          for (let i = 0; i < 3; i++) p.ring(TX, -H, 4 + s * 120 * K * (1 + i * 0.3), 1, i ? C[4] : WHITE, k);
          flare(p, TX, -H, 12 * K * k, 1, [C[3], C[5], WHITE]);
          if (arcs && s < 0.15) for (const a of arcs) p.polyline(a, C[5]);
        }
      }
      ps.draw(p); lights.draw(p);
    },
  };
}
