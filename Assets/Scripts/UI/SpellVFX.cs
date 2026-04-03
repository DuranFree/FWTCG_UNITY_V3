using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.Systems;

namespace FWTCG.UI
{
    /// <summary>
    /// DEV-21: Spell / unit death / legend VFX system.
    ///
    /// Subscribes to game events and spawns transient particle effects:
    ///   OnCardPlayed   → 16-point radial burst (color by RuneType) at canvas centre.
    ///   OnUnitDiedAtPos → 12-point death explosion at unit's canvas position.
    ///   LegendSystem.OnLegendEvolved → 20-particle flame (3 s) near legend zone.
    ///
    /// All VFX objects are self-Destroyed after their animation finishes.
    /// </summary>
    public class SpellVFX : MonoBehaviour
    {
        // ── Inspector refs ────────────────────────────────────────────────────
        [SerializeField] public Transform _vfxLayer;   // top-most canvas layer

        // ── Particle ownership tracking ───────────────────────────────────────
        // Tracks all particle GOs currently alive so OnDestroy can clean up any
        // that were orphaned by a coroutine interrupted mid-animation.
        private readonly System.Collections.Generic.HashSet<GameObject> _ownedParticles
            = new System.Collections.Generic.HashSet<GameObject>();

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            GameEventBus.OnCardPlayed              += OnCardPlayed; // DEV-27: migrated from GameManager
            GameEventBus.OnUnitDiedAtPos          += OnUnitDiedAtPos;
            LegendSystem.OnLegendEvolved          += OnLegendEvolved;
            GameEventBus.OnConquestScored         += OnConquestScored; // DEV-30 F1
        }

        private void OnDestroy()
        {
            GameEventBus.OnCardPlayed              -= OnCardPlayed;
            GameEventBus.OnUnitDiedAtPos          -= OnUnitDiedAtPos;
            LegendSystem.OnLegendEvolved          -= OnLegendEvolved;
            GameEventBus.OnConquestScored         -= OnConquestScored; // DEV-30 F1

            // Destroy any particle GOs orphaned by coroutines interrupted mid-animation.
            // Using explicit ownership list avoids destroying unrelated _vfxLayer children.
            foreach (var go in _ownedParticles)
                if (go != null) Destroy(go);
            _ownedParticles.Clear();
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void OnCardPlayed(UnitInstance card, string owner)
        {
            if (!this) return;
            if (!isActiveAndEnabled || _vfxLayer == null) return;
            // Burst from player's area (bottom) or enemy's area (top)
            float y = (owner == GameRules.OWNER_PLAYER) ? -180f : 180f;
            StartCoroutine(BurstParticles(new Vector2(0f, y), GetCardBurstColor(card), 12));
        }

        private void OnUnitDiedAtPos(UnitInstance unit, Vector2 canvasPos)
        {
            if (!this) return;
            if (!isActiveAndEnabled || _vfxLayer == null) return;
            StartCoroutine(BurstParticles(canvasPos, new Color(1f, 0.30f, 0.15f, 1f), 12));
        }

        private void OnLegendEvolved(string owner, int newLevel)
        {
            if (!this) return;
            if (!isActiveAndEnabled || _vfxLayer == null) return;
            // Approximate legend zone positions (player left / enemy right)
            float x = (owner == GameRules.OWNER_PLAYER) ? -550f : 550f;
            StartCoroutine(LegendFlame(new Vector2(x, 250f)));
        }

        // DEV-30 F1: conquest VFX — gold burst at score zone when conquest score is earned
        private void OnConquestScored(string owner)
        {
            if (!this) return; // guard: destroyed object may still hold a delegate reference
            if (!isActiveAndEnabled || _vfxLayer == null) return;
            string zone = owner == GameRules.OWNER_PLAYER ? "score_player" : "score_enemy";
            Vector2 pos = GameUI.Instance != null
                ? GameUI.Instance.GetZoneCanvasPos(zone)
                : new Vector2(owner == GameRules.OWNER_PLAYER ? -400f : 400f, 0f);
            StartCoroutine(BurstParticles(pos, GameColors.Gold, 20));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Burst particles
        // ═══════════════════════════════════════════════════════════════════════

        private IEnumerator BurstParticles(Vector2 origin, Color color, int count)
        {
            var rts  = new RectTransform[count];
            var imgs = new Image[count];

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"BurstPt_{i}");
                _ownedParticles.Add(go);
                go.transform.SetParent(_vfxLayer, false);

                var rt = go.AddComponent<RectTransform>();
                rt.anchoredPosition = origin;
                rt.sizeDelta = new Vector2(6f, 6f);

                var img = go.AddComponent<Image>();
                img.color = color;
                img.raycastTarget = false;

                rts[i]  = rt;
                imgs[i] = img;
            }

            const float DURATION = 0.6f;
            float elapsed = 0f;

            while (elapsed < DURATION)
            {
                elapsed += Time.deltaTime;
                float t     = Mathf.Clamp01(elapsed / DURATION);
                float eased = 1f - (1f - t) * (1f - t); // ease-out quad

                for (int i = 0; i < count; i++)
                {
                    float angle  = (float)i / count * Mathf.PI * 2f;
                    float radius = Mathf.Lerp(0f, 110f, eased);
                    rts[i].anchoredPosition = origin + new Vector2(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius
                    );

                    float sz = Mathf.Lerp(6f, 2f, t);
                    rts[i].sizeDelta = new Vector2(sz, sz);

                    var c = imgs[i].color;
                    c.a = (1f - t) * 0.9f;
                    imgs[i].color = c;
                }

                yield return null;
            }

            for (int i = 0; i < count; i++)
            {
                _ownedParticles.Remove(rts[i].gameObject);
                Destroy(rts[i].gameObject);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Legend flame (3 s)
        // ═══════════════════════════════════════════════════════════════════════

        private IEnumerator LegendFlame(Vector2 origin)
        {
            const int   FLAME_COUNT = 20;
            const float DURATION    = 3f;

            var rts        = new RectTransform[FLAME_COUNT];
            var imgs       = new Image[FLAME_COUNT];
            var velocities = new Vector2[FLAME_COUNT];
            var phases     = new float[FLAME_COUNT];

            for (int i = 0; i < FLAME_COUNT; i++)
            {
                var go = new GameObject($"Flame_{i}");
                _ownedParticles.Add(go);
                go.transform.SetParent(_vfxLayer, false);

                var rt = go.AddComponent<RectTransform>();
                rt.anchoredPosition = origin + new Vector2(Random.Range(-20f, 20f), 0f);
                rt.sizeDelta = new Vector2(8f, 12f);

                var img = go.AddComponent<Image>();
                float blend = (float)i / FLAME_COUNT;
                img.color = Color.Lerp(
                    new Color(1f, 0.30f, 0.05f, 0.90f),   // orange-red (base)
                    new Color(1f, 0.90f, 0.20f, 0.70f)    // yellow-white (tip)
                , blend);
                img.raycastTarget = false;

                velocities[i] = new Vector2(Random.Range(-15f, 15f), Random.Range(20f, 65f));
                phases[i]     = Random.Range(0f, Mathf.PI * 2f);
                rts[i]        = rt;
                imgs[i]       = img;
            }

            float elapsed = 0f;
            while (elapsed < DURATION)
            {
                elapsed += Time.deltaTime;
                float globalT = elapsed / DURATION;

                for (int i = 0; i < FLAME_COUNT; i++)
                {
                    var pos = rts[i].anchoredPosition;
                    pos += velocities[i] * Time.deltaTime;

                    phases[i] += Time.deltaTime * 3f;
                    pos.x += Mathf.Sin(phases[i]) * 8f * Time.deltaTime;

                    // Respawn particle when it has risen far enough
                    if (pos.y > origin.y + 140f)
                    {
                        pos = origin + new Vector2(Random.Range(-20f, 20f), 0f);
                        velocities[i] = new Vector2(Random.Range(-15f, 15f), Random.Range(20f, 65f));
                    }

                    rts[i].anchoredPosition = pos;

                    var c = imgs[i].color;
                    c.a = (1f - globalT) * 0.85f;
                    imgs[i].color = c;
                }

                yield return null;
            }

            for (int i = 0; i < FLAME_COUNT; i++)
            {
                _ownedParticles.Remove(rts[i].gameObject);
                Destroy(rts[i].gameObject);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Maps a card's RuneType to a burst particle colour.</summary>
        public static Color GetCardBurstColor(UnitInstance card)
        {
            if (card?.CardData == null) return GameColors.BuffColor;
            switch (card.CardData.RuneType)
            {
                case RuneType.Blazing:  return new Color(1.0f, 0.40f, 0.10f, 1f);
                case RuneType.Radiant:  return new Color(1.0f, 0.90f, 0.20f, 1f);
                case RuneType.Verdant:  return new Color(0.20f, 0.90f, 0.30f, 1f);
                case RuneType.Crushing: return new Color(0.70f, 0.20f, 0.10f, 1f);
                case RuneType.Chaos:    return new Color(0.80f, 0.10f, 0.90f, 1f);
                case RuneType.Order:    return new Color(0.10f, 0.70f, 1.00f, 1f);
                default:                return GameColors.BuffColor;
            }
        }
    }
}
