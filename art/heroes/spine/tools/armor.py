MAIN = {"#": 0, "M": 1, "m": 2, "w": 3}
TRIM = {"V": 0, "Y": 1, "y": 2, "v": 3}
GEM = {"G": 1, "c": 2, "C": 3}
FABRIC = {"D": 0, "T": 1, "t": 2, "g": 3}
FUR = {"J": 0, "K": 1, "k": 2, "j": 3}
FIXED = {"B": ("leather", 1), "b": ("leather", 2), "n": ("leather", 3), "N": ("leather", 0),
         "P": ("shirt", 1), "p": ("shirt", 2), "q": ("shirt", 3),
         "S": ("skin", 1), "s": ("skin", 2), "x": ("skin", 0)}
SHADOW = (40, 30, 42)
from armor_acc import EXTRA
FUR_TONES = [(132, 112, 96), (190, 174, 150), (224, 212, 192), (246, 240, 228)]

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
TINT = {1: 0.0, 2: 0.0, 3: 0.0, 4: 0.14, 5: 0.18, 6: 0.26, 7: 0.28, 8: 0.3, 9: 0.3, 10: 0.45, 11: 0.4}


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


def tinted(base, r):
    trim = trim_tones(r)
    return [mix(b, t, TINT[r]) for b, t in zip(base, trim)]


def palette(base, r, mat):
    material = base.split("_")[0]
    trim = trim_tones(r)
    cloth = cloth_tones(r)
    main = {"plate": tinted(mat["metal"], r), "leather": tinted(mat["leather"], r), "cloth": cloth}[material]
    fabric = [scale(c, 0.62) for c in cloth] if material == "cloth" else cloth
    edge = GOLD if material == "cloth" else trim
    pal = {"z": SHADOW, "W": mix(main[3], (255, 255, 255), 0.55), "E": trim[3]}
    for table, tones in ((MAIN, main), (TRIM, edge), (FABRIC, fabric), (FUR, FUR_TONES)):
        for ch, i in table.items():
            pal[ch] = tones[i]
    for ch, i in GEM.items():
        pal[ch] = trim[i] if material != "cloth" else trim[min(3, i + 1) if r < 9 else i]
    for ch, (m, i) in FIXED.items():
        pal[ch] = mat[m][i]
    pal.update(EXTRA)
    return pal


PLATE_EARLY = {
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
.o#MmMo.........
..o#mMmo........
...o#MMo........
....ooo.........
""", (0, 0)),
    "torso": """
..o#MmMwo..
.o#MmMmMwo.
o#mMmMmMmwo
o#MmMmMmMwo
o#mMmMmMmwo
o#MmMmMmMwo
o#mMmMmMmwo
o#MmMmMmMwo
o#mMmMmMmwo
oNBBbYbbbno
o#MmMmMmMwo
o#mMmMmMmwo
o#MmMmMmMwo
o#mMmMmMmwo
oVYyYyYyYvo
""",
    "torso_side": """
...o#Mwo...
..o#MmMwo..
..o#mMmMwo.
..o#MmMmwo.
..o#mMmMwo.
..o#MmMmwo.
..o#mMmMwo.
..o#MmMmwo.
..o#mMmMwo.
..oNBYbbno.
..o#MmMmwo.
..o#mMmMwo.
..o#MmMmwo.
..o#mMmMwo.
..oVYyYyvo.
""",
    "torso_back": """
..o#MmMwo..
.o#MmMmMwo.
o#mMmMmMmwo
o#MmMmMmMwo
o#mMmMmMmwo
o#MmMmMmMwo
o#mMmMmMmwo
o#MmMmMmMwo
o#mMmMmMmwo
oNBBbbbbbno
o#MmMmMmMwo
o#mMmMmMmwo
o#MmMmMmMwo
o#mMmMmMmwo
oVYyYyYyYvo
""",
    "upper_arm": "mMw\nMmw\nmMw\nMmw\nmMw\nMmw\nmMw\nVYv",
    "upper_arm_short": "mMw\nMmw\nmMw\nMmw\nVYv",
    "forearm": "mMw\nMmw\nmMw\nMmw\nmMw\nVYv",
    "elbow_cap": "mM\nMw",
    "coat_tail": """
o#Mwo.
o#mMo.
o#MmMo
o#mMwo
oVYyvo
""",
    "hand": "#Mw\nMmw\nMmw\noMo",
    "bracer": "VYv\nMmw\nMmw\nMmw",
    "shin": "VYyv\n#Mmw\n#MmW\n#Mmw\n#Mmw\n#MmW\n#Mmw\nVYyv\n#Mmw",
    "foot": "o#Mmo.\no#MmWo\noooooo",
}

PLATE_LATE = {
    "helmet": ("""
.o................o.
oWo..............oWo
oMwo............owMo
.oMwo.oooooooo.owMo.
..oMwo#MMmmmwwowMo..
.o#MMmmmmmmmmmwwWwo.
o#MMmmmmmmmmmmmwwWwo
oVYyyyyGyyyyyyyyyyvo
o#MMmmmmmmmmmmmmwwMo
o#MmMmMMmmmmmmmmwwMo
o#mMmMoVvvYvvvVoMmMo
o#MmMmo....#w#..oMo.
o#mMmMo....#w#..oMo.
o#MmMmo....#w#..oMo.
o#mMmMo.....#...oMo.
.o#MmMo.........oMo.
.o#mMmMo............
..o#MmMo............
...o#Mmo............
....ooo.............
""", (2, 2)),
    "torso": """
..oVYyYvo..
.o#MmmmwWo.
o#MMmmmmwWo
o#MDTTTtwwo
o#MDTyTtwwo
o#MDyyytwwo
o#MDTyTtwwo
o#MDTyTtmwo
o#MDTTTtmwo
oVYYYGYYYvo
o#MDTTTtwMo
o#mDTTTtmMo
o#MDTTTtmMo
oo.DTTTt.oo
...DTTTt...
...DTTTg...
...VYYYv...
""",
    "torso_side": """
...oVYvo...
..o#Mmwwo..
..o#MmmwWo.
..o#MDTtwo.
..o#MDytwo.
..o#Myyywo.
..o#MDytwo.
..o#MDTtwo.
..o#MDTtwo.
..oVYGYYvo.
..o#MDTtwo.
..o#MDTtwo.
..o#MDTtwo.
..ooDTTtoo.
....DTTt...
....DTTg...
....VYYv...
""",
    "torso_back": """
..oVYYYvo..
.oYDTTTtYo.
oDDTTTTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oVYYYYYYYvo
""",
    "upper_arm": (".VYyyv.\nVMmwwWv\n#MmmwwM\n.#MmwM.\n..Mmw..\n..Mmw..\n..Mmw..\n..VYv..", (3, 0)),
    "upper_arm_short": (".VYyyv.\nVMmwwWv\n#MmmwwM\n.#MmwM.\n..VYv..", (3, 0)),
    "forearm": "Mmw\nVYv\nooo\n#Mw\nMmW\n#Mw",
    "elbow_cap": "Wm\nMw",
    "coat_tail": """
oMMwo.
oMmwo.
oMmmwo
oVYyvo
.oooo.
""",
    "cape": ("""
..oVYYYYYvo..
..oDTTTTTtgo.
.oDTTTTTTtgo.
.oDTTtTTTtgo.
.oDTTtTTTTgo.
oDTTTtTTTtgo.
oDTTtTTTTtgo.
oDTTtTTTTtTgo
oDTTtTTTtTTgo
oDTTtTTTtTTgo
oDTtTTTTtTTgo
oDTtTTTTtTTgo
oDTtTTTtTTTgo
oDTtTTTtTTTgo
oDTtTTTtTTtgo
oDtTTTTtTTtgo
oDtTTTtTTTtgo
oDtTTTtTTTtgo
oVYyYYyYYyYvo
ooooooooooooo
""", (6, 0)),
    "hand": ("VYyv\nMmwW\nMmmW\n#MmM\noMMo", (1, 1)),
    "bracer": ("VYyv\nMmwW\nMmw.\nMmw.", (1, -2)),
    "shin": "VWwv\n#Mmw\n#MmW\n#Mmw\nVYyv\n#Mmw\n#MmW\n#Mmw\nVYyv",
    "foot": "o#Mmwo.\no#MmmWo\nooooooo",
}

LEATHER_EARLY = {
    "helmet": ("""
................
....oooooooo....
..oo#MMmmmmwoo..
.o#MMmmmmmmmwwo.
o#MMmmmmmmmmmmwo
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
..o#Mmwo...
oKko#mwoKjo
oKkMPpqmKjo
o#KMPpqmjwo
o#MmPpqmmwo
o#MmN#qmmwo
o#MmPN#mmwo
o#MmPpN#mwo
o#MmPpqN#wo
oNBBbYbbbno
o#MmmNmmmwo
o#MmmNmmmwo
oKkKkKkKkjo
ooooooooooo
""",
    "torso_side": """
..oKkKjo...
..oKkkkjo..
..o#MKqwo..
..o#MPqwwo.
..o#Mpqmwo.
..o#M#qmwo.
..o#MN#mwo.
..o#MpN#wo.
..o#Mpqmwo.
..oNBYbbno.
..o#MNmmwo.
..o#MNmmwo.
..o#MNmMwo.
..oooooooo.
""",
    "torso_back": """
.oKkKkKjo..
oKkkkkkkjjo
o#MKkkkKmwo
o#MMm#mmmwo
o#MMmm#mmwo
o#MMmmm#mwo
o#MMmmmm#wo
o#MMmmmmmwo
o#MMmmmmmwo
oNBBbbbbbno
o#MmmMmmmwo
o#MmmMmmmwo
o#MmmMmmMwo
ooooooooooo
""",
    "upper_arm": "KkJ\nMmw\nMmw\nMmw\nMmw\nMmw\nMmw\nVYv",
    "upper_arm_short": "KkJ\nMmw\nMmw\nMmw\nVYv",
    "forearm": "Mmw\nMmw\nooo\nMmw\n#Mw\nMmw",
    "elbow_cap": "Mm\nMw",
    "coat_tail": """
oMMwo..
oMMwo..
oMMmwo.
oMMmwo.
.oMMwo.
.oMMmwo
..oVYvo
..ooooo
""",
    "hand": "Mmw\nMmw\nMmw\noMo",
    "bracer": "KkJ\nKkj\n#M#\nMmw",
    "shin": "KkkJ\nKkjJ\n#Mmw\n#Mmw\n#Ymw\n#Mmw\n#Ymw\n#Mmw\n#Mmw",
    "foot": "o#Mmo.\no#Mmwo\noooooo",
}

LEATHER_LATE = {
    "helmet": ("""
................
....oooooooo....
..oo#MMmmmmwoo..
.o#MMmmmmmmmwwo.
o#MMmmmmmmmmmmwo
o#MmmMmmmmmmmmwo
o#MmmmMmmmmmmmMo
o#MmmmM#mmmmmmMo
o#MmmM#zzzzzzzzo
o#MmMz.........o
o#MmM.........Mo
o#MmM.........Mo
o#MmM.........Mo
o#MmDTTTtTTTTTgo
.o#DTTTTtTTTTgo.
..oDTTTTTtTTgo..
...oDTTTTTtgo...
....oooooooo....
""", (0, 0)),
    "torso": """
..oDTTTgo..
.oDTTTTTgo.
o#MDTTTgMwo
o#MMDTgMmwo
o#MmNDgNmwo
o#MmmNNmmwo
o#MmNmmNmwo
o#MNmmmmNwo
o#MmmmmmmWo
oNBYbbbYbno
o#MBBMmBBwo
o#MBbMmBbwo
o#MmmommMwo
ooooooooooo
""",
    "torso_side": """
...oDTgo...
..oDTTTgo..
..o#DTgMwo.
..o#MDgmwo.
..o#MNNmwo.
..o#NmmNwo.
..o#MmmNwo.
..o#MmmmWo.
..o#MmmmWo.
..oNBYbbno.
..o#BBmmwo.
..o#Bbmmwo.
..o#Mmomwo.
..oooooooo.
""",
    "torso_back": """
..oDTTTgo..
.oDTTTTTgo.
o#MDTTTgMwo
o#MMmDgmmwo
o#MmmDgmmwo
o#MmmDgmmwo
o#MmmmDgmwo
o#MmmmDgmwo
o#MmmmmDgwo
oNBBbbbbbno
o#MmmMmmmwo
o#MmmMmmmwo
o#MmmommMwo
ooooooooooo
""",
    "upper_arm": ("#MmwW\n.Mmw.\n.Mmw.\n.Mmw.\n.Mmw.\n.Mmw.\n.Mmw.\n.VYv.", (2, 0)),
    "upper_arm_short": ("#MmwW\n.Mmw.\n.Mmw.\n.Mmw.\n.VYv.", (2, 0)),
    "forearm": "DTg\nDTg\nooo\nMmw\nNnN\nMmw",
    "elbow_cap": "Tt\nTg",
    "coat_tail": """
oDTgo..
oDTTgo.
.oDTgo.
.oDTTgo
..oDTgo
..oDtgo
...oTgo
...ooo.
""",
    "cape": ("""
.oDTgo.
.oDTTgo
..oDTgo
..oDTTg
...oDTg
...oDtg
....oTg
....oVv
.....oo
""", (2, 0)),
    "hand": "Mmw\nNnN\nSss\noSo",
    "bracer": "NnN\nMmw\nNnN\nMmw",
    "shin": "DTTg\n#Mmw\nNnnN\n#Mmw\nNnnN\n#Mmw\nNnnN\n#Mmw\n#Mmw",
    "foot": "o#Mmo.\no#MmWo\noooooo",
}

CLOTH_EARLY = {
    "helmet": ("""
...oo.............
..o#Mo............
...o#Mo...........
....o#Mmo.........
....o#MCmwo.......
...o#MmmmmwVo.....
..o#MmmCmmmmVo....
.o#MmmmmmmCmmYo...
o#MMmmmmmmmmmmwo..
oVYyyyyyyyGyyyyyvo
.oVvvvvvvvvvvvvVo.
................o.
""", (1, 4)),
    "torso": """
..oVYYYvo..
.o#MYpYmwo.
o#MMmYmmmwo
o#MmmYmmmwo
o#MCmYmmmwo
o#MmmYmCmwo
o#MmmYmmmwo
o#MmmYmmmwo
o#MmmYmmmwo
o#BbbGbbbno
o#MmmYmmmwo
o#MmmYmmBwo
o#MmmYmmBwo
o#MmmYmmYwo
o#MmmYmmmwo
o#MCmYmCmwo
oVYyYYYyYvo
ooooooooooo
""",
    "torso_side": """
...oVYvo...
..o#MYmwo..
..o#MmYmwo.
..o#MmYmwo.
..o#MCYmwo.
..o#MmYmwo.
..o#MmYmwo.
..o#MmYmwo.
..o#MmYmwo.
..o#bbGbno.
..o#MmYBwo.
..o#MmYBwo.
..o#MmYYwo.
..o#MmYmwo.
..o#MmYmwo.
..o#MCYCwo.
..oVYYYYvo.
..oooooooo.
""",
    "torso_back": """
..oVYYYvo..
.o#MMmmmwo.
o#MMmmmmmwo
o#MMm#mmmwo
o#MMm#mmmwo
o#MCm#mmmwo
o#MMm#mCmwo
o#MMm#mmmwo
o#MMm#mmmwo
o#Bbbbbbbno
o#MMm#mmmwo
o#MMm#mmmwo
o#MMm#mmmwo
o#MMm#mmmwo
o#MMm#mmmwo
o#MCm#mCmwo
oVYyYYYyYvo
ooooooooooo
""",
    "upper_arm": "Mmw\nMmw\nMmw\nMmw\nMCw\nMmw\nMmw\nVYv",
    "upper_arm_short": "Mmw\nMmw\nMmw\nMmw\nVYv",
    "forearm": ("Mmw.\nMmww\nMmmw\nMmmw\nVYyv\n....", (1, 0)),
    "elbow_cap": "Mm\nMw",
    "coat_tail": """
oMMwo...
oMMwo...
oMMmwo..
oMCmwo..
.oMMwo..
.oMMmwo.
..oMMwo.
..oMMmwo
...oVYvo
...ooooo
""",
    "hand": "VYv\nMmw\nSss\noSo",
    "bracer": "Mmw\nVYv\nMmw\nMmw",
    "shin": "VYyv\n#Mmw\n#Mmw\nVYyv\n#Mmw\n#Mmw\nVYyv\n#Mmw\n#Mmw",
    "foot": "o#Mmo.\no#MmCo\noooooo",
}

CLOTH_LATE = {
    "helmet": ("""
......oo........
....oo#Moooo....
..oo#MMmmmmwoo..
.o#MMmmmmmmmwwo.
o#MMmmmmmmmmmmwo
o#MmmmmmmmmmmmMo
o#MmVYYyGyyyyvMo
o#MmMzzzzzzzzzMo
o#MmMz.......zMo
o#MmM.........Mo
o#MmM.........Mo
o#MmM.........Mo
o#MmM.........Mo
o#MMm........oMo
o#MMmo.......oo.
.o#MMmo.........
..oo#Mo.........
....oo..........
""", (0, 0)),
    "torso": """
..oVYYYvo..
.oDTTTTTgo.
oDTTTTTTTgo
oDTTTYTTTgo
oVYTTTTTYvo
o#MmmYmmmwo
o#MCmYmCmwo
o#MmmYmmmwo
o#MmmYmmmwo
oNBBbGbbbno
o#MmmYmCmwo
o#MCmYmmmwo
o#MmmYmmmwo
o#MmmYmmCwo
o#MCmYmmmwo
o#MmmYmmmwo
o#MmmYmmmwo
oVYyYYYyYvo
ooooooooooo
""",
    "torso_side": """
...oVYvo...
..oDTTTgo..
..oDTTTTgo.
..oDTYTTgo.
..oVYTTYvo.
..o#MYmmwo.
..o#MYmCwo.
..o#MYmmwo.
..o#MYmmwo.
..oNBGbbno.
..o#MYmCwo.
..o#MYmmwo.
..o#MYmmwo.
..o#MYmmwo.
..o#MYmCwo.
..o#MYmmwo.
..o#MYmmwo.
..oVYYYYvo.
..oooooooo.
""",
    "torso_back": """
..oVYYYvo..
.oDTTTTTgo.
oDTTTTTTTgo
oDTTTTTTTgo
oVYTTTTTYvo
oDTTTtTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oDTTTtTTtgo
oVYYYYYYYvo
ooooooooooo
""",
    "upper_arm": ("DTTg\nDTTg\nMmw.\nMmw.\nMCw.\nMmw.\nMmw.\nVYv.", (1, 0)),
    "upper_arm_short": ("DTTg\nDTTg\nMmw.\nMmw.\nVYv.", (1, 0)),
    "forearm": ("Mmw..\nMmww.\nMmmww\nMmCmw\nVYyyv", (1, 0)),
    "elbow_cap": "Mm\nMw",
    "coat_tail": """
oDTgo...
oDTgo...
oDTTgo..
oDTTgo..
.oDTgo..
.oDTTgo.
..oDTgo.
..oDTTgo
...oVYvo
...ooooo
""",
    "cape": ("""
..oVYYYYYvo..
..oDTTTTTtgo.
.oDTTTTTTtgo.
.oDTTtTTTtgo.
.oDTCtTTTTgo.
oDTTTtTTTtgo.
oDTTtTTCTtgo.
oDTTtTTTTtTgo
oDCTtTTTtTTgo
oDTTtTTTtTTgo
oDTtTTTTtTCgo
oDTtTTTTtTTgo
oDTtTCTtTTTgo
oDTtTTTtTTTgo
oDTtTTTtTTtgo
oDtTTTTtTTtgo
oDtTTCTtTTtgo
oDtTTTtTTTtgo
oDtTTTtTTTtgo
oDtTTTtTTTtgo
oVYyYYyYYyYvo
ooooooooooooo
""", (6, 0)),
    "hand": "VYv\nMCw\nSss\noSo",
    "bracer": "Mmw\nVYv\nMCw\nVYv",
    "shin": "VYyv\n#Mmw\n#MCw\n#Mmw\nVYyv\nSsss\nSsss\nNBbn\nSsss",
    "foot": "o#Sso.\noNBbno\noooooo",
}

def make_cape(top=9, bottom=17, length=27, stars=()):
    width = bottom + 2
    rows = []
    for i in range(length):
        w = round(top + (bottom - top) * (i / (length - 1)) ** 0.8)
        left = (width - w) // 2
        if i == 0:
            body = "o" + "VYYY" * w
            body = body[:w - 1] + "o"
        elif i == length - 2:
            body = "o" + ("VYyY" * w)[:w - 2] + "o"
        elif i == length - 1:
            body = "o" * w
        else:
            inner = []
            for j in range(w - 2):
                if j == 0:
                    inner.append("D")
                elif j == w - 3:
                    inner.append("g")
                elif (j + i // 6) % 4 == 2:
                    inner.append("t")
                else:
                    inner.append("T")
            if (i, ) and (i % 5 == 3) and stars:
                k = (i * 3) % max(1, w - 4) + 1
                inner[k] = "C"
            body = "o" + "".join(inner) + "o"
        rows.append("." * left + body + "." * (width - left - len(body)))
    return "\n".join(rows), (width // 2, 0)


PLATE_LATE["cape"] = make_cape()
CLOTH_LATE["cape"] = make_cape(stars=True)
LEATHER_LATE["cape"] = ("""
.......oDTo
......oDTTo
......oDTo.
.....oDTTo.
.....oDTo..
....oDTTo..
....oDTo...
...oDTTo...
...oDTo....
..oDTTo....
..oDTo.....
.oDTgo.....
.oDTo......
oDTgo......
oDTo.......
oTgo.......
oVvo.......
.oo........
""", (9, 0))

PLATE_LATE["upper_arm"] = ("..VYyyv..\n.VMmwwWv.\nVMMmmwwWv\n#MMmmmwwM\n.#MMmwwM.\n..VYyYv..\n...Mmw...\n...Mmw...\n...VYv...", (4, 1))
PLATE_LATE["upper_arm_short"] = ("..VYyyv..\n.VMmwwWv.\nVMMmmwwWv\n#MMmmmwwM\n.#MMmwwM.\n..VYyYv..\n...VYv...", (4, 1))
PLATE_LATE["hand"] = ("VYyyv\nMmmwW\nMmmwW\n#MmmM\n.oMo.", (2, 1))
PLATE_LATE["bracer"] = ("VYyyv\nMmmwW\n.Mmw.\n.Mmw.", (2, -2))
PLATE_LATE["shin"] = (".VWwv.\nVMmwWv\n.#Mmw.\n.#MmW.\n.VYyv.\n.#Mmw.\n.#MmW.\n.#Mmw.\n.VYyv.", (3, 0))
PLATE_LATE["foot"] = ("o#MMmwo.\no#MmmwWo\noooooooo", (1, 0))
PLATE_LATE["torso"] = ("\n".join("." + r + "." for r in PLATE_LATE["torso"].strip("\n").split("\n")[:10]) + """
oWMMDTTTtMmwo
o#MMDTTTtMmwo
oVYYDTTTtYYvo
o#MMDTTTtMmwo
oVYvDTTTtVYvo
ooo.DTTTg.ooo
....VYYYv....
""", (6, 13))
LEATHER_LATE["upper_arm"] = (".VYyv.\n#MmmwW\n.#MmwW\n..VYv.\n..Mmw.\n..Mmw.\n..Mmw.\n..Mmw.\n..VYv.", (3, 0))
LEATHER_LATE["upper_arm_short"] = (".VYyv.\n#MmmwW\n.#MmwW\n..VYv.\n..Mmw.\n..VYv.", (3, 0))
LEATHER_LATE["hand"] = ("Mmww\nNnnN\nSss.\noSo.", (1, 1))
LEATHER_LATE["shin"] = ("DTTgg\n#MmwW\nNnnNN\n#Mmw.\nNnnN.\n#Mmw.\nNnnN.\n#Mmw.\n#Mmw.", (2, 0))
CLOTH_LATE["upper_arm"] = (".DTTg.\nDTTTTg\nDTTTtg\n.VYYv.\n..Mmw.\n..Mmw.\n..MCw.\n..Mmw.\n..VYv.", (3, 0))
CLOTH_LATE["upper_arm_short"] = (".DTTg.\nDTTTTg\nDTTTtg\n.VYYv.\n..VYv.", (3, 0))
PLATE_EARLY["upper_arm"] = (".mMm.\nmMmMw\n.mMw.\n.Mmw.\n.mMw.\n.Mmw.\n.mMw.\n.VYv.", (2, 0))
PLATE_EARLY["upper_arm_short"] = (".mMm.\nmMmMw\n.mMw.\n.Mmw.\n.VYv.", (2, 0))
LEATHER_EARLY["upper_arm"] = (".KkJ.\nKkkjJ\n.Mmw.\n.Mmw.\n.Mmw.\n.Mmw.\n.Mmw.\n.VYv.", (2, 0))
LEATHER_EARLY["upper_arm_short"] = (".KkJ.\nKkkjJ\n.Mmw.\n.Mmw.\n.VYv.", (2, 0))

SHIELDS = {
    "heater": ("""
oooooooo
oWDTTTgo
oMTTYTgo
oMTYYYgo
oMTTYTgo
oMTTYTgo
.oMTTgo.
.oMTtgo.
..oMgo..
...oo...
""", (4, 4)),
    "round": ("""
..oooo..
.oBbbno.
oBbYYbno
oBYMwYbo
oBYMMYbo
oBbYYbno
.oBbbno.
..oooo..
""", (4, 4)),
}

SETS = {"plate_early": PLATE_EARLY, "plate_late": PLATE_LATE, "leather_early": LEATHER_EARLY,
        "leather_late": LEATHER_LATE, "cloth_early": CLOTH_EARLY, "cloth_late": CLOTH_LATE}
BASE_NAMES = {
    "plate_early": ("Кольчужный капюшон", "Кольчуга", "Латные рукавицы", "Латные сапоги"),
    "plate_late": ("Шлем ярла", "Латы стража", "Кулаки титана", "Поступь великана"),
    "leather_early": ("Кожаный капюшон", "Куртка", "Перчатки лучника", "Мокасины"),
    "leather_late": ("Маска следопыта", "Доспех ветров", "Перчатки ловкача", "Сапоги бегуна"),
    "cloth_early": ("Колпак", "Мантия", "Митенки", "Туфли"),
    "cloth_late": ("Венец звездочёта", "Облачение провидца", "Перчатки чародея", "Сандалии мудреца"),
}
SLOT_PIECES = {"helmet": ["helmet"],
               "chest": ["torso", "torso_side", "torso_back", "upper_arm", "upper_arm_short", "forearm", "elbow_cap", "coat_tail", "cape"],
               "gloves": ["hand", "bracer"], "boots": ["shin", "foot"]}
PIVOTS = {"torso": [5, 13], "torso_side": [5, 13], "torso_back": [5, 13], "upper_arm": [1, 0], "upper_arm_short": [1, 0],
          "forearm": [1, 0], "elbow_cap": [0.5, 0.5], "coat_tail": [2, 0], "cape": [6, 0],
          "hand": [1, 1], "bracer": [1, -2], "shin": [2, 0], "foot": [1, 0]}
HEAD_PIVOT = (9, 17)


def pieces(base, slot):
    return [p for p in SLOT_PIECES[slot] if p in SETS[base]]


def piece(base, name):
    entry = SETS[base][name]
    if name == "helmet":
        txt, (dx, dy) = entry
        return txt, [HEAD_PIVOT[0] + dx, HEAD_PIVOT[1] + dy]
    if isinstance(entry, tuple):
        txt, pivot = entry
        return txt, list(pivot)
    return entry, PIVOTS[name]
