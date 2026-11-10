import { readFileSync, writeFileSync } from "node:fs";
import { createFrame, clearFrame, createFx, composite } from "./pixelfx/src/core/index.js";

const job = JSON.parse(readFileSync(process.argv[2], "utf8"));
const mod = await import("./pixelfx/src/fx/" + job.id + ".js");
const [W, H] = job.size, [OX, OY] = job.origin;
const n = job.frames.length, plane = W * H * 4;
const back = Buffer.alloc(plane * n), front = Buffer.alloc(plane * n);
const f = createFrame(W, H);

job.frames.forEach((fr, i) => {
  const mask = { w: fr.w, h: fr.h, data: Uint8Array.from(fr.data) };
  const actor = { mask, x: fr.x, y: fr.y, w: fr.w, h: fr.h, k: 2, head: [0, fr.y], chest: [0, fr.y + fr.h * 0.3] };
  const inst = createFx(mod, { x: OX, y: OY, ...(job.options || {}), actor });
  const steps = Math.round(fr.t * 30);
  for (let s = 0; s < steps; s++) inst.update(1 / 30);
  clearFrame(f);
  inst.draw(f, "back");
  back.set(composite(f, new Uint8ClampedArray(plane), null), i * plane);
  clearFrame(f);
  inst.draw(f, "front");
  front.set(composite(f, new Uint8ClampedArray(plane), null), i * plane);
});

writeFileSync(job.out + ".back.bin", back);
writeFileSync(job.out + ".front.bin", front);
