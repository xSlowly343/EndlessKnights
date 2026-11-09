import base64
import json
import os
import subprocess
import sys

import numpy as np
from PIL import Image

import doll_base as DB

sys.path.insert(0, DB.DOLL_SRC)
sys.argv = sys.argv[:1] + [DB.DOLL_SRC]
import rig3 as R
from pal import MAT, OUTLINE

CHECK = os.path.join(DB.ROOT, "base", "hero", "check")
PROBE = os.path.join(DB.ROOT, "probe")

KEY_TO_PART = {"torso": "torso", "torso_side": "torso_side", "torso_back": "torso_back", "pelvis": "pelvis",
               "uF": "upper_arm", "uB": "upper_arm", "uF_short": "upper_arm_short", "fF": "forearm", "fB": "forearm",
               "hF": "hand", "hB": "hand", "thF": "thigh", "thB": "thigh", "shF": "shin", "shB": "shin", "ftF": "foot", "ftB": "foot",
               "coat": "coat_tail", "w": "weapon_sword"}
FAR = {"uB", "fB", "hB", "thB", "shB", "ftB", "coat"}


def capture(doll, pose):
    calls, layers = [], []
    original = R.place

    def spy(canvas, part, piv, pos, ang):
        if id(canvas) not in layers:
            layers.append(id(canvas))
        key = next(k for k, v in doll.p.items() if v is part)
        calls.append((layers.index(id(canvas)), key, tuple(piv), tuple(pos), ang))
        original(canvas, part, piv, pos, ang)

    R.place = spy
    try:
        frame = doll.frame(pose)
    finally:
        R.place = original
    return calls, frame


def head_attachments(sex):
    parts, _ = DB.head_parts(sex)
    out = [(f"head_{sex}", parts[f"head_{sex}"][1]["pivot"], (0, 0))]
    if f"blush_{sex}" in parts:
        out.append((f"blush_{sex}", [9, 17], (0, 0)))
    for side in ("far", "near"):
        meta = parts[f"eye_{sex}_{side}_open"][1]
        out.append((f"eye_{sex}_{side}_open", [9, 17], tuple(meta["origin_on_head"])))
    out.append((f"hair_{sex}_front", [9, 17], (0, 0)))
    return out


def offset(size, piv, origin=(0, 0)):
    w, h = size
    cx = origin[0] + w / 2 - (piv[0] + 0.5)
    cy = origin[1] + h / 2 - (piv[1] + 0.5)
    return cx, -cy


def build_skeleton(sex, calls, sizes):
    bones = [{"name": "root"}]
    slots, attachments = [], {}
    ranges = {}
    for i, (layer, key, piv, pos, ang) in enumerate(calls):
        bone = f"b{i}_{key}"
        bones.append({"name": bone, "parent": "root", "x": pos[0], "y": -pos[1], "rotation": ang})
        if key == "head":
            items = head_attachments(sex)
        elif key == "hairfly":
            items = [(f"hair_{sex}_fly", list(piv), (0, 0))]
        else:
            items = [(KEY_TO_PART[key], list(piv), (0, 0))]
        for name, p, origin in items:
            slot = f"s{len(slots)}_{name}"
            entry = {"name": slot, "bone": bone, "attachment": name}
            if key in FAR:
                entry["color"] = "e6e6e6ff"
            slots.append(entry)
            ranges.setdefault(layer, [len(slots) - 1, len(slots) - 1])[1] = len(slots) - 1
            w, h = sizes[name]
            x, y = offset((w, h), p, origin)
            attachments[slot] = {name: {"x": x, "y": y, "width": w, "height": h}}
    skel = {"skeleton": {"spine": "4.2.00"}, "bones": bones, "slots": slots, "skins": [{"name": "default", "attachments": attachments}]}
    return skel, ranges


def pack_atlas(names, path_png, path_atlas):
    ims = {n: Image.open(os.path.join(DB.OUT, f"parts_{DB.SCALE}x", n + ".png")) for n in names}
    W, x, y, row = 1024, 0, 0, 0
    pos = {}
    for n, im in sorted(ims.items(), key=lambda kv: -kv[1].height):
        if x + im.width > W:
            x, y, row = 0, y + row + 2, 0
        pos[n] = (x, y)
        x += im.width + 2
        row = max(row, im.height)
    H = y + row
    page = Image.new("RGBA", (W, H))
    for n, (px, py) in pos.items():
        page.alpha_composite(ims[n], (px, py))
    page.save(path_png)
    lines = [os.path.basename(path_png), f"size:{W},{H}", "filter:Nearest,Nearest"]
    for n, (px, py) in pos.items():
        lines += [n, f"bounds:{px},{py},{ims[n].width},{ims[n].height}"]
    open(path_atlas, "w").write("\n".join(lines) + "\n")


def over(base, layer):
    b, l = np.array(base), np.array(layer)
    al = l[..., 3] > 0
    p = np.pad(al, 1)
    ring = (p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]) & ~al
    b[al] = l[al]
    b[ring] = OUTLINE + (255,)
    return Image.fromarray(b)


HTML = """<!doctype html><html><body>
<script src="node_modules/@esotericsoftware/spine-webgl/dist/iife/spine-webgl.js"></script>
<script>
(async()=>{try{
const jobs=JOBS; const out={};
for(const job of jobs){
 const canvas=document.createElement('canvas'); canvas.width=64; canvas.height=64; document.body.appendChild(canvas);
 const ctx=new spine.ManagedWebGLRenderingContext(canvas,{preserveDrawingBuffer:true,alpha:true,premultipliedAlpha:false,antialias:false});
 const am=new spine.AssetManager(ctx,'check/');
 am.loadTextureAtlas(job.atlas); am.loadJson(job.json); await am.loadAll();
 const data=new spine.SkeletonJson(new spine.AtlasAttachmentLoader(am.require(job.atlas))).readSkeletonData(am.require(job.json));
 const sk=new spine.Skeleton(data); sk.updateWorldTransform(spine.Physics.update);
 const r=new spine.SceneRenderer(canvas,ctx,false);
 r.camera.position.x=32+OFFX; r.camera.position.y=-32+OFFY; r.camera.setViewport(64,64);
 out[job.name]=[];
 for(const [a,b] of job.ranges){
  ctx.gl.clearColor(0,0,0,0); ctx.gl.clear(ctx.gl.COLOR_BUFFER_BIT);
  r.begin(); r.drawSkeleton(sk,false,a,b); r.end();
  out[job.name].push(canvas.toDataURL());
 }
}
window.result=out; document.title='ok';
}catch(e){document.title='ERR '+e.message+e.stack;}})();
</script></body></html>"""

SHOT = """const {chromium}=require('playwright');
(async()=>{const b=await chromium.launch({args:['--use-gl=angle','--use-angle=swiftshader']});const p=await b.newPage();
await p.goto('http://localhost:8765/check.html');await p.waitForFunction(()=>document.title.length>0,{timeout:30000});
const t=await p.title(); if(t!=='ok'){console.log(t);process.exit(1);}
require('fs').writeFileSync(process.argv[2], JSON.stringify(await p.evaluate(()=>window.result)));await b.close();})();"""


def main():
    os.makedirs(CHECK, exist_ok=True)
    work = os.path.join(PROBE, "check")
    os.makedirs(work, exist_ok=True)
    sizes = {p["name"]: tuple(p["size_1x"]) for p in json.load(open(os.path.join(DB.OUT, "parts.json")))["parts"]}
    poses = {"idle": R.BASE_IDLE, "high": R.HIGH, "low": R.LOW, "up": R.UP}
    jobs, refs = [], {}
    for sex, look in (("male", {"weapon": "sword"}), ("female", {"female": True, "weapon": "sword"})):
        doll = R.Doll(look)
        for pname, pose in poses.items():
            calls, frame = capture(doll, dict(pose))
            skel, ranges = build_skeleton(sex, calls, sizes)
            name = f"{sex}_{pname}"
            json.dump(skel, open(os.path.join(work, name + ".json"), "w"))
            names = sorted({list(a.keys())[0] for a in skel["skins"][0]["attachments"].values()})
            pack_atlas(names, os.path.join(work, name + ".png"), os.path.join(work, name + ".atlas"))
            jobs.append({"name": name, "json": name + ".json", "atlas": name + ".atlas", "ranges": [ranges[k] for k in sorted(ranges)]})
            refs[name] = frame
    offx, offy = (float(v) for v in os.environ.get("CAM_OFFSET", "0,0").split(","))
    open(os.path.join(PROBE, "check.html"), "w").write(HTML.replace("JOBS", json.dumps(jobs)).replace("OFFX", str(offx)).replace("OFFY", str(offy)))
    open(os.path.join(PROBE, "check_shot.js"), "w").write(SHOT)
    result_path = os.path.join(work, "result.json")
    subprocess.run(["node", "check_shot.js", result_path], cwd=PROBE, check=True)
    result = json.load(open(result_path))
    rows, report = [], {}
    for name, ref in refs.items():
        comp = Image.new("RGBA", (64, 64))
        for url in result[name]:
            layer = Image.open(__import__("io").BytesIO(base64.b64decode(url.split(",")[1]))).convert("RGBA")
            a = np.array(layer)
            a[..., 3] = np.where(a[..., 3] > 0, 255, 0)
            comp = over(comp, Image.fromarray(a))
        ra, ca = np.array(ref), np.array(comp)
        shape = int(((ra[..., 3] > 0) != (ca[..., 3] > 0)).sum())
        both = (ra[..., 3] > 0) & (ca[..., 3] > 0)
        colour = int((both & (np.abs(ra[..., :3].astype(int) - ca[..., :3].astype(int)).max(-1) > 6)).sum())
        report[name] = {"silhouette": shape, "colour": colour, "figure": int((ra[..., 3] > 0).sum())}
        rows.append((ref, comp))
    sheet = Image.new("RGBA", (64 * len(rows), 128), (38, 32, 52, 255))
    for i, (ref, comp) in enumerate(rows):
        sheet.alpha_composite(ref, (64 * i, 0))
        sheet.alpha_composite(comp, (64 * i, 64))
    sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST).save(os.path.join(CHECK, "v8_vs_spine.png"))
    json.dump(report, open(os.path.join(CHECK, "diff_pixels.json"), "w"), indent=1)
    print(report)


if __name__ == "__main__":
    main()
