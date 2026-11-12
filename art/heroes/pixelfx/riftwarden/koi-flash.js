import { meta as source } from "./koi-splash.js";
import { flash } from "./_flash-kit.js";

const fx = flash("koi", 1, source);

export const meta = fx.meta;
export const create = fx.create;
