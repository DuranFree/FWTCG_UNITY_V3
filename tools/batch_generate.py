"""
批量生成 FWTCG 棋盘贴图 - LoL TCG 棋盘垫风格（金边切角 + 深海军蓝）
运行：python tools/batch_generate.py
"""

import sys, os
sys.path.insert(0, os.path.dirname(__file__))
from generate_texture import generate

# 统一风格前缀 — LoL TCG mat 风格
STYLE = (
    "Legends of Runeterra TCG game board UI texture, "
    "dark navy deep blue matte fabric surface background, "
    "ornate gold chamfered border frame with 45-degree diagonal corner cuts, "
    "small gold diamond rhombus decorative ornament at each chamfered corner, "
    "thin inner gold dashed line border offset 4px from outer frame, "
    "flat 2D top-down orthographic view, no perspective, no 3D depth, no shadows, "
    "no text, no letters, no numbers, no watermark, no logo, "
    "clean seamless game asset, photorealistic game board material quality, "
)

# 卡槽专用风格（用于单张卡牌占位框）
CARD_SLOT_STYLE = (
    "Legends of Runeterra TCG card slot placeholder frame, "
    "dark navy interior fill, "
    "gold ornate chamfered border with 45-degree cut corners, "
    "small gold fleur-de-lis or diamond ornament at each chamfered corner point, "
    "thin inner dashed gold line border, "
    "subtle rune watermark faintly visible at center interior, "
    "flat 2D portrait card frame, no perspective, no 3D, "
    "no text, no letters, clean game UI asset, "
)

TASKS = [
    # (filename, extra_prompt, resolution, aspect_ratio)
    (
        "bg_game_main.png",
        "Legends of Runeterra TCG game board playmat full background, "
        "large dark navy matte fabric surface, "
        "centered glowing gold spiral crest emblem logo watermark, "
        "intricate circular mandala with runic ring and dragon/magic motifs at center, "
        "subtle hexagonal grid pattern across entire surface, "
        "very faint gold line grid texture overlay, "
        "four corner gold ornate decorative frames, "
        "atmospheric moody dark blue game board, flat 2D top-down view, "
        "no text, no letters, seamless game mat texture, photorealistic quality",
        "2k", "1:1"
    ),
    (
        "zone_battlefield.png",
        STYLE +
        "wide horizontal battlefield zone frame panel, "
        "panoramic rectangular frame for playing cards, "
        "gold chamfered border all sides with cut 45-degree corners, "
        "diamond ornaments at all four chamfered corner cuts, "
        "deep navy semi-transparent interior fill, "
        "thin inner dashed gold line border, "
        "very faint crossed-swords crest watermark at center, "
        "battle arena zone indicator, wide landscape orientation",
        "1k", "16:9"
    ),
    (
        "zone_base.png",
        STYLE +
        "wide horizontal base camp zone frame panel, "
        "rectangular defensive zone frame, "
        "gold chamfered border with 45-degree corner cuts and diamond ornaments, "
        "deep navy semi-transparent interior, "
        "thin inner dashed gold border, "
        "faint castle shield crest watermark at center interior, "
        "fortress base zone indicator, wide landscape orientation",
        "1k", "16:9"
    ),
    (
        "zone_rune.png",
        STYLE +
        "wide horizontal rune resource zone frame panel, "
        "arcane mana resource strip frame, "
        "gold chamfered border with cut corners and diamond ornaments, "
        "deep navy interior with faint flowing arcane rune pattern texture, "
        "subtle teal blue glowing rune veins visible inside panel, "
        "thin inner dashed gold border, "
        "mystical resource zone indicator, wide landscape orientation",
        "1k", "16:9"
    ),
    (
        "zone_hero.png",
        CARD_SLOT_STYLE +
        "vertical portrait hero champion card slot, "
        "gold ornate chamfered frame with blue arcane rune glow accent at center, "
        "hero champion zone placeholder, portrait orientation 9:16 aspect ratio",
        "1k", "9:16"
    ),
    (
        "zone_legend.png",
        CARD_SLOT_STYLE +
        "vertical portrait legend card slot, premium quality frame, "
        "rich gold ornate chamfered frame more elaborate than hero slot, "
        "warm amber golden glow accent at center interior, "
        "legend zone placeholder, larger corner ornaments, "
        "portrait orientation 9:16 aspect ratio",
        "1k", "9:16"
    ),
    (
        "zone_hand.png",
        STYLE +
        "wide horizontal hand card tray zone, panoramic card holding strip, "
        "dark gradient translucent panel bar, "
        "thin gold chamfered border with cut corners, "
        "subtle inner gold glow line at top edge, "
        "smooth panoramic bar for holding fan of cards, "
        "player hand display zone indicator, very wide landscape 16:9",
        "1k", "16:9"
    ),
    (
        "ui_score_frame.png",
        STYLE +
        "tall vertical narrow score tracker panel frame, "
        "life point health counter sidebar, "
        "gold chamfered border with cut corners and diamond ornaments, "
        "stacked circular slots for number display, "
        "dark navy interior with circular ring borders at each number position, "
        "portrait orientation tall and narrow",
        "1k", "9:16"
    ),
    (
        "panel_glass.png",
        STYLE +
        "rectangular popup dialog panel frame, "
        "dark navy semi-transparent interior fill, "
        "gold ornate chamfered border with 45-degree cut corners and diamond ornaments, "
        "inner dashed gold border line, "
        "clean interior space for text content, "
        "fantasy TCG popup window frame, square format",
        "1k", "1:1"
    ),
    (
        "zone_main_pile.png",
        CARD_SLOT_STYLE +
        "small square deck pile zone, main card deck slot, "
        "compact frame with stacked card silhouette pattern, "
        "gold chamfered border, subtle faint deck stack lines, "
        "square format card deck holder indicator",
        "1k", "1:1"
    ),
]

def main():
    total = len(TASKS)
    success = 0
    failed = []

    print(f"=== 开始批量生成 {total} 张贴图（LoL TCG mat 风格）===\n")

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
