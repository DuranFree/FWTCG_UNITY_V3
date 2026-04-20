"""Batch 6: BF cards with reliable art (6 of 11)."""
import os, sys, io, re
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

# Only BF cards where art filename content matches the card id
BATCH = [
    dict(id='ascending_stairs', name='攀圣长阶',
         desc='使赢得游戏所需的分数 +1。'),
    dict(id='forgotten_monument', name='遗忘丰碑',
         desc='每名玩家在各自的第三回合开始前，无法从此处获得分数。'),
    dict(id='star_peak', name='星尖峰',
         desc='当你据守此处时，你可以选择召出一枚休眠的符文。'),
    dict(id='strength_obelisk', name='力量方尖碑',
         desc='每名玩家在各自的第一个回合开始阶段，额外召出一枚符文。'),
    dict(id='thunder_rune', name='雷霆之纹',
         desc='当你征服此处时，回收你的一张符文。'),
    dict(id='void_gate', name='虚空之门',
         desc='以此处的单位作为目标的法术或技能，造成的伤害 +1（每段伤害都 +1）。'),
]

def rewrite(path, c):
    with open(path, encoding='utf-8') as f:
        content = f.read()
    def sub_str(field, val):
        nonlocal content
        v = val.replace('"', '\\"')
        content = re.sub(rf'(\s_{field}:)\s*"[^"]*"', rf'\1 "{v}"', content, count=1)
        content = re.sub(rf'(\s_{field}:)\s*([^"\n][^\n]*)$', rf'\1 "{v}"', content, count=1, flags=re.M)
    sub_str('cardName', c['name'])
    sub_str('description', c['desc'])
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(content)

for c in BATCH:
    p = f"Assets/Resources/Cards/BF/{c['id']}.asset"
    if not os.path.exists(p):
        print(f'MISSING: {p}')
        continue
    rewrite(p, c)
    print(f'✓ {c["id"]}: {c["name"]}')
