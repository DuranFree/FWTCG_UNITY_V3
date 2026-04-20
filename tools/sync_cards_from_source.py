"""
Parse E:/claudeCode/FWTCG_V3d_V9/js/cards.js and align all Unity CardData assets.

Source is authoritative. For each card id found in source, rewrite the matching
.asset file's fields (cost, atk, rune type/cost, keywords, description, etc.)
to match source. Changes are reported per-card.
"""
import os, re, sys, io, json

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

SRC = 'E:/claudeCode/FWTCG_V3d_V9/js/cards.js'
UNITY_ROOT = '.'  # run from project root
MAIN_DIR = os.path.join(UNITY_ROOT, 'Assets/Resources/Cards')
BF_DIR = os.path.join(UNITY_ROOT, 'Assets/Resources/Cards/BF')

# ── Keyword → flag mapping (must match CardKeyword.cs bit flags) ──
KW_FLAGS = {
    '急速': 1 << 0,
    '壁垒': 1 << 1,
    '法盾': 1 << 2,
    '鼓舞': 1 << 3,
    '征服': 1 << 4,
    '绝念': 1 << 5,
    '反应': 1 << 6,
    '强攻': 1 << 7,
    '游走': 1 << 8,
    '预知': 1 << 9,
    '待命': 1 << 10,
    '眩晕': 1 << 11,
    '回响': 1 << 12,
    '坚守': 1 << 13,
    '瞬息': 1 << 14,
    '迅捷': 1 << 15,
    # 迅捷攻击 is legend-specific, treat as Swift for now
    '迅捷攻击': 1 << 15,
}

# ── RuneType enum mapping (FWTCG.Data.RuneType) ──
# Indices match the enum order. Verify against source enum definition.
RUNE_TYPES = {
    'none':     0,
    'blazing':  1,  # 炽烈
    'radiant':  2,  # 灵光
    'verdant':  3,  # 翠意
    'crushing': 4,  # 摧破
}


def js_string(raw):
    """Decode JS-style string escapes."""
    s = raw
    s = s.replace("\\'", "'")
    s = s.replace('\\"', '"')
    s = s.replace('\\n', '\n')
    return s


def parse_card_line(line):
    """Parse a single { id: 'x', name: '...', ... } line from cards.js.
    Returns dict or None if not a valid card line.
    """
    line = line.strip()
    if not line.startswith('{') or 'id:' not in line:
        return None
    # Remove trailing comma and braces
    inner = line[1:]
    if inner.endswith(','):
        inner = inner[:-1]
    if inner.endswith('}'):
        inner = inner[:-1]

    # Simple kv tokenizer (not perfect for nested objects, but works here)
    # Handle: id: 'x', name: 'x', cost: 5, keywords: ['a','b'], text: '...'
    out = {}
    # Extract id
    m = re.search(r"id:\s*'([^']+)'", inner)
    if not m:
        return None
    out['id'] = m.group(1)
    # name
    m = re.search(r"name:\s*'((?:[^'\\]|\\.)*)'", inner)
    if m: out['name'] = js_string(m.group(1))
    # type
    m = re.search(r"type:\s*'([^']+)'", inner)
    if m: out['type'] = m.group(1)
    # cost
    m = re.search(r"(?<!echo)(?<!sch)(?<!equip)(?<!equipSch)cost:\s*(\d+)", inner)
    if m: out['cost'] = int(m.group(1))
    # atk
    m = re.search(r"atk:\s*(\d+)", inner)
    if m: out['atk'] = int(m.group(1))
    # hp
    m = re.search(r"hp:\s*(\d+)", inner)
    if m: out['hp'] = int(m.group(1))
    # schCost
    m = re.search(r"schCost:\s*(\d+)", inner)
    if m: out['schCost'] = int(m.group(1))
    # schType
    m = re.search(r"schType:\s*'([^']+)'", inner)
    if m: out['schType'] = m.group(1)
    # hero flag
    if re.search(r"hero:\s*true", inner): out['hero'] = True
    # keywords
    m = re.search(r"keywords:\s*\[([^\]]*)\]", inner)
    if m:
        kws = re.findall(r"'([^']+)'", m.group(1))
        out['keywords'] = kws
    # text (description)
    m = re.search(r"text:\s*'((?:[^'\\]|\\.)*)'", inner)
    if m: out['text'] = js_string(m.group(1))
    # effect
    m = re.search(r"effect:\s*'([^']+)'", inner)
    if m: out['effect'] = m.group(1)
    # equipment bonuses
    m = re.search(r"atkBonus:\s*(\d+)", inner)
    if m: out['atkBonus'] = int(m.group(1))
    m = re.search(r"equipSchCost:\s*(\d+)", inner)
    if m: out['equipSchCost'] = int(m.group(1))
    m = re.search(r"equipSchType:\s*'([^']+)'", inner)
    if m: out['equipSchType'] = m.group(1)
    return out


def parse_source():
    """Return dict { id → card_dict } from cards.js."""
    with open(SRC, encoding='utf-8') as f:
        lines = f.readlines()
    cards = {}
    for i, line in enumerate(lines):
        c = parse_card_line(line)
        if c is None: continue
        cards[c['id']] = c  # dedupe (multiple copies)
    # Also parse the legend singleton consts
    content = ''.join(lines)
    # kaisa legend
    for m in re.finditer(r"(KAISA_LEGEND|MASTERYI_LEGEND)\s*=\s*\{(.*?)\};",
                          content, re.S):
        body = '{' + m.group(2) + '}'
        # Pull first line (the champion itself)
        first_line = body.split('\n')[0] + '}'  # close the first-level
        # Actually easier: rebuild as one line
        flat = body.replace('\n', ' ')
        # Truncate at first ' abilities:' to isolate champ
        idx = flat.find('abilities:')
        champ_src = flat[:idx].rstrip(', ')
        if not champ_src.endswith('}'):
            champ_src += '}'
        c = parse_card_line(champ_src)
        if c: cards[c['id']] = c
    return cards


def kw_flags(keywords):
    v = 0
    for k in keywords or []:
        v |= KW_FLAGS.get(k, 0)
    return v


def rune_type(sch):
    return RUNE_TYPES.get((sch or 'none').lower(), 0)


def yaml_quote(s):
    """Return yaml-safe single quoted or raw string."""
    # YAML prefers plain UTF-8 strings; just enclose in double quotes with escaping
    s = s.replace('\\', '\\\\').replace('"', '\\"')
    return '"' + s + '"'


def update_asset(path, card, id_is_bf):
    """Rewrite fields of an existing Unity .asset (CardData ScriptableObject)."""
    with open(path, encoding='utf-8') as f:
        content = f.read()

    changes = []

    def set_field(pattern, new_val, label):
        nonlocal content, changes
        m = re.search(pattern, content)
        if not m:
            changes.append(f"{label}: (field not found)")
            return
        old = m.group(1)
        if str(old).strip() != str(new_val).strip():
            content = content[:m.start(1)] + str(new_val) + content[m.end(1):]
            changes.append(f"{label}: {old} → {new_val}")

    # _cardName (string, may be quoted with unicode escapes)
    name_val = card.get('name', '')
    set_field(r'_cardName:\s*(.*)', yaml_quote(name_val), '_cardName')

    # _cost
    set_field(r'_cost:\s*(-?\d+)', card.get('cost', 0), '_cost')

    # _runeType (from schType)
    set_field(r'_runeType:\s*(-?\d+)', rune_type(card.get('schType')), '_runeType')

    # _runeCost (from schCost)
    set_field(r'_runeCost:\s*(-?\d+)', card.get('schCost', 0), '_runeCost')

    # _atk
    set_field(r'_atk:\s*(-?\d+)', card.get('atk', 0), '_atk')

    # _keywords
    set_field(r'_keywords:\s*(-?\d+)', kw_flags(card.get('keywords', [])), '_keywords')

    # _isSpell
    is_spell = 1 if card.get('type') == 'spell' else 0
    set_field(r'_isSpell:\s*(-?\d+)', is_spell, '_isSpell')

    # _isEquipment
    is_equip = 1 if card.get('type') == 'equipment' else 0
    set_field(r'_isEquipment:\s*(-?\d+)', is_equip, '_isEquipment')

    # _isHero
    is_hero = 1 if (card.get('hero') or card.get('type') == 'champion') else 0
    set_field(r'_isHero:\s*(-?\d+)', is_hero, '_isHero')

    # _description
    desc = card.get('text', '')
    set_field(r'_description:\s*(.*)', yaml_quote(desc), '_description')

    # _equipAtkBonus
    if card.get('type') == 'equipment':
        set_field(r'_equipAtkBonus:\s*(-?\d+)', card.get('atkBonus', 0), '_equipAtkBonus')
        set_field(r'_equipRuneType:\s*(-?\d+)', rune_type(card.get('equipSchType')), '_equipRuneType')
        set_field(r'_equipRuneCost:\s*(-?\d+)', card.get('equipSchCost', 0), '_equipRuneCost')

    if changes:
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(content)
    return changes


def main():
    src = parse_source()
    print(f'源表共 {len(src)} 张卡（含 legend）')

    # List all unity asset IDs
    unity_ids = []
    for fn in os.listdir(MAIN_DIR):
        if fn.endswith('.asset'):
            unity_ids.append((fn[:-6], os.path.join(MAIN_DIR, fn), False))
    for fn in os.listdir(BF_DIR):
        if fn.endswith('.asset'):
            unity_ids.append((fn[:-6], os.path.join(BF_DIR, fn), True))

    # Unity special cases (legacy IDs that map to source IDs)
    alias = {
        'kaisa_legend': 'kaisa',
        'yi_legend':    'masteryi',
    }

    print(f'Unity 共 {len(unity_ids)} 张 asset')
    missing_in_src = []
    total_changes = 0
    for uid, path, is_bf in unity_ids:
        src_id = alias.get(uid, uid)
        card = src.get(src_id)
        if not card:
            missing_in_src.append(uid)
            continue
        changes = update_asset(path, card, is_bf)
        if changes:
            total_changes += 1
            print(f"\n── {uid} ({src_id}) ──")
            for c in changes:
                print(f"  {c}")

    print(f'\n=== 完成 ===')
    print(f'修改了 {total_changes} 张 asset')
    if missing_in_src:
        print(f'源表找不到的 asset（保持不动）: {missing_in_src}')


if __name__ == '__main__':
    main()
