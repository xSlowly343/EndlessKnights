import base64
import io
import json
import os
import subprocess
import sys
import time

import numpy as np
from PIL import Image

import draw
import rig

PROBE = os.path.normpath(os.path.join(rig.HERE, "..", "..", "probe"))
PREVIEW = os.path.join(rig.OUT, "preview")
VIEW = (240, 150)
CAMERA = (22, 60)
BG = (46, 40, 66)
GROUND = (74, 66, 96)
PORT = 8766

PAGE = """<!doctype html><html><body>
<script src="node_modules/@esotericsoftware/spine-webgl/dist/iife/spine-webgl.js"></script>
<script>
(async()=>{try{
const cfg=CFG; const out={};
const canvas=document.createElement('canvas'); canvas.width=cfg.view[0]; canvas.height=cfg.view[1]; document.body.appendChild(canvas);
const ctx=new spine.ManagedWebGLRenderingContext(canvas,{preserveDrawingBuffer:true,alpha:true,premultipliedAlpha:false,antialias:false});
const am=new spine.AssetManager(ctx,'hero2_export/');
am.loadTextureAtlas('hero2.atlas'); am.loadJson('hero2.json'); await am.loadAll();
const data=new spine.SkeletonJson(new spine.AtlasAttachmentLoader(am.require('hero2.atlas'))).readSkeletonData(am.require('hero2.json'));
const r=new spine.SceneRenderer(canvas,ctx,false);
r.camera.position.x=cfg.camera[0]; r.camera.position.y=cfg.camera[1]; r.camera.setViewport(cfg.view[0],cfg.view[1]);
const grab=(sk)=>{const res=[];for(const [a,b] of cfg.layers){ctx.gl.clearColor(0,0,0,0);ctx.gl.clear(ctx.gl.COLOR_BUFFER_BIT);r.begin();r.drawSkeleton(sk,false,a,b);r.end();res.push(canvas.toDataURL());}return res;};
const make=()=>{const sk=new spine.Skeleton(data);const skin=new spine.Skin('combo');for(const n of cfg.skins)skin.addSkin(data.findSkin(n));sk.setSkin(skin);sk.setSlotsToSetupPose();return sk;};
for(const job of cfg.jobs){
 const sk=make(); const sd=new spine.AnimationStateData(data); sd.defaultMix=job.mix; const st=new spine.AnimationState(sd);
 let first=true; for(const [a,loop,delay] of job.queue){ if(first){st.setAnimation(0,a,loop);first=false;} else st.addAnimation(0,a,loop,delay); }
 const frames=[]; const dt=1/60; st.apply(sk); sk.updateWorldTransform(spine.Physics.reset);
 for(let i=0;i<job.warm;i++){st.update(dt);st.apply(sk);sk.update(dt);sk.updateWorldTransform(spine.Physics.update);}
 for(let i=0;i<job.steps;i++){st.update(dt);st.apply(sk);sk.update(dt);sk.updateWorldTransform(spine.Physics.update); if(i%job.every==0)frames.push(grab(sk));}
 out[job.key]=frames;
}
out.info={bones:data.bones.length,slots:data.slots.length,ik:data.ikConstraints.length,physics:data.physicsConstraints.length,animations:data.animations.map(a=>a.name)};
window.result=out; document.title='ok';
}catch(e){document.title='ERR '+e.message+' '+e.stack;}})();
</script></body></html>"""

SHOT = """const {chromium}=require('playwright');
(async()=>{const b=await chromium.launch({args:['--use-gl=angle','--use-angle=swiftshader']});const p=await b.newPage();
await p.goto('http://localhost:PORT/hero2.html');await p.waitForFunction(()=>document.title.length>0,{timeout:240000});
const t=await p.title(); if(t!=='ok'){console.log(t);process.exit(1);}
require('fs').writeFileSync(process.argv[2], JSON.stringify(await p.evaluate(()=>window.result)));await b.close();})();"""


def decode(url):
    return np.array(Image.open(io.BytesIO(base64.b64decode(url.split(",")[1]))).convert("RGBA"))


def compose(layers, scale=1):
    under, body, over = (decode(u) for u in layers)
    h, w = body.shape[:2]
    canvas = np.zeros((h, w, 3), np.float32)
    canvas[:] = BG
    ground = h - (h // 2 - CAMERA[1])
    canvas[min(h, ground):] = GROUND
    shade = under[..., 3:4] / 255 * 0.45
    canvas = canvas * (1 - shade) + under[..., :3] * shade
    solid = body[..., 3] > 110
    pad = np.pad(solid, 1)
    edge = (pad[:-2, 1:-1] | pad[2:, 1:-1] | pad[1:-1, :-2] | pad[1:-1, 2:]) & ~solid
    canvas[edge] = draw.OUTLINE
    canvas[solid] = body[..., :3][solid]
    glow = over[..., 3] > 110
    canvas[glow] = over[..., :3][glow]
    im = Image.fromarray(canvas.astype(np.uint8), "RGB")
    return im.resize((w * scale, h * scale), Image.NEAREST) if scale > 1 else im


def link():
    target = os.path.join(PROBE, "hero2_export")
    if not os.path.islink(target):
        os.symlink(rig.EXPORT, target)


def run(jobs):
    link()
    meta = json.load(open(os.path.join(rig.EXPORT, "hero2.meta.json")))
    cfg = {"view": VIEW, "camera": CAMERA, "skins": meta["preset"], "layers": [meta["layers"][k] for k in ("under", "body", "over")], "jobs": jobs}
    open(os.path.join(PROBE, "hero2.html"), "w").write(PAGE.replace("CFG", json.dumps(cfg)))
    open(os.path.join(PROBE, "hero2_shot.js"), "w").write(SHOT.replace("PORT", str(PORT)))
    server = subprocess.Popen([sys.executable, "-m", "http.server", str(PORT), "--directory", PROBE], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    time.sleep(1)
    out = os.path.join(PROBE, "hero2_result.json")
    try:
        subprocess.run(["node", "hero2_shot.js", out], cwd=PROBE, check=True)
    finally:
        server.terminate()
    res = json.load(open(out))
    for name in ("hero2.html", "hero2_shot.js", "hero2_result.json"):
        os.remove(os.path.join(PROBE, name))
    os.remove(os.path.join(PROBE, "hero2_export"))
    return res


def job(key, queue, steps, every=2, warm=0, mix=0.05):
    return {"key": key, "queue": queue, "steps": steps, "every": every, "warm": warm, "mix": mix}


def gif(frames, path, ms, scale=3):
    ims = [compose(f, scale).convert("P", palette=Image.ADAPTIVE, colors=64) for f in frames]
    ims[0].save(path, save_all=True, append_images=ims[1:], duration=ms, loop=0)


def sheet(frames, path, cols, crop=None, scale=2):
    ims = [compose(f) for f in frames]
    if crop:
        ims = [im.crop(crop) for im in ims]
    w, h = ims[0].size
    rows = (len(ims) + cols - 1) // cols
    out = Image.new("RGB", (cols * w, rows * h), BG)
    for i, im in enumerate(ims):
        out.paste(im, ((i % cols) * w, (i // cols) * h))
    out.resize((out.width * scale, out.height * scale), Image.NEAREST).save(path)


def main():
    rig.build()
    os.makedirs(PREVIEW, exist_ok=True)
    cat = json.load(open(os.path.join(rig.EXPORT, "hero2.meta.json")))["animations"]
    combo = [["Idle", True, 0], ["Hit1", False, 1.2], ["Hit2", False, 0.36], ["Hit3", False, 0.36], ["Flourish", False, 0], ["Idle", True, 0]]
    jobs = [job("combo", combo, 290, warm=60)]
    for name, info in cat.items():
        steps = int(round(info["duration"] * 60)) + (0 if info["loop"] else 12)
        queue = [[name, True, 0]] if info["loop"] else [["Idle", True, 0], [name, False, 0.1], ["Idle", True, 0]]
        jobs.append(job(name, queue, steps + (0 if info["loop"] else 6), warm=120 if info["loop"] else 3, mix=0.0 if info["loop"] else 0.05))
    res = run(jobs)
    print(res.pop("info"))
    gif(res["combo"], os.path.join(PREVIEW, "combo.gif"), 30)
    for name in cat:
        gif(res[name], os.path.join(PREVIEW, f"{name.lower()}.gif"), 30)
        sheet(res[name], os.path.join(PREVIEW, f"sheet_{name.lower()}.png"), 8, crop=(40, 0, 240, 135))
    Image.Image.save(compose(res["Idle"][0], 4), os.path.join(PREVIEW, "idle_x4.png"))


if __name__ == "__main__":
    main()
