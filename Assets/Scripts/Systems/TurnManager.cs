using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using FWTCG.AI;
using FWTCG.Core;
using FWTCG.UI;

namespace FWTCG.Systems
{
    /// <summary>
    /// Controls the six-phase turn flow:
    /// Awaken → Start(据守) → Summon(召出符文) → Draw(抽牌) → Action → End
    ///
    /// Player action phase waits for actionComplete flag set by GameManager.
    /// AI action phase delegates to SimpleAI.
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        public static event Action<string> OnPhaseChanged;
        public static event Action<string> OnMessage;
        public static event Action<string> OnBannerRequest;

        // ── Action-phase gate ─────────────────────────────────────────────────
        private bool _actionComplete = false;
        private bool _endTurnRequested = false;

        public string CurrentPhase => _gs != null ? _gs.Phase : GameRules.PHASE_AWAKEN;

        // ── References (injected by GameManager) ──────────────────────────────
        private GameState _gs;
        private ScoreManager _scoreMgr;
        private CombatSystem _combatSys;
        private SimpleAI _ai;
        private EntryEffectSystem _entryEffects;
        private SpellSystem _spellSys;
        private ReactiveSystem _reactiveSys;
        private ReactiveWindowUI _reactiveWindow;
        private LegendSystem _legendSys;
        private BattlefieldSystem _bfSys;

        public void Inject(GameState gs, ScoreManager score, CombatSystem combat, SimpleAI ai,
                           EntryEffectSystem entryEffects = null,
                           SpellSystem spellSys = null,
                           ReactiveSystem reactiveSys = null,
                           ReactiveWindowUI reactiveWindow = null,
                           LegendSystem legendSys = null,
                           BattlefieldSystem bfSys = null)
        {
            _gs = gs;
            _scoreMgr = score;
            _combatSys = combat;
            _ai = ai;
            _entryEffects = entryEffects;
            _spellSys = spellSys;
            _reactiveSys = reactiveSys;
            _reactiveWindow = reactiveWindow;
            _legendSys = legendSys;
            _bfSys = bfSys;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Signals that the player has finished their action phase.
        /// Called by GameManager when the "End Turn" button is pressed.
        /// </summary>
        public void EndTurn()
        {
            _actionComplete = true;
            _endTurnRequested = true;
        }

        /// <summary>
        /// Starts a full turn for the given owner. Returns when the turn ends.
        /// Called from GameManager's coroutine via StartCoroutine wrapper.
        /// </summary>
        public async Task StartTurn(string who, GameState gs)
        {
            _gs = gs;
            _actionComplete = false;
            _endTurnRequested = false;

            gs.Turn = who;
            gs.Phase = GameRules.PHASE_AWAKEN;

            Broadcast($"── 回合 {gs.Round + 1} [{DisplayName(who)}] 开始 ──");

            await DoAwaken(who, gs);
            if (gs.GameOver) return;

            await DoStart(who, gs);
            if (gs.GameOver) return;

            await DoSummon(who, gs);
            if (gs.GameOver) return;

            await DoDraw(who, gs);
            if (gs.GameOver) return;

            await DoAction(who, gs);
            if (gs.GameOver) return;

            await DoEndPhase(who, gs);
        }

        // ── Static broadcast / banner helpers ────────────────────────────────
        public static void BroadcastMessage_Static(string msg)
        {
            OnMessage?.Invoke(msg);
            Debug.Log(msg);
        }

        public static void ShowBanner_Static(string text)
        {
            OnBannerRequest?.Invoke(text);
            Debug.Log($"[Banner] {text}");
        }

        // ── Six phases ────────────────────────────────────────────────────────

        /// <summary>
        /// Phase 1 – Awaken: un-exhaust units, un-tap runes, reset schematic energy,
        /// clear per-turn tracking counters.
        /// </summary>
        private async Task DoAwaken(string who, GameState gs)
        {
            SetPhase(who, gs, GameRules.PHASE_AWAKEN);

            // Un-exhaust all units in base and on all battlefields
            foreach (UnitInstance u in gs.GetBase(who)) u.Exhausted = false;
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                List<UnitInstance> bfUnits = who == GameRules.OWNER_PLAYER
                    ? gs.BF[i].PlayerUnits
                    : gs.BF[i].EnemyUnits;
                foreach (UnitInstance u in bfUnits) u.Exhausted = false;
            }

            // Un-tap all runes
            foreach (RuneInstance r in gs.GetRunes(who)) r.Tapped = false;

            // Reset mana and schematic energy
            gs.SetMana(who, 0);
            gs.ResetSch(who);

            // Reset per-turn tracking
            gs.BFScoredThisTurn.Clear();
            gs.BFConqueredThisTurn.Clear();
            gs.CardsPlayedThisTurn = 0;
            gs.DreamingTreeTriggeredThisTurn = false;

            // Reset legend ability usage
            _legendSys?.ResetForTurn(who, gs);

            // Rule 728: Destroy ephemeral units from the PREVIOUS turn for BOTH players.
            // Ephemeral units have SummonedOnRound < gs.Round (they were created in a past round).
            DestroyEphemeralUnits(gs);

            Broadcast($"[觉醒] {DisplayName(who)} 符文解除横置，符能清零");
            await Delay(GameRules.PHASE_DELAY_MS);
        }

        /// <summary>
        /// Phase 2 – Start: For each battlefield controlled by the current player,
        /// award +1 hold score (据守得分).
        /// </summary>
        private async Task DoStart(string who, GameState gs)
        {
            SetPhase(who, gs, GameRules.PHASE_START);

            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                BattlefieldState bf = gs.BF[i];
                if (bf.Ctrl == who)
                {
                    Broadcast($"[据守] {DisplayName(who)} 控制战场{i + 1}，+1分");
                    _scoreMgr.AddScore(who, 1, GameRules.SCORE_TYPE_HOLD, i, gs);
                    if (gs.GameOver) return;

                    // Battlefield hold-phase triggered effects
                    _bfSys?.OnHoldPhaseEffects(i, who, gs);
                }
            }

            await Delay(GameRules.PHASE_DELAY_MS);
        }

        /// <summary>
        /// Phase 3 – Summon: Draw runes from rune deck into active rune zone.
        /// Second player draws 3 on their first turn; all others draw 2.
        /// </summary>
        private async Task DoSummon(string who, GameState gs)
        {
            SetPhase(who, gs, GameRules.PHASE_SUMMON);

            bool isSecondPlayerFirstTurn = (who != gs.First) && !gs.IsFirstTurnDone(who);
            int drawCount = isSecondPlayerFirstTurn
                ? GameRules.RUNES_FIRST_TURN_SECOND
                : GameRules.RUNES_PER_TURN;

            List<RuneInstance> runeDeck = gs.GetRuneDeck(who);
            List<RuneInstance> runes = gs.GetRunes(who);

            // Cap: max 12 runes in play
            int slotsAvailable = GameRules.MAX_RUNES_IN_PLAY - runes.Count;
            int actualDraw = Mathf.Min(drawCount, Mathf.Max(0, slotsAvailable));

            if (slotsAvailable <= 0)
                Broadcast($"[召符] {DisplayName(who)} 符文区已满（{runes.Count}/{GameRules.MAX_RUNES_IN_PLAY}）");

            int drawn = 0;
            for (int i = 0; i < actualDraw; i++)
            {
                if (runeDeck.Count == 0)
                {
                    Broadcast($"[召符] {DisplayName(who)} 符文牌库已空");
                    break;
                }
                RuneInstance r = runeDeck[0];
                runeDeck.RemoveAt(0);
                runes.Add(r);
                drawn++;
            }

            Broadcast($"[召符] {DisplayName(who)} 召出 {drawn} 张符文（共 {runes.Count} 张）");
            gs.SetFirstTurnDone(who, true);

            await Delay(GameRules.PHASE_DELAY_MS);
        }

        /// <summary>
        /// Phase 4 – Draw: Draw 1 card from deck.
        /// If deck is empty: shuffle discard into deck (burnout → opponent +1),
        /// then attempt to draw. If still empty, no card is drawn.
        /// </summary>
        private async Task DoDraw(string who, GameState gs)
        {
            SetPhase(who, gs, GameRules.PHASE_DRAW);

            List<UnitInstance> deck = gs.GetDeck(who);
            List<UnitInstance> hand = gs.GetHand(who);
            List<UnitInstance> discard = gs.GetDiscard(who);
            string opponent = gs.Opponent(who);

            if (deck.Count == 0)
            {
                if (discard.Count == 0)
                {
                    Broadcast($"[燃尽] {DisplayName(who)} 牌库和废牌堆均为空，无法抽牌");
                    // Rule 515.4.d: clear all pools even when no draw occurs
                    foreach (string p in new[] { GameRules.OWNER_PLAYER, GameRules.OWNER_ENEMY })
                    { gs.SetMana(p, 0); gs.ResetSch(p); }
                    await Delay(GameRules.PHASE_DELAY_MS);
                    return;
                }

                // Shuffle discard into deck
                Broadcast($"[燃尽] {DisplayName(who)} 牌库耗尽，洗牌！对手 +1分");
                ShuffleDiscard(deck, discard);
                _scoreMgr.AddScore(opponent, 1, GameRules.SCORE_TYPE_BURNOUT, null, gs);
                if (gs.GameOver) return;
            }

            if (deck.Count == 0)
            {
                Broadcast($"[抽牌] {DisplayName(who)} 无牌可抽");
                // Rule 515.4.d: clear all pools even when no draw occurs
                foreach (string p in new[] { GameRules.OWNER_PLAYER, GameRules.OWNER_ENEMY })
                { gs.SetMana(p, 0); gs.ResetSch(p); }
                await Delay(GameRules.PHASE_DELAY_MS);
                return;
            }

            UnitInstance drawn = deck[0];
            deck.RemoveAt(0);
            hand.Add(drawn);
            Broadcast($"[抽牌] {DisplayName(who)} 抽到 {drawn.UnitName}（手牌 {hand.Count}）");

            // Rule 515.4.d: clear ALL players' mana pools at end of draw phase
            foreach (string p in new[] { GameRules.OWNER_PLAYER, GameRules.OWNER_ENEMY })
            {
                gs.SetMana(p, 0);
                gs.ResetSch(p);
            }
            Broadcast("[抽牌结束] 所有玩家符文池清空");

            await Delay(GameRules.PHASE_DELAY_MS);
        }

        /// <summary>
        /// Phase 5 – Action: Player waits for input; AI takes automated actions.
        /// </summary>
        private async Task DoAction(string who, GameState gs)
        {
            SetPhase(who, gs, GameRules.PHASE_ACTION);
            _actionComplete = false;

            if (who == GameRules.OWNER_PLAYER)
            {
                Broadcast("[行动] 玩家回合 — 请操作（横置符文、打出单位、移动单位，完成后点击「结束回合」）");

                // Wait for player to click "End Turn" button
                // This is a busy-wait using async Task.Delay to avoid blocking the main thread
                while (!_actionComplete)
                {
                    await Task.Delay(100);
                    if (gs.GameOver) return;
                }
            }
            else
            {
                Broadcast("[行动] AI 回合思考中…");
                await _ai.TakeAction(gs, this, _combatSys, _scoreMgr, _entryEffects,
                                     _spellSys, _reactiveSys, _reactiveWindow,
                                     _legendSys, _bfSys);
            }

            // If any contested battlefields remain when turn ends, auto-resolve them
            // (handles AI turns and any duels the player chose not to initiate)
            if (!gs.GameOver)
            {
                _combatSys.ResolveAllBattlefields(who, gs, _scoreMgr);
                // DEV-17: wait for hit flash + death animations fired by ResolveAllBattlefields
                await Task.Delay(550);
            }
        }

        /// <summary>
        /// Phase 6 – End: Reset marked damage on all units, clear mana/energy, advance round.
        /// </summary>
        private async Task DoEndPhase(string who, GameState gs)
        {
            SetPhase(who, gs, GameRules.PHASE_END);

            // Reset HP (clear marked damage) on ALL units for BOTH players (Rule 627.5)
            ResetAllUnits(gs);

            // Clear mana and schematic energy
            gs.SetMana(who, 0);
            gs.ResetSch(who);

            Broadcast($"[结束] {DisplayName(who)} 回合结束，法力与符能清零");

            // Advance game state
            // Extra turn (time_warp): don't switch to opponent, give another turn to same player
            if (gs.ExtraTurnPending)
            {
                gs.ExtraTurnPending = false;
                gs.Turn = who;
                Broadcast($"[时间扭曲] {DisplayName(who)} 获得额外回合！");
            }
            else
            {
                string next = gs.Opponent(who);
                if (who == GameRules.OWNER_ENEMY)
                    gs.Round++;
                gs.Turn = next;
            }

            await Delay(GameRules.PHASE_DELAY_MS);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetPhase(string who, GameState gs, string phase)
        {
            gs.Phase = phase;
            string msg = $"[阶段] {DisplayName(who)} → {phase.ToUpper()}";
            OnPhaseChanged?.Invoke(msg);
            Debug.Log(msg);
        }

        private void Broadcast(string msg)
        {
            OnMessage?.Invoke(msg);
            Debug.Log(msg);
        }

        private static async Task Delay(int ms)
        {
            await Task.Delay(ms);
        }

        private void ResetAllUnits(GameState gs)
        {
            foreach (string owner in new[] { GameRules.OWNER_PLAYER, GameRules.OWNER_ENEMY })
            {
                foreach (UnitInstance u in gs.GetBase(owner))
                    u.ResetEndOfTurn();

                for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
                {
                    List<UnitInstance> bfUnits = owner == GameRules.OWNER_PLAYER
                        ? gs.BF[i].PlayerUnits
                        : gs.BF[i].EnemyUnits;
                    foreach (UnitInstance u in bfUnits)
                        u.ResetEndOfTurn();
                }
            }
        }

        private void ShuffleDiscard(List<UnitInstance> deck, List<UnitInstance> discard)
        {
            // Fisher-Yates shuffle
            for (int i = discard.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                UnitInstance temp = discard[i];
                discard[i] = discard[j];
                discard[j] = temp;
            }
            deck.AddRange(discard);
            discard.Clear();
        }

        private string DisplayName(string owner) =>
            owner == GameRules.OWNER_PLAYER ? "玩家" : "AI";

        /// <summary>
        /// Rule 728: Destroy ephemeral units whose SummonedOnRound is before the current round.
        /// Called at the start of Awaken for both players.
        /// </summary>
        private void DestroyEphemeralUnits(GameState gs)
        {
            foreach (string owner in new[] { GameRules.OWNER_PLAYER, GameRules.OWNER_ENEMY })
            {
                // Check base
                var baseList = gs.GetBase(owner);
                for (int i = baseList.Count - 1; i >= 0; i--)
                {
                    UnitInstance u = baseList[i];
                    if (u.IsEphemeral && u.SummonedOnRound < gs.Round)
                    {
                        GameManager.FireUnitDied(u);
                        baseList.RemoveAt(i);
                        Broadcast($"[瞬息] {u.UnitName}({DisplayName(owner)}) 回合开始前销毁");
                    }
                }

                // Check battlefields
                for (int bfId = 0; bfId < GameRules.BATTLEFIELD_COUNT; bfId++)
                {
                    List<UnitInstance> bfUnits = owner == GameRules.OWNER_PLAYER
                        ? gs.BF[bfId].PlayerUnits
                        : gs.BF[bfId].EnemyUnits;
                    for (int i = bfUnits.Count - 1; i >= 0; i--)
                    {
                        UnitInstance u = bfUnits[i];
                        if (u.IsEphemeral && u.SummonedOnRound < gs.Round)
                        {
                            GameManager.FireUnitDied(u);
                            bfUnits.RemoveAt(i);
                            Broadcast($"[瞬息] {u.UnitName}({DisplayName(owner)}) 回合开始前销毁");
                        }
                    }
                }
            }
        }
    }
}
