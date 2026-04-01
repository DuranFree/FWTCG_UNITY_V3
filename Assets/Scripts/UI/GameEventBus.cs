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

        // ── Fire helpers ──────────────────────────────────────────────────────

        public static void FireUnitFloatText(UnitInstance unit, string text, Color color)
            => OnUnitFloatText?.Invoke(unit, text, color);

        public static void FireZoneFloatText(string zone, string text, Color color)
            => OnZoneFloatText?.Invoke(zone, text, color);

        public static void FireEventBanner(string text, float duration = 1.5f, bool large = false)
            => OnEventBanner?.Invoke(text, duration, large);

        /// <summary>Fires the spell duel banner. Called when player's spell enters the duel window. DEV-19.</summary>
        public static void FireDuelBanner() => OnDuelBanner?.Invoke();

        /// <summary>Clears all queued banners and toasts immediately. Call at turn start.</summary>
        public static void FireClearBanners() => OnClearBanners?.Invoke();

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
            => FireEventBanner($"绝念：{unitName} — {effectDesc}", 1.5f);

        public static void FireEntryEffectBanner(string unitName, string effectDesc)
            => FireEventBanner($"{unitName}：{effectDesc}", 1.5f);

        public static void FireHoldScoreBanner()
            => FireEventBanner("据守 +1分", 1.5f);

        public static void FireConquerScoreBanner()
            => FireEventBanner("征服！+1分", 2f);

        public static void FireBurnoutBanner(string burnedOwner)
        {
            string who = burnedOwner == GameRules.OWNER_PLAYER ? "玩家" : "AI";
            FireEventBanner($"燃尽！{who} 牌库耗尽，对手 +1分", 3f, large: true);
        }

        public static void FireLegendSkillBanner(string skillName, string effectDesc)
            => FireEventBanner($"{skillName}：{effectDesc}", 2f);

        public static void FireLegendEvolvedBanner()
            => FireEventBanner("进化！+3/+3", 2.5f, large: true);

        public static void FireTimeWarpBanner()
            => FireEventBanner("时间扭曲！获得额外回合", 2.5f, large: true);
    }
}
