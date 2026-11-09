import sys, importlib
sys.path.insert(0, sys.argv[1])
from pal import *
from PIL import Image
def sprite(txt, pal=None):
    pal = pal or {}
    rows = txt.strip('\n').split('\n'); w = max(len(r) for r in rows)
    im = Image.new('RGBA', (w, len(rows)))
    for y, r in enumerate(rows):
        for x, c in enumerate(r):
            if c == 'o':
                im.putpixel((x, y), OUTLINE + (255,)); continue
            if c in LETTER:
                m, i = LETTER[c]; col = pal.get(m, MAT[m])[i]
                im.putpixel((x, y), tuple(col) + (255,))
    return im
def outline(im, col=OUTLINE):
    import numpy as np
    a = np.array(im); al = a[..., 3] > 0
    p = np.pad(al, 1); ring = (p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]) & ~al
    a[ring] = col + (255,); return Image.fromarray(a)
if __name__ == '__main__':
    mod = importlib.import_module(sys.argv[2]); names = sys.argv[3].split(',')
    ims = [outline(Image.new('RGBA', (1, 1)).resize((1, 1)) if False else sprite(getattr(mod, n)).crop((-1, -1, sprite(getattr(mod, n)).width + 1, sprite(getattr(mod, n)).height + 1))) for n in names]
    W = sum(i.width + 2 for i in ims); H = max(i.height for i in ims)
    c = Image.new('RGBA', (W, H), (60, 60, 66, 255)); x = 0
    for i in ims: c.alpha_composite(i, (x, 0)); x += i.width + 2
    c.resize((c.width * 12, c.height * 12), Image.NEAREST).save(sys.argv[4])
