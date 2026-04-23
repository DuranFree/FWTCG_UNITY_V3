using System;
using UnityEngine;
using FWTCG.Core;

namespace FWTCG.UI
{
    /// <summary>
    /// Central event bus for visual feedback events (DEV-18b).
    ///
    /// All game systems Fire events here; GameUI subscribes and routes them
    /// to FloatText / EventBanner. Keeps UI logic out of game systems.
    ///
    /// Float text events carry a UnitInstance (position resolved via CardView
    /// in GameUI) or a named zone string (score area, rune area, etc.).
    /// Event banner events are screen-center small banners with a text + duration.
    /// </summary>
    public static class GameEventBus
    {
        // ── Float text on a specific unit ────────────────────────────────────
        /// <summary>
        /// Fire to show a floating text badge on a unit's CardView.
        /// GameUI resolves position via FindCardView(unit).
        /// </summary>
        public static event Action<UnitInstance, string, Color> OnUnitFloatText;

        // ── Float text at a named zone ────────────────────────────────────────
        /// <summary>
        /// Fire to show a floating text at a named zone ("score_player",
        /// "score_enemy", "rune_player", "rune_enemy").
        /// GameUI resolves canvas position from zone name.
        /// </summary>
        public static event Action<string, string, Color> OnZoneFloatText;

        // ── Small event banner (screen-center, queued) ────────────────────────
        /// <summary>
        /// Fire to queue a small screen-center event banner.
        /// duration: seconds to show banner (default 1.5s).
        /// large: true for important events (evolve, burnout, time warp → 2.5s banner).
        /// </summary>
        public static event Action<string, float, bool> OnEventBanner;

        // ── Spell duel banner (DEV-19) ────────────────────────────────────────
        /// <summary>
        /// Fired when a spell duel starts (player spell played + reaction window opens).
        /// GameUI shows a prominent "⚡ 法术对决！" overlay banner.
        /// </summary>
        public static event Action OnDuelBanner;

        // ── Clear all banners / toasts (turn change) ──────────────────────────
        /// <summary>
        /// Fired at the start of each new turn (AWAKEN phase).
        /// EventBanner and ToastUI immediately clear their queues and hide.
        /// </summary>
        public static event Action OnClearBanners;

        // ── Delay next EventBanner drain ──────────────────────────────────────
        /// <summary>
        /// Tells EventBanner to wait <paramref name="seconds"/> before draining
        /// the next queued batch (used after DuelBanner so combat EventBanners
        /// don't appear until the big banner has fully cleared the screen).
        /// </summary>
        public static event Action<float> OnSetBannerDelay;

        // ── Fire helpers ──────────────────────────────────────────────────────

        public static void FireUnitFloatText(UnitInstance unit, string text, Color color)
            => OnUnitFloatText?.Invoke(unit, text, color);

        public static void FireZoneFloatText(string zone, string text, Color color)
            => OnZoneFloatText?.Invoke(zone, text, color);

        public static void FireEventBanner(string text, float duration = 1.0f, bool large = false)
            => OnEventBanner?.Invoke(text, duration, large);

        /// <summary>Fires the spell duel banner. Called when player's spell enters the duel window. DEV-19.</summary>
        public static void FireDuelBanner() => OnDuelBanner?.Invoke();

        /// <summary>Clears all queued banners and toasts immediately. Call at turn start.</summary>
        public static void FireClearBanners() => OnClearBanners?.Invoke();

        /// <summary>
        /// Sets a one-shot drain delay on EventBanner: the next batch of queued
        /// banners will wait <paramref name="seconds"/> before starting to show.
        /// Call just before CheckAndResolveCombat so combat EventBanners appear
        /// only after the DuelBanner has fully left the screen.
        /// </summary>
        public static void FireSetBannerDelay(float seconds) => OnSetBannerDelay?.Invoke(seconds);

        // ── Game-manager game events (DEV-27: migrated from GameManager static events) ──

        /// <summary>Fired when a card play fails. Subscribers shake the failing CardView.</summary>
        public static event Action<UnitInstance> OnCardPlayFailed;
        public static void FireCardPlayFailed(UnitInstance unit) => OnCardPlayFailed?.Invoke(unit);

        /// <summary>Fired when a unit takes damage: (unit, amount, sourceName). Used for red flash + shake.</summary>
        public static event Action<UnitInstance, int, string> OnUnitDamaged;
        public static void FireUnitDamaged(UnitInstance unit, int damage, string source = "")
            => OnUnitDamaged?.Invoke(unit, damage, source);

        /// <summary>Fired just BEFORE a unit is removed from game state (HP = 0). Used for death animation.</summary>
        public static event Action<UnitInstance> OnUnitDied;
        public static void FireUnitDied(UnitInstance unit) => OnUnitDied?.Invoke(unit);

        /// <summary>Fired when a card is successfully played by any player. Used for board flash.</summary>
        public static event Action<UnitInstance, string> OnCardPlayed;
        public static void FireCardPlayed(UnitInstance unit, string owner) => OnCardPlayed?.Invoke(unit, owner);

        /// <summary>
        /// DEV-32 A6: 单位进入基地/战场。EntryEffectSystem 订阅处理入场效果。
        /// 替代原先 GameManager/SimpleAI 对 EntryEffectSystem 的直接调用。
        /// </summary>
        public static event Action<UnitInstance, string> OnUnitEntered;
        public static void FireUnitEntered(UnitInstance unit, string owner)
            => OnUnitEntered?.Invoke(unit, owner);

        /// <summary>
        /// DEV-32 A6: 一批单位阵亡（结算绝念）。bfId=-1 表示基地阵亡。
        /// DeathwishSystem 订阅处理 alert_sentinel / wailing_poro 等死亡触发。
        /// </summary>
        public static event Action<System.Collections.Generic.List<UnitInstance>, int> OnUnitsDied;
        public static void FireUnitsDied(System.Collections.Generic.List<UnitInstance> units, int bfId)
            => OnUnitsDied?.Invoke(units, bfId);

        /// <summary>Fired to show a hint toast in the UI. Any system may call FireHintToast.</summary>
        public static event Action<string> OnHintToast;
        public static void FireHintToast(string msg) => OnHintToast?.Invoke(msg);

        // ── Unit death position event (DEV-21) ────────────────────────────────
        /// <summary>
        /// Fired by GameUI when a unit dies, carrying the unit's canvas-local position.
        /// SpellVFX subscribes to spawn death-explosion particles at the exact spot.
        /// </summary>
        public static event Action<UnitInstance, Vector2> OnUnitDiedAtPos;
        public static void FireUnitDiedAtPos(UnitInstance unit, Vector2 canvasPos)
            => OnUnitDiedAtPos?.Invoke(unit, canvasPos);

        // ── Convenience: score float texts ───────────────────────────────────

        /// <summary>Show "+X分" golden float in the owner's score zone.</summary>
        public static void FireScoreFloat(string owner, int pts)
        {
            string zone = owner == GameRules.OWNER_PLAYER ? "score_player" : "score_enemy";
            string text = $"+{pts}分";
            FireZoneFloatText(zone, text, GameColors.ScorePulseColor);
        }

        // ── Convenience: rune float texts ─────────────────────────────────────

        public static void FireRuneTapFloat(string owner)
        {
            string zone = owner == GameRules.OWNER_PLAYER ? "rune_player" : "rune_enemy";
            FireZoneFloatText(zone, "法力+1", GameColors.ManaColor);
        }

        public static void FireRuneRecycleFloat(string owner)
        {
            string zone = owner == GameRules.OWNER_PLAYER ? "rune_player" : "rune_enemy";
            FireZoneFloatText(zone, "符能+1", GameColors.SchColor);
        }

        // ── Convenience: unit ATK buff ────────────────────────────────────────

        public static void FireUnitAtkBuff(UnitInstance unit, int delta)
        {
            if (unit == null) return;
            string text = delta >= 0 ? $"+{delta}战力" : $"{delta}战力";
            Color col = delta >= 0 ? GameColors.BuffColor : GameColors.DebuffColor;
            FireUnitFloatText(unit, text, col);
        }

        // ── Convenience: known banner events ─────────────────────────────────

        public static void FireDeathwishBanner(string unitName, string effectDesc)
            => FireEventBanner($"绝念：{unitName} — {effectDesc}", 1.0f);

        public static void FireEntryEffectBanner(string unitName, string effectDesc)
            => FireEventBanner($"{unitName}：{effectDesc}", 1.0f);

        public static void FireHoldScoreBanner()
            => FireEventBanner("据守 +1分", 1.0f);

        public static void FireConquerScoreBanner()
            => FireEventBanner("征服！+1分", 1.0f);

        // ── DOT-8: Turn changed (AWAKEN phase start) ─────────────────────────
        /// <summary>
        /// Fired at the start of each turn (AWAKEN phase).
        /// GameUI subscribes to show the "你的回合" sweep banner and animate mana fill.
        /// </summary>
        public static event Action<string, int> OnTurnChanged; // owner, round
        public static void FireTurnChanged(string owner, int round) => OnTurnChanged?.Invoke(owner, round);

        // ── DOT-8: AOE multi-target highlight ────────────────────────────────
        /// <summary>
        /// Fired when a skill/spell hits multiple targets simultaneously.
        /// CombatAnimator subscribes to stagger-highlight each target with 0.1s delay.
        /// </summary>
        public static event Action<UnitInstance[]> OnAOETargets;
        public static void FireAOETargets(UnitInstance[] targets) => OnAOETargets?.Invoke(targets);

        // ── DOT-8: Legend skill fired (for closeup) ──────────────────────────
        /// <summary>
        /// Fired when a legend skill is successfully activated.
        /// LegendSkillShowcase subscribes to play fullscreen darken + card zoom.
        /// </summary>
        public static event Action<FWTCG.Core.LegendInstance, string> OnLegendSkillFired; // legend, owner
        public static void FireLegendSkillFired(FWTCG.Core.LegendInstance legend, string owner)
            => OnLegendSkillFired?.Invoke(legend, owner);

        // ── DEV-30 F1: conquest VFX ───────────────────────────────────────────
        /// <summary>Fired when a conquest score is successfully awarded. SpellVFX subscribes for particle burst.</summary>
        public static event Action<string> OnConquestScored;
        public static void FireConquestScored(string owner) => OnConquestScored?.Invoke(owner);

        public static void FireBurnoutBanner(string burnedOwner)
        {
            string who = burnedOwner == GameRules.OWNER_PLAYER ? "玩家" : "AI";
            FireEventBanner($"燃尽！{who} 牌库耗尽，对手 +1分", 1.5f, large: true);
        }

        public static void FireLegendSkillBanner(string skillName, string effectDesc)
            => FireEventBanner($"{skillName}：{effectDesc}", 1.0f);

        public static void FireLegendEvolvedBanner()
            => FireEventBanner("进化！+3/+3", 1.0f, large: true);

        public static void FireTimeWarpBanner()
            => FireEventBanner("时间扭曲！获得额外回合", 1.5f, large: true);
    }
}
