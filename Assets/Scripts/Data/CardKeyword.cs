using System;

namespace FWTCG.Data
{
    /// <summary>
    /// Keyword flags for unit/spell cards.
    /// Multiple keywords use bitwise OR.
    /// </summary>
    [Flags]
    public enum CardKeyword
    {
        None        = 0,
        Haste       = 1 << 0,  // 急速: unit-only, optional pay [1]+[C] to enter active (Rule 717)
        Barrier     = 1 << 1,  // 壁垒: must take lethal damage first in combat (Rule 727)
        SpellShield = 1 << 2,  // 法盾: opponent pays extra sch to target (Rule 721)
        Inspire     = 1 << 3,  // 军团 (Legion, Rule 24): 条件关键词 — 本回合已打出过其他牌时，[文本]效果生效
                               // 历史命名为 Inspire；术语统一为"军团"，别名 Legion。
        Conquest    = 1 << 4,  // 征服: trigger on conquering a BF
        Deathwish   = 1 << 5,  // 绝念: trigger on death
        Reactive    = 1 << 6,  // 反应: can be played as reaction
        StrongAtk   = 1 << 7,  // 强攻: +X power when attacking, default X=1 (Rule 719)
        Roam        = 1 << 8,  // 游走: can move between battlefields
        Foresight   = 1 << 9,  // 预知: view top deck on entry
        Standby     = 1 << 10, // 待命: face-down 0-cost reactive
        Stun        = 1 << 11, // 眩晕: cannot contribute power in combat
        Echo        = 1 << 12, // 回响: can be played a second time this turn
        Guard       = 1 << 13, // 坚守: +1 power when defending
        Ephemeral   = 1 << 14, // 瞬息: destroyed at start of next turn (Rule 728)
        Swift       = 1 << 15, // 迅捷: can be played during spell duel (Rule 718)
    }
}
