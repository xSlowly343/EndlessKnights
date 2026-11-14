export default function riftSky(fx) {
  const { TAU, rand, noise, cycle, ease, WHITE } = fx;
  const K = fx.kit;
  const DARK = K.ramp(["#0a0614", "#150b2a", "#24124a"]);
  const VOID = K.ramp(["#2e1870", "#4a2a9a", "#8f5bff", "#b79cff"]);
  const EDGE = K.ramp(["#e8daff"])[0];
  const SHARD = [EDGE, VOID[3], VOID[2]];

  function chip(p, x, y, turn, size, alpha) {
    const q = ((Math.floor(turn) % 4) + 4) % 4;
    p.dot(x, y, SHARD[0], alpha);
    if (size < 2) return;
    p.dot(x + (q === 0 || q === 3 ? 1 : -1), y, SHARD[1], alpha);
    p.dot(x, y + (q < 2 ? 1 : -1), SHARD[2], alpha);
  }

  const crackLine = (x) => 5 + noise(x * 0.07, 3) * 4 + noise(x * 0.31, 5) * 1.6;

  function sky(p, phase) {
    for (let y = 0; y < 26; y++) {
      for (let x = -80; x < 80; x++) {
        const t = y / 26 + 0.05 * Math.sin(x * 0.11 + phase * TAU) + 0.03 * Math.sin(x * 0.37 - phase * TAU * 2);
        const alpha = y < 15 ? 1 : 1 - (y - 15) / 11;
        p.dot(x, y, t < 0.2 ? DARK[0] : t < 0.42 ? DARK[1] : t < 0.66 ? DARK[2] : VOID[0], alpha);
      }
    }
    for (let i = 0; i < 26; i++) {
      const x = Math.floor(rand(i, 1, 401) * 160) - 80, y = Math.floor(rand(i, 2, 401) * 9);
      const beat = (Math.floor(phase * 8) + i * 3) % 8;
      if (beat < 6) p.px(x, y, beat === 0 ? WHITE : beat < 3 ? VOID[3] : VOID[1]);
    }
    const sway = Math.round(Math.sin(phase * TAU) * 4), bob = Math.round(Math.cos(phase * TAU) * 1.2);
    const cx = 44 + sway;
    for (let dx = -13; dx <= 13; dx++) {
      const t = dx / 13, body = 9 * Math.sqrt(Math.max(0, 1 - t * t)), head = Math.abs(t) < 0.3 ? 5 * Math.sqrt(1 - (t / 0.3) ** 2) : 0;
      const horn = Math.abs(Math.abs(t) - 0.36) < 0.06 ? 4 : 0, top = Math.round(19 - body - head - horn) + bob;
      for (let y = Math.max(0, top); y < 22; y++) p.dot(cx + dx, y, y === top ? VOID[1] : DARK[0], y < 16 ? 1 : 1 - (y - 16) / 7);
    }
    p.px(cx - 2, 8 + bob, VOID[3]);
    p.px(cx + 1, 8 + bob, VOID[3]);
    p.glow(cx, 8 + bob, 5, VOID[2], 0.35);
    const breath = 0.5 + 0.5 * Math.sin(phase * TAU);
    for (let x = -72; x < 66; x++) {
      const end = Math.min(x + 72, 66 - x) / 14, wide = Math.min(1, end) * (0.6 + 1.1 * breath * (0.5 + 0.5 * noise(x * 0.13, 9)));
      const y = crackLine(x);
      if (wide < 0.45) {
        if ((x & 1) === 0) p.px(x, y, VOID[2]);
        continue;
      }
      const half = Math.round(wide);
      for (let dy = -half; dy <= half; dy++) {
        const rim = Math.abs(dy) === half;
        const star = rand(x + 200, dy + 9, 7) < 0.3 && (Math.floor(phase * 8) + x) % 4 < 2;
        p.px(x, y + dy, rim ? (half > 1 ? VOID[2] : EDGE) : star ? WHITE : half > 1 ? EDGE : WHITE);
      }
      if (x % 9 === 0) p.glow(x, y, 7, VOID[2], 0.22 + 0.15 * breath);
    }
    for (let i = 0; i < 5; i++) {
      const x = -60 + i * 27, y = crackLine(x), dir = i % 2 ? 1 : -1, len = 3 + (i % 3);
      for (let j = 1; j <= len; j++) p.px(x + j * (i % 3 === 0 ? 1 : -1), y + dir * j, j < len ? VOID[3] : VOID[1]);
    }
  }

  function edge(p, phase, side, seed, runners, breaks) {
    const at = (depth, y, color, alpha = 1) => {
      const x = side > 0 ? depth : -1 - depth;
      if (alpha >= 1) p.px(x, y, color);
      else p.dot(x, y, color, alpha);
    };
    const rim = (y) => 5.5 + noise(y * 0.19, seed) * 3 + noise(y * 0.6, seed + 4) * 1.4 + Math.sin(phase * TAU + y * 0.45 + seed) * 0.8;
    for (let y = -30; y < 30; y++) {
      const b = Math.max(2, Math.min(10, Math.round(rim(y))));
      for (let d = 0; d <= b; d++) {
        const inside = b - d;
        let color = inside === 0 ? EDGE : inside === 1 ? VOID[2] : inside === 2 ? VOID[1] : DARK[0];
        if (inside > 2) {
          const n = Math.sin(y * 0.3 + phase * TAU * side + d * 0.8);
          color = n > 0.6 ? DARK[2] : n > -0.2 ? DARK[1] : DARK[0];
          if (rand(d, y + 40, seed) < 0.05 && (Math.floor(phase * 6) + y) % 3 === 0) color = VOID[3];
        }
        at(d, y, color);
      }
      if ((y + Math.floor(phase * 12) * side + 600) % 4 === 0) at(b + 1, y, VOID[1]);
    }
    for (let i = 0; i < runners; i++) {
      const travel = (phase + i / runners) % 1, y = Math.round(side > 0 ? -30 + travel * 60 : 29 - travel * 60);
      if (y < -30 || y > 29) continue;
      const b = Math.max(2, Math.min(10, Math.round(rim(y))));
      at(b, y, WHITE);
      at(b + 1, y, EDGE);
      const back = y - side;
      if (back >= -30 && back <= 29) at(Math.max(2, Math.min(10, Math.round(rim(back)))), back, WHITE);
      p.glow(side > 0 ? b + 0.5 : -b - 0.5, y, 5, VOID[3], 0.5);
    }
    cycle(phase * 1.2, 1.2, breaks, seed + 20, (r) => r.range(0.6, 1), (age, life, r, i) => {
      const k = age / life, y0 = r.range(-26, 26), b = rim(Math.round(y0));
      const depth = b + 1 + ease.outQuad(k) * r.range(2, 13 - b), y = y0 - k * r.range(2, 8);
      if (depth > 13) return;
      const x = side > 0 ? depth : -1 - depth;
      chip(p, x, y, age * 8 + i, depth < 12 ? 2 : 1, k < 0.2 ? 1 : 1 - (k - 0.2) / 0.8);
    });
  }

  function motes(p, phase, info) {
    cycle(phase * info.seconds, info.seconds, 26, 431, (r) => r.range(1, 1.8), (age, life, r, i) => {
      const k = age / life, x = r.range(-78, 78) + Math.sin(k * 4 + i) * 2, y = r.range(-8, 29) - age * r.range(7, 16);
      if (y < -30) return;
      const alpha = Math.sin(k * Math.PI);
      if (i % 5 === 0) chip(p, x, y, age * 5 + i, 2, alpha);
      else {
        p.dot(x, y, i % 3 === 0 ? EDGE : i % 3 === 1 ? VOID[3] : VOID[2], alpha);
        if (i % 4 === 0 && alpha > 0.7) p.glow(x, y, 3, VOID[2], 0.3);
      }
    });
  }

  return {
    sky,
    edgeLeft: (p, phase) => edge(p, phase, 1, 11, 2, 4),
    edgeRight: (p, phase) => edge(p, phase, -1, 37, 3, 5),
    motes,
  };
}
