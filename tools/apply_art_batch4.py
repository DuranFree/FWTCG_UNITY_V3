"""Batch 4: cards 25-32 aligned to card art."""
import os, sys, io, re
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

BATCH = [
    dict(id='slam', name='扑咚！',
         cost=2, runeType=0, runeCost=0, atk=0, keywords=36864,  # 迅捷+回响
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='迅捷（可在你的回合或法术对决中打出。）回响（你可以选择支付此额外费用，以重复发动此法术效果。）眩晕一名进攻方单位。（使其在本回合内无法造成伤害。）'),
    dict(id='smoke_bomb', name='烟幕弹',
         cost=2, runeType=1, runeCost=1, atk=0, keywords=64,  # 反应
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='反应（可在任意时机打出，甚至先于其他法术和技能的结算。）让一名单位在本回合内 -4 战力，不得低于 1。'),
    dict(id='starburst', name='星芒凝汇',
         cost=6, runeType=1, runeCost=2, atk=0, keywords=0,
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='对最多两名单位各造成 6 点伤害。'),
    dict(id='stardrop', name='星落',
         cost=2, runeType=0, runeCost=2, atk=0, keywords=0,
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='进行两次：对一名单位造成 3 点伤害。（可以选择不同的单位。）'),
    dict(id='strike_ask_later', name='先打再问',
         cost=1, runeType=3, runeCost=2, atk=0, keywords=32768,  # 迅捷
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='迅捷（可在你的回合或法术对决中打出。）让一名单位在本回合内 +5 战力。'),
    dict(id='swindle', name='"敲"诈',
         cost=1, runeType=0, runeCost=0, atk=0, keywords=64,  # 反应
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='反应（可在任意时机打出，甚至先于其他法术和技能的结算。）让一名单位在本回合内 -1 战力，不得低于 1。抽一张牌。'),
    dict(id='thousand_tail', name='千尾监视者',
         cost=7, runeType=1, runeCost=1, atk=7, keywords=1,  # 急速
         isSpell=0, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='急速（你可以选择额外支付 1 和 1 灵光符能，让我以活跃状态进场。）当你打出我时，让所有敌方单位本回合内 -3 战力，不得低于 1。'),
    dict(id='time_warp', name='时间扭曲',
         cost=10, runeType=1, runeCost=4, atk=0, keywords=0,
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='在当前回合结束后，再进行一个回合。然后放逐此牌。'),
]

def rewrite(path, c):
    with open(path, encoding='utf-8') as f:
        content = f.read()
    def sub_str(field, val):
        nonlocal content
        # Escape quotes in val
        v = val.replace('"', '\\"')
        content = re.sub(rf'(\s_{field}:)\s*"[^"]*"', rf'\1 "{v}"', content, count=1)
        content = re.sub(rf'(\s_{field}:)\s*([^"\n][^\n]*)$', rf'\1 "{v}"', content, count=1, flags=re.M)
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
