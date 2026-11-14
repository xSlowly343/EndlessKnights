export default function elements(fx) {
  const { clamp, seg, bump, TAU, rng, cycle, burst, bolt, forks, WHITE } = fx;
  const K = fx.kit;
  const STONE = K.ramp(["#4a3620", "#7a5a36", "#c9a066", "#e6c890"]);
  const AQUA = K.ramp(["#1c4f86", "#2a6fb0", "#5bb8ff", "#bfe6f5"]);
  const VOLT = K.ramp(["#8a7a1a", "#f0e04c", "#fff7b0"]);
  const WIND = K.ramp(["#4f86a0", "#7fb6d0", "#bfe6f5"]);
  const CRACK = K.ramp(["#5b9bff", "#9fb4d6", "#e6e9f0"]);
  const LOOP = 8 / 12;

  function earth(tier) {
    const crumbs = [5, 9, 14][tier - 1];
    return (p, phase, info, g) => {
      const s = g.s, half = [8, 11, 14][tier - 1] * s;
      burst((3 + tier * 3) * s, 501 + tier, (r) => {
        const x = Math.floor(r.range(-half, half)), big = r.chance(0.4);
        p.px(x, 0, STONE[1]);
        p.px(x + 1, 0, STONE[0]);
        if (big || s > 1) {
          p.px(x, -1, STONE[2]);
          if (big && s > 1) p.px(x + 1, -1, STONE[1]);
        }
      });
      cycle(phase * LOOP, LOOP, crumbs * s, 509 + tier, (r) => r.range(0.35, 0.55), (age, life, r) => {
        const x = r.range(-half * 0.8, half * 0.8), from = r.range(5, 9) * s, k = age / life;
        const land = 0.62;
        if (k < land) {
          const y = -from * (1 - (k / land) ** 2);
          p.px(x, y, STONE[3]);
          if (s > 1) p.px(x + 1, y, STONE[2]);
          p.px(x, y - 1, STONE[2]);
        } else {
          const hop = (k - land) / (1 - land), side = r.sign();
          p.px(x + side * hop * 3 * s, -Math.sin(hop * Math.PI) * 2 * s, STONE[2]);
          K.puff(p, x, -1, (1 + hop * 1.5) * s, STONE[2], 0.5 * (1 - hop));
        }
      });
      cycle(phase * LOOP, LOOP, tier * s, 523, () => LOOP, (age, life, r) => {
        const k = age / life;
        K.puff(p, r.range(-half, half) + k * r.sign() * 3 * s, -1 - k * 2 * s, (1.2 + k * 1.6) * s, STONE[1], 0.4 * (1 - k));
      });
    };
  }

  function water(tier) {
    return (p, phase, info, g) => {
      const s = g.s, rx = [9, 14][tier - 1] * s, ry = (1 + tier * 0.5) * s;
      for (let x = -rx; x <= rx; x++) {
        const arch = Math.round(ry * Math.sqrt(Math.max(0, 1 - (x / rx) ** 2)));
        for (let y = -arch; y <= arch; y++) {
          const shine = Math.sin((x / (4 * s) + phase) * TAU + y) > 0.72;
          p.px(x, y, y === arch ? AQUA[0] : shine ? AQUA[3] : y < 0 ? AQUA[2] : AQUA[1]);
        }
      }
      const ring = phase;
      K.floorRing(p, 0, 0, rx * (0.25 + ring * 0.75), Math.max(1, ry * (0.25 + ring * 0.75)), AQUA[3], AQUA[2], 1 - ring * 0.8);
      cycle(phase * LOOP, LOOP, (2 + tier * 2) * s, 601 + tier, (r) => r.range(0.4, 0.6), (age, life, r) => {
        const x = r.range(-rx * 0.8, rx * 0.8), from = r.range(10, 17) * s, k = age / life, land = 0.7;
        if (k < land) {
          const y = -from * (1 - (k / land) ** 2);
          for (let i = 0; i <= s; i++) p.px(x, y - i, i === 0 ? AQUA[3] : AQUA[2]);
        } else {
          const hop = (k - land) / (1 - land);
          for (const side of [-1, 1]) p.px(x + side * (1 + hop * 2.5 * s), -ry - Math.sin(hop * Math.PI) * 2.5 * s, hop < 0.6 ? AQUA[3] : AQUA[2]);
          if (hop < 0.5) p.px(x, -ry - 1, WHITE);
        }
      });
    };
  }

  function strike(p, path, core, rim, wide) {
    for (let i = 1; i < path.length; i++) {
      if (wide) p.beam(path[i - 1][0], path[i - 1][1], path[i][0], path[i][1], 3, rim);
      p.line(path[i - 1][0], path[i - 1][1], path[i][0], path[i][1], core);
    }
  }

  function lightning(p, u, info, g) {
    const s = g.s, frame = Math.round(u * (info.frames - 1)), height = 35 * s;
    const main = bolt(rng(701, frame < 3 ? 0 : 1), 3 * s, -height, -2 * s, -1, { jag: 0.3, depth: 4 });
    if (frame === 0) {
      strike(p, main.slice(0, Math.ceil(main.length * 0.45)), VOLT[2], VOLT[1], false);
      p.glow(3 * s, -height, 4 * s, VOLT[1], 0.4);
      return;
    }
    if (frame <= 3) {
      const bright = frame < 3;
      strike(p, main, bright ? WHITE : VOLT[2], VOLT[1], s > 1 || frame === 1);
      if (frame === 1 && s === 1) strike(p, main.map(([x, y]) => [x + 1, y]), VOLT[1], VOLT[1], false);
      for (const branch of forks(rng(709, frame), main, { count: frame === 1 ? 4 : 2, len: 9 * s, jag: 0.35 })) strike(p, branch, bright ? VOLT[2] : VOLT[1], VOLT[0], false);
      p.glow(0, -height * 0.5, height * 0.55, VOLT[1], frame === 1 ? 0.45 : 0.25);
      fx.sparkle(p, -2 * s, -1, (frame === 1 ? 5 : 3) * s, WHITE, VOLT[1], 0.8);
    }
    const after = clamp((u - 0.25) / 0.75);
    burst(9 * s, 719, (r) => {
      const a = r.range(-Math.PI, 0), speed = r.range(20, 55) * s;
      K.spark(p, -2 * s, -1, Math.cos(a) * speed, Math.sin(a) * speed, after * 0.3, r.range(0.14, 0.3), [WHITE, VOLT[2], VOLT[1], VOLT[0]], 150 * s, 1);
    });
    if (frame >= 4)
      burst(5 * s, 727, (r) => {
        const y = -r.range(0.15, 0.9) * height, x = r.range(-7, 7) * s;
        if ((frame + Math.floor(r.next() * 2)) % 2) p.line(x, y, x + r.range(-3, 3) * s, y + r.range(1, 4) * s, frame === 4 ? VOLT[2] : VOLT[0]);
      });
  }

  function air(p, u, info, g) {
    const s = g.s;
    for (let band = 0; band < 3; band++) {
      const delay = band * 0.12, k = clamp((u - delay) / (1 - delay));
      if (k <= 0 || k >= 1) continue;
      const height = (band - 1) * 6 * s, length = 14 * s, head = -6 * s + k * k * 42 * s;
      for (let i = 0; i < length; i++) {
        const along = i / length, x = head - i;
        const curl = k < 0.45 ? Math.sin((along * 1.5 + k * 2) * TAU) * (1 - k / 0.45) * 5 * s : 0;
        const y = height + curl + Math.sin(along * 3 + band) * s;
        if (along > 1 - k * 0.3 && i % 2) continue;
        p.px(x, y, i < 2 * s ? WHITE : along < 0.4 ? WIND[2] : along < 0.75 ? WIND[1] : WIND[0]);
        if (s > 1 && along < 0.5) p.px(x, y + 1, WIND[1]);
      }
    }
    const kick = bump(u, 0, 0.35);
    if (kick > 0) {
      p.arc(0, 0, 9 * s, s, -1.9 + u * 3, 0.9 + u * 3, WIND[2], kick);
      p.arc(0, 0, 6 * s, 1, 1.2 + u * 4, 3.4 + u * 4, WIND[1], kick);
    }
    burst(6 * s, 811, (r) => {
      const k = clamp(u * 1.2 - r.range(0, 0.3));
      if (k <= 0 || k >= 1) return;
      p.px(-4 * s + k * r.range(22, 34) * s, r.range(-9, 9) * s - Math.sin(k * Math.PI) * 2 * s, k < 0.6 ? WIND[2] : WIND[0]);
    });
  }

  function fractureChip(p, u, info, g) {
    const s = g.s, flash = 1 - seg(u, 0, 0.35);
    if (flash > 0) {
      for (let i = 0; i < 5; i++) {
        const a = i * (TAU / 5) + 0.4, reach = (3 + (1 - flash) * 7) * s;
        const path = bolt(rng(907, i), 0, 0, Math.cos(a) * reach, Math.sin(a) * reach, { jag: 0.3, depth: 2 });
        for (let k = 1; k < path.length; k++) p.line(path[k - 1][0], path[k - 1][1], path[k][0], path[k][1], flash > 0.5 ? WHITE : CRACK[2]);
      }
      p.glow(0, 0, 8 * s, CRACK[0], 0.35 * flash);
    }
    burst(8 * s, 911, (r) => {
      const a = r.range(0, TAU), speed = r.range(26, 52) * s, age = u * info.seconds;
      const spot = K.spark(p, 0, 0, Math.cos(a) * speed, Math.sin(a) * speed - 12 * s, age, r.range(0.16, 0.26), [WHITE, CRACK[2], CRACK[1], CRACK[0]], 160 * s, 1.2);
      if (spot && s > 1) p.px(spot[0] + 1, spot[1], CRACK[1]);
    });
  }

  return { earth1: earth(1), earth2: earth(2), earth3: earth(3), water1: water(1), water2: water(2), lightning, air, fractureChip };
}
