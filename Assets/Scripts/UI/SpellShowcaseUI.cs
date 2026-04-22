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
        public const float FLY_IN_DURATION   = 0.4f;
        public const float HOLD_DURATION     = 0.5f;
        // Single-card outro is now a magical dissolve; group outro is still a fly-out.
        public const float DISSOLVE_DURATION = 0.73f;
        public const float FLY_OUT_DURATION  = DISSOLVE_DURATION; // alias for back-compat with existing callers/tests
        public const float TOTAL_DURATION    = FLY_IN_DURATION + HOLD_DURATION + DISSOLVE_DURATION;

        // ── Dissolve state ─────────────────────────────────────────────────────
        private Material _dissolveMat;       // instance material for _artImage
        private Material _artOriginalMat;    // restored if we ever detach
        private RectTransform _sparksRoot;   // sibling of _cardPanel under overlay

        // ── Queue (task chain) — multiple rapid calls play sequentially ────────
        private Task _queueTail = Task.CompletedTask;

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

            // ── 极简化：去掉卡框/底板/文字，只剩一张大"原画"居中溶解 ──
            StripDecorationsForMinimalLayout();

            // Attach dissolve material to the art image (falls back to fly-out if shader missing).
            if (_artImage != null)
            {
                _artOriginalMat = _artImage.material;
                _dissolveMat = SpellDissolveFX.CreateDissolveMaterial();
                if (_dissolveMat != null)
                    _artImage.material = _dissolveMat;
            }

            // Build a sparks root sibling of _cardPanel under the same overlay container.
            if (_cardPanel != null && _cardPanel.parent is RectTransform parentRT)
            {
                var go = new GameObject("SpellDissolveSparks");
                _sparksRoot = go.AddComponent<RectTransform>();
                _sparksRoot.SetParent(parentRT, false);
                _sparksRoot.anchorMin = _sparksRoot.anchorMax = new Vector2(0.5f, 0.5f);
                _sparksRoot.pivot = new Vector2(0.5f, 0.5f);
                _sparksRoot.anchoredPosition = Vector2.zero;
                _sparksRoot.sizeDelta = _cardPanel.sizeDelta;
                _sparksRoot.SetAsLastSibling();
            }

            Hide();
        }

        /// <summary>
        /// 运行时一次性剥离 SceneBuilder 搭的卡框/玻璃/描边/文字，让展示只剩居中的一张大原画。
        /// 不改场景资产 — 纯运行时覆盖。
        /// </summary>
        private void StripDecorationsForMinimalLayout()
        {
            // 1. 放大卡牌容器到竖向大牌比例（约 20% × 52% 屏幕）
            if (_cardPanel != null)
            {
                _cardPanel.anchorMin = new Vector2(0.40f, 0.24f);
                _cardPanel.anchorMax = new Vector2(0.60f, 0.76f);
                _cardPanel.offsetMin = Vector2.zero;
                _cardPanel.offsetMax = Vector2.zero;

                // 卡牌底板 Image（深蓝方框）—— 隐藏
                var panelImg = _cardPanel.GetComponent<Image>();
                if (panelImg != null) panelImg.enabled = false;

                // GlassPanelFX（玻璃光泽）—— 禁用
                var glass = _cardPanel.GetComponent<GlassPanelFX>();
                if (glass != null) glass.enabled = false;

                // 金色描边子对象（CreateZoneBorderFrame 建的 Border* / ZoneBorder*）—— 隐藏
                foreach (Transform child in _cardPanel)
                {
                    string n = child.name;
                    if (n.StartsWith("Border") || n.StartsWith("ZoneBorder"))
                        child.gameObject.SetActive(false);
                }
            }

            // 2. 隐藏三段文字：归属 / 卡名 / 效果描述
            if (_ownerLabel != null)   _ownerLabel.gameObject.SetActive(false);
            if (_cardNameText != null) _cardNameText.gameObject.SetActive(false);
            if (_effectText != null)   _effectText.gameObject.SetActive(false);

            // 3. 原画填满整个卡牌容器（preserveAspect 会自动保持比例，黑条边缘自然与 dim 融合）
            if (_artImage != null)
            {
                var artRT = _artImage.rectTransform;
                artRT.anchorMin = Vector2.zero;
                artRT.anchorMax = Vector2.one;
                artRT.offsetMin = Vector2.zero;
                artRT.offsetMax = Vector2.zero;
            }
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

        /// <summary>
        /// 强制关闭 showcase 并解除所有 await。RestartGameInPlace 用，
        /// 避免 OnComplete 被 DOTween.KillAll 打断而 IsShowing 永远 true、
        /// 调用方 await 永远不返回的死锁。
        /// </summary>
        public void ForceHide()
        {
            TweenHelper.KillSafe(ref _showSeq);
            _activeTcs?.TrySetResult(true);
            _activeTcs = null;
            _queueTail = null;
            IsShowing = false;
            Hide();
            if (_cardPanel != null) _cardPanel.gameObject.SetActive(false);
            if (_groupPanel != null) _groupPanel.gameObject.SetActive(false);
            if (_slotsRoot != null)
            {
                for (int i = _slotsRoot.childCount - 1; i >= 0; i--)
                    Destroy(_slotsRoot.GetChild(i).gameObject);
            }
            // 清空遗留 sparks（OnComplete 被 KillAll 打断时 sg 会孤立堆积）
            if (_sparksRoot != null)
            {
                for (int i = _sparksRoot.childCount - 1; i >= 0; i--)
                    Destroy(_sparksRoot.GetChild(i).gameObject);
            }
        }

        private void OnDestroy()
        {
            TweenHelper.KillSafe(ref _showSeq);
            // Resolve pending TCS so awaiting callers don't hang
            _activeTcs?.TrySetResult(true);
            _activeTcs = null;
            IsShowing = false;
            if (_dissolveMat != null)
            {
                if (_artImage != null && _artImage.material == _dissolveMat)
                    _artImage.material = _artOriginalMat;
                Destroy(_dissolveMat);
                _dissolveMat = null;
            }
            if (Instance == this) Instance = null;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// 公开入口：按队列顺序播放单张法术展示。多次快速调用会排队，依次播完。
        /// </summary>
        public Task ShowAsync(UnitInstance spell, string owner)
        {
            if (spell == null) return Task.CompletedTask;
            var prev = _queueTail;
            var next = ShowAsyncChained(prev, spell, owner);
            _queueTail = next;
            return next;
        }

        private async Task ShowAsyncChained(Task prev, UnitInstance spell, string owner)
        {
            try { await prev; } catch { /* upstream error is its caller's problem */ }
            if (this == null || !isActiveAndEnabled) return; // singleton destroyed mid-chain
            await ShowSingleInternal(spell, owner);
        }

        /// <summary>
        /// 公开入口：按队列顺序播放多卡群组展示。
        /// </summary>
        public Task ShowGroupAsync(List<UnitInstance> spells, string owner)
        {
            if (spells == null || spells.Count == 0) return Task.CompletedTask;
            if (spells.Count == 1) return ShowAsync(spells[0], owner);
            var prev = _queueTail;
            var next = ShowGroupAsyncChained(prev, spells, owner);
            _queueTail = next;
            return next;
        }

        private async Task ShowGroupAsyncChained(Task prev, List<UnitInstance> spells, string owner)
        {
            try { await prev; } catch { }
            if (this == null || !isActiveAndEnabled) return;
            await ShowGroupInternal(spells, owner);
        }

        private Task ShowGroupInternal(List<UnitInstance> spells, string owner)
        {
            if (spells == null || spells.Count == 0) return Task.CompletedTask;

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
            var seq = DOTween.Sequence();

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
        /// 内部实现：单张法术的完整飞入 + hold + 溶解序列。返回 Task，外层队列负责串行。
        /// </summary>
        private Task ShowSingleInternal(UnitInstance spell, string owner)
        {
            if (spell == null) return Task.CompletedTask;

            var tcs = new TaskCompletionSource<bool>();
            _activeTcs = tcs;
            IsShowing = true;

            // Populate content (sync)
            bool isPlayer = owner == GameRules.OWNER_PLAYER;
            PopulateSingleCard(spell, isPlayer);

            float flyStartY = isPlayer ? -FLY_OFFSET : FLY_OFFSET;

            // Reset position + dissolve state
            if (_cardPanel != null)
            {
                var pos = _cardPanel.anchoredPosition;
                _cardPanel.anchoredPosition = new Vector2(pos.x, flyStartY);
            }
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            SpellDissolveFX.ResetAmount(_dissolveMat);
            SpellDissolveFX.SetDirection(_dissolveMat, isPlayer);

            // Build animation sequence (unscaled time for pause-safe)
            TweenHelper.KillSafe(ref _showSeq);
            var seq = DOTween.Sequence();

            // Fly in
            if (_cardPanel != null)
                seq.Append(_cardPanel.DOAnchorPosY(FLY_END_Y, FLY_IN_DURATION).SetEase(Ease.InOutQuad));
            if (_canvasGroup != null)
                seq.Join(_canvasGroup.DOFade(1f, FLY_IN_DURATION).SetEase(Ease.InOutQuad));

            // Hold
            seq.AppendInterval(HOLD_DURATION);

            // Dissolve outro — bot 高速下跳过（同 CardView：KillAll 打断会留残影）
            bool useDissolve = _dissolveMat != null && _artImage != null
                               && FWTCG.Core.GameTiming.SpeedMultiplier <= 10f;
            if (useDissolve)
            {
                // Burst sparks at dissolve start + mid-way
                seq.AppendCallback(() =>
                {
                    if (_sparksRoot != null && _cardPanel != null)
                    {
                        int n = FWTCG.Core.GameTiming.SpeedMultiplier > 10f ? 10 : 70;
                        SpellDissolveFX.BurstSparks(_sparksRoot, n, _cardPanel.sizeDelta.x, _cardPanel.sizeDelta.y);
                    }
                });
                seq.Append(SpellDissolveFX.TweenAmount(_dissolveMat, 1.12f, DISSOLVE_DURATION));
                if (_cardPanel != null)
                    seq.Join(_cardPanel.DOScale(1.05f, DISSOLVE_DURATION).SetEase(Ease.InOutSine));
                seq.InsertCallback(FLY_IN_DURATION + HOLD_DURATION + DISSOLVE_DURATION * 0.35f, () =>
                {
                    if (_sparksRoot != null && _cardPanel != null)
                    {
                        int n = FWTCG.Core.GameTiming.SpeedMultiplier > 10f ? 5 : 35;
                        SpellDissolveFX.BurstSparks(_sparksRoot, n, _cardPanel.sizeDelta.x, _cardPanel.sizeDelta.y);
                    }
                });
                if (_canvasGroup != null)
                    seq.Append(_canvasGroup.DOFade(0f, 0.23f).SetEase(Ease.InQuad));
            }
            else
            {
                // Shader unavailable — preserve previous fly-out behaviour
                float flyExitY = isPlayer ? -FLY_OFFSET : FLY_OFFSET;
                if (_cardPanel != null)
                    seq.Append(_cardPanel.DOAnchorPosY(flyExitY, DISSOLVE_DURATION).SetEase(Ease.InOutQuad));
                if (_canvasGroup != null)
                    seq.Join(_canvasGroup.DOFade(0f, DISSOLVE_DURATION).SetEase(Ease.InOutQuad));
            }

            seq.OnComplete(() =>
            {
                // Reset card panel scale so next showcase starts clean
                if (_cardPanel != null) _cardPanel.localScale = Vector3.one;
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
            nameText.font      = (Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf"));
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
            descText.font             = (Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf"));
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descText.verticalOverflow   = VerticalWrapMode.Truncate;

            return go;
        }
    }
}
