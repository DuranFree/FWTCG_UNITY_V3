# FWTCG UI 贴图需求清单

> 生成规则：所有贴图必须是 **纯平面 2D**，正俯视，无透视，无 3D 光影。
> 装饰/按钮/图标类必须 **透明背景 PNG**。
> 面板/区域类如果需要半透明效果，也用透明背景 PNG。

---

## 通用 Prompt 前缀（每条都要加）

```
flat 2D game UI texture, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth,
```

透明背景元素额外加：
```
transparent background, PNG alpha channel,
```

---

## 1. 全屏背景

| # | 元素名 | UI路径 | 尺寸 | Prompt |
|---|--------|--------|------|--------|
| 1.1 | Background | Canvas/Background | 1920x1080 | `flat 2D game UI texture, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, dark fantasy card game playmat surface, deep navy blue fabric texture with subtle arcane rune watermarks, golden thin grid lines dividing card zones, muted mystical glow spots, uniform flat lighting, 1920x1080, game background sprite` |

---

## 2. 棋盘区域

| # | 元素名 | UI路径 | 尺寸 | 数量 | Prompt |
|---|--------|--------|------|------|--------|
| 2.1 | Base区域 | Canvas/.../EnemyBase, PlayerBase | 670x103 | x2 | `flat 2D game UI texture, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, horizontal rectangular panel, dark indigo semi-transparent bar, thin golden border line top and bottom, subtle inner glow, card zone indicator strip, clean edges, 670x103, sprite asset` |
| 2.2 | HeroZone | Canvas/.../EnemyHeroZone, PlayerHeroZone | 120x198 | x2 | `flat 2D game UI texture, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, vertical card slot frame, dark obsidian rectangle, thin ornate golden border, faint blue rune glow at center, single card placeholder, transparent background, PNG alpha channel, 120x198, sprite asset` |
| 2.3 | LegendZone | Canvas/.../EnemyLegendZone, PlayerLegendZone | 120x198 | x2 | `flat 2D game UI texture, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, vertical card slot frame, dark obsidian rectangle, thin ornate golden border with gold accent, faint golden glow at center, legend card placeholder, transparent background, PNG alpha channel, 120x198, sprite asset` |
| 2.4 | RuneZone | Canvas/.../EnemyRunes, PlayerRunes | 670x95 | x2 | `flat 2D game UI texture, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, horizontal mana resource bar, dark panel with faint teal energy veins pattern, thin border, semi-transparent, card game resource zone, clean edges, 670x95, sprite asset` |
| 2.5 | MainPile | Canvas/.../EnemyMainPile, PlayerMainPile | 223x95 | x2 | `flat 2D game UI texture, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, small rectangular deck zone, dark slate with faint stack line pattern, minimalist, thin border, semi-transparent, 223x95, sprite asset` |
| 2.6 | RunePile | Canvas/.../EnemyRunePile, PlayerRunePile | 223x95 | x2 | `flat 2D game UI texture, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, small rectangular deck zone, dark slate with faint teal crystal pattern, minimalist, thin border, semi-transparent, 223x95, sprite asset` |
| 2.7 | DiscardExile | Canvas/.../EnemyDiscardExile, PlayerDiscardExile | 447x103 | x2 | `flat 2D game UI texture, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, horizontal split panel, left half faint red tint right half faint purple tint, dark base, thin divider line center, graveyard and exile zone, semi-transparent, 447x103, sprite asset` |
| 2.8 | HandZone(玩家) | Canvas/PlayerHandZone | 1720x120 | x1 | `flat 2D game UI texture, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, wide horizontal card tray, dark gradient bar, subtle golden glow line at top edge, semi-transparent, card hand holding area, seamless edges, 1720x120, sprite asset` |
| 2.9 | HandZone(敌方) | Canvas/EnemyHandZone | 1720x50 | x1 | `flat 2D game UI texture, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, narrow horizontal bar, very dark translucent strip, subtle border, enemy hand zone indicator, seamless edges, 1720x50, sprite asset` |
| 2.10 | StandbyZone | Canvas/.../StandbyZone | 780x100 | x2 | `flat 2D game UI texture, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, horizontal field zone, very dark translucent panel, faint dotted grid pattern, subtle blue edge glow, standby area, 780x100, sprite asset` |

---

## 3. 按钮

| # | 元素名 | UI路径 | 尺寸 | 当前色 | Prompt |
|---|--------|--------|------|--------|--------|
| 3.1 | EndTurnButton | Canvas/.../EndTurnButton | 100x42 | 白 | `flat 2D game UI button, top-down orthographic view, no perspective, no 3D lighting, horizontal pill-shaped golden button, ornate fantasy frame, warm metallic sheen, clean edges, transparent background, PNG alpha channel, 100x42, sprite asset` |
| 3.2 | ConfirmRunesBtn | Canvas/.../ConfirmRunesBtn | 100x42 | 金 | `flat 2D game UI button, top-down orthographic view, no perspective, no 3D lighting, golden confirm button, checkmark icon accent, fantasy RPG style, metallic gold, transparent background, PNG alpha channel, 100x42, sprite asset` |
| 3.3 | CancelRunesBtn | Canvas/.../CancelRunesBtn | 100x42 | 红 | `flat 2D game UI button, top-down orthographic view, no perspective, no 3D lighting, dark red cancel button, X mark accent, fantasy RPG style, transparent background, PNG alpha channel, 100x42, sprite asset` |
| 3.4 | ReactButton | Canvas/.../ReactButton | 100x42 | 橙 | `flat 2D game UI button, top-down orthographic view, no perspective, no 3D lighting, orange reaction button, lightning bolt accent, urgent glow, fantasy card game, transparent background, PNG alpha channel, 100x42, sprite asset` |
| 3.5 | SkipReactionBtn | Canvas/.../SkipReactionBtn | 100x42 | 灰 | `flat 2D game UI button, top-down orthographic view, no perspective, no 3D lighting, muted grey skip button, right arrow accent, understated fantasy style, transparent background, PNG alpha channel, 100x42, sprite asset` |
| 3.6 | TapAllRunesBtn | Canvas/.../TapAllRunesBtn | 100x42 | 灰 | `flat 2D game UI button, top-down orthographic view, no perspective, no 3D lighting, grey-blue utility button, tap all icon, fantasy card game style, transparent background, PNG alpha channel, 100x42, sprite asset` |
| 3.7 | 圆形按钮(通用) | RestartButton, OkButton, ConfirmButton 等 | 100x100 | 蓝 | `flat 2D game UI button, top-down orthographic view, no perspective, no 3D lighting, circular button, blue gem center, golden ring border, fantasy RPG icon button, transparent background, PNG alpha channel, 100x100, sprite asset` |
| 3.8 | LogToggleBtn | Canvas/LogToggleBtn | 28x296 | 紫 | `flat 2D game UI button, top-down orthographic view, no perspective, no 3D lighting, tall narrow vertical tab button, dark purple, thin golden edge, log panel toggle handle, transparent background, PNG alpha channel, 28x296, sprite asset` |
| 3.9 | ViewerClose | Canvas/.../ViewerClose | 192x86 | 红 | `flat 2D game UI button, top-down orthographic view, no perspective, no 3D lighting, horizontal close button, dark red, X icon center, fantasy style, transparent background, PNG alpha channel, 192x86, sprite asset` |
| 3.10 | CancelBtn(宽) | Canvas/.../SpellTargetPopup/.../CancelBtn | 496x34 | 灰 | `flat 2D game UI button, top-down orthographic view, no perspective, no 3D lighting, wide flat cancel bar, dark grey, subtle border, minimalist fantasy style, transparent background, PNG alpha channel, 496x34, sprite asset` |

---

## 4. 面板/弹窗

| # | 元素名 | UI路径 | 尺寸 | Prompt |
|---|--------|--------|------|--------|
| 4.1 | BannerPanel | Canvas/BannerPanel | 1152x216 | `flat 2D game UI panel, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, wide announcement banner, dark navy rectangle, ornate golden border frame top and bottom, subtle inner glow, phase announcement display, clean edges, 1152x216, sprite asset` |
| 4.2 | CombatResultPanel | Canvas/CombatResultPanel | 1152x324 | `flat 2D game UI panel, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, wide result display frame, dark navy with faint crossed swords watermark at center, golden corner decorations, battle report panel, 1152x324, sprite asset` |
| 4.3 | DialogBox | Canvas/AskPromptPanel/DialogBox | 720x420 | `flat 2D game UI panel, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, rectangular dialog window, dark blue-black background, ornate golden frame border, rounded corners, fantasy card game popup, 720x420, sprite asset` |
| 4.4 | PopupBox | Canvas/SpellTargetPopup/PopupBox | 520x160 | `flat 2D game UI panel, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, small notification popup, dark panel, thin golden border, fantasy style, 520x160, sprite asset` |
| 4.5 | ToastPanel | Canvas/ToastPanel | 1152x86 | `flat 2D game UI panel, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, thin horizontal notification bar, dark translucent, subtle blue edge glow, minimalist, clean edges, 1152x86, sprite asset` |
| 4.6 | MessagePanel | Canvas/MessagePanel | 200x880 | `flat 2D game UI panel, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, tall vertical log panel, dark background, thin golden side borders, game message list area, seamless edges, 200x880, sprite asset` |
| 4.7 | CardPanel | Canvas/SpellShowcasePanel/CardPanel | 384x302 | `flat 2D game UI panel, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, card display frame, dark blue-black with thin golden border, card showcase window, 384x302, sprite asset` |
| 4.8 | GroupPanel | Canvas/SpellShowcasePanel/GroupPanel | 1536x432 | `flat 2D game UI panel, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, wide group display area, dark panel with subtle grid, thin golden border, card group showcase, 1536x432, sprite asset` |
| 4.9 | DetailPanel | Canvas/CardDetailPopup/DetailPanel | 700x500 | `flat 2D game UI panel, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, card detail popup frame, dark navy background, ornate golden border, card information display, 700x500, sprite asset` |
| 4.10 | TopBar | Canvas/TopBar | 1920x36 | `flat 2D game UI texture, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, thin horizontal top bar, dark navy, subtle golden line at bottom edge, game status bar, seamless edges, 1920x36, sprite asset` |
| 4.11 | PlayerInfoStrip | Canvas/.../PlayerInfoStrip | 1720x32 | `flat 2D game UI texture, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, thin horizontal info strip, dark navy, subtle border, player status bar, seamless edges, 1720x32, sprite asset` |
| 4.12 | EventBannerPanel | Canvas/EventBannerPanel | 200x36 | `flat 2D game UI panel, top-down orthographic view, no perspective, no 3D lighting, no shadows, no depth, small event notification bar, dark navy, thin border, compact banner, 200x36, sprite asset` |

---

## 5. 装饰元素

| # | 元素名 | UI路径 | 尺寸 | Prompt |
|---|--------|--------|------|--------|
| 5.1 | SpinOuter | Canvas/DecorLayer/SpinOuter | 180x180 | `flat 2D game UI decoration, top-down orthographic view, no perspective, no 3D lighting, circular magic circle outer ring, blue arcane rune glyphs, glowing lines, transparent background, PNG alpha channel, 180x180, sprite asset` |
| 5.2 | SpinInner | Canvas/DecorLayer/SpinInner | 120x120 | `flat 2D game UI decoration, top-down orthographic view, no perspective, no 3D lighting, inner magic circle, teal energy core, spinning glyph pattern, transparent background, PNG alpha channel, 120x120, sprite asset` |
| 5.3 | SigilOuter | Canvas/DecorLayer/SigilOuter | 280x280 | `flat 2D game UI decoration, top-down orthographic view, no perspective, no 3D lighting, large arcane sigil circle, golden faded ancient runes, transparent background, PNG alpha channel, 280x280, sprite asset` |
| 5.4 | SigilInner | Canvas/DecorLayer/SigilInner | 190x190 | `flat 2D game UI decoration, top-down orthographic view, no perspective, no 3D lighting, inner sigil core, ivory cream glow, mystic symbol, transparent background, PNG alpha channel, 190x190, sprite asset` |
| 5.5 | CornerGem | Canvas/DecorLayer/CornerGem0-3 | 48x48 | `flat 2D game UI icon, top-down orthographic view, no perspective, no 3D lighting, small golden diamond gem, fantasy UI corner decoration, transparent background, PNG alpha channel, 48x48, sprite asset` |
| 5.6 | DividerOrb | Canvas/DecorLayer/DividerOrb | 18x18 | `flat 2D game UI icon, top-down orthographic view, no perspective, no 3D lighting, tiny blue glowing energy orb, UI divider dot, transparent background, PNG alpha channel, 18x18, sprite asset` |
| 5.7 | CoinCircle | Canvas/CoinFlipPanel/.../CoinCircle | 160x160 | `flat 2D game UI token, top-down orthographic view, no perspective, no 3D lighting, golden coin face, dragon emblem center, ornate rim, coin flip token, transparent background, PNG alpha channel, 160x160, sprite asset` |
| 5.8 | ScanLight | Canvas/CoinFlipPanel/ScanLight | 240x3 | `flat 2D game UI effect, top-down orthographic view, no perspective, no 3D lighting, thin horizontal light beam, blue-white glow, scan line effect, transparent background, PNG alpha channel, 240x3, sprite asset` |
| 5.9 | TitleBeam | Canvas/CoinFlipPanel/TitleBeam | 1920x60 | `flat 2D game UI effect, top-down orthographic view, no perspective, no 3D lighting, wide horizontal light beam, blue-cyan glow, title accent bar, fading edges, transparent background, PNG alpha channel, 1920x60, sprite asset` |
| 5.10 | BgGradientOverlay | Canvas/CoinFlipPanel/BgGradientOverlay | 1500x1500 | `flat 2D game UI texture, top-down orthographic view, no perspective, no 3D lighting, large radial gradient, teal to transparent, subtle circular glow overlay, transparent background, PNG alpha channel, 1500x1500, sprite asset` |
| 5.11 | HexBreathOverlay | Canvas/CoinFlipPanel/HexBreathOverlay | 1920x1080 | `flat 2D game UI texture, top-down orthographic view, no perspective, no 3D lighting, full screen hexagonal pattern overlay, teal semi-transparent, breathing effect texture, geometric hex grid, transparent background, PNG alpha channel, 1920x1080, sprite asset` |

---

## 6. 计时器

| # | 元素名 | UI路径 | 尺寸 | Prompt |
|---|--------|--------|------|--------|
| 6.1 | TimerRing | Canvas/TimerDisplay/TimerRing | 66x66 | `flat 2D game UI element, top-down orthographic view, no perspective, no 3D lighting, circular timer ring, golden metallic, clock border frame, hollow center, transparent background, PNG alpha channel, 66x66, sprite asset` |
| 6.2 | TimerFill | Canvas/TimerDisplay/TimerFill | 62x62 | `flat 2D game UI element, top-down orthographic view, no perspective, no 3D lighting, solid circle, green energy fill, radial timer progress indicator, transparent background, PNG alpha channel, 62x62, sprite asset` |
| 6.3 | TimerInner | Canvas/TimerDisplay/TimerInner | 44x44 | `flat 2D game UI element, top-down orthographic view, no perspective, no 3D lighting, small dark circle, deep navy center, timer background, transparent background, PNG alpha channel, 44x44, sprite asset` |

---

## 7. 卡牌相关

| # | 元素名 | UI路径 | 尺寸 | 数量 | Prompt |
|---|--------|--------|------|------|--------|
| 7.1 | CardBack | Canvas/.../CardBack | 217x40 | x4 | `flat 2D game UI texture, top-down orthographic view, no perspective, no 3D lighting, horizontal card deck indicator strip, dark navy with subtle repeating pattern, card stack preview bar, 217x40, sprite asset` |
| 7.2 | ScorePip | Canvas/.../Score_0 ~ Score_8 | 73x28 | x18 | `flat 2D game UI element, top-down orthographic view, no perspective, no 3D lighting, small rounded rectangle score pip, golden metallic, level indicator, transparent background, PNG alpha channel, 73x28, sprite asset` |

---

## 生成后的文件组织

```
Assets/Textures/UI/
├── Background/
│   └── bg_playmat.png              (1.1)
├── Zones/
│   ├── zone_base.png               (2.1, 共用)
│   ├── zone_hero.png               (2.2)
│   ├── zone_legend.png             (2.3)
│   ├── zone_rune.png               (2.4)
│   ├── zone_main_pile.png          (2.5)
│   ├── zone_rune_pile.png          (2.6)
│   ├── zone_discard_exile.png      (2.7)
│   ├── zone_hand_player.png        (2.8)
│   ├── zone_hand_enemy.png         (2.9)
│   └── zone_standby.png            (2.10)
├── Buttons/
│   ├── btn_end_turn.png            (3.1)
│   ├── btn_confirm.png             (3.2)
│   ├── btn_cancel.png              (3.3)
│   ├── btn_react.png               (3.4)
│   ├── btn_skip.png                (3.5)
│   ├── btn_tap_all.png             (3.6)
│   ├── btn_circle_blue.png         (3.7, 共用)
│   ├── btn_log_toggle.png          (3.8)
│   ├── btn_close_red.png           (3.9)
│   └── btn_cancel_wide.png         (3.10)
├── Panels/
│   ├── panel_banner.png            (4.1)
│   ├── panel_combat_result.png     (4.2)
│   ├── panel_dialog.png            (4.3)
│   ├── panel_popup.png             (4.4)
│   ├── panel_toast.png             (4.5)
│   ├── panel_message_log.png       (4.6)
│   ├── panel_card_display.png      (4.7)
│   ├── panel_group_display.png     (4.8)
│   ├── panel_detail.png            (4.9)
│   ├── bar_top.png                 (4.10)
│   ├── bar_player_info.png         (4.11)
│   └── panel_event_banner.png      (4.12)
├── Decorations/
│   ├── decor_spin_outer.png        (5.1)
│   ├── decor_spin_inner.png        (5.2)
│   ├── decor_sigil_outer.png       (5.3)
│   ├── decor_sigil_inner.png       (5.4)
│   ├── decor_corner_gem.png        (5.5)
│   ├── decor_divider_orb.png       (5.6)
│   ├── decor_coin.png              (5.7)
│   ├── decor_scan_light.png        (5.8)
│   ├── decor_title_beam.png        (5.9)
│   ├── decor_gradient_overlay.png  (5.10)
│   └── decor_hex_overlay.png       (5.11)
├── Timer/
│   ├── timer_ring.png              (6.1)
│   ├── timer_fill.png              (6.2)
│   └── timer_inner.png             (6.3)
└── Cards/
    ├── card_back_strip.png         (7.1)
    └── score_pip.png               (7.2)
```

---

## 注意事项

1. **敌方和玩家共用同一张贴图**（镜像布局），不需要各生成一份
2. **所有圆形按钮共用一张** btn_circle_blue.png
3. **生成后告诉我文件路径**，我会用 MCP 自动赋给对应 UI 元素
4. **尺寸不需要严格匹配像素**，Unity Image 会自动拉伸，但比例要对
5. **9-slice 切片**：面板类（panel_dialog, panel_popup 等）建议生成时边框均匀，方便后续设置 Image Type = Sliced
