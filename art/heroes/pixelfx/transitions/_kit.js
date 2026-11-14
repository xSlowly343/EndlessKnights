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

  const pulse = (phase, n = 1, shift = 0) => 0.5 + 0.5 * Math.sin((phase * n + shift) * TAU);

  return { ramp, spark, floorRing, puff, mirror, pulse, clamp };
}
