import { meta as source } from "./bone-grasp.js";
import { flash } from "./_flash-kit.js";

const fx = flash("bone", 1, source);

export const meta = fx.meta;
export const create = fx.create;
