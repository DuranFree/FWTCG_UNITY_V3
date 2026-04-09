"""
生成 layout_v3.svg
新比例：压缩英雄行/符文/基地，补回基地区，战场不变
"""

# ── 新区域边界 ──────────────────────────────────────────
# 敌方手牌露出:  y=0  ~ 40
# 敌方英雄行:    y=40,  h=100  → end=140
# gap 4px        → 144
# 敌方符文区:    y=144, h=52   → end=196
# gap 4px        → 200
# 敌方基地:      y=200, h=80   → end=280
# gap 4px        → 284
# 战场:          y=284, h=316  → end=600  center=442
# gap 4px        → 604
# 我方基地:      y=604, h=80   → end=684
# gap 4px        → 688
# 我方符文区:    y=688, h=52   → end=740
# gap 4px        → 744
# 我方英雄行:    y=744, h=100  → end=844
# gap 4px        → 848
# 按钮条:        y=848, h=18   → end=866
# 我方手牌:      y=866 ~ 1080

EH_Y, EH_H = 40, 100        # 敌英雄行
ER_Y, ER_H = 144, 52        # 敌符文区
EB_Y, EB_H = 200, 80        # 敌基地
BF_Y, BF_H = 284, 316       # 战场
PB_Y, PB_H = 604, 80        # 我方基地
PR_Y, PR_H = 688, 52        # 我方符文区
PH_Y, PH_H = 744, 100       # 我方英雄行
BTN_Y, BTN_H = 848, 18      # 按钮条

BF_CENTER = BF_Y + BF_H // 2   # 442

lines = []
def L(s=""):
    lines.append(s)

# ── SVG 头 ────────────────────────────────────────────
L('<svg width="1920" height="1080" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1920 1080">')
L('<defs>')
L('  <radialGradient id="bg" cx="50%" cy="50%" r="70%">')
L('    <stop offset="0%" stop-color="#0f2035"/>')
L('    <stop offset="100%" stop-color="#050d18"/>')
L('  </radialGradient>')
L('  <linearGradient id="goldH" x1="0" y1="0" x2="1" y2="0">')
L('    <stop offset="0%" stop-color="#604808"/>')
L('    <stop offset="35%" stop-color="#c89b3c"/>')
L('    <stop offset="65%" stop-color="#c89b3c"/>')
L('    <stop offset="100%" stop-color="#604808"/>')
L('  </linearGradient>')
L('  <linearGradient id="purH" x1="0" y1="0" x2="1" y2="0">')
L('    <stop offset="0%" stop-color="#281050"/>')
L('    <stop offset="35%" stop-color="#7050c0"/>')
L('    <stop offset="65%" stop-color="#7050c0"/>')
L('    <stop offset="100%" stop-color="#281050"/>')
L('  </linearGradient>')
L('  <linearGradient id="bf0Grad" x1="0" y1="0" x2="0" y2="1">')
L('    <stop offset="0%" stop-color="#081828"/>')
L('    <stop offset="50%" stop-color="#0c2240"/>')
L('    <stop offset="100%" stop-color="#081828"/>')
L('  </linearGradient>')
L('  <linearGradient id="bf1Grad" x1="0" y1="0" x2="0" y2="1">')
L('    <stop offset="0%" stop-color="#081820"/>')
L('    <stop offset="50%" stop-color="#0a2030"/>')
L('    <stop offset="100%" stop-color="#081820"/>')
L('  </linearGradient>')
L('  <filter id="glow" x="-20%" y="-20%" width="140%" height="140%">')
L('    <feGaussianBlur stdDeviation="2.5" result="blur"/>')
L('    <feMerge><feMergeNode in="blur"/><feMergeNode in="SourceGraphic"/></feMerge>')
L('  </filter>')
L('  <filter id="softglow">')
L('    <feGaussianBlur stdDeviation="4" result="blur"/>')
L('    <feMerge><feMergeNode in="blur"/><feMergeNode in="SourceGraphic"/></feMerge>')
L('  </filter>')
L('  <linearGradient id="pillarBorder" x1="0" y1="0" x2="0" y2="1">')
L('    <stop offset="0%"   stop-color="#c89b3c"/>')
L('    <stop offset="25%"  stop-color="#7050c0"/>')
L('    <stop offset="50%"  stop-color="#3070d0"/>')
L('    <stop offset="75%"  stop-color="#7050c0"/>')
L('    <stop offset="100%" stop-color="#c89b3c"/>')
L('  </linearGradient>')
L('  <linearGradient id="pillarFill" x1="0" y1="1" x2="0" y2="0">')
L('    <stop offset="0%"   stop-color="#1848c0"/>')
L('    <stop offset="50%"  stop-color="#6030d0"/>')
L('    <stop offset="100%" stop-color="#c040f0" stop-opacity="0.4"/>')
L('  </linearGradient>')
L(f'  <clipPath id="pillarClip">')
L(f'    <rect x="948" y="{BF_Y}" width="24" height="{BF_H}"/>')
L('  </clipPath>')
L('</defs>')
L()

# ── 背景 ─────────────────────────────────────────────
L('<!-- BACKGROUND -->')
L('<rect width="1920" height="1080" fill="url(#bg)"/>')
L(f'<g opacity="0.04" transform="translate(960,{BF_CENTER})">')
L('  <circle r="260" fill="none" stroke="#4090d0" stroke-width="4"/>')
L('  <circle r="190" fill="none" stroke="#4090d0" stroke-width="2"/>')
L('  <circle r="100" fill="none" stroke="#4090d0" stroke-width="1"/>')
L('  <line x1="-310" y1="0" x2="310" y2="0" stroke="#4090d0" stroke-width="1"/>')
L('  <line x1="0" y1="-310" x2="0" y2="310" stroke="#4090d0" stroke-width="1"/>')
L('</g>')
L('<rect x="1" y="1" width="1918" height="1078" fill="none" stroke="#c89b3c" stroke-width="1" opacity="0.2"/>')
L()

# ── LEFT SCORE STRIP ─────────────────────────────────
L('<!-- LEFT SCORE STRIP (x=0-44) -->')
L('<rect x="0" y="0" width="44" height="1080" fill="#040c14"/>')
L('<line x1="43" y1="0" x2="43" y2="1080" stroke="#c89b3c" stroke-width="1" opacity="0.35"/>')
L('<text x="22" y="14" fill="#c89b3c" font-size="7" text-anchor="middle" font-family="Arial" opacity="0.4" letter-spacing="1">SCORE</text>')
L(f'<line x1="6" y1="{BF_CENTER}" x2="38" y2="{BF_CENTER}" stroke="#c89b3c" stroke-width="1" opacity="0.4"/>')
L()

# Enemy score: 9 circles covering y=40~280
enemy_score_cys = [int(40 + 20 + i*26) for i in range(9)]  # [60, 86, 112, 138, 164, 190, 216, 242, 268]
score_labels = list(range(8, -1, -1))
for i, (cy, lbl) in enumerate(zip(enemy_score_cys, score_labels)):
    opacity = max(0.4, 0.95 - i*0.07)
    sw = 2.0 if i == 0 else (1.5 if i <= 2 else 1.2)
    fill = '"none"' if i < 8 else '"#132030"'
    L(f'<circle cx="22" cy="{cy}" r="14" fill={fill} stroke="#c89b3c" stroke-width="{sw}" opacity="{opacity:.2f}"/>')
    bw = '  font-weight="bold"' if i <= 1 else ''
    L(f'<text   x="22"  y="{cy+5}"  fill="#c89b3c" font-size="{13 if i==0 else 12}" text-anchor="middle" font-family="Arial"{bw}>{lbl}</text>')

L()

# Player score: 9 circles covering y=604~844
player_score_cys = [int(604 + 20 + i*26) for i in range(9)]
for i, (cy, lbl) in enumerate(zip(player_score_cys, score_labels)):
    opacity = max(0.4, 0.95 - i*0.07)
    sw = 2.0 if i == 0 else (1.5 if i <= 2 else 1.2)
    fill = '"none"' if i < 8 else '"#132030"'
    extra = ' filter="url(#glow)"' if i == 0 else ''
    bw = '  font-weight="bold"' if i <= 1 else ''
    L(f'<circle cx="22" cy="{cy}" r="14" fill={fill} stroke="#c89b3c" stroke-width="{sw}" opacity="{opacity:.2f}"{extra}/>')
    L(f'<text   x="22"  y="{cy+5}"  fill="#c89b3c" font-size="{13 if i==0 else 12}" text-anchor="middle" font-family="Arial"{bw}>{lbl}</text>')
L()

# ── LEFT UTIL COLUMN (x=48-244) ──────────────────────
# Deck boxes: enemy zone y=40-280 (240px) and player zone y=604-844 (240px)
# Each side has 2 boxes, 118px each with 4px gap

def deck_box(x, y, w, h, border_color, fill_color, card_stroke, title, subtitle, count, count_color="#d4c090"):
    cx = x + w // 2
    card_x = x + w//2 - 27
    card_y = y + 14
    cw, ch = 54, int(h * 0.52)  # scale card to box height
    L(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{fill_color}" stroke="{border_color}" stroke-width="1.5" rx="5"/>')
    L(f'<rect x="{card_x}" y="{card_y}" width="54" height="{ch}" fill="{fill_color}" stroke="{card_stroke}" stroke-width="1.5" rx="3"/>')
    L(f'<rect x="{card_x+4}" y="{card_y+4}" width="46" height="{ch-8}" fill="none" stroke="{card_stroke}30" stroke-width="1" rx="2"/>')
    ty = card_y + ch + 14
    L(f'<text x="{cx}" y="{ty}" fill="{border_color}" font-size="11" font-family="Arial" font-weight="bold" text-anchor="middle">{title}</text>')
    L(f'<text x="{cx}" y="{ty+11}" fill="{border_color}70" font-size="9" font-family="Arial" text-anchor="middle">{subtitle}</text>')
    L(f'<text x="{cx}" y="{ty+27}" fill="{count_color}" font-size="15" font-family="Arial" font-weight="bold" text-anchor="middle">{count}</text>')

# Enemy Main Deck (top)
deck_box(48, EH_Y, 194, 118, "#c89b3c", "#0c1a28", "#c89b3c", "主牌堆", "MAIN DECK", "30")
# Enemy Rune Deck
deck_box(48, EH_Y+122, 194, 118, "#7050c0", "#0c1228", "#8060d0", "符文堆", "RUNE DECK", "10", "#b090f0")
# Player Rune Deck
deck_box(48, PB_Y, 194, 118, "#7050c0", "#0c1228", "#8060d0", "符文堆", "RUNE DECK", "10", "#b090f0")
# Player Main Deck
deck_box(48, PB_Y+122, 194, 118, "#c89b3c", "#0c1a28", "#c89b3c", "主牌堆", "MAIN DECK", "30")
L()

# ── RIGHT UTIL COLUMN (x=1676-1870) ─────────────────
# Same y positions, different titles
deck_box(1676, EH_Y,      194, 118, "#c89b3c", "#0c1a28", "#c89b3c", "弃牌", "DISCARD", "0")
deck_box(1676, EH_Y+122,  194, 118, "#c89b3c", "#0a1420", "#c89b3c", "放逐区", "EXILE", "0")
deck_box(1676, PB_Y,      194, 118, "#c89b3c", "#0a1420", "#c89b3c", "放逐区", "EXILE", "0")
deck_box(1676, PB_Y+122,  194, 118, "#c89b3c", "#0c1a28", "#c89b3c", "弃牌", "DISCARD", "0")
L()

# ── ENEMY HERO ROW ───────────────────────────────────
L(f'<!-- ENEMY HERO ROW (y={EH_Y}, h={EH_H}) -->')
hero_y = EH_Y + 4
hero_h = EH_H - 10

# Hero slot
L(f'<rect x="252" y="{hero_y}" width="76" height="{hero_h}" fill="#101828" stroke="#d4a828" stroke-width="2" rx="5" filter="url(#glow)"/>')
L(f'<rect x="256" y="{hero_y+4}" width="68" height="{hero_h-8}" fill="#080e1e" stroke="#d4a82860" stroke-width="1" rx="3" stroke-dasharray="3,2"/>')
cy = hero_y + hero_h//2
L(f'<text x="290" y="{cy-4}" fill="#d4a828" font-size="11" font-family="Arial" font-weight="bold" text-anchor="middle">英雄</text>')
L(f'<text x="290" y="{cy+8}" fill="#d4a82890" font-size="8" font-family="Arial" text-anchor="middle">CHAMPION</text>')
# Legend slot
L(f'<rect x="336" y="{hero_y}" width="76" height="{hero_h}" fill="#101828" stroke="#c89b3c" stroke-width="1.5" rx="5"/>')
L(f'<rect x="340" y="{hero_y+4}" width="68" height="{hero_h-8}" fill="#080e1e" stroke="#c89b3c60" stroke-width="1" rx="3" stroke-dasharray="3,2"/>')
L(f'<text x="374" y="{cy-4}" fill="#c89b3c" font-size="11" font-family="Arial" font-weight="bold" text-anchor="middle">传说</text>')
L(f'<text x="374" y="{cy+8}" fill="#c89b3c90" font-size="8" font-family="Arial" text-anchor="middle">LEGEND</text>')
# 技能按钮
L(f'<rect x="340" y="{EH_Y+EH_H-14}" width="68" height="10" fill="#201008" stroke="#c89b3c" stroke-width="1" rx="3"/>')
L(f'<text x="374" y="{EH_Y+EH_H-6}" fill="#c89b3c" font-size="7" text-anchor="middle" font-family="Arial">技 能</text>')
L()

# ── ENEMY HAND FAN ───────────────────────────────────
# pivot (960,-905), base card x=922 y=-31 w=76 h=116 — unchanged from v2
L('<!-- ENEMY HAND FAN (pivot 960,-905) -->')
L(f'<text x="960" y="{EH_Y-6}" fill="#c89b3c" font-size="9" text-anchor="middle" font-family="Arial" opacity="0.45" letter-spacing="2">HAND · 敌方手牌</text>')
angles = [12, -12, 8, -8, 4, -4, 0]
opacities = ["50", "50", "60", "60", "75", "75", "ff"]
stroke_ws = [1.2, 1.2, 1.2, 1.2, 1.3, 1.3, 1.5]
for ang, op, sw in zip(angles, opacities, stroke_ws):
    L(f'<g transform="rotate({ang}, 960, -905)">')
    L(f'  <rect x="922" y="-31" width="76" height="116" fill="#0c1624" stroke="#c89b3c{op}" stroke-width="{sw}" rx="3"/>')
    L('</g>')
L()

# ── ENEMY RUNE ZONE ───────────────────────────────────
L(f'<!-- ENEMY RUNES (y={ER_Y}, h={ER_H}) -->')
L(f'<rect x="248" y="{ER_Y}" width="1424" height="{ER_H}" fill="#0c1030" rx="4"/>')
L(f'<rect x="248" y="{ER_Y}" width="1424" height="{ER_H}" fill="none" stroke="url(#purH)" stroke-width="2" rx="4"/>')
L(f'<text x="258" y="{ER_Y+13}" fill="#9070e0" font-size="10" font-family="Arial" font-weight="bold" letter-spacing="2" filter="url(#glow)">RUNES  符文区</text>')
er_cy = ER_Y + ER_H // 2
er_r = 20
for i in range(12):
    cx = 642 + i * 58
    if i < 8:
        L(f'<circle cx="{cx}" cy="{er_cy}" r="{er_r}" fill="#0c0e22" stroke="#7050c0" stroke-width="1.2"/>')
    else:
        L(f'<circle cx="{cx}" cy="{er_cy}" r="{er_r}" fill="#0a0c1c" stroke="#403080" stroke-width="1" stroke-dasharray="4,2" opacity="0.5"/>')
L()

# ── ENEMY BASE ZONE ───────────────────────────────────
L(f'<!-- ENEMY BASE (y={EB_Y}, h={EB_H}) -->')
L(f'<rect x="248" y="{EB_Y}" width="1424" height="{EB_H}" fill="#0e1c28" rx="4"/>')
L(f'<rect x="248" y="{EB_Y}" width="1424" height="{EB_H}" fill="none" stroke="url(#goldH)" stroke-width="2" rx="4"/>')
L(f'<text x="258" y="{EB_Y+13}" fill="#c89b3c" font-size="10" font-family="Arial" font-weight="bold" letter-spacing="2" filter="url(#glow)">BASE  基地</text>')
# 8 base card slots: w=60, h=66, spacing=8 → total=(60+8)*8-8=536, start_x=(1424-536)//2+248=692
bc_w, bc_h = 60, 66
bc_total = bc_w * 8 + 8 * 7
bc_start_x = 248 + (1424 - bc_total) // 2
bc_y = EB_Y + (EB_H - bc_h) // 2
for i in range(7):
    bx = bc_start_x + i * (bc_w + 8)
    L(f'<rect x="{bx}" y="{bc_y}" width="{bc_w}" height="{bc_h}" fill="#0a1620" stroke="#c89b3c" stroke-width="1" rx="3" stroke-dasharray="4,2"/>')
# standby slot
bx = bc_start_x + 7 * (bc_w + 8)
L(f'<rect x="{bx}" y="{bc_y}" width="{bc_w}" height="{bc_h}" fill="#0a1028" stroke="#4060a0" stroke-width="1.5" rx="3" stroke-dasharray="3,2"/>')
L(f'<text x="{bx+bc_w//2}" y="{bc_y+bc_h//2+3}" fill="#4060a0" font-size="8" text-anchor="middle" font-family="Arial" opacity="0.8">待命</text>')
L()

# ── BATTLEFIELD ───────────────────────────────────────
L(f'<!-- BATTLEFIELD (y={BF_Y}, h={BF_H}, center={BF_CENTER}) -->')
L(f'<rect x="48" y="{BF_Y}" width="896" height="{BF_H}" fill="url(#bf0Grad)" rx="6"/>')
L(f'<rect x="48" y="{BF_Y}" width="896" height="{BF_H}" fill="none" stroke="#4080b0" stroke-width="2.5" rx="6" filter="url(#glow)"/>')
L(f'<rect x="976" y="{BF_Y}" width="894" height="{BF_H}" fill="url(#bf1Grad)" rx="6"/>')
L(f'<rect x="976" y="{BF_Y}" width="894" height="{BF_H}" fill="none" stroke="#3070a0" stroke-width="2.5" rx="6" filter="url(#glow)"/>')
L()

# BF[0] 右侧战场牌区 (center x=889)
bf_title_y = BF_Y + 36
L(f'<text x="889" y="{bf_title_y}" fill="#4080b0" font-size="13" text-anchor="middle" font-family="Arial" font-weight="bold" filter="url(#softglow)">战场 ①</text>')
L(f'<rect x="853" y="{bf_title_y+6}" width="72" height="50" fill="#0c1828" stroke="#c89b3c" stroke-width="1.5" rx="3"/>')
L(f'<rect x="858" y="{bf_title_y+10}" width="62" height="42" fill="none" stroke="#c89b3c40" stroke-width="1" rx="2"/>')
# BF[1] 左侧战场牌区 (center x=1031)
L(f'<text x="1031" y="{bf_title_y}" fill="#3070a0" font-size="13" text-anchor="middle" font-family="Arial" font-weight="bold" filter="url(#softglow)">战场 ②</text>')
L(f'<rect x="995" y="{bf_title_y+6}" width="72" height="50" fill="#0c1828" stroke="#c89b3c" stroke-width="1.5" rx="3"/>')
L(f'<rect x="1000" y="{bf_title_y+10}" width="62" height="42" fill="none" stroke="#c89b3c40" stroke-width="1" rx="2"/>')
L()

# Standby zones (my side, below center line)
sb_y = BF_CENTER + 20
L(f'<rect x="853" y="{sb_y}" width="72" height="106" fill="#0a0e1c" stroke="#4060a0" stroke-width="1.5" rx="3"/>')
L(f'<rect x="857" y="{sb_y+4}" width="64" height="98" fill="none" stroke="#4060a060" stroke-width="1" rx="2" stroke-dasharray="3,2"/>')
L(f'<text x="889" y="{sb_y+48}" fill="#4060a0" font-size="10" text-anchor="middle" font-family="Arial" font-weight="bold">待命区</text>')
L(f'<text x="889" y="{sb_y+60}" fill="#4060a080" font-size="8" text-anchor="middle" font-family="Arial">STANDBY</text>')
L(f'<rect x="995" y="{sb_y}" width="72" height="106" fill="#0a0e1c" stroke="#4060a0" stroke-width="1.5" rx="3"/>')
L(f'<rect x="999" y="{sb_y+4}" width="64" height="98" fill="none" stroke="#4060a060" stroke-width="1" rx="2" stroke-dasharray="3,2"/>')
L(f'<text x="1031" y="{sb_y+48}" fill="#4060a0" font-size="10" text-anchor="middle" font-family="Arial" font-weight="bold">待命区</text>')
L(f'<text x="1031" y="{sb_y+60}" fill="#4060a080" font-size="8" text-anchor="middle" font-family="Arial">STANDBY</text>')
L()

# Center divider
cd_y = BF_CENTER + 4
L(f'<line x1="56" y1="{cd_y}" x2="830" y2="{cd_y}" stroke="#2a5888" stroke-width="1.5" stroke-dasharray="20,8" opacity="0.7"/>')
L(f'<line x1="1090" y1="{cd_y}" x2="1862" y2="{cd_y}" stroke="#2a5888" stroke-width="1.5" stroke-dasharray="20,8" opacity="0.7"/>')
L()

# BF[0] enemy card slots (34px from top of BF, y=318)
ec_y = BF_Y + 34
pc_y = BF_CENTER + 16   # player card slots
for i in range(8):
    cx = 94 + i * 92
    L(f'<rect x="{cx}" y="{ec_y}" width="76" height="116" fill="#091828" stroke="#3a6090" stroke-width="1" rx="3" stroke-dasharray="4,2"/>')
for i in range(8):
    cx = 94 + i * 92
    L(f'<rect x="{cx}" y="{pc_y}" width="76" height="116" fill="#0a1c2c" stroke="#4090b0" stroke-width="1" rx="3" stroke-dasharray="4,2"/>')
# BF[1] card slots
for i in range(8):
    cx = 1104 + i * 92
    L(f'<rect x="{cx}" y="{ec_y}" width="76" height="116" fill="#091828" stroke="#3a6090" stroke-width="1" rx="3" stroke-dasharray="4,2"/>')
for i in range(8):
    cx = 1104 + i * 92
    L(f'<rect x="{cx}" y="{pc_y}" width="76" height="116" fill="#0a1c2c" stroke="#4090b0" stroke-width="1" rx="3" stroke-dasharray="4,2"/>')
L()

# ── 魔法符纹能量柱 ─────────────────────────────────────
L(f'<!-- 魔法符纹能量柱 (x=944-976, bf center={BF_CENTER}) -->')
L(f'<rect x="944" y="{BF_Y}" width="32" height="{BF_H}" fill="#030810"/>')
L(f'<polygon points="952,{BF_Y} 968,{BF_Y} 976,{BF_Y+8} 960,{BF_Y+16} 944,{BF_Y+8}" fill="#120c20" stroke="url(#pillarBorder)" stroke-width="1"/>')
L(f'<polygon points="952,{BF_Y+BF_H} 968,{BF_Y+BF_H} 976,{BF_Y+BF_H-8} 960,{BF_Y+BF_H-16} 944,{BF_Y+BF_H-8}" fill="#120c20" stroke="url(#pillarBorder)" stroke-width="1"/>')
L(f'<rect x="944" y="{BF_Y+8}" width="32" height="{BF_H-16}" fill="none" stroke="url(#pillarBorder)" stroke-width="1.5"/>')
fill_y = BF_CENTER - 80
L(f'<rect x="948" y="{fill_y}" width="24" height="160" fill="url(#pillarFill)" clip-path="url(#pillarClip)" opacity="0.8" rx="2"/>')
L(f'<rect x="944" y="{fill_y-3}" width="32" height="5" fill="#5090ff" opacity="0.7" rx="1" filter="url(#glow)"/>')
L(f'<text x="960" y="{BF_Y+30}" fill="#c89b3c" font-size="9" text-anchor="middle" font-family="Arial" opacity="0.5">✦</text>')
L(f'<text x="960" y="{BF_CENTER+5}" fill="#7090c0" font-size="7" text-anchor="middle" font-family="Arial" opacity="0.6">⬟</text>')
L(f'<text x="960" y="{BF_Y+BF_H-18}" fill="#5080d0" font-size="9" text-anchor="middle" font-family="Arial" opacity="0.7">✦</text>')
L(f'<text x="960" y="{BF_Y-5}" fill="#c84040" font-size="6.5" text-anchor="middle" font-family="Arial" opacity="0.7" letter-spacing="1">对方 ↓</text>')
L(f'<text x="960" y="{BF_Y+BF_H+10}" fill="#40c070" font-size="6.5" text-anchor="middle" font-family="Arial" opacity="0.7" letter-spacing="1">我方 ↑</text>')
L()

# ── PLAYER BASE ZONE ──────────────────────────────────
L(f'<!-- PLAYER BASE (y={PB_Y}, h={PB_H}) -->')
L(f'<rect x="248" y="{PB_Y}" width="1424" height="{PB_H}" fill="#0e1c28" rx="4"/>')
L(f'<rect x="248" y="{PB_Y}" width="1424" height="{PB_H}" fill="none" stroke="url(#goldH)" stroke-width="2" rx="4"/>')
L(f'<text x="258" y="{PB_Y+13}" fill="#c89b3c" font-size="10" font-family="Arial" font-weight="bold" letter-spacing="2" filter="url(#glow)">BASE  基地</text>')
pb_y = PB_Y + (PB_H - bc_h) // 2
for i in range(7):
    bx = bc_start_x + i * (bc_w + 8)
    L(f'<rect x="{bx}" y="{pb_y}" width="{bc_w}" height="{bc_h}" fill="#0a1620" stroke="#c89b3c" stroke-width="1" rx="3" stroke-dasharray="4,2"/>')
bx = bc_start_x + 7 * (bc_w + 8)
L(f'<rect x="{bx}" y="{pb_y}" width="{bc_w}" height="{bc_h}" fill="#0a1028" stroke="#4060a0" stroke-width="1.5" rx="3" stroke-dasharray="3,2"/>')
L(f'<text x="{bx+bc_w//2}" y="{pb_y+bc_h//2+3}" fill="#4060a0" font-size="8" text-anchor="middle" font-family="Arial" opacity="0.8">待命</text>')
L()

# ── PLAYER RUNE ZONE ──────────────────────────────────
L(f'<!-- PLAYER RUNES (y={PR_Y}, h={PR_H}) -->')
L(f'<rect x="248" y="{PR_Y}" width="1424" height="{PR_H}" fill="#0c1030" rx="4"/>')
L(f'<rect x="248" y="{PR_Y}" width="1424" height="{PR_H}" fill="none" stroke="url(#purH)" stroke-width="2" rx="4"/>')
L(f'<text x="258" y="{PR_Y+13}" fill="#9070e0" font-size="10" font-family="Arial" font-weight="bold" letter-spacing="2" filter="url(#glow)">RUNES  符文区</text>')
pr_cy = PR_Y + PR_H // 2
for i in range(12):
    cx = 642 + i * 58
    if i < 8:
        L(f'<circle cx="{cx}" cy="{pr_cy}" r="{er_r}" fill="#0c0e22" stroke="#7050c0" stroke-width="1.2"/>')
    else:
        L(f'<circle cx="{cx}" cy="{pr_cy}" r="{er_r}" fill="#0a0c1c" stroke="#403080" stroke-width="1" stroke-dasharray="4,2" opacity="0.5"/>')
L()

# ── PLAYER HERO ROW ───────────────────────────────────
L(f'<!-- PLAYER HERO ROW (y={PH_Y}, h={PH_H}) -->')
ph_hero_y = PH_Y + 4
# Hero slot
L(f'<rect x="366" y="{ph_hero_y}" width="76" height="{hero_h}" fill="#101828" stroke="#d4a828" stroke-width="2" rx="5" filter="url(#glow)"/>')
L(f'<rect x="370" y="{ph_hero_y+4}" width="68" height="{hero_h-8}" fill="#080e1e" stroke="#d4a82860" stroke-width="1" rx="3" stroke-dasharray="3,2"/>')
ph_cy = ph_hero_y + hero_h // 2
L(f'<text x="404" y="{ph_cy-4}" fill="#d4a828" font-size="11" font-family="Arial" font-weight="bold" text-anchor="middle">英雄</text>')
L(f'<text x="404" y="{ph_cy+8}" fill="#d4a82890" font-size="8" font-family="Arial" text-anchor="middle">CHAMPION</text>')
# Legend slot
L(f'<rect x="450" y="{ph_hero_y}" width="76" height="{hero_h}" fill="#101828" stroke="#c89b3c" stroke-width="1.5" rx="5"/>')
L(f'<rect x="454" y="{ph_hero_y+4}" width="68" height="{hero_h-8}" fill="#080e1e" stroke="#c89b3c60" stroke-width="1" rx="3" stroke-dasharray="3,2"/>')
L(f'<text x="488" y="{ph_cy-4}" fill="#c89b3c" font-size="11" font-family="Arial" font-weight="bold" text-anchor="middle">传说</text>')
L(f'<text x="488" y="{ph_cy+8}" fill="#c89b3c90" font-size="8" font-family="Arial" text-anchor="middle">LEGEND</text>')
# 技能按钮
L(f'<rect x="454" y="{PH_Y+PH_H-14}" width="68" height="10" fill="#201008" stroke="#c89b3c" stroke-width="1" rx="3"/>')
L(f'<text x="488" y="{PH_Y+PH_H-6}" fill="#c89b3c" font-size="7" text-anchor="middle" font-family="Arial">技 能</text>')
L()

# ── PLAYER HAND FAN ───────────────────────────────────
# 新 pivot: (960, 1860), base card x=922 y=878 w=76 h=116
HAND_PIVOT_Y = 1860
HAND_CARD_Y  = 878
L(f'<!-- PLAYER HAND FAN (pivot 960,{HAND_PIVOT_Y}) -->')
L(f'<text x="960" y="{BTN_Y-4}" fill="#c89b3c" font-size="9" text-anchor="middle" font-family="Arial" opacity="0.45" letter-spacing="2">HAND · 我方手牌</text>')
angles_p = [-12, -8, -4, 0, 4, 8, 12]
opacities_p = ["55", "65", "80", "ff", "80", "65", "55"]
stroke_ws_p = [1.2, 1.2, 1.3, 1.5, 1.3, 1.2, 1.2]
for ang, op, sw in zip(angles_p, opacities_p, stroke_ws_p):
    L(f'<g transform="rotate({ang}, 960, {HAND_PIVOT_Y})">')
    L(f'  <rect x="922" y="{HAND_CARD_Y}" width="76" height="116" fill="#0c1624" stroke="#c89b3c{op}" stroke-width="{sw}" rx="3"/>')
    L('</g>')
L()

# ── 内边框 ────────────────────────────────────────────
L('<!-- 内边框 -->')
L(f'<rect x="252" y="{EH_Y+4}" width="1416" height="{EH_H-8}" fill="none" stroke="#c89b3c" stroke-width="0.6" rx="3" opacity="0.22"/>')
L(f'<rect x="252" y="{ER_Y+4}" width="1416" height="{ER_H-8}" fill="none" stroke="#9070e0" stroke-width="0.6" rx="3" opacity="0.22"/>')
L(f'<rect x="252" y="{EB_Y+4}" width="1416" height="{EB_H-8}" fill="none" stroke="#c89b3c" stroke-width="0.6" rx="3" opacity="0.22"/>')
L(f'<rect x="252" y="{PB_Y+4}" width="1416" height="{PB_H-8}" fill="none" stroke="#c89b3c" stroke-width="0.6" rx="3" opacity="0.22"/>')
L(f'<rect x="252" y="{PR_Y+4}" width="1416" height="{PR_H-8}" fill="none" stroke="#9070e0" stroke-width="0.6" rx="3" opacity="0.22"/>')
L()

# ── 操作按钮组 (右侧, y=748~840) ───────────────────────
btn_area_y = PH_Y + 4
L('<!-- 操作按钮组 -->')
L(f'<rect x="1446" y="{btn_area_y}" width="108" height="34" fill="#0c2240" stroke="#4090d0" stroke-width="2" rx="5" filter="url(#glow)"/>')
L(f'<rect x="1450" y="{btn_area_y+4}" width="100" height="26" fill="none" stroke="#4090d030" stroke-width="0.8" rx="4"/>')
L(f'<text x="1500" y="{btn_area_y+15}" fill="#60b0f0" font-size="13" text-anchor="middle" font-family="Arial" font-weight="bold" filter="url(#glow)">结束回合</text>')
L(f'<text x="1500" y="{btn_area_y+27}" fill="#4090d070" font-size="7" text-anchor="middle" font-family="Arial" letter-spacing="2">END TURN</text>')
L(f'<rect x="1450" y="{btn_area_y+42}" width="100" height="28" fill="#080e1c" stroke="#2a5888" stroke-width="1.2" rx="4"/>')
L(f'<text x="1500" y="{btn_area_y+58}" fill="#3878b0" font-size="10" text-anchor="middle" font-family="Arial" font-weight="bold">召回单位</text>')
L(f'<rect x="1446" y="{btn_area_y+78}" width="108" height="28" fill="#080e1c" stroke="#2a5888" stroke-width="1.2" rx="4"/>')
L(f'<text x="1500" y="{btn_area_y+94}" fill="#3878b0" font-size="10" text-anchor="middle" font-family="Arial" font-weight="bold">查看弃牌堆</text>')
L()

# ── 按钮条 ────────────────────────────────────────────
L(f'<!-- BUTTON STRIP (y={BTN_Y}, h={BTN_H}) -->')
L(f'<rect x="248" y="{BTN_Y}" width="1424" height="{BTN_H}" fill="#060e18" stroke="#304860" stroke-width="1" rx="2"/>')
L(f'<text x="680"  y="{BTN_Y+13}" fill="#506878" font-size="8" font-family="Arial" text-anchor="middle">法力: 3/3</text>')
L(f'<text x="820"  y="{BTN_Y+13}" fill="#506878" font-size="8" font-family="Arial" text-anchor="middle">符能: 2/2</text>')
L(f'<text x="960"  y="{BTN_Y+13}" fill="#506878" font-size="8" font-family="Arial" text-anchor="middle">阶段: 行动</text>')
L(f'<text x="1100" y="{BTN_Y+13}" fill="#506878" font-size="8" font-family="Arial" text-anchor="middle">回合: 第 3 回合</text>')
L(f'<text x="1240" y="{BTN_Y+13}" fill="#506878" font-size="8" font-family="Arial" text-anchor="middle">先手: 玩家</text>')
L()

# ── 版本标记 ──────────────────────────────────────────
L(f'<text x="960" y="1079" fill="#c89b3c" font-size="6" text-anchor="middle" font-family="Arial" opacity="0.2">')
L(f'  FWTCG PC Layout v3.0  |  1920×1080  |  英雄100+符文52+基地80+战场316+基地80+符文52+英雄100+按钮18 = 862px  |  手牌866~1080')
L('</text>')
L('</svg>')

# ── 写出文件 ──────────────────────────────────────────
import os
out = os.path.join(os.path.dirname(__file__), "layout_v3.svg")
with open(out, "w", encoding="utf-8") as f:
    f.write("\n".join(lines))
print(f"Written: {out}  ({len(lines)} lines)")
