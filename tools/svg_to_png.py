"""
svg_to_png.py — SVG → PNG 批量转换脚本
依赖：pip install cairosvg

用法：
  python tools/svg_to_png.py          # 全量转换
  python tools/svg_to_png.py --dry    # 仅预览，不写文件

规则：
  - bg_* 文件：原始 SVG 尺寸输出，宽度上限 2048px
  - 其余文件：4x 放大输出
  - frame_gold / frame_silver → 同时输出到 Assets/Resources/UI/（覆盖旧版）
  - 其余 → Assets/Resources/UI/Generated/
"""

import sys
import os
import re

try:
    import cairosvg
except ImportError:
    print("❌ 缺少依赖：cairosvg")
    print("   请运行：pip install cairosvg")
    sys.exit(1)

# ── 路径配置 ──────────────────────────────────────────────
SCRIPT_DIR   = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.dirname(SCRIPT_DIR)
SVG_DIR      = os.path.join(PROJECT_ROOT, "Assets", "Art", "SVG")
OUT_GENERATED = os.path.join(PROJECT_ROOT, "Assets", "Resources", "UI", "Generated")
OUT_UI        = os.path.join(PROJECT_ROOT, "Assets", "Resources", "UI")

# 需要额外输出到 Assets/Resources/UI/ 覆盖旧贴图的文件
OVERRIDE_FILES = {"frame_gold", "frame_silver"}

# bg_* 最大宽度（原尺寸，不 4x）
BG_MAX_WIDTH = 2048

# 默认缩放倍数
SCALE_4X = 4.0

# ── SVG 尺寸读取（正则，不依赖 lxml）────────────────────
_WH_RE = re.compile(r'<svg[^>]*\swidth="([0-9.]+)"[^>]*\sheight="([0-9.]+)"')

def get_svg_size(path: str):
    with open(path, "r", encoding="utf-8") as f:
        head = f.read(1024)
    m = _WH_RE.search(head)
    if m:
        return float(m.group(1)), float(m.group(2))
    return None, None

# ── 主转换逻辑 ────────────────────────────────────────────
def convert(svg_path: str, out_path: str, scale: float, dry: bool):
    w, h = get_svg_size(svg_path)
    if w and h:
        out_w = int(w * scale)
        out_h = int(h * scale)
    else:
        out_w = out_h = None

    if dry:
        size_str = f"{out_w}×{out_h}" if out_w else "auto"
        print(f"  [dry] {os.path.basename(svg_path)} → {os.path.relpath(out_path, PROJECT_ROOT)}  ({size_str})")
        return

    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    kwargs = {"write_to": out_path}
    if out_w:
        kwargs["output_width"]  = out_w
        kwargs["output_height"] = out_h

    cairosvg.svg2png(url=svg_path, **kwargs)
    size_str = f"{out_w}×{out_h}" if out_w else "auto"
    print(f"  ✅ {os.path.basename(svg_path)} → {os.path.relpath(out_path, PROJECT_ROOT)}  ({size_str})")

def main():
    dry = "--dry" in sys.argv

    if not os.path.isdir(SVG_DIR):
        print(f"❌ SVG 目录不存在：{SVG_DIR}")
        sys.exit(1)

    svgs = sorted(f for f in os.listdir(SVG_DIR) if f.endswith(".svg"))
    if not svgs:
        print("⚠️  SVG 目录为空，无文件可转换")
        return

    print(f"{'[DRY RUN] ' if dry else ''}开始转换 {len(svgs)} 个 SVG → PNG\n")

    ok = 0
    errors = []

    for fname in svgs:
        stem = os.path.splitext(fname)[0]
        svg_path = os.path.join(SVG_DIR, fname)
        out_name = stem + ".png"

        # 决定输出路径
        out_path = os.path.join(OUT_GENERATED, out_name)

        # 决定缩放
        is_bg = stem.startswith("bg_")
        if is_bg:
            w, _ = get_svg_size(svg_path)
            scale = min(1.0, BG_MAX_WIDTH / w) if w else 1.0
        else:
            scale = SCALE_4X

        try:
            convert(svg_path, out_path, scale, dry)

            # frame_gold / frame_silver 同时覆盖 UI/ 目录
            if stem in OVERRIDE_FILES and not dry:
                override_path = os.path.join(OUT_UI, out_name)
                convert(svg_path, override_path, scale, dry)
                print(f"     ↳ 同步覆盖 Assets/Resources/UI/{out_name}")

            ok += 1
        except Exception as e:
            print(f"  ❌ {fname}: {e}")
            errors.append(fname)

    print(f"\n{'─'*50}")
    if dry:
        print(f"[DRY RUN] 共 {len(svgs)} 个文件，实际未写入")
    else:
        print(f"完成：{ok}/{len(svgs)} 成功", end="")
        if errors:
            print(f"，{len(errors)} 个失败：{errors}")
        else:
            print(" ✅")
        print(f"输出目录：{os.path.relpath(OUT_GENERATED, PROJECT_ROOT)}")

if __name__ == "__main__":
    main()
