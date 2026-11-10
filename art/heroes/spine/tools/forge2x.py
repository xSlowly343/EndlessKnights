from pix import Canvas
from armor2x import piece, BODY_PIVOT

M3 = ("M", "m", "w")
L3 = ("N", "B", "b")
F3 = ("J", "K", "k")
T3 = ("D", "T", "t")
G3 = ("V", "Y", "y")
HELM_OFF = (2, 4)


def helm_canvas():
    return Canvas(36, 40)


def helm_piece(c):
    bx, by = BODY_PIVOT["helmet"]
    return c.text(), (bx + HELM_OFF[0], by + HELM_OFF[1])


def mail(c, mask):
    for x, y in mask:
        c.put(x, y, "m" if (x + y) % 2 else "M")
    for x, y in mask:
        if (x + y) % 4 == 0 and (x, y - 1) in mask:
            c.put(x, y, "w")
    c.outline(mask, "#")


def fluting(c, mask, xs):
    for x in xs:
        for (px, py) in mask:
            if px == x:
                c.put(px, py, "#")
            if px == x + 1:
                c.put(px, py, "W")


def crack(c, pts, ch="E", glow="L"):
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        c.line(x0, y0, x1, y1, ch)
    x, y = pts[len(pts) // 2]
    c.put(x, y, glow)


def rivets(c, pts, ch="Y"):
    for x, y in pts:
        c.put(x, y, ch)


def leather(c, mask):
    c.shade(mask, L3, edge="N")


def metal(c, mask, spec="W"):
    c.shade(mask, M3, spec=spec, edge="#")


def fur(c, mask):
    c.shade(mask, F3, edge="J")
    for x, y in mask:
        if (x * 3 + y * 5) % 7 == 0:
            c.put(x, y, "j")


def gold(c, mask):
    c.shade(mask, G3, edge="V")


def cloth(c, mask):
    c.shade(mask, T3, edge="D")


def arm_tube(c, x0, x1, y0, y1, fill):
    m = c.mask_rect(x0, y0, x1, y1)
    fill(c, m)
    return m


def leg_pieces(thigh_fill, shin_fill, knee_fill=None, thigh_extra=None, shin_extra=None, knee_r=4):
    out = {}
    c = Canvas(10, 20)
    m = c.mask_poly([(1, 0), (9, 0), (9, 16), (8, 20), (2, 20), (1, 16)])
    thigh_fill(c, m)
    if thigh_extra:
        thigh_extra(c, m)
    out["thigh"] = piece(c, "thigh", -1, -1)
    c = Canvas(10, 19)
    m = c.mask_poly([(1, 0), (9, 0), (9, 11), (8, 19), (2, 19), (1, 11)])
    shin_fill(c, m)
    if shin_extra:
        shin_extra(c, m)
    out["shin"] = piece(c, "shin", -1, 0)
    c = Canvas(2 * knee_r, 2 * knee_r)
    m = c.mask_ellipse(knee_r, knee_r, knee_r, knee_r)
    (knee_fill or shin_fill)(c, m)
    out["knee_cap"] = (c.text(), (knee_r - 0.5, knee_r - 0.5))
    return out


def boot_pieces(h, top_fill, foot_fill, cuff_fill=None, cuff=3, foot_w=14, toe="round", top_extra=None):
    out = {}
    c = Canvas(12, h)
    m = c.mask_poly([(1, 0), (11, 0), (11, h), (1, h)])
    top_fill(c, m)
    if cuff_fill and cuff:
        cm = c.mask_rect(0, 0, 12, min(cuff, h))
        cuff_fill(c, cm)
    if top_extra:
        top_extra(c, m)
    out["boot_top"] = piece(c, "boot_top", -2, 18 - h)
    w = 18 + (5 if toe == "point" else 0)
    c = Canvas(w, 8)
    if toe == "point":
        pts = [(0, 0), (11, 0), (14, 2), (w - 6, 4), (w, 5), (w, 6.5), (w - 4, 8), (0, 8)]
    elif toe == "claw":
        pts = [(0, 0), (11, 0), (14, 2), (w - 2, 3.5), (w, 6), (w, 8), (0, 8)]
    else:
        pts = [(0, 0), (11, 0), (14, 2), (w - 2, 3.5), (w, 5.5), (w, 8), (0, 8)]
    m = c.mask_poly(pts)
    foot_fill(c, m)
    edge = c.get(0, 4)
    for x in range(0, w):
        if (x, 7) in m:
            c.put(x, 7, edge if edge != "." else "#")
    if toe != "point":
        for x in (6, 7, 8):
            c.put(x, 7, ".")
        c.put(5, 6, edge)
        c.put(9, 6, edge)
    tip = max(x for x, y in m if y == 5)
    c.put(tip - 2, 4, "W")
    if toe == "claw":
        for x in range(w - 7, w, 3):
            c.put(x, 7, "W")
            c.put(x + 1, 6, "W")
    out["foot"] = piece(c, "foot", -3, -1)
    return out


def standard_limbs(fore_fill, hand_fill, cuff_fill, shin_fill, foot_fill, fore_extra=None, hand_extra=None, shin_extra=None, foot_w=14):
    out = {}
    c = Canvas(8, 12)
    m = arm_tube(c, 0, 8, 0, 12, fore_fill)
    if fore_extra:
        fore_extra(c, m)
    out["forearm"] = piece(c, "forearm", -1, 0)
    c = Canvas(10, 10)
    m = c.mask_poly([(1, 1), (8, 0), (10, 3), (10, 8), (7, 10), (2, 10), (0, 7)])
    hand_fill(c, m)
    if hand_extra:
        hand_extra(c, m)
    out["hand"] = piece(c, "hand", -2, -1)
    c = Canvas(10, 6)
    m = c.mask_poly([(1, 0), (9, 0), (10, 6), (0, 6)])
    cuff_fill(c, m)
    out["bracer"] = piece(c, "bracer", -2, 6)
    return out


def torso_canvas(w=26, h=37):
    c = Canvas(w, h)
    return c, (w - 22) // 2


def neck_fill(c):
    first = next(y for y in range(c.h) if any(c.get(x, y) != "." for x in range(c.w)))
    row = [c.get(x, first) for x in range(c.w)]
    xs = [x for x, ch in enumerate(row) if ch != "."]
    mid = (min(xs) + max(xs)) // 2
    for y in range(first):
        for x in range(mid - 6, mid + 7):
            c.put(x, y, row[x] if row[x] != "." else "#")


def torso_piece(c, o):
    neck_fill(c)
    return piece(c, "torso", -o, 0)


def side_piece(c, o):
    neck_fill(c)
    return piece(c, "torso_side", -o, 0)


def back_piece(c, o):
    neck_fill(c)
    return piece(c, "torso_back", -o, 0)


def pauldron_piece(c, ox, oy):
    return piece(c, "upper_arm", -ox, -oy)


def elbow(fill, r=4):
    c = Canvas(2 * r, 2 * r)
    m = c.mask_ellipse(r, r, r, r)
    fill(c, m)
    return c.text(), (r - 0.5, r - 0.5)


def tail(fill, w=10, h=16):
    c = Canvas(w, h)
    m = c.mask_poly([(0, 0), (w - 1, 0), (w, h), (1, h)])
    fill(c, m)
    return piece(c, "coat_tail", -0, 0)


def jarl_forge():
    pal = {"#": (36, 38, 46), "M": (78, 82, 94), "m": (120, 126, 140), "w": (168, 174, 186), "W": (220, 226, 234),
           "N": (54, 32, 22), "B": (96, 60, 38), "b": (132, 88, 56), "n": (168, 120, 80),
           "J": (70, 58, 48), "K": (120, 104, 86), "k": (166, 150, 128), "j": (206, 194, 172),
           "V": (100, 62, 26), "Y": (176, 116, 48), "y": (214, 160, 80), "v": (240, 200, 130),
           "u": (214, 202, 176), "i": (244, 238, 222), "U": (150, 138, 116), "z": (30, 24, 26),
           "D": (52, 46, 40), "T": (84, 74, 60), "t": (116, 104, 84), "g": (150, 136, 110)}
    P = {}
    c = helm_canvas()
    cap = c.mask_poly([(5, 20), (6, 13), (10, 7), (16, 3), (21, 3), (27, 7), (31, 13), (32, 20)])
    metal(c, cap)
    for x in range(5, 33):
        c.put(x, 19, "Y")
        c.put(x, 20, "V")
    c.line(19, 3, 19, 19, "Y")
    aven = c.mask_poly([(3, 20), (13, 20), (13, 36), (8, 39), (3, 36)])
    mail(c, aven)
    nas = c.mask_rect(22, 20, 25, 30)
    metal(c, nas)
    for side, x0, d in ((0, 6, -1), (1, 30, 1)):
        for i in range(9):
            x = x0 + d * (i // 2 + 1)
            y = 13 - i
            c.put(x, y, "u" if i < 7 else "i")
            c.put(x + d, y, "U" if i < 5 else "u")
    P["helmet"] = helm_piece(c)
    c, o = torso_canvas()
    body = c.mask_poly([(o + 1, 4), (o + 21, 4), (o + 22, 30), (o, 30)])
    mail(c, body)
    apron = c.mask_poly([(o + 5, 9), (o + 17, 9), (o + 18, 36), (o + 4, 36)])
    leather(c, apron)
    c.line(o + 5, 9, o + 1, 4, "N")
    c.line(o + 17, 9, o + 21, 4, "N")
    belt = c.mask_rect(o, 19, o + 22, 22)
    leather(c, belt)
    rivets(c, [(o + 10, 20), (o + 11, 20), (o + 10, 21), (o + 11, 21)], "Y")
    for x in range(o + 6, o + 17, 3):
        c.put(x, 30, "n")
    P["torso"] = torso_piece(c, o)
    c, o = torso_canvas(22)
    body = c.mask_poly([(4, 2), (18, 2), (18, 30), (4, 30)])
    mail(c, body)
    apron = c.mask_rect(12, 9, 19, 36)
    leather(c, apron)
    belt = c.mask_rect(4, 19, 18, 22)
    leather(c, belt)
    P["torso_side"] = side_piece(c, 0)
    c, o = torso_canvas()
    body = c.mask_poly([(o + 1, 2), (o + 21, 2), (o + 22, 30), (o, 30)])
    mail(c, body)
    c.line(o + 11, 3, o + 11, 18, "N")
    belt = c.mask_rect(o, 19, o + 22, 22)
    leather(c, belt)
    P["torso_back"] = back_piece(c, o)
    for short in (False, True):
        c = Canvas(16, 18 if short else 22)
        sleeve = c.mask_rect(5, 4, 11, c.h)
        mail(c, sleeve)
        pad = c.mask_ellipse(8, 5, 8, 5)
        fur(c, pad)
        P["upper_arm_short" if short else "upper_arm"] = pauldron_piece(c, 5, 2)
    P["elbow_cap"] = elbow(leather)
    P["coat_tail"] = tail(leather)
    P.update(standard_limbs(leather, leather, fur, leather, leather,
                            fore_extra=lambda c, m: [c.put(x, y, "Y") for x, y in m if y in (2, 9) and 0 < x < 7],
                            shin_extra=lambda c, m: [fur(c, c.mask_rect(1, 0, 11, 5))]))
    tools = Canvas(8, 11)
    tm = tools.mask_rect(1, 0, 3, 10)
    tools.shade(tm, ("M", "m", "w"), edge="#")
    hm = tools.mask_rect(4, 0, 8, 3)
    metal(tools, hm)
    hh = tools.mask_rect(5, 3, 7, 10)
    leather(tools, hh)
    acc = {"belt_front": (tools.text(), (-6.5, -0.5))}
    P.update(leg_pieces(cloth, cloth, knee_fill=cloth,
                        shin_extra=lambda c, m: [c.line(1, y, 9, y + 4, "N", m) for y in range(0, 16, 4)] + [c.line(9, y, 1, y + 4, "B", m) for y in range(2, 16, 4)]))
    P.update(boot_pieces(6, leather, leather, cuff_fill=fur, cuff=3))
    return {"palette": pal, "pieces": P, "accessories": acc}


def mountain_hammer():
    pal = {"#": (30, 34, 40), "M": (70, 78, 90), "m": (104, 114, 128), "w": (146, 156, 170), "W": (196, 204, 214),
           "N": (50, 32, 22), "B": (92, 60, 36), "b": (128, 88, 54), "n": (162, 120, 76),
           "V": (112, 78, 28), "Y": (196, 150, 56), "y": (232, 196, 100), "v": (250, 230, 160),
           "E": (255, 214, 96), "L": (255, 250, 210), "z": (26, 22, 24), "Q": (168, 172, 184), "X": (90, 94, 106),
           "D": (70, 58, 40), "T": (112, 96, 66), "t": (150, 132, 92), "g": (186, 168, 124)}
    P = {}
    c = helm_canvas()
    cap = c.mask_poly([(4, 21), (6, 12), (11, 6), (18, 4), (25, 6), (30, 12), (32, 21)])
    c.shade(cap, ("V", "Y", "y"), spec="v", edge="V")
    brim = c.mask_poly([(2, 20), (34, 20), (35, 23), (1, 23)])
    c.shade(brim, ("V", "Y", "y"), edge="V")
    lamp = c.mask_ellipse(27, 14, 4, 4)
    c.shade(lamp, ("Y", "E", "L"), edge="#")
    c.put(28, 13, "L")
    for x in range(29, 36):
        c.put(x, 14, "E" if x % 2 else "L")
    strap = c.mask_rect(9, 23, 11, 36)
    leather(c, strap)
    P["helmet"] = helm_piece(c)
    c, o = torso_canvas(28)
    plate = c.mask_poly([(o + 1, 5), (o + 21, 5), (o + 23, 18), (o + 21, 22), (o + 1, 22), (o - 1, 18)])
    metal(c, plate)
    c.line(o + 6, 9, o + 16, 17, "#", plate)
    c.line(o + 7, 9, o + 17, 17, "W", plate)
    c.line(o + 15, 9, o + 9, 15, "#", plate)
    rivets(c, [(o + 2, 8), (o + 20, 8), (o + 2, 19), (o + 20, 19), (o + 11, 7)])
    belt = c.mask_rect(o - 1, 22, o + 23, 25)
    leather(c, belt)
    rivets(c, [(o + 11, 23), (o + 12, 23)])
    skirt = c.mask_poly([(o, 25), (o + 22, 25), (o + 23, 34), (o - 1, 34)])
    leather(c, skirt)
    for x in range(o + 2, o + 22, 4):
        c.line(x, 26, x, 33, "N", skirt)
    P["torso"] = torso_piece(c, o)
    c, o = torso_canvas(22)
    plate = c.mask_poly([(4, 4), (18, 4), (19, 18), (18, 22), (4, 22), (3, 18)])
    metal(c, plate)
    rivets(c, [(5, 8), (17, 8), (11, 6)])
    belt = c.mask_rect(3, 22, 19, 25)
    leather(c, belt)
    skirt = c.mask_rect(3, 25, 19, 34)
    leather(c, skirt)
    P["torso_side"] = side_piece(c, 0)
    c, o = torso_canvas(28)
    plate = c.mask_poly([(o + 1, 3), (o + 21, 3), (o + 23, 18), (o + 21, 22), (o + 1, 22), (o - 1, 18)])
    metal(c, plate)
    pick = c.mask_rect(o + 10, 2, o + 12, 21)
    leather(c, pick)
    head = c.mask_poly([(o + 4, 4), (o + 18, 2), (o + 18, 5), (o + 4, 6)])
    metal(c, head)
    belt = c.mask_rect(o - 1, 22, o + 23, 25)
    leather(c, belt)
    skirt = c.mask_poly([(o, 25), (o + 22, 25), (o + 23, 34), (o - 1, 34)])
    leather(c, skirt)
    P["torso_back"] = back_piece(c, o)
    for short in (False, True):
        c = Canvas(20, 18 if short else 22)
        arm = c.mask_rect(7, 8, 13, c.h)
        leather(c, arm)
        pad = c.mask_ellipse(10, 6, 10, 6)
        metal(c, pad)
        for x, y in pad:
            if (x, y + 1) not in pad:
                c.put(x, y, "Y")
        rivets(c, [(4, 5), (10, 2), (16, 5)])
        P["upper_arm_short" if short else "upper_arm"] = pauldron_piece(c, 7, 3)
    P["elbow_cap"] = elbow(metal)
    P["coat_tail"] = tail(leather)

    def spikes(c, m):
        for x in range(1, c.w - 1, 3):
            c.put(x, c.h - 1, "W")
    P.update(standard_limbs(leather, leather, metal, leather, metal, shin_extra=lambda c, m: metal(c, c.mask_rect(2, 0, 10, 5)), foot_w=15))
    rope = Canvas(9, 9)
    rm = rope.mask_ellipse(4.5, 4.5, 4.5, 4.5)
    rope.shade(rm, ("N", "B", "b"), edge="N")
    hole = rope.mask_ellipse(4.5, 4.5, 2, 2)
    for x, y in hole:
        rope.put(x, y, ".")
    acc = {"belt_hang": (rope.text(), (4, -1)), "belt_front": (Canvas(1, 1).text(), (0, 0))}
    acc.pop("belt_front")
    P.update(leg_pieces(cloth, cloth, knee_fill=leather, knee_r=5,
                        thigh_extra=lambda c, m: [c.put(x, 8, "D") for x in range(2, 9)]))
    P.update(boot_pieces(9, leather, metal, cuff_fill=leather, cuff=2, foot_w=15,
                         top_extra=lambda c, m: [rivets(c, [(9, 4), (10, 4), (9, 7), (10, 7)])]))
    return {"palette": pal, "pieces": P, "accessories": acc}


def colossus_anvil():
    hide_hair = True
    pal = {"#": (14, 12, 16), "M": (40, 38, 44), "m": (66, 62, 70), "w": (100, 96, 104), "W": (150, 146, 152),
           "E": (255, 120, 30), "L": (255, 220, 120), "V": (120, 40, 10), "Y": (220, 90, 24), "y": (255, 150, 50), "v": (255, 210, 120),
           "Q": (150, 154, 166), "X": (70, 74, 86), "z": (8, 6, 8)}
    P = {}
    c = helm_canvas()
    bucket = c.mask_poly([(3, 13), (33, 13), (34, 38), (2, 38)])
    metal(c, bucket)
    top = c.mask_poly([(2, 10), (34, 10), (35, 14), (1, 14)])
    metal(c, top)
    for x in range(12, 34):
        c.put(x, 24, "#")
        c.put(x, 25, "E" if x % 3 else "L")
        c.put(x, 26, "#")
    crack(c, [(8, 16), (11, 19), (9, 22)])
    crack(c, [(26, 30), (29, 35)])
    rivets(c, [(6, 36), (14, 36), (22, 36), (30, 36), (6, 16), (30, 16)], "W")
    P["helmet"] = helm_piece(c)
    c, o = torso_canvas(30)
    block = c.mask_poly([(o - 1, 4), (o + 23, 4), (o + 23, 26), (o - 1, 26)])
    metal(c, block)
    crack(c, [(o + 4, 10), (o + 7, 14), (o + 5, 19), (o + 8, 24)])
    crack(c, [(o + 18, 9), (o + 16, 15), (o + 19, 21)])
    c.line(o, 8, o + 22, 22, "Q")
    c.line(o, 9, o + 22, 23, "X")
    c.line(o + 22, 8, o, 22, "Q")
    c.line(o + 22, 9, o, 23, "X")
    anv = c.mask_poly([(o + 7, 12), (o + 15, 12), (o + 17, 14), (o + 13, 15), (o + 13, 18), (o + 9, 18), (o + 9, 15), (o + 5, 14)])
    c.shade(anv, ("V", "Y", "y"), edge="#")
    for i, y in enumerate((26, 29, 32)):
        lame = c.mask_rect(o - 2 + i, y, o + 24 - i, y + 3)
        metal(c, lame)
        c.line(o - 1 + i, y + 1, o + 23 - i, y + 1, "E")
    P["torso"] = torso_piece(c, o)
    c, o = torso_canvas(22)
    block = c.mask_rect(3, 3, 19, 26)
    metal(c, block)
    crack(c, [(14, 8), (12, 14), (15, 20)])
    for i, y in enumerate((26, 29, 32)):
        lame = c.mask_rect(3 - i, y, 19 + i, y + 3)
        metal(c, lame)
    P["torso_side"] = side_piece(c, 0)
    c, o = torso_canvas(30)
    block = c.mask_poly([(o - 1, 2), (o + 23, 2), (o + 23, 26), (o - 1, 26)])
    metal(c, block)
    crack(c, [(o + 11, 4), (o + 9, 12), (o + 12, 20)])
    for i, y in enumerate((26, 29, 32)):
        lame = c.mask_rect(o - 2 + i, y, o + 24 - i, y + 3)
        metal(c, lame)
    P["torso_back"] = back_piece(c, o)
    for short in (False, True):
        c = Canvas(24, 20 if short else 26)
        arm = c.mask_rect(9, 10, 15, c.h)
        metal(c, arm)
        anvil = c.mask_poly([(0, 2), (24, 2), (24, 6), (20, 7), (19, 11), (5, 11), (4, 7), (0, 6)])
        metal(c, anvil)
        c.line(1, 3, 23, 3, "W")
        crack(c, [(8, 6), (11, 9), (14, 6)])
        for y in range(6, 18):
            c.put(3, y, "Q" if y % 2 else "X")
        P["upper_arm_short" if short else "upper_arm"] = pauldron_piece(c, 9, 3)
    P["elbow_cap"] = elbow(metal, 5)
    P["coat_tail"] = tail(metal, 12, 14)

    def glow_seam(c, m):
        for x, y in m:
            if y == c.h // 2 and 0 < x < c.w - 1:
                c.put(x, y, "E")
    limbs = standard_limbs(metal, metal, metal, metal, metal, fore_extra=glow_seam, hand_extra=glow_seam, shin_extra=glow_seam, foot_w=16)
    P.update(limbs)
    chain = Canvas(5, 14)
    for y in range(0, 10):
        chain.put(2, y, "Q" if y % 2 else "X")
    wt = chain.mask_rect(0, 10, 5, 14)
    metal(chain, wt)
    acc = {"belt_hang": (chain.text(), (2, -1))}
    P.update(leg_pieces(metal, metal, knee_fill=metal, knee_r=5,
                        thigh_extra=lambda c, m: crack(c, [(4, 3), (6, 9), (4, 15)]),
                        shin_extra=lambda c, m: crack(c, [(6, 3), (4, 9), (6, 15)])))
    P.update(boot_pieces(4, metal, metal, foot_w=16))
    return {"palette": pal, "pieces": P, "accessories": acc}


def northern_forge():
    pal = {"#": (40, 56, 80), "M": (104, 128, 160), "m": (150, 174, 202), "w": (200, 220, 238), "W": (240, 250, 255),
           "J": (150, 156, 168), "K": (208, 212, 220), "k": (232, 236, 242), "j": (252, 252, 255),
           "V": (90, 100, 120), "Y": (176, 186, 204), "y": (220, 228, 240), "v": (250, 252, 255),
           "E": (90, 210, 255), "L": (220, 250, 255), "D": (30, 50, 90), "T": (50, 80, 140), "t": (80, 120, 190), "g": (130, 170, 230), "z": (20, 30, 50)}
    P = {}
    c = helm_canvas()
    dome = c.mask_poly([(5, 21), (6, 13), (11, 7), (18, 5), (25, 7), (30, 13), (31, 21)])
    metal(c, dome)
    for x in range(5, 32):
        c.put(x, 20, "Y")
    c.put(22, 13, "E")
    c.put(21, 13, "L")
    c.put(22, 12, "L")
    for side in (0, 1):
        x0 = 6 if side == 0 else 30
        d = -1 if side == 0 else 1
        wing = c.mask_poly([(x0, 16), (x0 + d * 7, 3), (x0 + d * 6, 10), (x0 + d * 9, 7), (x0 + d * 5, 18), (x0, 19)])
        c.shade(wing, ("E", "w", "L"), edge="#")
    cheek = c.mask_rect(4, 21, 11, 33)
    metal(c, cheek)
    P["helmet"] = helm_piece(c)
    c, o = torso_canvas()
    plate = c.mask_poly([(o + 2, 4), (o + 20, 4), (o + 21, 18), (o + 19, 22), (o + 3, 22), (o + 1, 18)])
    metal(c, plate)
    for x, y in ((o + 11, 11), (o + 10, 12), (o + 12, 12), (o + 11, 13), (o + 11, 14), (o + 9, 15), (o + 13, 15)):
        c.put(x, y, "E")
    c.put(o + 11, 12, "L")
    mantle = c.mask_poly([(o - 1, 4), (o + 23, 4), (o + 22, 10), (o + 17, 12), (o + 5, 12), (o, 10)])
    fur(c, mantle)
    belt = c.mask_rect(o + 1, 22, o + 21, 25)
    c.shade(belt, ("V", "Y", "y"), edge="V")
    skirt = c.mask_poly([(o + 1, 25), (o + 21, 25), (o + 22, 34), (o, 34)])
    cloth(c, skirt)
    for x in range(o + 2, o + 21, 4):
        c.line(x, 26, x, 33, "D", skirt)
    P["torso"] = torso_piece(c, o)
    c, o = torso_canvas(22)
    plate = c.mask_rect(4, 4, 18, 22)
    metal(c, plate)
    c.put(14, 12, "E")
    mantle = c.mask_rect(3, 4, 19, 11)
    fur(c, mantle)
    belt = c.mask_rect(4, 22, 18, 25)
    c.shade(belt, ("V", "Y", "y"), edge="V")
    skirt = c.mask_rect(4, 25, 18, 34)
    cloth(c, skirt)
    P["torso_side"] = side_piece(c, 0)
    c, o = torso_canvas()
    mantle = c.mask_poly([(o - 1, 2), (o + 23, 2), (o + 23, 34), (o - 1, 34)])
    fur(c, mantle)
    P["torso_back"] = back_piece(c, o)
    for short in (False, True):
        c = Canvas(20, 18 if short else 22)
        arm = c.mask_rect(7, 8, 13, c.h)
        metal(c, arm)
        pad = c.mask_ellipse(10, 7, 9, 6)
        metal(c, pad)
        trim = c.mask_poly([(1, 2), (19, 2), (18, 5), (2, 5)])
        fur(c, trim)
        for x in (4, 10, 16):
            c.put(x, 9, "E")
        P["upper_arm_short" if short else "upper_arm"] = pauldron_piece(c, 7, 3)
    P["elbow_cap"] = elbow(metal)
    P["coat_tail"] = tail(fur)
    P.update(standard_limbs(metal, metal, fur, metal, metal,
                            fore_extra=lambda c, m: c.put(4, 6, "E"),
                            shin_extra=lambda c, m: fur(c, c.mask_rect(1, 0, 11, 5))))
    cape = Canvas(36, 54)
    for y in range(54):
        w = 20 + int(15 * y / 53)
        x0 = (36 - w) // 2
        for x in range(x0, x0 + w):
            cape.put(x, y, "K")
    cm = {(x, y) for y in range(54) for x in range(36) if cape.get(x, y) != "."}
    fur(cape, cm)
    for x in range(36):
        for y in range(48, 54):
            if (x, y) in cm and (x + y) % 3 == 0:
                cape.put(x, y, "j")
    acc = {"cape": (cape.text(), (17.5, 0.5))}
    P.update(leg_pieces(cloth, cloth, knee_fill=metal,
                        thigh_extra=lambda c, m: [c.put(x, y, "D") for x, y in m if (x + y) % 4 == 0],
                        shin_extra=lambda c, m: [c.put(x, y, "D") for x, y in m if (x + y) % 4 == 0]))
    P.update(boot_pieces(8, metal, metal, cuff_fill=fur, cuff=4))
    return {"palette": pal, "pieces": P, "accessories": acc}


def smith_line():
    hide_hair = True
    pal = {"#": (30, 8, 14), "M": (90, 20, 30), "m": (140, 34, 44), "w": (190, 64, 66), "W": (236, 140, 120),
           "V": (120, 80, 20), "Y": (210, 160, 60), "y": (244, 206, 110), "v": (255, 240, 180),
           "D": (50, 10, 16), "T": (100, 20, 30), "t": (140, 34, 44), "g": (180, 60, 60), "z": (10, 4, 6)}
    P = {}
    c = helm_canvas()
    dome = c.mask_poly([(4, 38), (4, 17), (9, 12), (18, 10), (27, 12), (32, 17), (33, 38)])
    metal(c, dome)
    fluting(c, dome, [10, 16, 22])
    for x in range(13, 33):
        c.put(x, 24, "#")
        c.put(x, 25, "z")
        c.put(x, 26, "#")
    for i, x in enumerate(range(6, 32, 5)):
        h = 5 if i % 2 else 8
        c.line(x, 12, x + 1, 12 - h, "Y")
        c.put(x + 1, 12 - h, "v")
    for x in range(5, 33):
        c.put(x, 13, "Y")
    for x in range(5, 33):
        c.put(x, 36, "Y")
    P["helmet"] = helm_piece(c)
    c, o = torso_canvas()
    plate = c.mask_poly([(o + 2, 4), (o + 20, 4), (o + 21, 20), (o + 1, 20)])
    metal(c, plate)
    fluting(c, plate, [o + 5, o + 9, o + 13, o + 17])
    for x in range(o + 2, o + 21):
        c.put(x, 20, "Y")
    c.put(o + 11, 12, "Y")
    c.put(o + 11, 11, "v")
    skirt = c.mask_poly([(o + 1, 21), (o + 21, 21), (o + 23, 35), (o - 1, 35)])
    cloth(c, skirt)
    for x in range(o + 1, o + 22, 3):
        c.line(x, 22, x - 1, 34, "D", skirt)
    for x in range(o - 1, o + 24):
        c.put(x, 35, "Y")
    P["torso"] = torso_piece(c, o)
    c, o = torso_canvas(22)
    plate = c.mask_rect(4, 4, 18, 21)
    metal(c, plate)
    fluting(c, plate, [7, 11, 15])
    skirt = c.mask_rect(3, 21, 19, 35)
    cloth(c, skirt)
    P["torso_side"] = side_piece(c, 0)
    c, o = torso_canvas()
    plate = c.mask_poly([(o + 1, 2), (o + 21, 2), (o + 21, 21), (o + 1, 21)])
    metal(c, plate)
    fluting(c, plate, [o + 5, o + 11, o + 17])
    skirt = c.mask_poly([(o + 1, 21), (o + 21, 21), (o + 23, 35), (o - 1, 35)])
    cloth(c, skirt)
    P["torso_back"] = back_piece(c, o)
    for short in (False, True):
        c = Canvas(20, 22 if short else 26)
        arm = c.mask_rect(7, 12, 13, c.h)
        metal(c, arm)
        pad = c.mask_ellipse(10, 10, 10, 6)
        metal(c, pad)
        fluting(c, pad, [5, 10, 15])
        for x, h in ((3, 5), (8, 8), (13, 8), (17, 5)):
            c.line(x, 6, x, 6 - h, "w")
            c.put(x, 6 - h, "W")
        P["upper_arm_short" if short else "upper_arm"] = pauldron_piece(c, 7, 7)
    P["elbow_cap"] = elbow(metal, 5)
    P["coat_tail"] = tail(cloth, 12, 18)
    P.update(standard_limbs(metal, metal, metal, metal, metal,
                            fore_extra=lambda c, m: fluting(c, m, [3]),
                            hand_extra=lambda c, m: [c.put(x, 1, "W") for x in (2, 5, 8)],
                            shin_extra=lambda c, m: [c.put(10, 1, "W"), c.put(11, 0, "W"), fluting(c, m, [5])], foot_w=16))
    from armor import make_cape
    txt, _ = make_cape(top=10, bottom=19, length=28)
    rows = txt.split("\n")
    for i in range(len(rows) - 6, len(rows)):
        rows[i] = "".join("." if (j + i) % 5 == 0 and ch not in ".o" else ch for j, ch in enumerate(rows[i]))
    acc = {"cape": ("\n".join(rows), (10, 0)), "_cape_native": False}
    P.update(leg_pieces(metal, metal, knee_fill=metal, knee_r=5,
                        thigh_extra=lambda c, m: fluting(c, m, [4]),
                        shin_extra=lambda c, m: fluting(c, m, [5])))
    knee_txt, knee_piv = P["knee_cap"]
    kc = Canvas(10, 12)
    km = kc.mask_ellipse(5, 7, 5, 5)
    metal(kc, km)
    spike = kc.mask_poly([(6, 4), (9, 4), (10, 0)])
    kc.shade(spike, ("w", "w", "W"), edge="#")
    P["knee_cap"] = (kc.text(), (4.5, 6.5))
    P.update(boot_pieces(3, metal, metal, foot_w=14, toe="point"))
    return {"palette": pal, "pieces": P, "accessories": acc}


def underside_forge():
    hide_hair = True
    pal = {"#": (8, 4, 14), "M": (28, 18, 40), "m": (46, 32, 64), "w": (78, 60, 104), "W": (150, 130, 190),
           "E": (180, 80, 255), "L": (240, 200, 255), "V": (60, 20, 90), "Y": (140, 60, 210), "y": (190, 110, 255), "v": (230, 190, 255),
           "u": (60, 46, 80), "i": (110, 90, 140), "U": (34, 24, 46), "z": (4, 2, 6)}
    P = {}
    c = helm_canvas()
    dome = c.mask_poly([(4, 38), (5, 16), (10, 11), (18, 9), (26, 11), (31, 16), (32, 38)])
    metal(c, dome)
    for x in range(14, 32):
        c.put(x, 25, "E" if x % 2 else "L")
    crack(c, [(9, 16), (12, 22), (10, 30)])
    for side in (0, 1):
        pts = [(8, 15), (2, 11), (0, 3), (3, 2), (5, 9), (11, 12)] if side == 0 else [(28, 15), (33, 10), (35, 2), (33, 3), (30, 9), (25, 12)]
        horn = c.mask_poly(pts)
        c.shade(horn, ("U", "u", "i"), edge="#")
    P["helmet"] = helm_piece(c)
    c, o = torso_canvas(28)
    plate = c.mask_poly([(o, 4), (o + 22, 4), (o + 24, 14), (o + 21, 24), (o + 1, 24), (o - 2, 14)])
    metal(c, plate)
    crack(c, [(o + 11, 6), (o + 9, 11), (o + 12, 16), (o + 10, 22)])
    crack(c, [(o + 3, 9), (o + 6, 14)])
    crack(c, [(o + 19, 10), (o + 16, 18)])
    for i, y in enumerate((24, 28)):
        lame = c.mask_poly([(o - 1 + i, y), (o + 23 - i, y), (o + 22 - i, y + 4), (o + i, y + 4)])
        metal(c, lame)
        c.line(o + i, y + 2, o + 22 - i, y + 2, "E")
    P["torso"] = torso_piece(c, o)
    c, o = torso_canvas(22)
    plate = c.mask_poly([(4, 4), (18, 4), (19, 14), (18, 24), (4, 24), (3, 14)])
    metal(c, plate)
    crack(c, [(14, 6), (12, 12), (15, 18)])
    lame = c.mask_rect(3, 24, 19, 32)
    metal(c, lame)
    P["torso_side"] = side_piece(c, 0)
    c, o = torso_canvas(28)
    plate = c.mask_poly([(o, 2), (o + 22, 2), (o + 24, 14), (o + 21, 24), (o + 1, 24), (o - 2, 14)])
    metal(c, plate)
    for x in range(o + 4, o + 20, 5):
        sp = c.mask_poly([(x, 6), (x + 3, 6), (x + 1, 0)])
        metal(c, sp)
    crack(c, [(o + 11, 4), (o + 13, 12), (o + 10, 20)])
    lame = c.mask_rect(o, 24, o + 22, 32)
    metal(c, lame)
    P["torso_back"] = back_piece(c, o)
    for short in (False, True):
        c = Canvas(22, 20 if short else 24)
        arm = c.mask_rect(8, 9, 14, c.h)
        metal(c, arm)
        pad = c.mask_poly([(0, 9), (3, 2), (8, 4), (11, 0), (14, 4), (19, 2), (22, 9), (18, 12), (4, 12)])
        metal(c, pad)
        crack(c, [(6, 6), (9, 10)])
        crack(c, [(16, 5), (14, 10)])
        P["upper_arm_short" if short else "upper_arm"] = pauldron_piece(c, 8, 3)
    P["elbow_cap"] = elbow(metal, 5)
    P["coat_tail"] = tail(metal, 12, 14)

    def claws(c, m):
        for x in (1, 4, 7):
            c.put(x, c.h - 1, "W")
        c.put(5, 4, "E")
    P.update(standard_limbs(metal, metal, metal, metal, metal,
                            fore_extra=lambda c, m: crack(c, [(3, 2), (5, 7), (3, 10)]),
                            hand_extra=claws, shin_extra=lambda c, m: crack(c, [(6, 3), (5, 10), (7, 16)]), foot_w=16))
    shards = Canvas(14, 16)
    for (cx, cy, h) in ((3, 4, 4), (10, 2, 5), (7, 11, 4)):
        m = shards.mask_poly([(cx, cy - h), (cx + 2, cy), (cx, cy + h), (cx - 2, cy)])
        shards.shade(m, ("Y", "E", "L"), edge="#")
    acc = {"back_item": (shards.text(), (2, 15))}
    P.update(leg_pieces(metal, metal, knee_fill=metal, knee_r=5,
                        thigh_extra=lambda c, m: crack(c, [(5, 2), (3, 9), (6, 16)]),
                        shin_extra=lambda c, m: crack(c, [(4, 2), (6, 9), (4, 15)])))
    P.update(boot_pieces(4, metal, metal, foot_w=14, toe="claw"))
    return {"palette": pal, "pieces": P, "accessories": acc}


LINE = {"jarl_forge": jarl_forge, "mountain_hammer": mountain_hammer, "colossus_anvil": colossus_anvil,
        "northern_forge": northern_forge, "smith_line": smith_line, "underside_forge": underside_forge}
HIDES_HAIR = {"colossus_anvil", "smith_line", "underside_forge"}
SLOT_OF_LEGS = {"thigh": "chest", "shin": "chest", "knee_cap": "chest", "boot_top": "boots", "foot": "boots"}
SLOT_OF = {"helmet": "helmet", "crest": "helmet", "hair_front": "helmet", "hair_fly": "helmet", "head": "helmet", "blush": "helmet", "torso": "chest", "torso_side": "chest", "torso_back": "chest",
           "upper_arm": "chest", "upper_arm_short": "chest", "forearm": "chest", "elbow_cap": "chest", "coat_tail": "chest",
           "cape": "chest", "back_item": "chest", "belt_front": "chest", "belt_hang": "chest", "pauldron_deco": "chest",
           "hand": "gloves", "bracer": "gloves", "thigh": "chest", "shin": "chest", "knee_cap": "chest", "boot_top": "boots", "foot": "boots"}


def build():
    return {k: f() for k, f in LINE.items()}
