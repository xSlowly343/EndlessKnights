import sys, os
from PIL import Image
d, out = sys.argv[1], sys.argv[2]; Z = int(sys.argv[3]) if len(sys.argv) > 3 else 2
order = sys.argv[4].split(',') if len(sys.argv) > 4 else ['Hit1', 'Hit2', 'Hit3']
L = sorted(x for x in os.listdir(d) if os.path.isdir(f'{d}/{x}')); rows = []
for l in L:
    fs = []
    for n in order: fs += sorted(f for f in os.listdir(f'{d}/{l}') if f.startswith(n + '_'))
    rows.append((l, fs))
w0, h0 = Image.open(f'{d}/{L[0]}/{rows[0][1][0]}').size
W = max(len(r[1]) for r in rows)
im = Image.new('RGBA', (w0 * Z * W, h0 * Z * len(rows)), (70, 90, 70, 255))
for r, (l, fs) in enumerate(rows):
    for c, f in enumerate(fs):
        a = Image.open(f'{d}/{l}/{f}'); im.alpha_composite(a.resize((a.width * Z, a.height * Z), Image.NEAREST), (c * w0 * Z, r * h0 * Z))
im.save(out)
