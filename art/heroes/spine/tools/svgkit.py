import math

UNIT = 4
LIGHT = (0.8, 0.6)


def catmull(points, closed=True, tension=0.5):
    n = len(points)
    if n < 3:
        return "M" + " L".join(f"{x:.3f},{-y:.3f}" for x, y in points)
    segs = []
    rng = range(n) if closed else range(n - 1)
    for i in rng:
        p0 = points[(i - 1) % n] if closed or i > 0 else points[i]
        p1 = points[i]
        p2 = points[(i + 1) % n]
        p3 = points[(i + 2) % n] if closed or i + 2 < n else p2
        c1 = (p1[0] + (p2[0] - p0[0]) * tension / 3, p1[1] + (p2[1] - p0[1]) * tension / 3)
        c2 = (p2[0] - (p3[0] - p1[0]) * tension / 3, p2[1] - (p3[1] - p1[1]) * tension / 3)
        segs.append(f"C{c1[0]:.3f},{-c1[1]:.3f} {c2[0]:.3f},{-c2[1]:.3f} {p2[0]:.3f},{-p2[1]:.3f}")
    head = f"M{points[0][0]:.3f},{-points[0][1]:.3f} "
    return head + " ".join(segs) + (" Z" if closed else "")


def polygon(points):
    return "M" + " L".join(f"{x:.3f},{-y:.3f}" for x, y in points) + " Z"


def arc_points(c, r, a0, a1, steps=16):
    return [(c[0] + r * math.cos(a0 + (a1 - a0) * i / steps), c[1] + r * math.sin(a0 + (a1 - a0) * i / steps)) for i in range(steps + 1)]


def capsule(p0, p1, r0, r1, steps=14):
    dx, dy = p1[0] - p0[0], p1[1] - p0[1]
    ang = math.atan2(dy, dx)
    length = math.hypot(dx, dy)
    phi = math.asin(max(-0.9, min(0.9, (r0 - r1) / length)))
    a = ang + math.pi / 2 + phi
    b = ang - math.pi / 2 - phi
    return arc_points(p1, r1, b, a, steps) + arc_points(p0, r0, a, b + 2 * math.pi, steps)


def ellipse(c, rx, ry, steps=28, rot=0.0):
    out = []
    for i in range(steps):
        t = 2 * math.pi * i / steps
        x, y = rx * math.cos(t), ry * math.sin(t)
        out.append((c[0] + x * math.cos(rot) - y * math.sin(rot), c[1] + x * math.sin(rot) + y * math.cos(rot)))
    return out


def rgb(c):
    return f"rgb({c[0]},{c[1]},{c[2]})"


class Shape:
    def __init__(self, d, mat, pts, shade=0.9, light=0.55, outline=True, tone=2):
        self.d = d
        self.mat = mat
        self.pts = pts
        self.shade = shade
        self.light = light
        self.outline = outline
        self.tone = tone


class Part:
    def __init__(self, name, palette, outline_color, outline_width=1.0):
        self.name = name
        self.palette = palette
        self.outline_color = outline_color
        self.outline_width = outline_width
        self.groups = []
        self.lines = []
        self.flats = []

    def group(self, shapes, mat, shade=0.9, light=0.55, outline=True, tone=2):
        self.groups.append((shapes, mat, shade, light, outline, tone))
        return self

    def smooth(self, pts, mat, **kw):
        return self.group([(catmull(pts), pts)], mat, **kw)

    def poly(self, pts, mat, **kw):
        return self.group([(polygon(pts), pts)], mat, **kw)

    def flat(self, pts, color, smooth=True):
        self.flats.append(((catmull(pts) if smooth else polygon(pts)), pts, color))
        return self

    def line(self, pts, color, width=0.9, closed=False, smooth=True):
        d = catmull(pts, closed=closed) if smooth and len(pts) > 2 else ("M" + " L".join(f"{x:.3f},{-y:.3f}" for x, y in pts))
        self.lines.append((d, pts, color, width))
        return self

    def bbox(self):
        xs, ys = [], []
        for shapes, *_ in self.groups:
            for _, pts in shapes:
                xs += [p[0] for p in pts]
                ys += [p[1] for p in pts]
        for _, pts, _ in self.flats:
            xs += [p[0] for p in pts]
            ys += [p[1] for p in pts]
        for _, pts, _, _ in self.lines:
            xs += [p[0] for p in pts]
            ys += [p[1] for p in pts]
        pad = self.outline_width + 1
        return (math.floor(min(xs) - pad), math.floor(min(ys) - pad), math.ceil(max(xs) + pad), math.ceil(max(ys) + pad))

    def svg(self):
        x0, y0, x1, y1 = self.bbox()
        w, h = x1 - x0, y1 - y0
        lx, ly = LIGHT
        defs, outl, body = [], [], []
        big = f'<rect x="{x0 - 5}" y="{-y1 - 5}" width="{w + 10}" height="{h + 10}"'
        for gi, (shapes, mat, shade, light, outline, tone) in enumerate(self.groups):
            gid = f"g{gi}"
            pal = self.palette[mat]
            paths = "".join(f'<path d="{d}"/>' for d, _ in shapes)
            defs.append(f'<g id="{gid}">{paths}</g>')
            defs.append(f'<clipPath id="c{gid}">{paths}</clipPath>')
            if outline:
                outl.append(f'<use href="#{gid}" fill="none" stroke="{rgb(self.outline_color)}" stroke-width="{2 * self.outline_width:.3f}" stroke-linejoin="round"/>')
                outl.append(f'<use href="#{gid}" fill="{rgb(self.outline_color)}"/>')
            body.append(f'<use href="#{gid}" fill="{rgb(pal[tone])}"/>')
            if shade > 0:
                defs.append(f'<mask id="ms{gid}">{big} fill="white"/><use href="#{gid}" fill="black" transform="translate({lx * shade:.3f},{-ly * shade:.3f})"/></mask>')
                body.append(f'<g clip-path="url(#c{gid})">{big} fill="{rgb(pal[tone - 1])}" mask="url(#ms{gid})"/></g>')
            if light > 0:
                defs.append(f'<mask id="ml{gid}">{big} fill="white"/><use href="#{gid}" fill="black" transform="translate({-lx * light:.3f},{ly * light:.3f})"/></mask>')
                body.append(f'<g clip-path="url(#c{gid})">{big} fill="{rgb(pal[tone + 1])}" mask="url(#ml{gid})"/></g>')
        for d, _, color in self.flats:
            body.append(f'<path d="{d}" fill="{rgb(color)}"/>')
        for d, _, color, width in self.lines:
            body.append(f'<path d="{d}" fill="none" stroke="{rgb(color)}" stroke-width="{width:.3f}" stroke-linecap="round" stroke-linejoin="round"/>')
        return (f'<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" '
                f'width="{w * UNIT}" height="{h * UNIT}" viewBox="{x0} {-y1} {w} {h}" shape-rendering="geometricPrecision">'
                f'<defs>{"".join(defs)}</defs>{"".join(outl)}{"".join(body)}</svg>'), (x0, y0, x1, y1)

    def colors(self):
        cols = [self.outline_color]
        for shapes, mat, *_ in self.groups:
            cols += list(self.palette[mat])
        cols += [c for _, _, c in self.flats]
        cols += [c for _, _, c, _ in self.lines]
        return cols
