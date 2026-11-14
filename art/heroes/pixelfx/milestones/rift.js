export default function rift(fx) {
  const { seg, bump, ease, burst, cycle, TAU, WHITE, hex } = fx;
  const K = fx.kit;
  const RIFT = { deep: hex("#0a0614"), dark: hex("#3a2080"), mid: hex("#8f5bff"), light: hex("#c3a6ff"), pale: hex("#e8daff"), flash: WHITE };
  const HOME = { deep: hex("#0e1630"), dark: hex("#274a9c"), mid: hex("#5b9bff"), light: hex("#a9ccff"), pale: hex("#e6f0ff"), flash: hex("#ffd27a") };
  const GOLD = K.ramp(["#6a3f0e", "#b67c1c", "#f0b43c", "#ffd27a", "#fff3c4"]);
  const STONE = K.ramp(["#1c2238", "#3a4670", "#6f82b0", "#a9b8d8"]);
  const STEP_W = 9;
  const STEP_H = 4;

  const exit = (p, u, from = 0.86) => (p.fade = 1 - 0.7 * seg(u, from, 1));

  function steps(t) {
    return (p, u, info) => {
      const time = u * info.seconds;
      exit(p, u);
      for (let i = 0; i < 3; i++) {
        const baseX = 9 + i * 7, baseY = -4 - i * 6;
        const build = ease.outCubic(seg(u, 0.2 + i * 0.1, 0.44 + i * 0.1)), lit = bump(u, 0.5 + i * 0.11, 0.72 + i * 0.11), dip = Math.round(bump(u, 0.5 + i * 0.11, 0.62 + i * 0.11));
        burst(8, 503 + i * 7, (g, n) => {
          const cellX = n % 4, cellY = Math.floor(n / 4);
          const fromX = baseX + cellX * 2 + g.range(-8, 8), fromY = baseY - g.range(6, 22);
          const hover = seg(u, 0, 0.2 + i * 0.1), shake = hover < 1 ? Math.sin(time * 40 + n * 2) * 0.8 : 0;
          const backX = fromX + (fromX - baseX) * 0.12 * hover, backY = fromY - 2 * hover;
          const x = backX + (baseX + cellX * 2 - backX) * build + shake * (1 - build), y = backY + (baseY + cellY * 2 - backY + (cellY ? -1 : 0)) * build + dip;
          if (build >= 1) return;
          const alpha = seg(u, i * 0.03 - 0.06, 0.1 + i * 0.03);
          p.dot(x, y, n % 3 ? t.mid : t.pale, alpha);
          p.dot(x + 1, y, t.dark, alpha);
          p.dot(x, y + 1, t.dark, alpha);
        });
        if (build >= 1) {
          for (let y = 0; y < STEP_H; y++)
            for (let x = 0; x < STEP_W; x++) {
              const top = y === 0, edge = x === 0 || x === STEP_W - 1 || y === STEP_H - 1;
              const tone = lit > 0.5 ? (top ? t.flash : t.pale) : top ? t.pale : edge ? t.dark : t.mid;
              p.px(baseX + x, baseY + y + dip, tone);
            }
          p.px(baseX + STEP_W, baseY + STEP_H - 1 + dip, t.deep);
          for (let x = 1; x < STEP_W; x += 2) p.dot(baseX + x, baseY + STEP_H + dip, t.deep, 0.6);
          if (lit > 0) {
            fx.sparkle(p, baseX + STEP_W / 2, baseY - 3, 1 + lit * 3, t.flash, t.pale, 0.7);
            p.glow(baseX + STEP_W / 2, baseY, 6 + lit * 4, t.mid, 0.5 * lit);
          }
        }
      }
      cycle(time, 0.4, 7, 521, () => 0.36, (age, life, g) => {
        if (u < 0.44) return;
        const k = age / life;
        p.dot(g.range(8, 32), -g.range(2, 26) + k * 5, k < 0.5 ? t.pale : t.dark, 1 - k);
      });
      const top = bump(u, 0.76, 0.98);
      if (top > 0) {
        for (let d = 0; d < 3; d++)
          for (const side of [-1, 1]) p.dot(28 + side * d - (side < 0 ? 1 : 0), -26 - top * 5 + d, d ? t.pale : t.flash, top);
        p.dot(27, -23 - top * 5, t.light, top);
        p.dot(28, -23 - top * 5, t.light, top);
      }
    };
  }

  function card(p, cx, top, lean, lit, squash = 0) {
    const tall = 11 - squash, half = 3 + (squash ? 1 : 0);
    for (let y = 0; y < tall; y++) {
      const shift = Math.round((lean * (tall - 1 - y)) / 10);
      for (let x = -half; x <= half; x++) {
        const edge = x === -half || x === half || y === 0 || y === tall - 1;
        const corner = (x === -half || x === half) && (y === 0 || y === tall - 1);
        if (corner) continue;
        const inner = Math.abs(x) + Math.abs(y - (tall - 1) / 2) * 0.8 < 2.4;
        p.px(cx + x + shift, top + y, edge ? GOLD[lit ? 3 : 1] : inner ? (lit ? GOLD[4] : GOLD[2]) : x < 0 && y < tall / 2 ? RIFT.light : RIFT.mid);
      }
    }
  }

  function wick(p, x, y, time, seed) {
    const sway = Math.round(Math.sin(time * 18 + seed) * 0.8);
    p.px(x, y, WHITE);
    p.px(x + sway, y - 1, GOLD[4]);
    p.px(x + sway, y - 2, GOLD[2]);
    p.glow(x, y - 1, 4, GOLD[2], 0.4);
  }

  function giftOffer(p, u, info) {
    const time = u * info.seconds;
    exit(p, u, 0.84);
    const brace = bump(u, 0, 0.2), fan = ease.outBack(seg(u, 0.16, 0.5)), settle = Math.round(bump(u, 0.5, 0.66) * 1.5);
    const top = -43 + settle;
    if (fan <= 0) {
      card(p, 0, top + 3 + Math.round(brace * 2), 0, false, Math.round(brace * 2));
      p.glow(0, top + 8, 5 + brace * 7, RIFT.mid, 0.3 + 0.4 * brace);
      return;
    }
    for (const side of [-1, 1]) card(p, side * Math.round(fan * 11), top + 3, side * Math.min(1, fan) * 2, seg(u, 0.5, 0.6) > 0 && Math.floor(seg(u, 0.5, 0.86) * 3) === (side < 0 ? 0 : 2));
    card(p, 0, top + 3 - Math.round(Math.min(fan, 1.1) * 3), 0, Math.floor(seg(u, 0.5, 0.86) * 3) === 1 && u > 0.5);
    if (fan >= 1) {
      wick(p, -11, top + 1, time, 0);
      wick(p, 0, top - 2, time, 2);
      wick(p, 11, top + 1, time, 4);
    }
    cycle(time, 0.4, 8, 541, () => 0.34, (age, life, g) => {
      if (u < 0.3) return;
      const k = age / life;
      p.dot(g.range(-15, 15), top + 16 + g.range(0, 5) - k * 7, k < 0.4 ? RIFT.pale : RIFT.mid, 1 - k);
    });
    p.glow(0, top + 8, 14, RIFT.mid, 0.25 * Math.min(1, fan));
  }

  function giftChosen(p, u, info) {
    const time = u * info.seconds;
    exit(p, u);
    const lift = bump(u, 0, 0.24), dive = ease.inCubic(seg(u, 0.2, 0.44)), sweep = seg(u, 0.42, 0.84), chest = -15;
    if (dive < 1) {
      const top = -40 - Math.round(lift * 3) + dive * (chest + 45 - 8);
      card(p, 0, Math.round(top), 0, true, dive > 0.6 ? 3 : 0);
      if (dive <= 0) wick(p, 0, Math.round(top) - 2, time, 2);
      for (let trail = 1; trail <= 4; trail++) if (dive > 0.1) p.dot(trail % 2 ? -2 : 2, top - trail * 3 * dive, trail < 2 ? RIFT.pale : RIFT.mid, dive * (1 - trail / 5));
      p.glow(0, top + 5, 7 + lift * 5, GOLD[2], 0.3 + 0.4 * lift);
    }
    const hit = bump(u, 0.4, 0.62);
    if (hit > 0) {
      fx.sparkle(p, 0, chest, 2 + hit * 5, WHITE, GOLD[3], 0.9);
      p.glow(0, chest, 10 + hit * 8, RIFT.mid, 0.6 * hit);
    }
    if (sweep > 0 && sweep < 1) {
      const y = -30 + ease.inOutQuad(sweep) * 30;
      K.floorRing(p, 0, y, 9 + Math.sin(sweep * Math.PI) * 2, 2.5, GOLD[4], RIFT.dark);
      K.floorRing(p, 0, y - 1, 8 + Math.sin(sweep * Math.PI) * 2, 2.5, RIFT.pale, RIFT.dark, 0.8);
      for (const x of [-7, -3, 2, 6]) for (let tail = 1; tail < 6; tail++) if ((x + tail + Math.floor(time * 30)) % 3 === 0) p.dot(x, y - tail * 2, tail < 3 ? RIFT.pale : RIFT.mid, 1 - tail / 6);
    }
    const land = seg(u, 0.78, 1);
    if (land > 0) K.floorRing(p, 0, 0, 9 + ease.outCubic(land) * 9, 2.5 + land * 1.5, RIFT.pale, RIFT.dark, 1 - land);
    burst(9, 547, (g) => {
      const a = g.range(-2.6, -0.5);
      K.spark(p, g.range(-6, 6), -1, Math.cos(a) * g.range(10, 22), Math.sin(a) * g.range(24, 46), time - 0.8 * info.seconds, g.range(0.14, 0.2), [WHITE, GOLD[4], RIFT.pale, RIFT.mid], 90, 0.6, 0.04);
    });
  }

  function gate(p, u, info) {
    const time = u * info.seconds;
    exit(p, u);
    const rattle = u < 0.2 ? Math.round(Math.sin(time * 70) * 1.1) : 0, swing = ease.outBack(seg(u, 0.2, 0.56)), open = Math.min(swing, 1.12);
    const cx = 20, tall = 30, half = 10, leaf = Math.max(2, Math.round(half * (1 - open * 0.78)));
    const glow = seg(u, 0.1, 0.5) * (1 - 0.5 * seg(u, 0.8, 1));
    if (open > 0) {
      const gap = half - leaf;
      for (let y = 1; y <= tall - 2; y++)
        for (let x = -gap; x < gap; x++) {
          const band = ((y + Math.floor(time * 24)) % 6) < 2;
          p.px(cx + x, -y, Math.abs(x + 0.5) < gap * 0.45 ? (band ? WHITE : GOLD[4]) : GOLD[3]);
        }
      for (let i = 0; i < 7; i++) {
        const a = -Math.PI / 2 + (i - 3) * 0.3, r0 = 15, r1 = 17 + Math.min(open, 1) * (i % 2 ? 4 : 7);
        p.line(cx + Math.cos(a) * r0, -14 + Math.sin(a) * r0, cx + Math.cos(a) * r1, -14 + Math.sin(a) * r1, GOLD[3], Math.min(open, 1) * (1 - 0.6 * seg(u, 0.8, 1)));
      }
    }
    for (const side of [-1, 1]) {
      const hinge = cx + side * half + rattle * (open > 0 ? 0 : 1);
      for (let y = 1; y <= tall - 3; y++)
        for (let d = 0; d < leaf; d++) {
          const x = side < 0 ? hinge + d : hinge - 1 - d;
          const edge = d === leaf - 1 || y === 1 || y === tall - 3, band = y === 8 || y === 20;
          p.px(x, -y, edge ? STONE[0] : band ? GOLD[open > 0 ? 3 : 1] : d === 0 ? STONE[1] : (d + y) % 4 === 0 ? STONE[1] : STONE[2]);
        }
      if (open <= 0) p.px(cx + (side < 0 ? -1 : 0), -14, glow > 0.5 ? WHITE : GOLD[2]);
      for (let y = 0; y <= tall; y++)
        for (let d = 0; d < 3; d++) {
          const x = cx + side * (half + 1) + (side < 0 ? -d - 1 : d);
          p.px(x, -y, d === 2 || y === 0 ? STONE[0] : y === tall ? STONE[3] : d === 0 ? STONE[3] : STONE[2]);
        }
      p.px(cx + side * (half + 2) - (side < 0 ? 1 : 0), -tall - 1, GOLD[3]);
    }
    for (let x = -half - 3; x < half + 3; x++) p.px(cx + x, -tall - 2 + (Math.abs(x + 0.5) > half ? 1 : 0), Math.abs(x + 0.5) > half + 1 ? STONE[0] : STONE[3]);
    if (open <= 0 && glow > 0) for (let y = 2; y < tall - 3; y++) if ((y + Math.floor(time * 30)) % 3) p.dot(cx - 1 + (y % 2), -y, y % 4 ? GOLD[4] : WHITE, glow);
    p.glow(cx, -14, 10 + glow * 12, GOLD[2], 0.5 * glow);
    burst(10, 557, (g, i) => {
      const side = i % 2 ? 1 : -1;
      K.spark(p, cx + side * (half - 1), -1, side * g.range(8, 26), -g.range(6, 22), time - 0.24 * info.seconds - g.range(0, 0.1), g.range(0.22, 0.4), [STONE[3], STONE[2], STONE[1]], 80, 1.5, 0);
    });
    cycle(time, 0.4, 7, 563, () => 0.38, (age, life, g) => {
      if (u < 0.5) return;
      const k = age / life;
      p.dot(cx + g.range(-5, 5) + k * g.range(-8, 8), -g.range(3, 24) - k * 6, k < 0.5 ? WHITE : GOLD[3], 1 - k);
    });
  }

  return { riftStep: steps(RIFT), featureOpen: steps(HOME), giftOffer, giftChosen, gate };
}
