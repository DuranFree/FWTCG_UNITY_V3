using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;
using FWTCG.Data;

namespace FWTCG.UI
{
    /// <summary>
    /// DEV-16 → DOT-5: Full-screen spell showcase overlay.
    /// Fly-in/hold/fly-out animation uses a DOTween Sequence (no coroutines).
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
        [SerializeField] private RectTransform _cardPanel;     // single-card animated container
        [SerializeField] private Text          _ownerLabel;    // "玩家" / "AI"
        [SerializeField] private Text          _cardNameText;
        [SerializeField] private Text          _effectText;
        [SerializeField] private Image         _artImage;      // optional card art
        [SerializeField] private RectTransform _groupPanel;    // multi-card animated container
        [SerializeField] private Transform     _slotsRoot;     // HLG row inside _groupPanel

        // Card panel Y offsets for fly animation (player = from bottom, enemy = from top)
        private const float FLY_OFFSET  =  180f;   // distance from centre to start/exit
        private const float FLY_END_Y   =    0f;

        // Track whether a showcase is currently in progress
        public bool IsShowing { get; private set; }

        // DOTween handle
        private Sequence _showSeq;
        // Active TCS — resolved in OnDestroy to prevent hang if destroyed mid-animation
        private TaskCompletionSource<bool> _activeTcs;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Hide();
        }

        private void Hide()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha          = 0f;
                _canvasGroup.interactable   = false;
                _canvasGroup.blocksRaycasts = false;
            }
        }

        private void OnDestroy()
        {
            TweenHelper.KillSafe(ref _showSeq);
            // Resolve pending TCS so awaiting callers don't hang
            _activeTcs?.TrySetResult(true);
            _activeTcs = null;
            IsShowing = false;
            if (Instance == this) Instance = null;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Show a group of spells side by side. If only 1 spell, falls back to ShowAsync.
        /// </summary>
        public Task ShowGroupAsync(List<UnitInstance> spells, string owner)
        {
            if (spells == null || spells.Count == 0) return Task.CompletedTask;
            if (spells.Count == 1) return ShowAsync(spells[0], owner);

            var tcs = new TaskCompletionSource<bool>();
            _activeTcs = tcs;
            IsShowing = true;
            bool isPlayer = owner == GameRules.OWNER_PLAYER;

            // Build grid slots (sync)
            RectTransform animPanel = _groupPanel != null ? _groupPanel : _cardPanel;
            BuildGroupSlots(spells, isPlayer);

            float flyStartY = isPlayer ? -FLY_OFFSET : FLY_OFFSET;
            float flyExitY  = isPlayer ? -FLY_OFFSET : FLY_OFFSET;

            if (animPanel != null)
            {
                var pos = animPanel.anchoredPosition;
                animPanel.anchoredPosition = new Vector2(pos.x, flyStartY);
                animPanel.gameObject.SetActive(true);
            }
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;

            // Build animation sequence (unscaled time for pause-safe)
            TweenHelper.KillSafe(ref _showSeq);
            var seq = DOTween.Sequence().SetUpdate(true);

            // Fly in
            if (animPanel != null)
                seq.Append(animPanel.DOAnchorPosY(FLY_END_Y, FLY_IN_DURATION).SetEase(Ease.InOutQuad));
            if (_canvasGroup != null)
                seq.Join(_canvasGroup.DOFade(1f, FLY_IN_DURATION).SetEase(Ease.InOutQuad));

            // Hold
            seq.AppendInterval(HOLD_DURATION);

            // Fly out
            if (animPanel != null)
                seq.Append(animPanel.DOAnchorPosY(flyExitY, FLY_OUT_DURATION).SetEase(Ease.InOutQuad));
            if (_canvasGroup != null)
                seq.Join(_canvasGroup.DOFade(0f, FLY_OUT_DURATION).SetEase(Ease.InOutQuad));

            // Capture for closure
            var slotsRoot = _slotsRoot;
            var cardPanel = _cardPanel;
            seq.OnComplete(() =>
            {
                // Clean up slots
                if (slotsRoot != null)
                {
                    for (int i = slotsRoot.childCount - 1; i >= 0; i--)
                        Destroy(slotsRoot.GetChild(i).gameObject);
                    slotsRoot.gameObject.SetActive(false);
                }
                if (animPanel != null && animPanel != cardPanel)
                    animPanel.gameObject.SetActive(false);
                Hide();
                IsShowing = false;
                tcs.TrySetResult(true);
            });
            seq.SetTarget(gameObject);
            _showSeq = seq;

            return tcs.Task;
        }

        /// <summary>
        /// Show the spell showcase. Awaitable — resolves after the full animation completes.
        /// Safe to call from async Task context.
        /// </summary>
        public Task ShowAsync(UnitInstance spell, string owner)
        {
            if (spell == null) return Task.CompletedTask;

            var tcs = new TaskCompletionSource<bool>();
            _activeTcs = tcs;
            IsShowing = true;

            // Populate content (sync)
            bool isPlayer = owner == GameRules.OWNER_PLAYER;
            PopulateSingleCard(spell, isPlayer);

            float flyStartY = isPlayer ? -FLY_OFFSET : FLY_OFFSET;
            float flyExitY  = isPlayer ? -FLY_OFFSET : FLY_OFFSET;

            // Reset position
            if (_cardPanel != null)
            {
                var pos = _cardPanel.anchoredPosition;
                _cardPanel.anchoredPosition = new Vector2(pos.x, flyStartY);
            }
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;

            // Build animation sequence (unscaled time for pause-safe)
            TweenHelper.KillSafe(ref _showSeq);
            var seq = DOTween.Sequence().SetUpdate(true);

            // Fly in
            if (_cardPanel != null)
                seq.Append(_cardPanel.DOAnchorPosY(FLY_END_Y, FLY_IN_DURATION).SetEase(Ease.InOutQuad));
            if (_canvasGroup != null)
                seq.Join(_canvasGroup.DOFade(1f, FLY_IN_DURATION).SetEase(Ease.InOutQuad));

            // Hold
            seq.AppendInterval(HOLD_DURATION);

            // Fly out
            if (_cardPanel != null)
                seq.Append(_cardPanel.DOAnchorPosY(flyExitY, FLY_OUT_DURATION).SetEase(Ease.InOutQuad));
            if (_canvasGroup != null)
                seq.Join(_canvasGroup.DOFade(0f, FLY_OUT_DURATION).SetEase(Ease.InOutQuad));

            seq.OnComplete(() =>
            {
                Hide();
                IsShowing = false;
                tcs.TrySetResult(true);
            });
            seq.SetTarget(gameObject);
            _showSeq = seq;

            return tcs.Task;
        }

        // ── Content helpers ────────────────────────────────────────────────────

        private void PopulateSingleCard(UnitInstance spell, bool isPlayer)
        {
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
        }

        private void BuildGroupSlots(List<UnitInstance> spells, bool isPlayer)
        {
            if (_slotsRoot == null) return;

            for (int i = _slotsRoot.childCount - 1; i >= 0; i--)
                Destroy(_slotsRoot.GetChild(i).gameObject);

            // Disable any existing HLG — we position manually
            var existingHlg = _slotsRoot.GetComponent<HorizontalLayoutGroup>();
            if (existingHlg != null) existingHlg.enabled = false;

            int count   = Mathf.Min(spells.Count, 9);
            int rows    = Mathf.CeilToInt(count / 3f);
            float slotW = rows == 1 ? 150f : rows == 2 ? 125f : 105f;
            float slotH = rows == 1 ? 185f : rows == 2 ? 155f : 130f;
            const float gapX = 8f;
            const float gapY = 8f;

            float totalW = 3 * slotW + 2 * gapX;
            float totalH = rows * slotH + (rows - 1) * gapY;

            // Resize slotsRoot to contain all rows
            var slotsRT = _slotsRoot.GetComponent<RectTransform>();
            if (slotsRT != null) slotsRT.sizeDelta = new Vector2(totalW, totalH);

            for (int i = 0; i < count; i++)
            {
                int row = i / 3;
                int col = i % 3;
                // Left-aligned: col 0 starts at left edge regardless of how many cards in the row
                float x = col * (slotW + gapX) - totalW * 0.5f + slotW * 0.5f;
                float y = -(row * (slotH + gapY)) + totalH * 0.5f - slotH * 0.5f;

                var slot = CreateCardSlot(spells[i], _slotsRoot, isPlayer, slotW, slotH);
                var slotRT = slot.GetComponent<RectTransform>();
                if (slotRT != null)
                {
                    slotRT.anchorMin = slotRT.anchorMax = new Vector2(0.5f, 0.5f);
                    slotRT.pivot     = new Vector2(0.5f, 0.5f);
                    slotRT.anchoredPosition = new Vector2(x, y);
                }
            }

            _slotsRoot.gameObject.SetActive(true);
        }

        private static GameObject CreateCardSlot(UnitInstance spell, Transform parent,
                                                  bool isPlayer, float slotW, float slotH)
        {
            var go = new GameObject(spell.UnitName + "_slot");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(slotW, slotH);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.06f, 0.14f, 0.95f);

            var outline = go.AddComponent<Outline>();
            outline.effectColor = isPlayer
                ? new Color(0.29f, 0.87f, 0.5f, 0.8f)
                : new Color(0.97f, 0.44f, 0.44f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            // Card name
            var nameGO = new GameObject("Name");
            nameGO.transform.SetParent(go.transform, false);
            var nameRT = nameGO.AddComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0f, 0.65f);
            nameRT.anchorMax = new Vector2(1f, 0.9f);
            nameRT.offsetMin = new Vector2(4f, 0f);
            nameRT.offsetMax = new Vector2(-4f, 0f);
            var nameText = nameGO.AddComponent<Text>();
            nameText.text      = spell.UnitName;
            nameText.fontSize  = Mathf.RoundToInt(slotW * 0.10f);
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color     = new Color(240f/255f, 230f/255f, 210f/255f);
            nameText.font      = Resources.GetBuiltinResource<Font>("Arial.ttf");
            nameText.fontStyle = FontStyle.Bold;
            nameText.resizeTextForBestFit = true;
            nameText.resizeTextMinSize    = 8;
            nameText.resizeTextMaxSize    = Mathf.RoundToInt(slotW * 0.12f);

            // Effect description
            var descGO = new GameObject("Desc");
            descGO.transform.SetParent(go.transform, false);
            var descRT = descGO.AddComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0f, 0f);
            descRT.anchorMax = new Vector2(1f, 0.65f);
            descRT.offsetMin = new Vector2(6f, 4f);
            descRT.offsetMax = new Vector2(-6f, 0f);
            var descText = descGO.AddComponent<Text>();
            descText.text             = spell.CardData?.Description ?? "";
            descText.fontSize         = Mathf.RoundToInt(slotW * 0.075f);
            descText.alignment        = TextAnchor.UpperCenter;
            descText.color            = new Color(180f/255f, 180f/255f, 180f/255f);
            descText.font             = Resources.GetBuiltinResource<Font>("Arial.ttf");
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descText.verticalOverflow   = VerticalWrapMode.Truncate;

            return go;
        }
    }
}
