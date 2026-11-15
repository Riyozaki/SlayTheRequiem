#!/usr/bin/env node
// Рендер произвольных SVG в PNG (resvg): node tools/art/render_svg.mjs in.svg out.png [in2.svg out2.png ...]
// Для сцен (tools/art/scenes/build_scenes.py превращает PNG в JPG). Размер PNG = размер SVG.
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname } from "node:path";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const { Resvg } = require("@resvg/resvg-js");
const args = process.argv.slice(2);
for (let i = 0; i + 1 < args.length; i += 2) {
  const png = new Resvg(readFileSync(args[i]), { fitTo: { mode: "original" } }).render().asPng();
  mkdirSync(dirname(args[i + 1]), { recursive: true });
  writeFileSync(args[i + 1], png);
}
console.log(`SVG → PNG: ${Math.floor(args.length / 2)}`);
