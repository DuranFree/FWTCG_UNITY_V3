"""
Sample the specific keyword TEXT color (not card theme) from each card art.
Targets the first keyword badge in the description area — usually a small colored
rectangle/bracket at the top-left of the text block.
"""
import os, sys, io, json
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
from PIL import Image
import numpy as np


# Cards per keyword (pick one representative card per keyword)
KW_CARDS = {
    '急速':   'kaisa_hero',        # 急速+征服
    '壁垒':   'yordel_instructor', # 壁垒
    '法盾':   'jax',               # 法盾
    '鼓舞':   'noxus_recruit',     # 鼓舞
    '征服':   'kaisa_hero',        # 征服 (use kaisa_hero, has both)
    '绝念':   'alert_sentinel',    # 绝念
    '反应':   'duel_stance',       # 反应
    '强攻':   'rengar',            # 反应+强攻
    '游走':   'yi_hero',           # 游走
    '预知':   'foresight_mech',    # 预知
    '待命':   'zhonya',            # 待命
    '回响':   'divine_ray',        # 回响
    '迅捷':   'guilty_pleasure',   # 迅捷
}


def find_badge_color(img_path):
    """Sample colored badge in the upper-description area (first keyword usually here)."""
    img = np.array(Image.open(img_path).convert('RGB'))
    h, w = img.shape[:2]
    # Description area — the keyword badge is usually at the upper-left of the text block
    # Try y=60%~75%, x=5%~45% (first keyword region)
    y0, y1 = int(h * 0.60), int(h * 0.78)
    x0, x1 = int(w * 0.05), int(w * 0.45)
    region = img[y0:y1, x0:x1]
    r = region[:, :, 0].astype(int)
    g = region[:, :, 1].astype(int)
    b = region[:, :, 2].astype(int)
    maxc = np.maximum(np.maximum(r, g), b)
    minc = np.minimum(np.minimum(r, g), b)
    sat = maxc - minc
    # High saturation, medium-high brightness (keyword text)
    mask = (sat >= 80) & (maxc >= 130) & (maxc <= 255)
    if mask.sum() < 20:
        return None
    # Cluster by hue (12 buckets, 30deg)
    buckets = {}
    rs, gs, bs = region[:, :, 0][mask], region[:, :, 1][mask], region[:, :, 2][mask]
    for pr, pg, pb in zip(rs, gs, bs):
        mx = max(pr, pg, pb); mn = min(pr, pg, pb); d = mx - mn
        if d == 0: continue
        if mx == pr:
            hue = (60 * (int(pg) - int(pb)) / d) % 360
        elif mx == pg:
            hue = 60 * (int(pb) - int(pr)) / d + 120
        else:
            hue = 60 * (int(pr) - int(pg)) / d + 240
        bucket = int(hue // 30)
        buckets.setdefault(bucket, []).append((int(pr), int(pg), int(pb)))
    if not buckets:
        return None
    best = max(buckets.values(), key=len)
    arr = np.array(best)
    mean = arr.mean(axis=0).astype(int)
    return int(mean[0]), int(mean[1]), int(mean[2])


print('关键词 → 采样自 card → 颜色')
results = {}
for kw, cid in KW_CARDS.items():
    path = f'Assets/Resources/CardArt/{cid}.png'
    if not os.path.exists(path):
        # try .jpg
        path = f'Assets/Resources/CardArt/{cid}.jpg'
        if not os.path.exists(path):
            print(f'{kw:4s} [{cid}]: art missing')
            continue
    c = find_badge_color(path)
    if c:
        r, g, b = c
        hex_c = f'#{r:02X}{g:02X}{b:02X}'
        results[kw] = hex_c
        print(f'{kw:4s} [{cid:20s}] → {hex_c} rgb{c}')
    else:
        print(f'{kw:4s} [{cid:20s}] → no color')

print()
print('C# dict literal:')
for kw, hex_c in results.items():
    print(f'            {{ CardKeyword.{kw}, "{hex_c}" }},  // {kw}')
