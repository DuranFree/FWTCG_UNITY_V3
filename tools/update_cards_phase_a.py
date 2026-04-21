#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Sync all CardData .asset files to match card image data.

Descriptions are VERBATIM from the card images in /ref/ksha and /ref/jiansheng.
"""
import os
import re
import sys

# RuneType: 0=Blazing/Fury, 1=Radiant/Mind, 2=Verdant/Calm, 3=Crushing/Body, 4=Chaos, 5=Order
# CardKeyword bitflags:
#   None=0, Haste=1, Barrier=2, SpellShield=4, Inspire=8, Conquest=16, Deathwish=32,
#   Reactive=64, StrongAtk=128, Roam=256, Foresight=512, Standby=1024, Stun=2048,
#   Echo=4096, Guard=8192, Ephemeral=16384, Swift=32768
# SpellTargetType: 0=None, 1=EnemyUnit, 2=FriendlyUnit, 3=AnyUnit

# (id, name, cost, atk, rune, rcost, desc, kw, effid,
#  isEq, eqAtk, eqRune, eqRcost, isSp, spTarget, isHero, secRune, secRcost)
CARDS = [
    # ─── Kaisa deck units (Fury + Mind) ───
    ("noxus_recruit", "诺克萨斯新兵", 4, 4, 0, 0,
     "军团 — 我的费用减少[2]。（如果你在本回合内已打出过其他卡牌，则发动此效果。）",
     8, "noxus_recruit_enter", 0, 0, 0, 0, 0, 0, 0, 0, 0),
    ("alert_sentinel", "警觉的哨兵", 2, 1, 1, 0,
     "绝念 — 抽一张牌。（当我被摧毁时，发动此效果。）",
     32, "alert_sentinel_die", 0, 0, 0, 0, 0, 0, 0, 0, 0),
    ("yordel_instructor", "约德尔教官", 3, 2, 1, 0,
     "壁垒（我在战斗中首先承担伤害。）当你打出我时，抽一张牌。",
     2, "yordel_instructor_enter", 0, 0, 0, 0, 0, 0, 0, 0, 0),
    ("bad_poro", "坏坏魄罗", 2, 2, 0, 0,
     "当我征服一处战场时，打出1枚休眠的「硬币」装备指示物。",
     16, "bad_poro_conquer", 0, 0, 0, 0, 0, 0, 0, 0, 0),
    ("rengar", "雷恩加尔", 3, 3, 0, 1,
     "反应。强攻（当我进攻时，+2。）若本回合开始前我在基地，则我可以以活跃状态进场。",
     192, "rengar_enter", 0, 0, 0, 0, 0, 0, 0, 0, 0),
    ("kaisa_hero", "卡莎·九死一生", 4, 4, 0, 0,
     "急速（你可以选择额外支付[1]和1炽烈符能，让我以活跃状态进场。）当我征服一处战场时，抽一张牌。",
     17, "kaisa_hero_conquer", 0, 0, 0, 0, 0, 0, 1, 0, 0),
    ("darius", "德莱厄斯", 5, 5, 0, 1,
     "每当你在本回合中打出第二张牌时，让我本回合+2，并让我变为活跃状态。",
     0, "darius_second_card", 0, 0, 0, 0, 0, 0, 0, 0, 0),
    ("thousand_tail", "千尾监视者", 7, 7, 1, 1,
     "急速（你可以选择额外支付[1]和1灵光符能，让我以活跃状态进场。）当你打出我时，让所有敌方单位本回合-3，不得低于1。",
     1, "thousand_tail_enter", 0, 0, 0, 0, 0, 0, 0, 0, 0),
    ("foresight_mech", "先见机甲", 2, 2, 1, 0,
     "你的「机械」属性单位获得【预知】。（当你打出我时，查看主牌堆顶的一张牌，你可以选择将其置底。）",
     512, "foresight_mech_enter", 0, 0, 0, 0, 0, 0, 0, 0, 0),

    # ─── Kaisa deck spells ───
    ("hex_ray", "海克斯射线", 1, 0, 0, 1,
     "迅捷（可在你的回合或法术对决中打出。）对战场上的一名单位造成3点伤害。",
     32768, "hex_ray", 0, 0, 0, 0, 1, 1, 0, 0, 0),
    ("void_seek", "虚空索敌", 3, 0, 0, 1,
     "迅捷（可在你的回合或法术对决中打出。）对战场上的一名单位造成4点伤害，然后抽一张牌。",
     32768, "void_seek", 0, 0, 0, 0, 1, 1, 0, 0, 0),
    ("stardrop", "星落", 2, 0, 0, 2,
     "进行两次：对一名单位造成3点伤害。（可以选择不同的单位。）",
     0, "stardrop", 0, 0, 0, 0, 1, 1, 0, 0, 0),
    ("starburst", "星芒凝汇", 6, 0, 1, 2,
     "对最多两名单位各造成6点伤害。",
     0, "starburst", 0, 0, 0, 0, 1, 1, 0, 0, 0),
    ("evolve_day", "进化日", 6, 0, 1, 1,
     "抽四张牌。",
     0, "evolve_day", 0, 0, 0, 0, 1, 0, 0, 0, 0),
    ("akasi_storm", "艾卡西亚暴雨", 7, 0, 0, 2,
     "进行六次：对一名单位造成2点伤害。（你可以选择不同的目标。）",
     0, "akasi_storm", 0, 0, 0, 0, 1, 0, 0, 1, 1),
    ("furnace_blast", "风箱炎息", 1, 0, 0, 1,
     "迅捷（可在你的回合或法术对决中打出。）回响（你可以选择支付额外费用，以重复此法术效果。）对同一位置的最多三名单位各造成1点伤害。",
     36864, "furnace_blast", 0, 0, 0, 0, 1, 0, 0, 0, 0),
    ("time_warp", "时间扭曲", 10, 0, 1, 4,
     "在当前回合结束后，再进行一个回合。然后放逐此牌。",
     0, "time_warp", 0, 0, 0, 0, 1, 0, 0, 0, 0),
    ("divine_ray", "透体圣光", 2, 0, 1, 1,
     "回响（你可以选择支付额外费用，以重复此法术效果。）对战场上的一名单位造成2点伤害。",
     4096, "divine_ray", 0, 0, 0, 0, 1, 1, 0, 0, 0),
    ("swindle", "愚诈", 1, 0, 1, 0,
     "反应（可在任意时机打出，甚至先于其他法术和技能的结算。）让一名单位在本回合内-1，不得低于1。抽一张牌。",
     64, "swindle", 0, 0, 0, 0, 1, 0, 0, 0, 0),
    ("retreat_rune", "择日再战", 1, 0, 1, 0,
     "反应（可在任意时机打出，甚至先于其他法术和技能的结算。）让一名友方单位返回其所属者的手牌，然后其所属者召出一枚休眠的符文。",
     64, "retreat_rune", 0, 0, 0, 0, 1, 0, 0, 0, 0),
    ("guilty_pleasure", "罪恶快感", 2, 0, 0, 1,
     "迅捷（可在你的回合或法术对决中打出。）弃置一张手牌。对战场上的一名单位造成等同于被弃置牌费用的伤害。（无视符能费用。）",
     32768, "guilty_pleasure", 0, 0, 0, 0, 1, 1, 0, 0, 0),
    ("smoke_bomb", "烟幕弹", 2, 0, 1, 1,
     "反应（可在任意时机打出，甚至先于其他法术和技能的结算。）让一名单位在本回合内-4，不得低于1。",
     64, "smoke_bomb", 0, 0, 0, 0, 1, 0, 0, 0, 0),

    # ─── Yi deck units (Calm + Body) ───
    ("yi_hero", "易·锋芒毕现", 7, 6, 3, 1,
     "游走（我可以向其他战场进行移动。）我以活跃状态进场。",
     256, "yi_hero_enter", 0, 0, 0, 0, 0, 0, 1, 0, 0),
    ("jax", "贾克斯·万般皆武", 5, 5, 3, 1,
     "法盾（敌方法术需额外支付[1]才能选中。）当你打出我时，让你基地的装备卡获得【反应】。",
     4, "jax_enter", 0, 0, 0, 0, 0, 0, 0, 0, 0),
    ("tiyana_warden", "缇亚娜·冕卫", 7, 4, 2, 1,
     "法盾（敌方法术需额外支付[1]才能选中。）如果我位于战场上，则对手无法得分。",
     4, "tiyana_enter", 0, 0, 0, 0, 0, 0, 0, 3, 1),
    ("wailing_poro", "哀哀魄罗", 2, 2, 2, 0,
     "绝念 — 当我被摧毁时，如果此处没有其他友方单位，则抽一张牌。（当我被摧毁时，发动此效果。）",
     32, "wailing_poro_die", 0, 0, 0, 0, 0, 0, 0, 0, 0),
    ("sandshoal_deserter", "沙墟啸匪", 6, 5, 3, 0,
     "敌方法术和技能无法将我选作目标。",
     4, "sandshoal_deserter_enter", 0, 0, 0, 0, 0, 0, 0, 0, 0),

    # ─── Yi equipment ───
    ("zhonya", "中娅沙漏", 2, 0, 2, 0,
     "隐匿（支付[1]正面朝下放置此牌，之后可以0费反应打出。）下一次当友方单位被摧毁时，改为将此牌摧毁，然后该单位以休眠状态返回基地。（把该单位移到基地，此行为不视为移动。）",
     1088, "zhonya_equip", 1, 0, 2, 0, 0, 0, 0, 0, 0),
    ("trinity_force", "三相之力", 4, 0, 3, 0,
     "装配[1]（支付[1]：将此牌贴附到你控制的一名单位上。）当我据守一处战场时，获得的分数+1。+2战力",
     0, "trinity_equip", 1, 2, 3, 1, 0, 0, 0, 0, 0),
    ("guardian_angel", "守护天使", 2, 0, 2, 0,
     "装配[1]（支付[1]：将此牌贴附到你控制的一名单位上。）若此牌被附着的单位会被摧毁，改为将此牌摧毁。召回该单位并使其休眠。（把它移到基地，此行为不视为移动。）+1战力",
     0, "guardian_equip", 1, 1, 2, 1, 0, 0, 0, 0, 0),
    ("dorans_blade", "多兰之刃", 2, 0, 3, 0,
     "装配[1]（支付[1]：将此牌贴附到你控制的一名单位上。）+2战力",
     0, "dorans_equip", 1, 2, 3, 1, 0, 0, 0, 0, 0),

    # ─── Yi deck spells ───
    ("rally_call", "迎敌号令", 2, 0, 3, 0,
     "迅捷（可在你的回合或法术对决中打出。）在本回合内，你打出的所有单位以活跃状态进场。抽一张牌。",
     32768, "rally_call", 0, 0, 0, 0, 1, 0, 0, 0, 0),
    ("balance_resolve", "御御守念", 3, 0, 2, 0,
     "迅捷（可在你的回合或法术对决中打出。）如果对手分数离获胜分数不超过3，则此法术的费用减少[3]。抽一张牌，然后召出一枚休眠的符文。",
     32768, "balance_resolve", 0, 0, 0, 0, 1, 0, 0, 0, 0),
    ("slam", "扑咚", 2, 0, 3, 0,
     "迅捷（可在你的回合或法术对决中打出。）回响（你可以选择支付额外费用，以重复此法术效果。）眩晕一名单位。（使其在本回合内无法造成战斗伤害。）",
     36864, "slam", 0, 0, 0, 0, 1, 1, 0, 0, 0),
    ("strike_ask_later", "先打再问", 1, 0, 3, 2,
     "迅捷（可在你的回合或法术对决中打出。）让一名单位在本回合内+5。",
     32768, "strike_ask_later", 0, 0, 0, 0, 1, 2, 0, 0, 0),
    ("scoff", "藐视", 1, 0, 2, 1,
     "反应（可在任意时机打出，甚至先于其他法术和技能的结算。）无效化一个法术，但其费用不得高于[4]，符能费用不得高于[3]。",
     64, "scoff", 0, 0, 0, 0, 1, 0, 0, 0, 0),
    ("duel_stance", "冰斗架势", 1, 0, 2, 1,
     "反应（可在任意时机打出，甚至先于其他法术和技能的结算。）让一名友方单位在本回合内+1。如果它是你在这一回合打出的，则它本回合内额外获得+1。",
     64, "duel_stance", 0, 0, 0, 0, 1, 0, 0, 0, 0),
    ("well_trained", "训练有素", 2, 0, 2, 0,
     "反应（可在任意时机打出，甚至先于其他法术和技能的结算。）让一名单位在本回合内+2，然后抽一张牌。",
     64, "well_trained", 0, 0, 0, 0, 1, 0, 0, 0, 0),
    ("wind_wall", "风之障壁", 3, 0, 2, 2,
     "反应（可在任意时机打出，甚至先于其他法术和技能的结算。）无效化一个法术。",
     64, "wind_wall", 0, 0, 0, 0, 1, 0, 0, 0, 0),
    ("flash_counter", "疾速反制", 2, 0, 3, 0,
     "反应（可在任意时机打出，甚至先于其他法术和技能的结算。）无效化一个敌方法术或装备卡对目标的法术或技能效果。",
     64, "flash_counter", 0, 0, 0, 0, 1, 0, 0, 0, 0),

    # ─── Legends ───
    ("kaisa_legend", "卡莎·虚空之女", 0, 0, 0, 0,
     "横置：反应 — 获得1任意符能，仅可用于打出法术。（获得资源的反应技能无法成为其他法术的反应目标。）",
     0, "", 0, 0, 0, 0, 0, 0, 0, 1, 0),
    ("yi_legend", "无极剑圣", 0, 0, 3, 0,
     "如果你只有一名友方单位防守一处战场，则该单位+2。",
     0, "", 0, 0, 0, 0, 0, 0, 0, 2, 0),
]

def to_unicode_escape(s):
    result = []
    for c in s:
        if ord(c) < 128:
            result.append(c)
        else:
            result.append(f'\\u{ord(c):04X}')
    return ''.join(result)

CARD_DIR = "E:/claudeCode/unity/FWTCG_UNITY_V3/Assets/Resources/Cards"

def update_asset(card):
    (cid, name, cost, atk, rune, rcost, desc, kw, effid,
     is_eq, eq_atk, eq_rune, eq_rcost, is_sp, sp_target, is_hero,
     sec_rune, sec_rcost) = card

    path = os.path.join(CARD_DIR, cid + '.asset')
    if not os.path.exists(path):
        print(f"  MISS: {cid}")
        return False

    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()

    enc_name = to_unicode_escape(name)
    enc_desc = to_unicode_escape(desc)

    # Use functions for replacements to avoid backslash interpretation in \u escapes
    replacements = [
        (r'(_cardName: )".*?"',           lambda m: m.group(1) + f'"{enc_name}"'),
        (r'(_cost: )\d+',                  lambda m: m.group(1) + str(cost)),
        (r'(_runeType: )\d+',              lambda m: m.group(1) + str(rune)),
        (r'(_runeCost: )\d+',              lambda m: m.group(1) + str(rcost)),
        (r'(_atk: )\d+',                   lambda m: m.group(1) + str(atk)),
        (r'(_keywords: )\d+',              lambda m: m.group(1) + str(kw)),
        (r'(_effectId: ).*',               lambda m: m.group(1) + effid),
        (r'(_isEquipment: )\d+',           lambda m: m.group(1) + str(is_eq)),
        (r'(_equipAtkBonus: )\d+',         lambda m: m.group(1) + str(eq_atk)),
        (r'(_equipRuneType: )\d+',         lambda m: m.group(1) + str(eq_rune)),
        (r'(_equipRuneCost: )\d+',         lambda m: m.group(1) + str(eq_rcost)),
        (r'(_isHero: )\d+',                lambda m: m.group(1) + str(is_hero)),
        (r'(_isSpell: )\d+',               lambda m: m.group(1) + str(is_sp)),
        (r'(_spellTargetType: )\d+',       lambda m: m.group(1) + str(sp_target)),
        (r'(_description: )".*?"',         lambda m: m.group(1) + f'"{enc_desc}"'),
    ]

    new_content = content
    for pat, rep in replacements:
        new_content = re.sub(pat, rep, new_content)

    if '_secondaryRuneType:' not in new_content:
        new_content = re.sub(
            r'(_runeCost: \d+\n)',
            lambda m: m.group(1) + f'  _secondaryRuneType: {sec_rune}\n  _secondaryRuneCost: {sec_rcost}\n',
            new_content, count=1
        )
    else:
        new_content = re.sub(r'(_secondaryRuneType: )\d+',
                             lambda m: m.group(1) + str(sec_rune), new_content)
        new_content = re.sub(r'(_secondaryRuneCost: )\d+',
                             lambda m: m.group(1) + str(sec_rcost), new_content)

    if new_content != content:
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(new_content)
        print(f"  OK:   {cid}")
        return True
    print(f"  SAME: {cid}")
    return False

if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    n = 0
    for card in CARDS:
        if update_asset(card):
            n += 1
    print(f"\nUpdated {n}/{len(CARDS)} cards")
