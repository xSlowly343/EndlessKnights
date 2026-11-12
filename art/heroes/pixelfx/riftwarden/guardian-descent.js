import { Particles, Lights, P, PF, Rng, fit, clamp, lerp, seg, ease, band, sparkle, flare, WHITE } from "../../core/index.js";
import { spear } from "./guardian-spear.js";

const END = 0.92, FLOOR = 0, FALL = 0.14, HIT = 0.24, BEAT = 0.52, RISE = 0.62, SHUT = 0.74, RIFT = -43, TIP = -5;

export const meta = {
  name: "Guardian descent",
  desc: "The sky tears open and a spear drops through it into the target; it stands in a ring of gold, wings spread under a halo, beats once and climbs back as the seam closes.",
  tags: ["holy", "impact", "summon", "one-shot"],
  size: [116, 70],
  origin: [72, 58],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#4a2c08", "#9a6c18", "#e8b030", "#ffd84a", "#fff09a", "#fffbe6", "#ffffff"],
    sky: ["#1c3a8c", "#3a7ae0", "#7cc0ff", "#d8f0ff"],
    seed: 186,
  },
};

function spread(p, x, y, side, k, down, C, S) {
  const n = 8, arm = [];
  for (let i = 0; i <= n; i++) {
    const f = i / n, g = -0.15 - (0.75 - down * 0.95) * Math.pow(f, 0.85), R = 21 * k * f;
    arm.push([x + side * Math.cos(g) * R, y + Math.sin(g) * R - Math.sin(f * Math.PI) * 2.5 * k]);
  }
  for (let i = n; i >= 1; i--) {
    const f = i / n, [bx, by] = arm[i], L = (4 + 10 * Math.sin(0.25 + f * 2.2)) * k, dx = side * (0.1 + f * 0.5 - down * 0.2), tx = bx + dx * L * 0.6, ty = by + L;
    p.beam(bx, by, tx, ty, 4.4, i % 2 ? S[1] : C[2], 1);
    p.beam(bx, by, tx, ty, 3, (u, v) => (u > 0.72 ? (i % 2 ? S[2] : C[4]) : v < 0.5 ? C[6] : C[5]), 1);
  }
  for (let i = 1; i <= n; i++) p.beam(arm[i - 1][0], arm[i - 1][1], arm[i][0], arm[i][1], 3.6 - i * 0.22, (u, v) => (v < 0.5 ? C[6] : C[4]), 1);
  p.dot(arm[n][0], arm[n][1], WHITE, 1);
  p.light(arm[n][0], arm[n][1], C[4], 1);
}

export function create(o) {
  const C = fit(o.palette, 7), S = fit(o.sky, 4), R = new Rng(o.seed);
  const ps = new Particles({ rng: R, ground: () => FLOOR, sparks: [WHITE, C[4], C[2]], haze: C[2] }), lights = new Lights();
  const feathers = [];
  let t = 0, hit = false, left = false;
  lights.add(0, RIFT, 26, S[2], 0.6, 0.3);
  const shed = (n, y0, y1) => { for (let i = 0; i < n; i++) feathers.push({ x: R.range(-22, 22), y: R.range(y0, y1), vx: R.range(-30, 30), ph: R.range(0, 6), born: t, life: R.range(0.26, 0.4) }); };
  return {
    update(dt) {
      t += dt;
      if (t < SHUT && R.chance(dt * 40)) ps.spawn(P.STAR, R.range(-16, 16), RIFT + R.range(0, 5), R.range(-8, 8), R.range(20, 60), R.range(0.2, 0.36), R.chance(0.5) ? C[4] : S[3], 1);
      if (!hit && t >= HIT) {
        hit = true;
        lights.add(0, -6, 34, C[3], 0.7, 0.24);
        for (let i = 0; i < 26; i++) {
          const dir = i % 2 ? 1 : -1, sp = R.range(70, 230) * (dir > 0 ? 0.35 : 1);
          ps.spawn(P.SPARK, 0, -3, dir * sp, -R.range(20, 170), R.range(0.2, 0.44));
        }
        for (let i = 0; i < 8; i++) ps.spawn(P.VAPOUR, R.range(-14, 14), -R.range(0, 5), R.range(-60, 60) * 0.7, R.range(-20, -4), R.range(0.25, 0.4), C[4], R.range(3, 5.5), 0, PF.BACK);
        shed(6, -34, -18);
      }
      if (t > HIT && t < RISE && R.chance(dt * 36)) ps.spawn(P.EMBER, R.range(-20, 20), -R.range(0, 30), R.range(-10, 10), R.range(-50, -20), R.range(0.2, 0.4), C[4], R.range(1, 1.8));
      if (!left && t >= RISE) {
        left = true;
        lights.add(0, -24, 30, C[4], 0.5, 0.2);
        shed(7, -40, -16);
        for (let i = 0; i < 12; i++) ps.spawn(P.SPARK, R.range(-3, 3), -R.range(4, 30), R.range(-40, 40), R.range(-160, -60), R.range(0.14, 0.26));
      }
      ps.update(dt);
      lights.update(dt);
    },
    drawBack(p) {
      if (t >= END) return;
      const open = ease.outBack(seg(t, 0, 0.12), 1.6) * (1 - ease.inCubic(seg(t, SHUT, SHUT + 0.12)));
      if (open > 0.02) {
        const w = 17 * open, h = 3.2 * open;
        p.glow(0, RIFT, w + 8, S[1], 0.6 * open);
        for (let i = -4; i <= 4; i++) {
          const x0 = i * w * 0.2, spread = i * 3.4, L = (30 + (i % 2 ? 6 : 0)) * open * (t < HIT ? seg(t, 0.04, 0.16) : 1);
          p.line(x0, RIFT + 1, x0 + spread, RIFT + 1 + L, (v) => (v < 0.3 ? C[5] : v < 0.6 ? C[4] : C[3]), (v) => 0.5 * (1 - v) * (0.6 + 0.4 * Math.sin(t * 16 + i * 2)));
        }
        p.ellipse(0, RIFT, w + 1.5, h + 1.2, S[1], 1);
        p.ellipse(0, RIFT, w, h, (d) => (d > 0.75 ? S[2] : d > 0.4 ? S[3] : WHITE), 1);
        p.line(-w - 5 * open, RIFT, -w, RIFT, S[2], 1);
        p.line(w, RIFT, w + 5 * open, RIFT, S[2], 1);
        p.dot(-w - 6 * open, RIFT, WHITE, open);
        p.dot(w + 6 * open, RIFT, WHITE, open);
      }
      if (t >= HIT && t < RISE + 0.06) {
        const k = ease.outBack(seg(t, HIT + 0.02, HIT + 0.18), 2) * (1 - seg(t, RISE - 0.04, RISE + 0.04)), down = t >= BEAT ? Math.sin(seg(t, BEAT, RISE) * Math.PI) : 0;
        if (k > 0.03) for (const side of [-1, 1]) spread(p, side * 2, -23 + down * 2, side, clamp(k, 0, 1.15), down, C, S);
      }
      ps.draw(p, "back");
    },
    draw(p) {
      if (t >= END) return;
      const ring = t >= HIT ? ease.outQuart(seg(t, HIT, HIT + 0.26)) : 0;
      if (ring > 0 && t < RISE + 0.1) {
        const a = 1 - seg(t, RISE - 0.04, RISE + 0.1);
        band(p, 0, 1, 6 + 20 * ring, 0.22, 2, a, [C[1], C[2], C[4], C[6]], undefined, ring < 0.4);
        band(p, 0, 1, 4 + 12 * ring, 0.22, 1, a * 0.8, [C[1], C[2], C[3], C[5]]);
        for (let i = 0; i < 8; i++) { const g = i * 0.785 - t * 2; p.dot(Math.cos(g) * (5 + 16 * ring), 1 + Math.sin(g) * (5 + 16 * ring) * 0.22, WHITE, a); }
      }
      if (t >= FALL && t < RISE + 0.1) {
        const fall = ease.inQuad(seg(t, FALL, HIT)), up = ease.inCubic(seg(t, RISE, RISE + 0.1));
        const tipY = t < RISE ? lerp(RIFT + 10, TIP, fall) + (t >= HIT && t < HIT + 0.06 ? 2 : 0) : lerp(TIP, RIFT + 4, up);
        const L = Math.min(30, tipY - RIFT + 2);
        if (t < HIT || t >= RISE) for (let i = -1; i <= 1; i++) p.line(i * 3, tipY - (t < HIT ? L : -4), i * 3, tipY - (t < HIT ? L + 4 : -14 * up), i ? S[2] : C[5], 0.8);
        p.glowLine(0, RIFT, 0, tipY, 7, C[3], 0.35);
        if (L > 6) spear(p, 0, tipY, Math.PI / 2, L, C, S, 1, 1);
        if (t >= HIT + 0.04 && t < RISE) {
          const hk = ease.outBack(seg(t, HIT + 0.04, HIT + 0.16), 2), hy = tipY - 32;
          p.ellipse(0, hy, 8 * hk, 2.2 * hk, (d) => (d > 0.62 ? (Math.sin(t * 30) > 0 ? WHITE : C[5]) : 0), 1);
          p.glow(0, hy, 10, C[4], 0.5 * hk);
        }
      }
      if (t >= HIT && t < HIT + 0.12) {
        const f = 1 - (t - HIT) / 0.12;
        flare(p, 0, TIP, 14 * f, f, [C[2], C[3], C[4], C[5]]);
        p.ellipse(0, 0, 20 * (1.15 - f * 0.4), 3 * f, WHITE, 1);
      }
      for (const f of feathers) {
        const s = t - f.born;
        if (s > f.life) continue;
        const x = f.x + f.vx * s + Math.sin(s * 9 + f.ph) * 5, y = f.y + s * 46, tilt = Math.cos(s * 9 + f.ph) * 0.9, a = s / f.life > 0.7 ? 0.5 : 1;
        if (y > FLOOR) continue;
        p.beam(x - Math.cos(tilt) * 3, y - Math.sin(tilt) * 3, x + Math.cos(tilt) * 3, y + Math.sin(tilt) * 3, 2.2, (u) => (u < 0.5 ? C[6] : S[3]), a);
        p.dot(x + Math.cos(tilt) * 3, y + Math.sin(tilt) * 3, C[3], a);
        p.light(x, y, C[4], 0.4);
      }
      if (t >= SHUT + 0.08 && t < SHUT + 0.18) sparkle(p, 0, RIFT, 7 * (1 - (t - SHUT - 0.08) / 0.1), S[2], WHITE, 0.4);
      ps.draw(p, "front");
      lights.draw(p);
    },
  };
}
