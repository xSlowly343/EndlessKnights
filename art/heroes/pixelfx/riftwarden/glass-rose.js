import { Particles, Bits, Lights, P, B, Rng, rng, fit, clamp, seg, ease, bolt, hex, mix, scale, sparkle, TAU, WHITE } from "../../core/index.js";

const END = 0.9, RAD = 21, N = 8, CRACK = 0.36, BREAK = 0.44, FLOOR = 16;

export const meta = {
  name: "Glass rose",
  desc: "A rose window leads itself in pane by pane round the target and throws its colours on the floor; a glint walks across, it cracks from the heart and bursts into falling glass.",
  tags: ["glass", "impact", "one-shot"],
  size: [114, 80],
  origin: [58, 44],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#e8324a", "#f0a020", "#f4e04a", "#3ac86a", "#32b4e8", "#5a5ae8", "#b44ae0", "#f05aa8"],
    lead: "#141020",
    shine: "#fff8e0",
    seed: 172,
  },
};

export function create(o) {
  const J = fit(o.palette, N), LD = hex(o.lead), LT = hex(o.shine), R = new Rng(o.seed);
  const ps = new Particles({ rng: R, ground: () => FLOOR, sparks: [WHITE, LT, J[1]] }), bits = new Bits({ ground: () => FLOOR }), lights = new Lights();
  let t = 0, broke = false, cracked = false;
  lights.add(0, 0, 30, LT, 0.7, 0.2);
  const born = (i) => 0.03 + i * 0.022;
  return {
    update(dt) {
      t += dt;
      if (!cracked && t >= CRACK) {
        cracked = true;
        lights.add(0, 0, 26, LT, 0.6, 0.12);
      }
      if (!broke && t >= BREAK) {
        broke = true;
        lights.add(0, 0, 46, LT, 1, 0.26);
        for (let i = 0; i < N; i++) {
          const a0 = (i / N) * TAU - Math.PI / 2;
          for (let k = 0; k < 5; k++) {
            const a = a0 + R.range(0.05, TAU / N - 0.05), d = R.range(6, RAD - 2), sp = R.range(50, 150), c = J[i];
            bits.add({ kind: k < 3 ? B.CHUNK : B.SHARD, x: Math.cos(a) * d, y: Math.sin(a) * d, vx: Math.cos(a) * sp * 0.8 - 14, vy: Math.sin(a) * sp - R.range(30, 110), a: R.range(0, 6), va: R.range(-16, 16),
              s: k < 3 ? R.range(2.8, 4.6) : R.range(3, 6), c, ramp: [LD, scale(c, 0.55), scale(c, 0.8), c, mix(c, WHITE, 0.6)], bounce: 0.5, drag: 1.2, life: R.range(0.36, 0.46) });
          }
          ps.spawn(P.STAR, Math.cos(a0 + 0.4) * RAD * 0.8, Math.sin(a0 + 0.4) * RAD * 0.8, Math.cos(a0) * 26, Math.sin(a0) * 26, R.range(0.2, 0.36), mix(J[i], WHITE, 0.3), R.range(2, 3.4), R.range(0, 0.12));
        }
        for (let i = 0; i < 20; i++) {
          const a = R.range(0, TAU), sp = R.range(70, 200);
          ps.spawn(P.SPARK, 0, 0, Math.cos(a) * sp, Math.sin(a) * sp - 40, R.range(0.2, 0.42));
        }
      }
      ps.update(dt);
      bits.update(dt);
      lights.update(dt);
    },
    drawBack(p) {
      if (t >= END || t >= BREAK) return;
      const k = seg(t, 0.05, 0.3);
      p.glow(0, 0, 34, LT, 0.35 * k);
      for (let i = 0; i < N; i++) {
        const shown = seg(t, born(i), born(i) + 0.05);
        if (shown < 1) continue;
        const a = (i / N) * TAU - Math.PI / 2 + TAU / N / 2, x = Math.cos(a) * 13, y = Math.sin(a) * 13, beam = seg(t, born(i) + 0.05, born(i) + 0.2);
        const fx = -36 + i * 5.5, tx = x + (fx - x) * beam, ty = y + (FLOOR - y) * beam;
        p.glowLine(x, y, tx, ty, 4, J[i], 0.4);
        p.line(x, y, tx, ty, J[i], (u) => 0.35 * u);
        p.ellipse(fx, FLOOR + 1, 3.4 * beam, 1.3, J[i], 0.8 * beam);
        p.glow(fx, FLOOR, 7, J[i], 0.5 * beam);
      }
    },
    draw(p) {
      if (t >= END) return;
      if (t < BREAK) {
        const frame = ease.outBack(seg(t, 0, 0.1), 1.6), hot = t > BREAK - 0.035;
        p.ring(0, 0, (RAD + 2.5) * frame, 2.6, LD, 1);
        p.ring(0, 0, (RAD + 1.6) * frame, 1, hot ? WHITE : (v, g) => (Math.cos(g + 2.2) > 0.3 ? LT : hex("#b8902a")), 1);
        for (let i = 0; i < N; i++) {
          const s = seg(t, born(i), born(i) + 0.05);
          if (s <= 0) continue;
          const a0 = (i / N) * TAU - Math.PI / 2, c = J[i], flash = s < 1 || hot;
          p.arc(0, 0, RAD * (0.7 + 0.3 * s), RAD * (0.7 + 0.3 * s) - 6.5, a0 + 0.07, a0 + TAU / N - 0.07, (u, v) => (flash ? WHITE : v > 0.84 ? scale(c, 0.6) : v < 0.3 ? mix(c, LT, 0.55) : u < 0.2 ? mix(c, WHITE, 0.3) : c), 1);
          const m = a0 + TAU / N / 2;
          p.circle(Math.cos(m) * 15.5, Math.sin(m) * 15.5, 2.3, flash ? WHITE : mix(J[(i + 3) % N], LT, 0.35), 1);
          p.ring(Math.cos(m) * 15.5, Math.sin(m) * 15.5, 3.1, 0.9, LD, 1);
          p.line(Math.cos(a0) * 6, Math.sin(a0) * 6, Math.cos(a0) * RAD, Math.sin(a0) * RAD, LD, 1);
        }
        p.circle(0, 0, 6.4 * frame, LD, 1);
        p.circle(0, 0, 5.2 * frame, (d, g) => (hot ? WHITE : d < 0.45 ? LT : Math.sin(g * 4 + 0.8) > 0 ? J[1] : J[2]), 1);
        p.ring(0, 0, 2.6 * frame, 0.9, LD, 1);
        if (t > 0.2 && t < 0.34) {
          const g = -RAD + seg(t, 0.2, 0.34) * RAD * 2.4;
          for (let y = -RAD; y <= RAD; y++) {
            const x = g - y * 0.6;
            if (x * x + y * y < RAD * RAD) { p.dot(x, y, WHITE, 0.9); p.dot(x - 1, y, LT, 0.55); p.dot(x - 3, y, WHITE, 0.4); }
          }
        }
        if (t >= CRACK) {
          const Rc = rng(o.seed, 9), f = seg(t, CRACK, BREAK - 0.02);
          for (let i = 0; i < 5; i++) {
            const a = (i / 5) * TAU + Rc.range(-0.4, 0.4), l = RAD * (0.35 + 0.65 * f);
            const path = bolt(Rc, 0, 0, Math.cos(a) * l, Math.sin(a) * l, { jag: 0.2, depth: 3 });
            p.polyline(path, WHITE, 1);
          }
          sparkle(p, 0, 0, 5 + 5 * f, LT, WHITE);
        }
      } else if (t < BREAK + 0.1) {
        const f = 1 - (t - BREAK) / 0.1;
        sparkle(p, 0, 0, 18 * f, LT, WHITE);
        p.ring(0, 0, RAD + 10 * (1 - f), 2.2, f > 0.5 ? WHITE : LT, f);
        for (let i = 0; i < N; i++) {
          const a = (i / N) * TAU - Math.PI / 2 + TAU / N / 2;
          p.line(Math.cos(a) * 8, Math.sin(a) * 8, Math.cos(a) * (RAD + 9 * (1 - f)), Math.sin(a) * (RAD + 9 * (1 - f)), J[i], f);
        }
      }
      bits.draw(p);
      ps.draw(p, "front");
      lights.draw(p);
    },
  };
}
