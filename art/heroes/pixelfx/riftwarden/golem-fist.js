import { Particles, Bits, Lights, P, PF, B, Rng, fit, clamp, lerp, seg, ease, band, sparkle, WHITE } from "../../core/index.js";
import { rock, block, glyph } from "./golem-rubble.js";

const END = 0.94, FLOOR = 0, BUILT = 0.3, DROP = 0.4, HIT = 0.46, BREAK = 0.66, TOP = -29, LOW = -10;

export const meta = {
  name: "Golem fist",
  desc: "Stones leap from a rune ring to build a fist above the target; its runes flare, it hangs a beat and comes down, then cracks along its seams and falls to rubble.",
  tags: ["earth", "impact", "summon", "one-shot"],
  size: [116, 66],
  origin: [72, 54],
  duration: END,
  loop: false,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#16131a", "#2c2830", "#454048", "#625a5e", "#857a76", "#a89c90", "#d0c6b4"],
    rune: ["#5e3a0c", "#e8a218", "#ffd23c", "#fff08a"],
    seed: 176,
  },
};

const PARTS = [
  [[-7, -14], [7, -14], [9, -8], [-9, -8]],
  [[-11, -8], [11, -8], [12.5, 2], [-12.5, 2]],
  [[-12, 2.6], [-6.8, 2.6], [-6.8, 8.6], [-8, 9.6], [-11, 9.6], [-12, 8.6]],
  [[-5.8, 2.6], [-0.6, 2.6], [-0.6, 9.2], [-1.8, 10.2], [-4.6, 10.2], [-5.8, 9.2]],
  [[0.4, 2.6], [5.6, 2.6], [5.6, 9.2], [4.4, 10.2], [1.6, 10.2], [0.4, 9.2]],
  [[6.6, 2.6], [11.8, 2.6], [11.8, 8.4], [10.6, 9.4], [7.8, 9.4], [6.6, 8.4]],
  [[-16, -3], [-12, -6], [-9.5, 3], [-12, 7.5], [-16.5, 3.5]],
];
const mid = (pts) => [pts.reduce((s, q) => s + q[0], 0) / pts.length, pts.reduce((s, q) => s + q[1], 0) / pts.length];

export function create(o) {
  const C = fit(o.palette, 7), N = fit(o.rune, 4), R = new Rng(o.seed);
  const ps = new Particles({ rng: R, ground: () => FLOOR, sparks: [WHITE, N[2], N[0]], haze: C[3] }), lights = new Lights();
  const bits = new Bits({ ground: () => FLOOR });
  const from = PARTS.map((_, i) => [-32 + i * 8.5 + R.range(-2, 2), 0.03 + ((i * 3) % 7) * 0.02, R.range(-3, 3), R.range(-3, 3)]);
  const fall = [];
  let t = 0, hit = false, broken = false;
  lights.add(0, 0, 30, N[1], 0.5, 0.3);
  const fistY = () => (t < DROP ? TOP - 3 * ease.outCubic(seg(t, BUILT, DROP)) : lerp(TOP - 3, LOW, ease.inCubic(seg(t, DROP, HIT))));
  return {
    update(dt) {
      t += dt;
      if (t < BUILT && R.chance(dt * 70)) ps.spawn(P.GRIT, R.range(-30, 22), -1, R.range(-20, 20), R.range(-120, -50), R.range(0.2, 0.4), C[R.int(3, 5)], 1);
      if (t < HIT && R.chance(dt * 30)) ps.spawn(P.EMBER, R.range(-28, 22), -1, R.range(-8, 8), R.range(-60, -30), R.range(0.2, 0.4), N[2], R.range(1, 1.8));
      if (!hit && t >= HIT) {
        hit = true;
        lights.add(0, -4, 34, N[1], 0.6, 0.22);
        for (let i = 0; i < 24; i++) {
          const dir = i % 2 ? 1 : -1, sp = R.range(80, 240) * (dir > 0 ? 0.4 : 1);
          ps.spawn(P.SPARK, dir * 6, -3, dir * sp, -R.range(20, 150), R.range(0.2, 0.45));
        }
        for (let i = 0; i < 14; i++) {
          const dir = i % 2 ? 1 : -1;
          ps.spawn(P.DUST, dir * R.range(4, 14), -R.range(0, 4), dir * R.range(40, 150) * (dir > 0 ? 0.5 : 1), R.range(-40, -5), R.range(0.28, 0.42), C[i % 3 ? 4 : 5], R.range(3.5, 6.5), 0, i % 3 ? 0 : PF.BACK);
        }
        for (let i = 0; i < 12; i++) bits.add({ kind: B.CHUNK, x: R.range(-14, 12), y: -2, vx: R.range(-140, 60), vy: R.range(-260, -110), a: R.range(0, 6), va: R.range(-16, 16), s: R.range(1.3, 2.4), ramp: [C[0], C[2], C[3], C[5], C[6]], g: 1, bounce: 0.4, life: R.range(0.3, 0.42) });
      }
      if (!broken && t >= BREAK) {
        broken = true;
        lights.add(0, -12, 30, N[1], 0.6, 0.2);
        PARTS.forEach((pts, i) => { const [px, py] = mid(pts); fall.push({ i, x: px, y: LOW + py, vx: px * R.range(7, 12) - 14, vy: R.range(-230, -130), a: 0, va: R.range(-10, 10) }); });
        for (let i = 0; i < 8; i++) ps.spawn(P.DUST, R.range(-12, 12), LOW + R.range(-8, 8), R.range(-40, 40), R.range(-30, 10), R.range(0.2, 0.26), C[4], R.range(3, 6));
        for (let i = 0; i < 10; i++) ps.spawn(P.EMBER, R.range(-10, 10), LOW + R.range(-8, 6), R.range(-20, 20), R.range(-80, -30), R.range(0.18, 0.26), N[2], R.range(1, 2));
      }
      for (const f of fall) { f.vy += 950 * dt; f.x += f.vx * dt; f.y += f.vy * dt; f.a += f.va * dt; if (f.y > -3) { f.y = -3; f.vy *= -0.3; f.vx *= 0.6; f.va *= 0.5; } }
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
      const ring = ease.outCubic(seg(t, 0, 0.1)) * (1 - seg(t, BREAK, BREAK + 0.14)), pulse = t > HIT ? 1 - seg(t, HIT, HIT + 0.2) : 0;
      if (ring > 0.02) {
        const rr = 26 * ring + pulse * 3;
        band(p, 0, 1, rr, 0.2, 1.6, ring, [N[0], N[1], N[2], N[3]], undefined, pulse > 0.6);
        band(p, 0, 1, rr * 0.62, 0.2, 1, ring * 0.8, [N[0], N[0], N[1], N[2]]);
        for (let i = 0; i < 8; i++) {
          const g = i * 0.785 + t * 1.6, x = Math.cos(g) * rr * 0.81, y = 1 + Math.sin(g) * rr * 0.81 * 0.2;
          p.dot(x, y, N[3], ring);
          p.dot(x + 1, y, N[2], ring);
          p.light(x, y, N[2], 0.8 * ring);
        }
      }
      if (t >= HIT && t < HIT + 0.24) {
        const k = seg(t, HIT, HIT + 0.24);
        band(p, 0, 1, 8 + 30 * ease.outQuart(k), 0.22, 3, 1 - k, [C[2], C[4], N[2], N[3]], undefined, k < 0.25);
      }
      const piece = (i, x, y, rot, sq, a, hot) => {
        const [mx, my] = mid(PARTS[i]), ca = Math.cos(rot), sa = Math.sin(rot);
        const pts = PARTS[i].map(([qx, qy]) => [x + ((qx - mx) * ca - (qy - my) * sa) * sq, y + ((qx - mx) * sa + (qy - my) * ca) / sq]);
        block(p, pts, x - 3, y - 3, C, a, hot);
      };
      if (t < BREAK) {
        const fy = fistY(), built = seg(t, BUILT - 0.03, BUILT + 0.05), flash = t >= BUILT && t < BUILT + 0.07 ? 1 : 0, glowK = built * (0.6 + 0.4 * Math.sin(t * 26)) + pulse;
        const squash = t >= HIT ? 1 + 0.14 * (1 - seg(t, HIT, HIT + 0.1)) : 1, crack = seg(t, HIT + 0.04, BREAK);
        if (t >= DROP && t < HIT + 0.03) for (let i = -2; i <= 2; i++) p.line(i * 5, fy - 15, i * 5, Math.max(-47, fy - 15 - 14 * seg(t, DROP, HIT)), i % 2 ? C[5] : N[2], 0.7);
        if (built > 0) p.glow(0, fy, 20, N[1], 0.22 * glowK);
        for (const i of [0, 6, 1, 2, 3, 4, 5]) {
          const [mx, my] = mid(PARTS[i]), [sx, delay, wob, spin] = from[i], k = ease.inOutCubic(seg(t, delay, delay + 0.17));
          if (k <= 0) { piece(i, sx, -3, spin, 1, seg(t, 0, 0.04), 0); continue; }
          const x = lerp(sx, mx * squash, k) + Math.sin(k * Math.PI) * wob * 3, y = lerp(-3, fy + my / squash, k) - Math.sin(k * Math.PI) * 5;
          piece(i, x, y, spin * (1 - k) * 2, k >= 1 ? squash : 1, 1, 0);
          if (k < 1) { p.glow(x, y, 9, N[1], 0.3); p.line(x, y + 4, x, y + 4 + 6 * Math.sin(k * Math.PI), N[2], 0.8); }
        }
        if (built > 0) {
          const hot = flash || pulse > 0.4 || crack > 0.5, seams = [[-9, -8, 9, -8], [-12.5, 2.3, 12.5, 2.3], [-6.3, 2.6, -6.3, 9], [-0.1, 2.6, -0.1, 9.6], [6.1, 2.6, 6.1, 9], [-11.4, -6, -9.6, 3]];
          for (let s = 0; s < seams.length; s++) {
            const [x0, y0, x1, y1] = seams[s];
            p.line(x0 * squash, fy + y0 / squash, x1 * squash, fy + y1 / squash, hot ? N[3] : s < 2 ? N[2] : N[1], built);
            p.glowLine(x0 * squash, fy + y0 / squash, x1 * squash, fy + y1 / squash, 3 + crack * 2, N[1], (0.3 + crack * 0.4 + flash * 0.4) * built);
          }
          glyph(p, -4, fy - 3 / squash, 2.2, 0, hot ? N[3] : N[2], N[3], built);
          glyph(p, 5, fy - 3.4 / squash, 2, 2, hot ? N[3] : N[2], N[3], built);
          glyph(p, 0, fy - 11 / squash, 1.4, 1, N[2], N[3], built);
          if (crack > 0) for (const [x0, y0, x1, y1] of [[-3, -7, -1, -2], [-1, -2, -4, 1], [7, -7, 8, -1], [-8, -1, -10, 2], [2, 3, 3, 8]].slice(0, Math.ceil(crack * 5))) p.line(x0 * squash, fy + y0 / squash, x1 * squash, fy + y1 / squash, N[3], 1);
        }
        if (flash) sparkle(p, 0, fy - 2, 11, N[2], WHITE, 0.35);
      }
      for (const f of fall) piece(f.i, f.x, f.y, f.a, 1, 1 - seg(t, BREAK + 0.12, BREAK + 0.26), 0);
      if (t >= HIT && t < HIT + 0.1) {
        const f = 1 - (t - HIT) / 0.1;
        p.ellipse(0, -1, 22 * (1.2 - f * 0.4), 3.5 * f, WHITE, 1);
        sparkle(p, -13, -2, 8 * f, N[2], WHITE);
        sparkle(p, 12, -2, 7 * f, N[2], WHITE);
      }
      bits.draw(p);
      ps.draw(p, "front");
      lights.draw(p);
    },
  };
}
