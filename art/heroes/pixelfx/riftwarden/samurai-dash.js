import { fit, clamp, rand, hex, WHITE } from "../../core/index.js";

const FLY = 0.24;

export const meta = {
  name: "Samurai dash",
  desc: "A spirit swordsman at a dead run, drawn in place so the game moves him: straw hat low, hand on the hilt, a vermilion scarf and a red after-image strung out behind.",
  tags: ["spirit", "projectile", "summon", "one-shot"],
  size: [118, 46],
  origin: [98, 26],
  duration: FLY,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#08060c", "#1a1426", "#3a2e52", "#5e4e80", "#9a8ab0", "#e8dcc8", "#ffffff"],
    red: ["#5a0c14", "#c8202a", "#ff4a3a", "#ffb09a"],
    seed: 179,
  },
};

export function ronin(p, x, feet, o, C, V, a = 1) {
  const dir = o.dir ?? 1, crouch = o.crouch ?? 0, lean = o.lean ?? 0, stride = o.stride ?? 0, ghost = o.ghost ?? false;
  const ink = ghost ? V[0] : C[2], rim = ghost ? V[1] : V[2], cloth = ghost ? V[0] : C[3], edge = ghost ? V[1] : C[4];
  const hipY = feet - 10 + crouch * 3.5, hx = x - dir * lean * 1.5, sx = hx + dir * (2 + lean * 4), sy = hipY - 8.5 + crouch * 1.5 + Math.abs(lean) * 1.5;
  const fa = x + dir * (4 + stride * 7 + crouch * 3), fb = x - dir * (4 + stride * 7 + crouch * 2), la = feet - Math.max(0, stride) * 0, lb = feet - Math.max(0, stride) * 4;
  p.poly([[hx - 3.4, hipY - 1], [hx + 3.4, hipY - 1], [fa + dir * 1.5, la], [fa - dir * 4.5, la], [hx, hipY + 4], [fb + dir * 4, lb], [fb - dir * 1.5, lb]], cloth, a);
  p.line(fa - dir * 1, la, fa + dir * 3, la, ink, a);
  p.line(fb - dir * 2, lb, fb + dir * 1, lb, ink, a);
  p.line(hx + dir * 3.4, hipY - 1, fa + dir * 1.5, la, rim, a);
  p.line(hx - dir * 3.4, hipY - 1, fb - dir * 1.5, lb, edge, a);
  p.beam(hx, hipY, sx, sy, 6.6, ink, a);
  p.line(hx - dir * 3, hipY - 2, sx - dir * 3, sy, edge, a);
  p.line(hx + dir * 3, hipY - 1, sx + dir * 3, sy, rim, a);
  p.line(hx - 3, hipY, hx + 3, hipY, V[1], a);
  const hdx = sx + dir * (1 + lean * 1.5), hdy = sy - 4.5;
  p.circle(hdx, hdy, 2.6, ink, a);
  p.poly([[hdx - dir * 7.5, hdy - 0.2], [hdx + dir * 7.5, hdy + 0.6 + lean], [hdx + dir * 1, hdy - 5.2]], ghost ? V[0] : C[4], a);
  p.line(hdx - dir * 7.5, hdy - 0.2, hdx + dir * 7.5, hdy + 0.6 + lean, ghost ? V[1] : C[5], a);
  p.line(hdx - dir * 7.5, hdy - 0.2, hdx + dir * 1, hdy - 5.2, ghost ? V[1] : C[5], a);
  p.line(hdx + dir * 1, hdy - 5.2, hdx + dir * 7.5, hdy + 0.6 + lean, rim, a);
  if (!ghost) { p.dot(hdx + dir * 1.6, hdy + 1.2, V[2], a); p.light(hdx + dir * 1.6, hdy + 1.2, V[2], 0.8 * a); }
  const sa = o.sheath ?? 2.6, scx = hx - dir * 1, scy = hipY - 1;
  p.line(scx, scy, scx + dir * Math.cos(sa) * 11, scy + Math.sin(sa) * 11, ghost ? V[1] : C[5], a);
  p.line(scx, scy + 1, scx + dir * Math.cos(sa) * 11, scy + 1 + Math.sin(sa) * 11, ghost ? V[0] : C[1], a);
  const hand = o.hand ?? [dir * 3, -1], gx = hx + hand[0], gy = hipY + hand[1];
  p.beam(sx, sy + 1, gx, gy, 2.6, ink, a);
  p.dot(gx, gy, ghost ? V[2] : C[5], a);
  if ((o.blade ?? 0) > 0) {
    const ba = o.bladeAng ?? 0, L = 17 * o.blade, bx = gx + dir * Math.cos(ba) * L, by = gy + Math.sin(ba) * L;
    p.line(gx, gy + 1, bx, by + 1, C[4], a);
    p.line(gx, gy, bx, by, WHITE, a);
    p.dot(gx - dir * Math.cos(ba) * 2, gy - Math.sin(ba) * 2, V[1], a);
    if (!ghost) p.glowLine(gx, gy, bx, by, 3, V[2], 0.4 * a);
  }
  return [sx, sy - 2];
}

export function draw(p, t, o) {
  if (t >= FLY) return;
  const C = fit(o.palette, 7), V = fit(o.red, 4), u = clamp(t / FLY), run = Math.sin(u * Math.PI * 4), feet = 14;
  for (let i = 0; i < 7; i++) {
    const y = -12 + i * 4.2 + (rand(o.seed, i) - 0.5) * 3, L = 20 + rand(o.seed, i + 9) * 26, off = ((u * 2 + rand(o.seed, i + 3)) % 1) * 24;
    p.line(-22 - off, y, -22 - off - L, y, (v) => (v < 0.3 ? C[5] : C[4]), (v) => 0.6 * (1 - v));
  }
  ronin(p, -32, feet, { crouch: 1, lean: 1, stride: -run, ghost: true, hand: [3, -1] }, C, V, 1);
  const neck = ronin(p, -10, feet - Math.abs(run) * 1.2, { crouch: 1, lean: 1, stride: run, hand: [4, -1.5], sheath: 2.9 }, C, V, 1);
  for (let k = 0; k < 2; k++) {
    let px = neck[0] - 2, py = neck[1] + 3 + k;
    for (let i = 1; i <= 12; i++) {
      const d = i / 12, x = neck[0] - 2 - d * (34 - k * 8), y = neck[1] + 3 + k * 2 - d * 4 + Math.sin(d * 7 - u * 16 + k * 2) * 3.4 * d;
      p.beam(px, py, x, y, Math.max(1, 3 - d * 2.2), d < 0.5 ? V[2] : V[1], 1 - d * 0.5);
      px = x; py = y;
    }
    p.dot(px, py, V[3], 1);
  }
  p.glow(-10, 0, 20, V[1], 0.3);
  for (let i = 0; i < 9; i++) {
    const born = rand(o.seed, i + 30) * 0.8, age = u - born;
    if (age < 0 || age > 0.35) continue;
    const x = -14 - rand(o.seed, i + 40) * 20 - age * 110, y = feet - 1 - rand(o.seed, i + 50) * 5 - age * 12;
    p.disc(x, y, 1 + age * 6, C[4], 0.4 * (1 - age / 0.35));
  }
  const g = 0.5 + 0.5 * Math.sin(u * Math.PI * 2);
  p.dot(-5, 2, WHITE, g);
  p.light(-5, 2, V[3], g);
}
