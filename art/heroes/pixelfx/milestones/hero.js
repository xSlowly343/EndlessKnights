export default function hero(fx) {
  const { clamp, seg, bump, ease, burst, TAU, WHITE } = fx;
  const K = fx.kit;
  const GOLD = K.ramp(["#6a3f0e", "#b67c1c", "#f0b43c", "#ffd27a", "#fff3c4"]);
  const ECHO = K.ramp(["#0f4a52", "#1f8f8a", "#3fe0d0", "#c8fff4"]);

  const exit = (p, u, from = 0.86) => (p.fade = 1 - 0.7 * seg(u, from, 1));

  function chevron(p, x, y, half, light, dark, alpha) {
    for (let d = 0; d <= half; d++)
      for (const side of [-1, 1]) {
        const px = side < 0 ? x - d - 1 : x + d;
        p.dot(px, y + d, light, alpha);
        p.dot(px, y + d + 1, dark, alpha);
      }
  }

  function levelUp(p, u, info) {
    const time = u * info.seconds;
    exit(p, u);
    const gather = seg(u, 0, 0.14), rise = ease.outCubic(seg(u, 0.14, 0.62)), pop = seg(u, 0.6, 1.05);
    if (gather < 1) {
      K.floorRing(p, 0, 0, 15 - gather * 8, 3 - gather, GOLD[3], GOLD[1]);
      p.glow(0, -2, 6 + gather * 5, GOLD[2], 0.25 + 0.3 * gather);
    }
    const ringY = -1 - rise * 33;
    if (gather >= 1 && pop < 1) {
      const wide = 8 + 2 * Math.sin(rise * Math.PI) + pop * 9, alpha = 1 - pop;
      for (let y = 0; y > ringY; y--) {
        const k = (y - ringY) / 12;
        if (k > 1) continue;
        for (const x of [-6, -2, 1, 5]) if ((x * 3 + y + Math.floor(time * 30)) % 4 === 0) p.dot(x, y, k < 0.4 ? GOLD[4] : GOLD[2], (1 - k) * alpha);
      }
      K.floorRing(p, 0, ringY, wide, 2.4 + pop, GOLD[4], GOLD[1], alpha);
      K.floorRing(p, 0, ringY + 1, wide - 1, 2.4 + pop, GOLD[2], GOLD[0], alpha * 0.8);
      p.glow(0, ringY, 9, GOLD[2], 0.35 * alpha);
    }
    for (let i = 0; i < 3; i++) {
      const k = seg(u, 0.26 + i * 0.11, 0.7 + i * 0.1);
      if (k <= 0 || k >= 1) continue;
      const lift = ease.outQuad(k) * 20;
      chevron(p, 0, -12 - i * 5 - lift, 3 + (k < 0.2 ? 1 : 0), k < 0.25 ? WHITE : GOLD[4], GOLD[2], 1 - seg(k, 0.6, 1));
    }
    burst(14, 211, (g, i) => {
      const born = 0.14 + (i / 14) * 0.42, a = g.range(0, TAU), startY = -1 - ease.outCubic(seg(born, 0.14, 0.62)) * 33;
      K.spark(p, Math.cos(a) * 8, startY, Math.cos(a) * g.range(6, 16), -g.range(4, 18), time - born * info.seconds, g.range(0.24, 0.4), [WHITE, GOLD[4], GOLD[3], GOLD[2], GOLD[1]], 110, 0.6, 0.04);
    });
    const crown = bump(u, 0.58, 1.08);
    if (crown > 0) {
      fx.sparkle(p, 0, -37, 2 + crown * 4, WHITE, GOLD[3], 0.8);
      for (const side of [-1, 1]) p.dot(side * (5 + crown * 4), -36 - crown * 2, GOLD[4], crown);
    }
  }

  function arcBand(p, cx, cy, r, colors, alpha) {
    if (r < 2) return;
    const n = Math.ceil(r * 5);
    for (let i = 0; i < n; i++) {
      const a = (i / n) * TAU, c = Math.cos(a), s = Math.sin(a);
      const x = cx + c * r, y = cy + s * r * 0.62;
      if (y > 9 || y < -50) continue;
      p.dot(x, y, colors[2], alpha);
      p.dot(x - c * 1.2, y - s * 1.2, colors[1], alpha * 0.8);
      if (i % 3 === 0) p.dot(x + c * 1.4, y + s * 1.4, colors[3], alpha);
    }
  }

  function prestige(p, u, info) {
    const time = u * info.seconds;
    exit(p, u, 0.88);
    const squeeze = seg(u, 0, 0.14), flash = bump(u, 0.1, 0.3), cy = -14;
    if (squeeze < 1)
      burst(44, 307, (g) => {
        const a = g.range(0, TAU), far = g.range(70, 150), near = 6 + (1 - squeeze) * far, len = 16 + squeeze * 22;
        p.line(Math.cos(a) * near, cy + Math.sin(a) * near * 0.5, Math.cos(a) * (near + len), cy + Math.sin(a) * (near + len) * 0.5, (k) => (k < 0.4 ? WHITE : k < 0.7 ? ECHO[3] : ECHO[2]), (k) => 1 - k * 0.5);
      });
    if (flash > 0) {
      p.circle(0, cy, 3 + flash * 5, WHITE, flash);
      p.glow(0, cy, 22 + flash * 26, ECHO[2], 0.75 * flash);
    }
    const rush = seg(u, 0.16, 0.86);
    if (rush > 0 && rush < 1) {
      const force = Math.sin(rush * Math.PI);
      burst(30, 311, (g) => {
        const y = g.range(-48, 2), speed = g.range(260, 460), len = g.range(10, 30) * (0.4 + force), x = 150 - ((time * speed + g.range(0, 200)) % 200);
        if (Math.abs(y - cy) < 9 && Math.abs(x) < 12) return;
        p.line(x, y, x + len, y, (k) => (k < 0.2 ? WHITE : k < 0.6 ? ECHO[3] : ECHO[1]), (k) => force * (1 - k * 0.8));
      });
      for (let post = 0; post < 7; post++) {
        const x = 150 - ((time * 330 + post * 29) % 200), tall = post % 3 === 0 ? 9 : 5;
        for (let y = 0; y < tall; y++) p.dot(x + y * 0.5, 1 - y, y === tall - 1 ? WHITE : ECHO[2], force);
        p.dot(x + 4, 2, ECHO[0], force);
        p.dot(x + 6, 2, ECHO[0], force * 0.7);
      }
    }
    for (let i = 0; i < 3; i++) {
      const k = seg(u, 0.16 + i * 0.1, 0.62 + i * 0.12);
      if (k > 0 && k < 1) arcBand(p, 0, cy, ease.outQuad(k) * 165, ECHO, 1 - seg(k, 0.55, 1));
    }
    burst(22, 313, (g) => {
      const born = 0.5 + g.range(0, 0.18), k = seg(u, born, born + 0.26);
      if (k <= 0 || k >= 1) return;
      const sx = g.range(-26, 128), sy = g.range(-46, 2), pull = ease.inCubic(k);
      const x = sx + (0 - sx) * pull, y = sy + (cy - sy) * pull;
      p.dot(x, y, k > 0.7 ? WHITE : ECHO[3]);
      p.dot(x + (sx > 0 ? 1 : -1), y, ECHO[2], 0.8);
      p.dot(x + (sx > 0 ? 2 : -2) * (1 + pull), y + (sy - cy) * 0.04, ECHO[1], 0.6);
    });
    const land = seg(u, 0.8, 1);
    if (land > 0) {
      K.floorRing(p, 0, 0, 4 + ease.outCubic(land) * 20, 1 + land * 3, ECHO[3], ECHO[1], 1 - land * 0.6);
      fx.sparkle(p, 0, cy, 6 * (1 - land), WHITE, ECHO[2], 0.9);
      p.glow(0, cy, 16, ECHO[2], 0.5 * (1 - land));
    }
  }

  return { levelUp, prestige };
}
