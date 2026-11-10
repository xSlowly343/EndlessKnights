import { Particles, Lights, P, Rng, fit, flare, clamp, crystal, WHITE } from "../../core/index.js";

const RUN = 0.26, END = 1.5;

export const meta = {
  name: "Clay spikes",
  desc: "The hammer blow sends a ripple of churned clay along the floor; under the target three fired-clay spikes burst up and toss it, hold, then crumble back down.",
  tags: ["earth", "clay", "ground", "spikes", "melee", "one-shot"],
  size: [320, 130],
  origin: [30, 104],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: ["#1e1008", "#40220e", "#6e3c18", "#a4602a", "#d89048", "#f4c47e", "#fff0d0"], tx: 150, seed: 2 },
};

export function create(o) {
  const C = fit(o.palette, 7), R = new Rng(o.seed), TX = o.tx;
  const ps = new Particles({ rng: R, ground: () => 0 }), lights = new Lights();
  const spikes = [[-8, 18, -0.25], [0, 30, 0.05], [9, 22, 0.3]];
  let t = 0, up = false;
  return {
    update(dt) {
      t += dt;
      if (t < RUN) { const x = 8 + (TX - 8) * (t / RUN); for (let i = 0; i < 2; i++) ps.spawn(P.GRIT, x + R.range(-3, 3), -1, R.range(-30, 30), R.range(-90, -40), R.range(0.3, 0.6), C[3]); if (R.next() < 0.6) ps.spawn(P.DUST, x, -2, R.range(-10, 10), R.range(-20, -5), R.range(0.3, 0.6), C[3]); }
      if (!up && t >= RUN) { up = true; lights.add(TX, -14, 40, C[4], 0.8, 0.4); for (let i = 0; i < 18; i++) ps.spawn(P.GRIT, TX + R.range(-10, 10), -2, R.range(-120, 120), R.range(-220, -80), R.range(0.4, 0.8), C[R.next() < 0.5 ? 3 : 2]); for (let i = 0; i < 6; i++) ps.spawn(P.DUST, TX + R.range(-14, 14), -2, R.range(-40, 40), R.range(-30, -10), R.range(0.6, 1.0), C[3]); }
      ps.update(dt); lights.update(dt);
    },
    draw(p) {
      if (t < RUN) { const x = 8 + (TX - 8) * (t / RUN); p.ellipse(x, 0, 6, 2, C[2], 0.9); p.ellipse(x, -1, 4, 1.4, C[3], 1); }
      if (t >= RUN) {
        const s = t - RUN, grow = clamp(s / 0.07), sink = clamp((s - 0.7) / 0.4), h = grow * (1 - sink);
        for (const [dx, H, lean] of spikes) if (h > 0.02) crystal(p, TX + dx, 0, H * h, 6, lean, [C[1], C[2], C[3], C[4], C[5]], { glint: false });
        if (s < 0.08) flare(p, TX, -4, 8, 0.8, [C[3], C[5], WHITE]);
      }
      ps.draw(p); lights.draw(p);
    },
  };
}
