#!/usr/bin/env node
// Рендер векторных значков мода: art/{ui,map,monsters}/**/*.svg → mod/SlayTheRequiem/images/{ui,map,monsters}/**/*.png.
// Исходники — SVG в art/ui (вне Godot-проекта, чтобы движок их не импортировал). Размер PNG = размер SVG.
//
//   npm i --prefix /tmp/r @resvg/resvg-js@2
//   NODE_PATH=/tmp/r/node_modules node tools/art/render_ui_icons.mjs
//
// Значки рисуются в стиле art-ui-animation-spec.md §1: толстый тёмный контур, 2–3 тона, палитра §1.1.
import { readFileSync, writeFileSync, mkdirSync, readdirSync, statSync } from "node:fs";
import { join, relative, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const { Resvg } = require("@resvg/resvg-js");

const root = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const groups = ["ui", "map", "monsters"];

function* walk(dir) {
  for (const name of readdirSync(dir).sort()) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) yield* walk(path);
    else if (name.endsWith(".svg")) yield path;
  }
}

let count = 0;
for (const group of groups) {
  const src = join(root, "art", group);
  const out = join(root, "mod", "SlayTheRequiem", "images", group);
  for (const file of walk(src)) {
    const rel = relative(src, file).replace(/\.svg$/, ".png");
    const png = new Resvg(readFileSync(file), { fitTo: { mode: "original" } }).render().asPng();
    mkdirSync(dirname(join(out, rel)), { recursive: true });
    writeFileSync(join(out, rel), png);
    console.log(`${relative(root, file)} → images/${group}/${rel}`);
    count++;
  }
}
console.log(`${count} значков`);
