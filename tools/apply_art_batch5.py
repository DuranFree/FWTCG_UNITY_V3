"""Batch 5: cards 33-42 aligned to card art (last batch of main cards)."""
import os, sys, io, re
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

BATCH = [
    dict(id='tiyana_warden', name='缇亚娜·冕卫',
         cost=7, runeType=2, runeCost=2, atk=4, keywords=4,  # 法盾
         isSpell=0, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='法盾（对手必须支付 1 符征才能将我选为法术或技能的目标。）如果我位于战场上，则对手无法获得据守分。'),
    dict(id='trinity_force', name='三相之力',
         cost=4, runeType=3, runeCost=0, atk=0, keywords=0,
         isSpell=0, isEquipment=1, isHero=0,
         equipAtkBonus=2, equipRuneType=3, equipRuneCost=1,
         desc='部署【摧】（支付 1 摧破符能）：将此牌贴附到你控制的一名单位上。当装备单位据守战场时，额外获得 1 分。+2 战力。'),
    dict(id='void_seek', name='虚空索敌',
         cost=3, runeType=0, runeCost=1, atk=0, keywords=32768,  # 迅捷
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='迅捷（可在你的回合或法术对决中打出。）对战场上的一名单位造成 4 点伤害，然后抽一张牌。'),
    dict(id='wailing_poro', name='哀哀魄罗',
         cost=2, runeType=0, runeCost=0, atk=2, keywords=32,  # 绝念
         isSpell=0, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='绝念 — 当我被摧毁时，如果此处没有其他友方单位，则抽一张牌。'),
    dict(id='well_trained', name='训练有素',
         cost=2, runeType=0, runeCost=0, atk=0, keywords=64,  # 反应
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='反应（可在任意时机打出，甚至先于其他法术和技能的结算。）让一名单位在本回合内 +2 战力，然后抽一张牌。'),
    dict(id='wind_wall', name='风之障壁',
         cost=3, runeType=2, runeCost=2, atk=0, keywords=64,  # 反应
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='反应（可在任意时机打出，甚至先于其他法术和技能的结算。）无效化一个法术。'),
    dict(id='yi_hero', name='易·锋芒毕现',
         cost=7, runeType=3, runeCost=1, atk=6, keywords=256,  # 游走
         isSpell=0, isEquipment=0, isHero=1,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='游走（我可以向其他战场进行移动。）我以活跃状态进场。'),
    dict(id='yi_legend', name='无极剑圣（易）',
         cost=5, runeType=3, runeCost=0, atk=5, keywords=0,
         isSpell=0, isEquipment=0, isHero=1,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='被动光环：当此战场仅有 1 名友方单位防守时，该单位本回合战力 +2。'),
    dict(id='yordel_instructor', name='约德尔教官',
         cost=3, runeType=0, runeCost=0, atk=2, keywords=2,  # 壁垒
         isSpell=0, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='壁垒（在战斗中首次承担伤害。）当你打出我时，抽一张牌。'),
    dict(id='zhonya', name='中娅沙漏',
         cost=2, runeType=2, runeCost=0, atk=0, keywords=1024,  # 待命
         isSpell=0, isEquipment=1, isHero=0,
         equipAtkBonus=0, equipRuneType=2, equipRuneCost=0,
         desc='待命（可盖放，之后 0 费作为反应打出。）当友方单位将被摧毁时，摧毁此装备代替，使单位以休眠状态撤回基地。'),
]

def rewrite(path, c):
    with open(path, encoding='utf-8') as f:
        content = f.read()
    def sub_str(field, val):
        nonlocal content
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
