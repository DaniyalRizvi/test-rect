using System;
using UnityEngine;
using UnityEngine.UI;

public class CardView : MonoBehaviour
{
    [SerializeField] private Image faceImage;
    [SerializeField] private Image backImage;

    private Button button;

    public int CardId { get; private set; }
    public bool IsRevealed { get; private set; }
    public bool IsMatched { get; private set; }

    public event Action<CardView> Clicked;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(HandleClick);
        }
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

        ResetState();
    }

    public void Reveal()
    {
        if (IsMatched)
        {
            return;
        }

        IsRevealed = true;
        UpdateVisuals();
    }

    public void Hide()
    {
        if (IsMatched)
        {
            return;
        }

        IsRevealed = false;
        UpdateVisuals();
    }

    public void Match()
    {
        IsMatched = true;
        IsRevealed = true;
        UpdateVisuals();
        SetInteractable(false);
    }

    public void ResetState()
    {
        IsMatched = false;
        IsRevealed = false;
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
}
