using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MemoryGameController : MonoBehaviour
{
    private const string SaveLevelKey = "MemoryGame.Level";
    private const string SaveScoreKey = "MemoryGame.Score";
    private const string SaveDataKey = "MemoryGame.SaveData";

    [System.Serializable]
    private class CardSaveState
    {
        public int cardId;
        public int faceIndex;
        public bool matched;
        public bool revealed;
        public Vector2 position;
    }

    [System.Serializable]
    private class SaveData
    {
        public int levelIndex;
        public int score;
        public int movesUsed;
        public int movesLimit;
        public int rows;
        public int columns;
        public float previewTime;
        public float mismatchDelay;
        public List<CardSaveState> cards;
    }

    [Header("Grid")]
    [SerializeField] private GridLayoutGroup gridLayout;
    [SerializeField] private CardView cardPrefab;

    [Header("Cards")]
    [SerializeField] private Sprite cardBackSprite;
    [SerializeField] private List<Sprite> cardFaces = new List<Sprite>();

    [Header("UI")]
    [SerializeField] private Text movesText;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text comboText;
    [SerializeField] private Text levelText;

    [Header("Scoring")]
    [SerializeField] private int scorePerMatch = 5;
    [SerializeField] private int comboBonusPerMatch = 2;
    [SerializeField] private float comboDisplayTime = 0.75f;

    [Header("Level Settings")]
    [SerializeField] private int rows = 2;
    [SerializeField] private int columns = 2;
    [SerializeField] private float previewTime = 2f;
    [SerializeField] private float mismatchDelay = 0.6f;

    [Header("Progression")]
    [SerializeField] private float levelCompleteDelay = 1.5f;
    [SerializeField] private float levelFailedDelay = 1.5f;
    [SerializeField] private int maxRows = 4;
    [SerializeField] private int maxColumns = 4;
    [SerializeField] private int sizeIncreaseEveryLevels = 1;
    [SerializeField] private int rowIncreaseStep = 1;
    [SerializeField] private int columnIncreaseStep = 1;
    [SerializeField] private float previewIncreasePerPair = 0.05f;
    [SerializeField] private float maxPreviewTime = 4f;
    [SerializeField] private float mismatchDecreasePerLevel = 0.05f;
    [SerializeField] private float minMismatchDelay = 0.2f;

    [Header("Moves")]
    [SerializeField] private float baseMovesPerPair = 1.5f;
    [SerializeField] private float bonusMoveChance = 0.35f;
    [SerializeField] private int bonusMovesMin = 1;
    [SerializeField] private int bonusMovesMax = 3;

    [Header("Layout")]
    [SerializeField] private bool randomizePositions = false;
    [SerializeField] private Vector2 randomPadding = new Vector2(20f, 20f);
    [SerializeField] private Vector2 randomSpacing = new Vector2(10f, 10f);

    private readonly List<CardView> cards = new List<CardView>();
    private readonly List<int> cardFaceIndices = new List<int>();
    private CardView firstSelection;
    private CardView secondSelection;
    private bool isBusy;
    private int matchedCount;
    private int levelIndex;
    private int startRows;
    private int startColumns;
    private float startPreviewTime;
    private float startMismatchDelay;
    private int movesUsed;
    private int movesLimit;
    private int score;
    private int comboStreak;
    private Coroutine comboRoutine;
    private SaveData loadedData;

    private void Start()
    {
        EnsureEvenGrid();
        startRows = rows;
        startColumns = columns;
        startPreviewTime = previewTime;
        startMismatchDelay = mismatchDelay;
        LoadProgress();
        UpdateScoreText();
        UpdateMovesText();
        UpdateLevelText();
        if (comboText != null)
        {
            comboText.gameObject.SetActive(false);
        }
        StartCoroutine(StartLevel(loadedData != null && loadedData.cards != null && loadedData.cards.Count > 0));
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveProgress();
        }
    }

    private void OnApplicationQuit()
    {
        SaveProgress();
    }

    public void ClearSaveData()
    {
        PlayerPrefs.DeleteKey(SaveLevelKey);
        PlayerPrefs.DeleteKey(SaveScoreKey);
        PlayerPrefs.DeleteKey(SaveDataKey);
        levelIndex = 0;
        score = 0;
        rows = startRows;
        columns = startColumns;
        previewTime = startPreviewTime;
        mismatchDelay = startMismatchDelay;
        movesUsed = 0;
        movesLimit = 0;
        loadedData = null;
        UpdateScoreText();
        UpdateMovesText();
        UpdateLevelText();
        StartCoroutine(StartLevel(false));
    }

    public void RestartCurrentLevel()
    {
        if (isBusy)
        {
            return;
        }

        StartCoroutine(RestartLevel());
    }

    private System.Collections.IEnumerator StartLevel(bool useSavedState)
    {
        ClearGrid();
        if (useSavedState && loadedData != null && loadedData.cards != null && loadedData.cards.Count > 0)
        {
            GenerateCardsFromSave(loadedData);
            ApplySavedLevelState(loadedData);
            yield return PreviewAllCards();
            ApplySavedCardStates(loadedData);
            loadedData = null;
            SaveProgress();
            yield break;
        }

        GenerateCards();
        ResetLevelState(false);
        yield return PreviewAllCards();
        SaveProgress();
    }

    private void GenerateCards()
    {
        int totalCards = rows * columns;
        if (totalCards <= 0 || totalCards % 2 != 0)
        {
            Debug.LogError("Grid size must be a positive even number.");
            return;
        }

        if (cardFaces.Count == 0)
        {
            Debug.LogError("No card faces assigned.");
            return;
        }

        cardFaceIndices.Clear();
        int pairsNeeded = totalCards / 2;
        var deck = new List<(int id, int faceIndex, Sprite sprite)>(totalCards);
        for (int i = 0; i < pairsNeeded; i++)
        {
            int faceIndex = i % cardFaces.Count;
            deck.Add((i, faceIndex, cardFaces[faceIndex]));
            deck.Add((i, faceIndex, cardFaces[faceIndex]));
        }

        Shuffle(deck);

        if (gridLayout != null)
        {
            gridLayout.enabled = !randomizePositions;
        }

        for (int i = 0; i < deck.Count; i++)
        {
            var card = Instantiate(cardPrefab, gridLayout.transform);
            card.Initialize(deck[i].id, deck[i].sprite, cardBackSprite);
            card.Clicked += HandleCardClicked;
            cards.Add(card);
            cardFaceIndices.Add(deck[i].faceIndex);
        }

        if (randomizePositions)
        {
            RandomizeCardPositions();
        }

        matchedCount = 0;
    }

    private void HandleCardClicked(CardView card)
    {
        if (isBusy)
        {
            return;
        }

        if (firstSelection == null)
        {
            firstSelection = card;
            firstSelection.Reveal();
            return;
        }

        if (secondSelection == null)
        {
            secondSelection = card;
            secondSelection.Reveal();
            movesUsed++;
            UpdateMovesText();
            StartCoroutine(ResolveSelection());
        }
    }

    private System.Collections.IEnumerator ResolveSelection()
    {
        isBusy = true;

        if (firstSelection.CardId == secondSelection.CardId)
        {
            firstSelection.Match();
            secondSelection.Match();
            matchedCount += 2;
            comboStreak++;
            int comboBonus = comboStreak > 1 ? comboBonusPerMatch * (comboStreak - 1) : 0;
            score += scorePerMatch + comboBonus;
            UpdateScoreText();
            ShowCombo(comboStreak > 1);
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayMatch();
            }
        }
        else
        {
            comboStreak = 0;
            yield return new WaitForSeconds(mismatchDelay);
            firstSelection.Hide();
            secondSelection.Hide();
        }

        firstSelection = null;
        secondSelection = null;
        SaveProgress();

        if (matchedCount >= cards.Count)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayGameWin();
            }
            StartCoroutine(AdvanceLevel());
            yield break;
        }

        if (movesUsed >= movesLimit)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayGameOver();
            }
            StartCoroutine(RestartLevel());
            yield break;
        }

        isBusy = false;
    }

    private System.Collections.IEnumerator AdvanceLevel()
    {
        isBusy = true;
        yield return new WaitForSeconds(levelCompleteDelay);
        levelIndex++;
        ApplyDifficulty();
        UpdateLevelText();
        isBusy = false;
        StartCoroutine(StartLevel(false));
    }

    private System.Collections.IEnumerator RestartLevel()
    {
        isBusy = true;
        yield return new WaitForSeconds(levelFailedDelay);
        ResetCurrentLevelState(false);
        yield return PreviewAllCards();
        SaveProgress();
        isBusy = false;
    }

    private void ResetCurrentLevelState(bool save = true)
    {
        matchedCount = 0;
        movesUsed = 0;
        comboStreak = 0;
        movesLimit = CalculateMovesLimit();
        score = 0;
        UpdateMovesText();
        UpdateScoreText();
        if (comboText != null)
        {
            comboText.gameObject.SetActive(false);
        }

        foreach (var card in cards)
        {
            card.ResetState();
        }

        if (save)
        {
            SaveProgress();
        }
    }

    private System.Collections.IEnumerator PreviewAllCards()
    {
        foreach (var card in cards)
        {
            card.Reveal();
        }

        yield return new WaitForSeconds(previewTime);

        foreach (var card in cards)
        {
            card.Hide();
        }
    }

    private void ApplyDifficulty()
    {
        int sizeIncrease = sizeIncreaseEveryLevels > 0 ? levelIndex / sizeIncreaseEveryLevels : levelIndex;
        rows = Mathf.Min(startRows + (sizeIncrease * rowIncreaseStep), maxRows);
        columns = Mathf.Min(startColumns + (sizeIncrease * columnIncreaseStep), maxColumns);
        EnsureEvenGrid();
        int pairs = (rows * columns) / 2;
        previewTime = Mathf.Min(maxPreviewTime, startPreviewTime + (pairs * previewIncreasePerPair));
        mismatchDelay = Mathf.Max(minMismatchDelay, startMismatchDelay - (levelIndex * mismatchDecreasePerLevel));
    }

    private void ResetLevelState(bool keepCardStates = false)
    {
        matchedCount = 0;
        movesUsed = 0;
        comboStreak = 0;
        movesLimit = CalculateMovesLimit();
        UpdateMovesText();
        if (comboText != null)
        {
            comboText.gameObject.SetActive(false);
        }

        if (keepCardStates)
        {
            foreach (var card in cards)
            {
                card.Hide();
            }
        }
    }

    private int CalculateMovesLimit()
    {
        int pairs = (rows * columns) / 2;
        int baseMoves = Mathf.Max(1, Mathf.CeilToInt(pairs * baseMovesPerPair));
        if (Random.value < bonusMoveChance)
        {
            int bonus = Random.Range(bonusMovesMin, bonusMovesMax + 1);
            baseMoves += bonus;
        }

        return baseMoves;
    }

    private void UpdateMovesText()
    {
        if (movesText != null)
        {
            movesText.text = $"Moves:\n{movesUsed}/{movesLimit}";
        }
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
        {
            scoreText.text = $"Score:\n{score}";
        }
    }

    private void UpdateLevelText()
    {
        if (levelText != null)
        {
            levelText.text = $"Level: {levelIndex + 1}";
        }
    }

    private void ShowCombo(bool active)
    {
        if (comboText == null)
        {
            return;
        }

        if (!active)
        {
            comboText.gameObject.SetActive(false);
            return;
        }

        comboText.gameObject.SetActive(true);
        if (comboRoutine != null)
        {
            StopCoroutine(comboRoutine);
        }
        comboRoutine = StartCoroutine(HideComboAfterDelay());
    }

    private System.Collections.IEnumerator HideComboAfterDelay()
    {
        yield return new WaitForSeconds(comboDisplayTime);
        if (comboText != null)
        {
            comboText.gameObject.SetActive(false);
        }
    }

    private void RandomizeCardPositions()
    {
        if (gridLayout == null)
        {
            return;
        }

        var parentRect = gridLayout.transform as RectTransform;
        if (parentRect == null || cards.Count == 0)
        {
            return;
        }

        var sampleRect = cards[0].transform as RectTransform;
        if (sampleRect == null)
        {
            return;
        }

        float cardWidth = sampleRect.rect.width > 0f ? sampleRect.rect.width : sampleRect.sizeDelta.x;
        float cardHeight = sampleRect.rect.height > 0f ? sampleRect.rect.height : sampleRect.sizeDelta.y;
        if (cardWidth <= 0f || cardHeight <= 0f)
        {
            return;
        }

        float width = Mathf.Max(0f, parentRect.rect.width - (randomPadding.x * 2f));
        float height = Mathf.Max(0f, parentRect.rect.height - (randomPadding.y * 2f));
        float cellWidth = cardWidth + randomSpacing.x;
        float cellHeight = cardHeight + randomSpacing.y;

        int columnsCount = Mathf.FloorToInt(width / cellWidth);
        int rowsCount = Mathf.FloorToInt(height / cellHeight);
        if (columnsCount <= 0 || rowsCount <= 0 || columnsCount * rowsCount < cards.Count)
        {
            gridLayout.enabled = true;
            Shuffle(cards);
            foreach (var card in cards)
            {
                card.transform.SetAsLastSibling();
            }
            return;
        }

        var positions = new List<Vector2>(columnsCount * rowsCount);
        float startX = -width * 0.5f + (cellWidth * 0.5f);
        float startY = -height * 0.5f + (cellHeight * 0.5f);
        for (int row = 0; row < rowsCount; row++)
        {
            for (int column = 0; column < columnsCount; column++)
            {
                positions.Add(new Vector2(startX + (column * cellWidth), startY + (row * cellHeight)));
            }
        }

        Shuffle(positions);

        for (int i = 0; i < cards.Count; i++)
        {
            var rect = cards[i].transform as RectTransform;
            if (rect == null)
            {
                continue;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = positions[i];
        }
    }

    private void EnsureEvenGrid()
    {
        int totalCards = rows * columns;
        if (totalCards % 2 == 0)
        {
            return;
        }

        if (columns < maxColumns)
        {
            columns++;
        }
        else if (rows < maxRows)
        {
            rows++;
        }
        else if (columns > 1)
        {
            columns--;
        }
        else if (rows > 1)
        {
            rows--;
        }
    }

    private void ClearGrid()
    {
        foreach (var card in cards)
        {
            if (card != null)
            {
                card.Clicked -= HandleCardClicked;
                Destroy(card.gameObject);
            }
        }

        cards.Clear();
        firstSelection = null;
        secondSelection = null;
        isBusy = false;
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void SaveProgress()
    {
        var data = new SaveData
        {
            levelIndex = levelIndex,
            score = score,
            movesUsed = movesUsed,
            movesLimit = movesLimit,
            rows = rows,
            columns = columns,
            previewTime = previewTime,
            mismatchDelay = mismatchDelay,
            cards = new List<CardSaveState>(cards.Count)
        };

        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            var rect = card.transform as RectTransform;
            int faceIndex = i < cardFaceIndices.Count ? cardFaceIndices[i] : 0;
            data.cards.Add(new CardSaveState
            {
                cardId = card.CardId,
                faceIndex = faceIndex,
                matched = card.IsMatched,
                revealed = card.IsRevealed,
                position = rect != null ? rect.anchoredPosition : Vector2.zero
            });
        }

        PlayerPrefs.SetString(SaveDataKey, JsonUtility.ToJson(data));
        PlayerPrefs.SetInt(SaveLevelKey, levelIndex);
        PlayerPrefs.SetInt(SaveScoreKey, score);
        PlayerPrefs.Save();
    }

    private void LoadProgress()
    {
        loadedData = null;
        if (PlayerPrefs.HasKey(SaveDataKey))
        {
            var json = PlayerPrefs.GetString(SaveDataKey);
            if (!string.IsNullOrEmpty(json))
            {
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data != null && data.cards != null && data.cards.Count > 0)
                {
                    levelIndex = Mathf.Max(0, data.levelIndex);
                    score = Mathf.Max(0, data.score);
                    movesUsed = Mathf.Max(0, data.movesUsed);
                    movesLimit = Mathf.Max(1, data.movesLimit);
                    rows = Mathf.Max(1, data.rows);
                    columns = Mathf.Max(1, data.columns);
                    previewTime = data.previewTime > 0f ? data.previewTime : startPreviewTime;
                    mismatchDelay = data.mismatchDelay > 0f ? data.mismatchDelay : startMismatchDelay;
                    loadedData = data;
                    return;
                }
            }
        }

        if (PlayerPrefs.HasKey(SaveLevelKey))
        {
            levelIndex = Mathf.Max(0, PlayerPrefs.GetInt(SaveLevelKey));
            score = Mathf.Max(0, PlayerPrefs.GetInt(SaveScoreKey));
            ApplyDifficulty();
        }
        else
        {
            levelIndex = 0;
            score = 0;
            rows = startRows;
            columns = startColumns;
            previewTime = startPreviewTime;
            mismatchDelay = startMismatchDelay;
        }
    }

    private void GenerateCardsFromSave(SaveData data)
    {
        if (data == null || data.cards == null || data.cards.Count == 0)
        {
            return;
        }

        cardFaceIndices.Clear();
        if (gridLayout != null)
        {
            gridLayout.enabled = !randomizePositions;
        }

        foreach (var savedCard in data.cards)
        {
            int faceIndex = cardFaces.Count > 0 ? Mathf.Clamp(savedCard.faceIndex, 0, cardFaces.Count - 1) : 0;
            var faceSprite = cardFaces.Count > 0 ? cardFaces[faceIndex] : null;
            var card = Instantiate(cardPrefab, gridLayout.transform);
            card.Initialize(savedCard.cardId, faceSprite, cardBackSprite);
            card.Clicked += HandleCardClicked;
            cards.Add(card);
            cardFaceIndices.Add(faceIndex);

            if (randomizePositions)
            {
                var rect = card.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = savedCard.position;
                }
            }
        }
    }

    private void ApplySavedCardStates(SaveData data)
    {
        if (data == null || data.cards == null)
        {
            return;
        }

        matchedCount = 0;
        int count = Mathf.Min(cards.Count, data.cards.Count);
        for (int i = 0; i < count; i++)
        {
            var card = cards[i];
            var savedCard = data.cards[i];

            if (savedCard.matched)
            {
                card.Match();
                matchedCount++;
                continue;
            }

            if (savedCard.revealed)
            {
                card.Reveal();
            }
            else
            {
                card.Hide();
            }
        }
    }

    private void ApplySavedLevelState(SaveData data)
    {
        if (data == null)
        {
            return;
        }

        levelIndex = Mathf.Max(0, data.levelIndex);
        score = Mathf.Max(0, data.score);
        movesUsed = Mathf.Max(0, data.movesUsed);
        movesLimit = data.movesLimit > 0 ? data.movesLimit : CalculateMovesLimit();
        comboStreak = 0;
        UpdateScoreText();
        UpdateMovesText();
        UpdateLevelText();
        if (comboText != null)
        {
            comboText.gameObject.SetActive(false);
        }
    }
}
