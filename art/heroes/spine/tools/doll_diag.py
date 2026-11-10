import base64
import io
import json
import os
import subprocess

import numpy as np
from PIL import Image, ImageDraw

import doll_base as DB
import doll_skeleton as DS
from doll_render import PROBE, compose, decode, link_export

OUT = os.path.join(DB.ROOT, "base", "hero", "diagnostics")
SIZE = (96, 64)
SLOT_NAMES = [s[1] for s in DS.SLOTS]


def slot_range(*names):
    idx = sorted(SLOT_NAMES.index(n) for n in names)
    ranges, start, prev = [], idx[0], idx[0]
    for i in idx[1:]:
        if i != prev + 1:
            ranges.append([start, prev])
            start = i
        prev = i
    ranges.append([start, prev])
    return ranges


GROUPS = {
    "leg_far": slot_range("thigh_far", "knee_far", "shin_far", "foot_far"),
    "arm_far": slot_range("upper_arm_far", "elbow_far", "forearm_far", "bracer_far", "hand_far"),
    "leg_near": slot_range("thigh_near", "knee_near", "shin_near", "foot_near"),
    "core": slot_range("pelvis", "torso", "head", "blush", "eye_far", "eye_near", "hair_front", "helmet"),
    "arm_near": slot_range("weapon", "upper_arm_near", "elbow_near", "forearm_near", "bracer_near", "hand_near"),
}


def view(name):
    setup = DS.SHOULDERS_SETUP
    out = {"slots": {"torso": name}}
    for i, side in enumerate(("near", "far")):
        sx = DB.SHOULDERS[name][i]
        out.setdefault("bones", {})[f"shoulder_{side}"] = {"y": -(sx - 5) - setup[side]}
    return out


def pose(*parts):
    out = {"bones": {}, "slots": {}, "ik": {}}
    for p in parts:
        for k in ("bones", "slots", "ik"):
            for name, v in p.get(k, {}).items():
                if k == "bones":
                    cur = out["bones"].setdefault(name, {})
                    for kk, vv in v.items():
                        cur[kk] = cur.get(kk, 0) + vv
                else:
                    out[k][name] = v
    return out


def b(name, **kw):
    return {"bones": {name: kw}}


POSES = {
    "setup": pose(),
    "head_up": pose(b("head", rotate=25)),
    "head_down": pose(b("head", rotate=-25)),
    "lean_forward": pose(b("torso", rotate=-35)),
    "lean_back": pose(b("torso", rotate=25)),
    "twist_side": pose(view("torso_side")),
    "twist_back": pose(view("torso_back")),
    "arm_forward": pose(b("upper_arm_near", rotate=90)),
    "arm_up": pose(b("upper_arm_near", rotate=150)),
    "arm_overhead": pose(b("upper_arm_near", rotate=185)),
    "arm_back": pose(b("upper_arm_near", rotate=-60)),
    "elbow_90": pose(b("upper_arm_near", rotate=60), b("forearm_near", rotate=90)),
    "elbow_150": pose(b("upper_arm_near", rotate=60), b("forearm_near", rotate=150)),
    "elbow_hyper": pose(b("upper_arm_near", rotate=60), b("forearm_near", rotate=-35)),
    "wrist_up": pose(b("upper_arm_near", rotate=45), b("hand_near", rotate=70)),
    "wrist_down": pose(b("upper_arm_near", rotate=45), b("hand_near", rotate=-70)),
    "far_arm_up": pose(b("upper_arm_far", rotate=160), b("forearm_far", rotate=60)),
    "far_arm_bent": pose(b("upper_arm_far", rotate=-50), b("forearm_far", rotate=140)),
    "crouch": pose(b("hip", y=-4)),
    "deep_crouch": pose(b("hip", y=-7.5), b("torso", rotate=-15)),
    "jump_tuck": pose(b("hip", y=7), b("ik_foot_near", x=3, y=8), b("ik_foot_far", x=-1, y=9)),
    "overreach": pose(b("hip", y=6)),
    "kick": pose(b("ik_foot_near", x=11, y=10), b("torso", rotate=12)),
    "high_kick": pose(b("ik_foot_near", x=8, y=16), b("torso", rotate=20), b("hip", y=-1)),
    "back_kick": pose(b("ik_foot_far", x=-12, y=8), b("torso", rotate=-25)),
    "wide_stance": pose(b("ik_foot_near", x=8), b("ik_foot_far", x=-7), b("hip", y=-3)),
    "lunge": pose(b("hip", x=6, y=-4), b("ik_foot_near", x=13), b("torso", rotate=-25), b("upper_arm_near", rotate=95)),
    "foot_tilt": pose(b("foot_near", rotate=25), b("foot_far", rotate=-20)),
    "turn_feet_still": pose(view("torso_back"), b("torso", rotate=-10), b("ik_foot_near", x=6), b("ik_foot_far", x=-5), b("hip", y=-2)),
    "combo_extreme": pose(b("upper_arm_near", rotate=170), b("forearm_near", rotate=40), b("torso", rotate=22), b("ik_foot_near", x=10, y=11),
                          b("upper_arm_far", rotate=-70), b("forearm_far", rotate=90)),
    "fk_legs_split": pose({"ik": {"leg_near_ik": 0, "leg_far_ik": 0}}, b("thigh_near", rotate=70), b("shin_near", rotate=-110),
                          b("thigh_far", rotate=-45), b("shin_far", rotate=60)),
}

PAGE = """<!doctype html><html><body>
<script src="node_modules/@esotericsoftware/spine-webgl/dist/iife/spine-webgl.js"></script>
<script>
(async()=>{try{
const cfg=CFG; const out={};
const canvas=document.createElement('canvas'); canvas.width=96*RES; canvas.height=80*RES; document.body.appendChild(canvas);
const ctx=new spine.ManagedWebGLRenderingContext(canvas,{preserveDrawingBuffer:true,alpha:true,premultipliedAlpha:false,antialias:false});
const am=new spine.AssetManager(ctx,'export/');
am.loadTextureAtlas('hero.atlas'); am.loadJson('hero.json'); await am.loadAll();
const data=new spine.SkeletonJson(new spine.AtlasAttachmentLoader(am.require('hero.atlas'))).readSkeletonData(am.require('hero.json'));
const r=new spine.SceneRenderer(canvas,ctx,false);
r.camera.position.x=18; r.camera.position.y=38; r.camera.setViewport(96,80);
const draw=(sk,ranges)=>{ctx.gl.clearColor(0,0,0,0);ctx.gl.clear(ctx.gl.COLOR_BUFFER_BIT);r.begin();for(const [a,b] of ranges)r.drawSkeleton(sk,false,a,b);r.end();return canvas.toDataURL();};
for(const job of cfg.jobs){
 const sk=new spine.Skeleton(data); const skin=new spine.Skin('combo'); for(const n of job.skins)skin.addSkin(data.findSkin(n)); sk.setSkin(skin);
 out[job.key]={};
 for(const [pname,p] of Object.entries(cfg.poses)){
  sk.setToSetupPose();
  for(const [bn,v] of Object.entries(p.bones)){const bo=sk.findBone(bn); if(v.rotate)bo.rotation+=v.rotate; if(v.x)bo.x+=v.x; if(v.y)bo.y+=v.y;}
  for(const [sn,a] of Object.entries(p.slots)){sk.findSlot(sn).setAttachment(sk.getAttachment(sk.findSlot(sn).data.index,a));}
  for(const [ik,m] of Object.entries(p.ik)){sk.findIkConstraint(ik).mix=m;}
  sk.updateWorldTransform(spine.Physics.none);
  const layers=cfg.layers.map(rg=>draw(sk,[rg]));
  const groups={}; for(const [g,rg] of Object.entries(cfg.groups))groups[g]=draw(sk,rg);
  out[job.key][pname]={layers,groups};
 }
}
window.result=out; document.title='ok';
}catch(e){document.title='ERR '+e.message+' '+e.stack;}})();
</script></body></html>"""

SHOT = DS_SHOT = """const {chromium}=require('playwright');
(async()=>{const b=await chromium.launch({args:['--use-gl=angle','--use-angle=swiftshader']});const p=await b.newPage();
await p.goto('http://localhost:8765/diag.html');await p.waitForFunction(()=>document.title.length>0,{timeout:120000});
const t=await p.title(); if(t!=='ok'){console.log(t);process.exit(1);}
require('fs').writeFileSync(process.argv[2], JSON.stringify(await p.evaluate(()=>window.result)));await b.close();})();"""


def pieces(img):
    a = np.array(img)[..., 3] > 0
    seen = np.zeros_like(a)
    sizes = []
    h, w = a.shape
    for y in range(h):
        for x in range(w):
            if a[y, x] and not seen[y, x]:
                stack, n = [(y, x)], 0
                seen[y, x] = True
                while stack:
                    cy, cx = stack.pop()
                    n += 1
                    for dy in (-1, 0, 1):
                        for dx in (-1, 0, 1):
                            ny, nx = cy + dy, cx + dx
                            if 0 <= ny < h and 0 <= nx < w and a[ny, nx] and not seen[ny, nx]:
                                seen[ny, nx] = True
                                stack.append((ny, nx))
                sizes.append(n)
    return sorted(sizes, reverse=True)


def zoom_sheet(key, entries, names, scale=2, cols=6, box=(8, 16, 72, 80)):
    box = tuple(v * DB.K for v in box)
    w, h = (box[2] - box[0]) * scale, (box[3] - box[1]) * scale + 14
    rows = (len(names) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * w, rows * h), (38, 32, 52, 255))
    d = ImageDraw.Draw(sheet)
    for i, name in enumerate(names):
        img = compose(entries[name]["layers"]).crop(box)
        x, y = (i % cols) * w, (i // cols) * h
        sheet.alpha_composite(img.resize((img.width * scale, img.height * scale), Image.NEAREST), (x, y + 14))
        d.text((x + 4, y + 2), name, fill=(210, 210, 220))
    sheet.save(os.path.join(OUT, f"zoom_{key}.png"))


def render(jobs, poses):
    link_export()
    meta = json.load(open(os.path.join(DS.EXPORT, "hero.meta.json")))
    layers = [meta["layers"][k] for k in ("back", "body", "front")]
    cfg = {"layers": layers, "groups": GROUPS, "poses": poses, "jobs": jobs}
    open(os.path.join(PROBE, "diag.html"), "w").write(PAGE.replace("CFG", json.dumps(cfg)).replace("RES", str(DB.K)))
    open(os.path.join(PROBE, "diag_shot.js"), "w").write(SHOT)
    res_path = os.path.join(PROBE, "check", "diag_result.json")
    os.makedirs(os.path.dirname(res_path), exist_ok=True)
    subprocess.run(["node", "diag_shot.js", res_path], cwd=PROBE, check=True)
    return json.load(open(res_path)), meta


def faults_of(entry):
    faults = {}
    for g, url in entry["groups"].items():
        p = pieces(decode(url))
        if len(p) > 1:
            faults[g] = p[1:]
    return faults


def main():
    os.makedirs(OUT, exist_ok=True)
    meta = json.load(open(os.path.join(DS.EXPORT, "hero.meta.json")))
    jobs = [{"key": sex, "skins": meta["presets"][sex]} for sex in ("male", "female")]
    for weapon in ("axe", "staff"):
        jobs.append({"key": f"male_{weapon}", "skins": meta["presets"]["male"][:-1] + [f"weapon/{weapon}"]})
    res, meta = render(jobs, POSES)
    report = {}
    for job in jobs:
        key = job["key"]
        names = list(POSES)
        cols = 8
        rows = (len(names) + cols - 1) // cols
        cell = (SIZE[0] * 2, SIZE[1] * 2 + 12)
        sheet = Image.new("RGBA", (cols * cell[0], rows * cell[1]), (38, 32, 52, 255))
        d = ImageDraw.Draw(sheet)
        for i, name in enumerate(names):
            entry = res[key][name]
            img = compose(entry["layers"])
            faults = faults_of(entry)
            if faults:
                report.setdefault(key, {})[name] = faults
            x, y = (i % cols) * cell[0], (i // cols) * cell[1]
            sheet.alpha_composite(img.crop((0, 16 * DB.K, 96 * DB.K, 80 * DB.K)).resize((SIZE[0] * 2, SIZE[1] * 2), Image.NEAREST), (x, y + 12))
            d.text((x + 3, y + 1), name, fill=(255, 120, 120) if faults else (210, 210, 220))
        sheet.save(os.path.join(OUT, f"poses_{key}.png"))
        zoom_sheet(key, res[key], names)
    json.dump(report, open(os.path.join(OUT, "defects.json"), "w"), indent=1)
    print(json.dumps(report, indent=1) if report else "no defects")


if __name__ == "__main__":
    main()
