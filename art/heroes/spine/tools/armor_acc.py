EXTRA = {"R": (140, 34, 38), "r": (200, 64, 58), "U": (150, 138, 116), "u": (212, 202, 178), "i": (244, 238, 222),
         "L": (255, 250, 222), "X": (90, 94, 106), "Q": (168, 172, 184)}

SPRITES = {
    "plume": ("""
....oo..
..oDTTo.
.oDTTtgo
oDTtgoo.
oDto....
.oYo....
..o.....
""", (2, 5)),
    "halo": ("""
.oLLLLLo.
oLE...ELo
.oLLLLLo.
""", (4, 6)),
    "horns": ("""
o...........o
uo.........ou
iuo.......oui
.iuo.....oui.
..iuo...oui..
...oo...oo...
""", (6, 5)),
    "seal": ("""
.oRo.
oRrRo
.oRo.
.pqp.
.p.q.
.q.p.
""", (2, -2)),
    "spikes": ("""
o.o.o
WoWoW
MoMoM
""", (2, 3)),
    "fur": ("""
.oKKko.
oKkkkjo
oJKkKjo
.oJoJo.
""", (3, 1)),
    "skull": ("""
.oio.
oiiio
ozizo
.oio.
""", (2, 1)),
    "runes": ("""
L.E
.E.
E.L
.L.
""", (1, -2)),
    "pouch": ("""
oNNo
oBbo
oBno
.oo.
""", (-2, 0)),
    "scroll": ("""
oYo
oqo
opo
oqo
oYo
""", (-2, 0)),
    "ribbon": ("""
oTo..
oTto.
.oTo.
.oTto
..oTo
...o.
""", (-1, 0)),
    "lantern": ("""
.o.
oMo
oEo
oLo
oMo
.o.
""", (1, -1)),
    "flask": ("""
.o.
oqo
oGo
oCo
oGo
.o.
""", (1, -1)),
    "chain_skull": ("""
.X.
.Q.
.X.
.Q.
oio
izi
.u.
""", (1, -1)),
    "crystal": ("""
.o.
oCo
oco
oGo
.o.
""", (1, -1)),
    "banner": ("""
oDTTTTTTToBo
oTTTTYTTToBo
oTTTYYYTToBo
oTTTTYTTToBo
oTTTTYTTToBo
oTTTTYTTgoBo
oVYyYyYyvoBo
o.o..o..ooBo
.........oBo
.........oBo
.........oBo
.........oBo
.........oBo
.........oBo
.........oBo
.........oBo
.........oBo
.........oBo
.........oBo
.........oBo
.........oBo
.........oBo
.........oBo
.........oBo
.........oBo
.........oBo
.........oBo
.........oBo
.........oBo
.........oBo
.........oBo
.........oBo
.........oYo
""", (10, 32)),
    "wings": ("""
..............oo
............ooLo
..........ooLLLo
.........oLLLELo
........oLLELLLo
.......oLLELLLo.
......oLLELLLLo.
.....oLLELLLELo.
....oLLELLLELo..
...oLLELLLELLo..
..oLLLLLLELLo...
.oLELLLLELLo....
oLLLLELLLLo.....
oLELLLLLLo......
.oLLLELLo.......
..oLLLLo........
...oooo.........
""", (13, 12)),
    "wings_dark": ("""
..............oo
............ooDo
..........ooDTTo
.........oDTTgTo
........oDTgTTTo
.......oDTgTTTo.
......oDTgTTTTo.
.....oDTgTTTgTo.
....oDTgTTTgTo..
...oDTgTTTgTTo..
..oDTTTTTgTTo...
.oDgTTTTgTTo....
oDTTTgTTTTo.....
oDgTTTTTTo......
.oDTTgTTo.......
..oDTTTo........
...oooo.........
""", (13, 12)),
}

TIER = {1: 0, 2: 0, 3: 0, 4: 1, 5: 1, 6: 2, 7: 2, 8: 3, 9: 3, 10: 4, 11: 4}

THEMES = {
    "plate_early": {1: [("chest", "belt_front", "pouch")], 2: [("chest", "pauldron_deco", "seal")],
                    3: [("chest", "pauldron_deco", "spikes"), ("chest", "belt_hang", "chain_skull")], 4: [("helmet", "crest", "halo")]},
    "plate_late": {1: [("chest", "belt_front", "ribbon")], 2: [("helmet", "crest", "plume"), ("chest", "pauldron_deco", "seal")],
                   3: [("chest", "back_item", "banner")], 4: [("chest", "back_item", "wings"), ("helmet", "crest", "halo")]},
    "leather_early": {1: [("chest", "belt_front", "pouch")], 2: [("chest", "belt_hang", "lantern")],
                      3: [("chest", "pauldron_deco", "fur"), ("helmet", "crest", "horns")], 4: [("chest", "pauldron_deco", "runes")]},
    "leather_late": {1: [("chest", "belt_front", "pouch")], 2: [("chest", "belt_hang", "flask")],
                     3: [("chest", "pauldron_deco", "skull"), ("chest", "belt_hang", "chain_skull")], 4: [("chest", "back_item", "wings_dark")]},
    "cloth_early": {1: [("chest", "belt_front", "scroll")], 2: [("chest", "belt_hang", "lantern")],
                    3: [("chest", "pauldron_deco", "runes")], 4: [("helmet", "crest", "halo")]},
    "cloth_late": {1: [("chest", "belt_front", "scroll")], 2: [("chest", "pauldron_deco", "runes"), ("chest", "belt_hang", "crystal")],
                   3: [("helmet", "crest", "halo")], 4: [("chest", "back_item", "wings")]},
}

CREST_LIFT = {"plate_late": 3, "cloth_early": 6, "cloth_late": 1}


def accessories(base, rarity):
    chosen = {}
    for level in range(1, TIER[rarity] + 1):
        for item_slot, slot, name in THEMES[base].get(level, []):
            chosen[(item_slot, slot)] = name
    return chosen


def sprite(base, slot, name):
    txt, pivot = SPRITES[name]
    px, py = pivot
    if slot == "crest":
        py += CREST_LIFT.get(base, 0)
    return txt, [px, py]
