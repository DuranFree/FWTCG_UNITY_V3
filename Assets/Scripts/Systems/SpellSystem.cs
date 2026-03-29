using System.Collections.Generic;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;

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

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves a spell. Pass target=null for non-targeting spells (SpellTargetType.None).
        /// Removes spell from owner's hand and adds to owner's discard pile.
        /// </summary>
        public void CastSpell(UnitInstance spell, string owner, UnitInstance target, GameState gs)
        {
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
                    // 2 Radiant: deal 6 damage to target enemy
                    // DEV-3: simplified from "2 targets" to 1 target
                    DealDamage(target, 6, gs);
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

                default:
                    Log($"[法术] 未实现效果: {spell.CardData.EffectId}");
                    break;
            }

            // Move spell from hand to discard
            gs.GetHand(owner).Remove(spell);
            gs.GetDiscard(owner).Add(spell);

            Log($"[法术] {spell.UnitName} 结算完毕，已弃置");
        }

        // ── Effect implementations ────────────────────────────────────────────

        private void DealDamage(UnitInstance target, int amount, GameState gs)
        {
            if (target == null) return;

            // Spell shield: absorbs one instance of spell damage
            if (target.HasSpellShield)
            {
                target.HasSpellShield = false;
                Log($"[法盾] {target.UnitName} 法术护盾抵消了 {amount} 点伤害");
                return;
            }

            target.CurrentHp -= amount;
            Log($"[伤害] {target.UnitName} 受到 {amount} 点法术伤害（剩余HP: {target.CurrentHp}）");

            if (target.CurrentHp <= 0)
                RemoveDeadUnit(target, gs);
        }

        private void RemoveDeadUnit(UnitInstance unit, GameState gs)
        {
            string owner = unit.Owner;

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
                if (picked.CurrentHp <= 0 && !dead.Contains(picked))
                    dead.Add(picked);
            }

            foreach (UnitInstance u in dead)
                RemoveDeadUnit(u, gs);
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
                Log($"[平衡意志] 召出符文 {r.RuneType}（符文区共 {runes.Count} 张）");
            }
            else
            {
                Log("[平衡意志] 符文牌库已空或符文区已满，无法召出符文");
            }
        }

        private void Slam(UnitInstance target)
        {
            if (target == null) return;

            if (target.HasSpellShield)
            {
                target.HasSpellShield = false;
                Log($"[冲击] {target.UnitName} 法术护盾抵消了眩晕");
                return;
            }

            target.Stunned = true;
            Log($"[冲击] {target.UnitName} 被眩晕（本回合无法攻击）");
        }

        private void StrikeAskLater(UnitInstance target, int bonus)
        {
            if (target == null) return;
            target.TempAtkBonus += bonus;
            Log($"[先斩后奏] {target.UnitName} 获得+{bonus}战力（本回合）");
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
