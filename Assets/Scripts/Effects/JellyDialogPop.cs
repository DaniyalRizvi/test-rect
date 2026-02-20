using System.Collections;
using UnityEngine;

namespace EyeSlide.UIFX
{
    // Dialog pop-in with optional center-compensated scaling and easing.
    [DisallowMultipleComponent]
    public class JellyDialogPop : MonoBehaviour
    {
        [Header("Target")]
        public Transform target;

        [Header("Scales")]
        [Range(0.01f, 1f)] public float startScale = 0.75f;
        [Range(1f, 1.5f)] public float overshootScale = 1.08f;

        [Header("Timings (seconds)")]
        [Range(0.01f, 1f)] public float inDuration = 0.18f; // start -> overshoot
        [Range(0.01f, 1f)] public float settleDuration = 0.22f; // overshoot -> base
        [Tooltip("Optional delay before the pop-in starts.")]
        [Range(0f, 1f)] public float delay = 0f;

        [Header("Options")]
        public bool useUnscaledTime = true;
        [Tooltip("Fade CanvasGroup alpha from 0->1 during pop.")]
        public bool fadeIfCanvasGroupFound = true;
        [Tooltip("Compensate pivot so scaling appears from visual center.")]
        public bool scaleFromCenter = true;

        public enum EasingMode { Smooth, Cubic, Quint }
        [Header("Easing")]
        public EasingMode easing = EasingMode.Smooth;

        private Vector3 _baseScale = Vector3.one;
        private Coroutine _routine;
        private CanvasGroup _cg;
        private RectTransform _rt;
        private Vector2 _centerBaseAnch;
        private Vector2 _centerSize;
        private Vector2 _centerPivot;

        private Transform T => target != null ? target : transform;

        private void Awake()
        {
            _baseScale = T.localScale;
            _cg = T.GetComponent<CanvasGroup>();
            _rt = T as RectTransform;
        }

        private void OnEnable()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Play());
        }

        private void OnDisable()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            T.localScale = _baseScale;
            if (_cg != null && fadeIfCanvasGroupFound) _cg.alpha = 1f;
            if (_rt != null && scaleFromCenter) _rt.anchoredPosition = _centerBaseAnch;
        }

        private IEnumerator Play()
        {
            if (delay > 0f)
            {
                if (useUnscaledTime)
                {
                    float end = Time.unscaledTime + delay; while (Time.unscaledTime < end) yield return null;
                }
                else yield return new WaitForSeconds(delay);
            }

            CacheCenter();
            T.localScale = _baseScale * startScale;
            if (_cg != null && fadeIfCanvasGroupFound) _cg.alpha = 0f;

            // Start -> overshoot (fade to 1)
            yield return ScalePhase(_baseScale * overshootScale, inDuration, fadeTo: 1f);
            // Overshoot -> base
            yield return ScalePhase(_baseScale, settleDuration);

            _routine = null;
        }

        private void CacheCenter()
        {
            if (!scaleFromCenter || _rt == null)
            {
                _centerBaseAnch = Vector2.zero; _centerSize = Vector2.zero; _centerPivot = Vector2.zero; return;
            }
            _centerBaseAnch = _rt.anchoredPosition;
            _centerSize = _rt.rect.size;
            _centerPivot = _rt.pivot;
        }

        private IEnumerator ScalePhase(Vector3 targetScale, float duration, float? fadeTo = null)
        {
            var from = T.localScale;
            float t = 0f;
            float startAlpha = (_cg != null && fadeIfCanvasGroupFound) ? _cg.alpha : 1f;
            float endAlpha = fadeTo.HasValue ? fadeTo.Value : startAlpha;
            if (duration <= 0.0001f)
            {
                T.localScale = targetScale;
                if (_cg != null && fadeIfCanvasGroupFound) _cg.alpha = endAlpha;
                ApplyCenterComp(targetScale);
                yield break;
            }
            while (t < 1f)
            {
                t += (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) / duration;
                float eased = Evaluate(easing, Mathf.Clamp01(t));
                var newScale = Vector3.LerpUnclamped(from, targetScale, eased);
                T.localScale = newScale;
                ApplyCenterComp(newScale);
                if (_cg != null && fadeIfCanvasGroupFound)
                {
                    _cg.alpha = Mathf.Lerp(startAlpha, endAlpha, Mathf.SmoothStep(0f, 1f, t));
                }
                yield return null;
            }
            T.localScale = targetScale;
            ApplyCenterComp(targetScale);
            if (_cg != null && fadeIfCanvasGroupFound) _cg.alpha = endAlpha;
        }

        private void ApplyCenterComp(Vector3 currentScale)
        {
            if (!scaleFromCenter || _rt == null) return;
            float sRel = SafeRelative(currentScale.x, _baseScale.x);
            Vector2 delta = (1f - sRel) * new Vector2(0.5f - _centerPivot.x, 0.5f - _centerPivot.y) * _centerSize;
            _rt.anchoredPosition = _centerBaseAnch + delta;
        }

        private static float Evaluate(EasingMode mode, float t)
        {
            switch (mode)
            {
                case EasingMode.Cubic:
                    return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
                case EasingMode.Quint:
                    return t < 0.5f ? 16f * t * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 5f) / 2f;
                case EasingMode.Smooth:
                default:
                    return t * t * (3f - 2f * t);
            }
        }

        private static float SafeRelative(float value, float baseValue)
        {
            if (Mathf.Approximately(baseValue, 0f)) return 1f;
            return value / baseValue;
        }
    }
}
