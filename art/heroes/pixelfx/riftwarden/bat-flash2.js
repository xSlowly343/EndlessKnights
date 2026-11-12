import { meta as source } from "./bat-swarm.js";
import { flash } from "./_flash-kit.js";

const fx = flash("bat", 2, source);

export const meta = fx.meta;
export const create = fx.create;
