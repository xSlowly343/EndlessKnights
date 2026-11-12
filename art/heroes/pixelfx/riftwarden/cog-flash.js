import { meta as source } from "./cog-break.js";
import { flash } from "./_flash-kit.js";

const fx = flash("cog", 1, source);

export const meta = fx.meta;
export const create = fx.create;
