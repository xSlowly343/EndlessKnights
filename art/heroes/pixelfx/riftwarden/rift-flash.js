import { meta as source } from "./rift-claw.js";
import { flash } from "./_flash-kit.js";

const fx = flash("rift", 1, source);

export const meta = fx.meta;
export const create = fx.create;
