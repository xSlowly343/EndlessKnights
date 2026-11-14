export default function badges(fx) {
  const { seg, bump, ease, burst, TAU, WHITE, hex } = fx;
  const K = fx.kit;
  const GOLD = K.ramp(["#6a3f0e", "#b67c1c", "#f0b43c", "#ffd27a", "#fff3c4"]);
  const PAPER = K.ramp(["#8a7a5c", "#d8c8a0", "#f4ead0", "#fffaf0"]);
  const WAX = K.ramp(["#7a1f1a", "#c8382e", "#ff7a6a"]);
  const CHECK = K.ramp(["#1f6a3c", "#4fc277", "#b8f5cf"]);
  const RIBBONS = { achievement: K.ramp(["#8a2a22", "#e0574a", "#ff9a8a"]), collection: K.ramp(["#274a9c", "#5b9bff", "#a9ccff"]), campTier: K.ramp(["#1f6a3c", "#4fc277", "#b8f5cf"]) };
  const EMBLEMS = {
    achievement: ["..x..", ".xxx.", "xxxxx", ".xxx.", ".x.x."],
    collection: ["xxxxx", "x...x", "x.x.x", "x...x", "xxxxx"],
    campTier: ["..x..", ".xxx.", ".x.x.", "xx.xx", "xxxxx"]
  };
  const CONFETTI = [hex("#e0574a"), hex("#5b9bff"), hex("#4fc277"), hex("#f0b43c"), hex("#b07cff"), hex("#3fe0d0")];

  const exit = (p, u, from = 0.86) => (p.fade = 1 - 0.7 * seg(u, from, 1));

  function medal(kind) {
    const ribbon = RIBBONS[kind], emblem = EMBLEMS[kind];
    return (p, u, info) => {
      const time = u * info.seconds;
      exit(p, u, 0.88);
      const call = bump(u, -0.08, 0.16), fall = seg(u, 0.1, 0.56), rest = 3, y = Math.round(-22 + ease.outBounce(fall) * (22 + rest));
      const landed = fall > 0.36, squash = bump(u, 0.26, 0.34) > 0.3 ? 1 : 0, glint = seg(u, 0.56, 0.86);
      if (call > 0) {
        fx.sparkle(p, 0, -9, 1 + call * 2, WHITE, GOLD[3], 0.6);
        for (let d = 0; d < 4; d++) p.dot(d % 2 ? -1 : 0, -11 + d * 2 + call * 2, GOLD[4], call);
      }
      if (fall <= 0) return;
      for (const side of [-1, 1])
        for (let d = 0; d < 6; d++) {
          const x = side * (1 + Math.round(d * 0.5)) - (side < 0 ? 1 : 0), ry = y - 5 - d + (landed ? 0 : -Math.round((1 - fall) * 2));
          p.px(x, ry, d === 5 ? ribbon[0] : ribbon[1]);
          p.px(x + side, ry, d > 3 ? ribbon[0] : ribbon[2]);
        }
      const radius = 5, wide = radius + squash, tall = radius - squash;
      for (let dy = -tall; dy <= tall; dy++)
        for (let dx = -wide; dx <= wide; dx++) {
          const d = Math.hypot(dx / (wide + 0.4), dy / (tall + 0.4));
          if (d > 1) continue;
          const angle = Math.atan2(dy, dx), rim = d > 0.72;
          const lit = glint > 0 && glint < 1 && Math.abs(((angle - glint * TAU * 1.2 + Math.PI * 5) % TAU) - Math.PI) < 0.55;
          p.px(dx, y + dy + squash, rim ? (lit ? WHITE : dy < 0 ? GOLD[3] : GOLD[1]) : dx + dy < -2 ? GOLD[4] : GOLD[2]);
        }
      if (!squash) emblem.forEach((row, ey) => [...row].forEach((ch, ex) => ch === "x" && p.px(ex - 2, y + ey - 2, ey < 2 ? GOLD[0] : GOLD[1])));
      p.glow(0, y, 8, GOLD[2], 0.3 + 0.3 * bump(u, 0.26, 0.5));
      burst(6, 601, (g, i) => {
        const a = -Math.PI / 2 + (i - 2.5) * 0.5, speed = g.range(30, 44);
        K.spark(p, 0, rest + 3, Math.cos(a) * speed, Math.sin(a) * speed, time - 0.28 * info.seconds, g.range(0.34, 0.46), [CONFETTI[i], CONFETTI[i], CONFETTI[(i + 3) % 6]], 150, 1.4, 0.04);
      });
      const shock = seg(u, 0.27, 0.5);
      if (shock > 0 && shock < 1) K.floorRing(p, 0, rest + 6, 3 + shock * 7, 1, GOLD[4], GOLD[1], 1 - shock);
    };
  }

  function scroll(sealed) {
    return (p, u, info) => {
      const time = u * info.seconds;
      exit(p, u, 0.88);
      const wiggle = u < 0.16 ? Math.round(Math.sin(time * 60) * 1.1) : 0, unroll = ease.outBack(seg(u, 0.14, 0.48)), tick = seg(u, 0.46, sealed ? 0.64 : 0.74);
      const hop = Math.round(bump(u, 0.48, 0.62) * 1.5), top = -8 - hop, length = Math.round(Math.min(unroll, 1.1) * 14);
      const roller = (y, shade) => {
        for (let x = -8; x <= 7; x++) p.px(x + wiggle, y, x === -8 || x === 7 ? GOLD[1] : shade);
        p.px(-9 + wiggle, y, GOLD[3]);
        p.px(8 + wiggle, y, GOLD[3]);
      };
      for (let y = 1; y < length; y++)
        for (let x = -6; x <= 5; x++) {
          const edge = x === -6 || x === 5, line = y % 3 === 0 && x > -5 && x < 4 - (y % 2) * 3 && y < length - 1 && y < 11;
          p.px(x, top + y, edge ? PAPER[0] : line ? PAPER[1] : PAPER[2]);
        }
      roller(top, PAPER[3]);
      roller(top - 1, PAPER[1]);
      roller(top + Math.max(1, length), PAPER[3]);
      roller(top + Math.max(1, length) + 1, PAPER[0]);
      if (unroll <= 0) p.glow(0, top, 5, GOLD[2], 0.3);
      if (tick > 0) {
        const path = [[-3, 7], [-2, 8], [-1, 9], [0, 8], [1, 7], [2, 6], [3, 5], [4, 4]], shown = Math.ceil(tick * path.length);
        for (let i = 0; i < shown; i++) {
          const [x, y] = path[i], head = i === shown - 1 && tick < 1;
          p.px(x, top + y - 1, head ? WHITE : CHECK[1]);
          p.px(x, top + y, CHECK[0]);
          p.px(x, top + y - 2, head ? WHITE : CHECK[2]);
        }
        if (tick >= 1 && !sealed) fx.sparkle(p, 4, top + 2, 3 * bump(u, 0.72, 0.96), WHITE, CHECK[2], 0.8);
      }
      if (sealed) {
        const stamp = seg(u, 0.62, 0.76), sy = top + 12, sx = 4;
        if (stamp > 0) {
          const drop = Math.round((1 - ease.inCubic(stamp)) * 9), big = stamp < 1 ? 0 : 1;
          for (let dy = -2 - big; dy <= 2 + big; dy++)
            for (let dx = -2 - big; dx <= 2 + big; dx++) {
              if (Math.abs(dx) + Math.abs(dy) > 3 + big) continue;
              p.px(sx + dx, sy + dy - drop, dx + dy < -1 ? WAX[2] : Math.abs(dx) + Math.abs(dy) > 2 ? WAX[0] : WAX[1]);
            }
          if (stamp >= 1) {
            p.px(sx, sy, WAX[2]);
            p.px(sx - 1, sy, WAX[0]);
            p.px(sx + 1, sy, WAX[0]);
            const ring = seg(u, 0.76, 0.96);
            if (ring > 0 && ring < 1) p.ring(sx + 0.5, sy + 0.5, 4 + ring * 5, 1, WAX[2], 1 - ring);
          }
        }
        burst(6, 613, (g) => {
          const a = g.range(0, TAU);
          K.spark(p, sx, sy, Math.cos(a) * g.range(14, 30), Math.sin(a) * g.range(14, 30) - 8, time - 0.76 * info.seconds, g.range(0.1, 0.16), [WAX[2], WAX[1], WAX[0]], 120, 0.8, 0);
        });
      }
      burst(5, 617, (g) => {
        const born = 0.2 + g.range(0, 0.2);
        K.spark(p, g.range(-7, 7), top + length, g.range(-10, 10), g.range(6, 16), time - born * info.seconds, g.range(0.16, 0.26), [PAPER[3], PAPER[1], PAPER[0]], 30, 1, 0);
      });
    };
  }

  return { achievement: medal("achievement"), collection: medal("collection"), campTier: medal("campTier"), quest: scroll(false), trial: scroll(true) };
}
