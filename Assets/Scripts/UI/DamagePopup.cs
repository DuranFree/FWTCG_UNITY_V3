using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FWTCG.UI
{
    /// <summary>
    /// Floating damage number. Spawned at a card's canvas position, floats up and fades out.
    /// DEV-17 — DEV-31 cleanup: 对象池化（原每次 new GameObject 导致 GC churn）。
    /// 实例完成动画后回池（SetActive(false)），Create 时优先复用。
    /// </summary>
    public class DamagePopup : MonoBehaviour
    {
        private Text _text;
        private RectTransform _rt;
        private Sequence _seq;
        private Outline _outline;

        // 对象池（懒初始化；场景切换清理见 ClearPool）
        private static readonly Stack<DamagePopup> _pool = new Stack<DamagePopup>();

        /// <summary>
        /// Spawns a floating damage number at the given canvas-local position.
        /// Parent should be the root Canvas transform so it renders above everything.
        /// </summary>
        public static DamagePopup Create(int damage, Vector2 canvasLocalPos, Transform canvasRoot)
        {
            DamagePopup popup = null;
            // 池中取（跳过已被销毁的条目）
            while (popup == null && _pool.Count > 0)
            {
                var candidate = _pool.Pop();
                if (candidate != null) popup = candidate;
            }

            if (popup == null)
            {
                var go = new GameObject("DmgPopup");
                var rt = go.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(90f, 44f);

                var text = go.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 30;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.raycastTarget = false;

                var outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);

                popup = go.AddComponent<DamagePopup>();
                popup._text = text;
                popup._rt = rt;
                popup._outline = outline;
            }
            else
            {
                popup.gameObject.SetActive(true);
            }

            // 重新挂载（池化对象可能已跨 parent；Canvas 单例时大多数情况不变）
            popup.transform.SetParent(canvasRoot, false);
            popup._rt.anchoredPosition = canvasLocalPos;
            popup._rt.localScale = Vector3.one;
            popup._text.text = $"-{damage}";
            popup._text.color = new Color(1f, 0.18f, 0.18f, 1f);
            popup.StartAnimation();
            return popup;
        }

        /// <summary>
        /// 可选：场景卸载 / 测试 reset 时清空池。
        /// </summary>
        public static void ClearPool()
        {
            while (_pool.Count > 0)
            {
                var p = _pool.Pop();
                if (p != null) Destroy(p.gameObject);
            }
        }

        private void StartAnimation()
        {
            TweenHelper.KillSafe(ref _seq);

            const float duration = 0.85f;
            const float solidTime = duration * 0.4f;
            const float fadeTime  = duration * 0.6f;

            Vector2 endPos = _rt.anchoredPosition + new Vector2(0f, 75f);

            _seq = DOTween.Sequence().SetTarget(gameObject).LinkKillOnDestroy(gameObject);
            _rt.localScale = Vector3.one * 0.6f;
            _seq.Append(_rt.DOScale(1.15f, duration * 0.2f).SetEase(Ease.OutBack));
            _seq.Append(_rt.DOScale(1f, duration * 0.1f).SetEase(Ease.InQuad));
            _seq.Join(_rt.DOAnchorPos(endPos, duration).SetEase(Ease.OutCubic));
            _seq.Insert(solidTime, _text.DOFade(0f, fadeTime).SetEase(Ease.InQuad));
            _seq.OnComplete(ReturnToPool);
        }

        private void ReturnToPool()
        {
            if (this == null) return;
            gameObject.SetActive(false);
            _pool.Push(this);
        }

        private void OnDestroy()
        {
            TweenHelper.KillSafe(ref _seq);
        }
    }
}
