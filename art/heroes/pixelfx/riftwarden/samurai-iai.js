import { Particles, Lights, P, PF, Rng, fit, clamp, lerp, seg, ease, sparkle, WHITE } from "../../core/index.js";
import { ronin } from "./samurai-dash.js";

const END = 0.84, CUT = 0.22, SPLIT = 0.36, SHEATHE = 0.58, FEET = 14, AX = -27, BX = 16, ANG = -0.2;

export const meta = {
  name: "Samurai iai",
  desc: "Under a red sun a swordsman stands out of ink; one draw and he is past the target, a paper screen slides apart along the cut, and the blade clicks home.",
  tags: ["spirit", "impact", "summon", "one-shot"],
  size: [116, 76],
  origin: [74, 44],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#08060c", "#1a1426", "#3a2e52", "#5e4e80", "#9a8ab0", "#e8dcc8", "#ffffff"],
    red: ["#5a0c14", "#c8202a", "#ff4a3a", "#ffb09a"],
    seed: 180,
  },
};

function screen(p, cx, cy, half, shift, C, a) {
  const w = 15, h = 17, ca = Math.cos(ANG), sa = Math.sin(ANG), ox = cx + ca * shift * half * -1 + -sa * shift * half * 0.6, oy = cy + sa * shift * half * -1 + ca * shift * half * 0.6 + half * shift * 0.5;
  p.each(ox - w - 1, oy - h - 1, ox + w + 1, oy + h + 1, (x, y, px, py) => {
    const lx = x - ox, ly = y - oy, side = -lx * sa + ly * ca;
    if (Math.abs(lx) > w + 1 || Math.abs(ly) > h + 1 || side * half < 0.4) return;
    const frame = Math.abs(lx) > w - 1 || Math.abs(ly) > h - 1, bar = Math.abs(((lx + w) % 7.5) - 3.75) > 3.2 || Math.abs(((ly + h) % 8.5) - 4.25) > 3.7;
    if (frame || bar) p.put(px, py, frame ? C[3] : C[4], a);
    else if (Math.abs(side) < 1.5) p.put(px, py, WHITE, a);
    else p.blendPx(px, py, C[5], 0.42 * a);
  });
}

export function create(o) {
  const C = fit(o.palette, 7), V = fit(o.red, 4), R = new Rng(o.seed);
  const ps = new Particles({ rng: R, ground: () => FEET, sparks: [WHITE, V[3], V[1]], haze: C[2] }), lights = new Lights();
  const petals = [];
  let t = 0, cut = false, split = false, clicked = false;
  lights.add(AX, 4, 22, V[1], 0.5, 0.3);
  for (let i = 0; i < 8; i++) ps.spawn(P.VAPOUR, AX + R.range(-8, 8), FEET - R.range(0, 6), R.range(-20, 20), R.range(-40, -10), R.range(0.2, 0.36), C[2], R.range(3, 6), R.range(0, 0.08));
  return {
    update(dt) {
      t += dt;
      if (!cut && t >= CUT) {
        cut = true;
        lights.add(0, 0, 36, V[2], 0.8, 0.2);
        for (let i = 0; i < 12; i++) ps.spawn(P.DUST, lerp(AX, BX, i / 11), FEET - 1, R.range(-20, 20), R.range(-30, -6), R.range(0.2, 0.34), C[4], R.range(2, 4), i * 0.004, PF.BACK);
      }
      if (!split && t >= SPLIT) {
        split = true;
        lights.add(0, 0, 38, V[2], 0.9, 0.24);
        for (let i = 0; i < 24; i++) {
          const along = R.range(-22, 18), side = i % 2 ? 1 : -1, sp = R.range(60, 200);
          ps.spawn(P.SPARK, Math.cos(ANG) * along, Math.sin(ANG) * along, -Math.sin(ANG) * side * sp + R.range(-40, 20), Math.cos(ANG) * side * sp - 20, R.range(0.2, 0.4));
        }
        for (let i = 0; i < 9; i++) petals.push({ x: R.range(-20, 16), y: R.range(-12, 8), vx: R.range(-40, 20), vy: R.range(-70, -20), ph: R.range(0, 6), born: t, life: R.range(0.3, 0.42) });
      }
      if (!clicked && t >= SHEATHE) {
        clicked = true;
        lights.add(BX + 2, 5, 16, V[3], 0.8, 0.14);
        for (let i = 0; i < 16; i++) ps.spawn(P.EMBER, BX + R.range(-5, 5), FEET - R.range(0, 22), R.range(-16, 10), R.range(-70, -20), R.range(0.1, 0.16), i % 3 ? C[4] : V[2], R.range(1, 2.2), R.range(0, 0.08));
      }
      ps.update(dt);
      lights.update(dt);
    },
    drawBack(p) {
      if (t >= END) return;
      const sun = ease.outBack(seg(t, 0, 0.14), 1.3) * (1 - ease.inCubic(seg(t, SHEATHE + 0.08, SHEATHE + 0.24)));
      if (sun > 0.02) {
        const r = 13 * sun, sx = -4, sy = -17;
        p.glow(sx, sy, r + 12, V[1], 0.45 * sun);
        p.circle(sx, sy, r, (d, g) => (d > 0.88 ? V[3] : Math.sin(g) * d > 0.25 ? V[1] : V[2]), Math.min(1, sun * 2));
        for (let i = 0; i < 3; i++) p.line(sx - r * 0.9, sy + 3 + i * 3.2, sx + r * 0.9, sy + 3 + i * 3.2, V[0], sun > 0.9 ? 0.6 : 0);
        p.arc(sx, sy, r + 3.5, 1, -2.9, -0.2, V[2], (v) => 0.8 * sun * Math.sin(v * Math.PI));
      }
      if (t >= CUT - 0.01 && t < SPLIT + 0.26) {
        const shift = ease.outCubic(seg(t, SPLIT, SPLIT + 0.24)) * 9, a = 1 - seg(t, SPLIT + 0.14, SPLIT + 0.26);
        screen(p, 0, -1, -1, shift, C, a);
        screen(p, 0, -1, 1, shift, C, a);
      }
      ps.draw(p, "back");
    },
    draw(p) {
      if (t >= END) return;
      if (t < CUT) {
        const k = seg(t, 0.03, 0.14), coil = seg(t, 0.14, CUT);
        for (let i = 0; i < 5; i++) p.disc(AX + Math.sin(i * 2.1 + t * 12) * 5, FEET - 2 - i * 4 * k, 5 - i * 0.6, C[1], 0.5 * (1 - k * 0.6));
        ronin(p, AX, FEET, { crouch: 0.4 + 0.6 * coil, lean: 0.3 + 0.5 * coil, hand: [3 + coil, -1.5], sheath: 2.7 }, C, V, k);
        if (coil > 0.5) { p.dot(AX + 5, 3, WHITE, 1); p.light(AX + 5, 3, V[3], 1); }
      } else {
        const rise = ease.inOutCubic(seg(t, SPLIT + 0.04, SHEATHE - 0.06)), put = ease.inOutQuad(seg(t, SHEATHE - 0.12, SHEATHE)), fade = 1 - seg(t, SHEATHE + 0.03, SHEATHE + 0.2);
        const ghostA = 1 - seg(t, CUT, CUT + 0.12);
        if (ghostA > 0) for (let g = 0; g < 2; g++) if (ghostA > 0.6 - g * 0.4) ronin(p, lerp(AX, BX, (g + 0.5) / 3), FEET, { crouch: 1, lean: 1, ghost: true, hand: [5, -3] }, C, V, 1);
        if (fade > 0) ronin(p, BX, FEET, { crouch: 1 - rise, lean: 0.9 * (1 - rise), hand: [lerp(6, 2, put), lerp(-6, -1, put)], blade: 1 - put, bladeAng: lerp(-0.75, 2.3, ease.inOutQuad(seg(t, SPLIT, SHEATHE - 0.04))), sheath: 2.5 }, C, V, fade);
      }
      if (t >= CUT && t < CUT + 0.15) {
        const f = 1 - (t - CUT) / 0.15, ca = Math.cos(ANG), sa = Math.sin(ANG);
        p.glowLine(ca * -38, sa * -38, ca * 26, sa * 26, 7, V[2], 0.7 * f);
        p.beam(ca * -40, sa * -40, ca * 27, sa * 27, 1 + 4.4 * f * f, (u, v) => (v < 0.5 ? WHITE : V[2]), (u) => Math.sin(u * Math.PI) * Math.min(1, f * 1.6));
        sparkle(p, 0, 0, 13 * f, V[3], WHITE, 0.4);
      }
      if (t >= CUT + 0.1 && t < SPLIT) p.line(Math.cos(ANG) * -30, Math.sin(ANG) * -30, Math.cos(ANG) * 22, Math.sin(ANG) * 22, V[3], 0.8);
      if (t >= SPLIT && t < SPLIT + 0.14) {
        const f = 1 - (t - SPLIT) / 0.14, ca = Math.cos(ANG), sa = Math.sin(ANG);
        p.beam(ca * -34, sa * -34, ca * 24, sa * 24, 1 + 6 * f, (u, v) => (v < 0.4 ? WHITE : v < 0.7 ? V[3] : V[2]), (u) => Math.sin(u * Math.PI) * f);
        for (const side of [-1, 1]) p.arc(0, 0, 8 + 18 * (1 - f * f), 2, side < 0 ? ANG + Math.PI * 0.2 : ANG - Math.PI * 0.8, side < 0 ? ANG + Math.PI * 0.8 : ANG - Math.PI * 0.2, f > 0.5 ? WHITE : V[2], f);
      }
      if (t >= SHEATHE && t < SHEATHE + 0.1) sparkle(p, BX + 1, 5, 7 * (1 - (t - SHEATHE) / 0.1), V[3], WHITE, 0.5);
      for (const f of petals) {
        const s = t - f.born;
        if (s > f.life) continue;
        const x = f.x + f.vx * s + Math.sin(s * 10 + f.ph) * 4, y = f.y + f.vy * s + 260 * s * s, tilt = Math.cos(s * 10 + f.ph), a = 1 - s / f.life;
        if (y > FEET) continue;
        p.line(x - tilt * 1.5, y - 1 + Math.abs(tilt), x + tilt * 1.5, y + 1 - Math.abs(tilt), V[2], a);
        p.dot(x, y, V[3], a);
      }
      ps.draw(p, "front");
      lights.draw(p);
    },
  };
}
