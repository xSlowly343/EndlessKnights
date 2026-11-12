import sys

FAMILIES = ("hit", "queue", "auto", "strong")


def blocks(lines):
    start = None
    for index, line in enumerate(lines):
        if line == "    {":
            start = index
        elif line in ("    },", "    }") and start is not None:
            yield start, index
            start = None


def block_id(lines, start):
    return lines[start + 1].split('"')[3]


def fold(lines, class_id):
    for family in FAMILIES:
        found = {block_id(lines, s): (s, e) for s, e in blocks(lines)}
        forms = [found[f"{class_id}_{family}_{rank}"] for rank in (1, 2, 3)]
        nested = []
        for number, (start, end) in enumerate(forms[1:]):
            body = ["    " + line for line in lines[start:end]]
            body.append("        }" + ("," if number == 0 else ""))
            nested += body
        for start, end in sorted(forms[1:], reverse=True):
            del lines[start:end + 1]
        first_start, first_end = forms[0]
        if lines[first_end - 1] != "      ]":
            raise SystemExit(f"{class_id}_{family}_1: branches are not the last field")
        lines[first_end - 1] = "      ],"
        lines[first_end:first_end] = ['      "evolutions": ['] + nested + ["      ]"]
    return lines


def drop(lines, skill_id):
    for start, end in blocks(lines):
        if block_id(lines, start) == skill_id:
            del lines[start:end + 1]
            return lines
    raise SystemExit(f"{skill_id}: not found")


def main():
    path, class_id, removed = sys.argv[1], sys.argv[2], sys.argv[3:]
    with open(path, encoding="utf-8", newline="") as source:
        lines = source.read().split("\n")
    fold(lines, class_id)
    for skill_id in removed:
        drop(lines, skill_id)
    with open(path, "w", encoding="utf-8", newline="") as target:
        target.write("\n".join(lines))


if __name__ == "__main__":
    main()
