from pix import Canvas

METAL = ("M", "m", "w")
GOLD = ("V", "Y", "y")
CLOTH = ("D", "T", "t")
BODY_PIVOT = {"torso": (10.5, 26.5), "torso_side": (10.5, 26.5), "torso_back": (10.5, 26.5), "helmet": (18.5, 34.5),
              "upper_arm": (2.5, 0.5), "upper_arm_short": (2.5, 0.5), "forearm": (2.5, 0.5), "elbow_cap": (0.5, 0.5),
              "hand": (2.5, 2.5), "bracer": (2.5, 0.5), "shin": (4.5, 0.5), "foot": (2.5, 0.5), "coat_tail": (4.5, 0.5),
              "thigh": (4.5, 0.5), "boot_top": (4.5, 0.5)}


def piece(c, part, dx, dy):
    bx, by = BODY_PIVOT[part]
    return c.text(), (bx - dx, by - dy)


def rivets(c, pts):
    for x, y in pts:
        c.put(x, y, "W")


def knight_helmet():
    c = Canvas(34, 36)
    dome = c.mask_poly([(3, 11), (5, 5), (10, 2), (18, 1), (26, 2), (31, 7), (33, 14), (33, 28), (30, 33), (21, 35), (8, 35), (3, 31), (2, 21)])
    c.shade(dome, METAL, spec="W", edge="#")
    c.line(22, 2, 22, 19, "y", dome)
    c.line(23, 2, 23, 19, "Y", dome)
    for x in range(13, 33):
        c.put(x, 21, "#")
        c.put(x, 22, "z")
        c.put(x, 23, "z")
        c.put(x, 24, "#")
    c.line(22, 24, 22, 33, "Y", dome)
    c.line(23, 24, 23, 33, "y", dome)
    for y in (27, 30):
        for x in (26, 29, 31):
            c.put(x, y, "z")
    rim = [(x, y) for (x, y) in dome if y in (33, 34) and (x, y + 1) not in dome]
    for x, y in dome:
        if (x, y + 2) not in dome and (x, y + 1) in dome:
            c.put(x, y, "Y")
    rivets(c, [(6, 30), (10, 32), (16, 33), (28, 31)])
    c.put(23, 12, "G")
    c.put(22, 12, "c")
    return piece(c, "helmet", -1, -1)


def knight_torso(width=26, view="front"):
    c = Canvas(width, 37)
    o = (width - 22) // 2
    plate = c.mask_poly([(o + 2, 2), (o + 7, 0), (o + 15, 0), (o + 20, 2), (o + 21, 9), (o + 20, 18), (o + 2, 18), (o + 1, 9)])
    c.shade(plate, METAL, spec="W", edge="#")
    if view != "back":
        c.line(o + 13, 3, o + 13, 16, "W", plate)
        c.line(o + 12, 3, o + 12, 16, "w", plate)
        for x in range(o + 4, o + 19):
            c.put(x, 12, "#" if (x - o) % 7 else "W")
    belt = c.mask_rect(o + 1, 18, o + 21, 21)
    c.shade(belt, GOLD, edge="V")
    c.put(o + 11, 19, "G")
    c.put(o + 12, 19, "c")
    for i, y in enumerate((21, 24, 27)):
        lame = c.mask_poly([(o + 1 - i, y), (o + 21 + i, y), (o + 21 + i, y + 3), (o + 1 - i, y + 3)])
        c.shade(lame, METAL, spec="W", edge="#")
    for side in (0, 1):
        x0 = o - 2 if side == 0 else o + 16
        tas = c.mask_poly([(x0, 29), (x0 + 8, 29), (x0 + 7, 35), (x0 + 1, 35)])
        c.shade(tas, METAL, spec="W", edge="#")
        for x in range(x0 + 1, x0 + 7):
            c.put(x, 30, "Y")
    if view == "front":
        tab = c.mask_poly([(o + 8, 20), (o + 15, 20), (o + 15, 36), (o + 8, 36)])
        c.shade(tab, CLOTH, edge="D")
        c.line(o + 11, 23, o + 11, 33, "Y")
        c.line(o + 12, 23, o + 12, 33, "y")
        c.line(o + 9, 26, o + 14, 26, "Y")
        c.line(o + 9, 27, o + 14, 27, "y")
        c.line(o + 8, 36, o + 15, 36, "V")
    if view == "back":
        cape = c.mask_poly([(o + 1, 1), (o + 21, 1), (o + 22, 36), (o, 36)])
        c.shade(cape, CLOTH, edge="D")
        for x in (o + 6, o + 11, o + 16):
            c.line(x, 4, x + 1, 35, "D", cape)
        c.line(o, 36, o + 22, 36, "V")
        c.put(o + 3, 2, "Y")
        c.put(o + 18, 2, "Y")
    return piece(c, "torso", -o, 0)


def knight_torso_side():
    c = Canvas(22, 37)
    plate = c.mask_poly([(5, 1), (10, 0), (16, 1), (18, 8), (17, 18), (5, 18), (4, 8)])
    c.shade(plate, METAL, spec="W", edge="#")
    c.line(14, 3, 14, 16, "W", plate)
    belt = c.mask_rect(4, 18, 18, 21)
    c.shade(belt, GOLD, edge="V")
    c.put(13, 19, "G")
    for i, y in enumerate((21, 24, 27)):
        lame = c.mask_rect(4 - i, y, 18 + i, y + 3)
        c.shade(lame, METAL, spec="W", edge="#")
    tab = c.mask_rect(11, 20, 17, 36)
    c.shade(tab, CLOTH, edge="D")
    c.line(14, 23, 14, 33, "Y")
    c.line(12, 26, 16, 26, "Y")
    return piece(c, "torso_side", 0, 0)


def knight_pauldron(short=False):
    h = 18 if short else 24
    c = Canvas(20, h)
    for cy, rx, ry in ((14.5, 7.5, 2.6), (11.5, 9, 3.2)):
        m = c.mask_ellipse(10, cy, rx, ry)
        c.shade(m, METAL, spec="W", edge="#")
        for x, y in m:
            if (x, y + 1) not in m and (x, y) not in ((x, y)):
                pass
    top = c.mask_ellipse(10, 6.5, 10, 6.5)
    c.shade(top, METAL, spec="W", edge="#")
    for x, y in top:
        if (x, y + 1) not in top and (x, y - 1) in top and y > 6:
            c.put(x, y, "Y")
            c.put(x, y - 1, "y")
    rivets(c, [(4, 7), (10, 3), (16, 7)])
    c.put(10, 6, "G")
    arm = c.mask_rect(7, 16, 13, h)
    c.shade(arm, METAL, spec="W", edge="#")
    c.line(7, h - 1, 12, h - 1, "Y")
    return piece(c, "upper_arm", -7, -3)


def knight_vambrace():
    c = Canvas(8, 12)
    m = c.mask_rect(0, 0, 8, 12)
    c.shade(m, METAL, spec="W", edge="#")
    for x in range(1, 7):
        c.put(x, 1, "Y")
        c.put(x, 10, "Y")
    return piece(c, "forearm", -1, 0)


def knight_couter():
    c = Canvas(10, 10)
    m = c.mask_ellipse(5, 5, 5, 5)
    c.shade(m, METAL, spec="W", edge="#")
    c.put(5, 5, "Y")
    c.put(4, 5, "y")
    bx, by = 4.5, 4.5
    return c.text(), (bx, by)


def knight_gauntlet():
    c = Canvas(10, 11)
    fist = c.mask_poly([(1, 2), (8, 1), (10, 4), (10, 9), (7, 11), (2, 11), (0, 8)])
    c.shade(fist, METAL, spec="W", edge="#")
    for y in (5, 7, 9):
        c.line(2, y, 8, y, "#", fist)
    c.line(1, 2, 8, 1, "Y")
    return piece(c, "hand", -2, -1)


def knight_cuff():
    c = Canvas(10, 6)
    m = c.mask_poly([(1, 0), (9, 0), (10, 5), (0, 5)])
    c.shade(m, METAL, spec="W", edge="#")
    c.line(0, 5, 10, 5, "Y")
    return piece(c, "bracer", -2, 6)


def knight_greave():
    c = Canvas(12, 20)
    body = c.mask_poly([(2, 5), (10, 5), (10, 15), (9, 20), (3, 20), (2, 15)])
    c.shade(body, METAL, spec="W", edge="#")
    c.line(8, 7, 8, 18, "W", body)
    knee = c.mask_ellipse(6, 4, 6, 4)
    c.shade(knee, METAL, spec="W", edge="#")
    c.put(6, 4, "G")
    for x in range(2, 11):
        c.put(x, 8, "Y")
    return piece(c, "shin", -2, -2)


def knight_sabaton():
    c = Canvas(16, 8)
    m = c.mask_poly([(1, 0), (8, 0), (11, 3), (15, 5), (16, 8), (0, 8)])
    c.shade(m, METAL, spec="W", edge="#")
    for x0 in (4, 8, 11):
        c.line(x0, 1, x0 + 1, 7, "#", m)
    c.line(0, 7, 16, 7, "V")
    return piece(c, "foot", -1, -1)


def knight_culet():
    c = Canvas(12, 12)
    for i, y in enumerate((0, 4, 8)):
        m = c.mask_rect(i, y, 11 - i, y + 4)
        c.shade(m, METAL, spec="W", edge="#")
    c.line(3, 11, 8, 11, "Y")
    return piece(c, "coat_tail", -1, 0)


def knight_legs():
    from forge2x import leg_pieces, boot_pieces, rivets as _r
    def metal(c, m):
        c.shade(m, METAL, spec="W", edge="#")
    out = leg_pieces(metal, metal, knee_fill=metal, knee_r=6,
                     thigh_extra=lambda c, m: [c.put(x, 2, "Y") for x in range(2, 9)],
                     shin_extra=lambda c, m: [c.put(7, y, "W") for y in range(3, 16)])
    knee = Canvas(12, 12)
    km = knee.mask_ellipse(6, 6, 6, 6)
    metal(knee, km)
    knee.put(6, 6, "G")
    knee.put(5, 6, "c")
    out["knee_cap"] = (knee.text(), (5.5, 5.5))
    out.update(boot_pieces(4, metal, metal, foot_w=16))
    return out


def plate_late():
    return {
        "helmet": knight_helmet(),
        "torso": knight_torso(),
        "torso_side": knight_torso_side(),
        "torso_back": knight_torso(view="back"),
        "upper_arm": knight_pauldron(),
        "upper_arm_short": knight_pauldron(short=True),
        "forearm": knight_vambrace(),
        "elbow_cap": knight_couter(),
        "hand": knight_gauntlet(),
        "bracer": knight_cuff(),
        "coat_tail": knight_culet(),
        **knight_legs(),
    }
