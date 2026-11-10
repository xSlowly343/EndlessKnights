import { Particles, Lights, P, Rng, fit, flare, band, clamp, crystal, bolt, WHITE } from "../../core/index.js";

const RUN = 0.3, END = 1.8;

export const meta = {
  name: "Quake slam",
  desc: "The sledge hits: a crater dents the floor, a ring of dust and flying rocks burst out, a wide crack tears to the target heaving the ground, and stone spikes erupt under it in a spray of rock.",
  tags: ["earth", "quake", "ground", "crack", "spikes", "melee", "one-shot"],
  size: [360, 160],
  origin: [40, 130],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: ["#1a120c", "#3a281a", "#5e4430", "#8a6a4a", "#b89670", "#e0c8a0", "#fff4e0"], tx: 150, seed: 4 },
};

export function create(o) {
  const C = fit(o.palette, 7), R = new Rng(o.seed), TX = o.tx;
  const ps = new Particles({ rng: R, ground: () => 0 }), lights = new Lights();
  const crack = bolt(R, 4, 1, TX, 1, { jag: 0.06, depth: 6 });
  const spikes = [[-14, 18, -0.4], [-7, 26, -0.15], [0, 36, 0.02], [8, 28, 0.2], [15, 18, 0.45]];
  for (let i = 0; i < 26; i++) { const a = -Math.PI * R.range(0.05, 0.95), sp = R.range(90, 260); ps.spawn(P.GRIT, R.range(-4, 4), -1, Math.cos(a) * sp, Math.sin(a) * sp, R.range(0.4, 0.9), C[R.next() < 0.5 ? 3 : 2]); }
  for (let i = 0; i < 10; i++) ps.spawn(P.DUST, R.range(-10, 10), -2, R.range(-80, 80), R.range(-30, -8), R.range(0.6, 1.1), C[4]);
  lights.add(0, -6, 50, C[5], 0.9, 0.35);
  let t = 0, up = false;
  return {
    update(dt) {
      t += dt;
      if (t < RUN) {
        const x = 4 + (TX - 4) * (t / RUN);
        for (let i = 0; i < 5; i++) ps.spawn(P.GRIT, x + R.range(-4, 4), -1, R.range(-40, 40), R.range(-220, -70), R.range(0.4, 0.8), C[R.next() < 0.5 ? 3 : 4]);
        if (R.next() < 0.7) ps.spawn(P.DUST, x, -2, R.range(-20, 20), R.range(-26, -8), R.range(0.5, 0.9), C[4]);
      }
      if (!up && t >= RUN) {
        up = true; lights.add(TX, -18, 60, C[5], 1, 0.45);
        for (let i = 0; i < 34; i++) ps.spawn(P.GRIT, TX + R.range(-16, 16), -2, R.range(-160, 160), R.range(-300, -100), R.range(0.5, 1.0), C[R.next() < 0.5 ? 3 : 2]);
        for (let i = 0; i < 12; i++) ps.spawn(P.DUST, TX + R.range(-20, 20), -2, R.range(-60, 60), R.range(-40, -10), R.range(0.8, 1.3), C[4]);
      }
      ps.update(dt); lights.update(dt);
    },
    draw(p) {
      const s0 = t, ring = clamp(1 - s0 / 0.5);
      if (ring > 0) { band(p, 0, 0, 6 + s0 * 90, 0.18, 2, ring, [C[2], C[3], C[4], C[5]]); if (s0 < 0.08) flare(p, 0, -3, 10, 0.9, [C[4], C[5], WHITE]); }
      const dent = clamp(1 - (t - 1.2) / 0.5);
      if (dent > 0) { p.ellipse(0, 1, 12, 2.4, C[0], 0.9 * dent); p.ellipse(0, 0, 9, 1.6, C[1], dent); }
      const reach = clamp(t / RUN), n = Math.max(2, Math.round(crack.length * reach)), pts = crack.slice(0, n), gone = clamp((END - t) / 0.4);
      p.polyline(pts.map(([x, y]) => [x, y - 1]), C[4], 0.8 * gone);
      p.polyline(pts.map(([x, y]) => [x, y]), C[0], gone);
      p.polyline(pts.map(([x, y]) => [x, y + 1]), C[0], gone);
      p.polyline(pts.map(([x, y]) => [x, y + 2]), C[1], 0.8 * gone);
      if (reach < 1) { const hx = pts[pts.length - 1][0]; p.ellipse(hx, -2, 7, 3, C[3], 0.9); p.ellipse(hx, -3, 5, 2, C[4], 1); }
      if (t >= RUN) {
        const s = t - RUN, grow = clamp(s / 0.08), sink = clamp((s - 0.75) / 0.45), h = grow * (1 - sink);
        for (const [dx, H, lean] of spikes) if (h > 0.02) crystal(p, TX + dx, 0, H * h, 7, lean, [C[1], C[2], C[3], C[4], C[5]], { glint: false });
        if (s < 0.1) flare(p, TX, -6, 12, 1, [C[4], C[5], WHITE]);
        band(p, TX, 0, 8 + s * 70, 0.18, 1.5, clamp(1 - s / 0.5), [C[2], C[3], C[4]]);
      }
      ps.draw(p); lights.draw(p);
    },
  };
}
