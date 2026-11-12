import { meta as source } from "./wolf-bite.js";
import { flash } from "./_flash-kit.js";

const fx = flash("wolf", 1, source);

export const meta = fx.meta;
export const create = fx.create;
