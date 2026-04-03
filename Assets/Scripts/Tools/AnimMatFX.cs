using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace FWTCG.FX
{
    /// <summary>
    /// Drives material property animations (float interpolation) in sequence.
    /// Adapted from TCG Engine's AnimMatFX — namespace changed to FWTCG.FX.
    /// </summary>
    public class AnimMatFX : MonoBehaviour
    {
        private Material _target;
        private float _timer = 0f;
        private float _startVal = 0f;
        private float _currentVal = 0f;

        private AnimMatAction _current = null;
        private Queue<AnimMatAction> _sequence = new Queue<AnimMatAction>();

        void Update()
        {
            if (_target == null)
                return;

            if (_current == null && _sequence.Count > 0)
            {
                _current = _sequence.Dequeue();

                // HIGH-fix #1 + #3: only read GetFloat for Float-type actions; guard missing property
                if (_current.type == AnimMatActionType.Float)
                {
                    if (!_target.HasProperty(_current.targetName))
                    {
                        Debug.LogWarning($"[AnimMatFX] Shader property '{_current.targetName}' not found on {_target.name}. Skipping.");
                        _current = null;
                        return;
                    }
                    _startVal = _target.GetFloat(_current.targetName);
                    _currentVal = _startVal;
                }

                _timer = 0f;
            }

            if (_current != null)
            {
                if (_timer < _current.duration)
                {
                    _timer += Time.deltaTime;

                    if (_current.type == AnimMatActionType.Float)
                    {
                        float dist = Mathf.Abs(_current.targetValue - _startVal);
                        float speed = dist / Mathf.Max(_current.duration, 0.01f);
                        _currentVal = Mathf.MoveTowards(_currentVal, _current.targetValue, speed * Time.deltaTime);
                        _target.SetFloat(_current.targetName, _currentVal);
                    }
                }
                else
                {
                    // HIGH-fix #2: write exact final value before invoking callback
                    if (_current.type == AnimMatActionType.Float)
                        _target.SetFloat(_current.targetName, _current.targetValue);

                    _current.callback?.Invoke();
                    _current = null;
                }
            }
        }

        public void SetFloat(string name, float value, float duration)
        {
            _sequence.Enqueue(new AnimMatAction
            {
                type = AnimMatActionType.Float,
                duration = duration,
                targetName = name,
                targetValue = value,
            });
        }

        public void Callback(float delay, UnityAction callback)
        {
            _sequence.Enqueue(new AnimMatAction
            {
                type = AnimMatActionType.None,
                duration = delay,
                callback = callback,
            });
        }

        public void Clear()
        {
            _target = null;
            _timer = 0f;
            _startVal = 0f;   // MEDIUM-fix: reset stale float state
            _currentVal = 0f;
            _sequence.Clear();
            _current = null;
        }

        public static AnimMatFX Create(GameObject obj, Material target)
        {
            AnimMatFX anim = obj.GetComponent<AnimMatFX>();
            if (anim == null)
                anim = obj.AddComponent<AnimMatFX>();

            anim.Clear();
            anim._target = target;
            return anim;
        }
    }

    public enum AnimMatActionType
    {
        None  = 0,
        Float = 1, // contiguous; reserved values 2-4 for future Color/Vector/Int
    }

    public class AnimMatAction
    {
        public AnimMatActionType type;
        public string targetName;
        public float targetValue;
        public float duration = 1f;
        public UnityAction callback = null;
    }
}
