import base64
import io
import json
import os
import subprocess
import sys

import numpy as np
from PIL import Image

import doll_base as DB
from doll_spine_check import over
import doll_skeleton as DS

sys.argv = sys.argv[:1] + [DB.DOLL_SRC]
import rig3 as R
import combo4 as C4

PROBE = os.path.join(DB.ROOT, "probe")
PREVIEW = os.path.join(DB.ROOT, "base", "hero", "preview")
SIZE = (96, 64)
LOOKS = {"male": {"weapon": "sword"}, "female": dict(DB.LOOKS["b"])}

PAGE = """<!doctype html><html><body>
<script src="node_modules/@esotericsoftware/spine-webgl/dist/iife/spine-webgl.js"></script>
<script>
(async()=>{try{
const cfg=CFG; const out={};
const canvas=document.createElement('canvas'); canvas.width=96*RES; canvas.height=64*RES; document.body.appendChild(canvas);
const ctx=new spine.ManagedWebGLRenderingContext(canvas,{preserveDrawingBuffer:true,alpha:true,premultipliedAlpha:false,antialias:false});
const am=new spine.AssetManager(ctx,'export/');
am.loadTextureAtlas('hero.atlas'); am.loadJson('hero.json'); await am.loadAll();
const data=new spine.SkeletonJson(new spine.AtlasAttachmentLoader(am.require('hero.atlas'))).readSkeletonData(am.require('hero.json'));
const r=new spine.SceneRenderer(canvas,ctx,false);
r.camera.position.x=18; r.camera.position.y=30; r.camera.setViewport(96,64);
const grab=(sk)=>{const res=[];for(const [a,b] of cfg.layers){ctx.gl.clearColor(0,0,0,0);ctx.gl.clear(ctx.gl.COLOR_BUFFER_BIT);r.begin();r.drawSkeleton(sk,false,a,b);r.end();res.push(canvas.toDataURL());}return res;};
const make=(names)=>{const sk=new spine.Skeleton(data);const skin=new spine.Skin('combo');for(const n of names)skin.addSkin(data.findSkin(n));sk.setSkin(skin);sk.setSlotsToSetupPose();return sk;};
for(const job of cfg.compare){
 const sk=make(job.skins); const anim=data.findAnimation(job.anim); const frames=[];
 for(const t of job.times){sk.setToSetupPose();anim.apply(sk,0,t,false,[],1,spine.MixBlend.setup,spine.MixDirection.mixIn);sk.updateWorldTransform(spine.Physics.none);frames.push(grab(sk));}
 out[job.key]=frames;
}
for(const job of cfg.play){
 const sk=make(job.skins); const sd=new spine.AnimationStateData(data); sd.defaultMix=0.04; const st=new spine.AnimationState(sd);
 let first=true; for(const a of job.queue){ if(first){st.setAnimation(0,a,false);first=false;} else st.addAnimation(0,a,false,0); }
 const frames=[]; const dt=1/60; sk.updateWorldTransform(spine.Physics.reset);
 for(let i=0;i<job.steps;i++){st.update(dt);st.apply(sk);sk.update(dt);sk.updateWorldTransform(spine.Physics.update); if(i%2==0)frames.push(grab(sk));}
 out[job.key]=frames;
}
window.result=out; document.title='ok';
}catch(e){document.title='ERR '+e.message+' '+e.stack;}})();
</script></body></html>"""

SHOT = """const {chromium}=require('playwright');
(async()=>{const b=await chromium.launch({args:['--use-gl=angle','--use-angle=swiftshader']});const p=await b.newPage();
await p.goto('http://localhost:8765/hero.html');await p.waitForFunction(()=>document.title.length>0,{timeout:120000});
const t=await p.title(); if(t!=='ok'){console.log(t);process.exit(1);}
require('fs').writeFileSync(process.argv[2], JSON.stringify(await p.evaluate(()=>window.result)));await b.close();})();"""


def decode(url):
    a = np.array(Image.open(io.BytesIO(base64.b64decode(url.split(",")[1]))).convert("RGBA"))
    a[..., 3] = np.where(a[..., 3] > 0, 255, 0)
    return Image.fromarray(a)


def compose(layers):
    c = Image.new("RGBA", decode(layers[0]).size)
    for url in layers:
        c = over(c, decode(url))
    return c


def link_export():
    link = os.path.join(PROBE, "export")
    if not os.path.exists(link):
        os.symlink(DS.EXPORT, link)
    parts = os.path.join(PROBE, "parts_tex")
    if not os.path.exists(parts):
        os.symlink(os.path.join(DB.OUT, "parts_tex"), parts)


def main():
    os.makedirs(PREVIEW, exist_ok=True)
    link_export()
    meta = json.load(open(os.path.join(DS.EXPORT, "hero.meta.json")))
    layers = [meta["layers"][k] for k in ("back", "body", "front")]
    compare, play, refs = [], [], {}
    for sex, look in LOOKS.items():
        doll = R.Doll(look)
        for anim, (frames, loop) in DS.v8_sequences(doll).items():
            times, t = [], 0.0
            for P, ms in frames:
                times.append(round(t, 4))
                t += ms / 1000
            key = f"{sex}_{anim}"
            compare.append({"key": key, "skins": meta["presets"][sex], "anim": anim, "times": times})
            refs[key] = [doll.frame(dict(P), size=SIZE).resize((SIZE[0] * DB.K, SIZE[1] * DB.K), Image.NEAREST) for P, _ in frames]
        play.append({"key": f"{sex}_play", "skins": meta["presets"][sex], "queue": ["Idle", "Idle", "Enter", "Hit1", "Hit2", "Hit3", "Exit3", "Idle"], "steps": 200})
    cfg = {"layers": layers, "compare": compare, "play": play}
    open(os.path.join(PROBE, "hero.html"), "w").write(PAGE.replace("CFG", json.dumps(cfg)).replace("RES", str(DB.K)))
    open(os.path.join(PROBE, "hero_shot.js"), "w").write(SHOT)
    res_path = os.path.join(PROBE, "check", "hero_result.json")
    os.makedirs(os.path.dirname(res_path), exist_ok=True)
    subprocess.run(["node", "hero_shot.js", res_path], cwd=PROBE, check=True)
    res = json.load(open(res_path))
    report = {}
    for sex in LOOKS:
        rows = []
        for job in compare:
            if not job["key"].startswith(sex):
                continue
            mine = [compose(f) for f in res[job["key"]]]
            ref = refs[job["key"]]
            sil = [int(((np.array(a)[..., 3] > 0) != (np.array(b)[..., 3] > 0)).sum()) for a, b in zip(ref, mine)]
            fig = [int((np.array(a)[..., 3] > 0).sum()) for a in ref]
            report[job["key"]] = round(100 * sum(sil) / max(1, sum(fig)), 1)
            rows.append((ref, mine))
        width = max(len(r[0]) for r in rows)
        cw, chh = SIZE[0] * DB.K, SIZE[1] * DB.K
        sheet = Image.new("RGBA", (cw * width, chh * 2 * len(rows)), (38, 32, 52, 255))
        for ri, (ref, mine) in enumerate(rows):
            for fi, (a, b) in enumerate(zip(ref, mine)):
                sheet.alpha_composite(a, (fi * cw, ri * 2 * chh))
                sheet.alpha_composite(b, (fi * cw, (ri * 2 + 1) * chh))
        sheet.save(os.path.join(PREVIEW, f"v8_vs_spine_{sex}.png"))
        frames = [compose(f) for f in res[f"{sex}_play"]]
        gif = []
        for f in frames:
            bg = Image.new("RGBA", f.size, (38, 32, 52, 255))
            bg.alpha_composite(f)
            gif.append(bg.resize((f.width * 2, f.height * 2), Image.NEAREST).convert("P", palette=Image.ADAPTIVE))
        gif[0].save(os.path.join(PREVIEW, f"spine_play_{sex}.gif"), save_all=True, append_images=gif[1:], duration=33, loop=0, disposal=2)
    json.dump(report, open(os.path.join(PREVIEW, "silhouette_diff_percent.json"), "w"), indent=1)
    print(report)


if __name__ == "__main__":
    main()
