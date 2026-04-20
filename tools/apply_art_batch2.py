"""Batch 2: cards 9-16 aligned to card art."""
import os, sys, io, re
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

BATCH = [
    dict(id='evolve_day',
         name='进化日',
         cost=6, runeType=1, runeCost=1, atk=0, keywords=0,
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='抽四张牌。'),
    dict(id='flash_counter',
         name='极速反制',
         cost=2, runeType=2, runeCost=1, atk=0, keywords=64,  # 反应
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='反应（可在任意时机打出，甚至先于其他法术和技能的结算。）无效化一个以我方单位或友方装备为目标的敌方法术或技能。'),
    dict(id='foresight_mech',
         name='先见机甲',
         cost=2, runeType=0, runeCost=0, atk=2, keywords=512,  # 预知
         isSpell=0, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='你的「机械」属性单位获得预知。（当你打出我时，查看主牌堆顶的一张牌，你可以选择将其保留。）'),
    dict(id='furnace_blast',
         name='风箱炎息',
         cost=1, runeType=1, runeCost=0, atk=0, keywords=36864,  # 迅捷+回响
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='迅捷（可在你的回合或法术对决中打出。）回响（你可以选择支付此额外费用，以重复发动此法术效果。）对同一位置的最多三名单位各造成 1 点伤害。'),
    dict(id='guardian_angel',
         name='守护天使',
         cost=2, runeType=2, runeCost=0, atk=0, keywords=0,
         isSpell=0, isEquipment=1, isHero=0,
         equipAtkBonus=1, equipRuneType=2, equipRuneCost=1,
         desc='部署【法】（支付 1 符征）：将此牌贴附到你控制的一名单位上。如果所附着的单位被摧毁，则其改为保留在你的基地（在休眠状态下），并且将此装备卸除。+1 战力。'),
    dict(id='guilty_pleasure',
         name='罪恶快感',
         cost=2, runeType=0, runeCost=1, atk=0, keywords=32768,  # 迅捷
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='迅捷（可在你的回合或法术对决中打出。）弃置一张手牌。对战场上的一名单位造成等同于被弃置牌的法力费用的伤害。（无视其符能限制。）'),
    dict(id='hex_ray',
         name='海克斯射线',
         cost=1, runeType=0, runeCost=1, atk=0, keywords=32768,  # 迅捷
         isSpell=1, isEquipment=0, isHero=0,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='迅捷（可在你的回合或法术对决中打出。）对战场上的一名单位造成 3 点伤害。'),
    dict(id='jax',
         name='贾克斯·万般皆武',
         cost=5, runeType=2, runeCost=1, atk=5, keywords=4,  # 法盾
         isSpell=0, isEquipment=0, isHero=1,
         equipAtkBonus=0, equipRuneType=0, equipRuneCost=0,
         desc='法盾（对手必须支付 1 符征才能将我选为法术或技能的目标。）你所持任意装备获得反应关键词。（装备不在贴附状态时，将它保留到你基地中一名单位上。）'),
]

def rewrite(path, c):
    with open(path, encoding='utf-8') as f:
        content = f.read()

    def sub_str(field, val):
        nonlocal content
        content = re.sub(
            rf'(\s_{field}:)\s*"[^"]*"',
            rf'\1 "{val}"',
            content, count=1)
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

for c in BATCH:
    p = f"Assets/Resources/Cards/{c['id']}.asset"
    if not os.path.exists(p):
        print(f'MISSING: {p}')
        continue
    rewrite(p, c)
    print(f'✓ {c["id"]}: {c["name"]}')
