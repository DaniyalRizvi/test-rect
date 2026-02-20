using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private Image faceImage;
    [SerializeField] private Image backImage;

    [Header("Animation")]
    [SerializeField] private float pressScale = 0.92f;
    [SerializeField] private float pressDuration = 0.08f;
    [SerializeField] private float flipDuration = 0.3f;
    [SerializeField] private float liftHeight = 18f;
    [SerializeField] private float flipScale = 1.08f;

    private Button button;
    private RectTransform rectTransform;
    private Coroutine pressRoutine;
    private Coroutine flipRoutine;
    private bool isPressed;
    private bool pointerInside;
    private Vector3 baseScale;
    private Vector2 basePosition;
    private Quaternion baseRotation;

    public int CardId { get; private set; }
    public bool IsRevealed { get; private set; }
    public bool IsMatched { get; private set; }

    public event Action<CardView> Clicked;

    private void Awake()
    {
        button = GetComponent<Button>();
        rectTransform = transform as RectTransform;
        CacheBaseTransform();
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }

    public void Initialize(int cardId, Sprite faceSprite, Sprite backSprite)
    {
        CardId = cardId;
        if (faceImage != null)
        {
            faceImage.sprite = faceSprite;
        }

        if (backImage != null)
        {
            backImage.sprite = backSprite;
        }

        CacheBaseTransform();
        StartCoroutine(CacheBaseTransformNextFrame());
        ResetState();
    }

    public void RefreshBaseTransform()
    {
        CacheBaseTransform();
    }

    public void Reveal()
    {
        if (IsMatched || IsRevealed)
        {
            return;
        }

        IsRevealed = true;
        StartFlip(true);
    }

    public void Hide()
    {
        if (IsMatched || !IsRevealed)
        {
            return;
        }

        IsRevealed = false;
        StartFlip(false);
    }

    public void Match()
    {
        IsMatched = true;
        IsRevealed = true;
        StopAnimations();
        UpdateVisuals();
        SetInteractable(false);
    }

    public void ResetState()
    {
        IsMatched = false;
        IsRevealed = false;
        StopAnimations();
        UpdateVisuals();
        SetInteractable(true);
    }

    public void SetInteractable(bool value)
    {
        if (button != null)
        {
            button.interactable = value;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsPointerInteractable())
        {
            return;
        }

        isPressed = true;
        pointerInside = true;
        StartPressScale(pressScale);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPressed)
        {
            return;
        }

        isPressed = false;
        StartPressScale(1f);
        if (pointerInside)
        {
            HandleClick();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        if (isPressed)
        {
            isPressed = false;
            StartPressScale(1f);
        }
    }

    private void HandleClick()
    {
        if (IsMatched || IsRevealed)
        {
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }

        Clicked?.Invoke(this);
    }

    private void UpdateVisuals()
    {
        if (faceImage != null)
        {
            faceImage.enabled = IsRevealed || IsMatched;
        }

        if (backImage != null)
        {
            backImage.enabled = !IsRevealed && !IsMatched;
        }
    }

    private bool IsPointerInteractable()
    {
        return !IsMatched && !IsRevealed && (button == null || button.interactable);
    }

    private void StartPressScale(float scaleMultiplier)
    {
        if (pressRoutine != null)
        {
            StopCoroutine(pressRoutine);
        }

        pressRoutine = StartCoroutine(PressScaleRoutine(scaleMultiplier));
    }

    private System.Collections.IEnumerator PressScaleRoutine(float scaleMultiplier)
    {
        CacheBaseTransform();
        float elapsed = 0f;
        Vector3 startScale = rectTransform != null ? rectTransform.localScale : transform.localScale;
        Vector3 targetScale = baseScale * scaleMultiplier;

        while (elapsed < pressDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / pressDuration);
            Vector3 scale = Vector3.Lerp(startScale, targetScale, t);
            if (rectTransform != null)
            {
                rectTransform.localScale = scale;
            }
            else
            {
                transform.localScale = scale;
            }
            yield return null;
        }

        if (rectTransform != null)
        {
            rectTransform.localScale = targetScale;
        }
        else
        {
            transform.localScale = targetScale;
        }

        pressRoutine = null;
    }

    private void StartFlip(bool reveal)
    {
        if (flipRoutine != null)
        {
            StopCoroutine(flipRoutine);
        }

        StopPressRoutine();
        flipRoutine = StartCoroutine(FlipRoutine(reveal));
    }

    private System.Collections.IEnumerator FlipRoutine(bool reveal)
    {
        CacheBaseTransform();
        float elapsed = 0f;
        float half = flipDuration * 0.5f;
        bool visualsSwapped = false;

        while (elapsed < flipDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / flipDuration);
            float liftT = t < 0.5f ? t * 2f : (1f - t) * 2f;
            float scaleT = t < 0.5f ? t * 2f : (1f - t) * 2f;
            float rotationT = Mathf.SmoothStep(0f, 1f, t);
            float yRotation = Mathf.Lerp(0f, 180f, rotationT);

            if (!visualsSwapped && elapsed >= half)
            {
                UpdateVisuals();
                visualsSwapped = true;
            }

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = basePosition + Vector2.up * (liftHeight * liftT);
                rectTransform.localScale = baseScale * Mathf.Lerp(1f, flipScale, scaleT);
                rectTransform.localRotation = baseRotation * Quaternion.Euler(0f, reveal ? yRotation : 180f - yRotation, 0f);
            }
            else
            {
                transform.localScale = baseScale * Mathf.Lerp(1f, flipScale, scaleT);
                transform.localRotation = baseRotation * Quaternion.Euler(0f, reveal ? yRotation : 180f - yRotation, 0f);
            }

            yield return null;
        }

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = basePosition;
            rectTransform.localScale = Vector3.one;
            if (!reveal)
            {
                baseRotation = Quaternion.identity;
            }
            rectTransform.localRotation = baseRotation * Quaternion.Euler(0f, reveal ? 180f : 0f, 0f);
        }
        else
        {
            transform.localScale = Vector3.one;
            if (!reveal)
            {
                baseRotation = Quaternion.identity;
            }
            transform.localRotation = baseRotation * Quaternion.Euler(0f, reveal ? 180f : 0f, 0f);
        }

        flipRoutine = null;
    }

    private void StopAnimations()
    {
        StopPressRoutine();
        if (flipRoutine != null)
        {
            StopCoroutine(flipRoutine);
            flipRoutine = null;
        }

        CacheBaseTransform();
        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one;
            rectTransform.anchoredPosition = basePosition;
            rectTransform.localRotation = baseRotation;
        }
        else
        {
            transform.localScale = Vector3.one;
            transform.localRotation = baseRotation;
        }
    }

    private void StopPressRoutine()
    {
        if (pressRoutine != null)
        {
            StopCoroutine(pressRoutine);
            pressRoutine = null;
        }
    }

    private System.Collections.IEnumerator CacheBaseTransformNextFrame()
    {
        yield return null;
        CacheBaseTransform();
    }

    private void CacheBaseTransform()
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }

        if (rectTransform != null)
        {
            baseScale = rectTransform.localScale;
            basePosition = rectTransform.anchoredPosition;
            baseRotation = rectTransform.localRotation;
        }
        else
        {
            baseScale = transform.localScale;
            baseRotation = transform.localRotation;
        }

        if (baseScale == Vector3.zero)
        {
            baseScale = Vector3.one;
        }
    }
}
