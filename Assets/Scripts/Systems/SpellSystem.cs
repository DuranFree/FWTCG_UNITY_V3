using System.Collections.Generic;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG;

namespace FWTCG.Systems
{
    /// <summary>
    /// Handles spell card effects. Called by GameManager when the player casts a spell.
    /// For DEV-3: implements 10 non-reactive spells (6 Kaisa + 4 Yi).
    ///
    /// Spells are removed from hand and added to discard inside CastSpell().
    /// The GameManager is responsible for deducting mana before calling CastSpell.
    /// </summary>
    public class SpellSystem : MonoBehaviour
    {
        public static event System.Action<string> OnSpellLog;

        [SerializeField] private BattlefieldSystem _bfSys;

        // Captured at CastSpell entry so DealDamage knows the source without extra params.
        private string _currentSpellName = "";

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves a spell. Pass target=null for non-targeting spells (SpellTargetType.None).
        /// Removes spell from owner's hand and adds to owner's discard pile.
        /// </summary>
        public void CastSpell(UnitInstance spell, string owner, UnitInstance target, GameState gs)
        {
            _currentSpellName = spell.UnitName;
            Log($"[法术] {spell.UnitName} 发动！");

            switch (spell.CardData.EffectId)
            {
                // ── Kaisa spells ────────────────────────────────────────────
                case "hex_ray":
                    // Haste, 1 Blazing: deal 3 damage to target enemy
                    DealDamage(target, 3, gs);
                    break;

                case "void_seek":
                    // Haste, 1 Blazing: deal 4 damage to target enemy + draw 1
                    DealDamage(target, 4, gs);
                    DrawCards(owner, 1, gs);
                    break;

                case "stardrop":
                    // 2 Blazing: deal 3 damage to target twice
                    DealDamage(target, 3, gs);
                    if (target != null && target.CurrentHp > 0)
                        DealDamage(target, 3, gs);
                    break;

                case "starburst":
                    // 星芒凝汇 — "对最多两名单位各造成6点伤害"
                    // 第一个目标来自 SpellTargetPopup；第二个目标自动选另一个敌方单位（若存在）
                    DealDamage(target, 6, gs);
                    Starburst_DealToSecondTarget(owner, target, gs);
                    break;

                case "akasi_storm":
                    // 2 Radiant + 1 Blazing: deal 2 damage to random enemy unit, 6 times
                    AkasiStorm(owner, gs);
                    break;

                case "evolve_day":
                    // 1 Radiant: draw 4 cards
                    DrawCards(owner, 4, gs);
                    break;

                // ── Yi spells ────────────────────────────────────────────────
                case "rally_call":
                    // Haste: all friendly units enter active state + draw 1
                    RallyCall(owner, gs);
                    DrawCards(owner, 1, gs);
                    break;

                case "balance_resolve":
                    // Haste: draw 1 + summon 1 rune from rune deck
                    // DEV-3: skips the "conditional cost-2" part (needs targeting UI)
                    DrawCards(owner, 1, gs);
                    SummonRune(owner, gs);
                    break;

                case "slam":
                    // Haste + Echo: stun target enemy unit (spell shield blocks it)
                    Slam(target);
                    break;

                case "strike_ask_later":
                    // Haste, 2 Crushing: give target friendly unit +5 ATK this turn
                    StrikeAskLater(target, 5);
                    break;

                case "furnace_blast":
                    // Echo, 1 Blazing: deal 1 damage to up to 3 enemy units
                    FurnaceBlast(owner, gs);
                    break;

                case "time_warp":
                    // 3 Radiant: gain extra turn
                    gs.ExtraTurnPending = true;
                    Log("[时间扭曲] 获得额外回合！");
                    FWTCG.UI.GameEventBus.FireTimeWarpBanner(); // DEV-18b
                    break;

                case "divine_ray":
                    // 透体圣光 — "回响。对战场上的一名单位造成2点伤害。"
                    // 单次基础伤害；Echo 由 GameManager 驱动再次 CastSpell 实现（基础效果仍是一次伤害）
                    DealDamage(target, 2, gs);
                    break;

                case "guilty_pleasure":
                    // 罪恶快感 — "迅捷。弃置一张手牌。对战场上的一名单位造成等同于被弃置牌费用的伤害。"
                    GuiltyPleasure(owner, target, gs);
                    break;

                default:
                    Log($"[法术] 未实现效果: {spell.CardData.EffectId}");
                    break;
            }

            // dreaming_tree: draw 1 if this spell targeted a friendly unit on that BF
            _bfSys?.OnSpellTargetsFriendlyUnit(target, owner, gs);

            // Move spell from hand to discard
            gs.GetHand(owner).Remove(spell);
            gs.GetDiscard(owner).Add(spell);

            Log($"[法术] {spell.UnitName} 结算完毕，已弃置");
        }

        // ── Effect implementations ────────────────────────────────────────────

        private void DealDamage(UnitInstance target, int amount, GameState gs)
        {
            if (target == null) return;

            // Rule 721: SpellShield is an extra targeting COST, not a damage absorber.
            // By the time we are here, the caster has already paid the SpellShield cost
            // (checked in GameManager / AI targeting). Damage resolves normally.

            // void_gate: +1 damage to units on that BF
            if (_bfSys != null)
                amount += _bfSys.GetSpellDamageBonus(target, gs);

            target.CurrentHp -= amount;
            Log($"[伤害] {target.UnitName} 受到 {amount} 点法术伤害（剩余HP: {target.CurrentHp}）");
            GameManager.FireUnitDamaged(target, amount, _currentSpellName);

            if (target.CurrentHp <= 0)
                RemoveDeadUnit(target, gs);
        }

        private void RemoveDeadUnit(UnitInstance unit, GameState gs)
        {
            string owner = unit.Owner;

            // C-6: Guardian Angel death replacement
            if (TryGuardianProtectFromSpell(unit, owner, gs))
            {
                return;
            }

            // Fire BEFORE removal so GameUI can play death animation on the still-visible CardView (DEV-17)
            GameManager.FireUnitDied(unit);

            // Check base
            if (gs.GetBase(owner).Remove(unit))
            {
                gs.GetDiscard(owner).Add(unit);
                Log($"[死亡] {unit.UnitName} 从基地阵亡");
                return;
            }

            // Check each battlefield
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                List<UnitInstance> bfUnits = owner == GameRules.OWNER_PLAYER
                    ? gs.BF[i].PlayerUnits
                    : gs.BF[i].EnemyUnits;

                if (bfUnits.Remove(unit))
                {
                    gs.GetDiscard(owner).Add(unit);
                    Log($"[死亡] {unit.UnitName} 从战场{i + 1}阵亡");
                    UpdateBFControl(i, gs);
                    return;
                }
            }
        }

        /// <summary>
        /// C-6 守护天使死亡替换：法术致死时若附着 guardian_equip，改为销毁装备 + 单位回基地休眠。
        /// </summary>
        private bool TryGuardianProtectFromSpell(UnitInstance unit, string owner, GameState gs)
        {
            var equip = unit.AttachedEquipment;
            if (equip == null) return false;
            if (equip.CardData.EffectId != "guardian_equip") return false;

            // 销毁装备
            gs.GetDiscard(owner).Add(equip);
            int bonus = equip.CardData.EquipAtkBonus;
            if (bonus > 0)
                unit.CurrentAtk = Mathf.Max(0, unit.CurrentAtk - bonus);
            unit.AttachedEquipment = null;
            equip.AttachedTo = null;

            // 从原位置移除
            if (gs.GetBase(owner).Remove(unit))
            {
                // already in base, move back with exhausted state
            }
            else
            {
                for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
                {
                    var bfUnits = owner == GameRules.OWNER_PLAYER
                        ? gs.BF[i].PlayerUnits : gs.BF[i].EnemyUnits;
                    if (bfUnits.Remove(unit))
                    {
                        UpdateBFControl(i, gs);
                        break;
                    }
                }
            }

            // 恢复并回基地休眠
            unit.CurrentHp = unit.CurrentAtk;
            unit.Exhausted = true;
            unit.TempAtkBonus = 0;
            unit.Stunned = false;
            gs.GetBase(owner).Add(unit);

            Log($"[守护天使] {equip.UnitName} 被摧毁，保护 {unit.UnitName} 休眠返回基地");
            return true;
        }

        private void UpdateBFControl(int bfIdx, GameState gs)
        {
            BattlefieldState bf = gs.BF[bfIdx];
            bool hasPlayer = bf.PlayerUnits.Count > 0;
            bool hasEnemy  = bf.EnemyUnits.Count > 0;

            if (hasPlayer && !hasEnemy)
                bf.Ctrl = GameRules.OWNER_PLAYER;
            else if (hasEnemy && !hasPlayer)
                bf.Ctrl = GameRules.OWNER_ENEMY;
            // Both present or neither: keep current ctrl
        }

        private void DrawCards(string owner, int count, GameState gs)
        {
            List<UnitInstance> deck    = gs.GetDeck(owner);
            List<UnitInstance> hand    = gs.GetHand(owner);
            List<UnitInstance> discard = gs.GetDiscard(owner);
            int drawn = 0;

            for (int i = 0; i < count; i++)
            {
                if (deck.Count == 0)
                {
                    if (discard.Count == 0) break;
                    // Shuffle discard back (no burnout for spell draws)
                    deck.AddRange(discard);
                    discard.Clear();
                }
                if (deck.Count == 0) break;
                hand.Add(deck[0]);
                deck.RemoveAt(0);
                drawn++;
            }

            Log($"[法术] {DisplayName(owner)} 摸 {drawn} 张牌（手牌 {hand.Count}）");
        }

        private void AkasiStorm(string owner, GameState gs)
        {
            string enemy = gs.Opponent(owner);

            // Collect all enemy units in play
            var allEnemies = new List<UnitInstance>(gs.GetBase(enemy));
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                List<UnitInstance> bfUnits = enemy == GameRules.OWNER_PLAYER
                    ? gs.BF[i].PlayerUnits
                    : gs.BF[i].EnemyUnits;
                allEnemies.AddRange(bfUnits);
            }

            if (allEnemies.Count == 0)
            {
                Log("[狂暴之风] 无敌方单位可击");
                return;
            }

            // Deal 2 damage to a random enemy, 6 times (ignore spell shield for area effects)
            var dead = new List<UnitInstance>();
            for (int hit = 0; hit < 6; hit++)
            {
                var alive = allEnemies.FindAll(u => u.CurrentHp > 0);
                if (alive.Count == 0) break;
                UnitInstance picked = alive[Random.Range(0, alive.Count)];
                picked.CurrentHp -= 2;
                Log($"[狂暴之风] 第{hit + 1}击 → {picked.UnitName}（剩余HP: {picked.CurrentHp}）");
                GameManager.FireUnitDamaged(picked, 2, _currentSpellName);
                if (picked.CurrentHp <= 0 && !dead.Contains(picked))
                    dead.Add(picked);
            }

            foreach (UnitInstance u in dead)
                RemoveDeadUnit(u, gs);
        }

        private void FurnaceBlast(string owner, GameState gs)
        {
            string enemy = gs.Opponent(owner);
            var allEnemies = new List<UnitInstance>(gs.GetBase(enemy));
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                List<UnitInstance> bfUnits = enemy == GameRules.OWNER_PLAYER
                    ? gs.BF[i].PlayerUnits : gs.BF[i].EnemyUnits;
                allEnemies.AddRange(bfUnits);
            }

            int hits = Mathf.Min(3, allEnemies.Count);
            var dead = new List<UnitInstance>();
            for (int h = 0; h < hits; h++)
            {
                UnitInstance t = allEnemies[h];
                DealDamage(t, 1, gs);
                if (t.CurrentHp <= 0 && !dead.Contains(t))
                    dead.Add(t);
            }
            Log($"[熔炉烈焰] 对 {hits} 个敌方单位各造成1点伤害");
        }

        private void RallyCall(string owner, GameState gs)
        {
            int count = 0;

            foreach (UnitInstance u in gs.GetBase(owner))
                if (u.Exhausted) { u.Exhausted = false; count++; }

            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                List<UnitInstance> bfUnits = owner == GameRules.OWNER_PLAYER
                    ? gs.BF[i].PlayerUnits
                    : gs.BF[i].EnemyUnits;
                foreach (UnitInstance u in bfUnits)
                    if (u.Exhausted) { u.Exhausted = false; count++; }
            }

            Log($"[集结号令] {count} 个单位重新活跃");
        }

        private void SummonRune(string owner, GameState gs)
        {
            List<RuneInstance> runeDeck = gs.GetRuneDeck(owner);
            List<RuneInstance> runes    = gs.GetRunes(owner);

            if (runeDeck.Count > 0 && runes.Count < GameRules.MAX_RUNES_IN_PLAY)
            {
                RuneInstance r = runeDeck[0];
                runeDeck.RemoveAt(0);
                runes.Add(r);
                Log($"[平衡意志] 召出符文 {r.RuneType.ToChinese()}（符文区共 {runes.Count} 张）");
            }
            else
            {
                Log("[平衡意志] 符文牌库已空或符文区已满，无法召出符文");
            }
        }

        private void Slam(UnitInstance target)
        {
            if (target == null) return;

            // Rule 721: SpellShield is an extra targeting COST, not a stun-blocker.
            // The targeting cost was already paid before CastSpell was called.
            target.Stunned = true;
            Log($"[冲击] {target.UnitName} 被眩晕（本回合无法攻击）");
        }

        private void StrikeAskLater(UnitInstance target, int bonus)
        {
            if (target == null) return;
            target.TempAtkBonus += bonus;
            Log($"[先斩后奏] {target.UnitName} 获得+{bonus}战力（本回合）");
        }

        // ── guilty_pleasure 动态伤害 ──────────────────────────────────────────

        /// <summary>
        /// 罪恶快感：弃1张手牌，对目标造成等于弃牌费用的伤害。
        /// 优先弃非法术牌（避免丢弃未结算的反应牌），手牌空则跳过伤害。
        /// </summary>
        private void GuiltyPleasure(string owner, UnitInstance target, GameState gs)
        {
            if (target == null) return;
            var hand = gs.GetHand(owner);
            if (hand.Count == 0)
            {
                Log("[罪恶快感] 手牌为空，无牌可弃");
                return;
            }

            UnitInstance toDiscard = null;
            foreach (var c in hand)
            {
                if (!c.CardData.IsSpell) { toDiscard = c; break; }
            }
            if (toDiscard == null) toDiscard = hand[0];

            int dmg = toDiscard.CardData.Cost;
            hand.Remove(toDiscard);
            gs.GetDiscard(owner).Add(toDiscard);
            Log($"[罪恶快感] 弃置 {toDiscard.UnitName}（费用{dmg}）");

            if (dmg > 0)
                DealDamage(target, dmg, gs);
            else
                Log("[罪恶快感] 弃牌费用为0，无伤害");
        }

        // ── starburst 二目标辅助 ──────────────────────────────────────────────

        /// <summary>
        /// 星芒凝汇第二目标：自动挑选与第一目标不同的另一个敌方存活单位并造成 6 点伤害。
        /// </summary>
        private void Starburst_DealToSecondTarget(string owner, UnitInstance first, GameState gs)
        {
            string enemy = gs.Opponent(owner);
            var candidates = new List<UnitInstance>(gs.GetBase(enemy));
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                var bfUnits = enemy == GameRules.OWNER_PLAYER
                    ? gs.BF[i].PlayerUnits : gs.BF[i].EnemyUnits;
                candidates.AddRange(bfUnits);
            }

            UnitInstance second = null;
            foreach (var u in candidates)
            {
                if (u == first) continue;
                if (u.CurrentHp <= 0) continue;
                if (u.HasSpellShield) continue; // simplified: skip shielded secondary
                second = u;
                break;
            }

            if (second != null)
            {
                DealDamage(second, 6, gs);
            }
            else
            {
                Log("[星芒凝汇] 无第二合法目标，跳过第二次伤害");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string DisplayName(string owner) =>
            owner == GameRules.OWNER_PLAYER ? "玩家" : "AI";

        private void Log(string msg)
        {
            Debug.Log(msg);
            OnSpellLog?.Invoke(msg);
        }
    }
}
