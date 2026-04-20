"""Batch 3: cards 17-24 aligned to card art."""
import os, sys, io, re
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

BATCH = [
    dict(id='kaisa_hero',
         name='卡莎·九死一生',
         cost=4, runeType=0, runeCost=1, atk=4, keywords=17,  # 急速+征服
         isSpell=0, isEquipment=0, isHero=1,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='急速（你可以选择额外支付 1 和 1 炽烈符能，让我以活跃状态进场。）当我征服一处战场时，抽一张牌。'),
    dict(id='kaisa_legend',
         name='虚空之女（卡莎）',
         cost=5, runeType=0, runeCost=0, atk=5, keywords=0,
         isSpell=0, isEquipment=0, isHero=1,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='迅捷攻击。进化：盟友集满 4 关键词后升级，+3/+3。'),
    dict(id='noxus_recruit',
         name='诺克萨斯新兵',
         cost=4, runeType=0, runeCost=0, atk=4, keywords=8,  # 鼓舞
         isSpell=0, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='鼓舞 — 我的费用减少 1。（如果你在本回合内已打出过其他卡牌，则发动此效果。）'),
    dict(id='rally_call',
         name='迎敌号令',
         cost=2, runeType=0, runeCost=0, atk=0, keywords=32768,  # 迅捷
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='迅捷（可在你的回合或法术对决中打出。）在本回合中，你打出的所有单位以活跃状态进场。抽一张牌。'),
    dict(id='rengar',
         name='雷恩加尔·暴起',
         cost=3, runeType=0, runeCost=1, atk=3, keywords=192,  # 反应+强攻
         isSpell=0, isEquipment=0, isHero=1,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='反应（可在任意时机打出，甚至先于其他法术和技能的结算。）强攻（如果我是进攻方，则 +2 战力。）需消耗 1 点炽烈符能打出。'),
    dict(id='retreat_rune',
         name='择日再战',
         cost=1, runeType=0, runeCost=0, atk=0, keywords=64,  # 反应
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='反应（可在任意时机打出，甚至先于其他法术和技能的结算。）让一名友方单位返回其所属的手牌，然后让其所有者召出一枚休眠的符文。'),
    dict(id='sandshoal_deserter',
         name='沙塔啸匪',
         cost=6, runeType=0, runeCost=0, atk=5, keywords=0,
         isSpell=0, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='敌方法术和技能无法将我选作目标。'),
    dict(id='scoff',
         name='蔑视',
         cost=1, runeType=2, runeCost=1, atk=0, keywords=64,  # 反应
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='反应（可在任意时机打出，甚至先于其他法术和技能的结算。）无效化一个法术，该法术费用不得高于 4，也不得高于当前可用法力。'),
]

def rewrite(path, c):
    with open(path, encoding='utf-8') as f:
        content = f.read()

    def sub_str(field, val):
        nonlocal content
        content = re.sub(rf'(\s_{field}:)\s*"[^"]*"', rf'\1 "{val}"', content, count=1)
        content = re.sub(rf'(\s_{field}:)\s*([^"\n][^\n]*)$', rf'\1 "{val}"', content, count=1, flags=re.M)

    def sub_num(field, val):
        nonlocal content
        content = re.sub(rf'(\s_{field}:)\s*(-?\d+)', rf'\1 {val}', content, count=1)

    sub_str('cardName', c['name']); sub_num('cost', c['cost'])
    sub_num('runeType', c['runeType']); sub_num('runeCost', c['runeCost'])
    sub_num('atk', c['atk']); sub_num('keywords', c['keywords'])
    sub_num('isSpell', c['isSpell']); sub_num('isEquipment', c['isEquipment'])
    sub_num('isHero', c['isHero'])
    sub_num('equipAtkBonus', c['equipAtkBonus']); sub_num('equipRuneType', c['equipRuneType'])
    sub_num('equipRuneCost', c['equipRuneCost']); sub_str('description', c['desc'])

    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(content)

for c in BATCH:
    p = f"Assets/Resources/Cards/{c['id']}.asset"
    if not os.path.exists(p):
        print(f'MISSING: {p}')
        continue
    rewrite(p, c)
    print(f'✓ {c["id"]}: {c["name"]}')
