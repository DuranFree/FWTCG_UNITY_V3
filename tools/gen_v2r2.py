"""重新生成 layout_v2.svg — v2.3: 手牌大, 符文/基地扩展, 侧栏宽度不变"""

EH_Y, EH_H = 8,   100
ER_Y, ER_H = 110, 130
EB_Y, EB_H = 242, 160
BF_Y, BF_H = 404, 260
PB_Y, PB_H = 666, 160
PR_Y, PR_H = 828, 130
PH_Y, PH_H = 960, 100
BTN_Y, BTN_H = 1062, 18
BF_CENTER = BF_Y + BF_H // 2  # 534

CARD_RATIO = 1.4  # 标准卡牌高宽比 h/w (TCG 63×88mm)

# 侧栏宽度保持原来不变
SCORE_W  = 44
DECK_X_L = 48
DECK_X_R = 1676
DECK_W   = 194
MAIN_X   = 248
MAIN_END = 1672
MAIN_W   = MAIN_END - MAIN_X   # 1424

HAND_W, HAND_H = 110, int(110 * CARD_RATIO)  # = 154，标准卡牌比例
HAND_CARD_X = 960 - HAND_W // 2    # 905
PLAYER_CARD_Y = 908
PLAYER_PIVOT  = 2050
ENEMY_CARD_Y  = -90
ENEMY_PIVOT   = -1100

RUNE_R    = 46
RUNE_STEP = 68
RUNE_COUNT = 12
RUNE_START_CX = MAIN_X + (MAIN_W - ((RUNE_COUNT-1)*RUNE_STEP + 2*RUNE_R)) // 2 + RUNE_R

BASE_CARD_W  = 72
BASE_CARD_GAP = 8
BASE_SLOT_COUNT = 8
BASE_TOTAL_W = BASE_SLOT_COUNT*BASE_CARD_W + (BASE_SLOT_COUNT-1)*BASE_CARD_GAP
BASE_START_X = MAIN_X + (MAIN_W - BASE_TOTAL_W) // 2

lines = []
def L(s=""): lines.append(s)

L('<svg width="1920" height="1080" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1920 1080">')
L('<defs>')
L('  <radialGradient id="bg" cx="50%" cy="50%" r="70%">')
L('    <stop offset="0%" stop-color="#0f2035"/><stop offset="100%" stop-color="#050d18"/>')
L('  </radialGradient>')
L('  <linearGradient id="goldH" x1="0" y1="0" x2="1" y2="0">')
L('    <stop offset="0%" stop-color="#604808"/><stop offset="35%" stop-color="#c89b3c"/>')
L('    <stop offset="65%" stop-color="#c89b3c"/><stop offset="100%" stop-color="#604808"/>')
L('  </linearGradient>')
L('  <linearGradient id="purH" x1="0" y1="0" x2="1" y2="0">')
L('    <stop offset="0%" stop-color="#281050"/><stop offset="35%" stop-color="#7050c0"/>')
L('    <stop offset="65%" stop-color="#7050c0"/><stop offset="100%" stop-color="#281050"/>')
L('  </linearGradient>')
L('  <linearGradient id="bf0Grad" x1="0" y1="0" x2="0" y2="1">')
L('    <stop offset="0%" stop-color="#081828"/><stop offset="50%" stop-color="#0c2240"/><stop offset="100%" stop-color="#081828"/>')
L('  </linearGradient>')
L('  <linearGradient id="bf1Grad" x1="0" y1="0" x2="0" y2="1">')
L('    <stop offset="0%" stop-color="#081820"/><stop offset="50%" stop-color="#0a2030"/><stop offset="100%" stop-color="#081820"/>')
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
L('    <stop offset="0%" stop-color="#c89b3c"/><stop offset="25%" stop-color="#7050c0"/>')
L('    <stop offset="50%" stop-color="#3070d0"/><stop offset="75%" stop-color="#7050c0"/>')
L('    <stop offset="100%" stop-color="#c89b3c"/>')
L('  </linearGradient>')
L('  <linearGradient id="pillarFill" x1="0" y1="1" x2="0" y2="0">')
L('    <stop offset="0%" stop-color="#1848c0"/><stop offset="50%" stop-color="#6030d0"/>')
L('    <stop offset="100%" stop-color="#c040f0" stop-opacity="0.4"/>')
L('  </linearGradient>')
L(f'  <clipPath id="pillarClip"><rect x="948" y="{BF_Y}" width="24" height="{BF_H}"/></clipPath>')
L('</defs>')
L()

L('<rect width="1920" height="1080" fill="url(#bg)"/>')
L(f'<g opacity="0.04" transform="translate(960,{BF_CENTER})">')
L('  <circle r="240" fill="none" stroke="#4090d0" stroke-width="4"/>')
L('  <circle r="170" fill="none" stroke="#4090d0" stroke-width="2"/>')
L('  <circle r="90" fill="none" stroke="#4090d0" stroke-width="1"/>')
L('  <line x1="-280" y1="0" x2="280" y2="0" stroke="#4090d0" stroke-width="1"/>')
L('  <line x1="0" y1="-280" x2="0" y2="280" stroke="#4090d0" stroke-width="1"/>')
L('</g>')
L('<rect x="1" y="1" width="1918" height="1078" fill="none" stroke="#c89b3c" stroke-width="1" opacity="0.2"/>')
L()

L('<!-- LEFT SCORE STRIP -->')
L(f'<rect x="0" y="0" width="{SCORE_W}" height="1080" fill="#040c14"/>')
L(f'<line x1="{SCORE_W-1}" y1="0" x2="{SCORE_W-1}" y2="1080" stroke="#c89b3c" stroke-width="1" opacity="0.3"/>')
L(f'<text x="{SCORE_W//2}" y="14" fill="#c89b3c" font-size="7" text-anchor="middle" font-family="Arial" opacity="0.4" letter-spacing="1">SCORE</text>')
L(f'<line x1="4" y1="{BF_CENTER}" x2="{SCORE_W-4}" y2="{BF_CENTER}" stroke="#c89b3c" stroke-width="1" opacity="0.4"/>')

scx = SCORE_W // 2
e_score_step = (EB_Y + EB_H - EH_Y - 40) // 8
for i, lbl in enumerate(range(8, -1, -1)):
    cy = EH_Y + 20 + i * e_score_step
    op = max(0.38, 0.92 - i*0.07)
    sw = 1.8 if i == 0 else (1.4 if i <= 2 else 1.1)
    fill = "none" if i < 8 else "#132030"
    bw = ' font-weight="bold"' if i <= 1 else ''
    L(f'<circle cx="{scx}" cy="{cy}" r="11" fill="{fill}" stroke="#c89b3c" stroke-width="{sw}" opacity="{op:.2f}"/>')
    L(f'<text x="{scx}" y="{cy+4}" fill="#c89b3c" font-size="11" text-anchor="middle" font-family="Arial"{bw}>{lbl}</text>')

p_score_step = (PH_Y + PH_H - PB_Y - 40) // 8
for i, lbl in enumerate(range(8, -1, -1)):
    cy = PB_Y + 20 + i * p_score_step
    op = max(0.38, 0.92 - i*0.07)
    sw = 1.8 if i == 0 else (1.4 if i <= 2 else 1.1)
    fill = "none" if i < 8 else "#132030"
    bw = ' font-weight="bold"' if i <= 1 else ''
    L(f'<circle cx="{scx}" cy="{cy}" r="11" fill="{fill}" stroke="#c89b3c" stroke-width="{sw}" opacity="{op:.2f}"/>')
    L(f'<text x="{scx}" y="{cy+4}" fill="#c89b3c" font-size="11" text-anchor="middle" font-family="Arial"{bw}>{lbl}</text>')
L()

def deck_box(x, y, w, h, cx, title, subtitle, count, border_c, fill_c, card_c, num_c="#d4c090"):
    cw = w - 12
    ch = h - 16
    card_x = x + 6
    card_y = y + 8
    L(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="#0a1620" stroke="{border_c}" stroke-width="1.5" rx="4"/>')
    L(f'<rect x="{card_x}" y="{card_y}" width="{cw}" height="{ch}" fill="{fill_c}" stroke="{card_c}" stroke-width="1.5" rx="3"/>')
    L(f'<rect x="{card_x+3}" y="{card_y+3}" width="{cw-6}" height="{ch-6}" fill="none" stroke="{card_c}30" stroke-width="1" rx="2"/>')
    L(f'<text x="{cx}" y="{card_y+int(ch*0.22)}" fill="{border_c}" font-size="13" font-family="Arial" font-weight="bold" text-anchor="middle">{title}</text>')
    L(f'<text x="{cx}" y="{card_y+int(ch*0.22)+16}" fill="{border_c}70" font-size="9" font-family="Arial" text-anchor="middle">{subtitle}</text>')
    L(f'<text x="{cx}" y="{card_y+int(ch*0.68)}" fill="{num_c}" font-size="38" font-family="Arial" font-weight="bold" text-anchor="middle">{count}</text>')

deck_h = (EB_Y + EB_H - EH_Y - 4) // 2
player_deck_h = (PH_Y + PH_H - PB_Y - 4) // 2

def card_box(col_x, col_w, h):
    """按卡牌比例1:1.4计算牌堆框尺寸，居中于列宽内。返回(x, w)"""
    ch = h - 16  # 内框高度
    bw = int(ch / CARD_RATIO) + 12  # 外框宽度
    bw = min(bw, col_w)
    return col_x + (col_w - bw) // 2, bw

lb_x, lb_w = card_box(DECK_X_L, DECK_W, deck_h)
rb_x, rb_w = card_box(DECK_X_R, DECK_W, deck_h)
lpb_x, lpb_w = card_box(DECK_X_L, DECK_W, player_deck_h)
rpb_x, rpb_w = card_box(DECK_X_R, DECK_W, player_deck_h)
lcx = lb_x + lb_w // 2
rcx = rb_x + rb_w // 2

L('<!-- LEFT DECKS -->')
deck_box(lb_x, EH_Y,              lb_w, deck_h,        lcx, "主牌堆", "MAIN DECK", "30", "#c89b3c", "#081420", "#c89b3c")
deck_box(lb_x, EH_Y+deck_h+4,    lb_w, deck_h,        lcx, "符文堆", "RUNE DECK", "10", "#7050c0", "#080e20", "#8060d0", "#b090f0")
deck_box(lpb_x, PB_Y,              lpb_w, player_deck_h, lcx, "符文堆", "RUNE DECK", "10", "#7050c0", "#080e20", "#8060d0", "#b090f0")
deck_box(lpb_x, PB_Y+player_deck_h+4, lpb_w, player_deck_h, lcx, "主牌堆", "MAIN DECK", "30", "#c89b3c", "#081420", "#c89b3c")
L()
L('<!-- RIGHT DECKS -->')
deck_box(rb_x, EH_Y,              rb_w, deck_h,        rcx, "弃牌",   "DISCARD",   "0",  "#c89b3c", "#081420", "#c89b3c")
deck_box(rb_x, EH_Y+deck_h+4,    rb_w, deck_h,        rcx, "放逐区", "EXILE",      "0",  "#c89b3c", "#0a1420", "#c89b3c")
deck_box(rpb_x, PB_Y,              rpb_w, player_deck_h, rcx, "放逐区", "EXILE",      "0",  "#c89b3c", "#0a1420", "#c89b3c")
deck_box(rpb_x, PB_Y+player_deck_h+4, rpb_w, player_deck_h, rcx, "弃牌", "DISCARD",  "0",  "#c89b3c", "#081420", "#c89b3c")
L()

L(f'<!-- ENEMY HERO ROW y={EH_Y} h={EH_H} -->')
hcy = EH_Y + EH_H // 2
for hx, lbl, eng, sc in [(MAIN_X+4,"英雄","CHAMPION","#d4a828"),(MAIN_X+92,"传说","LEGEND","#c89b3c")]:
    L(f'<rect x="{hx}" y="{EH_Y+4}" width="76" height="{EH_H-8}" fill="#101828" stroke="{sc}" stroke-width="2" rx="5" filter="url(#glow)"/>')
    L(f'<rect x="{hx+4}" y="{EH_Y+8}" width="68" height="{EH_H-16}" fill="#080e1e" stroke="{sc}60" stroke-width="1" rx="3" stroke-dasharray="3,2"/>')
    L(f'<text x="{hx+38}" y="{hcy-4}" fill="{sc}" font-size="11" font-family="Arial" font-weight="bold" text-anchor="middle">{lbl}</text>')
    L(f'<text x="{hx+38}" y="{hcy+8}" fill="{sc}90" font-size="8" font-family="Arial" text-anchor="middle">{eng}</text>')
L(f'<rect x="{MAIN_X+96}" y="{EH_Y+EH_H-14}" width="68" height="10" fill="#201008" stroke="#c89b3c" stroke-width="1" rx="3"/>')
L(f'<text x="{MAIN_X+130}" y="{EH_Y+EH_H-6}" fill="#c89b3c" font-size="7" text-anchor="middle" font-family="Arial">技 能</text>')
L()

L(f'<!-- ENEMY HAND FAN -->')
L(f'<text x="960" y="22" fill="#c89b3c" font-size="9" text-anchor="middle" font-family="Arial" opacity="0.45" letter-spacing="2">HAND · 敌方手牌</text>')
for ang, op, sw in zip([14,-14,9,-9,5,-5,0],["45","45","58","58","72","72","ee"],["1.2","1.2","1.3","1.3","1.4","1.4","1.6"]):
    lx1, ly1 = HAND_CARD_X+14, ENEMY_CARD_Y+14
    lx2, ly2 = HAND_CARD_X+HAND_W-14, ENEMY_CARD_Y+HAND_H-14
    L(f'<g transform="rotate({ang}, 960, {ENEMY_PIVOT})">')
    L(f'  <rect x="{HAND_CARD_X}" y="{ENEMY_CARD_Y}" width="{HAND_W}" height="{HAND_H}" fill="#0c1624" stroke="#c89b3c{op}" stroke-width="{sw}" rx="4"/>')
    L(f'  <line x1="{lx1}" y1="{ly1}" x2="{lx2}" y2="{ly2}" stroke="#c89b3c18" stroke-width="1"/>')
    L(f'  <line x1="{lx2}" y1="{ly1}" x2="{lx1}" y2="{ly2}" stroke="#c89b3c18" stroke-width="1"/>')
    L('</g>')
L()

L(f'<!-- ENEMY RUNES y={ER_Y} h={ER_H} -->')
L(f'<rect x="{MAIN_X}" y="{ER_Y}" width="{MAIN_W}" height="{ER_H}" fill="#0c1030" rx="4"/>')
L(f'<rect x="{MAIN_X}" y="{ER_Y}" width="{MAIN_W}" height="{ER_H}" fill="none" stroke="url(#purH)" stroke-width="2" rx="4"/>')
L(f'<text x="{MAIN_X+10}" y="{ER_Y+16}" fill="#9070e0" font-size="11" font-family="Arial" font-weight="bold" letter-spacing="2" filter="url(#glow)">RUNES  符文区</text>')
er_cy = ER_Y + ER_H // 2
for i in range(RUNE_COUNT):
    cx = RUNE_START_CX + i * RUNE_STEP
    if i < 8:
        L(f'<circle cx="{cx}" cy="{er_cy}" r="{RUNE_R}" fill="#0c0e22" stroke="#7050c0" stroke-width="1.5"/>')
    else:
        L(f'<circle cx="{cx}" cy="{er_cy}" r="{RUNE_R}" fill="#0a0c1c" stroke="#403080" stroke-width="1" stroke-dasharray="4,2" opacity="0.5"/>')
L()

L(f'<!-- ENEMY BASE y={EB_Y} h={EB_H} -->')
L(f'<rect x="{MAIN_X}" y="{EB_Y}" width="{MAIN_W}" height="{EB_H}" fill="#0e1c28" rx="4"/>')
L(f'<rect x="{MAIN_X}" y="{EB_Y}" width="{MAIN_W}" height="{EB_H}" fill="none" stroke="url(#goldH)" stroke-width="2" rx="4"/>')
L(f'<text x="{MAIN_X+10}" y="{EB_Y+16}" fill="#c89b3c" font-size="11" font-family="Arial" font-weight="bold" letter-spacing="2" filter="url(#glow)">BASE  基地</text>')
bc_h = int(BASE_CARD_W * CARD_RATIO)  # = 100, 等比例标准卡牌比例
bc_y = EB_Y + (EB_H - bc_h) // 2  # 垂直居中
for i in range(7):
    bx = BASE_START_X + i*(BASE_CARD_W+BASE_CARD_GAP)
    L(f'<rect x="{bx}" y="{bc_y}" width="{BASE_CARD_W}" height="{bc_h}" fill="#0a1620" stroke="#c89b3c" stroke-width="1" rx="3" stroke-dasharray="4,2"/>')
bx = BASE_START_X + 7*(BASE_CARD_W+BASE_CARD_GAP)
L(f'<rect x="{bx}" y="{bc_y}" width="{BASE_CARD_W}" height="{bc_h}" fill="#0a1028" stroke="#4060a0" stroke-width="1.5" rx="3" stroke-dasharray="3,2"/>')
L(f'<text x="{bx+BASE_CARD_W//2}" y="{bc_y+bc_h//2+4}" fill="#4060a0" font-size="9" text-anchor="middle" font-family="Arial" opacity="0.8">待命</text>')
L()

L(f'<!-- BATTLEFIELD y={BF_Y} h={BF_H} center={BF_CENTER} -->')
# 战场延伸到侧栏下面（与原v2一致）
L(f'<rect x="48" y="{BF_Y}" width="896" height="{BF_H}" fill="url(#bf0Grad)" rx="6"/>')
L(f'<rect x="48" y="{BF_Y}" width="896" height="{BF_H}" fill="none" stroke="#4080b0" stroke-width="2.5" rx="6" filter="url(#glow)"/>')
L(f'<rect x="976" y="{BF_Y}" width="894" height="{BF_H}" fill="url(#bf1Grad)" rx="6"/>')
L(f'<rect x="976" y="{BF_Y}" width="894" height="{BF_H}" fill="none" stroke="#3070a0" stroke-width="2.5" rx="6" filter="url(#glow)"/>')

for slot_cx, label, stroke in [(889,"战场 ①","#4080b0"),(1031,"战场 ②","#3070a0")]:
    L(f'<text x="{slot_cx}" y="{BF_Y+28}" fill="{stroke}" font-size="13" text-anchor="middle" font-family="Arial" font-weight="bold" filter="url(#softglow)">{label}</text>')
    L(f'<rect x="{slot_cx-36}" y="{BF_Y+34}" width="72" height="52" fill="#0c1828" stroke="#c89b3c" stroke-width="1.5" rx="3"/>')

STDBY_W = 72
STDBY_H = int(STDBY_W * CARD_RATIO)  # = 100, 标准卡牌比例
sb_y = BF_CENTER + 18
for slot_cx in [889, 1031]:
    L(f'<rect x="{slot_cx-STDBY_W//2}" y="{sb_y}" width="{STDBY_W}" height="{STDBY_H}" fill="#0a0e1c" stroke="#4060a0" stroke-width="1.5" rx="3"/>')
    L(f'<rect x="{slot_cx-STDBY_W//2+4}" y="{sb_y+4}" width="{STDBY_W-8}" height="{STDBY_H-8}" fill="none" stroke="#4060a060" stroke-width="1" rx="2" stroke-dasharray="3,2"/>')
    L(f'<text x="{slot_cx}" y="{sb_y+STDBY_H//2+4}" fill="#4060a0" font-size="10" text-anchor="middle" font-family="Arial" font-weight="bold">待命区</text>')

cd_y = BF_CENTER + 4
L(f'<line x1="56" y1="{cd_y}" x2="830" y2="{cd_y}" stroke="#2a5888" stroke-width="1.5" stroke-dasharray="20,8" opacity="0.7"/>')
L(f'<line x1="1090" y1="{cd_y}" x2="1862" y2="{cd_y}" stroke="#2a5888" stroke-width="1.5" stroke-dasharray="20,8" opacity="0.7"/>')

BF_SLOT_W = 76
BF_SLOT_H = int(BF_SLOT_W * CARD_RATIO)  # = 106, 标准卡牌比例
ec_y = BF_Y + (BF_H // 2 - BF_SLOT_H) // 2
pc_y = BF_CENTER + (BF_H // 2 - BF_SLOT_H) // 2
for i in range(8):
    cx = 94 + i * 92   # 原始卡槽x起点
    L(f'<rect x="{cx}" y="{ec_y}" width="{BF_SLOT_W}" height="{BF_SLOT_H}" fill="#091828" stroke="#3a6090" stroke-width="1" rx="3" stroke-dasharray="4,2"/>')
    L(f'<rect x="{cx}" y="{pc_y}" width="{BF_SLOT_W}" height="{BF_SLOT_H}" fill="#0a1c2c" stroke="#4090b0" stroke-width="1" rx="3" stroke-dasharray="4,2"/>')
for i in range(8):
    cx = 988 + i * 92
    L(f'<rect x="{cx}" y="{ec_y}" width="{BF_SLOT_W}" height="{BF_SLOT_H}" fill="#091828" stroke="#3a6090" stroke-width="1" rx="3" stroke-dasharray="4,2"/>')
    L(f'<rect x="{cx}" y="{pc_y}" width="{BF_SLOT_W}" height="{BF_SLOT_H}" fill="#0a1c2c" stroke="#4090b0" stroke-width="1" rx="3" stroke-dasharray="4,2"/>')
L()

L(f'<rect x="944" y="{BF_Y}" width="32" height="{BF_H}" fill="#030810"/>')
L(f'<polygon points="952,{BF_Y} 968,{BF_Y} 976,{BF_Y+8} 960,{BF_Y+16} 944,{BF_Y+8}" fill="#120c20" stroke="url(#pillarBorder)" stroke-width="1"/>')
L(f'<polygon points="952,{BF_Y+BF_H} 968,{BF_Y+BF_H} 976,{BF_Y+BF_H-8} 960,{BF_Y+BF_H-16} 944,{BF_Y+BF_H-8}" fill="#120c20" stroke="url(#pillarBorder)" stroke-width="1"/>')
L(f'<rect x="944" y="{BF_Y+8}" width="32" height="{BF_H-16}" fill="none" stroke="url(#pillarBorder)" stroke-width="1.5"/>')
fill_y = BF_CENTER - 60
L(f'<rect x="948" y="{fill_y}" width="24" height="120" fill="url(#pillarFill)" clip-path="url(#pillarClip)" opacity="0.8" rx="2"/>')
L(f'<rect x="944" y="{fill_y-3}" width="32" height="5" fill="#5090ff" opacity="0.7" rx="1" filter="url(#glow)"/>')
L(f'<text x="960" y="{BF_Y+26}" fill="#c89b3c" font-size="9" text-anchor="middle" font-family="Arial" opacity="0.5">+</text>')
L(f'<text x="960" y="{BF_CENTER+5}" fill="#7090c0" font-size="7" text-anchor="middle" font-family="Arial" opacity="0.6">o</text>')
L(f'<text x="960" y="{BF_Y-5}" fill="#c84040" font-size="6.5" text-anchor="middle" font-family="Arial" opacity="0.7" letter-spacing="1">对方 下</text>')
L(f'<text x="960" y="{BF_Y+BF_H+10}" fill="#40c070" font-size="6.5" text-anchor="middle" font-family="Arial" opacity="0.7" letter-spacing="1">我方 上</text>')
L()

L(f'<!-- PLAYER BASE y={PB_Y} h={PB_H} -->')
L(f'<rect x="{MAIN_X}" y="{PB_Y}" width="{MAIN_W}" height="{PB_H}" fill="#0e1c28" rx="4"/>')
L(f'<rect x="{MAIN_X}" y="{PB_Y}" width="{MAIN_W}" height="{PB_H}" fill="none" stroke="url(#goldH)" stroke-width="2" rx="4"/>')
L(f'<text x="{MAIN_X+10}" y="{PB_Y+16}" fill="#c89b3c" font-size="11" font-family="Arial" font-weight="bold" letter-spacing="2" filter="url(#glow)">BASE  基地</text>')
pb_y = PB_Y + (PB_H - bc_h) // 2  # 垂直居中
for i in range(7):
    bx = BASE_START_X + i*(BASE_CARD_W+BASE_CARD_GAP)
    L(f'<rect x="{bx}" y="{pb_y}" width="{BASE_CARD_W}" height="{bc_h}" fill="#0a1620" stroke="#c89b3c" stroke-width="1" rx="3" stroke-dasharray="4,2"/>')
bx = BASE_START_X + 7*(BASE_CARD_W+BASE_CARD_GAP)
L(f'<rect x="{bx}" y="{pb_y}" width="{BASE_CARD_W}" height="{bc_h}" fill="#0a1028" stroke="#4060a0" stroke-width="1.5" rx="3" stroke-dasharray="3,2"/>')
L(f'<text x="{bx+BASE_CARD_W//2}" y="{pb_y+bc_h//2+4}" fill="#4060a0" font-size="9" text-anchor="middle" font-family="Arial" opacity="0.8">待命</text>')
L()

L(f'<!-- PLAYER RUNES y={PR_Y} h={PR_H} -->')
L(f'<rect x="{MAIN_X}" y="{PR_Y}" width="{MAIN_W}" height="{PR_H}" fill="#0c1030" rx="4"/>')
L(f'<rect x="{MAIN_X}" y="{PR_Y}" width="{MAIN_W}" height="{PR_H}" fill="none" stroke="url(#purH)" stroke-width="2" rx="4"/>')
L(f'<text x="{MAIN_X+10}" y="{PR_Y+16}" fill="#9070e0" font-size="11" font-family="Arial" font-weight="bold" letter-spacing="2" filter="url(#glow)">RUNES  符文区</text>')
pr_cy = PR_Y + PR_H // 2
for i in range(RUNE_COUNT):
    cx = RUNE_START_CX + i * RUNE_STEP
    if i < 8:
        L(f'<circle cx="{cx}" cy="{pr_cy}" r="{RUNE_R}" fill="#0c0e22" stroke="#7050c0" stroke-width="1.5"/>')
    else:
        L(f'<circle cx="{cx}" cy="{pr_cy}" r="{RUNE_R}" fill="#0a0c1c" stroke="#403080" stroke-width="1" stroke-dasharray="4,2" opacity="0.5"/>')
L()

L(f'<!-- PLAYER HERO ROW y={PH_Y} h={PH_H} -->')
pcy = PH_Y + PH_H // 2
for hx, lbl, eng, sc in [(MAIN_X+4,"英雄","CHAMPION","#d4a828"),(MAIN_X+92,"传说","LEGEND","#c89b3c")]:
    L(f'<rect x="{hx}" y="{PH_Y+4}" width="76" height="{PH_H-8}" fill="#101828" stroke="{sc}" stroke-width="2" rx="5" filter="url(#glow)"/>')
    L(f'<rect x="{hx+4}" y="{PH_Y+8}" width="68" height="{PH_H-16}" fill="#080e1e" stroke="{sc}60" stroke-width="1" rx="3" stroke-dasharray="3,2"/>')
    L(f'<text x="{hx+38}" y="{pcy-4}" fill="{sc}" font-size="11" font-family="Arial" font-weight="bold" text-anchor="middle">{lbl}</text>')
    L(f'<text x="{hx+38}" y="{pcy+8}" fill="{sc}90" font-size="8" font-family="Arial" text-anchor="middle">{eng}</text>')
L(f'<rect x="{MAIN_X+96}" y="{PH_Y+PH_H-14}" width="68" height="10" fill="#201008" stroke="#c89b3c" stroke-width="1" rx="3"/>')
L(f'<text x="{MAIN_X+130}" y="{PH_Y+PH_H-6}" fill="#c89b3c" font-size="7" text-anchor="middle" font-family="Arial">技 能</text>')

btn_y = PH_Y + 4
L(f'<rect x="1580" y="{btn_y}" width="108" height="34" fill="#0c2240" stroke="#4090d0" stroke-width="2" rx="5" filter="url(#glow)"/>')
L(f'<text x="1634" y="{btn_y+14}" fill="#60b0f0" font-size="13" text-anchor="middle" font-family="Arial" font-weight="bold" filter="url(#glow)">结束回合</text>')
L(f'<text x="1634" y="{btn_y+26}" fill="#4090d070" font-size="7" text-anchor="middle" font-family="Arial" letter-spacing="2">END TURN</text>')
L(f'<rect x="1584" y="{btn_y+42}" width="100" height="26" fill="#080e1c" stroke="#2a5888" stroke-width="1.2" rx="4"/>')
L(f'<text x="1634" y="{btn_y+57}" fill="#3878b0" font-size="10" text-anchor="middle" font-family="Arial" font-weight="bold">召回单位</text>')
L(f'<rect x="1580" y="{btn_y+76}" width="108" height="26" fill="#080e1c" stroke="#2a5888" stroke-width="1.2" rx="4"/>')
L(f'<text x="1634" y="{btn_y+91}" fill="#3878b0" font-size="10" text-anchor="middle" font-family="Arial" font-weight="bold">查看弃牌堆</text>')
L()

L(f'<!-- PLAYER HAND FAN -->')
L(f'<text x="960" y="{BTN_Y-4}" fill="#c89b3c" font-size="9" text-anchor="middle" font-family="Arial" opacity="0.45" letter-spacing="2">HAND · 我方手牌</text>')
for ang, op, sw in zip([-14,-9,-5,0,5,9,14],["45","58","72","ee","72","58","45"],["1.2","1.3","1.4","1.6","1.4","1.3","1.2"]):
    lx1, ly1 = HAND_CARD_X+14, PLAYER_CARD_Y+14
    lx2, ly2 = HAND_CARD_X+HAND_W-14, PLAYER_CARD_Y+HAND_H-14
    L(f'<g transform="rotate({ang}, 960, {PLAYER_PIVOT})">')
    L(f'  <rect x="{HAND_CARD_X}" y="{PLAYER_CARD_Y}" width="{HAND_W}" height="{HAND_H}" fill="#0c1624" stroke="#c89b3c{op}" stroke-width="{sw}" rx="4"/>')
    L(f'  <line x1="{lx1}" y1="{ly1}" x2="{lx2}" y2="{ly2}" stroke="#c89b3c18" stroke-width="1"/>')
    L(f'  <line x1="{lx2}" y1="{ly1}" x2="{lx1}" y2="{ly2}" stroke="#c89b3c18" stroke-width="1"/>')
    L('</g>')
L()

L(f'<!-- BUTTON STRIP -->')
L(f'<rect x="{MAIN_X}" y="{BTN_Y}" width="{MAIN_W}" height="{BTN_H}" fill="#060e18" stroke="#304860" stroke-width="1" rx="2"/>')
for tx, txt in [(550,"法力: 3/3"),(730,"符能: 2/2"),(960,"阶段: 行动"),(1190,"回合: 第 3 回合"),(1380,"先手: 玩家")]:
    L(f'<text x="{tx}" y="{BTN_Y+13}" fill="#506878" font-size="8" font-family="Arial" text-anchor="middle">{txt}</text>')
L()

L(f'<text x="960" y="1079" fill="#c89b3c" font-size="6" text-anchor="middle" font-family="Arial" opacity="0.2">FWTCG v2.3 | 1920x1080 | 英雄100+符文130+基地160+战场260+按钮18 | 手牌110x170 | 牌堆100px贴边</text>')
L('</svg>')

import os
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "layout_v2.svg")
with open(out, "w", encoding="utf-8") as f:
    f.write("\n".join(lines))
print(f"Written {len(lines)} lines -> {out}")
