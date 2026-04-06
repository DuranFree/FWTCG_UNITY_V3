using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.FX;
using FWTCG.Systems;
using FWTCG.VFX;

namespace FWTCG.UI
{
    /// <summary>
    /// DEV-21 / VFX-4: Spell / unit death / legend VFX system.
    ///
    /// Subscribes to game events and spawns transient particle effects:
    ///   OnCardPlayed   → VFXResolver FX prefabs + radial burst (color by RuneType).
    ///   OnUnitDiedAtPos → VFXResolver death FX + death explosion.
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
        private readonly HashSet<GameObject> _ownedParticles = new();

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

            // DOT-4: kill DOTween sequences on owned particles before destroying them
            foreach (var go in _ownedParticles)
            {
                if (go == null) continue;
                DOTween.Kill(go);
                Destroy(go);
            }
            _ownedParticles.Clear();
        }

        // ── Event handlers ────────────────────────────────────────────────────

        /// <summary>Card enter animation is 0.42s; delay FX to sync with landing.</summary>
        private const float CARD_PLAY_FX_DELAY = 0.43f;

        private void OnCardPlayed(UnitInstance card, string owner)
        {
            if (!this) return;
            if (!isActiveAndEnabled || _vfxLayer == null) return;

            StartCoroutine(DelayedCardPlayFX(card, owner));
        }

        // ── VFX-8 projectile constants ──────────────────────────────────────
        public const float PROJECTILE_DURATION = 0.4f;
        /// <summary>Y offset for projectile origin (player hand area).</summary>
        public const float PLAYER_HAND_Y = -300f;
        /// <summary>Y offset for AI projectile origin (top of screen).</summary>
        public const float AI_ORIGIN_Y = 340f;

        /// <summary>Max seconds to wait for showcase panel to close before giving up.</summary>
        private const float SHOWCASE_WAIT_TIMEOUT = 5f;

        private IEnumerator DelayedCardPlayFX(UnitInstance card, string owner)
        {
            // Wait for card entrance animation to finish before spawning FX at landing spot
            yield return new WaitForSeconds(CARD_PLAY_FX_DELAY);
            if (!this || !isActiveAndEnabled || _vfxLayer == null) yield break;

            // VFX-8 fix: If spell showcase panel is showing, wait for it to close
            // so the projectile isn't hidden behind the full-screen showcase overlay.
            if (SpellShowcaseUI.Instance != null && SpellShowcaseUI.Instance.IsShowing)
            {
                float waited = 0f;
                while (SpellShowcaseUI.Instance != null && SpellShowcaseUI.Instance.IsShowing
                       && waited < SHOWCASE_WAIT_TIMEOUT)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
                if (!this || !isActiveAndEnabled || _vfxLayer == null) yield break;
            }

            // After delay, find the CardView at its final position
            Vector2 cardPos = ResolveCardFinalPos(card, owner);
            Vector3 endPos = new Vector3(cardPos.x, cardPos.y, 0f);

            // VFX-8: Try projectile path first (spell/equipment cards)
            bool usedProjectile = false;
            if (card?.CardData != null)
            {
                string projFXName = VFXResolver.ResolveProjectile(card.CardData);
                if (projFXName != null)
                {
                    var projPrefab = VFXResolver.GetPrefab(projFXName);
                    if (projPrefab != null)
                    {
                        // Projectile origin: player → hand area, AI → top of screen
                        float originY = (owner == GameRules.OWNER_PLAYER) ? PLAYER_HAND_Y : AI_ORIGIN_Y;
                        Vector3 startPos = new Vector3(0f, originY, 0f);

                        var configs = VFXResolver.Resolve(card.CardData);
                        StartCoroutine(ProjectileThenFXRoutine(projPrefab, startPos, endPos, configs));
                        usedProjectile = true;
                    }
                }
            }

            // Fallback: direct FX spawn (units, or cards without projectile prefab)
            if (!usedProjectile && card?.CardData != null)
            {
                var configs = VFXResolver.Resolve(card.CardData);
                StartCoroutine(SpawnResolvedFX(configs, endPos));
            }

            // Original radial burst (kept as ambient particle layer)
            BurstParticles(cardPos, GetCardBurstColor(card), 12);
        }

        /// <summary>
        /// VFX-8: Launch projectile from start to end, then spawn impact FX at arrival.
        /// </summary>
        private IEnumerator ProjectileThenFXRoutine(GameObject projPrefab, Vector3 start, Vector3 end,
                                                     List<FXConfig> impactConfigs)
        {
            bool arrived = false;
            var projGO = FXTool.DoProjectileFX(projPrefab, start, end, PROJECTILE_DURATION, () => arrived = true);
            if (projGO != null)
                _ownedParticles.Add(projGO);

            // Wait for projectile to arrive (or timeout safety)
            float timeout = PROJECTILE_DURATION + 0.5f;
            float waited = 0f;
            while (!arrived && waited < timeout)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            // Clean up tracking (projectile self-destroys)
            if (projGO != null) _ownedParticles.Remove(projGO);

            if (!this || !isActiveAndEnabled) yield break;

            // Spawn impact FX at target
            if (impactConfigs != null && impactConfigs.Count > 0)
                StartCoroutine(SpawnResolvedFX(impactConfigs, end));

            // Impact hit burst
            BurstParticles((Vector2)end, Color.white, 8);
        }

        /// <summary>
        /// VFX-6 fix: After delay, find the CardView at its final resting position.
        /// Falls back to target container pos, then hardcoded ±180.
        /// </summary>
        private Vector2 ResolveCardFinalPos(UnitInstance card, string owner)
        {
            float fallbackY = (owner == GameRules.OWNER_PLAYER) ? -180f : 180f;
            Vector2 fallback = new Vector2(0f, fallbackY);

            if (GameUI.Instance == null || card == null) return fallback;

            // After delay, card should be at its final position — find it directly
            var cv = GameUI.Instance.FindCardView(card);
            if (cv != null)
            {
                var cvRT = cv.GetComponent<RectTransform>();
                var layerRT = _vfxLayer as RectTransform ?? _vfxLayer.GetComponent<RectTransform>();
                if (cvRT != null && layerRT != null)
                {
                    Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(null, cvRT.position);
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            layerRT, screenPt, null, out Vector2 localPos))
                        return localPos;
                }
            }

            // Fallback: use target container position
            Vector2 containerPos = GameUI.Instance.GetCardPlayTargetPos(card, owner);
            if (containerPos != Vector2.zero) return containerPos;

            return fallback;
        }

        private void OnUnitDiedAtPos(UnitInstance unit, Vector2 canvasPos)
        {
            if (!this) return;
            if (!isActiveAndEnabled || _vfxLayer == null) return;

            // VFX-4: Spawn resolved death FX prefabs
            if (unit?.CardData != null)
            {
                var configs = VFXResolver.ResolveDeathFX(unit.CardData);
                StartCoroutine(SpawnResolvedFX(configs, new Vector3(canvasPos.x, canvasPos.y, 0f)));
            }

            // Original death burst
            BurstParticles(canvasPos, new Color(1f, 0.30f, 0.15f, 1f), 12);
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
            BurstParticles(pos, GameColors.Gold, 20);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // VFX-4: Resolved FX prefab spawning
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Spawn a list of FXConfig entries as world-space prefabs (with delays/repeats).</summary>
        private IEnumerator SpawnResolvedFX(List<FXConfig> configs, Vector3 worldPos)
        {
            foreach (var cfg in configs)
            {
                if (cfg.Delay > 0f)
                    yield return new WaitForSeconds(cfg.Delay);

                var prefab = VFXResolver.GetPrefab(cfg.PrefabName);
                if (prefab == null) continue;

                for (int i = 0; i < cfg.RepeatCount; i++)
                {
                    if (i > 0 && cfg.RepeatInterval > 0f)
                        yield return new WaitForSeconds(cfg.RepeatInterval);

                    float lifetime = cfg.Duration > 0f ? cfg.Duration : 3f;
                    var fx = FXTool.DoFX(prefab, worldPos, lifetime);
                    if (fx != null)
                    {
                        _ownedParticles.Add(fx);

                        if (cfg.Scale != 1f)
                            fx.transform.localScale *= cfg.Scale;

                        if (cfg.HasTint)
                        {
                            // Tint all particle system renderers
                            var ps = fx.GetComponentInChildren<ParticleSystem>();
                            if (ps != null)
                            {
                                var main = ps.main;
                                main.startColor = cfg.Tint;
                            }
                        }

                        // Auto-remove from tracking when destroyed
                        StartCoroutine(RemoveAfterDelay(fx, lifetime));
                    }
                }
            }
        }

        private IEnumerator RemoveAfterDelay(GameObject go, float delay)
        {
            yield return new WaitForSeconds(delay + 0.1f);
            _ownedParticles.Remove(go);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Burst particles
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>DOT-4: Radial burst particles via DOTween (replaced coroutine).</summary>
        public const float BURST_DURATION = 0.6f;
        public const float BURST_RADIUS   = 110f;
        public const float BURST_START_SIZE = 6f;
        public const float BURST_END_SIZE   = 2f;

        private void BurstParticles(Vector2 origin, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"BurstPt_{i}");
                _ownedParticles.Add(go);
                go.transform.SetParent(_vfxLayer, false);

                var rt = go.AddComponent<RectTransform>();
                rt.anchoredPosition = origin;
                rt.sizeDelta = new Vector2(BURST_START_SIZE, BURST_START_SIZE);

                var img = go.AddComponent<Image>();
                img.color = new Color(color.r, color.g, color.b, 0.9f);
                img.raycastTarget = false;

                float angle = (float)i / count * Mathf.PI * 2f;
                Vector2 endPos = origin + new Vector2(
                    Mathf.Cos(angle) * BURST_RADIUS,
                    Mathf.Sin(angle) * BURST_RADIUS);

                var seq = DOTween.Sequence().SetTarget(go);
                seq.Append(rt.DOAnchorPos(endPos, BURST_DURATION).SetEase(Ease.OutQuad));
                seq.Join(rt.DOSizeDelta(new Vector2(BURST_END_SIZE, BURST_END_SIZE), BURST_DURATION).SetEase(Ease.Linear));
                seq.Join(img.DOFade(0f, BURST_DURATION).SetEase(Ease.Linear));
                // capture for closure
                var capturedGo = go;
                seq.OnComplete(() =>
                {
                    _ownedParticles.Remove(capturedGo);
                    Destroy(capturedGo);
                });
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
