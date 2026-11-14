export default function kit(core) {
  const { hex, clamp, fly, TAU } = core;

  const ramp = (list) => list.map((c) => hex(c));

  function spark(p, x, y, vx, vy, age, life, colors, g = 0, drag = 0, tail = 0.03) {
    if (age < 0 || age >= life) return;
    const k = age / life;
    const [px, py] = fly(x, y, vx, vy, age, g, drag);
    const alpha = k > 0.7 ? 1 - ((k - 0.7) / 0.3) * 0.75 : 1;
    if (tail > 0 && age > tail) {
      const [qx, qy] = fly(x, y, vx, vy, age - tail, g, drag);
      if (Math.floor(qx) !== Math.floor(px) || Math.floor(qy) !== Math.floor(py)) p.dot(qx, qy, colors[colors.length - 1], alpha * 0.8);
    }
    p.dot(px, py, colors[Math.min(colors.length - 1, Math.floor(k * colors.length))], alpha);
    return [px, py];
  }

  function floorRing(p, cx, cy, rx, ry, near, far, alpha = 1) {
    if (rx < 0.5) return;
    const n = Math.max(8, Math.ceil(rx * 7));
    for (let i = 0; i < n; i++) {
      const a = (i / n) * TAU, s = Math.sin(a);
      p.dot(cx + Math.cos(a) * rx, cy + s * ry, s < 0 ? far : near, alpha);
    }
  }

  function puff(p, x, y, r, color, alpha) {
    if (alpha <= 0.01 || r <= 0) return;
    p.disc(x, y, r, color, (d) => alpha * (1 - d * d * 0.7));
  }

  function mirror(x) {
    return -Math.floor(x) - 1;
  }

  function flame(p, cx, base, half, height, phase, colors, seed = 0, turns = 1) {
    const tall = height * (0.82 + 0.18 * Math.sin((phase * 2 * turns + seed * 0.37) * TAU));
    for (let row = 0; row < tall; row++) {
      const v = row / tall;
      const sway = Math.sin((phase * turns + seed * 0.21) * TAU + row * 0.8) * v * 1.3 + Math.sin((phase * 2 * turns + seed * 0.53) * TAU + row * 0.45) * v * 0.7;
      const wide = half * (1 - v) ** 0.75 * (0.82 + 0.18 * Math.sin((phase * 3 * turns + seed * 0.11) * TAU + row * 1.7));
      const mid = cx + sway;
      for (let x = Math.round(mid - wide); x <= Math.round(mid + wide - 1); x++) {
        const d = Math.abs(x + 0.5 - mid) / Math.max(wide, 0.5);
        const heat = clamp((1 - v * 0.85) * (1 - d * 0.75));
        p.px(x, base - row, colors[Math.min(colors.length - 1, Math.floor(heat * colors.length))]);
      }
    }
  }

  function scaled(p, sample, left, right, rows, scale, shiftX = 0, lift = 0) {
    if (scale <= 0) return;
    const top = Math.floor(rows * scale);
    for (let row = 0; row <= top; row++) {
      const source = Math.min(rows, Math.floor(row / scale));
      for (let x = left; x <= right; x++) {
        const color = sample(x, source);
        if (color) p.px(x + shiftX, -row - lift, color);
      }
    }
  }

  const index = (x) => (x >= 0 ? x : -x - 1);

  const pulse = (phase, n = 1, shift = 0) => 0.5 + 0.5 * Math.sin((phase * n + shift) * TAU);

  return { ramp, spark, floorRing, puff, mirror, pulse, clamp, flame, scaled, index };
}
