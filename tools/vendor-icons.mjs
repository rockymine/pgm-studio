/**
 * Regenerates wwwroot/js/studio/vendor/lucide-icons.js — the icon set the app actually uses, inlined.
 *
 *   node tools/vendor-icons.mjs [--check]
 *
 * The full lucide UMD bundle is ~414 KB for ~1,750 icons; the studio names about sixty of them. This
 * emits just those, plus the ~40 lines of lucide's own render path (createElement / replaceElement /
 * toPascalCase / mergeClasses), as one dependency-free classic script that sets window.lucide.
 *
 * Icon names are collected by taking every kebab-case string literal in the client and asking lucide
 * whether it is an icon — the same toPascalCase lookup the runtime does. That catches the names built
 * in C# (RegionNode.Icon(type), PhaseIcon, c.Icon) which a scan of data-lucide="..." attributes alone
 * would miss. Over-collecting is harmless (a stray "map" or "circle" costs bytes); under-collecting is
 * caught at runtime, because the emitted createIcons reports an unknown name as a console error and the
 * e2e smoke sweep fails any page that logs one.
 *
 * The package is fetched with `npm pack` (download only — no install, no lifecycle scripts, no
 * node_modules) and pinned below. Bump VERSION deliberately: lucide renames icons between releases,
 * which is what made the old cdn.jsdelivr.net "@latest" tag unsafe as well as unreachable.
 *
 * --check re-runs the generation and exits non-zero if the committed file is stale.
 */

import { execFileSync } from "node:child_process";
import { createRequire } from "node:module";
import { mkdtempSync, readFileSync, writeFileSync, readdirSync, statSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const VERSION = "1.27.0";
const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const CLIENT = join(ROOT, "src/PgmStudio.Client");
const OUT = join(CLIENT, "wwwroot/js/studio/vendor/lucide-icons.js");
const SCAN_EXTENSIONS = [".razor", ".cs", ".js", ".html"];

/** Every kebab-case-looking string literal in the client, from markup attributes and C# alike. */
function collectCandidates(dir, into = new Set()) {
  for (const entry of readdirSync(dir)) {
    if (entry === "bin" || entry === "obj" || entry === "vendor") continue;
    const path = join(dir, entry);
    if (statSync(path).isDirectory()) {
      collectCandidates(path, into);
      continue;
    }
    if (!SCAN_EXTENSIONS.some((ext) => entry.endsWith(ext))) continue;
    const source = readFileSync(path, "utf8");
    for (const [, name] of source.matchAll(/["']([a-z][a-z0-9]*(?:-[a-z0-9]+)*)["']/g)) into.add(name);
  }
  return into;
}

/** lucide's own name resolution: "check-circle-2" -> "CheckCircle2". */
const toPascalCase = (s) =>
  s.replace(/^([A-Z])|[\s-_]+(\w)/g, (_, first, rest) => (rest ? rest.toUpperCase() : first.toLowerCase()))
    .replace(/^./, (c) => c.toUpperCase());

/** Download + unpack the pinned release. `npm pack` fetches the tarball only — it never installs. */
function fetchLucide() {
  const dir = mkdtempSync(join(tmpdir(), "lucide-vendor-"));
  execFileSync("npm", ["pack", `lucide@${VERSION}`, "--ignore-scripts", "--silent"], {
    cwd: dir, stdio: ["ignore", "ignore", "inherit"],
  });
  execFileSync("tar", ["xzf", `lucide-${VERSION}.tgz`], { cwd: dir });
  return join(dir, "package");
}

const pkg = fetchLucide();
const { default: icons } = await import(pathToFileURL(join(pkg, "dist/esm/iconsAndAliases.mjs")).href)
  .then((m) => ({ default: m.default ?? m }));
const license = readFileSync(join(pkg, "LICENSE"), "utf8").trim();

const used = [...collectCandidates(CLIENT)]
  .map((name) => [name, icons[toPascalCase(name)]])
  .filter(([, node]) => Array.isArray(node))
  .sort(([a], [b]) => a.localeCompare(b));

if (used.length === 0) throw new Error("collected no icon names — the scan or the lookup is broken");

const body = used.map(([name, node]) => `  ${JSON.stringify(name)}: ${JSON.stringify(node)},`).join("\n");

const generated = `// lucide v${VERSION} (ISC) — VENDORED subset: the ${used.length} icons this app names, plus lucide's
// render path. Replaces the cdn.jsdelivr.net "@latest" script tag, which pinned nothing and is
// unreachable from any egress-restricted environment (no icon rendered at all there).
//
// GENERATED — do not edit. Regenerate: node tools/vendor-icons.mjs   (verify: --check)
//
// ${license.split("\n").filter(Boolean).slice(0, 1).join(" ")}
// Full licence text: https://github.com/lucide-icons/lucide/blob/main/LICENSE
(function () {
  "use strict";

  var icons = {
${body}
  };

  var defaults = {
    xmlns: "http://www.w3.org/2000/svg", width: 24, height: 24, viewBox: "0 0 24 24",
    fill: "none", stroke: "currentColor", "stroke-width": 2,
    "stroke-linecap": "round", "stroke-linejoin": "round",
  };

  function build(tag, attrs, children) {
    var el = document.createElementNS("http://www.w3.org/2000/svg", tag);
    Object.keys(attrs).forEach(function (k) { el.setAttribute(k, String(attrs[k])); });
    (children || []).forEach(function (child) { el.appendChild(build(child[0], child[1], child[2])); });
    return el;
  }

  // Mirrors lucide's replaceElement: source attributes win over the caller's, the element keeps its own
  // classes alongside "lucide lucide-<name>", and anything already carrying an a11y prop is left alone.
  function replace(element, attrs) {
    var name = element.getAttribute("data-lucide");
    if (name == null) return;
    var node = icons[name];
    if (!node) {
      // Loud on purpose: an unvendored name renders nothing at all, and a silent blank is exactly the
      // failure the CDN tag used to produce. The e2e smoke sweep fails any page that logs an error.
      console.error("lucide: icon \\"" + name + "\\" is not in the vendored subset — re-run tools/vendor-icons.mjs");
      return;
    }
    var own = {};
    Array.prototype.forEach.call(element.attributes, function (a) { own[a.name] = a.value; });
    var a11y = Object.keys(own).some(function (k) {
      return k.indexOf("aria-") === 0 || k === "role" || k === "title";
    });

    var merged = Object.assign({}, defaults, { "data-lucide": name });
    if (!a11y) merged["aria-hidden"] = "true";
    Object.assign(merged, attrs || {}, own);

    var classes = ["lucide", "lucide-" + name]
      .concat(String(own.class || "").split(" "), String((attrs && attrs.class) || "").split(" "))
      .filter(function (c, i, all) { return c && c.trim() !== "" && all.indexOf(c) === i; });
    if (classes.length) merged.class = classes.join(" ");

    var svg = build("svg", merged, node);
    if (element.parentNode) element.parentNode.replaceChild(svg, element);
  }

  window.lucide = {
    icons: icons,
    createIcons: function (options) {
      var opts = options || {};
      var root = opts.root || document;
      Array.prototype.forEach.call(root.querySelectorAll("[data-lucide]"), function (el) {
        replace(el, opts.attrs);
      });
    },
  };
})();
`;

if (process.argv.includes("--check")) {
  const current = readFileSync(OUT, "utf8");
  if (current !== generated) {
    console.error(`stale: ${OUT} does not match tools/vendor-icons.mjs output — re-run it`);
    process.exit(1);
  }
  console.log(`up to date: ${used.length} icons`);
} else {
  writeFileSync(OUT, generated);
  console.log(`wrote ${OUT} — ${used.length} icons, ${(generated.length / 1024).toFixed(1)} KB`);
}
