export default function event(fx) {
  const { seg, bump, ease, TAU, burst, sparkle, fly, WHITE } = fx;
  const K = fx.kit;
  const GOLD = K.ramp(["#6a3f0e", "#b67c1c", "#f0b43c", "#ffd878", "#fff6d0"]);
  const MAG = K.ramp(["#4f1a62", "#93359f", "#e86ad0", "#ffb4ec", "#ffe6fa"]);
  const PARTY = K.ramp(["#ffd878", "#6fe0ff", "#ffffff", "#ff8ad8", "#9dff9a", "#f0b43c"]);
  const LOOP = 8 / 12;
  const BIG = { mid: 26, R: 14.2, thick: 3.2, tieX: 9, tieRow: 15, tail: 19, wide: 2, stars: 7, top: 5 };
  const SMALL = { mid: 13, R: 8.2, thick: 2.2, tieX: 5, tieRow: 7, tail: 9, wide: 1, stars: 3, top: 2 };

  function hoop(p, g, s) {
    const R = g.R * s.scale;
    if (R < 1.5) return;
    const thick = Math.max(1.2, g.thick * Math.min(1, s.scale + 0.2)), inner = R - thick;
    const reach = Math.ceil(R);
    for (let row = -reach; row <= reach; row++) {
      for (let x = -reach - 1; x <= reach; x++) {
        const dx = x + 0.5, dy = row + 0.5, d = Math.hypot(dx, dy);
        if (d > R) continue;
        const a = Math.atan2(dy, dx);
        let color;
        if (d >= inner) {
          const turn = (((a / TAU - s.spin) % 1) + 1) % 1;
          const glint = Math.min(turn, 1 - turn) < 0.035 || Math.abs(turn - 0.5) < 0.035;
          if (glint) color = WHITE;
          else if (d > R - 0.9) color = dy < 0 && g.wide > 1 ? GOLD[0] : GOLD[1];
          else if (d < inner + 0.9 && g.wide > 1) color = dy > 0 ? GOLD[1] : GOLD[3];
          else color = dy > R * 0.35 && dx < 0 ? GOLD[4] : dy > 0 ? GOLD[3] : GOLD[2];
        } else {
          const v = (((a / TAU) * 3 + Math.log(d / s.scale + 1) * 0.95 - s.phase) % 1 + 1) % 1;
          if (d < inner * 0.16) color = s.hot ? WHITE : MAG[4];
          else if (s.hot) color = v < 0.45 ? WHITE : MAG[3];
          else color = v < 0.16 ? MAG[3] : v < 0.46 ? MAG[2] : v < 0.76 ? MAG[1] : MAG[0];
        }
        p.px(x, -(g.mid + row), color);
      }
    }
    p.glow(0, -g.mid, R * 1.5, MAG[2], 0.2 + (s.hot ? 0.25 : 0));
    const stars = Math.round(g.stars * Math.min(1, s.scale));
    for (let i = 0; i < stars; i++) {
      const dir = i % 2 ? 1 : -1, radius = inner * (0.3 + 0.55 * ((i * 0.37) % 1));
      const a = (i / g.stars) * TAU + s.phase * TAU * dir;
      const x = Math.cos(a) * radius, y = -g.mid - Math.sin(a) * radius, color = PARTY[i % PARTY.length];
      p.px(x, y, color);
      if (g.wide > 1 && (Math.floor(s.phase * 8) + i) % 3 === 0) {
        p.px(x - 1, y, color); p.px(x + 1, y, color); p.px(x, y - 1, color); p.px(x, y + 1, color);
        p.px(x, y, WHITE);
      }
    }
  }

  function ribbons(p, g, s) {
    if (s.ribbon <= 0) return;
    const length = Math.round(g.tail * Math.min(1, s.ribbon));
    for (const side of [1, -1]) {
      for (let k = 0; k <= length; k++) {
        const depth = k / g.tail;
        const row = g.tieRow - k * (g.tieRow / g.tail);
        const wave = Math.sin(k * 0.55 - s.phase * TAU + (side > 0 ? 0 : 1.7)) * (0.6 + 1.8 * depth) * s.wave;
        const x = side * (g.tieX + k * 0.22) + wave + (side > 0 ? 0 : -1);
        const crest = Math.cos(k * 0.55 - s.phase * TAU + (side > 0 ? 0 : 1.7)) > 0.3;
        const stripe = Math.floor(k / 3) % 2 === 0;
        for (let w = 0; w < g.wide; w++) p.px(x + w * side, -row, stripe ? (crest ? MAG[3] : MAG[2]) : crest ? GOLD[3] : GOLD[2]);
        if (g.wide > 1) p.px(x + 2 * side, -row, stripe ? MAG[1] : GOLD[1]);
      }
    }
  }

  function topper(p, g, s) {
    if (s.top <= 0) return;
    const y = -(g.mid + g.R + (g.wide > 1 ? 3 : 1)), arm = Math.round(g.top * Math.min(1.2, s.top) * (0.8 + 0.2 * K.pulse(s.phase, 2)) * 0.5);
    p.px(0, y, WHITE);
    p.px(-1, y, WHITE);
    for (let d = 1; d <= arm; d++) {
      const color = d === arm ? GOLD[2] : GOLD[4];
      p.px(d, y, color); p.px(-1 - d, y, color); p.px(0, y - d, color); p.px(-1, y - d, color);
      if (d === 1) { p.px(0, y + 1, color); p.px(-1, y + 1, color); }
    }
    p.glow(0, y, 3 + g.top, GOLD[3], 0.35);
  }

  function portal(p, g, s) {
    ribbons(p, g, s);
    hoop(p, g, s);
    topper(p, g, s);
  }

  function confetti(p, x, y, color, age, i, alpha = 1) {
    const flip = Math.floor(age * 22 + i) % 3;
    p.dot(x, y, color, alpha);
    if (flip === 0) p.dot(x + 1, y, color, alpha);
    else if (flip === 1) p.dot(x, y + 1, color, alpha);
  }

  function pop(p, x0, y0, time, at, count, seed, speed, lift, life) {
    burst(count, seed, (r, i) => {
      const age = time - at - r.range(0, 0.03), span = life * r.range(0.7, 1);
      if (age < 0 || age >= span) return;
      const a = r.range(0, TAU), v = r.range(0.35, 1) * speed;
      const [x, y] = fly(x0, y0, Math.cos(a) * v, Math.sin(a) * v - lift, age, 260, 3);
      confetti(p, x, y, PARTY[i % PARTY.length], age, i, age / span > 0.75 ? 0.5 : 1);
    });
  }

  function idleState(phase) {
    return { scale: 1, spin: phase * 0.5, phase, ribbon: 1, wave: 1, top: 1, hot: false };
  }

  function sprinkle(p, g, time, amount, seed) {
    fx.cycle(time, LOOP, amount, seed, (r) => r.range(0.4, 0.62), (age, life, r, i) => {
      const a = r.range(0, TAU), [x, y] = fly(Math.cos(a) * g.R, -g.mid + Math.sin(a) * g.R, Math.cos(a) * 10, -r.range(4, 16), age, 90, 1);
      if (y < 0) confetti(p, x, y, PARTY[i % PARTY.length], age, i, age / life > 0.7 ? 0.5 : 1);
    });
  }

  function idle(p, phase) {
    portal(p, BIG, idleState(phase));
    sprinkle(p, BIG, phase * LOOP, 7, 91);
  }

  function open(p, u, info) {
    if (u >= 1) return idle(p, 0);
    const time = u * info.seconds, g = BIG;
    if (u < 0.22) {
      const k = seg(u, 0, 0.22);
      sparkle(p, 0, -g.mid, 1 + 5 * k, WHITE, k > 0.5 ? MAG[3] : GOLD[3], 0.6);
      for (let i = 0; i < 4; i++) {
        const a = k * 5 + (i * TAU) / 4, d = 3 + 5 * k;
        p.px(Math.cos(a) * d, -g.mid + Math.sin(a) * d, PARTY[i]);
      }
    }
    const grow = ease.outBack(seg(u, 0.18, 0.56), 3.2);
    portal(p, g, {
      scale: u < 0.18 ? 0 : 0.12 + 0.88 * grow, spin: (u - 1) * 1.4, phase: (u - 1) * info.seconds * 1.5 - (1 - seg(u, 0.18, 1)) * 0.9, ribbon: ease.outBack(seg(u, 0.44, 0.72), 2),
      wave: 1 + 2.2 * (1 - seg(u, 0.5, 1)), top: ease.outBack(seg(u, 0.56, 0.76), 4), hot: u > 0.44 && u < 0.56,
    });
    if (u > 0.42 && u < 0.6) p.glow(0, -g.mid, 22, MAG[3], 0.4 * (1 - seg(u, 0.42, 0.6)));
    pop(p, 0, -g.mid, time, 0.44 * info.seconds, 34, 93, 150, 40, 0.3);
    if (u > 0.7) sprinkle(p, g, (u - 1) * info.seconds, 7, 91);
  }

  function close(p, u, info) {
    const time = u * info.seconds, g = BIG;
    const swell = bump(u, 0, 0.26);
    const shrink = ease.inBack(seg(u, 0.22, 0.62), 2.2);
    const scale = u < 0.22 ? 1 + 0.1 * swell : Math.max(0, 1 - shrink);
    portal(p, g, {
      scale, spin: u * u * 3, phase: time * 2 + u * u * 4, ribbon: 1 - ease.inQuad(seg(u, 0.1, 0.44)), wave: 1 + 2.5 * seg(u, 0, 0.3), top: 1 - seg(u, 0.14, 0.26),
      hot: u < 0.14 || (u > 0.5 && u < 0.62),
    });
    if (u > 0.54 && u < 0.8) {
      const k = seg(u, 0.54, 0.8);
      sparkle(p, 0, -g.mid, 6 * (1 - k), WHITE, MAG[3], 0.7 * (1 - k));
    }
    pop(p, 0, -g.mid, time, 0.58 * info.seconds, 22, 95, 110, 30, 0.16);
    burst(10, 96, (r, i) => {
      const age = time - (0.2 + r.range(0, 0.3)) * info.seconds, life = r.range(0.12, 0.2);
      if (age < 0 || age >= life) return;
      const a = r.range(0, TAU), d = (g.R + 4) * (1 - ease.inQuad(age / life));
      p.px(Math.cos(a + age * 9) * d, -g.mid + Math.sin(a + age * 9) * d, PARTY[i % PARTY.length]);
    });
  }

  function stepIn(p, u, info) {
    const time = u * info.seconds;
    burst(30, 97, (r, i) => {
      const born = r.range(0, 0.5) * info.seconds, life = r.range(0.14, 0.22), age = time - born;
      if (age < 0 || age >= life) return;
      const k = age / life, a = r.range(0, TAU) + k * TAU * 1.3, d = r.range(9, 15) * (1 - ease.inQuad(k));
      const x = Math.cos(a) * d + 3 * k, y = -15 + Math.sin(a) * d * 0.9 - 3 * k;
      confetti(p, x, y, PARTY[i % PARTY.length], age, i);
      const b = a - 0.35, e = d + 1;
      p.dot(Math.cos(b) * e + 3 * k, -15 + Math.sin(b) * e * 0.9 - 3 * k, MAG[2], 0.7);
    });
    const twist = seg(u, 0.05, 0.9);
    if (twist > 0 && twist < 1) {
      const radius = 14 * (1 - ease.inQuad(twist));
      for (let arm = 0; arm < 2; arm++) {
        const start = twist * TAU * 2 + arm * Math.PI;
        for (let j = 0; j < 14; j++) {
          const a = start - j * 0.11, d = radius + j * 0.25;
          p.dot(Math.cos(a) * d, -15 + Math.sin(a) * d * 0.9, j < 3 ? WHITE : j < 8 ? MAG[3] : MAG[2], 1 - j / 16);
        }
      }
      p.glow(0, -15, 16, MAG[2], 0.25 * Math.sin(twist * Math.PI));
    }
    if (u > 0.8 && u < 1) sparkle(p, 1, -17, 4 * (1 - seg(u, 0.8, 1)), WHITE, MAG[3], 0.5);
  }

  function arrive(p, u, info) {
    const time = u * info.seconds;
    if (u < 0.3) {
      const k = seg(u, 0, 0.3);
      sparkle(p, 0, -8, 7 * (1 - k), WHITE, GOLD[3], 0.8 * (1 - k));
      p.glow(0, -8, 20, MAG[3], 0.4 * (1 - k));
    }
    burst(30, 99, (r, i) => {
      const age = time - r.range(0, 0.03), life = r.range(0.24, 0.36);
      if (age < 0 || age >= life) return;
      const a = -Math.PI / 2 + r.range(-1.25, 1.25), v = r.range(60, 190);
      const [x, y] = fly(0, -8, Math.cos(a) * v, Math.sin(a) * v, age, 380, 3.2);
      if (y > 0) return;
      const color = PARTY[i % PARTY.length];
      if (i % 4 === 0) {
        p.px(x, y, WHITE); p.px(x - 1, y, color); p.px(x + 1, y, color); p.px(x, y - 1, color); p.px(x, y + 1, color);
      } else confetti(p, x, y, color, age, i, age / life > 0.75 ? 0.5 : 1);
    });
    for (const side of [1, -1]) {
      const reach = Math.round(18 * ease.outCubic(seg(u, 0, 0.4))), gone = Math.round(18 * ease.inQuad(seg(u, 0.45, 0.95)));
      for (let k = gone; k < reach; k++) {
        const t = k / 18, x = side * (2 + t * 11) + Math.sin(k * 0.9 + time * 20) * 1.4, y = -8 - t * 26 + t * t * 9;
        p.px(x, y, Math.floor(k / 3) % 2 ? MAG[2] : GOLD[3]);
        p.px(x + side, y, Math.floor(k / 3) % 2 ? MAG[3] : GOLD[2]);
      }
    }
    const ring = seg(u, 0.05, 0.7);
    if (ring > 0) K.floorRing(p, 0, -1, 2 + 11 * ease.outCubic(ring), 0.6 + 2.2 * ease.outCubic(ring), MAG[3], MAG[1], 1 - ring);
  }

  function ally(p, u, info, leaving) {
    const time = u * info.seconds, g = SMALL, up = u < 0.5;
    if (u < 0.12) sparkle(p, 0, -g.mid, 2, WHITE, GOLD[3], 0.4);
    portal(p, g, {
      scale: up ? (u < 0.08 ? 0 : 0.2 + 0.8 * ease.outBack(seg(u, 0.08, 0.34), 3)) : Math.max(0, 1 - ease.inBack(seg(u, 0.68, 0.92), 2)), spin: u * 1.5, phase: time * 2,
      ribbon: up ? ease.outBack(seg(u, 0.24, 0.42), 2) : 1 - seg(u, 0.6, 0.76), wave: 1, top: up ? seg(u, 0.3, 0.4) : 1 - seg(u, 0.6, 0.7), hot: u > 0.26 && u < 0.36,
    });
    pop(p, 0, -g.mid, time, 0.28 * info.seconds, 8, 101, 70, 20, 0.2);
    const from = 0.36 * info.seconds, span = 0.38 * info.seconds;
    burst(8, leaving ? 103 : 104, (r, i) => {
      const born = from + r.range(0, 0.5) * span, life = r.range(0.4, 0.6) * span, age = time - born;
      if (age < 0 || age >= life) return;
      const k = age / life, bx = -r.range(5, 10), by = -r.range(3, 16), gx = r.range(-2, 2), gy = -g.mid + r.range(-2, 2);
      const e = leaving ? ease.inQuad(k) : ease.outQuad(k);
      const x = leaving ? bx + (gx - bx) * e : gx + (bx - gx) * e, y = (leaving ? by + (gy - by) * e : gy + (by - gy) * e) - Math.sin(k * Math.PI) * 2;
      p.px(x, y, PARTY[i % PARTY.length]);
    });
    if (u > 0.86 && u < 1) sparkle(p, 0, -g.mid, 3 * (1 - seg(u, 0.86, 1)), WHITE, MAG[3], 0.4);
  }

  return {
    open,
    idle,
    close,
    stepIn,
    arrive,
    allyOut: (p, u, info) => ally(p, u, info, true),
    allyIn: (p, u, info) => ally(p, u, info, false),
  };
}
