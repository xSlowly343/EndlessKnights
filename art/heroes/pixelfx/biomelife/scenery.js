export default function scenery(fx) {
  const { TAU, hex, rand, noise, clamp } = fx;
  const S = fx.scene;
  const { W } = S;
  const BLADE = S.ramp(["#4f8a3c", "#6aa348", "#9ad05c"]);
  const STEM = hex("#4f8a3c");
  const PETAL = S.ramp(["#ffffff", "#ffd6e8", "#fff0a8", "#cfe0ff"]);
  const HEART = hex("#ffc93c");
  const BUD = S.ramp(["#c9b6d6", "#a9c08a"]);
  const SHADE = hex("#173a3c");
  const MIST = S.ramp(["#c4d0ff", "#8fa0e0"]);
  const STONE = S.ramp(["#c6d4de", "#a5b8c8", "#8299ad", "#5f7890"]);
  const ROOF = S.ramp(["#c79a98", "#a87a80"]);
  const LAMP = S.ramp(["#fff3b0", "#ffd24a", "#ff9d3c"]);
  const MILL = S.ramp(["#e4e6d6", "#c0cab8", "#9aa898", "#b47c68", "#8a5e54", "#f6f1e2", "#8f7a66"]);

  const tall = (x, seed = 401) => {
    const patch = 0.5 + 0.5 * noise(x * 0.09, seed);
    return rand(x, seed) < 0.25 + patch * 0.55 ? 1 + Math.floor(rand(x, seed + 1) * (1.6 + patch * 2.6)) : 0;
  };

  function turf(p, u, sway, turns, flat = 0) {
    for (let x = 0; x < W; x++) {
      const h = Math.max(0, tall(x) - (flat && tall(x) > 2 ? 1 : 0));
      if (!h) continue;
      const wave = Math.sin((u * turns + x / 40) * TAU) + 0.5 * Math.sin((u * turns * 2 + x / 13) * TAU);
      for (let k = 0; k < h; k++) {
        const v = h > 1 ? k / (h - 1) : 1;
        const lean = Math.round((flat ? -flat * (0.75 + 0.25 * wave) : sway * wave) * v ** 1.4);
        p.px(((x + lean) % W + W) % W, -1 - k + (flat && k === h - 1 && h > 1 ? 1 : 0), k === h - 1 ? BLADE[2] : (x + k) % 3 ? BLADE[1] : BLADE[0]);
      }
    }
  }

  const grass = (p, u) => turf(p, u, 0.8, 1);
  const bossGrass = (p, u) => turf(p, u, 0, 2, 2.4);

  function feet(p, u, tier, turns) {
    for (let x = -18; x <= 18; x++) {
      const edge = 1 - Math.abs(x) / 19, h = Math.round((2 + rand(x, 411) * 3.4) * Math.sqrt(edge));
      if (h < 1 || rand(x, 412) < 0.18) continue;
      const wave = Math.sin((u * turns + x / 14) * TAU);
      for (let k = 0; k < h; k++) {
        const lean = Math.round(wave * tier * 0.75 * (k / Math.max(1, h - 1)) ** 1.3);
        p.px(x + lean, -1 - k, k === h - 1 ? BLADE[2] : (x + k) % 3 ? BLADE[1] : BLADE[0]);
      }
    }
  }

  const sway1 = (p, u) => feet(p, u, 1, 1);
  const sway2 = (p, u) => feet(p, u, 2, 1);

  function sway3(p, u, info) {
    feet(p, u, 3, 2);
    fx.cycle(u * info.seconds, info.seconds, 7, 421, (r) => r.range(0.45, 0.75), (age, life, r, i) => {
      const k = age / life, x = r.range(-15, 15) + Math.sin(k * 5 + i) * 2.5 + k * 3, y = -4 - k * r.range(8, 13);
      p.dot(x, y, PETAL[i % 3], k > 0.6 ? 1 - (k - 0.6) / 0.4 : 1);
      if (i % 2 && k < 0.7) p.dot(x + 1, y + (Math.floor(age * 12) % 2 ? 1 : 0), PETAL[(i + 1) % 3], 0.8);
    });
  }

  const BLOOMS = [];
  for (let i = 0; i < 17; i++) {
    const x = 4 + Math.floor(i * 6.9 + rand(i, 431) * 4);
    if (x < 118) BLOOMS.push({ x, big: i % 3 !== 1, hue: i % 4, stem: 2 + (i % 2) + (i % 3 === 0 ? 1 : 0) });
  }

  function bloom(p, flower, lean, fold) {
    const { x, big, hue, stem } = flower;
    for (let k = 0; k < stem; k++) p.px(x + (k === stem - 1 ? lean : 0), -1 - k, STEM);
    const hx = x + lean, hy = -1 - stem;
    if (fold >= 2) return p.px(hx, hy, BUD[hue % 2 ? 0 : 1]);
    if (!big) return p.px(hx, hy, fold ? BUD[0] : PETAL[hue]);
    if (fold === 0) {
      p.px(hx - 1, hy, PETAL[hue]);
      p.px(hx + 1, hy, PETAL[hue]);
      p.px(hx, hy - 1, PETAL[hue]);
      p.px(hx, hy, HEART);
    } else {
      p.px(hx, hy - 1, PETAL[hue]);
      p.px(hx, hy, PETAL[hue === 0 ? 1 : hue]);
    }
  }

  function flowers(p, u) {
    for (const flower of BLOOMS) bloom(p, flower, Math.round(0.8 * Math.sin((u + flower.x / 40) * TAU)), 0);
  }

  function flowersClose(p, u, info) {
    const i = Math.round(u * (info.frames - 1));
    BLOOMS.forEach((flower, n) => bloom(p, flower, 0, clamp(Math.floor((i + (n % 2)) / 2), 0, 2)));
  }

  function flowersNight(p) {
    for (const flower of BLOOMS) bloom(p, flower, 0, 2);
  }

  function cloudShadows(p) {
    const t = S.tile(p);
    const blobs = [[24, 7, 26, 4], [70, 13, 34, 4.5], [118, 5, 22, 3], [146, 15, 20, 3.5], [96, 2, 14, 2]];
    for (const [cx, cy, rx, ry] of blobs) {
      for (let y = Math.floor(cy - ry); y <= cy + ry; y++) {
        for (let x = Math.floor(cx - rx); x <= cx + rx; x++) {
          const d = ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2;
          if (d > 1 || y < 0) continue;
          const ragged = d > 0.72 && (x + y * 2) % 3 === 0;
          if (!ragged) t.blend(x, y, SHADE, 0.2);
        }
      }
    }
  }

  function nightMist(p, u) {
    S.haze(p, u, 35, 54, MIST[0], 0.34, 441, 1, 0.06);
    S.haze(p, u, 42, 56, MIST[1], 0.22, 442, 2, 0.1);
  }

  function mill(p, u) {
    for (let k = 0; k < 9; k++) {
      const half = k < 3 ? 2 : k < 7 ? 1.5 : 1;
      for (let x = -Math.ceil(half); x <= Math.ceil(half); x++) {
        if (Math.abs(x) > half + 0.5) continue;
        p.px(x, -1 - k, x > 0 ? MILL[1] : x === Math.ceil(half) * -1 ? MILL[2] : MILL[0]);
      }
    }
    p.px(0, -1, MILL[4]);
    p.px(0, -2, MILL[4]);
    p.px(0, -6, MILL[2]);
    for (let x = -2; x <= 2; x++) p.px(x, -10, MILL[3]);
    for (let x = -1; x <= 1; x++) p.px(x, -11, MILL[3]);
    p.px(0, -12, MILL[4]);
    const hub = [0.5, -9.5], turn = (u / 4) * TAU;
    for (let b = 0; b < 4; b++) {
      const a = turn + (b / 4) * TAU, dx = Math.cos(a), dy = Math.sin(a);
      for (let r = 1; r <= 7; r++) {
        p.px(hub[0] + dx * r, hub[1] + dy * r, MILL[6]);
        if (r >= 3) p.px(hub[0] + dx * r - dy * 1.2, hub[1] + dy * r + dx * 1.2, MILL[5]);
        if (r >= 4 && r <= 6) p.px(hub[0] + dx * r - dy * 2.1, hub[1] + dy * r + dx * 2.1, MILL[5]);
      }
    }
    p.px(hub[0], hub[1], MILL[4]);
  }

  const WINDOWS = [[-9, -9], [-1, -10], [1, -6], [8, -7], [-5, -3], [5, -3]];

  function block(p, x0, x1, top, crenels) {
    for (let x = x0; x <= x1; x++) {
      const notch = crenels && (x - x0) % 2 === 1 ? 1 : 0;
      for (let y = top + notch; y <= -1; y++) p.px(x, y, x === x0 ? STONE[0] : x === x1 ? STONE[2] : STONE[1]);
    }
  }

  function cone(p, cx, base, half) {
    for (let k = 0; k <= half + 1; k++) {
      const reach = Math.max(0, half - k);
      for (let x = cx - reach; x <= cx + reach; x++) p.px(x, base - k, x > cx ? ROOF[1] : ROOF[0]);
    }
  }

  function castle(p) {
    block(p, -12, 12, -5, true);
    block(p, -11, -7, -12, false);
    block(p, -3, 3, -11, true);
    block(p, 6, 10, -10, false);
    cone(p, -9, -13, 3);
    cone(p, 8, -11, 3);
    cone(p, 0, -13, 2);
    p.px(0, -17, STONE[3]);
    p.px(1, -17, ROOF[0]);
    p.px(1, -16, ROOF[0]);
    p.px(0, -16, STONE[3]);
    p.px(-1, -1, STONE[3]);
    p.px(0, -1, STONE[3]);
    p.px(-1, -2, STONE[3]);
    p.px(0, -2, STONE[3]);
    for (const [x, y] of WINDOWS) p.px(x, y, STONE[3]);
  }

  function castleLights(p, u) {
    WINDOWS.forEach(([x, y], i) => {
      const tick = S.beat(u, 8, i * 3), low = tick === 0 || (i % 2 === 0 && tick === 5);
      if (i === 4 && tick < 3) return;
      p.px(x, y, low ? LAMP[2] : tick % 3 === 0 ? LAMP[0] : LAMP[1]);
      p.glow(x + 0.5, y + 0.5, 3.5, LAMP[1], low ? 0.25 : 0.5);
    });
    p.px(-1, -1, LAMP[2]);
    p.px(0, -1, LAMP[2]);
    p.glow(0, -1, 4, LAMP[2], 0.3);
  }

  return { grass, bossGrass, sway1, sway2, sway3, flowers, flowersClose, flowersNight, cloudShadows, nightMist, mill, castle, castleLights };
}
