import { Particles, Lights, P, PF, Rng, fit, clamp, seg, ease, noise, hex, sparkle, WHITE } from "../../core/index.js";

const END = 0.86, R0 = 19, A0 = -2.5, SWEEP = 5.75, FLOOR = 16;

export const meta = {
  name: "Ink enso",
  desc: "The stroke lands as a blot and one breath of the brush leaves a circle, wet at the start and dry at the lift; ink runs, a red seal comes down, and it all lets go as petals.",
  tags: ["ink", "impact", "one-shot"],
  size: [100, 76],
  origin: [56, 42],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#06060c", "#1a1a2a", "#3c3c58", "#8a8ab0", "#d8d8f0", "#ffffff"],
    seal: ["#6a0c14", "#d8322a", "#ff8a6a"],
    seed: 162,
  },
};

export function create(o) {
  const K = fit(o.palette, 6), S = o.seal.map(hex), R = new Rng(o.seed);
  const ps = new Particles({ rng: R, ground: () => FLOOR, sparks: [WHITE, K[4], K[3]] }), lights = new Lights();
  const fingers = Array.from({ length: 11 }, (_, i) => ({ a: (i / 11) * Math.PI * 2 + R.range(-0.2, 0.2), l: R.range(7, 15), w: R.range(1.6, 3.2) }));
  const drops = Array.from({ length: 12 }, () => ({ a: R.range(0, Math.PI * 2), sp: R.range(60, 150), r: R.range(0.8, 1.9), life: R.range(0.2, 0.36) }));
  const runs = [-2.2, -1.5, -0.8, 0.1, 0.9].map((dx, i) => ({ a: Math.PI / 2 + dx * 0.36, len: R.range(5, 12), at: 0.2 + i * 0.03 + R.range(0, 0.04) }));
  const petals = [];
  let t = 0, stamped = false, freed = false;
  lights.add(0, 0, 30, K[4], 0.7, 0.2);
  return {
    update(dt) {
      t += dt;
      if (!stamped && t >= 0.3) {
        stamped = true;
        lights.add(15, 11, 22, S[1], 0.9, 0.22);
        for (let i = 0; i < 8; i++) ps.spawn(P.SPARK, 15, 11, R.range(-110, 110), R.range(-130, -10), R.range(0.14, 0.3));
      }
      if (!freed && t >= 0.56) {
        freed = true;
        for (let i = 0; i < 16; i++) {
          const a = A0 + (i / 16) * SWEEP;
          petals.push({ x: Math.cos(a) * R0, y: Math.sin(a) * R0, vx: Math.cos(a) * R.range(4, 26) - 14, vy: R.range(-34, -8), ph: R.range(0, 6), red: i % 3 === 0, life: R.range(0.18, 0.28) });
          ps.spawn(P.VAPOUR, Math.cos(a) * R0, Math.sin(a) * R0, Math.cos(a) * 14, Math.sin(a) * 14 - 10, R.range(0.18, 0.28), K[2], R.range(3, 5), 0, PF.BACK);
        }
        for (let i = 0; i < 12; i++) ps.spawn(P.EMBER, R.range(-22, 22), R.range(-22, 14), R.range(-16, 16), R.range(-50, -14), R.range(0.16, 0.28), 0, 1);
      }
      ps.update(dt);
      lights.update(dt);
    },
    drawBack(p) {
      if (t >= END) return;
      const paper = Math.min(seg(t, 0, 0.1), 1 - seg(t, 0.5, 0.7));
      if (paper > 0) {
        p.disc(0, 0, 27 + 2 * Math.sin(t * 9), K[4], (d, g) => 0.5 * paper * (d > 0.86 + 0.08 * Math.sin(g * 7) ? 0.35 : 1));
        p.glow(0, 0, 34, K[4], 0.25 * paper);
      }
      ps.draw(p, "back");
    },
    draw(p) {
      if (t >= END) return;
      const k = ease.outCubic(seg(t, 0.02, 0.2)), gone = seg(t, 0.56, 0.7), ink = 1 - gone;
      if (ink > 0 && k > 0) {
        const span = SWEEP * k, w = (u) => 2.2 + 5 * Math.pow(Math.sin(Math.PI * clamp(u * k * 0.9 + 0.08)), 0.7) * (1 - u * k * 0.25);
        const dry = (u, v) => (u * k > 0.72 && noise(v * 9 + 3, o.seed) < (u * k - 0.72) * 3.2 ? 0 : ink);
        p.arc(0, 0, R0 + 4.6, (u) => w(u) + 2.2, A0 - 0.03, A0 + span + 0.02, K[4], (u, v) => dry(u, v) * (u * k > 0.8 ? 0.7 : 1));
        p.arc(0, 0, R0 + 3.5, w, A0, A0 + span, (u, v) => (v > 0.86 ? K[2] : v < 0.12 ? K[1] : K[0]), dry);
        if (k < 1) {
          const a = A0 + span, x = Math.cos(a) * R0, y = Math.sin(a) * R0;
          p.circle(x, y, 4.4, K[4], 1);
          p.circle(x, y, 3.4, K[0], 1);
          p.beam(x, y, x + Math.cos(a - 2.2) * 12, y + Math.sin(a - 2.2) * 12, 2, (u) => (u < 0.5 ? K[3] : S[1]), 1);
          p.dot(x - 1, y - 1, WHITE, 1);
          p.glow(x, y, 9, K[4], 0.5);
        }
        for (const r of runs) {
          const g = ease.outCubic(seg(t, r.at, r.at + 0.26));
          if (g <= 0) continue;
          const x = Math.cos(r.a) * (R0 + 1), y = Math.sin(r.a) * (R0 + 1), l = r.len * g;
          p.beam(x, y, x, y + l, 3.2, K[4], ink);
          p.beam(x, y, x, y + l, 1.6, K[0], ink);
          p.circle(x, y + l, 2.4, K[4], ink);
          p.circle(x, y + l, 1.5, K[0], ink);
          p.dot(x - 0.5, y + l - 0.5, WHITE, ink);
        }
      }
      const blot = 1 - seg(t, 0.3, 0.52);
      if (blot > 0) {
        const sp = ease.outQuart(seg(t, 0, 0.1));
        for (const pass of [1, 0]) {
          for (const f of fingers) {
            const l = f.l * sp, x = Math.cos(f.a) * l, y = Math.sin(f.a) * l * 0.85;
            p.beam(0, 0, x, y, f.w + pass * 2, pass ? K[4] : K[0], (u) => (pass ? blot : blot) * (u > 0.85 ? 1 : 1));
            p.circle(x, y, f.w * 0.6 + pass, pass ? K[4] : K[0], blot);
          }
          p.circle(0, 0, 6.5 * sp + pass, pass ? K[4] : K[0], blot);
        }
        p.arc(0, 0, 6 * sp, 1.2, Math.PI * 1.05, Math.PI * 1.7, K[2], blot);
        p.dot(-2, -3, WHITE, blot);
      }
      if (t < 0.09) sparkle(p, 0, 0, 14 * (1 - t / 0.09), K[4], WHITE);
      for (const d of drops) {
        if (t > d.life) continue;
        const x = Math.cos(d.a) * d.sp * t, y = Math.sin(d.a) * d.sp * t * 0.8 + 380 * t * t, a = 1 - t / d.life;
        if (y > FLOOR) continue;
        p.circle(x, y, d.r + 1, K[4], a);
        p.circle(x, y, d.r, K[0], 1);
      }
      if (t >= 0.3 && ink > 0) {
        const s = t - 0.3, pop = s < 0.1 ? 1 + 0.5 * (1 - s / 0.1) : 1, h = 5 * pop, cx = 15, cy = 11;
        p.rect(cx - h - 1, cy - h - 1, h * 2 + 2, h * 2 + 2, s < 0.04 ? WHITE : S[0], ink);
        p.rect(cx - h, cy - h, h * 2, h * 2, s < 0.04 ? WHITE : S[1], ink);
        if (s >= 0.04) {
          p.line(cx - 3, cy - 3, cx + 2, cy - 3, S[2], ink);
          p.line(cx - 1, cy - 3, cx - 1, cy + 3, S[2], ink);
          p.line(cx - 3, cy, cx + 3, cy, WHITE, ink);
          p.line(cx + 2, cy - 1, cx + 2, cy + 3, S[2], ink);
          p.line(cx - 3, cy + 3, cx + 3, cy + 3, S[2], ink);
        }
        if (s < 0.12) p.ring(cx, cy, 6 + 60 * s, 1, S[2], 1 - s / 0.12);
      }
      for (const f of petals) {
        const s = t - 0.56;
        if (s > f.life) continue;
        const x = f.x + f.vx * s + Math.sin(s * 12 + f.ph) * 3, y = f.y + f.vy * s + 60 * s * s, a = 1 - s / f.life, tilt = Math.abs(Math.cos(s * 12 + f.ph));
        p.ellipse(x, y, 2.4, 0.8 + tilt * 1.4, f.red ? S[1] : K[0], a);
        p.dot(x - 1, y - 1, f.red ? S[2] : K[4], a);
        if (!f.red) p.dot(x + 1, y + 1, K[3], a);
      }
      ps.draw(p, "front");
      lights.draw(p);
    },
  };
}
