import { Particles, Lights, P, Rng, fit, sparkle, clamp, WHITE } from "../../core/index.js";

const FLY = 0.2, END = 2.0;

export const meta = {
  name: "Venom fan",
  desc: "Three small green crescents fly out in a fan and converge on the target; where they bite, a cloud of poison swells, drips and lingers.",
  tags: ["poison", "projectile", "fan", "cloud", "dot", "one-shot"],
  size: [320, 120],
  origin: [20, 96],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: ["#0c2410", "#164a1c", "#2a7a26", "#56b432", "#9ee05a", "#e0ffb0", "#ffffff"], tx: 150, height: 22, th: 16, seed: 9 },
};

export function create(o) {
  const C = fit(o.palette, 7), R = new Rng(o.seed), TX = o.tx, H = o.height, TH = o.th;
  const ps = new Particles({ rng: R, ground: () => 0, sparks: [WHITE, C[5], C[4], C[3]] }), lights = new Lights();
  const fan = [-14, 0, 14];
  let t = 0, hit = false;
  return {
    update(dt) {
      t += dt;
      if (!hit && t >= FLY) { hit = true; lights.add(TX, -TH, 40, C[4], 0.9, 0.4); for (let i = 0; i < 8; i++) ps.spawn(P.VAPOUR, TX + R.range(-8, 8), -TH + R.range(-6, 6), R.range(-12, 12), R.range(-14, 2), R.range(1.0, 1.6), C[3]); }
      if (t > FLY && t < END - 0.4 && R.next() < 0.25) ps.spawn(P.MOTE, TX + R.range(-8, 8), -TH + 4, 0, R.range(30, 60), R.range(0.3, 0.6), C[4]);
      ps.update(dt); lights.update(dt);
    },
    draw(p) {
      if (t < FLY) {
        const u = t / FLY;
        fan.forEach((d, i) => {
          const x = 10 + (TX - 10) * u, y = -(H + (TH - H) * u) + d * Math.sin(u * Math.PI) - d * 0 , r = 6;
          p.arc(x - r, y, r, (v) => 1 + 2 * Math.sin(Math.PI * v), -1, 1, (v) => (v > 0.4 && v < 0.6 ? WHITE : C[4]), 1);
          p.glow(x - 2, y, 8, C[3], 0.3);
        });
      }
      if (t >= FLY && t < FLY + 0.06) sparkle(p, TX, -TH, 8, C[5], WHITE);
      if (t >= FLY) p.glow(TX, -TH, 18, C[2], 0.25 * clamp(1 - (t - FLY) / (END - FLY)));
      ps.draw(p); lights.draw(p);
    },
  };
}
