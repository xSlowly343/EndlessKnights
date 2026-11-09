import math
import numpy as np
from PIL import Image

IMPACT = [(170, 70, 30), (236, 140, 50), (255, 206, 90), (255, 250, 220)]

def arc_field(shape, c, rx, ry, tilt):
    h, w = shape
    ys, xs = np.mgrid[0:h, 0:w].astype(float)
    t = math.radians(tilt)
    dx, dy = xs + 0.5 - c[0], ys + 0.5 - c[1]
    u = dx * math.cos(t) + dy * math.sin(t)
    v = -dx * math.sin(t) + dy * math.cos(t)
    rho = np.sqrt((u / rx) ** 2 + (v / ry) ** 2) + 1e-6
    g = np.sqrt((u / rx ** 2) ** 2 + (v / ry ** 2) ** 2) / rho + 1e-6
    phi = np.degrees(np.arctan2(v / ry, u / rx))
    return rho, g, phi

def sweep_pos(phi, a0, a1):
    span = a1 - a0
    rel = (phi - a0) * np.sign(span) % 360
    return rel / abs(span)

def paint(a, mask, col):
    a[mask, :3] = col; a[mask, 3] = 255

def slash(c, hip, spec, stage, cols, part=None):
    a = np.array(c); keep = a.copy()
    cx, cy = hip[0] + spec['c'][0], hip[1] + spec['c'][1]
    rho, g, phi = arc_field(a.shape[:2], (cx, cy), spec['rx'], spec['ry'], spec.get('tilt', 0))
    a0, a1 = spec['a0'], spec['a1']
    s = sweep_pos(phi, a0, a1)
    depth = (1 - rho) / g
    W = spec.get('w', 5)
    if stage == 0:
        on = (s <= 0.75)
        tt = np.clip(s / 0.75, 0, 1)
        th = 0.5 + W * 1.2 * tt ** 0.8
        m = on & (depth >= -0.5) & (depth <= th)
        paint(a, m, cols[3])
        paint(a, m & (depth > th * 0.7), cols[2])
    elif stage == 1:
        on = s <= 1
        th = 0.5 + W * s ** 0.8
        m = on & (depth >= -0.5) & (depth <= th)
        paint(a, m, cols[1])
        paint(a, m & (depth <= th * 0.6), cols[2])
        paint(a, m & (depth <= th * 0.3) & (s > 0.35), cols[3])
        paint(a, m & (depth > th * 0.85), cols[0])
    else:
        lo = 0.25 if stage == 2 else 0.55
        n = 3 if stage == 2 else 2
        for i in range(n):
            off = i * (W * 0.45)
            seg = (s >= lo + i * 0.08) & (s <= 1.08 - i * 0.04)
            m = seg & (np.abs(depth - off) <= 0.55)
            paint(a, m, cols[3] if i == 0 and stage == 2 else cols[2 - min(i, 1)] if stage == 2 else cols[1])
    if part is not None:
        side = (phi >= 0) if part == 'near' else (phi < 0)
        a[~side] = keep[~side]
    c.paste(Image.fromarray(a))

def tip(hip, spec):
    t = math.radians(spec.get('tilt', 0)); p = math.radians(spec['a1'])
    u, v = spec['rx'] * math.cos(p), spec['ry'] * math.sin(p)
    return (hip[0] + spec['c'][0] + u * math.cos(t) - v * math.sin(t), hip[1] + spec['c'][1] + u * math.sin(t) + v * math.cos(t))

def impact(c, pos, stage, cols=IMPACT):
    a = np.array(c); h, w = a.shape[:2]
    x0, y0 = pos
    def put(x, y, col):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < w and 0 <= y < h: a[y, x, :3] = col; a[y, x, 3] = 255
    rays = [(0, 7), (90, 5), (180, 4), (270, 5), (45, 4), (135, 3), (225, 3), (315, 4)]
    if stage == 0:
        for ang, L in rays:
            r = math.radians(ang)
            for k in range(L + 1):
                put(x0 + math.cos(r) * k, y0 + math.sin(r) * k, cols[3] if k < L * 0.5 else cols[2])
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1): put(x0 + dx, y0 + dy, cols[3])
    elif stage == 1:
        for ang, L in rays[:4]:
            r = math.radians(ang)
            for k in range(2, L + 3): put(x0 + math.cos(r) * k, y0 + math.sin(r) * k, cols[2] if k < L else cols[1])
        for k in range(24):
            r = math.radians(-60 + k * 7.5)
            put(x0 + math.cos(r) * 5, y0 + math.sin(r) * 3.5, cols[1] if k % 6 else cols[2])
    elif stage == 2:
        for k in range(18):
            r = math.radians(-50 + k * 9)
            if k % 3: put(x0 + math.cos(r) * 7, y0 + math.sin(r) * 5, cols[0])
        for ang in (20, 160, 250, 300):
            r = math.radians(ang); put(x0 + math.cos(r) * 8, y0 + math.sin(r) * 8, cols[2])
    c.paste(Image.fromarray(a))
