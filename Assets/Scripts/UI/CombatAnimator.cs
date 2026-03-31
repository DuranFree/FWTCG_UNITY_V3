using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Systems;

namespace FWTCG.UI
{
    /// <summary>
    /// DEV-18: Plays a shockwave effect on the relevant battlefield panel
    /// whenever CombatSystem.OnCombatResult fires.
    ///
    /// The shockwave is a radial Image (circle sprite) that scales from 0 → 1.5
    /// while fading from opaque → transparent over 0.45s.
    ///
    /// Assign _bf1Panel and _bf2Panel to the two BF root GameObjects.
    /// The shockwave Image is a child created at runtime so nothing needs to be
    /// pre-wired in the scene — SceneBuilder calls Init() after building.
    /// </summary>
    public class CombatAnimator : MonoBehaviour
    {
        [SerializeField] private RectTransform _bf1Panel;
        [SerializeField] private RectTransform _bf2Panel;

        // Shockwave parameters
        private const float SHOCKWAVE_DURATION  = 0.45f;
        private const float SHOCKWAVE_END_SCALE = 1.5f;
        private static readonly Color SHOCKWAVE_COLOR_GOLD = new Color(0.78f, 0.67f, 0.43f, 0.80f);
        private static readonly Color SHOCKWAVE_COLOR_CYAN = new Color(0.04f, 0.78f, 0.72f, 0.50f);

        // Runtime shockwave images (one per BF, created in Start)
        private Image _shockwave1;
        private Image _shockwave2;

        private void Awake()
        {
            CombatSystem.OnCombatResult += OnCombatResult;
        }

        private void OnDestroy()
        {
            CombatSystem.OnCombatResult -= OnCombatResult;
        }

        private void Start()
        {
            _shockwave1 = CreateShockwave(_bf1Panel);
            _shockwave2 = CreateShockwave(_bf2Panel);
        }

        /// <summary>Called from SceneBuilder after panels are assigned.</summary>
        public void Init(RectTransform bf1, RectTransform bf2)
        {
            _bf1Panel = bf1;
            _bf2Panel = bf2;
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void OnCombatResult(CombatSystem.CombatResult result)
        {
            // Determine which BF index fought. BFName is "战场1" or "战场2" etc.
            int bfIdx = result.BFName.Contains("1") ? 0 : 1;
            Image target = bfIdx == 0 ? _shockwave1 : _shockwave2;
            if (target != null)
                StartCoroutine(PlayShockwave(target));
        }

        private IEnumerator PlayShockwave(Image img)
        {
            if (img == null) yield break;

            RectTransform rt = img.rectTransform;
            float elapsed = 0f;

            // Start state
            rt.localScale = Vector3.zero;
            img.color = SHOCKWAVE_COLOR_GOLD;
            img.gameObject.SetActive(true);

            while (elapsed < SHOCKWAVE_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / SHOCKWAVE_DURATION);

                float scale = Mathf.Lerp(0f, SHOCKWAVE_END_SCALE, t);
                rt.localScale = new Vector3(scale, scale, 1f);

                // Fade out: start at full alpha, finish transparent
                float alpha = Mathf.Lerp(0.80f, 0f, t);
                img.color = new Color(SHOCKWAVE_COLOR_GOLD.r, SHOCKWAVE_COLOR_GOLD.g,
                                      SHOCKWAVE_COLOR_GOLD.b, alpha);
                yield return null;
            }

            img.gameObject.SetActive(false);
            rt.localScale = Vector3.zero;
        }

        private static Image CreateShockwave(RectTransform parent)
        {
            if (parent == null) return null;

            var go = new GameObject("ShockwaveRing");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(120f, 120f);

            var img = go.AddComponent<Image>();
            // Use built-in circle sprite or fall back to white quad
            Sprite circle = Resources.Load<Sprite>("UI/circle");
            if (circle != null) img.sprite = circle;
            img.color = new Color(0.78f, 0.67f, 0.43f, 0f);
            img.raycastTarget = false;

            go.SetActive(false);
            return img;
        }
    }
}
