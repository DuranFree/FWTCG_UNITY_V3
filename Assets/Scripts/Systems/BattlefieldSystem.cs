using System;
using System.Collections.Generic;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;

namespace FWTCG.Systems
{
    /// <summary>
    /// Handles all 19 battlefield card special abilities.
    ///
    /// Trigger points:
    ///   Score modifier  — ScoreManager.AddScore (forgotten_monument, ascending_stairs)
    ///   Hold phase      — TurnManager.DoStart    (altar_unity, aspirant_climb, bandle_tree,
    ///                                              strength_obelisk, star_peak)
    ///   Unit enter BF   — CombatSystem.MoveUnit  (trifarian_warcamp)
    ///   Unit leave BF   — CombatSystem.MoveUnit  (back_alley_bar)
    ///   Combat start    — CombatSystem.TriggerCombat  (reckoner_arena)
    ///   Conquest        — CombatSystem.TriggerCombat  (hirana, reaver_row, zaun_undercity,
    ///                                                   strength_obelisk, thunder_rune)
    ///   Defense failure — CombatSystem.TriggerCombat  (sunken_temple)
    ///   Recall block    — CombatSystem.RecallUnit      (vile_throat_nest)
    ///   Direct-play     — GameManager                  (rockfall_path)
    ///   Spell damage+   — SpellSystem.DealDamage       (void_gate)
    ///   Spell on ally   — SpellSystem.CastSpell        (dreaming_tree)
    ///
    /// DEV-6 simplifications:
    ///   - aspirant_climb: auto-applies to first eligible base unit (no player choice UI)
    ///   - star_peak: auto-summons without confirmation
    ///   - hirana: auto-consumes first buff-token unit
    ///   - reaver_row: auto-recovers first eligible discard unit
    ///   - zaun_undercity: auto-discards first hand card
    ///   - thunder_rune: auto-recycles first tapped rune
    ///   - sunken_temple: auto-triggers if mana >= 2
    ///   - bandle_tree: uses distinct rune types as proxy for region diversity
    ///   - back_alley_bar / trifarian_warcamp: player's units only (matches JS behaviour)
    /// </summary>
    public class BattlefieldSystem : MonoBehaviour
    {
        public static event Action<string> OnBattlefieldLog;

        // ── Helper ────────────────────────────────────────────────────────────

        /// <summary>Returns the card ID for the given battlefield index.</summary>
        private string GetBFId(int bfIndex, GameState gs) =>
            gs.BFNames != null && bfIndex < gs.BFNames.Length ? gs.BFNames[bfIndex] : "";

        /// <summary>
        /// Show the spell-style full-screen showcase for a battlefield card's triggered effect.
        /// Used only for rare events (conquest / defense failure) — not for per-turn hold effects
        /// to avoid spamming the player. Constructs a transient UnitInstance purely for display.
        /// </summary>
        private static void FireBFShowcase(string bfKey, string owner)
        {
            if (string.IsNullOrEmpty(bfKey)) return;
            if (FWTCG.UI.SpellShowcaseUI.Instance == null) return;
            var card = Resources.Load<CardData>($"Cards/BF/{bfKey}");
            if (card == null) return;
            // uid = -1 marks as display-only; no system tracks BF display units
            var display = new UnitInstance(-1, card, owner);
            FWTCG.UI.SpellShowcaseUI.Instance.ShowAsync(display, owner);
        }

        // ── Score modifiers ───────────────────────────────────────────────────

        /// <summary>
        /// Returns true if this hold-score attempt should be blocked.
        /// Applies: forgotten_monument (block hold before round 2).
        /// </summary>
        public bool ShouldBlockHoldScore(int bfId, GameState gs)
        {
            if (GetBFId(bfId, gs) == "forgotten_monument" && gs.Round < 2)
            {
                Log("[遗忘丰碑] 第三回合前无法从此战场得据守分！");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns extra points to add when scoring hold or conquest on this BF.
        /// (目前无卡面效果会在此加分；攀圣长阶已改为胜利门槛 +1，见 EffectiveWinScore。)
        /// </summary>
        public int GetBonusScorePoints(int bfId, string scoreType, GameState gs)
        {
            return 0;
        }

        /// <summary>
        /// Rule 101 / 攀圣长阶：任一战场为 ascending_stairs 时，获胜所需分数 +1。
        /// </summary>
        public static int EffectiveWinScore(GameState gs)
        {
            int bonus = 0;
            if (gs != null && gs.BFNames != null)
            {
                for (int i = 0; i < gs.BFNames.Length; i++)
                {
                    if (gs.BFNames[i] == "ascending_stairs") bonus += 1;
                }
            }
            return GameRules.WIN_SCORE + bonus;
        }

        // ── Hold phase effects ─────────────────────────────────────────────────

        /// <summary>
        /// Called by TurnManager.DoStart after awarding hold score for a BF.
        /// Triggers: altar_unity, aspirant_climb, bandle_tree, strength_obelisk, star_peak.
        /// </summary>
        public void OnHoldPhaseEffects(int bfId, string owner, GameState gs)
        {
            switch (GetBFId(bfId, gs))
            {
                case "altar_unity":
                    AltarUnityHold(owner, gs);
                    break;
                case "aspirant_climb":
                    AspirantClimbHold(owner, gs);
                    break;
                case "bandle_tree":
                    BandleTreeHold(owner, gs);
                    break;
                case "strength_obelisk":
                    StrengthObeliskExtra(owner, gs);
                    break;
                case "star_peak":
                    StarPeakHold(owner, gs);
                    break;
            }
        }

        // ── Unit movement effects ─────────────────────────────────────────────

        /// <summary>
        /// Called when a player unit enters a battlefield.
        /// Applies: trifarian_warcamp (buff token on entry, player units only).
        /// </summary>
        public void OnUnitEnterBattlefield(UnitInstance unit, int bfId, string owner, GameState gs)
        {
            if (owner != GameRules.OWNER_PLAYER) return;
            if (GetBFId(bfId, gs) == "trifarian_warcamp")
            {
                ApplyBuffToken(unit);
                Log($"[崔法利战营] {unit.UnitName} 进入战营，获得增益指示物(+1战力)！");
            }
        }

        /// <summary>
        /// Called when a player unit leaves a battlefield (to another BF, not recall).
        /// Applies: back_alley_bar (+1 temp atk this turn, player units only).
        /// </summary>
        public void OnUnitLeaveBattlefield(UnitInstance unit, int fromBfId, string owner, GameState gs)
        {
            if (owner != GameRules.OWNER_PLAYER) return;
            if (GetBFId(fromBfId, gs) == "back_alley_bar")
            {
                unit.TempAtkBonus += 1;
                Log($"[暗巷酒吧] {unit.UnitName} 离开酒馆，本回合战力+1！");
            }
        }

        // ── Combat start effects ───────────────────────────────────────────────

        /// <summary>
        /// Called at the start of TriggerCombat before damage is computed.
        /// Applies: reckoner_arena (units with power >= 5 gain StrongAtk or Guard).
        /// </summary>
        public void OnCombatStart(int bfId, string attacker, GameState gs)
        {
            if (GetBFId(bfId, gs) != "reckoner_arena") return;

            BattlefieldState bf = gs.BF[bfId];
            string defender = gs.Opponent(attacker);

            GrantReckonerKeywords(bf.PlayerUnits.Count > 0 && attacker == GameRules.OWNER_PLAYER
                ? bf.PlayerUnits : bf.EnemyUnits.Count > 0 && attacker == GameRules.OWNER_ENEMY
                ? bf.EnemyUnits : new List<UnitInstance>(), true);

            GrantReckonerKeywords(attacker == GameRules.OWNER_PLAYER
                ? bf.EnemyUnits : bf.PlayerUnits, false);
        }

        // ── Post-conquest effects ──────────────────────────────────────────────

        /// <summary>
        /// Called after the attacker conquers a BF.
        /// Applies: hirana, reaver_row, zaun_undercity, strength_obelisk, thunder_rune.
        /// </summary>
        public void OnConquest(int bfId, string attacker, GameState gs)
        {
            string bfKey = GetBFId(bfId, gs);
            switch (bfKey)
            {
                case "hirana":
                    FireBFShowcase(bfKey, attacker);
                    HiranaConquest(attacker, gs);
                    break;
                case "reaver_row":
                    FireBFShowcase(bfKey, attacker);
                    ReaverRowConquest(attacker, gs);
                    break;
                case "zaun_undercity":
                    FireBFShowcase(bfKey, attacker);
                    ZaunUndercityConquest(attacker, gs);
                    break;
                case "strength_obelisk":
                    FireBFShowcase(bfKey, attacker);
                    StrengthObeliskExtra(attacker, gs);
                    break;
                case "thunder_rune":
                    FireBFShowcase(bfKey, attacker);
                    ThunderRuneConquest(attacker, gs);
                    break;
            }
        }

        /// <summary>
        /// Called when the player fails to defend (enemy conquers player-held BF).
        /// Applies: sunken_temple (pay 2 mana → draw 1, for the player).
        /// </summary>
        public void OnDefenseFailure(int bfId, string defender, GameState gs)
        {
            if (defender != GameRules.OWNER_PLAYER) return;
            if (GetBFId(bfId, gs) == "sunken_temple" && gs.PMana >= 2)
            {
                FireBFShowcase("sunken_temple", defender);
                gs.PMana -= 2;
                DrawCard(defender, gs);
                Log("[沉没神庙] 防守失败！支付2法力，抽1张牌。");
            }
        }

        // ── Passive checks ────────────────────────────────────────────────────

        /// <summary>
        /// Returns false if the given owner cannot recall a unit from this BF.
        /// Applies: vile_throat_nest.
        /// </summary>
        public bool CanRecallFromBattlefield(int bfId, GameState gs)
        {
            if (GetBFId(bfId, gs) == "vile_throat_nest")
            {
                Log("[卑鄙之喉的巢穴] 此处单位无法撤回基地！");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Returns false if a unit card cannot be played directly to this BF from hand.
        /// Applies: rockfall_path.
        /// </summary>
        public bool CanPlayDirectlyToBattlefield(int bfId, GameState gs)
        {
            if (GetBFId(bfId, gs) == "rockfall_path")
            {
                Log("[落岩之径] 禁止从手牌直接将单位打入此战场！");
                return false;
            }
            return true;
        }

        // ── Spell effects ─────────────────────────────────────────────────────

        /// <summary>
        /// Returns extra spell damage bonus for a target unit based on its BF card.
        /// Applies: void_gate (+1 damage to units on this BF).
        /// </summary>
        public int GetSpellDamageBonus(UnitInstance target, GameState gs)
        {
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                List<UnitInstance> bfUnits = target.Owner == GameRules.OWNER_PLAYER
                    ? gs.BF[i].PlayerUnits
                    : gs.BF[i].EnemyUnits;

                if (bfUnits.Contains(target) && GetBFId(i, gs) == "void_gate")
                    return 1;
            }
            return 0;
        }

        /// <summary>
        /// Called when a spell targets a friendly unit on a BF.
        /// Applies: dreaming_tree (draw 1 card, once per turn).
        /// caster is the owner casting the spell; target must be caster's own unit.
        /// </summary>
        public void OnSpellTargetsFriendlyUnit(UnitInstance target, string caster, GameState gs)
        {
            if (target == null || target.Owner != caster) return;
            if (gs.DreamingTreeTriggeredThisTurn) return;

            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                List<UnitInstance> bfUnits = caster == GameRules.OWNER_PLAYER
                    ? gs.BF[i].PlayerUnits
                    : gs.BF[i].EnemyUnits;

                if (bfUnits.Contains(target) && GetBFId(i, gs) == "dreaming_tree")
                {
                    gs.DreamingTreeTriggeredThisTurn = true;
                    DrawCard(caster, gs);
                    Log($"[梦幻树] 以友军为目标施法，抽1张牌！");
                    return;
                }
            }
        }

        // ── Private effect implementations ────────────────────────────────────

        private void AltarUnityHold(string owner, GameState gs)
        {
            // Summon a 1/1 recruit to base (exhausted)
            UnitInstance recruit = CreateToken("recruit", "新兵", 1, owner, gs);
            recruit.Exhausted = true;
            gs.GetBase(owner).Add(recruit);
            Log($"[团结祭坛] 据守！召唤1名1/1新兵到{DisplayName(owner)}基地。");
        }

        private void AspirantClimbHold(string owner, GameState gs)
        {
            // Pay 1 mana → first base unit gets +1 permanent atk (buff token)
            List<UnitInstance> baseUnits = gs.GetBase(owner);
            if (gs.GetMana(owner) >= 1 && baseUnits.Count > 0)
            {
                gs.AddMana(owner, -1);
                ApplyBuffToken(baseUnits[0]);
                Log($"[试炼者之阶] 据守！支付1法力，{baseUnits[0].UnitName} 获得+1战力增益。");
            }
        }

        private void BandleTreeHold(string owner, GameState gs)
        {
            // Check distinct rune types among all owner's units (proxy for region diversity)
            var runeTypes = new HashSet<RuneType>();
            CollectUnitRuneTypes(gs.GetBase(owner), runeTypes);
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                List<UnitInstance> bfUnits = owner == GameRules.OWNER_PLAYER
                    ? gs.BF[i].PlayerUnits : gs.BF[i].EnemyUnits;
                CollectUnitRuneTypes(bfUnits, runeTypes);
            }

            if (runeTypes.Count >= 3)
            {
                gs.AddMana(owner, 1);
                Log($"[班德尔城神树] 据守！场上存在≥3种符能特性，{DisplayName(owner)}获得1法力！");
            }
        }

        private void StrengthObeliskExtra(string owner, GameState gs)
        {
            // Extra rune from rune deck
            List<RuneInstance> runeDeck = gs.GetRuneDeck(owner);
            List<RuneInstance> runes = gs.GetRunes(owner);
            if (runeDeck.Count > 0 && runes.Count < GameRules.MAX_RUNES_IN_PLAY)
            {
                RuneInstance r = runeDeck[0];
                runeDeck.RemoveAt(0);
                runes.Add(r);
                Log($"[力量方尖碑] {DisplayName(owner)} 额外获得1张符文！");
            }
        }

        private void StarPeakHold(string owner, GameState gs)
        {
            // Summon 1 exhausted rune from rune deck
            List<RuneInstance> runeDeck = gs.GetRuneDeck(owner);
            List<RuneInstance> runes = gs.GetRunes(owner);
            if (runeDeck.Count > 0 && runes.Count < GameRules.MAX_RUNES_IN_PLAY)
            {
                RuneInstance r = runeDeck[0];
                runeDeck.RemoveAt(0);
                r.Tapped = true; // enters exhausted/tapped
                runes.Add(r);
                Log($"[星尖峰] 据守！{DisplayName(owner)} 召出1枚休眠符文。");
            }
        }

        private void HiranaConquest(string attacker, GameState gs)
        {
            // Consume first buff-token unit → draw 1
            List<UnitInstance> allUnits = GetAllUnits(attacker, gs);
            UnitInstance buffed = allUnits.Find(u => u.BuffTokens > 0);
            if (buffed != null)
            {
                buffed.BuffTokens--;
                // Rule 139.2: power floor 0, not 1
                buffed.CurrentAtk = Mathf.Max(0, buffed.CurrentAtk - 1);
                buffed.CurrentHp  = Mathf.Max(0, buffed.CurrentHp  - 1);
                DrawCard(attacker, gs);
                Log($"[希拉娜修道院] 征服！消耗 {buffed.UnitName} 的增益指示物，抽1张牌。");
            }
        }

        private void ReaverRowConquest(string attacker, GameState gs)
        {
            // Auto-recover first cost<=2 unit from discard
            List<UnitInstance> discard = gs.GetDiscard(attacker);
            UnitInstance target = discard.Find(u => !u.CardData.IsSpell && u.CardData.Cost <= 2);
            if (target != null)
            {
                discard.Remove(target);
                target.Exhausted = true;
                gs.GetBase(attacker).Add(target);
                Log($"[掠夺者之街] 征服！将 {target.UnitName} 从废牌堆捞回基地。");
            }
        }

        private void ZaunUndercityConquest(string attacker, GameState gs)
        {
            // Auto-discard first hand card → draw 1
            List<UnitInstance> hand = gs.GetHand(attacker);
            if (hand.Count > 0)
            {
                UnitInstance discarded = hand[0];
                hand.RemoveAt(0);
                gs.GetDiscard(attacker).Add(discarded);
                DrawCard(attacker, gs);
                Log($"[祖安地沟] 征服！弃置 {discarded.UnitName}，抽1张牌。");
            }
        }

        private void ThunderRuneConquest(string attacker, GameState gs)
        {
            // Auto-recycle first tapped rune back to rune deck top
            List<RuneInstance> runes = gs.GetRunes(attacker);
            RuneInstance tapped = runes.Find(r => r.Tapped);
            if (tapped != null)
            {
                runes.Remove(tapped);
                tapped.Tapped = false;
                gs.GetRuneDeck(attacker).Insert(0, tapped);
                Log($"[雷霆之纹] 征服！回收1枚符文至符文牌库顶。");
            }
        }

        private void GrantReckonerKeywords(List<UnitInstance> units, bool isAttacking)
        {
            foreach (UnitInstance u in units)
            {
                // Rule 139.2: use EffectiveAtk (Max(0, CurrentAtk+TempAtkBonus))
                int power = u.EffectiveAtk();
                if (power >= GameRules.STRONG_POWER_THRESHOLD)
                {
                    if (isAttacking && !u.HasStrongAtk)
                    {
                        u.HasStrongAtk = true;
                        Log($"[清算人竞技场] {u.UnitName}(进攻) 战力≥5，获得【强攻】！");
                    }
                    else if (!isAttacking && !u.HasGuard)
                    {
                        u.HasGuard = true;
                        Log($"[清算人竞技场] {u.UnitName}(防守) 战力≥5，获得【坚守】！");
                    }
                }
            }
        }

        // ── Shared helpers ────────────────────────────────────────────────────

        private void ApplyBuffToken(UnitInstance unit)
        {
            unit.BuffTokens++;
            unit.CurrentAtk++;
            unit.CurrentHp++;
        }

        private void DrawCard(string owner, GameState gs)
        {
            List<UnitInstance> deck = gs.GetDeck(owner);
            List<UnitInstance> hand = gs.GetHand(owner);
            if (deck.Count > 0)
            {
                hand.Add(deck[0]);
                deck.RemoveAt(0);
            }
        }

        private UnitInstance CreateToken(string id, string name, int atk, string owner, GameState gs)
        {
            // Create a minimal CardData proxy — tokens use a dedicated helper in tests
            // In runtime we create a ScriptableObject-less stand-in via a thin wrapper
            var tokenData = ScriptableObject.CreateInstance<FWTCG.Data.CardData>();
#if UNITY_EDITOR
            tokenData.EditorSetup(id, name, 1, atk, RuneType.Blazing, 0, "");
#endif
            return gs.MakeUnit(tokenData, owner);
        }

        private List<UnitInstance> GetAllUnits(string owner, GameState gs)
        {
            var all = new List<UnitInstance>(gs.GetBase(owner));
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                all.AddRange(owner == GameRules.OWNER_PLAYER
                    ? gs.BF[i].PlayerUnits
                    : gs.BF[i].EnemyUnits);
            }
            return all;
        }

        private void CollectUnitRuneTypes(List<UnitInstance> units, HashSet<RuneType> set)
        {
            foreach (UnitInstance u in units)
                set.Add(u.CardData.RuneType);
        }

        private void Log(string msg)
        {
            Debug.Log(msg);
            OnBattlefieldLog?.Invoke(msg);
            TurnManager.BroadcastMessage_Static(msg);
        }

        private string DisplayName(string owner) =>
            owner == GameRules.OWNER_PLAYER ? "玩家" : "AI";
    }
}
