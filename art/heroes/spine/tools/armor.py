MAIN = {"#": 0, "M": 1, "m": 2, "w": 3}
TRIM = {"V": 0, "Y": 1, "y": 2, "v": 3}
GEM = {"G": 1, "c": 2, "C": 3}
FIXED = {"B": ("leather", 1), "b": ("leather", 2), "n": ("leather", 3), "N": ("leather", 0),
         "P": ("shirt", 1), "p": ("shirt", 2), "q": ("shirt", 3),
         "S": ("skin", 1), "s": ("skin", 2), "x": ("skin", 0)}

RARITY_HEX = ["9AA3B5", "E6E9F0", "4FC277", "5B9BFF", "B07CFF", "FF9A3C", "FF5A5A", "3FE0D0", None, "1E2370", "FFF4D6"]
SPECIAL_TRIM = {
    9: [(110, 40, 150), (230, 70, 80), (250, 210, 70), (90, 220, 230)],
    10: [(14, 16, 52), (30, 35, 112), (80, 100, 200), (236, 240, 255)],
    11: [(150, 110, 50), (232, 192, 104), (255, 244, 214), (255, 255, 255)],
}
SPECIAL_CLOTH = {
    9: [(40, 26, 64), (78, 46, 116), (122, 80, 170), (176, 140, 220)],
    10: [(10, 12, 40), (24, 28, 86), (40, 48, 128), (200, 210, 255)],
    11: [(150, 124, 80), (210, 186, 140), (246, 236, 210), (255, 252, 240)],
}
GOLD = [(140, 96, 44), (192, 146, 70), (226, 190, 104), (246, 226, 160)]


def hexrgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def scale(c, k):
    return tuple(max(0, min(255, int(v * k))) for v in c)


def mix(c, d, t):
    return tuple(int(a + (b - a) * t) for a, b in zip(c, d))


def trim_tones(r):
    if r in SPECIAL_TRIM:
        return SPECIAL_TRIM[r]
    c = hexrgb(RARITY_HEX[r - 1])
    return [scale(c, 0.42), scale(c, 0.68), c, mix(c, (255, 255, 255), 0.5)]


def cloth_tones(r):
    if r in SPECIAL_CLOTH:
        return SPECIAL_CLOTH[r]
    c = hexrgb(RARITY_HEX[r - 1])
    return [scale(c, 0.3), scale(c, 0.48), scale(c, 0.66), scale(c, 0.86)]


TINT = {1: 0.0, 2: 0.0, 3: 0.0, 4: 0.14, 5: 0.18, 6: 0.26, 7: 0.28, 8: 0.3, 9: 0.3, 10: 0.45, 11: 0.4}


def tinted(base, r):
    trim = trim_tones(r)
    k = TINT[r]
    return [mix(b, scale(t, 1.0 if i < 3 else 0.95), k) for i, (b, t) in enumerate(zip(base, trim))]


def palette(material, r, mat):
    trim = trim_tones(r)
    main = {"plate": tinted(mat["metal"], r), "leather": tinted(mat["leather"], r), "cloth": cloth_tones(r)}[material]
    edge = GOLD if material == "cloth" else trim
    pal = {}
    for ch, i in MAIN.items():
        pal[ch] = main[i]
    for ch, i in TRIM.items():
        pal[ch] = edge[i]
    for ch, i in GEM.items():
        pal[ch] = trim[i] if material != "cloth" else trim[min(3, i + 1) if r < 9 else i]
    for ch, (m, i) in FIXED.items():
        pal[ch] = mat[m][i]
    return pal


PLATE = {
    "helmet": ("""
................
....oooooooo....
..oo#MmMmMwwoo..
.o#MmMmMmMmwwwo.
o#MmMmMmMmMmMwwo
o#MMmMmMmMmMmwMo
o#MmMmMmMmMmMmMo
oVYyyyyGyyyyyyvo
o#MmMVvvvvvvvVmo
o#mMmo........Mo
o#MmMo........mo
o#mMmo........Mo
o#MmMo........mo
.o#mMo........Mo
.o#MMo..........
..o#Mo..........
...oo...........
................
""", (0, 0)),
    "torso": """
..oVYyYvo..
.oMMmmmwwo.
oYMmmmmmwYo
o#Mmmwmmmwo
o#Mmmwmmmwo
o#Mmmwmmwwo
o#MMmwmmwwo
o#MMmwmmMwo
o#MMMwMMMmo
oVYYYGYYYVo
oVyyyyyyyvo
o#MmmMmmmwo
o#Mmmommmwo
ooooooooooo
""",
    "torso_side": """
...oYyvo...
..oMmmwwo..
..o#Mmmwwo.
..o#Mmmwwo.
..o#Mmwmwo.
..o#Mmwmwo.
..o#MMwmwo.
..o#MMwmwo.
..o#MMMmwo.
..oVYYGYvo.
..oVyyyyvo.
..o#Mmmmwo.
..o#Mmmmwo.
..oooooooo.
""",
    "torso_back": """
..oVYYYvo..
.oMMmmmwwo.
oYMMmmmmwYo
o#MMm#mmwwo
o#MMm#mmwwo
o#MMm#mmwwo
o#MMm#mmwwo
o#MMm#mmwwo
o#MMm#mmMwo
oVYYYYYYYVo
oVyyyyyyyvo
o#MmmMmmmwo
o#Mmmommmwo
ooooooooooo
""",
    "upper_arm": ("""
.VYv.
#MmwY
#Mmwv
.Mmw.
.Mmw.
.Mmw.
.Mmw.
.VYv.
""", (2, 0)),
    "upper_arm_short": ("""
.VYv.
#MmwY
#Mmwv
.Mmw.
.VYv.
""", (2, 0)),
    "hand": """
VYv
Mmw
Mmw
oMo
""",
    "bracer": """
VYv
Mmw
Mmw
Mmw
""",
    "shin": """
VYyv
#Mmw
#Mmw
#Mww
#Mmw
#Mmw
#Mmw
VYyv
#Mmw
""",
    "foot": """
o#Mmo.
o#Mmwo
oooooo
""",
}

LEATHER = {
    "helmet": ("""
................
....oooooooo....
..oo#MMmmmmwoo..
.o#MMmmmmmmwwwo.
o#MMmmmmmmmmwwwo
o#MmmMmmmmmmmwMo
o#MmmmMmmmmmmmwo
o#MmmmMYyyyyyyvo
o#MmmYv.......vo
o#MmMv........wo
o#mMMv........Mo
o#MmMv........wo
o#mMMv........Mo
o#MMMv........o.
.o#MMv..........
..o#Mo..........
...oo...........
................
""", (0, 0)),
    "torso": """
..oVYYYvo..
.o#MPpqMwo.
o#MMPpqmwwo
o#MmPpqmmwo
o#MmPpqmmwo
o#Mm#ppmmwo
o#MmP#qmmwo
o#MmPp#mmwo
o#MmPpq#mwo
oVYYYGYYYvo
o#MmmMmmmwo
o#MmmMmmmwo
o#Mmmommmwo
ooooooooooo
""",
    "torso_side": """
...oVYvo...
..o#Mpqwo..
..o#MMpqwo.
..o#Mmpqwo.
..o#Mmpqwo.
..o#Mm#qwo.
..o#Mmp#wo.
..o#Mmpqwo.
..o#Mmpqwo.
..oVYYGYvo.
..o#MmMmwo.
..o#MmMmwo.
..o#Mmomwo.
..oooooooo.
""",
    "torso_back": """
..oVYYYvo..
.o#MMmmmwo.
o#MMmmmmmwo
o#MMm#mmmwo
o#MMmm#mmwo
o#MMmmm#mwo
o#MMmmmm#wo
o#MMmmmmmwo
o#MMmmmmmwo
oVYYYYYYYvo
o#MmmMmmmwo
o#MmmMmmmwo
o#Mmmommmwo
ooooooooooo
""",
    "upper_arm": """
VYv
Mmw
Mmw
Mmw
Mmw
Mmw
Mmw
Mmw
""",
    "upper_arm_short": """
VYv
Mmw
Mmw
Mmw
Mmw
""",
    "hand": """
Mmw
Mmw
Mmw
oMo
""",
    "bracer": """
VYv
Mmw
#M#
Mmw
""",
    "shin": """
VYyv
#Mmw
#Mmw
#Mmw
#Ymw
#Mmw
#Ymw
#Mmw
#Mmw
""",
    "foot": """
o#Mmo.
o#Mmwo
oooooo
""",
}

CLOTH = {
    "helmet": ("""
...oo.............
..o#Mo............
...o#Mo...........
....o#Mmo.........
....o#Mmmwo.......
...o#MmmmmwVo.....
..o#MmmmmmmmVo....
.o#MmmmmmmmmmYo...
o#MMmmmmmmmmmmwo..
oVYyyyyyyyGyyyyyvo
.oVvvvvvvvvvvvvVo.
................o.
..................
..................
..................
..................
..................
..................
..................
..................
""", (1, 4)),
    "torso": """
..oVYYYvo..
.o#MmYmmwo.
o#MMmYmmmwo
o#MmmYmmmwo
o#MmmYmmmwo
o#MmmYmmmwo
o#MmmYmmmwo
o#MmmYmmmwo
o#MmmYmmmwo
o#BbbGbbbno
o#MmmYmmmwo
o#MmmYmmmwo
o#MmmYmmmwo
o#MmmYmmmwo
o#MmmYmmmwo
o#MmmYmmmwo
oVYYYYYYYvo
ooooooooooo
""",
    "torso_side": """
...oVYvo...
..o#MYmwo..
..o#MmYmwo.
..o#MmYmwo.
..o#MmYmwo.
..o#MmYmwo.
..o#MmYmwo.
..o#MmYmwo.
..o#MmYmwo.
..o#bbGbno.
..o#MmYmwo.
..o#MmYmwo.
..o#MmYmwo.
..o#MmYmwo.
..o#MmYmwo.
..o#MmYmwo.
..oVYYYYvo.
..oooooooo.
""",
    "torso_back": """
..oVYYYvo..
.o#MMmmmwo.
o#MMmmmmmwo
o#MMm#mmmwo
o#MMm#mmmwo
o#MMm#mmmwo
o#MMm#mmmwo
o#MMm#mmmwo
o#MMm#mmmwo
o#Bbbbbbbno
o#MMm#mmmwo
o#MMm#mmmwo
o#MMm#mmmwo
o#MMm#mmmwo
o#MMm#mmmwo
o#MMm#mmmwo
oVYYYYYYYvo
ooooooooooo
""",
    "upper_arm": """
Mmw
Mmw
Mmw
Mmw
Mmw
Mmw
Mmw
VYv
""",
    "upper_arm_short": """
Mmw
Mmw
Mmw
Mmw
VYv
""",
    "hand": """
VYv
Mmw
Sss
oSo
""",
    "bracer": """
Mmw
VYv
Mmw
Mmw
""",
    "shin": """
VYyv
#Mmw
#Mmw
VYyv
#Mmw
#Mmw
VYyv
#Mmw
#Mmw
""",
    "foot": """
o#Mmo.
o#Mmwo
oooooo
""",
}

PLATE["coat_tail"] = """
oMMwo.
oMmwo.
oMmmwo
oVYyvo
.oooo.
"""
LEATHER["coat_tail"] = """
oMMwo..
oMMwo..
oMMmwo.
oMMmwo.
.oMMwo.
.oMMmwo
..oVYvo
..ooooo
"""
CLOTH["coat_tail"] = """
oMMwo...
oMMwo...
oMMmwo..
oMMmwo..
.oMMwo..
.oMMmwo.
..oMMwo.
..oMMmwo
...oVYvo
...ooooo
"""

for _set, _fore in ((PLATE, """
Mmw
VYv
ooo
#Mw
Mmw
#Mw
"""), (LEATHER, """
Mmw
Mmw
ooo
Mmw
#Mw
Mmw
"""), (CLOTH, """
Mmw
Mmw
Mmw
Mmw
VYv
Mmw
""")):
    _set["forearm"] = _fore
    _set["elbow_cap"] = """
Mm
Mw
"""

SETS = {"plate": PLATE, "leather": LEATHER, "cloth": CLOTH}
SLOT_PIECES = {"helmet": ["helmet"], "chest": ["torso", "torso_side", "torso_back", "upper_arm", "upper_arm_short", "coat_tail", "forearm", "elbow_cap"],
               "gloves": ["hand", "bracer"], "boots": ["shin", "foot"]}
PIVOTS = {"torso": [5, 13], "torso_side": [5, 13], "torso_back": [5, 13], "upper_arm": [1, 0], "upper_arm_short": [1, 0],
          "hand": [1, 1], "bracer": [1, -2], "coat_tail": [2, 0], "forearm": [1, 0], "elbow_cap": [0.5, 0.5], "shin": [2, 0], "foot": [1, 0]}
HEAD_PIVOT = (9, 17)


def piece(material, name):
    entry = SETS[material][name]
    if name == "helmet":
        txt, (dx, dy) = entry
        return txt, [HEAD_PIVOT[0] + dx, HEAD_PIVOT[1] + dy]
    if isinstance(entry, tuple):
        txt, pivot = entry
        return txt, list(pivot)
    return entry, PIVOTS[name]


def letter_color(ch, pal):
    return pal.get(ch)
