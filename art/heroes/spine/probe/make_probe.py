import json
from PIL import Image, ImageDraw
im = Image.new("RGBA", (32, 80), (0,0,0,0)); d = ImageDraw.Draw(im)
for y in range(80):
    c = (200,120,80,255) if (y//8)%2==0 else (120,70,50,255)
    d.line([(0,y),(15,y)], fill=c)
d.rectangle([20,0,27,20], fill=(240,220,90,255))
im.save("probe.png")
open("probe.atlas","w").write("probe.png\nsize:32,80\nfilter:Nearest,Nearest\nleg\nbounds:0,0,16,80\nhair\nbounds:20,0,8,21\n")
left=[(-8,y) for y in range(100,19,-10)]; right=[(8,y) for y in range(20,101,10)]
pts=left+right; n=len(pts)
def w(y):
    if y>=70: return 1.0
    if y<=50: return 0.0
    return (y-50)/20
verts=[]; uvs=[]
for x,y in pts:
    a=w(y); uvs += [(x+8)/16, (100-y)/80]
    infl=[]
    if a>0: infl.append((2, -(y-100), x, a))
    if a<1: infl.append((3, -(y-60), x, 1-a))
    verts.append(len(infl))
    for b,lx,ly,ww in infl: verts += [b,lx,ly,round(ww,4)]
idx={p:i for i,p in enumerate(pts)}
tris=[]
for y in range(100,20,-10):
    a,b,c,dd=idx[(-8,y)],idx[(8,y)],idx[(-8,y-10)],idx[(8,y-10)]
    tris += [a,c,b, b,c,dd]
sk={"skeleton":{"spine":"4.3.00","width":100,"height":120},
 "bones":[{"name":"root"},{"name":"hip","parent":"root","y":100},
  {"name":"thigh","parent":"hip","rotation":-90,"length":40},
  {"name":"shin","parent":"thigh","x":40,"length":40},
  {"name":"foot_ik","parent":"root","y":22},
  {"name":"hair","parent":"hip","x":-10,"rotation":-90,"length":21}],
 "slots":[{"name":"hair","bone":"hair","attachment":"hair"},{"name":"leg","bone":"thigh","attachment":"leg"}],
 "constraints":[{"type":"ik","name":"leg_ik","bones":["thigh","shin"],"target":"foot_ik","bendPositive":False},
  {"type":"physics","name":"hair_phys","bone":"hair","rotate":1,"inertia":0.6,"strength":60,"damping":0.8,"gravity":0}],
 "skins":[{"name":"default","attachments":{
   "leg":{"leg":{"type":"mesh","uvs":uvs,"triangles":tris,"vertices":verts,"hull":n,"width":16,"height":80}},
   "hair":{"hair":{"x":10,"rotation":90,"width":8,"height":21}}}}],
 "animations":{"kick":{"bones":{
   "foot_ik":{"translate":[{"time":0,"x":0,"y":0},{"time":0.5,"x":45,"y":45},{"time":1,"x":0,"y":0}]},
   "root":{"translate":[{"time":0,"x":0},{"time":0.5,"x":40},{"time":1,"x":0}]}}}}}
json.dump(sk, open("probe.json","w"))
