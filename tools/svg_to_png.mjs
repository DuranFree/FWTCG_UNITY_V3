/**
 * svg_to_png.mjs — SVG → PNG 批量转换（Node.js + sharp）
 * 用法：node tools/svg_to_png.mjs [--dry]
 */

import { createRequire } from "module";
import path from "path";
import fs from "fs";
import { fileURLToPath } from "url";

const require = createRequire(import.meta.url);
const sharp = require("sharp");

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");

const SVG_DIR       = path.join(ROOT, "Assets", "Art", "SVG");
const OUT_GENERATED = path.join(ROOT, "Assets", "Resources", "UI", "Generated");
const OUT_UI        = path.join(ROOT, "Assets", "Resources", "UI");

// frame_gold/silver 不再覆盖 Assets/Resources/UI/（原始透明帧文件保留）
const OVERRIDE_FILES = new Set([]);
const BG_MAX_WIDTH   = 2048;
const SCALE_4X       = 4;

const dry = process.argv.includes("--dry");

// 从 SVG 读取 width/height
function getSvgSize(svgPath) {
  const head = fs.readFileSync(svgPath, "utf8").slice(0, 1024);
  const m = head.match(/<svg[^>]*\swidth="([0-9.]+)"[^>]*\sheight="([0-9.]+)"/);
  if (m) return { w: parseFloat(m[1]), h: parseFloat(m[2]) };
  return { w: null, h: null };
}

async function convert(svgPath, outPath, scale) {
  const { w, h } = getSvgSize(svgPath);
  const outW = w ? Math.round(w * scale) : undefined;
  const outH = h ? Math.round(h * scale) : undefined;

  const label = `${path.basename(svgPath)} → ${path.relative(ROOT, outPath)}  (${outW ?? "auto"}×${outH ?? "auto"})`;

  if (dry) {
    console.log(`  [dry] ${label}`);
    return;
  }

  fs.mkdirSync(path.dirname(outPath), { recursive: true });
  const svgContent = fs.readFileSync(svgPath);
  let img = sharp(svgContent, { density: 192 });
  if (outW && outH) img = img.resize(outW, outH);
  await img.png({ compressionLevel: 9 }).toFile(outPath);
  console.log(`  ✅ ${label}`);
}

async function main() {
  if (!fs.existsSync(SVG_DIR)) {
    console.error(`❌ SVG 目录不存在：${SVG_DIR}`);
    process.exit(1);
  }

  const svgs = fs.readdirSync(SVG_DIR).filter(f => f.endsWith(".svg")).sort();
  if (!svgs.length) { console.log("⚠️  SVG 目录为空"); return; }

  console.log(`${dry ? "[DRY RUN] " : ""}开始转换 ${svgs.length} 个 SVG → PNG\n`);

  let ok = 0;
  const errors = [];

  for (const fname of svgs) {
    const stem    = path.basename(fname, ".svg");
    const svgPath = path.join(SVG_DIR, fname);
    const outName = stem + ".png";
    const outPath = path.join(OUT_GENERATED, outName);

    // 背景类：原尺寸（最大 2048），其余 4x
    const isBg = stem.startsWith("bg_");
    let scale = SCALE_4X;
    if (isBg) {
      const { w } = getSvgSize(svgPath);
      scale = w ? Math.min(1, BG_MAX_WIDTH / w) : 1;
    }

    try {
      await convert(svgPath, outPath, scale);

      // frame_gold / frame_silver 同时写到 Assets/Resources/UI/
      if (OVERRIDE_FILES.has(stem)) {
        const overridePath = path.join(OUT_UI, outName);
        await convert(svgPath, overridePath, scale);
        if (!dry) console.log(`     ↳ 覆盖 Assets/Resources/UI/${outName}`);
      }

      ok++;
    } catch (e) {
      console.error(`  ❌ ${fname}: ${e.message}`);
      errors.push(fname);
    }
  }

  console.log("\n" + "─".repeat(50));
  if (dry) {
    console.log(`[DRY RUN] ${svgs.length} 个文件，未实际写入`);
  } else {
    console.log(`完成：${ok}/${svgs.length} 成功${errors.length ? `，失败：${errors.join(", ")}` : " ✅"}`);
    console.log(`输出：${path.relative(ROOT, OUT_GENERATED)}`);
  }
}

main().catch(e => { console.error(e); process.exit(1); });
