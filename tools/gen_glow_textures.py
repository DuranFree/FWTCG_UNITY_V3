"""
生成蓝色和红色宝石的预烘培 Glow 贴图。
原理：对原始宝石图应用重度高斯模糊，提取亮点并生成软边缘光晕。
用于 Unity UI 叠加层，模拟真实 Bloom 效果（无需后处理）。
"""
from PIL import Image, ImageFilter
import numpy as np

BASE = "E:/claudeCode/unity/FWTCG_UNITY_V3/Assets/Resources/UI/Generated"
BLUE_SRC = f"{BASE}/countdown_blue.png"
RED_SRC  = f"{BASE}/countdown_red.png"
BLUE_DST = f"{BASE}/countdown_glow_blue.png"
RED_DST  = f"{BASE}/countdown_glow_red.png"

def make_glow(src_path, dst_path, glow_color_rgb, glow_strength=2.5, blur_radius=22):
    """
    从原始宝石图生成预烘培 Glow 贴图。
    - src_path: 原始宝石 PNG（RGBA）
    - glow_color_rgb: glow 颜色 (r, g, b) 各 0-255
    - glow_strength: glow 强度倍率
    - blur_radius: 高斯模糊半径（越大 glow 扩散越远）
    """
    src = Image.open(src_path).convert("RGBA")
    arr = np.array(src, dtype=np.float32)

    R, G, B, A = arr[:,:,0], arr[:,:,1], arr[:,:,2], arr[:,:,3]

    # 提取亮度（宝石的亮部是 glow 的来源）
    lum = 0.299*R + 0.587*G + 0.114*B

    # 只对不透明像素（原始 alpha > 30）提取亮度
    mask = (A > 30).astype(np.float32)
    lum_masked = lum * mask

    # 归一化亮度 → 作为 glow 的强度分布
    lum_norm = np.clip(lum_masked / 255.0, 0, 1)

    # 对亮度做重度高斯模糊，生成散射光
    lum_img = Image.fromarray(np.clip(lum_norm * 255, 0, 255).astype(np.uint8), 'L')

    # 多层模糊叠加：紧密核心 + 宽散射
    blur_tight = np.array(lum_img.filter(ImageFilter.GaussianBlur(blur_radius * 0.4)), dtype=np.float32) / 255.0
    blur_wide  = np.array(lum_img.filter(ImageFilter.GaussianBlur(blur_radius)), dtype=np.float32) / 255.0
    blur_ultra = np.array(lum_img.filter(ImageFilter.GaussianBlur(blur_radius * 2.2)), dtype=np.float32) / 255.0

    # 合成多层 glow（紧密 + 中等 + 宽散射）
    glow_intensity = np.clip(
        blur_tight * 0.6 + blur_wide * 0.9 + blur_ultra * 0.5,
        0, 1
    ) * glow_strength
    glow_intensity = np.clip(glow_intensity, 0, 1)

    # 应用 glow 颜色
    gr, gg, gb = glow_color_rgb
    out_R = np.clip(glow_intensity * gr, 0, 255).astype(np.uint8)
    out_G = np.clip(glow_intensity * gg, 0, 255).astype(np.uint8)
    out_B = np.clip(glow_intensity * gb, 0, 255).astype(np.uint8)

    # Alpha 通道 = glow 强度（散射光区域可见）
    # 注意：原始宝石本体不需要在 glow 图中显示，只要周边散射光
    out_A = np.clip(glow_intensity * 255 * 0.85, 0, 255).astype(np.uint8)

    result = np.stack([out_R, out_G, out_B, out_A], axis=-1)
    out_img = Image.fromarray(result, 'RGBA')
    out_img.save(dst_path, 'PNG')
    print(f"OK: {dst_path}")

# 蓝色宝石 Glow：冷蓝/青色光晕
make_glow(
    BLUE_SRC, BLUE_DST,
    glow_color_rgb=(80, 160, 255),   # 蓝青色光
    glow_strength=2.8,
    blur_radius=24
)

# 红色宝石 Glow：橙红/火焰色光晕
make_glow(
    RED_SRC, RED_DST,
    glow_color_rgb=(255, 80, 20),    # 橙红色光
    glow_strength=2.8,
    blur_radius=24
)

print("全部 glow 贴图生成完毕")
