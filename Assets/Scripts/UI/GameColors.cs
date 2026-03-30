using UnityEngine;

namespace FWTCG.UI
{
    /// <summary>
    /// Centralized color constants for the entire game.
    /// All UI code should reference these instead of hardcoding colors.
    /// Based on the Hextech visual theme from the original FWTCG.
    /// </summary>
    public static class GameColors
    {
        // ── Gold palette ──────────────────────────────────────────────────────
        public static readonly Color Gold       = Hex("#c8aa6e"); // primary gold
        public static readonly Color GoldLight  = Hex("#f0e6d2"); // light gold / warm white
        public static readonly Color GoldDark   = Hex("#785a28"); // dark gold / muted

        // ── Teal palette (Hextech cyan) ───────────────────────────────────────
        public static readonly Color Teal       = Hex("#0ac8b9"); // hextech teal
        public static readonly Color TealDark   = Hex("#005a82"); // dim teal
        public static readonly Color TealGlow   = new Color(0.04f, 0.78f, 0.73f, 0.35f); // rgba(10,200,185,0.35)

        // ── Background palette ────────────────────────────────────────────────
        public static readonly Color Background     = Hex("#010a13"); // deep navy/black
        public static readonly Color BackgroundMid   = Hex("#0a1428"); // mid-tone navy

        // ── Gameplay status ───────────────────────────────────────────────────
        public static readonly Color PlayerGreen = Hex("#4ade80"); // player accent
        public static readonly Color EnemyRed    = Hex("#f87171"); // enemy accent

        // ── Card states (migrated from CardView.cs) ───────────────────────────
        public static readonly Color CardExhausted   = new Color(0.4f, 0.4f, 0.4f, 1f);
        public static readonly Color CardNormal      = Color.white;
        public static readonly Color CardPlayer      = new Color(0.85f, 0.92f, 1f, 1f);
        public static readonly Color CardEnemy       = new Color(1f, 0.85f, 0.85f, 1f);
        public static readonly Color CardSpellPlayer = new Color(0.95f, 0.85f, 1f, 1f);
        public static readonly Color CardSpellEnemy  = new Color(1f, 0.8f, 0.9f, 1f);
        public static readonly Color CardSelected    = new Color(0.4f, 1f, 0.4f, 1f);
        public static readonly Color CardFaceDown    = new Color(0.12f, 0.16f, 0.25f, 1f);

        // ── Visual state overlays ─────────────────────────────────────────────
        public static readonly Color StunnedOverlay  = new Color(1f, 0.3f, 0.3f, 0.35f);
        public static readonly Color CostDimFactor   = new Color(0.6f, 0.6f, 0.6f, 1f); // multiply to darken
        public static readonly Color BuffTokenColor  = Hex("#fbbf24"); // amber gold for +1/+1

        // ── Glow colors ──────────────────────────────────────────────────────
        public static readonly Color GlowPlayable = new Color(0.29f, 0.87f, 0.5f, 0.75f);  // green particle trail
        public static readonly Color GlowHover    = new Color(0.78f, 0.67f, 0.43f, 0.8f);   // gold hover pulse

        // ── Rune type colors ──────────────────────────────────────────────────
        public static readonly Color RuneBlazing  = new Color(1f, 0.55f, 0.1f, 1f);   // fire orange
        public static readonly Color RuneRadiant  = new Color(1f, 0.85f, 0.2f, 1f);   // golden yellow
        public static readonly Color RuneVerdant  = new Color(0.2f, 0.85f, 0.4f, 1f); // emerald green
        public static readonly Color RuneCrushing = new Color(0.85f, 0.2f, 0.2f, 1f); // crimson red
        public static readonly Color RuneChaos    = new Color(0.6f, 0.3f, 0.85f, 1f); // purple
        public static readonly Color RuneOrder    = new Color(0.3f, 0.6f, 0.95f, 1f); // blue

        // ── UI panel colors ───────────────────────────────────────────────────
        public static readonly Color PanelOverlay    = new Color(0f, 0f, 0f, 0.85f);  // fullscreen dimmer
        public static readonly Color PanelDetail     = Hex("#1e1e2f");                  // card detail background
        public static readonly Color PanelDetailBorder = Hex("#c9a227");               // gold border
        public static readonly Color TextWarm        = Hex("#e8d9c0");                  // warm beige text
        public static readonly Color BannerYellow    = new Color(1f, 0.92f, 0.23f, 1f);
        public static readonly Color ToastGold       = new Color(0.94f, 0.86f, 0.42f, 1f);

        // ── Rune tapped state ─────────────────────────────────────────────────
        public static readonly Color RuneTapped = new Color(0.5f, 0.5f, 0.5f, 1f);

        /// <summary>
        /// Convert a hex color string (#RRGGBB or #RRGGBBAA) to a Unity Color.
        /// </summary>
        public static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color c))
                return c;
            Debug.LogWarning($"[GameColors] Failed to parse hex color: {hex}");
            return Color.magenta;
        }
    }
}
