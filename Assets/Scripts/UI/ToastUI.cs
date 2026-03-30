using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Systems;

namespace FWTCG.UI
{
    /// <summary>
    /// Displays floating toast notifications for BattlefieldSystem events.
    /// Subscribes to BattlefieldSystem.OnBattlefieldLog and queues messages
    /// one at a time: fade-in 0.3s → stay 1.5s → fade-out 0.4s.
    /// </summary>
    public class ToastUI : MonoBehaviour
    {
        [SerializeField] private GameObject  _toastPanel;
        [SerializeField] private Text        _toastText;

        private CanvasGroup      _cg;
        private Queue<string>    _queue = new Queue<string>();
        private bool             _showing;

        private const float FADE_IN   = 0.3f;
        private const float STAY      = 1.5f;
        private const float FADE_OUT  = 0.4f;

        private void Awake()
        {
            if (_toastPanel != null)
            {
                _cg = _toastPanel.GetComponent<CanvasGroup>();
                if (_cg == null) _cg = _toastPanel.AddComponent<CanvasGroup>();
                _toastPanel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            BattlefieldSystem.OnBattlefieldLog += Enqueue;
        }

        private void OnDisable()
        {
            BattlefieldSystem.OnBattlefieldLog -= Enqueue;
        }

        private void Enqueue(string msg)
        {
            _queue.Enqueue(msg);
            if (!_showing) StartCoroutine(ProcessQueue());
        }

        private IEnumerator ProcessQueue()
        {
            _showing = true;
            while (_queue.Count > 0)
            {
                string msg = _queue.Dequeue();
                yield return StartCoroutine(ShowToast(msg));
                yield return new WaitForSeconds(0.05f); // tiny gap between toasts
            }
            _showing = false;
        }

        private IEnumerator ShowToast(string msg)
        {
            if (_toastPanel == null || _toastText == null) yield break;

            _toastText.text = msg;
            _toastPanel.SetActive(true);

            // Fade in
            float t = 0f;
            while (t < FADE_IN)
            {
                _cg.alpha = t / FADE_IN;
                t += Time.deltaTime;
                yield return null;
            }
            _cg.alpha = 1f;

            // Stay
            yield return new WaitForSeconds(STAY);

            // Fade out
            t = 0f;
            while (t < FADE_OUT)
            {
                _cg.alpha = 1f - (t / FADE_OUT);
                t += Time.deltaTime;
                yield return null;
            }
            _cg.alpha = 0f;
            _toastPanel.SetActive(false);
        }
    }
}
