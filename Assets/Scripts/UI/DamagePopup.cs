using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FWTCG.UI
{
    /// <summary>
    /// Floating damage number. Spawned at a card's canvas position, floats up and fades out.
    /// Self-destructs after animation. DEV-17.
    /// </summary>
    public class DamagePopup : MonoBehaviour
    {
        private Text _text;
        private RectTransform _rt;
        private Sequence _seq;

        /// <summary>
        /// Spawns a floating damage number at the given canvas-local position.
        /// Parent should be the root Canvas transform so it renders above everything.
        /// </summary>
        public static DamagePopup Create(int damage, Vector2 canvasLocalPos, Transform canvasRoot)
        {
            var go = new GameObject("DmgPopup");
            go.transform.SetParent(canvasRoot, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(90f, 44f);
            rt.anchoredPosition = canvasLocalPos;
            rt.localScale = Vector3.one;

            var text = go.AddComponent<Text>();
            text.text = $"-{damage}";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 30;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(1f, 0.18f, 0.18f, 1f);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;

            // Outline for readability
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var popup = go.AddComponent<DamagePopup>();
            popup._text = text;
            popup._rt = rt;
            return popup;
        }

        private void Start()
        {
            const float duration = 0.85f;
            const float solidTime = duration * 0.4f;   // 0.34s
            const float fadeTime  = duration * 0.6f;   // 0.51s

            Vector2 endPos = _rt.anchoredPosition + new Vector2(0f, 75f);

            _seq = DOTween.Sequence().SetTarget(gameObject);
            _seq.Append(_rt.DOAnchorPos(endPos, duration).SetEase(Ease.OutQuad));
            _seq.Insert(solidTime, _text.DOFade(0f, fadeTime).SetEase(Ease.Linear));
            _seq.OnComplete(() => Destroy(gameObject));
        }

        private void OnDestroy()
        {
            TweenHelper.KillSafe(ref _seq);
        }
    }
}
