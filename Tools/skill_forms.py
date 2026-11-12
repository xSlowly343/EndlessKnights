import json
import math
import sys

FAMILIES = ("hit", "queue", "auto", "strong")
ABSOLUTE = ("multiplier", "lowHpMultiplier", "pauseMultiplier", "queueMultiplier")


class Skills:
    def __init__(self, path):
        self.path = path
        with open(path, encoding="utf-8", newline="") as source:
            self.lines = source.read().split("\n")

    def save(self):
        with open(self.path, "w", encoding="utf-8", newline="") as target:
            target.write("\n".join(self.lines))

    def start(self, skill_id):
        needle = f'"id": "{skill_id}",'
        found = [i for i, line in enumerate(self.lines) if line.strip() == needle]
        if len(found) != 1:
            raise SystemExit(f"{skill_id}: found {len(found)} times")
        return found[0]

    def field_line(self, skill_id, field):
        index = self.start(skill_id)
        indent = self.lines[index][: len(self.lines[index]) - len(self.lines[index].lstrip())]
        prefix = f'{indent}"{field}": '
        for at in range(index + 1, len(self.lines)):
            if self.lines[at].startswith(prefix):
                return at, prefix
            if self.lines[at].startswith(indent + '"id"') or len(self.lines[at]) - len(self.lines[at].lstrip()) < len(indent):
                break
        raise SystemExit(f"{skill_id}: no field {field}")

    def get(self, skill_id, field):
        at, prefix = self.field_line(skill_id, field)
        return json.loads(self.lines[at][len(prefix):].rstrip(","))

    def set(self, skill_id, field, value):
        at, prefix = self.field_line(skill_id, field)
        comma = "," if self.lines[at].endswith(",") else ""
        self.lines[at] = prefix + dump(value) + comma

    def branch_lines(self, skill_id):
        at, _ = self.field_line(skill_id, "branches")
        result = []
        for index in range(at + 1, len(self.lines)):
            text = self.lines[index].strip()
            if not text.startswith("{"):
                break
            result.append(index)
        return result

    def branches(self, skill_id):
        return [json.loads(self.lines[i].strip().rstrip(",")) for i in self.branch_lines(skill_id)]

    def set_branches(self, skill_id, tiers):
        for index, tier in zip(self.branch_lines(skill_id), tiers):
            line = self.lines[index]
            indent = line[: len(line) - len(line.lstrip())]
            comma = "," if line.endswith(",") else ""
            self.lines[index] = indent + dump(tier) + comma

    def edit_effect(self, skill_id, **changes):
        effect = self.get(skill_id, "effect")
        effect.update(changes)
        self.set(skill_id, "effect", effect)

    def edit_text(self, skill_id, ru, en):
        self.set(skill_id, "text", {"ru": ru, "en": en})

    def edit_branch(self, skill_id, name_ru, **changes):
        tiers = self.branches(skill_id)
        hits = 0
        for tier in tiers:
            for option in tier["options"]:
                if option["name"]["ru"] == name_ru:
                    option.setdefault("effect", {}).update(changes)
                    hits += 1
        if hits != 1:
            raise SystemExit(f"{skill_id}: branch {name_ru} found {hits} times")
        self.set_branches(skill_id, tiers)


def merge_aura(skills, target, removed):
    effect = skills.get(target, "effect")
    effect["modifiers"] += skills.get(removed, "effect")["modifiers"]
    skills.set(target, "effect", effect)
    tiers = skills.branches(target)
    for tier, source in zip(tiers, skills.branches(removed)):
        for option, other in zip(tier["options"], source["options"]):
            if "modifiers" in option.get("effect", {}):
                option["effect"]["modifiers"] += other["effect"]["modifiers"]
    skills.set_branches(target, tiers)


def dump(value):
    return json.dumps(clean(value), ensure_ascii=False)


def clean(value):
    if isinstance(value, float) and value == int(value):
        return int(value)
    if isinstance(value, float):
        return round(value, 6)
    if isinstance(value, dict):
        return {key: clean(item) for key, item in value.items()}
    if isinstance(value, list):
        return [clean(item) for item in value]
    return value


def total(effect):
    targets = 1 + effect.get("queueTargets", 0)
    if effect["type"] == "strike":
        return effect["multiplier"] * targets
    if effect["type"] == "flurry":
        return effect["hits"] * effect["multiplier"] * targets
    return None


def number(value, comma):
    text = dump(value)
    return text.replace(".", ",") if comma else text


def rescale_text(text, pairs):
    for language, comma in (("ru", True), ("en", False)):
        for old, new in pairs:
            text[language] = text[language].replace("×" + number(old, comma), "\u0000" + number(new, comma))
        text[language] = text[language].replace("\u0000", "×")
    return text


def set_multiplier(skills, skill_id, new):
    effect = skills.get(skill_id, "effect")
    step = 0.5 if effect["type"] == "strike" else 0.1
    old = effect["multiplier"]
    factor = new / old
    pairs = [(old, new)]
    boss = effect.get("bossMultiplier")
    if boss:
        pairs.insert(0, (old * boss, new * boss))
    effect["multiplier"] = new
    for key in ABSOLUTE[1:]:
        if key in effect:
            scaled = round(effect[key] * factor * 2) / 2
            pairs.insert(0, (effect[key], scaled))
            effect[key] = scaled
    skills.set(skill_id, "effect", effect)
    tiers = skills.branches(skill_id)
    for tier in tiers:
        for option in tier["options"]:
            for key in ABSOLUTE:
                if key in option.get("effect", {}):
                    option["effect"][key] = round(option["effect"][key] * factor / step) * step
    skills.set_branches(skill_id, tiers)
    return pairs


def raise_form(skills, skill_id, need):
    effect = skills.get(skill_id, "effect")
    step = 0.5 if effect["type"] == "strike" else 0.1
    old = effect["multiplier"]
    new = math.ceil(old * need / total(effect) / step - 1e-9) * step
    pairs = set_multiplier(skills, skill_id, new)
    skills.set(skill_id, "text", rescale_text(skills.get(skill_id, "text"), pairs))
    return old, new


def strengthen(skills, class_id, apply):
    for family in FAMILIES:
        previous = None
        for rank in (1, 2, 3):
            skill_id = f"{class_id}_{family}_{rank}"
            effect = skills.get(skill_id, "effect")
            cooldown = skills.get(skill_id, "cooldownSeconds")
            cost = sum(skills.get(skill_id, "cost").values())
            power = total(effect)
            note = ""
            if power is None:
                note = f"  !! {effect['type']}: compare by hand"
            elif previous and previous[0] is not None:
                need = max(previous[0] * 1.0001, previous[0] * cooldown / previous[1])
                if power < need - 1e-9:
                    if apply:
                        old, new = raise_form(skills, skill_id, need)
                        power = total(skills.get(skill_id, "effect"))
                        note = f"  raised ×{dump(old)} -> ×{dump(new)}"
                    else:
                        note = f"  !! weaker: needs total {need:.2f}"
            print(f"{skill_id}: total {dump(power) if power is not None else '?'} / {dump(cooldown)} s / {dump(cost)}{note}")
            print(f"    {skills.get(skill_id, 'text')['ru']}")
            previous = (power, cooldown, cost)


def main():
    path, command, class_id = sys.argv[1], sys.argv[2], sys.argv[3]
    skills = Skills(path)
    strengthen(skills, class_id, command == "raise")
    if command == "raise":
        skills.save()


if __name__ == "__main__":
    main()
