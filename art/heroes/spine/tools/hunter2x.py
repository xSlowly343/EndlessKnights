from pix import Canvas
from forge2x import (helm_canvas, helm_piece, torso_canvas, torso_piece, side_piece, back_piece, pauldron_piece, elbow, tail,
                     leg_pieces, boot_pieces, standard_limbs, metal, leather, fur, cloth, gold, mail, crack, rivets, SLOT_OF)

SKIN = {"x": (170, 112, 88), "S": (204, 158, 128), "s": (230, 200, 170), "Z": (244, 224, 200)}
FACE = (12, 22, 32, 38)


def skin(c, m):
    c.shade(m, ("x", "S", "s"), edge="x")


def hood(c, fill, tip=True):
    outer = c.mask_poly([(3, 38), (2, 22), (4, 12), (10, 6), (18, 4), (26, 6), (32, 12), (34, 22), (33, 38)])
    x0, y0, x1, y1 = FACE
    face = {(x, y) for x in range(x0, x1) for y in range(y0, y1)}
    m = outer - face
    if tip:
        m |= c.mask_poly([(4, 12), (0, 16), (1, 22), (5, 20)])
    fill(c, m)
    for y in range(y0, y1):
        if (x0 - 1, y) in m:
            c.put(x0 - 1, y, c.get(x0 - 1, y) if c.get(x0 - 1, y) == "D" else "D")
    for x in range(x0, x1):
        if (x, y0 - 1) in m:
            c.put(x, y0 - 1, "D")
    return m


def cape(width_top, width_bottom, length, fill, folds=True):
    w = max(width_top, width_bottom) + 2
    c = Canvas(w, length)
    m = set()
    for y in range(length):
        ww = round(width_top + (width_bottom - width_top) * y / (length - 1))
        x0 = (w - ww) // 2
        m |= {(x, y) for x in range(x0, x0 + ww)}
    fill(c, m)
    if folds:
        for x in range(3, w - 3, 5):
            c.line(x, 4, x + 1, length - 2, "D", m)
    return c, ((w - 1) / 2, 0.5)


def huntress_trail():
    pal = {"#": (40, 30, 24), "M": (80, 60, 40), "m": (120, 90, 60), "w": (160, 124, 84), "W": (200, 170, 120),
           "N": (54, 34, 22), "B": (98, 64, 40), "b": (136, 92, 58), "n": (172, 124, 82),
           "D": (24, 52, 30), "T": (44, 92, 50), "t": (70, 132, 72), "g": (110, 170, 100),
           "V": (100, 70, 30), "Y": (170, 130, 60), "y": (210, 170, 90), "v": (240, 210, 140),
           "R": (170, 40, 36), "r": (220, 80, 60), "i": (240, 236, 222), **SKIN}
    P = {}
    c = helm_canvas()
    hood(c, cloth)
    quill = c.mask_poly([(6, 14), (10, 12), (8, 5), (4, 0), (1, 1), (2, 7)])
    c.shade(quill, ("R", "r", "r"), edge="#")
    c.line(7, 12, 3, 1, "i", quill)
    for y in (4, 7, 10):
        c.put(2 + y // 3, y, "#")
    P["helmet"] = helm_piece(c)
    c, o = torso_canvas()
    tunic = c.mask_poly([(o + 1, 0), (o + 21, 0), (o + 22, 33), (o, 33)])
    cloth(c, tunic)
    jerkin = c.mask_poly([(o + 2, 2), (o + 9, 2), (o + 11, 22), (o + 2, 22)]) | c.mask_poly([(o + 13, 2), (o + 20, 2), (o + 20, 22), (o + 12, 22)])
    leather(c, jerkin)
    for i in range(0, 20):
        c.put(o + 19 - i, 3 + i, "N")
        c.put(o + 18 - i, 3 + i, "b")
    belt = c.mask_rect(o, 21, o + 22, 24)
    leather(c, belt)
    rivets(c, [(o + 11, 22)])
    pouch = c.mask_rect(o + 3, 24, o + 8, 29)
    leather(c, pouch)
    P["torso"] = torso_piece(c, o)
    c, o = torso_canvas(22)
    tunic = c.mask_rect(4, 0, 18, 33)
    cloth(c, tunic)
    j = c.mask_rect(10, 2, 18, 22)
    leather(c, j)
    belt = c.mask_rect(4, 21, 18, 24)
    leather(c, belt)
    P["torso_side"] = side_piece(c, 0)
    c, o = torso_canvas()
    tunic = c.mask_poly([(o + 1, 0), (o + 21, 0), (o + 22, 33), (o, 33)])
    cloth(c, tunic)
    for i in range(0, 20):
        c.put(o + 2 + i, 3 + i, "N")
    belt = c.mask_rect(o, 21, o + 22, 24)
    leather(c, belt)
    P["torso_back"] = back_piece(c, o)
    for short in (False, True):
        c = Canvas(14, 18 if short else 22)
        sleeve = c.mask_rect(4, 2, 10, c.h)
        cloth(c, sleeve)
        pad = c.mask_ellipse(7, 4, 6, 4)
        leather(c, pad)
        P["upper_arm_short" if short else "upper_arm"] = pauldron_piece(c, 4, 1)
    P["elbow_cap"] = elbow(cloth)
    P["coat_tail"] = tail(cloth)

    def laces(c, m):
        for y in range(2, 11, 3):
            c.line(1, y, 6, y + 1, "N", m)

    def fingerless(c, m):
        for x, y in m:
            if y >= 6:
                c.put(x, y, "S" if x < 6 else "s")
        c.outline(m, "x")
    P.update(standard_limbs(leather, leather, leather, leather, leather, fore_extra=laces, hand_extra=fingerless))
    P.update(leg_pieces(cloth, cloth, knee_fill=cloth, shin_extra=lambda c, m: [c.put(x, 9, "D") for x in range(2, 9)]))
    P.update(boot_pieces(8, leather, leather, cuff_fill=lambda c, m: c.shade(m, ("B", "b", "n"), edge="N"), cuff=3))
    cc, cp = cape(18, 26, 40, cloth)
    pouch = Canvas(6, 6)
    pm = pouch.mask_rect(0, 0, 6, 6)
    leather(pouch, pm)
    acc = {"cape": (cc.text(), cp)}
    return {"palette": pal, "pieces": P, "accessories": acc}


def jungle_wind():
    pal = {"#": (36, 24, 14), "M": (78, 50, 28), "m": (112, 74, 42), "w": (148, 102, 60), "W": (184, 140, 90),
           "N": (40, 26, 14), "B": (78, 50, 28), "b": (112, 74, 42), "n": (148, 102, 60),
           "D": (20, 60, 24), "T": (40, 110, 40), "t": (80, 160, 60), "g": (140, 210, 90),
           "R": (190, 40, 30), "r": (240, 100, 50), "Y": (230, 190, 40), "y": (250, 230, 120), "V": (40, 110, 160), "v": (90, 180, 230),
           "u": (220, 214, 196), "i": (250, 248, 240), **SKIN}
    P = {}
    c = helm_canvas()
    band = c.mask_rect(4, 17, 33, 21)
    leather(c, band)
    cols = ["R", "Y", "T", "V", "r", "t", "Y", "R"]
    plume = set()
    for i, x in enumerate(range(6, 32, 3)):
        h = 10 + (i % 3) * 3
        tip = x + 1 + (i - 4) * 0.4
        f = c.mask_poly([(x - 1, 17), (x + 3, 17), (tip + 1.8, 17 - h + 3), (tip, 17 - h), (tip - 1.8, 17 - h + 3)])
        col = cols[i % len(cols)]
        for px, py in f:
            c.put(px, py, col)
        c.line(x + 1, 16, int(tip), 17 - h + 1, "i", f)
        plume |= f
    c.outline(plume, "#")
    rivets(c, [(10, 19), (18, 19), (26, 19)], "u")
    P["helmet"] = helm_piece(c)

    def bark(c, m):
        leather(c, m)
        for x, y in m:
            if (x * 7 + y * 3) % 11 == 0:
                c.put(x, y, "N")

    def leaves(c, cx, cy, n, rx=4, ry=3):
        for i in range(n):
            m = c.mask_ellipse(cx + i * (rx * 1.4), cy, rx, ry)
            cloth(c, m)
            c.line(int(cx + i * rx * 1.4 - rx + 1), int(cy), int(cx + i * rx * 1.4 + rx - 1), int(cy), "D", m)
    c, o = torso_canvas()
    body = c.mask_poly([(o + 2, 0), (o + 20, 0), (o + 21, 22), (o + 1, 22)])
    skin(c, body)
    plate = c.mask_poly([(o + 4, 4), (o + 18, 4), (o + 19, 20), (o + 3, 20)])
    bark(c, plate)
    for x in range(o + 5, o + 18, 2):
        c.put(x, 6, "u")
        c.put(x, 7, "i")
    skirt = set()
    for i, x in enumerate(range(o - 1, o + 22, 4)):
        skirt |= c.mask_poly([(x, 22), (x + 5, 22), (x + 4, 32 + (i % 2) * 2), (x + 2, 35 + (i % 2)), (x + 1, 32 + (i % 2) * 2)])
    cloth(c, skirt)
    for i, x in enumerate(range(o - 1, o + 22, 4)):
        c.line(x + 2, 23, x + 2, 33 + (i % 2), "D", skirt)
    belt = c.mask_rect(o + 1, 21, o + 21, 24)
    leather(c, belt)
    P["torso"] = torso_piece(c, o)
    c, o = torso_canvas(22)
    body = c.mask_rect(4, 0, 18, 22)
    skin(c, body)
    plate = c.mask_rect(8, 4, 18, 20)
    bark(c, plate)
    skirt = c.mask_rect(3, 22, 19, 35)
    cloth(c, skirt)
    P["torso_side"] = side_piece(c, 0)
    c, o = torso_canvas()
    body = c.mask_poly([(o + 2, 0), (o + 20, 0), (o + 21, 22), (o + 1, 22)])
    skin(c, body)
    plate = c.mask_poly([(o + 5, 3), (o + 17, 3), (o + 18, 20), (o + 4, 20)])
    bark(c, plate)
    skirt = c.mask_poly([(o + 1, 22), (o + 21, 22), (o + 23, 35), (o - 1, 35)])
    cloth(c, skirt)
    P["torso_back"] = back_piece(c, o)
    for short in (False, True):
        c = Canvas(18, 18 if short else 22)
        arm = c.mask_rect(6, 4, 12, c.h)
        skin(c, arm)
        for y in range(8, c.h, 4):
            c.line(6, y, 11, y + 2, "T", arm)
        for i, (cx, cy) in enumerate(((5, 4), (9, 3), (13, 4))):
            m = c.mask_ellipse(cx, cy, 4, 3)
            cloth(c, m)
        P["upper_arm_short" if short else "upper_arm"] = pauldron_piece(c, 6, 1)
    P["elbow_cap"] = elbow(skin)
    P["coat_tail"] = tail(cloth)

    def vines(c, m):
        for y in range(1, c.h, 3):
            c.line(0, y, c.w, y + 2, "T", m)

    def wraps(c, m):
        skin(c, m)
        vines(c, m)
    P.update(standard_limbs(bark, wraps, bark, bark, bark, fore_extra=lambda c, m: vines(c, m)))
    P.update(leg_pieces(wraps, wraps, knee_fill=skin))
    P.update(boot_pieces(4, wraps, lambda c, m: (skin(c, m), [c.put(x, y, "B") for x, y in m if y in (2, 3, 7)]), cuff=0, foot_w=14))
    fang = Canvas(10, 8)
    for x in range(0, 10, 3):
        fm = fang.mask_poly([(x, 0), (x + 2, 0), (x + 1, 6)])
        fang.shade(fm, ("u", "u", "i"), edge="#")
    acc = {"belt_front": (fang.text(), (-3, -1))}
    return {"palette": pal, "pieces": P, "accessories": acc}


def sand_track():
    pal = {"#": (60, 40, 20), "M": (120, 80, 34), "m": (170, 120, 56), "w": (210, 160, 80), "W": (240, 210, 140),
           "N": (60, 44, 30), "B": (110, 84, 56), "b": (150, 120, 84), "n": (190, 160, 118),
           "D": (120, 96, 66), "T": (190, 160, 116), "t": (220, 196, 152), "g": (240, 226, 190),
           "R": (110, 36, 24), "r": (170, 64, 40), "Y": (200, 150, 70), "y": (230, 190, 110), "V": (110, 76, 30), "v": (250, 220, 150), **SKIN}
    P = {}
    c = helm_canvas()
    turban = c.mask_poly([(3, 24), (3, 14), (8, 6), (18, 3), (28, 6), (34, 14), (34, 24)])
    cloth(c, turban)
    for y in (9, 13, 17, 21):
        c.line(4, y + 2, 33, y - 2, "D", turban)
    veil = c.mask_poly([(11, 30), (33, 30), (33, 37), (28, 40), (14, 40), (11, 37)])
    c.shade(veil, ("R", "r", "r"), edge="#")
    side = c.mask_rect(2, 22, 12, 40)
    cloth(c, side)
    c.put(30, 12, "Y")
    c.put(30, 13, "y")
    P["helmet"] = helm_piece(c)

    def scales(c, m):
        for x, y in m:
            c.put(x, y, "m" if (x + (y // 2) % 2) % 3 else "w")
            if y % 2 == 0 and (x + y // 2) % 3 == 0:
                c.put(x, y, "M")
        c.outline(m, "#")
    c, o = torso_canvas()
    robe = c.mask_poly([(o, 0), (o + 22, 0), (o + 23, 36), (o - 1, 36)])
    cloth(c, robe)
    vest = c.mask_poly([(o + 6, 3), (o + 16, 3), (o + 15, 19), (o + 7, 19)])
    scales(c, vest)
    sash = c.mask_rect(o, 19, o + 22, 23)
    c.shade(sash, ("R", "r", "r"), edge="#")
    tails = c.mask_poly([(o + 15, 22), (o + 18, 22), (o + 19, 33), (o + 16, 33)])
    c.shade(tails, ("R", "r", "r"), edge="#")
    for x in range(o + 2, o + 22, 4):
        c.line(x, 24, x - 1, 35, "D", robe)
    P["torso"] = torso_piece(c, o)
    c, o = torso_canvas(22)
    robe = c.mask_rect(3, 0, 19, 36)
    cloth(c, robe)
    v = c.mask_rect(11, 3, 17, 19)
    scales(c, v)
    sash = c.mask_rect(3, 19, 19, 23)
    c.shade(sash, ("R", "r", "r"), edge="#")
    P["torso_side"] = side_piece(c, 0)
    c, o = torso_canvas()
    robe = c.mask_poly([(o, 0), (o + 22, 0), (o + 23, 36), (o - 1, 36)])
    cloth(c, robe)
    sash = c.mask_rect(o, 19, o + 22, 23)
    c.shade(sash, ("R", "r", "r"), edge="#")
    P["torso_back"] = back_piece(c, o)
    for short in (False, True):
        c = Canvas(20, 18 if short else 22)
        sleeve = c.mask_rect(7, 4, 13, c.h)
        cloth(c, sleeve)
        guard = c.mask_poly([(1, 7), (6, 1), (14, 0), (19, 4), (18, 9), (12, 11), (4, 11)])
        metal(c, guard)
        c.line(3, 8, 17, 4, "Y", guard)
        P["upper_arm_short" if short else "upper_arm"] = pauldron_piece(c, 7, 2)
    P["elbow_cap"] = elbow(cloth)
    P["coat_tail"] = tail(cloth, 12, 18)

    def wrap(c, m):
        cloth(c, m)
        for y in range(1, c.h, 3):
            c.line(0, y, c.w, y + 1, "D", m)
    P.update(standard_limbs(wrap, wrap, wrap, wrap, wrap))
    c_th = None
    legs = leg_pieces(cloth, cloth, knee_fill=cloth)
    tc = Canvas(12, 20)
    tm = tc.mask_poly([(1, 0), (10, 0), (12, 12), (11, 20), (1, 20), (0, 12)])
    cloth(tc, tm)
    for x in (3, 6, 9):
        tc.line(x, 2, x, 18, "D", tm)
    from armor2x import piece as _piece
    legs["thigh"] = _piece(tc, "thigh", -2, -1)
    sc = Canvas(12, 19)
    sm = sc.mask_poly([(0, 0), (12, 0), (11, 10), (9, 17), (3, 17), (1, 10)])
    cloth(sc, sm)
    for x in range(1, 12):
        if (x, 16) in sm:
            sc.put(x, 16, "D")
    legs["shin"] = _piece(sc, "shin", -2, 0)
    P.update(legs)
    P.update(boot_pieces(5, wrap, cloth, cuff=0, toe="point"))
    cc, cp = cape(18, 30, 44, cloth)
    acc = {"cape": (cc.text(), cp)}
    return {"palette": pal, "pieces": P, "accessories": acc}


def falcon_wing():
    pal = {"#": (20, 34, 60), "M": (50, 84, 130), "m": (80, 130, 186), "w": (130, 180, 226), "W": (200, 230, 250),
           "N": (60, 40, 24), "B": (110, 76, 46), "b": (150, 110, 70), "n": (190, 150, 100),
           "D": (150, 150, 160), "T": (210, 212, 220), "t": (236, 238, 244), "g": (255, 255, 255),
           "V": (130, 90, 20), "Y": (220, 170, 50), "y": (250, 210, 100), "v": (255, 240, 170),
           "E": (230, 250, 255), "L": (255, 255, 255), "z": (10, 16, 30), "K": (96, 64, 40), "k": (150, 110, 70), "j": (210, 180, 130)}
    P = {}
    c = helm_canvas()
    hood(c, metal, tip=False)
    for i in range(5):
        c.line(6 + i * 6, 7, 4 + i * 6, 11, "M")
    visor = c.mask_poly([(10, 9), (26, 9), (34, 13), (36, 17), (33, 22), (30, 19), (12, 19)])
    metal(c, visor)
    beak = c.mask_poly([(29, 15), (36, 17), (37, 21), (34, 24), (32, 20)])
    gold(c, beak)
    for x in range(12, 30):
        c.put(x, 19, "#")
    for side in ((4, 22), (31, 22)):
        cheek = c.mask_poly([(side[0], side[1]), (side[0] + 5, side[1]), (side[0] + 4, side[1] + 14), (side[0] + 1, side[1] + 16)])
        c.shade(cheek, ("D", "T", "t"), edge="#")
    for i, x in enumerate(range(9, 27, 4)):
        f = c.mask_poly([(x, 8), (x + 4, 8), (x - 1, i % 2), (x - 3, 2 + i % 2)])
        c.shade(f, ("D", "T", "t"), edge="#")
    P["helmet"] = helm_piece(c)

    def feathers(c, m, rows=3):
        metal(c, m)
        for x, y in m:
            if y % rows == 0 and (x // 2 + y // rows) % 2 == 0:
                c.put(x, y, "M")
            if y % rows == 1 and (x // 2 + y // rows) % 2 == 1:
                c.put(x, y, "W")
    c, o = torso_canvas()
    plate = c.mask_poly([(o + 1, 0), (o + 21, 0), (o + 22, 33), (o, 33)])
    feathers(c, plate)
    jabot = set()
    for i, y in enumerate((1, 5, 9)):
        jabot |= c.mask_ellipse(o + 11, y + 2, 7 - i * 1.5, 2.5)
    c.shade(jabot, ("D", "T", "t"), edge="#")
    for i, y in enumerate((3, 7, 11)):
        c.line(o + 6 + i * 2, y + 1, o + 16 - i * 2, y + 1, "D", jabot)
    belt = c.mask_rect(o, 21, o + 22, 24)
    leather(c, belt)
    rivets(c, [(o + 11, 22)])
    P["torso"] = torso_piece(c, o)
    c, o = torso_canvas(22)
    plate = c.mask_rect(4, 0, 18, 33)
    feathers(c, plate)
    j = c.mask_rect(13, 2, 18, 14)
    cloth(c, j)
    belt = c.mask_rect(4, 21, 18, 24)
    leather(c, belt)
    P["torso_side"] = side_piece(c, 0)
    c, o = torso_canvas()
    plate = c.mask_poly([(o + 1, 0), (o + 21, 0), (o + 22, 33), (o, 33)])
    feathers(c, plate)
    belt = c.mask_rect(o, 21, o + 22, 24)
    leather(c, belt)
    P["torso_back"] = back_piece(c, o)
    for short in (False, True):
        c = Canvas(20, 18 if short else 22)
        arm = c.mask_rect(7, 6, 13, c.h)
        feathers(c, arm)
        for i, cy in enumerate((9, 6, 3)):
            m = c.mask_ellipse(10, cy, 10 - i * 1.5, 3)
            feathers(c, m, 2)
            for x, y in m:
                if (x, y + 1) not in m:
                    c.put(x, y, "t")
        P["upper_arm_short" if short else "upper_arm"] = pauldron_piece(c, 7, 2)
    P["elbow_cap"] = elbow(metal)
    P["coat_tail"] = tail(lambda c, m: feathers(c, m))
    P.update(standard_limbs(feathers, leather, leather, feathers, feathers, hand_extra=lambda c, m: c.outline(m, "N")))
    P.update(leg_pieces(feathers, feathers, knee_fill=metal))
    P.update(boot_pieces(8, leather, leather, cuff_fill=lambda c, m: c.shade(m, ("D", "T", "t"), edge="#"), cuff=3, toe="claw"))
    bird = Canvas(12, 12)
    body = bird.mask_ellipse(5, 7, 4, 4.5) | bird.mask_ellipse(7, 3, 2.6, 2.6) | bird.mask_poly([(1, 8), (3, 10), (0, 12)])
    bird.shade(body, ("K", "k", "j"), edge="#")
    bird.line(3, 6, 4, 10, "K", body)
    bird.put(10, 3, "Y")
    bird.put(10, 4, "Y")
    bird.put(8, 2, "z")
    bird.put(5, 11, "Y")
    bird.put(7, 11, "Y")
    acc = {"pauldron_deco": (bird.text(), (-1, 7))}
    return {"palette": pal, "pieces": P, "accessories": acc}


def thousand_roads():
    pal = {"#": (40, 10, 14), "M": (120, 24, 30), "m": (170, 40, 44), "w": (210, 80, 70), "W": (240, 150, 130),
           "N": (30, 30, 40), "B": (60, 60, 76), "b": (90, 90, 110), "n": (120, 120, 140),
           "D": (18, 22, 50), "T": (34, 44, 90), "t": (56, 70, 130), "g": (90, 110, 170),
           "V": (110, 80, 20), "Y": (200, 150, 50), "y": (240, 200, 100), "v": (255, 236, 160),
           "K": (140, 110, 50), "k": (200, 170, 90), "j": (236, 214, 140), "J": (100, 76, 30),
           "i": (240, 240, 236), "u": (200, 200, 196), **SKIN}
    P = {}
    c = helm_canvas()
    hat = c.mask_poly([(18, 6), (36, 19), (34, 21), (2, 21), (0, 19)])
    c.shade(hat, ("J", "K", "k"), edge="J")
    for i in range(1, 8):
        c.line(18, 7, 2 + i * 4, 20, "J", hat)
    c.line(1, 20, 35, 20, "j", hat)
    for y in range(22, 34):
        c.put(28 + (y - 22) // 6, y, "i")
    P["helmet"] = helm_piece(c)

    def lamellar(c, m):
        metal(c, m)
        for x, y in m:
            if y % 3 == 0:
                c.put(x, y, "#")
            elif x % 3 == 0:
                c.put(x, y, "M")
        for x, y in m:
            if y % 6 == 1 and x % 3 == 1:
                c.put(x, y, "Y")
    c, o = torso_canvas()
    coat = c.mask_poly([(o - 1, 0), (o + 23, 0), (o + 24, 34), (o - 2, 34)])
    cloth(c, coat)
    do = c.mask_poly([(o + 5, 2), (o + 17, 2), (o + 17, 21), (o + 5, 21)])
    lamellar(c, do)
    obi = c.mask_rect(o + 1, 20, o + 21, 24)
    c.shade(obi, ("V", "Y", "y"), edge="V")
    c.line(o + 4, 0, o + 4, 34, "D", coat)
    c.line(o + 18, 0, o + 18, 34, "D", coat)
    P["torso"] = torso_piece(c, o)
    c, o = torso_canvas(22)
    coat = c.mask_rect(3, 0, 19, 34)
    cloth(c, coat)
    d = c.mask_rect(11, 2, 17, 21)
    lamellar(c, d)
    obi = c.mask_rect(3, 20, 19, 24)
    c.shade(obi, ("V", "Y", "y"), edge="V")
    P["torso_side"] = side_piece(c, 0)
    c, o = torso_canvas()
    coat = c.mask_poly([(o - 1, 0), (o + 23, 0), (o + 24, 34), (o - 2, 34)])
    cloth(c, coat)
    mon = c.mask_ellipse(o + 11, 8, 3, 3)
    c.shade(mon, ("i", "i", "u"), edge="D")
    obi = c.mask_rect(o + 1, 20, o + 23, 24)
    c.shade(obi, ("V", "Y", "y"), edge="V")
    P["torso_back"] = back_piece(c, o)
    for short in (False, True):
        c = Canvas(20, 18 if short else 22)
        sleeve = c.mask_poly([(4, 3), (16, 3), (18, c.h), (2, c.h)])
        cloth(c, sleeve)
        sode = c.mask_rect(4, 1, 16, 10)
        lamellar(c, sode)
        P["upper_arm_short" if short else "upper_arm"] = pauldron_piece(c, 7, 1)
    P["elbow_cap"] = elbow(cloth)
    P["coat_tail"] = tail(cloth, 12, 18)
    P.update(standard_limbs(lamellar, cloth, cloth, cloth, cloth))
    from armor2x import piece as _piece
    legs = leg_pieces(cloth, cloth, knee_fill=cloth)
    for key, h, dy in (("thigh", 20, -1), ("shin", 19, 0)):
        hc = Canvas(14, h)
        hm = hc.mask_poly([(2, 0), (12, 0), (14, h), (0, h)])
        cloth(hc, hm)
        for x in (4, 7, 10):
            hc.line(x, 1, x + (x - 7) // 3, h - 1, "D", hm)
        legs[key] = _piece(hc, key, -3, dy)
    P.update(legs)

    def tabi(c, m):
        c.shade(m, ("u", "i", "i"), edge="#")
    P.update(boot_pieces(5, tabi, lambda c, m: (tabi(c, m), [c.put(x, 7, "K") for x, y in m if y == 7]), cuff=0))
    charms = Canvas(8, 10)
    for x, col in ((0, "M"), (4, "T")):
        cm = charms.mask_rect(x, 2, x + 3, 8)
        charms.shade(cm, (col, col, "Y"), edge="#")
        charms.put(x + 1, 0, "Y")
        charms.put(x + 1, 1, "Y")
    acc = {"belt_front": (charms.text(), (-5, -1))}
    return {"palette": pal, "pieces": P, "accessories": acc}


def rift_wind():
    pal = {"#": (8, 14, 30), "M": (150, 196, 210), "m": (200, 230, 236), "w": (232, 246, 248), "W": (255, 255, 255),
           "D": (12, 24, 46), "T": (28, 52, 86), "t": (44, 82, 124), "g": (70, 120, 164),
           "E": (110, 250, 236), "L": (230, 255, 252), "z": (6, 10, 20), "N": (8, 14, 30), "B": (20, 36, 62), "b": (30, 56, 92), "n": (44, 82, 124),
           "V": (40, 150, 160), "Y": (110, 250, 236), "y": (200, 255, 250), "v": (255, 255, 255)}
    P = {}

    def glow_lines(c, m, step=7, slope=2):
        for y0 in range(-c.w, c.h, step):
            c.line(0, y0, c.w, y0 + slope * 3, "E", m)

    def robe(c, m):
        cloth(c, m)
        glow_lines(c, m, 9, 1)
    c = helm_canvas()
    hood(c, cloth, tip=False)
    hm = {(x, y) for y in range(c.h) for x in range(c.w) if c.get(x, y) not in (".", None)}
    for x, y in ((5, 12), (5, 18)):
        c.line(x, y, x - 1, y + 14, "E", hm)
    x0, y0, x1, y1 = FACE
    mask = c.mask_poly([(x0, y0), (x1, y0), (x1, y1 - 6), (x0 + (x1 - x0) // 2 + 3, y1), (x0 + 3, y1 - 2), (x0, y1 - 6)])
    metal(c, mask)
    for x, y in ((x0 + 5, y0 + 5), (x0 + 6, y0 + 5), (x0 + 13, y0 + 5), (x0 + 14, y0 + 5)):
        c.put(x, y, "E")
    c.put(x0 + 5, y0 + 4, "L")
    c.put(x0 + 14, y0 + 4, "L")
    c.line(x0 + 10, y0 + 1, x0 + 10, y0 + 3, "E")
    P["helmet"] = helm_piece(c)
    c, o = torso_canvas()
    body = c.mask_poly([(o + 1, 0), (o + 21, 0), (o + 22, 33), (o, 33)])
    robe(c, body)
    wrap = c.mask_poly([(o + 2, 2), (o + 20, 2), (o + 18, 6), (o + 4, 6)])
    metal(c, wrap)
    sash = c.mask_rect(o, 20, o + 22, 23)
    metal(c, sash)
    c.put(o + 11, 21, "E")
    P["torso"] = torso_piece(c, o)
    c, o = torso_canvas(22)
    body = c.mask_rect(4, 0, 18, 33)
    robe(c, body)
    sash = c.mask_rect(4, 20, 18, 23)
    metal(c, sash)
    P["torso_side"] = side_piece(c, 0)
    c, o = torso_canvas()
    body = c.mask_poly([(o + 1, 0), (o + 21, 0), (o + 22, 33), (o, 33)])
    robe(c, body)
    sash = c.mask_rect(o, 20, o + 22, 23)
    metal(c, sash)
    P["torso_back"] = back_piece(c, o)
    for short in (False, True):
        c = Canvas(16, 18 if short else 22)
        arm = c.mask_rect(5, 3, 11, c.h)
        robe(c, arm)
        loop = c.mask_ellipse(8, 4, 7, 4)
        cloth(c, loop)
        c.line(1, 5, 15, 3, "E", loop)
        P["upper_arm_short" if short else "upper_arm"] = pauldron_piece(c, 5, 1)
    P["elbow_cap"] = elbow(cloth)
    P["coat_tail"] = tail(robe)

    def wraps(c, m):
        cloth(c, m)
        for y in range(1, c.h, 3):
            c.line(0, y, c.w, y + 1, "g", m)
    P.update(standard_limbs(wraps, wraps, cloth, wraps, cloth, hand_extra=lambda c, m: c.put(5, 4, "E")))
    P.update(leg_pieces(cloth, cloth, knee_fill=cloth, shin_extra=lambda c, m: [c.line(1, y, 9, y + 3, "E", m) for y in (3, 11)]))
    P.update(boot_pieces(6, wraps, cloth, cuff=0, toe="point"))
    feathers = Canvas(18, 22)
    for (cx, cy, h) in ((4, 9, 7), (10, 6, 8), (15, 12, 6)):
        fm = feathers.mask_poly([(cx, cy - h), (cx + 2, cy), (cx, cy + h), (cx - 2, cy)])
        feathers.shade(fm, ("V", "E", "L"), edge="#")
        feathers.line(cx, cy - h + 1, cx, cy + h - 1, "L", fm)
    cc, cp = cape(10, 22, 46, robe)
    acc = {"back_item": (feathers.text(), (4, 21)), "cape": (cc.text(), cp)}
    return {"palette": pal, "pieces": P, "accessories": acc}


LINE = {"huntress_trail": huntress_trail, "jungle_wind": jungle_wind, "sand_track": sand_track,
        "falcon_wing": falcon_wing, "thousand_roads": thousand_roads, "rift_wind": rift_wind}
HIDES_HAIR = {"rift_wind"}


def build():
    return {k: f() for k, f in LINE.items()}
