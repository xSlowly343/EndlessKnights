import { Particles, Bits, Lights, P, PF, B, Rng, fit, clamp, lerp, seg, ease, band, sparkle, WHITE } from "../../core/index.js";
import { maw, leaf } from "./vine-seed.js";

const END = 0.9, FLOOR = 0, GROWN = 0.34, CHOMP = 0.44, CHEW = 0.6, GONE = 0.82, RX = -15;

export const meta = {
  name: "Vine maw",
  desc: "A stalk climbs out of a rune ring, leafing as it goes; the bud on top yawns open, snaps down over the target, chews twice and drags itself back into the soil.",
  tags: ["nature", "impact", "creature", "summon", "one-shot"],
  size: [116, 68],
  origin: [74, 56],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#0c1a0e", "#1c3c18", "#2f6a22", "#52a02e", "#8cd040", "#c8f070", "#f4ffc0"],
    mouth: ["#4a0a3a", "#a81a7a", "#f04ab4", "#ffb0e0"],
    seed: 184,
  },
};

const UP = [[RX, 2], [RX - 12, -15], [RX + 12, -28], [RX + 2, -37]];
const BACK = [[RX, 2], [RX - 14, -15], [RX + 5, -32], [RX - 5, -33]];
const DOWN = [[RX, 2], [RX - 13, -21], [RX + 6, -43], [-3, -19]];

function bez(c, u) {
  const v = 1 - u, a = v * v * v, b = 3 * v * v * u, d = 3 * v * u * u, e = u * u * u;
  return [a * c[0][0] + b * c[1][0] + d * c[2][0] + e * c[3][0], a * c[0][1] + b * c[1][1] + d * c[2][1] + e * c[3][1]];
}

export function create(o) {
  const C = fit(o.palette, 7), M = fit(o.mouth, 4), R = new Rng(o.seed);
  const ps = new Particles({ rng: R, ground: () => FLOOR, sparks: [WHITE, M[3], M[1]], haze: C[2] }), lights = new Lights();
  const bits = new Bits({ ground: () => FLOOR }), loose = [];
  let t = 0, chomped = false, chews = 0, sunk = false;
  lights.add(RX, 0, 26, C[3], 0.5, 0.3);
  for (let i = 0; i < 10; i++) ps.spawn(P.GRIT, RX + R.range(-8, 8), -1, R.range(-50, 50), R.range(-140, -60), R.range(0.25, 0.45), i % 2 ? "#6e5a40" : "#4f3f2c", 1, R.range(0.04, 0.14));
  const shed = (x, y, n) => { for (let i = 0; i < n; i++) loose.push({ x, y, vx: R.range(-60, 40), vy: R.range(-90, -20), ph: R.range(0, 6), born: t, life: R.range(0.22, 0.32) }); };
  return {
    update(dt) {
      t += dt;
      if (t < GONE - 0.4 && R.chance(dt * 34)) ps.spawn(P.EMBER, RX + R.range(-14, 14), -1, R.range(-8, 8), R.range(-60, -24), R.range(0.25, 0.45), C[5], R.range(1, 1.8));
      if (!chomped && t >= CHOMP) {
        chomped = true;
        lights.add(0, -15, 22, M[1], 0.4, 0.18);
        for (let i = 0; i < 24; i++) {
          const a = R.range(0, Math.PI * 2), sp = R.range(70, 210);
          ps.spawn(P.SPARK, 0, -15, Math.cos(a) * sp * (Math.cos(a) > 0 ? 0.4 : 1), Math.sin(a) * sp - 30, R.range(0.2, 0.4));
        }
        for (let i = 0; i < 12; i++) ps.spawn(P.STAR, R.range(-10, 8), -15 + R.range(-9, 9), R.range(-40, 24), R.range(-50, -5), R.range(0.2, 0.34), i % 2 ? C[6] : M[3], 1);
        shed(-4, -22, 5);
      }
      if (chews < 2 && t >= CHOMP + 0.07 + chews * 0.06) {
        chews++;
        for (let i = 0; i < 4; i++) bits.add({ kind: B.DRIP, x: R.range(-5, 5), y: -13, vx: R.range(-40, 30), vy: R.range(-60, -10), c: i % 2 ? "#f04ab4" : "#c8f070", g: 0.7, life: 0.3 });
        for (let i = 0; i < 5; i++) ps.spawn(P.SPARK, 0, -15, R.range(-120, 50), R.range(-90, 20), R.range(0.14, 0.24));
      }
      if (!sunk && t >= GONE - 0.06) {
        sunk = true;
        lights.add(RX, -2, 22, C[4], 0.5, 0.16);
        for (let i = 0; i < 9; i++) ps.spawn(P.GRIT, RX + R.range(-6, 6), -1, R.range(-60, 60), R.range(-120, -50), R.range(0.07, 0.11), i % 2 ? "#6e5a40" : C[3], 1);
        for (let i = 0; i < 4; i++) ps.spawn(P.DUST, RX + R.range(-8, 8), -2, R.range(-30, 30), R.range(-20, -6), R.range(0.08, 0.11), "#6e5a40", R.range(3, 5));
      }
      ps.update(dt);
      bits.update(dt);
      lights.update(dt);
    },
    drawBack(p) {
      if (t >= END) return;
      const k = ease.outBack(seg(t, 0, 0.13), 1.4) * (1 - seg(t, GONE, END - 0.03));
      if (k > 0.02) {
        band(p, RX, 1, 17 * k, 0.24, 1.6, k, [C[1], C[3], C[5], C[6]], (g) => Math.sin(g) < 0);
        band(p, RX, 1, 11 * k, 0.24, 1, k * 0.8, [C[1], C[2], C[3], C[5]], (g) => Math.sin(g) < 0);
      }
      ps.draw(p, "back");
    },
    draw(p) {
      if (t >= END) return;
      const k = ease.outBack(seg(t, 0, 0.13), 1.4) * (1 - seg(t, GONE, END - 0.03));
      if (k > 0.02) {
        p.glow(RX, 0, 20, C[3], 0.35 * k);
        band(p, RX, 1, 17 * k, 0.24, 1.6, k, [C[1], C[3], C[5], C[6]], (g) => Math.sin(g) >= 0, t > CHOMP && t < CHOMP + 0.08);
        band(p, RX, 1, 11 * k, 0.24, 1, k * 0.8, [C[1], C[2], C[3], C[5]], (g) => Math.sin(g) >= 0);
        for (let i = 0; i < 6; i++) {
          const g = i * 1.047 + t * 2.4, x = RX + Math.cos(g) * 14 * k, y = 1 + Math.sin(g) * 14 * k * 0.24;
          p.dot(x, y, C[6], k);
          p.dot(x, y - 1, C[5], k * (0.5 + 0.5 * Math.sin(t * 20 + i)));
          p.light(x, y, C[4], 0.8 * k);
        }
      }
      if (t > 0.08 && t < GONE) {
        const wind = ease.inOutQuad(seg(t, GROWN - 0.04, GROWN + 0.05)), strike = ease.inCubic(seg(t, CHOMP - 0.045, CHOMP));
        const ctrl = UP.map((q, i) => [lerp(lerp(q[0], BACK[i][0], wind), DOWN[i][0], strike) + (i > 1 ? Math.sin(t * 13) * 1.6 * (1 - strike) : 0), lerp(lerp(q[1], BACK[i][1], wind), DOWN[i][1], strike)]);
        const out = t < CHEW ? ease.outCubic(seg(t, 0.08, GROWN - 0.02)) : 1 - ease.inCubic(seg(t, CHEW, GONE));
        const n = 24, pts = [], shake = t >= CHOMP && t < CHEW ? Math.sin(t * 120) : 0;
        for (let i = 0; i <= n; i++) { const q = bez(ctrl, (i / n) * out); pts.push([q[0] + (i / n) * shake, Math.min(q[1], FLOOR + 1)]); }
        for (let pass = 0; pass < 2; pass++) for (let i = 1; i <= n; i++) {
          const w = 5.4 - (i / n) * 2;
          if (pass === 0) p.beam(pts[i - 1][0], pts[i - 1][1], pts[i][0], pts[i][1], w + 2, C[0], 1);
          else p.beam(pts[i - 1][0], pts[i - 1][1], pts[i][0], pts[i][1], w, (q, v) => (v < 0.4 ? C[4] : Math.floor(i * 0.5 + t * 10) % 3 === 0 ? C[2] : C[3]), 1);
        }
        for (let i = 3; i < n - 2; i += 4) {
          const [x, y] = pts[i], [bx, by] = pts[i - 1], dir = Math.atan2(y - by, x - bx), side = (i >> 2) % 2 ? 1 : -1, grow = clamp(out * n - i, 0, 3) / 3;
          if (y > -2 || grow <= 0) continue;
          leaf(p, x, y, dir + side * 1.5 + Math.sin(t * 12 + i) * 0.14, (9 - i * 0.14) * grow, C);
          p.line(x, y, x + Math.cos(dir - side * 1.5) * 3.4, y + Math.sin(dir - side * 1.5) * 3.4, C[6], 1);
        }
        const head = pts[n], prev = pts[n - 2], ang = Math.atan2(head[1] - prev[1], head[0] - prev[0]);
        const open = t < GROWN - 0.04 ? 0.06 : t < CHOMP - 0.03 ? 0.06 + 0.94 * ease.outBack(seg(t, GROWN - 0.04, GROWN + 0.04), 2) : t < CHOMP ? 1 - seg(t, CHOMP - 0.03, CHOMP) : t < CHEW ? 0.34 * Math.abs(Math.sin((t - CHOMP) * Math.PI / 0.06)) * (t > CHOMP + 0.02 ? 1 : 0) : 0.05;
        const size = 0.55 + 0.6 * ease.outBack(seg(t, 0.14, GROWN), 1.6) * (t < CHEW ? 1 : 1 - 0.5 * seg(t, CHEW, GONE));
        if (head[1] < -1) maw(p, head[0] - Math.cos(ang) * 1, head[1] - Math.sin(ang) * 1, ang, size, open, C, M);
      }
      if (t >= CHOMP && t < CHOMP + 0.13) {
        const f = 1 - (t - CHOMP) / 0.13;
        sparkle(p, 0, -15, 7 * f, M[3], WHITE, 0.15);
        p.arc(0, -15, 8 + 16 * (1 - f * f), 2, Math.PI * 0.15, Math.PI * 1.15, f > 0.5 ? WHITE : M[2], f);
        p.arc(0, -15, 6 + 14 * (1 - f * f), 1.5, -Math.PI * 0.8, -Math.PI * 0.1, f > 0.5 ? WHITE : C[5], f);
      }
      for (const f of loose) {
        const s = t - f.born;
        if (s > f.life) continue;
        const x = f.x + f.vx * s + Math.sin(s * 10 + f.ph) * 4, y = f.y + f.vy * s + 280 * s * s;
        if (y < FLOOR) leaf(p, x, y, s * 14 + f.ph, 6, C, s / f.life > 0.7 ? 0.5 : 1);
      }
      bits.draw(p);
      ps.draw(p, "front");
      lights.draw(p);
    },
  };
}
