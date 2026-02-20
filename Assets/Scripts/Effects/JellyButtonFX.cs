using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EyeSlide.UIFX
{
    // Add this to any Button or clickable UI object to get a candy-like jelly press effect.
    // Works by scaling the target transform on pointer down/up with an overshoot bounce.
    [DisallowMultipleComponent]
    public class JellyButtonFX : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Header("Target")]
        [Tooltip("Transform to scale. If null, this component's transform is used.")]
        public Transform target;

        [Header("Scales")]
        [Tooltip("Multiplier applied on press down.")]
        [Range(0.5f, 1f)] public float pressedScale = 0.92f;
        [Tooltip("Slight overshoot above 1x on release before settling back to 1.")]
        [Range(1f, 1.3f)] public float releaseOvershoot = 1.06f;

        [Header("Timings (seconds)")]
    [Tooltip("Duration of the scale down on press.")]
    [Range(0.01f, 1f)] public float downDuration = 0.12f;
    [Tooltip("Duration to hit the overshoot on release.")]
    [Range(0.01f, 1f)] public float upOvershootDuration = 0.12f;
    [Tooltip("Duration to settle from overshoot to 1x.")]
    [Range(0.01f, 1f)] public float settleDuration = 0.18f;

        [Header("Options")]
        public bool useUnscaledTime = true;
        [Tooltip("Reset scale to 1 when the object is disabled.")]
        public bool resetOnDisable = true;
    [Tooltip("Compensate pivot so scaling appears from the visual center even if the RectTransform pivot isn't centered.")]
    public bool scaleFromCenter = true;

    public enum EasingMode { Smooth, Cubic, Quint, ElasticSoft }
    [Header("Easing")]
    [Tooltip("Interpolation used for down/overshoot/settle phases. 'Smooth' is a classic ease-in-out.")]
    public EasingMode easing = EasingMode.Smooth;

        private Vector3 _baseScale = Vector3.one;
        private Coroutine _routine;
        private bool _isPressed;
        private RectTransform _rt;
        private bool _centerActive;
        private Vector2 _centerBaseAnch; // session base anchoredPosition during a press (anchored mode)
        private Vector2 _initialAnch; // original anchoredPosition at Awake
        private Vector3 _centerBaseLocal; // session base localPosition during a press (local mode)
        private Vector3 _initialLocal; // original localPosition at Awake
        private Vector2 _centerSize;
        private Vector2 _centerPivot;

        public enum PositionMode { Anchored, Local }
        [Header("Center Compensation Mode")]
        [Tooltip("Use Anchored for typical Canvas layout; use Local to avoid scroll/layout reflows (recommended in ScrollViews).")]
        public PositionMode centerCompPositionMode = PositionMode.Anchored;
        [Tooltip("Auto-switch to Local mode if this button is inside a ScrollRect.")]
        public bool autoLocalInScrollRect = true;

        private Transform T => target != null ? target : transform;

        private void Awake()
        {
            _baseScale = T.localScale;
            _rt = T as RectTransform;
            if (_rt != null)
            {
                // Capture original anchor to prevent cumulative drift across presses
                _initialAnch = _rt.anchoredPosition;
                _initialLocal = T.localPosition;
                if (autoLocalInScrollRect)
                {
                    try
                    {
                        var sr = GetComponentInParent<UnityEngine.UI.ScrollRect>();
                        if (sr != null) centerCompPositionMode = PositionMode.Local;
                    }
                    catch { }
                }
            }
        }

        private void OnEnable()
        {
            // Ensure we start from base scale to avoid inherited scale from prefab previews
            T.localScale = _baseScale;
        }

        private void OnDisable()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            _isPressed = false;
            if (resetOnDisable)
            {
                T.localScale = _baseScale;
                if (_rt != null && scaleFromCenter)
                {
                    // Restore to original position for scale=1 based on mode
                    if (centerCompPositionMode == PositionMode.Anchored)
                        _rt.anchoredPosition = _initialAnch;
                    else
                        T.localPosition = _initialLocal;
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressed = true;
            BeginCenterComp();
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ScaleTo(_baseScale * pressedScale, downDuration, easing));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isPressed) return;
            _isPressed = false;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(BounceBack());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // If finger slides off, release the press visually
            if (_isPressed)
            {
                _isPressed = false;
                if (_routine != null) StopCoroutine(_routine);
                _routine = StartCoroutine(BounceBack());
            }
        }

        private IEnumerator BounceBack()
        {
            // Quick overshoot above 1x...
            yield return ScaleTo(_baseScale * releaseOvershoot, upOvershootDuration, easing);
            // ...then settle to exactly 1x
            yield return ScaleTo(_baseScale, settleDuration, easing);
            _routine = null;
            EndCenterComp();
        }

        private IEnumerator ScaleTo(Vector3 targetScale, float duration, EasingMode mode)
        {
            var from = T.localScale;
            RectTransform rt = (scaleFromCenter && _rt != null && _centerActive) ? _rt : null;
            Vector2 baseAnch = rt != null ? _centerBaseAnch : default;
            Vector3 baseLocal = _centerBaseLocal;
            Vector2 size = rt != null ? _centerSize : default;
            Vector2 pivot = rt != null ? _centerPivot : default;
            if (duration <= 0.0001f)
            {
                T.localScale = targetScale;
                if (rt != null)
                {
                    float sRel = SafeRelative(targetScale.x, _baseScale.x);
                    Vector2 delta = (1f - sRel) * new Vector2(0.5f - pivot.x, 0.5f - pivot.y) * size;
                    if (centerCompPositionMode == PositionMode.Anchored)
                        rt.anchoredPosition = baseAnch + delta;
                    else
                        T.localPosition = baseLocal + new Vector3(delta.x, delta.y, 0f);
                }
                yield break;
            }
            float t = 0f;
            while (t < 1f)
            {
                t += (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) / duration;
                float eased = Evaluate(mode, Mathf.Clamp01(t));
                var newScale = Vector3.LerpUnclamped(from, targetScale, eased);
                T.localScale = newScale;
                if (rt != null)
                {
                    float sRel = SafeRelative(newScale.x, _baseScale.x);
                    Vector2 delta = (1f - sRel) * new Vector2(0.5f - pivot.x, 0.5f - pivot.y) * size;
                    if (centerCompPositionMode == PositionMode.Anchored)
                        rt.anchoredPosition = baseAnch + delta;
                    else
                        T.localPosition = baseLocal + new Vector3(delta.x, delta.y, 0f);
                }
                yield return null;
            }
            T.localScale = targetScale;
            if (rt != null)
            {
                float sRel = SafeRelative(targetScale.x, _baseScale.x);
                Vector2 delta = (1f - sRel) * new Vector2(0.5f - pivot.x, 0.5f - pivot.y) * size;
                if (centerCompPositionMode == PositionMode.Anchored)
                    rt.anchoredPosition = baseAnch + delta;
                else
                    T.localPosition = baseLocal + new Vector3(delta.x, delta.y, 0f);
            }
        }

        private static float Evaluate(EasingMode mode, float t)
        {
            switch (mode)
            {
                case EasingMode.Cubic:
                    // classic ease-in-out cubic
                    return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
                case EasingMode.Quint:
                    // stronger ease-in-out quintic
                    return t < 0.5f ? 16f * t * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 5f) / 2f;
                case EasingMode.ElasticSoft:
                    // soft elastic tail near the end
                    const float c4 = (2f * Mathf.PI) / 3f;
                    return Mathf.Pow(2f, -6f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
                case EasingMode.Smooth:
                default:
                    // SmoothStep 0..1 (ease-in-out)
                    return t * t * (3f - 2f * t);
            }
        }

        private static float SafeRelative(float value, float baseValue)
        {
            if (Mathf.Approximately(baseValue, 0f)) return 1f;
            return value / baseValue;
        }

        private void BeginCenterComp()
        {
            if (!scaleFromCenter || _rt == null) { _centerActive = false; return; }
            _centerActive = true;
            // Always start from original positions to avoid drift
            _centerBaseAnch = _initialAnch;
            _centerBaseLocal = _initialLocal;
            _centerSize = _rt.rect.size;
            _centerPivot = _rt.pivot;
        }

        private void EndCenterComp()
        {
            if (!_centerActive) return;
            // Ensure final anchor matches center compensation at current scale
            float sRel = SafeRelative(T.localScale.x, _baseScale.x);
            Vector2 delta = (1f - sRel) * new Vector2(0.5f - _centerPivot.x, 0.5f - _centerPivot.y) * _centerSize;
            if (centerCompPositionMode == PositionMode.Anchored)
                _rt.anchoredPosition = _centerBaseAnch + delta;
            else
                T.localPosition = _centerBaseLocal + new Vector3(delta.x, delta.y, 0f);
            _centerActive = false;
        }
    }
}
