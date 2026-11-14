export default function dungeon(fx) {
  const { seg, bump, ease, TAU, cycle, burst, sparkle, WHITE } = fx;
  const K = fx.kit;
  const STONE = K.ramp(["#1c1f29", "#3a3f4f", "#5a6072", "#858b9c"]);
  const STEP = K.ramp(["#262b38", "#3a4050", "#535a6c", "#6f778a"]);
  const PIT = K.ramp(["#0c1410", "#12301f"]);
  const GREEN = K.ramp(["#1c5a3a", "#2f9a5c", "#6ffd92", "#d2ffe0"]);
  const WOOD = K.ramp(["#2a2118", "#5a4630"]);
  const SMOKE = K.ramp(["#3c4a44", "#66786e"]);
  const DUST = K.ramp(["#5d6270", "#868b98"]);
  const FIRE = [GREEN[0], GREEN[1], GREEN[2], GREEN[3], WHITE];
  const MOTE = [WHITE, GREEN[3], GREEN[2], GREEN[1], GREEN[0]];
  const LOOP = 8 / 12;
  const BIG = { mid: 9, pitX: 15, pitY: 6.4, ringX: 21.5, ringY: 8.5, runes: 12, torch: 19, thick: 2, foot: 4, cup: 15, fireTall: 10, fireHalf: 2.6, steps: 3 };
  const SMALL = { mid: 4, pitX: 7, pitY: 2.6, ringX: 10.6, ringY: 3.8, runes: 8, torch: 10, thick: 1, foot: 2, cup: 9, fireTall: 6, fireHalf: 1.7, steps: 0 };

  function pit(p, g, s) {
    if (s.pit <= 0) return;
    const rx = g.pitX * (0.55 + 0.45 * Math.min(1, s.pit)) + (s.pit > 1 ? 1 : 0), ry = Math.max(0.6, g.pitY * s.pit);
    const rim = g.steps ? 0.7 : 0.55;
    for (let row = Math.floor(g.mid - ry); row <= Math.ceil(g.mid + ry); row++) {
      for (let x = -Math.ceil(rx); x < Math.ceil(rx); x++) {
        const dx = (x + 0.5) / rx, dy = (row + 0.5 - g.mid) / ry, e = dx * dx + dy * dy;
        if (e > 1) continue;
        let color;
        if (ry < 1.2) color = s.hot ? WHITE : GREEN[2];
        else if (e > rim) {
          const a = Math.atan2(dy, dx), joint = g.steps && (((a + TAU) * 9) / TAU) % 1 < 0.16;
          color = joint ? STONE[1] : dy > 0.2 ? STONE[3] : dy < -0.5 ? STONE[1] : STONE[2];
        } else {
          color = dy < -0.1 && !s.hot ? PIT[0] : PIT[1];
          if (s.hot) color = dy < 0 ? GREEN[1] : GREEN[0];
          if (g.steps && s.pit >= 1) {
            for (let i = 0; i < g.steps; i++) {
              const left = -10 + i * 5, top = g.mid + 4 - i * 3;
              if (x >= left && x < left + 7 && row <= top && row > top - 3) color = row > top - 1 ? STEP[3 - i] : STEP[Math.max(0, 1 - i)];
            }
          }
          if (e > rim - 0.14 && dy > 0) color = STONE[0];
        }
        p.px(x, -row, color);
      }
    }
    if (s.pit >= 1) p.glow(0, -g.mid + 1, g.pitX * 0.9, GREEN[1], 0.25 + 0.08 * K.pulse(s.phase) + (s.hot ? 0.3 : 0));
  }

  function runeAt(g, i) {
    const a = (i / g.runes) * TAU + TAU / (g.runes * 2) - Math.PI / 2;
    return [Math.cos(a) * g.ringX, g.mid - Math.sin(a) * g.ringY * -1, a];
  }

  function ring(p, g, s) {
    if (s.ring <= 0) return;
    const grow = s.swell ?? 0, half = g.runes / 2;
    const dots = Math.ceil(g.ringX * 7);
    for (let d = 0; d < dots; d++) {
      const turn = d / dots, away = Math.abs(((turn + 0.25) % 1) - 0.5) * 2;
      if (1 - away > s.ring) continue;
      if (s.out !== undefined && away < s.out * 1.1) continue;
      const a = turn * TAU, x = Math.cos(a) * (g.ringX + grow), row = g.mid + Math.sin(a) * (g.ringY + grow * 0.4);
      if (row < 0) continue;
      if (d % 2 === 0) p.px(x, -row, GREEN[0]);
    }
    for (let i = 0; i < g.runes; i++) {
      const order = Math.min(i, g.runes - 1 - i) / (half - 0.5);
      if (order > s.ring) continue;
      if (s.out !== undefined && (i * 5) % g.runes < s.out * g.runes) continue;
      const a = ((i + 0.5) / g.runes) * TAU - Math.PI / 2;
      const x = Math.floor(Math.cos(a) * (g.ringX + grow)), row = Math.round(g.mid + Math.sin(a) * (g.ringY + grow * 0.4));
      if (row < 0) continue;
      const head = ((i / g.runes - s.phase) % 1 + 1) % 1, other = (head + 0.5) % 1;
      const near = Math.min(head, 1 - head, other, 1 - other) * g.runes;
      const fresh = s.ring < 1 && s.ring - order < 0.2;
      const tone = s.flash || fresh ? WHITE : near < 0.6 ? GREEN[3] : near < 1.6 ? GREEN[2] : GREEN[1];
      const shape = i % 3;
      p.px(x, -row, tone);
      if (g.steps) {
        if (shape === 0) { p.px(x, -row - 1, tone); p.px(x + 1, -row, tone); }
        else if (shape === 1) { p.px(x - 1, -row - 1, tone); p.px(x + 1, -row - 1, tone); }
        else { p.px(x, -row - 1, tone); p.px(x, -row - 2, tone); }
      } else if (shape === 0) p.px(x, -row - 1, tone);
      if (tone !== GREEN[1]) p.glow(x + 0.5, -row, 4, GREEN[2], s.flash ? 0.45 : 0.25);
    }
  }

  function torch(p, g, side, s) {
    if (s.torch <= 0) return;
    const tall = g.cup - g.foot + 2, sunk = Math.round((1 - Math.min(1, s.torch)) * tall);
    const x0 = side > 0 ? g.torch : -g.torch - g.thick;
    for (let row = g.foot; row + sunk <= g.cup + 1; row++) {
      const part = row + sunk;
      for (let k = 0; k < g.thick; k++) {
        const cup = part >= g.cup;
        p.px(x0 + k, -row, cup ? (k === 0 ? STONE[3] : STONE[2]) : part % 4 === 1 ? WOOD[0] : k === 0 ? WOOD[1] : WOOD[0]);
      }
      if (part >= g.cup && g.thick > 1) {
        p.px(x0 - 1, -row, part === g.cup ? STONE[1] : STONE[2]);
        p.px(x0 + g.thick, -row, STONE[1]);
      }
    }
    if (s.flame > 0 && s.torch >= 1) {
      const cx = x0 + g.thick / 2;
      K.flame(p, cx, -(g.cup + 2), g.fireHalf * Math.min(1.2, 0.7 + 0.3 * s.flame), g.fireTall * s.flame, s.phase, FIRE, side > 0 ? 3 : 8, 2);
      p.glow(cx, -(g.cup + 2 + g.fireTall * 0.3), g.fireTall * 1.1, GREEN[2], 0.24 * Math.min(1.5, s.flame));
    }
  }

  function portal(p, g, s) {
    ring(p, g, s);
    pit(p, g, s);
    torch(p, g, -1, s);
    torch(p, g, 1, s);
  }

  function mist(p, g, time, amount, seed) {
    cycle(time, LOOP, amount, seed, (r) => r.range(0.45, 0.66), (age, life, r, i) => {
      const k = age / life, x = r.range(-g.pitX * 0.6, g.pitX * 0.6) + Math.sin(k * 3 + i) * 2;
      K.puff(p, x, -g.mid - k * r.range(5, 10), 1.2 + k * 2.2, i % 2 ? GREEN[1] : GREEN[0], 0.34 * Math.sin(k * Math.PI));
    });
    cycle(time, LOOP, amount, seed + 1, (r) => r.range(0.35, 0.6), (age, life, r) => {
      K.spark(p, r.range(-g.pitX * 0.7, g.pitX * 0.7), -g.mid - r.range(0, 3), r.range(-5, 5), -r.range(14, 26), age, life, MOTE, 0, 0, 0);
    });
  }

  function idleState(phase) {
    return { ring: 1, pit: 1, torch: 1, flame: 1, phase, hot: false, flash: false };
  }

  function idle(p, phase) {
    portal(p, BIG, idleState(phase));
    mist(p, BIG, phase * LOOP, 4, 71);
  }

  function rimDust(p, g, k, grow) {
    if (k <= 0) return;
    for (const side of [1, -1]) {
      K.puff(p, side * (g.pitX + 1 + grow * 3), -g.mid + 2 - grow * 2, 1.4 + grow * 2.2, DUST[1], 0.5 * k);
      K.puff(p, side * (g.pitX * 0.5), -g.mid - g.pitY - grow * 3, 1.2 + grow * 2, DUST[0], 0.4 * k);
    }
  }

  function open(p, u, info) {
    if (u >= 1) return idle(p, 0);
    const time = u * info.seconds, g = BIG;
    if (u < 0.2) sparkle(p, 0, -g.mid, 4 * bump(u, -0.06, 0.2), WHITE, GREEN[2], 0.6);
    const crack = seg(u, 0.26, 0.4);
    const mouth = u < 0.26 ? 0 : u < 0.4 ? 0.08 : 0.08 + 0.92 * ease.outBack(seg(u, 0.4, 0.62), 2.4);
    if (u >= 0.26 && u < 0.42) {
      const half = Math.round(g.pitX * 0.7 * ease.outCubic(crack));
      for (let x = -half; x < half; x++) p.px(x, -g.mid + (Math.abs(x) % 5 === 2 ? 1 : 0), x % 2 ? WHITE : GREEN[3]);
    }
    portal(p, g, {
      ring: seg(u, 0.06, 0.36), pit: mouth, torch: ease.outBack(seg(u, 0.52, 0.7), 2), flame: u < 0.7 ? 0 : u < 0.8 ? 1.5 : 1.5 - 0.5 * seg(u, 0.8, 1),
      phase: (u - 1) * info.seconds * 1.5, hot: u > 0.4 && u < 0.56, flash: false,
    });
    rimDust(p, g, bump(u, 0.4, 0.86), seg(u, 0.4, 0.86));
    burst(8, 73, (r) => K.spark(p, r.range(-12, 12), -g.mid, r.range(-40, 40), -r.range(30, 90), time - 0.42 * info.seconds, r.range(0.16, 0.3), [STONE[3], STONE[2], STONE[1]], 420, 0, 0));
    for (const side of [1, -1])
      if (u > 0.68 && u < 0.84) sparkle(p, side * (g.torch + 1), -(g.cup + 4), 4 * (1 - seg(u, 0.68, 0.84)), WHITE, GREEN[2], 0.5);
    if (u > 0.62) mist(p, g, (u - 1) * info.seconds, 4, 71);
  }

  function close(p, u, info) {
    const time = u * info.seconds, g = BIG;
    const pulse = bump(u, 0, 0.22);
    portal(p, g, {
      ring: 1, swell: Math.round(pulse * 1.4), out: seg(u, 0.5, 0.96), pit: u < 0.18 ? 1 + pulse * 0.3 : 1 - ease.inCubic(seg(u, 0.18, 0.46)),
      torch: 1 - ease.inBack(seg(u, 0.62, 0.9), 1.5), flame: u < 0.2 ? 1 + 0.6 * pulse : 1 - seg(u, 0.36, 0.5), phase: time * 2, hot: u > 0.36 && u < 0.46, flash: u < 0.16,
    });
    rimDust(p, g, bump(u, 0.4, 0.9), seg(u, 0.4, 0.9));
    if (u > 0.44 && u < 0.6) {
      const k = seg(u, 0.44, 0.6), half = Math.round(g.pitX * 0.6 * (1 - k));
      for (let x = -half; x < half; x++) p.px(x, -g.mid, k < 0.4 ? WHITE : GREEN[2]);
    }
    const smoke = seg(u, 0.46, 1);
    if (smoke > 0 && smoke < 1)
      for (const side of [1, -1]) {
        const fade = Math.sin(smoke * Math.PI) * (1 - seg(u, 0.88, 1));
        K.puff(p, side * (g.torch + 1) + Math.sin(smoke * 5 + side) * 1.5, -(g.cup + 3) * (1 - seg(u, 0.62, 0.9)) - smoke * 8, 1.4 + smoke * 2.4, SMOKE[1], 0.5 * fade);
      }
    burst(8, 75, (r) => K.spark(p, r.range(-14, 14), -g.mid - r.range(-2, 2), r.range(-10, 10), -r.range(16, 40), time - 0.44 * info.seconds, r.range(0.12, 0.22), MOTE, 0, 0, 0));
  }

  function stepIn(p, u, info) {
    const time = u * info.seconds;
    const openK = ease.outCubic(seg(u, 0, 0.3)), shut = ease.inCubic(seg(u, 0.7, 1));
    const rx = 10 * openK * (1 - shut), ry = 3 * openK * (1 - shut);
    if (rx > 1) {
      for (let row = Math.floor(3 - ry); row <= 3 + ry; row++)
        for (let x = -Math.ceil(rx); x < rx; x++) {
          const e = ((x + 0.5) / rx) ** 2 + ((row + 0.5 - 3) / Math.max(ry, 0.5)) ** 2;
          if (e > 1) continue;
          p.px(x, -row, e > 0.72 ? (row > 3 ? GREEN[2] : GREEN[1]) : row > 3 ? PIT[1] : PIT[0]);
        }
      p.glow(0, -3, 14, GREEN[1], 0.35 * openK * (1 - shut));
    } else if (u > 0.7 && u < 1) sparkle(p, 0, -3, 3 * (1 - seg(u, 0.86, 1)), WHITE, GREEN[2], 0.4);
    for (let i = 0; i < 4; i++) {
      const at = 0.14 + i * 0.13, k = seg(u, at, at + 0.3);
      if (k <= 0 || k >= 1) continue;
      const row = 26 - i * 6 - k * 5, half = 6 - i;
      for (let x = -half; x < half; x++) {
        p.dot(x, -row, k < 0.3 ? GREEN[3] : GREEN[2], Math.sin(k * Math.PI));
        p.dot(x, -row + 1, GREEN[0], Math.sin(k * Math.PI) * 0.8);
      }
    }
    burst(30, 77, (r) => {
      const born = r.range(0, 0.7) * info.seconds;
      K.spark(p, r.range(-8, 8), -r.range(12, 32), r.range(-4, 4), r.range(30, 70), time - born, r.range(0.12, 0.22), MOTE, 60, 0, 0.04);
    });
  }

  function arrive(p, u, info) {
    const time = u * info.seconds;
    const shut = ease.inCubic(seg(u, 0.3, 0.8));
    const rx = 9 * (1 - shut), ry = 2.6 * (1 - shut);
    if (rx > 1)
      for (let row = Math.floor(3 - ry); row <= 3 + ry; row++)
        for (let x = -Math.ceil(rx); x < rx; x++) {
          const e = ((x + 0.5) / rx) ** 2 + ((row + 0.5 - 3) / Math.max(ry, 0.5)) ** 2;
          if (e <= 1) p.px(x, -row, e > 0.7 ? STONE[2] : row > 3 ? PIT[1] : PIT[0]);
        }
    const count = 10, gone = seg(u, 0.12, 0.92) * count;
    for (let i = 0; i < count; i++) {
      const a = (i / count) * TAU - Math.PI / 2, x = Math.floor(Math.cos(a) * 13), row = Math.round(3 + Math.sin(a) * 3.6);
      if (row < 0) continue;
      const left = i - gone;
      if (left < -1) continue;
      const tone = left < 0 ? GREEN[0] : left < 1 ? WHITE : u < 0.12 ? GREEN[3] : GREEN[2];
      p.px(x, -row, tone);
      if (left >= 0) {
        p.px(x, -row - 1, tone);
        p.glow(x + 0.5, -row, 4, GREEN[2], left < 1 ? 0.5 : 0.25);
      } else K.puff(p, x, -row - 2 - (-left) * 3, 1.2, SMOKE[1], 0.4 * (1 + left));
    }
    if (u < 0.3) p.glow(0, -6, 18, GREEN[2], 0.35 * (1 - seg(u, 0, 0.3)));
    burst(24, 79, (r) => {
      const born = r.range(0, 0.4) * info.seconds;
      K.spark(p, r.range(-7, 7), -r.range(1, 6), r.range(-8, 8), -r.range(50, 120), time - born, r.range(0.14, 0.28), MOTE, 60, 2.5, 0.04);
    });
    const puff = seg(u, 0.05, 0.6);
    if (puff > 0 && puff < 1) {
      K.puff(p, -9 - puff * 3, -3 - puff * 3, 1.4 + puff * 2.2, DUST[1], 0.5 * (1 - puff));
      K.puff(p, 9 + puff * 3, -3 - puff * 3, 1.4 + puff * 2.2, DUST[1], 0.5 * (1 - puff));
    }
  }

  function ally(p, u, info, leaving) {
    const time = u * info.seconds, g = SMALL, up = u < 0.5;
    if (u < 0.1) p.dot(0, -g.mid, WHITE, 1);
    portal(p, g, {
      ring: up ? seg(u, 0.03, 0.22) : 1, out: up ? undefined : seg(u, 0.7, 0.98), pit: up ? ease.outBack(seg(u, 0.16, 0.34), 2) : 1 - ease.inCubic(seg(u, 0.64, 0.8)),
      torch: up ? ease.outBack(seg(u, 0.2, 0.34), 2) : 1 - ease.inQuad(seg(u, 0.78, 0.94)), flame: up ? 1.3 * seg(u, 0.32, 0.42) : 1 - seg(u, 0.62, 0.76), phase: time * 2,
      hot: u > 0.28 && u < 0.4, flash: false,
    });
    const from = 0.36 * info.seconds, span = 0.38 * info.seconds;
    burst(8, leaving ? 81 : 82, (r) => {
      const born = from + r.range(0, 0.5) * span, life = r.range(0.4, 0.6) * span, age = time - born;
      if (age < 0 || age >= life) return;
      const k = age / life, bx = -r.range(5, 10), by = -r.range(5, 16), gx = r.range(-3, 3), gy = -g.mid;
      const e = leaving ? ease.inQuad(k) : ease.outQuad(k);
      const x = leaving ? bx + (gx - bx) * e : gx + (bx - gx) * e, y = leaving ? by + (gy - by) * e : gy + (by - gy) * e;
      p.px(x, y, k < 0.4 ? GREEN[3] : GREEN[2]);
    });
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
