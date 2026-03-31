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
        Haste       = 1 << 0,  // 急速: enter active (pay extra rune cost)
        Barrier     = 1 << 1,  // 壁垒: absorbs one damage instance
        SpellShield = 1 << 2,  // 法盾: immune to targeted spells
        Inspire     = 1 << 3,  // 鼓舞: other allies cost -1 on entry
        Conquest    = 1 << 4,  // 征服: trigger on conquering a BF
        Deathwish   = 1 << 5,  // 绝念: trigger on death
        Reactive    = 1 << 6,  // 反应: can be played as reaction
        StrongAtk   = 1 << 7,  // 强攻: +2 power when attacking
        Roam        = 1 << 8,  // 游走: can move between battlefields
        Foresight   = 1 << 9,  // 预知: view top deck on entry
        Standby     = 1 << 10, // 待命: face-down 0-cost reactive
        Stun        = 1 << 11, // 眩晕: cannot contribute power in combat
        Echo        = 1 << 12, // 回响: can be played a second time this turn
        Guard       = 1 << 13, // 坚守: +1 power when defending
        Ephemeral   = 1 << 14, // 瞬息: destroyed at start of next turn (Rule 728)
    }
}
