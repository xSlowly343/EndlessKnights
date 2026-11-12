import { Particles, Bits, Lights, P, PF, B, Rng, fit, clamp, seg, ease, band, sparkle, WHITE } from "../../core/index.js";
import { bone } from "./bone-crawl.js";

const END = 0.92, FLOOR = 0, RISE = 0.1, GRAB = 0.38, BREAK = 0.6, HX = 0, HY = -14;

export const meta = {
  name: "Bone grasp",
  desc: "The ground cracks on a seam of soul-light, a skeleton hand climbs out, spreads its fingers and clenches on the target, then falls apart into knuckles and dust.",
  tags: ["bone", "impact", "creature", "summon", "one-shot"],
  size: [112, 66],
  origin: [70, 54],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#1a1814", "#3a352c", "#5e5648", "#8a806c", "#b8ad96", "#e4dcc8", "#fffaf0"],
    soul: ["#0c2a22", "#1c6a4c", "#3cb47c", "#8cf0b4", "#e0fff0"],
    seed: 172,
  },
};

const CRACK = [[-30, 1], [-22, -0.5], [-15, 1.5], [-8, 0], [-2, 2], [5, 0.5], [12, 2], [19, 0], [25, 1]];

function hand(p, sink, curl, spread, C, a, shake) {
  const ox = HX - 1 + shake, oy = sink, fl = FLOOR + 0.5;
  bone(p, ox - 3, oy + 12, ox - 4.5, oy - 9, 3, C, a, fl);
  bone(p, ox + 2.5, oy + 12, ox + 3.5, oy - 9, 2.6, C, a, fl);
  for (let i = -1; i <= 1; i++) if (oy - 11 < fl) { p.circle(ox + i * 3.2, oy - 11.5, 2.4, C[1], a); p.circle(ox + i * 3.2, oy - 11.5, 1.7, i ? C[4] : C[5], a); }
  const tips = [];
  for (let i = 0; i < 4; i++) {
    const f = i - 1.5, kx = ox + f * 4.6, ky = oy - 22 + Math.abs(f) * 1.2;
    bone(p, ox + f * 2.2, oy - 13, kx, ky, 2, C, a, fl);
    let ang = -Math.PI / 2 + f * (0.2 + 0.16 * spread), x = kx, y = ky;
    const dir = f < 0 ? 1 : -1, len = [8.5 - Math.abs(f) * 0.8, 6.5 - Math.abs(f) * 0.5, 5];
    for (let j = 0; j < 3; j++) {
      ang += dir * curl * (j === 0 ? 0.75 : 1.05) * (0.8 + Math.abs(f) * 0.25) - dir * spread * 0.1 * j;
      const nx = x + Math.cos(ang) * len[j], ny = y + Math.sin(ang) * len[j];
      bone(p, x, y, nx, ny, 2 - j * 0.3, C, a, fl);
      x = nx; y = ny;
    }
    if (y < fl) p.tri(x - 1, y, x + 1, y, x + Math.cos(ang) * 3, y + Math.sin(ang) * 3, C[6], a);
    tips.push([x, y]);
  }
  let ang = Math.PI + 0.75 - spread * 0.25, x = ox - 6, y = oy - 14;
  for (let j = 0; j < 3; j++) {
    ang += curl * (j === 0 ? 0.5 : 0.95);
    const L = [6.5, 6, 4.5][j], nx = x + Math.cos(ang) * L, ny = y + Math.sin(ang) * L;
    bone(p, x, y, nx, ny, 2.2 - j * 0.3, C, a, fl);
    x = nx; y = ny;
  }
  return tips;
}

export function create(o) {
  const C = fit(o.palette, 7), S = fit(o.soul, 5), R = new Rng(o.seed);
  const ps = new Particles({ rng: R, ground: () => FLOOR, sparks: [WHITE, S[3], S[1]], haze: S[1] }), lights = new Lights();
  const bits = new Bits({ ground: () => FLOOR });
  const ramp = ["#1a1814", "#5e5648", "#8a806c", "#b8ad96", "#fffaf0"];
  const fall = [];
  let t = 0, grabbed = false, broken = false;
  lights.add(0, 0, 30, S[2], 0.6, 0.3);
  for (let i = 0; i < 14; i++) ps.spawn(P.GRIT, R.range(-26, 22), 0, R.range(-40, 40), R.range(-150, -50), R.range(0.3, 0.6), C[R.int(2, 4)], 1, R.range(0, 0.1));
  for (let i = 0; i < 6; i++) ps.spawn(P.DUST, R.range(-22, 18), -1, R.range(-30, 30), R.range(-20, -4), R.range(0.3, 0.5), C[3], R.range(3, 5), R.range(0, 0.1));
  return {
    update(dt) {
      t += dt;
      if (t < BREAK && R.chance(dt * 40)) ps.spawn(P.EMBER, R.range(-24, 20), -1, R.range(-10, 10), R.range(-70, -30), R.range(0.25, 0.5), S[3], R.range(1, 2));
      if (t > RISE && t < GRAB - 0.1 && R.chance(dt * 60)) ps.spawn(P.GRIT, R.range(-8, 8), -2, R.range(-70, 70), R.range(-120, -40), R.range(0.25, 0.45), C[R.int(2, 4)], 1);
      if (!grabbed && t >= GRAB) {
        grabbed = true;
        lights.add(HX, HY - 8, 30, S[3], 0.55, 0.2);
        for (let i = 0; i < 26; i++) {
          const a = R.range(0, Math.PI * 2), sp = R.range(80, 230);
          ps.spawn(P.SPARK, HX, HY - 8, Math.cos(a) * sp * (Math.cos(a) > 0 ? 0.5 : 1), Math.sin(a) * sp * 0.8 - 30, R.range(0.2, 0.4));
        }
        for (let i = 0; i < 7; i++) ps.spawn(P.VAPOUR, HX + R.range(-10, 10), HY - 8 + R.range(-8, 8), R.range(-50, 50), R.range(-50, 0), R.range(0.3, 0.5), S[2], R.range(4, 7), 0, PF.BACK);
      }
      if (!broken && t >= BREAK) {
        broken = true;
        lights.add(HX, -18, 30, S[2], 0.5, 0.25);
        for (let i = 0; i < 15; i++) {
          const y = -R.range(3, 36), x = HX + R.range(-9, 9) * (y < -16 ? 1 : 0.4);
          fall.push({ x, y, vx: R.range(-80, 40), vy: R.range(-150, -30), a: R.range(0, 6), va: R.range(-16, 16), L: y > -14 ? R.range(5, 8) : R.range(2.5, 4.5), w: y > -14 ? 2.8 : 1.8, life: R.range(0.22, 0.3) });
        }
        for (let i = 0; i < 8; i++) bits.add({ kind: B.CHUNK, x: HX + R.range(-8, 8), y: -R.range(4, 30), vx: R.range(-90, 50), vy: R.range(-140, -20), a: R.range(0, 6), va: R.range(-14, 14), s: R.range(1.2, 2), ramp, g: 0.9, bounce: 0.35, life: R.range(0.2, 0.3) });
        for (let i = 0; i < 9; i++) ps.spawn(P.DUST, HX + R.range(-10, 10), -R.range(4, 32), R.range(-30, 30), R.range(-20, 20), R.range(0.2, 0.3), C[4], R.range(3, 5.5));
        for (let i = 0; i < 12; i++) ps.spawn(P.EMBER, HX + R.range(-9, 9), -R.range(6, 32), R.range(-16, 16), R.range(-90, -40), R.range(0.2, 0.3), S[3], R.range(1, 2.2));
      }
      for (const f of fall) { f.vy += 950 * dt; f.x += f.vx * dt; f.y += f.vy * dt; f.a += f.va * dt; if (f.y > -1.5) { f.y = -1.5; f.vy *= -0.35; f.vx *= 0.6; f.va *= 0.4; } }
      ps.update(dt);
      bits.update(dt);
      lights.update(dt);
    },
    drawBack(p) {
      if (t >= END) return;
      ps.draw(p, "back");
    },
    draw(p) {
      if (t >= END) return;
      const open = ease.outCubic(seg(t, 0, 0.12)) * (1 - seg(t, BREAK + 0.06, END - 0.06)), glowK = open * (0.6 + 0.2 * Math.sin(t * 30));
      if (open > 0.02) {
        p.glowLine(-28 * open, 1, 24 * open, 1, 9, S[2], 0.7 * glowK);
        for (let i = 1; i < CRACK.length; i++) {
          const [ax, ay] = CRACK[i - 1], [bx, by] = CRACK[i];
          if (Math.abs((ax + bx) / 2) > 30 * open) continue;
          p.beam(ax, ay, bx, by, 1 + 2.2 * open * (1 - Math.abs((ax + bx) / 60)), (u, v) => (v < 0.4 ? S[4] : S[3]), 1);
          p.line(ax, ay + 1.6, bx, by + 1.6, C[0], open);
          if (i % 2) p.line(bx, by, bx + (i % 4 === 1 ? -4 : 3), by - 2.5 * open, S[2], open);
        }
        band(p, 0, 1, 10 + 24 * seg(t, 0, 0.3), 0.2, 2, (1 - seg(t, 0.05, 0.3)) * 0.9, [S[0], S[1], S[2], S[4]]);
      }
      if (t > RISE && t < BREAK) {
        const rise = ease.outBack(seg(t, RISE, 0.3), 1.3), sink = 40 * (1 - rise);
        const reach = seg(t, 0.26, GRAB - 0.03), snap = ease.outCubic(seg(t, GRAB - 0.03, GRAB + 0.02)), squeeze = seg(t, GRAB + 0.02, BREAK);
        const curl = 0.16 - 0.1 * reach + snap * (0.56 + 0.05 * Math.sin(squeeze * 40));
        const shake = t > GRAB ? Math.sin(t * 150) * (1 - squeeze * 0.5) : 0;
        p.glow(HX, HY - 6 + sink, 26, S[1], 0.5);
        const tips = hand(p, sink, curl, reach * (1 - snap), C, 1, shake);
        if (t > GRAB - 0.04) for (const [x, y] of tips) p.light(x, y, S[3], 0.8);
      }
      if (t >= GRAB && t < GRAB + 0.14) {
        const f = 1 - (t - GRAB) / 0.14;
        sparkle(p, HX, HY - 8, 10 * f, S[3], WHITE, 0.3);
        p.ring(HX, HY - 8, 8 + 20 * (1 - f * f), 2, f > 0.5 ? WHITE : S[3], f);
        for (let i = 0; i < 5; i++) {
          const a = -2.6 + i * 0.52;
          p.line(HX + Math.cos(a) * (10 + 10 * (1 - f)), HY - 8 + Math.sin(a) * (10 + 10 * (1 - f)), HX + Math.cos(a) * (16 + 14 * (1 - f)), HY - 8 + Math.sin(a) * (16 + 14 * (1 - f)), S[4], f);
        }
      }
      for (const f of fall) {
        const s = t - BREAK;
        if (s > f.life) continue;
        bone(p, f.x - Math.cos(f.a) * f.L, f.y - Math.sin(f.a) * f.L, f.x + Math.cos(f.a) * f.L, f.y + Math.sin(f.a) * f.L, f.w, C, s / f.life > 0.75 ? 0.5 : 1);
      }
      bits.draw(p);
      ps.draw(p, "front");
      lights.draw(p);
    },
  };
}
