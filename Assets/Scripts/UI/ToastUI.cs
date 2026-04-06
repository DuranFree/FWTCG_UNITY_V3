using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Systems;
using FWTCG;

namespace FWTCG.UI
{
    /// <summary>
    /// Displays floating toast notifications for hint messages and battlefield events.
    ///
    /// Dedup + fast response:
    ///   - Same message while showing → resets stay timer (no re-queue, no flicker)
    ///   - Same message already in queue → dropped
    ///   - Queue cap MAX_QUEUE: extra messages beyond cap are dropped
    ///   - ClearAll: subscribed to GameEventBus.OnClearBanners (fires on turn start)
    ///   - Speed: fade-in 0.15s / stay 0.8s / fade-out 0.2s
    /// </summary>
    public class ToastUI : MonoBehaviour
    {
        [SerializeField] private GameObject  _toastPanel;
        [SerializeField] private Text        _toastText;

        private CanvasGroup   _cg;
        private Queue<string> _queue         = new Queue<string>();
        private bool          _showing;
        private string        _currentText;
        private bool          _extendCurrent; // reset stay timer when same message fires
        private Tween         _fadeTween;

        private const float FADE_IN   = 0.15f;
        private const float STAY      = 0.8f;
        private const float FADE_OUT  = 0.2f;
        private const int   MAX_QUEUE = 3;

        private void Awake()
        {
            if (_toastPanel != null)
            {
                _cg = _toastPanel.GetComponent<CanvasGroup>();
                if (_cg == null) _cg = _toastPanel.AddComponent<CanvasGroup>();
                _toastPanel.SetActive(false);
            }
            if (_toastText != null) _toastText.supportRichText = true;
        }

        private void OnEnable()
        {
            BattlefieldSystem.OnBattlefieldLog += Enqueue;
            GameEventBus.OnHintToast           += Enqueue; // DEV-27: migrated from GameManager
            GameEventBus.OnClearBanners        += ClearAll;
        }

        private void OnDisable()
        {
            BattlefieldSystem.OnBattlefieldLog -= Enqueue;
            GameEventBus.OnHintToast           -= Enqueue;
            GameEventBus.OnClearBanners        -= ClearAll;
        }

        // ── Public API ──────────────────���─────────────────────────────────────

        /// <summary>Immediately stops all toasts and clears the queue (e.g. on turn change).</summary>
        public void ClearAll()
        {
            StopAllCoroutines();
            TweenHelper.KillSafe(ref _fadeTween);
            _queue.Clear();
            _showing       = false;
            _currentText   = null;
            _extendCurrent = false;
            if (_cg != null) _cg.alpha = 0f;
            if (_toastPanel != null) _toastPanel.SetActive(false);
        }

        // ── Private ───────────────────────────────────��───────────────────────

        private void Enqueue(string msg)
        {
            // DEV-26: guard against null/empty messages matching each other
            if (string.IsNullOrEmpty(msg)) return;

            // Same message currently showing → reset its stay timer instead of re-queuing
            if (_showing && msg == _currentText)
            {
                _extendCurrent = true;
                return;
            }

            // Already in queue → drop duplicate
            if (_queue.Contains(msg)) return;

            // Queue full → drop incoming (prevent pileup)
            if (_queue.Count >= MAX_QUEUE) return;

            _queue.Enqueue(msg);
            if (!_showing) StartCoroutine(ProcessQueue());
        }

        private IEnumerator ProcessQueue()
        {
            _showing = true;
            while (_queue.Count > 0)
            {
                string msg = _queue.Dequeue();
                yield return ShowToastRoutine(msg);
                yield return new WaitForSeconds(0.04f);
            }
            _showing     = false;
            _currentText = null;
        }

        private IEnumerator ShowToastRoutine(string msg)
        {
            if (_toastPanel == null || _toastText == null) yield break;

            _currentText    = msg;
            _extendCurrent  = false;
            _toastText.text = msg;
            _toastPanel.SetActive(true);

            // Fade in via DOTween
            TweenHelper.KillSafe(ref _fadeTween);
            if (_cg != null)
            {
                _cg.alpha = 0f;
                _fadeTween = TweenHelper.FadeCanvasGroup(_cg, 1f, FADE_IN);
                yield return _fadeTween.WaitForCompletion();
            }

            // Stay — resets whenever the same message fires again
            float elapsed = 0f;
            while (elapsed < STAY)
            {
                if (_extendCurrent)
                {
                    elapsed        = 0f;
                    _extendCurrent = false;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Fade out via DOTween
            TweenHelper.KillSafe(ref _fadeTween);
            if (_cg != null)
            {
                _fadeTween = TweenHelper.FadeCanvasGroup(_cg, 0f, FADE_OUT);
                yield return _fadeTween.WaitForCompletion();
            }

            if (_toastPanel != null) _toastPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            TweenHelper.KillSafe(ref _fadeTween);
        }
    }
}
