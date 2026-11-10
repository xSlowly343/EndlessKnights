import { Particles, Lights, P, Rng, fit, sparkle, flare, clamp, WHITE } from "../../core/index.js";

const CROSS = 0.16, END = 1.2;

export const meta = {
  name: "Iai cut",
  desc: "A hair-thin white line splits the strip in an instant, the world holds its breath, then a cross of two cuts flares on the target and petals scatter.",
  tags: ["melee", "slash", "katana", "instant", "petals", "one-shot"],
  size: [320, 110],
  origin: [20, 90],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: ["#3a1020", "#7a2040", "#c04070", "#f08aaa", "#ffd0e0", "#fff0f6", "#ffffff"], tx: 150, height: 22, seed: 8 },
};

export function create(o) {
  const C = fit(o.palette, 7), R = new Rng(o.seed), TX = o.tx, H = o.height;
  const ps = new Particles({ rng: R, ground: () => 0, sparks: [WHITE, C[5], C[4], C[3]] }), lights = new Lights();
  let t = 0, cut = false;
  return {
    update(dt) {
      t += dt;
      if (!cut && t >= CROSS) {
        cut = true;
        lights.add(TX, -H, 50, C[4], 1, 0.4);
        for (let i = 0; i < 16; i++) ps.spawn(P.SPARK, TX, -H, R.range(-160, 160), R.range(-160, 80), R.range(0.15, 0.4));
        for (let i = 0; i < 18; i++) ps.spawn(P.FLAKE, TX + R.range(-6, 6), -H + R.range(-6, 6), R.range(-50, 70), R.range(-60, 10), R.range(0.6, 1.0), i % 2 ? C[3] : C[4], 2);
      }
      ps.update(dt); lights.update(dt);
    },
    draw(p) {
      if (t < 0.1) {
        const k = 1 - t / 0.1;
        p.line(4, -H, TX + 40, -H, WHITE, 1);
        if (t < 0.034) p.line(4, -H - 1, TX + 40, -H - 1, C[5], 1);
        p.glowLine(4, -H, TX + 40, -H, 4, C[4], 0.5 * k);
      }
      if (t >= CROSS && t < CROSS + 0.3) {
        const k = 1 - (t - CROSS) / 0.3, L = 16 * (0.6 + 0.4 * k);
        p.beam(TX - L, -H - L, TX + L, -H + L, 2.4 * k + 0.8, WHITE);
        p.beam(TX - L, -H + L, TX + L, -H - L, 2.4 * k + 0.8, C[5]);
        flare(p, TX, -H, 12 * k, 1, [C[3], C[5], WHITE]);
        p.glow(TX, -H, 22, C[4], 0.5 * k);
      }
      if (t >= CROSS && t < CROSS + 0.06) sparkle(p, TX, -H, 10, C[5], WHITE);
      ps.draw(p); lights.draw(p);
    },
  };
}
