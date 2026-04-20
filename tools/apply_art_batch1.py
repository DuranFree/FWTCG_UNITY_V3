"""
Batch 1: apply card-art-based fixes to 8 assets directly by file rewrite.
Bypasses Unity's re-serialization surprises by doing exact YAML rewrites.
"""
import os, sys, io, re
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

# Batch 1 cards: (id, card_name, cost, runeType, runeCost, atk, keywords, isSpell, isEquipment, isHero, equipAtkBonus, equipRuneType, equipRuneCost, description)
# keywords: bit flags (急速=1,壁垒=2,法盾=4,鼓舞=8,征服=16,绝念=32,反应=64,强攻=128,游走=256,预知=512,待命=1024,眩晕=2048,回响=4096,坚守=8192,瞬息=16384,迅捷=32768)
# runeType: 0=Blazing,1=Radiant,2=Verdant,3=Crushing (Unity enum order)

BATCH = [
    # akasi_storm
    dict(id='akasi_storm',
         name='艾卡西亚暴雨',
         cost=7, runeType=1, runeCost=2, atk=0, keywords=0,
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='穹绽六次：对一名单位造成 2 点伤害。（你可以选择不同的单位。）'),
    # alert_sentinel
    dict(id='alert_sentinel',
         name='警觉的哨兵',
         cost=2, runeType=0, runeCost=0, atk=2, keywords=32,
         isSpell=0, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='绝念：抽 1 张牌。（当我被摧毁后，发动此效果。）'),
    # bad_poro
    dict(id='bad_poro',
         name='坏坏魄罗',
         cost=2, runeType=0, runeCost=0, atk=2, keywords=16,
         isSpell=0, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='当我征服一处战场时，打出 1 张休眠的「金币」装备指示物。'),
    # balance_resolve
    dict(id='balance_resolve',
         name='御衡守念',
         cost=3, runeType=0, runeCost=0, atk=0, keywords=32768,
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='迅捷（可在你的回合或法术对决中打出。）如果对手分数高于我方且差距不超过 3 分，则此法术的费用减少 ①。抽一张牌，然后召出一枚休眠的符文。'),
    # darius
    dict(id='darius',
         name='德莱厄斯',
         cost=5, runeType=0, runeCost=1, atk=5, keywords=0,
         isSpell=0, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='每当你在回合中打出第二张牌时，让我在该回合中 +2 战力，并让我变为活跃状态。'),
    # divine_ray
    dict(id='divine_ray',
         name='透体圣光',
         cost=2, runeType=0, runeCost=2, atk=0, keywords=4096,
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='回响（你可以选择支付此额外费用，以便额外发动一次效果。）对战场上任一名单位造成 4 点伤害；回响施放时改为造成 6 点伤害。'),
    # dorans_blade
    dict(id='dorans_blade',
         name='多兰之刃',
         cost=2, runeType=3, runeCost=0, atk=0, keywords=0,
         isSpell=0, isEquipment=1, isHero=0,
         equipAtkBonus=2, equipRuneType=3, equipRuneCost=1,
         desc='部署【法】（支付 1 符征）：将此牌贴附到你控制的一名单位上。+2 战力。'),
    # duel_stance
    dict(id='duel_stance',
         name='决斗架势',
         cost=1, runeType=0, runeCost=0, atk=0, keywords=64,
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='反应（可在任意时机打出，甚至先于其他法术和技能的结算。）让一名我方单位本回合内 +1 战力。如果它是你在该阶段一控制的单位，则它本回合内额外获得 +1 战力。'),
]

def rewrite(path, c):
    with open(path, encoding='utf-8') as f:
        content = f.read()

    def sub_str(field, val):
        nonlocal content
        # Match _field: "..." (any content until line end or closing quote)
        content = re.sub(
            rf'(\s_{field}:)\s*"[^"]*"',
            rf'\1 "{val}"',
            content, count=1)
        # Also match unquoted (e.g. BF assets have bare UTF-8)
        content = re.sub(
            rf'(\s_{field}:)\s*([^"\n][^\n]*)$',
            rf'\1 "{val}"',
            content, count=1, flags=re.M)

    def sub_num(field, val):
        nonlocal content
        content = re.sub(
            rf'(\s_{field}:)\s*(-?\d+)',
            rf'\1 {val}',
            content, count=1)

    sub_str('cardName', c['name'])
    sub_num('cost',     c['cost'])
    sub_num('runeType', c['runeType'])
    sub_num('runeCost', c['runeCost'])
    sub_num('atk',      c['atk'])
    sub_num('keywords', c['keywords'])
    sub_num('isSpell',     c['isSpell'])
    sub_num('isEquipment', c['isEquipment'])
    sub_num('isHero',      c['isHero'])
    sub_num('equipAtkBonus', c['equipAtkBonus'])
    sub_num('equipRuneType', c['equipRuneType'])
    sub_num('equipRuneCost', c['equipRuneCost'])
    sub_str('description', c['desc'])

    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(content)

    return True


for c in BATCH:
    p = f"Assets/Resources/Cards/{c['id']}.asset"
    if not os.path.exists(p):
        print(f'MISSING: {p}')
        continue
    rewrite(p, c)
    print(f'✓ {c["id"]}: {c["name"]}')
