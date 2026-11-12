import { Particles, Lights, P, Rng, fit, hex, sparkle, band, crystal, bolt, star, ngon, clamp, seg, bump, ease, rand, WHITE } from "../../core/index.js";

const SHORT = 0.22;
const DOUBLE = 0.32;
const ECHO_AT = 0.08;
const CENTRE_ABOVE_GROUND = 14;
const TOP = 33;

const grow = (u) => ease.outQuart(seg(u, 0, 0.3));
const fade = (u) => clamp(1 - seg(u, 0.45, 1));
const pulse = (v) => Math.sin(Math.PI * v);

function moved(p, dx, dy, angle, fn) {
  const saved = [p.tx, p.ty, p.cs, p.sn], tx = p.toX(dx, dy), ty = p.toY(dx, dy), base = Math.atan2(p.sn, p.cs) + angle * p.fx;
  p.tx = tx; p.ty = ty; p.cs = Math.cos(base); p.sn = Math.sin(base);
  fn();
  [p.tx, p.ty, p.cs, p.sn] = saved;
}

function cut(p, x0, y0, x1, y1, u, hot, mid, light) {
  const reach = seg(u, 0, 0.2), f = clamp(1 - seg(u, 0.3, 1));
  if (f <= 0) return;
  const ex = x0 + (x1 - x0) * reach, ey = y0 + (y1 - y0) * reach, L = Math.hypot(x1 - x0, y1 - y0) || 1;
  const nx = -(y1 - y0) / L, ny = (x1 - x0) / L, w = 0.5 + 1.1 * f;
  p.glowLine(x0, y0, ex, ey, 4, light, 0.35 * f);
  p.line(x0 + nx * w, y0 + ny * w, ex + nx * w, ey + ny * w, mid, (v) => f * pulse(v));
  p.line(x0 - nx * w, y0 - ny * w, ex - nx * w, ey - ny * w, mid, (v) => f * pulse(v));
  p.line(x0, y0, ex, ey, hot, (v) => Math.min(1, 0.35 + f) * Math.sqrt(pulse(v)));
}

function quad(p, x, y, angle, length, width, c, a) {
  const cx = Math.cos(angle), cy = Math.sin(angle), nx = -cy * width / 2, ny = cx * width / 2;
  p.poly([[x + nx, y + ny], [x + cx * length, y + cy * length], [x - nx, y - ny], [x - cx * length * 0.25, y - cy * length * 0.25]], c, a);
}

function curve(a, b, c, v) {
  const w = 1 - v;
  return [w * w * a[0] + 2 * w * v * b[0] + v * v * c[0], w * w * a[1] + 2 * w * v * b[1] + v * v * c[1]];
}

const MOTIFS = {
  void(p, u, s, K) {
    const { C, c } = K, f = fade(u), open = grow(u), r = 11 * s;
    p.glow(0, c, 13 * s, C[2], 0.3 * f);
    p.arc(-r + 2 + open * 4, c, r + open * 2, (v) => 1 + 4 * s * f * pulse(v), -1.2, 1.2, (v) => (Math.abs(v - 0.5) < 0.2 * f ? WHITE : Math.abs(v - 0.5) < 0.32 ? C[4] : C[3]), 0.3 + 0.7 * f);
    p.line(1, c - r * 1.15, 1, c + r * 1.15, (v) => (Math.abs(v - 0.5) < 0.3 * f ? WHITE : C[4]), (v) => f * pulse(v));
  },
  star(p, u, s, K) {
    const { C, c } = K, f = fade(u), r = 13 * s * grow(u) * (0.5 + 0.5 * f);
    p.glow(0, c, 12 * s, C[3], 0.35 * f);
    if (r > 2) {
      p.poly(star(0, c, r * 0.22, r, 4, -Math.PI / 2), C[4]);
      p.poly(star(0, c, r * 0.12, r * 0.6, 4, -Math.PI / 2), u < 0.5 ? WHITE : C[5]);
    }
    for (let i = 0; i < 4; i++) {
      const a = Math.PI / 4 + (i * Math.PI) / 2, d = (6 + 9 * s * u) * 1.1;
      sparkle(p, Math.cos(a) * d, c + Math.sin(a) * d, 2.4 * f, C[5], WHITE);
    }
  },
  moth(p, u, s, K) {
    const { C, c } = K, f = fade(u), g = grow(u);
    for (let i = 0; i < 3; i++) {
      const a = -2.6 + i * 1.25, d = 4 + 10 * s * g, x = Math.cos(a) * d - 2, y = c + Math.sin(a) * d * 0.8 - 5 * u;
      const flap = 2 + 4 * Math.abs(Math.sin(u * 22 + i * 2));
      if (f <= 0.1) continue;
      p.tri(x, y, x - flap - 1, y - 4, x - flap, y + 3, C[3], Math.min(1, f * 2.5));
      p.tri(x, y, x + flap + 1, y - 4, x + flap, y + 3, C[4], Math.min(1, f * 2.5));
      p.line(x, y - 2, x, y + 2, C[5], 1);
      p.light(x, y, C[3], 0.3 * f);
    }
    for (let i = 0; i < 6; i++) p.dot((rand(K.seed, i) - 0.5) * 22 * s * g, c + (rand(K.seed, i + 9) - 0.6) * 18 * s * g + 6 * u, C[5], f * 0.9);
  },
  cog(p, u, s, K) {
    const { C, c } = K, f = fade(u), r = (5 + 5 * s * grow(u)), rot = u * 2.6, y = c + 4;
    p.glow(0, y, 12 * s, C[4], 0.3 * f);
    for (let i = 0; i < 8; i++) {
      const a = rot + (i * Math.PI) / 4;
      quad(p, Math.cos(a) * (r - 1), y + Math.sin(a) * (r - 1), a, 3.5, 3, C[4], f);
    }
    p.ring(0, y, r, 2.4, (v) => (v > 0.6 ? C[5] : C[3]), f);
    p.ring(0, y, 2.2, 1.2, u < 0.4 ? WHITE : C[6], f);
  },
  card(p, u, s, K) {
    const { C, A, c } = K, f = fade(u), d = 12 * s;
    cut(p, -d, c - d * 0.85, d, c + d * 0.8, u, WHITE, A[2], A[1]);
    cut(p, d, c - d * 0.9, -d * 0.95, c + d * 0.8, u - 0.12, WHITE, C[5], A[1]);
    const y = c - 2 - 6 * u;
    p.poly([[0, y - 3], [2.2, y], [0, y + 3], [-2.2, y]], A[u < 0.5 ? 2 : 1], f);
  },
  bramble(p, u, s, K) {
    const { C, A, g } = K, f = fade(u), pop = grow(u) * (0.45 + 0.55 * f);
    const thorns = [[-7, 14, -3], [0, 21, 1], [6, 12, 3]];
    for (const [x, h, lean] of thorns) {
      const top = g - h * s * pop;
      if (g - top < 2) continue;
      p.tri(x - 2.5, g, x + 2.5, g, x + lean * pop, top, C[4]);
      p.line(x - 1.5, g - 1, x + lean * pop, top, C[6], 1);
      p.line(x + 2.5, g, x + lean * pop, top, C[2], 1);
      p.dot(x + lean * pop, top, A[3], 1);
    }
    p.glow(0, g - 6, 12 * s, C[4], 0.25 * f);
  },
  rift(p, u, s, K) {
    const { C, c } = K, f = fade(u), open = grow(u), w = 3.2 * s * f * open;
    const path = [[1, -14], [-2, -7], [2, -2], [-1, 5], [1, 13]].map(([x, y]) => [x, c + y * s * (0.4 + 0.6 * open)]);
    p.glow(0, c, 13 * s, C[3], 0.35 * f);
    if (w > 0.6) {
      p.poly([...path.map(([x, y], i) => [x - w * pulse(i / 4), y]), ...path.slice().reverse().map(([x, y], i) => [x + w * pulse(1 - i / 4), y])], C[1]);
      p.polyline(path.map(([x, y], i) => [x - w * pulse(i / 4) - 1, y]), C[5], 1);
      p.polyline(path.map(([x, y], i) => [x + w * pulse(1 - i / 4) + 1, y]), C[4], 1);
    } else p.polyline(path, u < 0.3 ? WHITE : C[4], f);
    for (let i = 0; i < 3; i++) cut(p, -9 * s, c - 9 * s + i * 6, 8 * s, c - 3 * s + i * 6, u - 0.08 * i, WHITE, C[5], C[3]);
  },
  koi(p, u, s, K) {
    const { C, A, g } = K, f = fade(u);
    band(p, 0, g, 4 + 13 * s * ease.outQuad(u), 0.22, 2, f, [C[1], C[2], C[3], C[5]]);
    for (let i = 0; i < 5; i++) {
      const dx = (i - 2) * 5.5 * s, h = (13 + (i % 2) * 7) * s, x = dx * u, y = g - h * 4 * u * (1 - u);
      p.line(x - dx * 0.12, y + 3, x, y, C[3], f);
      p.dot(x, y, i === 2 ? WHITE : C[4], 1);
      p.light(x, y, C[3], 0.35 * f);
    }
    const tail = bump(u, 0.1, 0.8), y = g - 9 * s * tail;
    if (tail > 0.25) {
      p.tri(0, y, -4, y - 5, 1, y - 3, A[1]);
      p.tri(0, y, 4, y - 5, 0, y - 3, A[2]);
    }
  },
  chord(p, u, s, K) {
    const { C, A, B, c } = K, f = fade(u), r = 5 + 11 * s * ease.outQuad(u);
    p.glow(0, c, 12 * s, C[4], 0.3 * f);
    p.arc(0, c, r, (v) => 2.4 * f * pulse(v), Math.PI - 1, Math.PI + 1, (v) => (Math.abs(v - 0.5) < 0.25 ? C[6] : C[4]), 1);
    p.arc(0, c, r * 0.62, (v) => 1.8 * f * pulse(v), Math.PI - 0.9, Math.PI + 0.9, A[2], 1);
    p.arc(0, c, r * 0.7, (v) => 1.8 * f * pulse(v), -0.8, 0.8, A[3], 1);
    const x = 3, y = c - 5 - 10 * s * u;
    if (f > 0.1) {
      p.line(x + 2, y, x + 2, y - 6, B[3], 1);
      p.line(x + 2, y - 6, x + 5, y - 5, B[3], 1);
      p.rect(x, y - 1, 3, 2, B[2], 1);
    }
  },
  phoenix(p, u, s, K) {
    const { C, g } = K, f = fade(u), pop = grow(u) * (0.5 + 0.5 * f);
    const tongues = [[-6, 13, -2], [0, 22, 0], [6, 14, 2]];
    p.glow(0, g - 8, 14 * s, C[3], 0.4 * f);
    for (const [x, h, lean] of tongues) {
      const top = g - h * s * pop, sway = Math.sin(u * 14 + x) * 1.5;
      if (g - top < 2) continue;
      p.poly([[x - 3, g], [x - 2 + sway, (g + top) / 2], [x + lean + sway, top], [x + 2 + sway, (g + top) / 2], [x + 3, g]], C[3]);
      p.poly([[x - 1.5, g], [x + sway, (g + top) / 2 + 2], [x + lean * 0.6 + sway, top + 5], [x + 1.5, g]], u < 0.4 ? C[6] : C[5]);
    }
    const wing = bump(u, 0.05, 0.85), y = g - 20 * s * pop;
    if (wing > 0.3) {
      p.line(0, y, -9 * s * wing, y - 5 * wing, C[4], 1);
      p.line(0, y, 9 * s * wing, y - 5 * wing, C[4], 1);
    }
  },
  glacier(p, u, s, K) {
    const { C, g } = K, f = fade(u), pop = grow(u);
    p.glow(0, g - 8, 13 * s, C[3], 0.3 * f);
    const spikes = [[-7, 13, 5, -3], [6, 11, 5, 3], [0, 21, 6, 0]];
    for (const [x, h, w, lean] of spikes) crystal(p, x, g, h * s * pop, w, lean, C, { a: f < 0.35 ? f / 0.35 : 1, white: u < 0.14, glint: seg(u, 0.2, 0.6) });
  },
  thunder(p, u, s, K) {
    const { C, c, g } = K, f = fade(u), top = c - Math.min(TOP, 25 * s);
    const lit = u < 0.16 || (u > 0.26 && u < 0.5);
    p.glow(0, g - 4, 13 * s, C[3], 0.4 * f);
    if (lit) {
      const points = bolt(new Rng(K.seed + (u > 0.2 ? 7 : 0)), -4, top, 0, g, { jag: 0.26, depth: 3 });
      p.polyline(points.map(([x, y]) => [x - 1, y]), C[3], 1);
      p.polyline(points.map(([x, y]) => [x + 1, y]), C[4], 1);
      p.polyline(points, WHITE, 1);
      const [bx, by] = points[Math.floor(points.length / 2)];
      p.line(bx, by, bx + 8 * s, by + 5, C[4], 1);
    }
    band(p, 0, g, 3 + 12 * s * ease.outQuad(u), 0.2, 2, f, [C[2], C[3], C[4], C[6]]);
  },
  blade(p, u, s, K) {
    const { C, A, c } = K, f = fade(u), g = grow(u);
    p.glow(0, c, 12 * s, C[3], 0.3 * f);
    for (let i = 0; i < 3; i++) {
      const a = -2.75 + i * 0.6, d = 4 + 15 * (1 - g), length = 12 * s;
      const x = Math.cos(a) * (d + length), y = c + Math.sin(a) * (d + length);
      quad(p, x, y, a + Math.PI, length, 2.6, C[3], f);
      p.line(x, y, x - Math.cos(a) * length, y - Math.sin(a) * length, u < 0.5 ? WHITE : C[5], f);
      p.line(x - Math.sin(a) * 2.5, y + Math.cos(a) * 2.5, x + Math.sin(a) * 2.5, y - Math.cos(a) * 2.5, A[1], f);
    }
    if (u > 0.2) sparkle(p, 0, c, 7 * s * f, C[5], WHITE);
  },
  wolf(p, u, s, K) {
    const { C, c } = K, f = fade(u), gap = 6 * s * (1 - ease.inQuad(seg(u, 0, 0.3))), r = 9 * s;
    p.glow(0, c, 13 * s, C[3], 0.3 * f);
    for (const dir of [-1, 1]) {
      const y = c + dir * (gap + 1);
      p.arc(0, y + dir * 3, r, 2, dir < 0 ? Math.PI + 0.35 : 0.35, dir < 0 ? 2 * Math.PI - 0.35 : Math.PI - 0.35, C[3], f);
      for (let i = 0; i < 4; i++) {
        const x = (i - 1.5) * 3.8 * s, long = (i === 0 || i === 3 ? 6 : 4) * s;
        p.tri(x - 1.6, y - dir * (long - 1), x + 1.6, y - dir * (long - 1), x, y, u < 0.45 ? WHITE : C[5], f);
      }
    }
    if (u > 0.28) p.line(-r, c, r, c, WHITE, (v) => f * pulse(v));
  },
  ink(p, u, s, K) {
    const { C, A, c } = K, f = fade(u), sweep = 4.4 * grow(u), r = 11 * s, a0 = -2.4;
    p.arc(0, c, r, (v) => (3.4 - 2.2 * v) * (0.4 + 0.6 * f), a0, a0 + Math.max(0.2, sweep), (v, w) => (w > 0.6 ? C[5] : C[4]), 1);
    p.arc(0, c, r - 1, 1, a0, a0 + Math.max(0.2, sweep * 0.8), C[3], f);
    for (let i = 0; i < 4; i++) {
      const a = a0 + sweep + 0.2 * i, d = r + 2 + 6 * u * (1 + i * 0.3);
      p.dot(Math.cos(a) * d, c + Math.sin(a) * d, C[4], f);
    }
    if (u > 0.3) p.rect(6 * s, c + 5 * s, 4, 4, A[1], f);
    if (u > 0.3) p.rect(7 * s + 0.5, c + 5 * s + 1, 2, 2, A[2], f);
  },
  glass(p, u, s, K) {
    const { S, c } = K, f = fade(u), g = grow(u);
    p.glow(0, c, 12 * s, S[4], 0.25 * f);
    for (let i = 0; i < 7; i++) {
      const a = -Math.PI + i * 0.9 + 0.3, d = 3 + (8 + (i % 3) * 2.5) * s * g, size = (3 + (i % 2)) * s * (0.4 + 0.6 * f), spin = a + u * 3 * (i % 2 ? 1 : -1);
      const x = Math.cos(a) * d * (a > -1.2 && a < 1.2 ? 0.75 : 1), y = c + Math.sin(a) * d * 0.9 + 5 * u * u;
      const tip = [x + Math.cos(spin) * size, y + Math.sin(spin) * size], left = [x + Math.cos(spin + 2.3) * size, y + Math.sin(spin + 2.3) * size], right = [x + Math.cos(spin - 2.3) * size, y + Math.sin(spin - 2.3) * size];
      p.poly([tip, left, right], S[i % S.length]);
      p.line(tip[0], tip[1], left[0], left[1], WHITE, f);
    }
    if (u < 0.35) sparkle(p, 0, c, 6 * s * (1 - u / 0.35), WHITE, WHITE);
  },
  firework(p, u, s, K) {
    const { C, A, c } = K, f = fade(u), r = 15 * s * ease.outCubic(u);
    p.glow(0, c, 13 * s, A[2], 0.3 * f);
    for (let i = 0; i < 12; i++) {
      const a = (i / 12) * Math.PI * 2 + 0.2, reach = Math.cos(a) > 0.45 ? 0.8 : 1, ramp = i % 2 ? A : C;
      for (let k = 0; k < 5; k++) {
        const d = r * reach * (0.4 + 0.15 * k);
        p.dot(Math.cos(a) * d, c + Math.sin(a) * d + 5 * u * u, k === 4 ? (u < 0.5 ? WHITE : ramp[ramp.length - 1]) : ramp[ramp.length - 2], Math.min(1, f * (0.6 + 0.2 * k)));
      }
      if (u > 0.45 && i % 3 === 0) sparkle(p, Math.cos(a) * r * reach, c + Math.sin(a) * r * reach + 5 * u * u, 2 * f, WHITE, ramp[ramp.length - 1]);
    }
  },
  bone(p, u, s, K) {
    const { C, A, g } = K, f = fade(u), pop = grow(u), curl = seg(u, 0.25, 0.55);
    p.glow(0, g - 4, 12 * s, A[2], 0.35 * f);
    const fingers = [[-7, 13], [0, 18], [7, 13]];
    for (const [x, h] of fingers) {
      const mid = [x * (1 - 0.15 * curl), g - h * 0.55 * s * pop], tip = [x * (1 - 0.75 * curl), g - h * s * pop * (1 - 0.2 * curl)];
      if (g - tip[1] < 2) continue;
      p.beam(x, g, mid[0], mid[1], 2.4, C[5], f < 0.3 ? f / 0.3 : 1);
      p.beam(mid[0], mid[1], tip[0], tip[1], 2, C[5], f < 0.3 ? f / 0.3 : 1);
      p.dot(mid[0], mid[1], C[6], f);
      p.dot(tip[0], tip[1], WHITE, f);
      p.dot(x + 1, g - 1, C[3], f);
    }
  },
  bat(p, u, s, K) {
    const { C, c } = K, f = fade(u);
    p.glow(0, c, 12 * s, C[3], 0.3 * f);
    for (let i = 0; i < 2; i++) {
      const dir = i ? 1 : -1, v = ease.outQuad(u), x = dir * (12 - 22 * v) * s * 0.8 - 2, y = c - 8 * s + 16 * s * v * (i ? 0.9 : 0.6) - (i ? 0 : 3);
      const flap = 2.5 * Math.sin(u * 26 + i * 2);
      if (f <= 0.1) continue;
      for (const side of [-1, 1]) {
        p.poly([[x, y - 1], [x + side * 4, y - 4 - flap], [x + side * 8, y - 1 + flap], [x + side * 5, y + flap * 0.5], [x + side * 3, y + 2], [x, y + 2]], C[4], Math.min(1, f * 2.5));
        p.line(x, y - 1, x + side * 4, y - 4 - flap, C[5], 1);
        p.line(x + side * 4, y - 4 - flap, x + side * 8, y - 1 + flap, C[5], 1);
      }
      p.rect(x - 1, y - 2, 2, 4, C[6], 1);
    }
    cut(p, -10 * s, c + 7 * s, 10 * s, c - 7 * s, u - 0.15, WHITE, C[5], C[3]);
  },
  golem(p, u, s, K) {
    const { C, A, g } = K, f = fade(u);
    band(p, 0, g, 4 + 12 * s * ease.outQuad(u), 0.22, 2, f, [A[0], A[1], A[2], A[3]]);
    p.glow(0, g - 3, 12 * s, A[1], 0.35 * f);
    for (let i = 0; i < 5; i++) {
      const vx = (i - 2) * 26 * s, vy = (150 + (i % 2) * 50 + (i === 2 ? 40 : 0)) * s, v = u * SHORT * 1.3;
      const x = vx * v, y = Math.min(g, g - vy * v + 950 * 0.45 * v * v), r = 2.2 + (i % 3) * 0.7;
      if (f <= 0.05) continue;
      p.poly(ngon(x, y, r + 0.8, 5, i + u * 6), C[2], Math.min(1, f * 2.5));
      p.poly(ngon(x - 0.5, y - 0.5, r, 5, i + u * 6), i % 2 ? C[5] : C[4], Math.min(1, f * 2.5));
    }
    if (u < 0.5) p.line(-5, g, 5, g - 1, A[3], 1 - u / 0.5);
  },
  serpent(p, u, s, K) {
    const { C, A, B, c, g } = K, f = fade(u), head = seg(u, 0, 0.35), tail = seg(u, 0.5, 1) * 0.9;
    const from = [-13, g], over = [-12, c - 20 * s], to = [3, c + 1];
    band(p, -12, g, 3 + 6 * s * ease.outQuad(u), 0.25, 1.5, f, [B[0], B[1], B[2], B[3]]);
    for (let i = 0; i <= 16; i++) {
      const v = tail + (head - tail) * (i / 16);
      if (head - tail <= 0.02) break;
      const [x, y] = curve(from, over, to, v), w = 1 + 2.2 * (0.35 + 0.65 * (i / 16));
      p.circle(x, y, w / 2 + 0.4, C[3]);
      p.dot(x, y - w / 2, C[5], 1);
      if (i % 4 === 1) p.dot(x - 1, y - w / 2 - 1.5, A[1], 1);
    }
    if (head - tail > 0.1) {
      const [x, y] = curve(from, over, to, head);
      p.tri(x - 2, y - 3, x + 4, y + 1, x - 2, y + 3, C[4]);
      p.dot(x, y - 1, WHITE, 1);
    }
    p.glow(0, c, 11 * s, C[3], 0.3 * f);
  },
  samurai(p, u, s, K) {
    const { C, A, c } = K, f = fade(u), d = Math.min(17 * s, 22);
    cut(p, -d, c + 4, d, c - 4, u, WHITE, A[2], A[1]);
    cut(p, -d + 3, c + 6, d - 4, c - 1, u - 0.1, C[5], A[1], A[1]);
    if (u > 0.4 && u < 0.85) sparkle(p, d - 1, c - 4, 4 * s * bump(u, 0.4, 0.85), WHITE, A[3]);
    for (let i = 0; i < 4; i++) {
      const x = -d * 0.7 + i * d * 0.45, y = c + 2 - i + 14 * u * u + i * 2 * u;
      if (u > 0.15) p.dot(x + Math.sin(u * 9 + i) * 1.5, y, A[i % 2 ? 2 : 3], f);
    }
  },
  raven(p, u, s, K) {
    const { C, A, c } = K, f = fade(u), g = grow(u), length = 12 * s;
    p.glow(0, c, 12 * s, A[1], 0.3 * f);
    for (let i = 0; i < 5; i++) {
      const a = -Math.PI / 2 - 0.5 + (i - 2) * 0.5 * g, x = Math.cos(a), y = Math.sin(a), nx = -y * 1.8, ny = x * 1.8, d = length * (0.5 + 0.5 * g);
      p.poly([[x * 2, c + y * 2], [x * d * 0.6 + nx, c + y * d * 0.6 + ny], [x * d, c + y * d], [x * d * 0.6 - nx, c + y * d * 0.6 - ny]], C[i % 2 ? 4 : 3], f < 0.4 ? f / 0.4 : 1);
      p.line(x * 2, c + y * 2, x * d, c + y * d, A[u < 0.4 ? 3 : 2], f);
    }
    p.ring(0, c, 3 + 2 * s * g, 1.2, A[2], f);
    p.dot(0, c, WHITE, f);
  },
  vine(p, u, s, K) {
    const { C, A, c, g } = K, f = fade(u), pop = grow(u), open = 1 - seg(u, 0.3, 0.42) * 0.85;
    const from = [-8, g], over = [-9, c + 2], to = [-1, c + 1];
    p.glow(0, c, 11 * s, C[4], 0.25 * f);
    for (let i = 0; i <= 10; i++) {
      const [x, y] = curve(from, over, to, (i / 10) * pop);
      p.circle(x, y, 1.2, C[3], f < 0.4 ? f / 0.4 : 1);
      if (i === 4 || i === 7) p.tri(x, y, x - 4, y - 2, x - 3, y + 1, C[4], f);
    }
    const [bx, by] = curve(from, over, to, pop), reach = 11 * s * pop, lift = 7 * s * open;
    if (pop > 0.3 && f > 0.1) {
      p.poly([[bx, by], [bx + reach * 0.5, by - lift - 2], [bx + reach, by - lift * 0.5]], C[4]);
      p.poly([[bx, by], [bx + reach * 0.5, by + lift + 2], [bx + reach, by + lift * 0.5]], C[3]);
      p.poly([[bx + 1, by], [bx + reach * 0.5, by - lift], [bx + reach - 1, by - lift * 0.5 + 0.5]], A[2]);
      p.poly([[bx + 1, by], [bx + reach * 0.5, by + lift], [bx + reach - 1, by + lift * 0.5 - 0.5]], A[1]);
      for (let i = 0; i < 3; i++) {
        p.dot(bx + reach * (0.45 + i * 0.22), by - lift * (0.85 - i * 0.18), WHITE, 1);
        p.dot(bx + reach * (0.45 + i * 0.22), by + lift * (0.85 - i * 0.18), WHITE, 1);
      }
    }
  },
  guardian(p, u, s, K) {
    const { C, A, c, g } = K, f = fade(u), top = c - Math.min(TOP, 26 * s), drop = seg(u, 0, 0.18), tip = top + (g - top) * drop;
    p.glowLine(0, top, 0, tip, 5, C[3], 0.4 * f);
    p.beam(0, top, 0, tip, 1 + 2.6 * f, C[4], 1);
    p.line(0, top, 0, tip, WHITE, f);
    band(p, 0, g, 3 + 11 * s * ease.outQuad(u), 0.2, 2, f * drop, [C[1], C[2], C[3], C[5]]);
    const wing = bump(u, 0.12, 0.95), y = c - 8 * s;
    if (wing > 0.2) {
      for (const dir of [-1, 1]) {
        const reach = dir > 0 ? 10 : 12;
        p.polyline([[dir * 2, y + 2], [dir * reach * s * wing, y - 6 * wing], [dir * (reach - 3) * s * wing, y + 3]], C[4], 1);
        p.line(dir * 2, y + 4, dir * (reach - 4) * s * wing, y + 1, A[2], 1);
      }
    }
  },
};

const SPECS = {
  void: { name: "Void", from: "void-burst", ground: false, echo: [-4, 0, 0.7] },
  star: { name: "Star", from: "star-fall", ground: true, echo: [-8, 5, 0] },
  moth: { name: "Moth", from: "moth-scatter", ground: false, echo: [-3, 2, 1.1] },
  cog: { name: "Cog", from: "cog-break", ground: true, echo: [-9, 6, 0] },
  card: { name: "Card", from: "card-cut", ground: false, accent: "suit", echo: [-2, 1, 0.8] },
  bramble: { name: "Bramble", from: "bramble-bloom", ground: true, accent: "rose", echo: [-10, 0, 0] },
  rift: { name: "Rift", from: "rift-claw", ground: false, echo: [-7, 1, 0.25] },
  koi: { name: "Koi", from: "koi-splash", ground: true, accent: "scales", echo: [-9, 0, 0] },
  chord: { name: "Chord", from: "chord-ring", ground: false, accent: "second", third: "third", echo: [-5, -3, 0] },
  phoenix: { name: "Phoenix", from: "phoenix-rise", ground: true, echo: [-10, 0, 0] },
  glacier: { name: "Glacier", from: "glacier-bloom", ground: true, echo: [-11, 0, 0] },
  thunder: { name: "Thunder", from: "thunder-strike", ground: true, echo: [-9, 0, 0] },
  blade: { name: "Blade", from: "blade-ring", ground: false, accent: "hilt", echo: [-2, 0, -0.75] },
  wolf: { name: "Wolf", from: "wolf-bite", ground: false, echo: [-4, 0, 0.5] },
  ink: { name: "Ink", from: "ink-enso", ground: false, accent: "seal", echo: [-3, 1, 2.2] },
  glass: { name: "Glass", from: "glass-rose", ground: false, echo: [-3, -2, 0.45] },
  firework: { name: "Firework", from: "firework-bloom", ground: false, accent: "rose", echo: [-9, -6, 0.3] },
  bone: { name: "Bone", from: "bone-grasp", ground: true, accent: "soul", echo: [-11, 0, 0] },
  bat: { name: "Bat", from: "bat-swarm", ground: false, echo: [-5, 4, 0.5] },
  golem: { name: "Golem", from: "golem-fist", ground: true, accent: "rune", echo: [-9, 0, 0] },
  serpent: { name: "Serpent", from: "serpent-strike", ground: true, accent: "fin", third: "water", echo: [-8, 0, 0] },
  samurai: { name: "Samurai", from: "samurai-iai", ground: false, accent: "red", echo: [-2, 0, 0.6] },
  raven: { name: "Raven", from: "raven-omen", ground: false, accent: "rune", echo: [-6, 3, -0.6] },
  vine: { name: "Vine", from: "vine-maw", ground: true, accent: "mouth", echo: [-9, 0, 0] },
  guardian: { name: "Guardian", from: "guardian-descent", ground: true, accent: "sky", echo: [-10, 0, 0] },
};

export function flash(id, tier, source) {
  const spec = SPECS[id], double = tier > 1, duration = double ? DOUBLE : SHORT, options = source.options;
  const centre = spec.ground ? -CENTRE_ABOVE_GROUND : 0;
  const meta = {
    name: `${spec.name} ${double ? "double flash" : "flash"}`,
    desc: double
      ? `Second swing tier of the ${id} strike: the mark lands twice, a beat apart, with a ring and thrown sparks.`
      : `First swing tier of the ${id} strike: one short mark on the target that is gone before the next swing.`,
    tags: ["impact", "strike", double ? "double" : "short", "one-shot"],
    size: [104, 92],
    origin: [58, spec.ground ? 72 : 58],
    duration,
    loop: false,
    fps: 30,
    bg: "ground",
    options: {
      palette: options.palette,
      accent: options[spec.accent] ?? options.palette.slice(-4),
      third: options[spec.third] ?? options.palette.slice(-4),
      seed: 11,
    },
  };
  function create(o) {
    const C = fit(o.palette, 7), A = fit(o.accent, 4), B = fit(o.third, 4), S = o.palette.map(hex), R = new Rng(o.seed), scale = double ? 1.22 : 1;
    const K = { C, A, B, S, c: centre, g: centre + CENTRE_ABOVE_GROUND, seed: o.seed };
    const ps = new Particles({ rng: R, ground: () => K.g, sparks: [WHITE, C[5], C[4], C[3]], haze: C[3] }), lights = new Lights();
    const at = spec.ground ? K.g - 4 : K.c;
    lights.add(0, at, 16 * scale, C[3], 0.4, 0.16);
    const sparks = double ? 12 : 5, reach = double ? 150 : 100;
    for (let i = 0; i < sparks; i++) {
      const back = i % 3 !== 0, a = spec.ground ? -Math.PI / 2 + R.range(-1.2, 1.2) : back ? Math.PI + R.range(-1.1, 1.1) : R.range(-0.9, 0.9);
      const sp = R.range(50, back || spec.ground ? reach : reach * 0.6), delay = double && i % 2 ? ECHO_AT : 0;
      ps.spawn(P.SPARK, R.range(-2, 2), at + R.range(-3, 3), Math.cos(a) * sp, Math.sin(a) * sp - 20, R.range(0.07, double ? 0.2 : 0.13), undefined, undefined, delay);
    }
    if (double) for (let i = 0; i < 4; i++) ps.spawn(P.STAR, R.range(-15, 9), at + R.range(-14, 6), R.range(-8, 8), R.range(-18, -6), R.range(0.1, 0.16), C[5], 1, R.range(0.05, 0.12));
    let t = 0;
    return {
      update(dt) { t += dt; ps.update(dt); lights.update(dt); },
      draw(p) {
        if (t >= duration) return;
        const first = t / SHORT, second = (t - ECHO_AT) / (DOUBLE - ECHO_AT);
        if (double && second > 0 && second < 1) {
          const [dx, dy, angle] = spec.echo;
          moved(p, dx, dy, angle, () => MOTIFS[id](p, second, scale, K));
        }
        if (first < 1) MOTIFS[id](p, first, double ? 1.08 : 1, K);
        if (t < 0.07) sparkle(p, 0, at, 6 * scale * (1 - t / 0.07), C[5], WHITE);
        if (double) {
          const ring = seg(t, ECHO_AT, DOUBLE - 0.04);
          if (ring > 0 && ring < 1) band(p, spec.ground ? 0 : -2, K.g, 5 + 15 * ease.outQuad(ring), spec.ground ? 0.22 : 0.18, 1.5, 0.8 * (1 - ring), [C[2], C[3], C[4], C[5]]);
        }
        ps.draw(p); lights.draw(p);
      },
    };
  }
  return { meta, create };
}
