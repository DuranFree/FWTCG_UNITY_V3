using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FWTCG.UI
{
    /// <summary>
    /// Screen-center small event banner for game event feedback (DEV-18b).
    ///
    /// Dedup + fast response:
    ///   - Same text currently showing → resets stay timer (no re-queue)
    ///   - Same text already in queue → dropped
    ///   - Queue cap MAX_QUEUE: extras dropped
    ///   - ClearAll: subscribed to GameEventBus.OnClearBanners (fires on turn start)
    ///   - Animation speed: slide-in 0.1s / slide-out 0.12s (halved from original 0.2/0.25s)
    ///
    /// DOT-3: AnimateIn/AnimateOut/ClearFade/WarningScalePop → DOTween.
    /// </summary>
    public class EventBanner : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [SerializeField] private Text          _bannerText;
        [SerializeField] private Image         _bannerBg;
        [SerializeField] private RectTransform _bannerRT;

        // ── State ─────────────────────────────────────────────────────────────
        private readonly Queue<(string text, float duration, bool large)> _queue
            = new Queue<(string, float, bool)>();
        private Coroutine   _showRoutine;
        private CanvasGroup _cg;
        private float       _drainDelay;   // one-shot delay before next DrainQueue starts

        // ── DOTween state ─────────────────────────────────────────────────────
        private Tween _animTween;       // current slide-in/out or scale-pop tween
        private Tween _clearFadeTween;  // quick clear-all fade

        private const int   MAX_QUEUE  = 4;
        private const float ANIM_IN    = 0.1f;   // was 0.2s
        private const float ANIM_OUT   = 0.12f;  // was 0.25s
        private const float CLEAR_FADE = 0.1f;   // quick fade on EndTurn / React

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
            _cg.alpha = 0f;
            _cg.blocksRaycasts = false;

            if (_bannerRT == null) _bannerRT = GetComponent<RectTransform>();

            // Switch to center-point anchor so sizeDelta controls the actual size
            if (_bannerRT != null)
            {
                _bannerRT.anchorMin = new Vector2(0.5f, 0.57f);
                _bannerRT.anchorMax = new Vector2(0.5f, 0.57f);
                _bannerRT.pivot     = new Vector2(0.5f, 0.5f);
                _bannerRT.sizeDelta = new Vector2(200f, 36f); // initial placeholder
            }
        }

        private void OnEnable()
        {
            GameEventBus.OnEventBanner    += EnqueueBanner;
            GameEventBus.OnClearBanners   += ClearAll;
            GameEventBus.OnSetBannerDelay += SetDrainDelay;
        }

        private void OnDisable()
        {
            GameEventBus.OnEventBanner    -= EnqueueBanner;
            GameEventBus.OnClearBanners   -= ClearAll;
            GameEventBus.OnSetBannerDelay -= SetDrainDelay;
            // 组件禁用时停止 DrainQueue，防止协程继续运行访问已销毁视图
            if (_showRoutine != null)
            {
                StopCoroutine(_showRoutine);
                _showRoutine = null;
            }
        }

        private void OnDestroy()
        {
            TweenHelper.KillSafe(ref _animTween);
            TweenHelper.KillSafe(ref _clearFadeTween);
        }

        private void SetDrainDelay(float seconds) => _drainDelay = seconds;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Called by GameEventBus subscription to queue a banner.</summary>
        public void EnqueueBanner(string text, float duration, bool large)
        {
            // If a clear-fade is in progress, cancel it and reset alpha before showing new content
            if (_clearFadeTween != null)
            {
                TweenHelper.KillSafe(ref _clearFadeTween);
                if (_cg != null) _cg.alpha = 0f;
            }

            // Queue full → drop
            if (_queue.Count >= MAX_QUEUE) return;

            _queue.Enqueue((text, duration, large));
            if (_showRoutine == null)
                _showRoutine = StartCoroutine(DrainQueue());
        }

        /// <summary>Clears all queued banners with a quick fade (e.g. on EndTurn / React).</summary>
        public void ClearAll()
        {
            if (_showRoutine != null)
            {
                StopCoroutine(_showRoutine);
                _showRoutine = null;
            }
            _queue.Clear();
            TweenHelper.KillSafe(ref _animTween);

            if (_cg == null || _cg.alpha <= 0f)
                return;

            // Quick visual fade-out instead of instant cut
            TweenHelper.KillSafe(ref _clearFadeTween);
            _clearFadeTween = TweenHelper.FadeCanvasGroup(_cg, 0f, CLEAR_FADE);
        }

        // ── Animation ─────────────────────────────────────────────────────────

        private IEnumerator DrainQueue()
        {
            // One-shot pre-delay (set by FireSetBannerDelay before combat resolution)
            if (_drainDelay > 0f)
            {
                float d = _drainDelay;
                _drainDelay = 0f;
                yield return new WaitForSeconds(d);
            }

            // Yield one frame so all synchronously-fired events pile into the queue
            yield return null;

            while (_queue.Count > 0)
            {
                // Collect everything currently queued as one batch
                var batch = new List<(string text, float duration, bool large)>();
                while (_queue.Count > 0)
                    batch.Add(_queue.Dequeue());

                yield return StartCoroutine(ShowBatch(batch));

                // Brief gap before next batch (if new events arrived while showing)
                if (_queue.Count > 0)
                    yield return new WaitForSeconds(0.3f);
            }

            _showRoutine = null;
        }

        /// <summary>
        /// Shows all items in <paramref name="batch"/> stacked as one multi-line banner.
        /// Uses the longest duration and large-flag from the batch.
        /// </summary>
        private IEnumerator ShowBatch(List<(string text, float duration, bool large)> batch)
        {
            bool  hasLarge = batch.Any(b => b.large);
            float duration = batch.Max(b => b.duration);
            // Extra second when 2+ lines so the player can read them all
            if (batch.Count >= 2) duration += 1f;

            if (_bannerText != null)
            {
                _bannerText.text     = string.Join("\n", batch.Select(b => b.text));
                _bannerText.fontSize = hasLarge ? 22 : 18;
                _bannerText.color    = hasLarge ? GameColors.BannerYellow : GameColors.GoldLight;
            }

            if (_bannerBg != null)
                _bannerBg.color = hasLarge
                    ? new Color(0.05f, 0.02f, 0f, 0.92f)
                    : new Color(0.02f, 0.05f, 0.1f, 0.88f);

            // Yield one frame so Unity layout refreshes preferredWidth / preferredHeight
            yield return null;

            // Auto-size: cap width at 480px; height driven by line count
            if (_bannerRT != null && _bannerText != null)
            {
                float w = Mathf.Min(_bannerText.preferredWidth  + 28f, 480f);
                float h = _bannerText.preferredHeight + 18f;
                _bannerRT.sizeDelta = new Vector2(w, h);
            }

            // Slide in from above (+30px → 0) while fading in
            Vector2 origin = _bannerRT != null ? _bannerRT.anchoredPosition : Vector2.zero;
            Vector2 above  = origin + new Vector2(0f, 30f);

            TweenHelper.KillSafe(ref _animTween);
            _animTween = CreateAnimateIn(above, origin);
            yield return _animTween.WaitForCompletion();

            // Stay
            _cg.alpha = 1f;
            yield return new WaitForSeconds(duration);

            // Fade out
            TweenHelper.KillSafe(ref _animTween);
            _animTween = CreateAnimateOut();
            yield return _animTween.WaitForCompletion();
        }

        /// <summary>Create slide-in tween: position from above + fade alpha 0→1.</summary>
        private Tween CreateAnimateIn(Vector2 fromPos, Vector2 toPos)
        {
            if (_bannerRT != null) _bannerRT.anchoredPosition = fromPos;
            _cg.alpha = 0f;

            var seq = DOTween.Sequence().SetTarget(gameObject);
            if (_bannerRT != null)
            {
                _bannerRT.localScale = Vector3.one * 0.92f;
                seq.Append(_bannerRT.DOAnchorPos(toPos, ANIM_IN).SetEase(Ease.InOutCubic));
                seq.Join(_bannerRT.DOScale(1f, ANIM_IN).SetEase(Ease.OutBack));
            }
            seq.Join(_cg.DOFade(1f, ANIM_IN).SetEase(Ease.OutCubic));
            return seq;
        }

        /// <summary>Create fade-out tween: alpha 1→0.</summary>
        private Tween CreateAnimateOut()
        {
            return TweenHelper.FadeCanvasGroup(_cg, 0f, ANIM_OUT);
        }

        // ── VFX-7h: Warning banner (red bg, white text, EaseOutBack scale pop) ──

        public const float WARN_DURATION = 1.5f;
        public const float WARN_SCALE_IN = 0.25f;
        public static readonly Color WarnBgColor = new Color(0.75f, 0.12f, 0.12f, 0.92f);

        /// <summary>Show a warning banner with red bg + scale-pop animation.</summary>
        public void ShowWarning(string text, float duration = WARN_DURATION)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_showRoutine != null) { StopCoroutine(_showRoutine); _showRoutine = null; }
            TweenHelper.KillSafe(ref _animTween);
            _queue.Clear();
            _showRoutine = StartCoroutine(WarningRoutine(text, duration));
        }

        private IEnumerator WarningRoutine(string text, float duration)
        {
            if (_bannerText != null)
            {
                _bannerText.text = text;
                _bannerText.fontSize = 20;
                _bannerText.color = Color.white;
            }
            if (_bannerBg != null)
                _bannerBg.color = WarnBgColor;

            yield return null; // layout refresh

            if (_bannerRT != null && _bannerText != null)
            {
                float w = Mathf.Min(_bannerText.preferredWidth + 28f, 480f);
                float h = _bannerText.preferredHeight + 18f;
                _bannerRT.sizeDelta = new Vector2(w, h);
            }

            // Scale pop: 0 → 1 with DOTween OutBack
            _cg.alpha = 1f;
            transform.localScale = Vector3.zero;
            TweenHelper.KillSafe(ref _animTween);
            _animTween = transform.DOScale(1f, WARN_SCALE_IN)
                .SetEase(Ease.OutBack)
                .SetTarget(gameObject);
            yield return _animTween.WaitForCompletion();

            transform.localScale = Vector3.one;

            // Audio trigger point
            if (FWTCG.Audio.AudioManager.Instance != null)
                FWTCG.Audio.AudioManager.Instance.PlayUIClick();

            // Stay
            yield return new WaitForSeconds(duration);

            // Fade out
            TweenHelper.KillSafe(ref _animTween);
            _animTween = CreateAnimateOut();
            yield return _animTween.WaitForCompletion();
            _showRoutine = null;
        }
    }
}
