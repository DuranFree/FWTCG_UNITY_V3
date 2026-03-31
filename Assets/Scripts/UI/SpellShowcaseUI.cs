using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;
using FWTCG.Data;

namespace FWTCG.UI
{
    /// <summary>
    /// DEV-16: Full-screen spell showcase overlay.
    /// When a spell is cast (player or AI), this panel flies in from the bottom,
    /// displays the card name + effect text for 0.5s, then fades out.
    ///
    /// Usage: await SpellShowcaseUI.Instance.ShowAsync(spell, owner)
    /// Safe to call when Instance is null (no-op).
    /// </summary>
    public class SpellShowcaseUI : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        public static SpellShowcaseUI Instance { get; private set; }

        // ── Timing constants ───────────────────────────────────────────────────
        public const float FLY_IN_DURATION  = 0.4f;
        public const float HOLD_DURATION    = 0.5f;
        public const float FLY_OUT_DURATION = 0.35f;
        public const float TOTAL_DURATION   = FLY_IN_DURATION + HOLD_DURATION + FLY_OUT_DURATION;

        // ── Inspector refs (wired by SceneBuilder) ────────────────────────────
        [SerializeField] private CanvasGroup   _canvasGroup;
        [SerializeField] private RectTransform _cardPanel;     // the animated card container
        [SerializeField] private Text          _ownerLabel;    // "玩家" / "AI"
        [SerializeField] private Text          _cardNameText;
        [SerializeField] private Text          _effectText;
        [SerializeField] private Image         _artImage;      // optional card art

        // Card panel start/end Y offsets for fly animation
        private const float FLY_START_Y =  -220f;
        private const float FLY_END_Y   =    0f;
        private const float FLY_OUT_Y   =   80f;

        // Track whether a showcase is currently in progress
        public bool IsShowing { get; private set; }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Show the spell showcase. Awaitable — resolves after the full animation completes.
        /// Safe to call from async Task context (bridges via TCS + coroutine).
        /// </summary>
        public Task ShowAsync(UnitInstance spell, string owner)
        {
            if (spell == null) return Task.CompletedTask;

            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            StartCoroutine(ShowCoroutine(spell, owner, tcs));
            return tcs.Task;
        }

        // ── Internal coroutine ─────────────────────────────────────────────────

        private IEnumerator ShowCoroutine(UnitInstance spell, string owner,
                                          System.Threading.Tasks.TaskCompletionSource<bool> tcs)
        {
            IsShowing = true;
            gameObject.SetActive(true);

            // Populate content
            bool isPlayer = owner == GameRules.OWNER_PLAYER;
            if (_ownerLabel != null)
            {
                _ownerLabel.text  = isPlayer ? "玩家" : "AI";
                _ownerLabel.color = isPlayer
                    ? new Color(0.29f, 0.87f, 0.5f)   // green
                    : new Color(0.97f, 0.44f, 0.44f);  // red
            }
            if (_cardNameText != null)
                _cardNameText.text = spell.UnitName;
            if (_effectText != null)
                _effectText.text = spell.CardData?.Description ?? "";
            if (_artImage != null && spell.CardData?.ArtSprite != null)
            {
                _artImage.sprite = spell.CardData.ArtSprite;
                _artImage.gameObject.SetActive(true);
            }
            else if (_artImage != null)
            {
                _artImage.gameObject.SetActive(false);
            }

            // Reset position
            if (_cardPanel != null)
            {
                var pos = _cardPanel.anchoredPosition;
                _cardPanel.anchoredPosition = new Vector2(pos.x, FLY_START_Y);
            }
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;

            // ── Fly in ─────────────────────────────────────────────────────────
            float t = 0f;
            while (t < FLY_IN_DURATION)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / FLY_IN_DURATION));
                if (_canvasGroup != null) _canvasGroup.alpha = p;
                if (_cardPanel != null)
                {
                    var pos = _cardPanel.anchoredPosition;
                    _cardPanel.anchoredPosition = new Vector2(pos.x,
                        Mathf.Lerp(FLY_START_Y, FLY_END_Y, p));
                }
                yield return null;
            }
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
            if (_cardPanel != null)
            {
                var pos = _cardPanel.anchoredPosition;
                _cardPanel.anchoredPosition = new Vector2(pos.x, FLY_END_Y);
            }

            // ── Hold ───────────────────────────────────────────────────────────
            float held = 0f;
            while (held < HOLD_DURATION)
            {
                held += Time.unscaledDeltaTime;
                yield return null;
            }

            // ── Fly out ────────────────────────────────────────────────────────
            t = 0f;
            while (t < FLY_OUT_DURATION)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / FLY_OUT_DURATION));
                if (_canvasGroup != null) _canvasGroup.alpha = 1f - p;
                if (_cardPanel != null)
                {
                    var pos = _cardPanel.anchoredPosition;
                    _cardPanel.anchoredPosition = new Vector2(pos.x,
                        Mathf.Lerp(FLY_END_Y, FLY_OUT_Y, p));
                }
                yield return null;
            }

            gameObject.SetActive(false);
            IsShowing = false;
            tcs.TrySetResult(true);
        }
    }
}
