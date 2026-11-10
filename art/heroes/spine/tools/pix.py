import math


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.px = [["."] * w for _ in range(h)]
        self.masks = []

    def inside(self, x, y):
        return 0 <= x < self.w and 0 <= y < self.h

    def put(self, x, y, ch):
        if self.inside(x, y):
            self.px[y][x] = ch

    def get(self, x, y):
        return self.px[y][x] if self.inside(x, y) else "."

    def mask_poly(self, pts):
        m = set()
        ys = [p[1] for p in pts]
        for y in range(max(0, int(min(ys))), min(self.h, int(math.ceil(max(ys))) + 1)):
            cy = y + 0.5
            xs = []
            for i in range(len(pts)):
                (x0, y0), (x1, y1) = pts[i], pts[(i + 1) % len(pts)]
                if (y0 <= cy < y1) or (y1 <= cy < y0):
                    xs.append(x0 + (cy - y0) * (x1 - x0) / (y1 - y0))
            xs.sort()
            for a, b in zip(xs[0::2], xs[1::2]):
                for x in range(max(0, int(round(a))), min(self.w, int(round(b)))):
                    m.add((x, y))
        return m

    def mask_ellipse(self, cx, cy, rx, ry):
        m = set()
        for y in range(self.h):
            for x in range(self.w):
                if ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1.0:
                    m.add((x, y))
        return m

    def mask_rect(self, x0, y0, x1, y1):
        return {(x, y) for y in range(y0, y1) for x in range(x0, x1) if self.inside(x, y)}

    def shade(self, mask, tones, light=(0.68, 0.32), spec=None, edge=None):
        if not mask:
            return mask
        rows, cols = {}, {}
        for x, y in mask:
            rows.setdefault(y, []).append(x)
            cols.setdefault(x, []).append(y)
        for x, y in mask:
            r, c = rows[y], cols[x]
            fx = (x - min(r)) / max(1, max(r) - min(r))
            fy = 1 - (y - min(c)) / max(1, max(c) - min(c))
            l = light[0] * fx + light[1] * fy
            if l < 0.3:
                ch = tones[0]
            elif l < 0.64:
                ch = tones[1]
            else:
                ch = tones[2]
            if spec and 0.74 <= fx <= 0.84 and fy > 0.25:
                ch = spec
            self.put(x, y, ch)
        if edge:
            self.outline(mask, edge)
        return mask

    def outline(self, mask, ch):
        for x, y in mask:
            if any((x + dx, y + dy) not in mask for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                self.put(x, y, ch)

    def line(self, x0, y0, x1, y1, ch, mask=None):
        n = max(abs(x1 - x0), abs(y1 - y0), 1)
        for i in range(n + 1):
            x = round(x0 + (x1 - x0) * i / n)
            y = round(y0 + (y1 - y0) * i / n)
            if mask is None or (x, y) in mask:
                self.put(x, y, ch)

    def stamp(self, x0, y0, art):
        for dy, row in enumerate(art.strip("\n").split("\n")):
            for dx, ch in enumerate(row):
                if ch != " ":
                    self.put(x0 + dx, y0 + dy, ch)

    def text(self):
        return "\n".join("".join(r) for r in self.px)
