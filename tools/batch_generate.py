"""
批量生成 FWTCG 棋盘贴图 - 统一蒸汽朋克幻想风格
运行：python tools/batch_generate.py
"""

import sys, os
sys.path.insert(0, os.path.dirname(__file__))
from generate_texture import generate

# 统一风格前缀
STYLE = (
    "dark steampunk fantasy TCG game board UI texture, "
    "bronze copper ornate gear decorations at corners and borders, "
    "blue purple arcane rune glowing accents along edges, "
    "deep navy black background surface, "
    "flat 2D top-down orthographic view, no perspective, no 3D shadows, no depth, "
    "no text, no letters, no watermark, "
    "clean seamless game asset, "
)

TASKS = [
    # (filename, extra_prompt, resolution, aspect_ratio)
    (
        "bg_game_main.png",
        STYLE + "full game playmat background, large dark mystical swirling vortex at center, "
                "deep charcoal energy spiral motif, atmospheric wide field, subtle hex grid lines, "
                "steampunk gear ornaments at four corners, moody dramatic atmosphere, "
                "game board surface texture",
        "2k", "1:1"
    ),
    (
        "zone_battlefield.png",
        STYLE + "wide horizontal battlefield zone frame, panoramic rectangular panel, "
                "thick ornate steampunk bronze frame on all sides, glowing blue rune line border, "
                "dark translucent interior fill, battle arena indicator strip, "
                "gear rivets at corners, energy pulse glow along frame edges",
        "1k", "16:9"
    ),
    (
        "zone_base.png",
        STYLE + "wide horizontal base zone bar, rectangular base camp indicator panel, "
                "medium-weight bronze border with gear motifs at ends, "
                "inner dark indigo semi-transparent fill, subtle shield emblem watermark at center, "
                "cool blue glow outline, fortress base zone marker",
        "1k", "16:9"
    ),
    (
        "zone_rune.png",
        STYLE + "wide horizontal rune resource zone bar, magical energy resource strip, "
                "teal blue glowing rune veins pattern inside panel, "
                "thin ornate bronze border, arcane energy flowing lines, "
                "mana crystal slot texture, mystical resource area indicator",
        "1k", "16:9"
    ),
    (
        "zone_hero.png",
        STYLE + "vertical portrait card slot frame, single card placeholder, "
                "dark obsidian rectangle with thick ornate bronze gear border, "
                "blue arcane rune glow at center, champion hero zone indicator, "
                "intricate steampunk frame ornaments, transparent-ready dark interior",
        "1k", "9:16"
    ),
    (
        "zone_legend.png",
        STYLE + "vertical portrait card slot frame, legend card placeholder, "
                "dark obsidian rectangle with thick ornate gold bronze border, "
                "golden amber rune glow at center, legend zone indicator, "
                "premium ornate steampunk frame, more elaborate decoration than hero slot, "
                "warm gold accent light",
        "1k", "9:16"
    ),
    (
        "zone_hand.png",
        STYLE + "wide horizontal hand zone tray bar, card holding area strip, "
                "dark gradient translucent panel, thin golden glow line at top edge, "
                "subtle card slot guides, player hand display area, "
                "panoramic smooth bar with faint bronze edge detail",
        "1k", "16:9"
    ),
    (
        "ui_score_frame.png",
        STYLE + "vertical narrow score counter frame, life point tracker panel, "
                "tall rectangular strip with circular number slots stacked vertically, "
                "bronze ring borders for each number position, "
                "blue glow accent on active row, steampunk dial aesthetic, "
                "health life score sidebar tracker",
        "1k", "9:16"
    ),
    (
        "panel_glass.png",
        STYLE + "rectangular popup panel frame, general purpose dialog window, "
                "dark navy semi-transparent background fill, "
                "ornate bronze corner pieces and border frame, "
                "subtle inner blue glow, clean interior for text content, "
                "fantasy card game popup box, uniform border all sides",
        "1k", "1:1"
    ),
    (
        "zone_main_pile.png",
        STYLE + "small square deck pile zone, main card deck slot area, "
                "compact dark panel with bronze border, subtle stacked card lines pattern, "
                "faint blue shimmer, deck holder indicator, "
                "small neat card zone with gear corner rivets",
        "1k", "1:1"
    ),
]

def main():
    total = len(TASKS)
    success = 0
    failed = []

    print(f"=== 开始批量生成 {total} 张贴图 ===\n")

    for i, (filename, prompt, resolution, ratio) in enumerate(TASKS, 1):
        print(f"\n{'='*60}")
        print(f"[{i}/{total}] 生成: {filename}")
        print(f"{'='*60}")
        try:
            path = generate(prompt, filename, resolution, ratio)
            success += 1
            print(f"[OK] [{i}/{total}] 完成: {filename}")
        except Exception as e:
            failed.append((filename, str(e)))
            print(f"[FAIL] [{i}/{total}] 失败: {filename} -- {e}")

    print(f"\n{'='*60}")
    print(f"=== 批量完成 ===")
    print(f"成功: {success}/{total}")
    if failed:
        print(f"失败列表:")
        for name, err in failed:
            print(f"  [FAIL] {name}: {err}")
    print(f"{'='*60}")

if __name__ == "__main__":
    main()
