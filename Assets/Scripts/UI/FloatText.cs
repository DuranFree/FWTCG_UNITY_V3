using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FWTCG.UI
{
    /// <summary>
    /// Pooled floating text for game event feedback (DEV-18b).
    ///
    /// Usage:
    ///   FloatText.Show(canvasLocalPos, "+1战力", GameColors.BuffColor, canvasRoot);
    ///
    /// Pool is initialized lazily. canvasRoot must be a Canvas transform
    /// (same Canvas used for DamagePopup). Floats upward 60px, fades out,
    /// then returns to pool. Font size defaults to 24; pass large=true for 32.
    /// </summary>
    public class FloatText : MonoBehaviour
    {
        // ── Pool ─────────────────────────────────────────────────────────────
        private const int POOL_SIZE = 12;
        private static readonly List<FloatText> _pool = new List<FloatText>(POOL_SIZE);
        private static Transform _poolRoot;

        private Text _text;
        private RectTransform _rt;
        private Sequence _seq;
        private bool _inUse;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Show a floating text at the given canvas-local position.
        /// </summary>
        public static FloatText Show(Vector2 canvasLocalPos, string content, Color color,
                                     Transform canvasRoot, bool large = false)
        {
            if (canvasRoot == null) return null;
            EnsurePool(canvasRoot);

            FloatText ft = GetFromPool(canvasRoot);
            ft.Play(canvasLocalPos, content, color, large);
            return ft;
        }

        // ── Pool management ───────────────────────────────────────────────────

        private static void EnsurePool(Transform canvasRoot)
        {
            if (_poolRoot != null) return;

            // DEV-26: _poolRoot was destroyed (scene reload) — clear stale MonoBehaviour refs
            _pool.Clear();

            var go = new GameObject("FloatTextPool");
            go.transform.SetParent(canvasRoot, false);
            // Must have a full-canvas RectTransform so children's anchoredPosition
            // uses the same origin as ScreenPointToLocalPointInRectangle on the canvas.
            var poolRT = go.AddComponent<RectTransform>();
            poolRT.anchorMin = Vector2.zero;
            poolRT.anchorMax = Vector2.one;
            poolRT.offsetMin = Vector2.zero;
            poolRT.offsetMax = Vector2.zero;
            _poolRoot = go.transform;

            for (int i = 0; i < POOL_SIZE; i++)
                _pool.Add(CreateInstance(_poolRoot));
        }

        private static FloatText GetFromPool(Transform canvasRoot)
        {
            foreach (var ft in _pool)
            {
                if (ft == null) continue; // DEV-26: guard against destroyed instances after scene reload
                if (!ft._inUse) return ft;
            }
            // Pool exhausted: create extra
            var extra = CreateInstance(_poolRoot != null ? _poolRoot : canvasRoot);
            _pool.Add(extra);
            return extra;
        }

        private static FloatText CreateInstance(Transform parent)
        {
            var go = new GameObject("FloatText");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120f, 40f);
            rt.localScale = Vector3.one;

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            var ft = go.AddComponent<FloatText>();
            ft._text = text;
            ft._rt = rt;
            ft._inUse = false;
            go.SetActive(false);
            return ft;
        }

        // ── Animation ─────────────────────────────────────────────────────────

        private void Play(Vector2 startPos, string content, Color color, bool large)
        {
            _inUse = true;
            gameObject.SetActive(true);

            _rt.anchoredPosition = startPos;
            _rt.localScale = Vector3.one;
            _text.text = content;
            _text.color = color;
            _text.fontSize = large ? 32 : 24;

            TweenHelper.KillSafe(ref _seq);

            Vector2 endPos = startPos + new Vector2(0f, 60f);
            const float duration = 0.9f;
            const float solidTime = duration * 0.35f;  // 0.315s
            const float fadeTime  = duration * 0.65f;  // 0.585s

            _seq = DOTween.Sequence().SetTarget(gameObject);
            _seq.Append(_rt.DOAnchorPos(endPos, duration).SetEase(Ease.OutQuad));
            _seq.Insert(solidTime, _text.DOFade(0f, fadeTime).SetEase(Ease.Linear));
            _seq.OnComplete(ReturnToPool);
        }

        private void ReturnToPool()
        {
            _inUse = false;
            _seq = null;
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            TweenHelper.KillSafe(ref _seq);
        }
    }
}
