using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MemoryGameController : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private GridLayoutGroup gridLayout;
    [SerializeField] private CardView cardPrefab;

    [Header("Cards")]
    [SerializeField] private Sprite cardBackSprite;
    [SerializeField] private List<Sprite> cardFaces = new List<Sprite>();

    [Header("Level Settings")]
    [SerializeField] private int rows = 2;
    [SerializeField] private int columns = 2;
    [SerializeField] private float previewTime = 2f;
    [SerializeField] private float mismatchDelay = 0.6f;

    [Header("Progression")]
    [SerializeField] private float levelCompleteDelay = 1.5f;
    [SerializeField] private int maxRows = 4;
    [SerializeField] private int maxColumns = 4;
    [SerializeField] private int sizeIncreaseEveryLevels = 1;
    [SerializeField] private int rowIncreaseStep = 1;
    [SerializeField] private int columnIncreaseStep = 1;
    [SerializeField] private float previewIncreasePerPair = 0.05f;
    [SerializeField] private float maxPreviewTime = 4f;
    [SerializeField] private float mismatchDecreasePerLevel = 0.05f;
    [SerializeField] private float minMismatchDelay = 0.2f;

    [Header("Layout")]
    [SerializeField] private bool randomizePositions = false;
    [SerializeField] private Vector2 randomPadding = new Vector2(20f, 20f);
    [SerializeField] private Vector2 randomSpacing = new Vector2(10f, 10f);

    private readonly List<CardView> cards = new List<CardView>();
    private CardView firstSelection;
    private CardView secondSelection;
    private bool isBusy;
    private int matchedCount;
    private int levelIndex;
    private int startRows;
    private int startColumns;
    private float startPreviewTime;
    private float startMismatchDelay;

    private void Start()
    {
        EnsureEvenGrid();
        startRows = rows;
        startColumns = columns;
        startPreviewTime = previewTime;
        startMismatchDelay = mismatchDelay;
        StartCoroutine(StartLevel());
    }

    private IEnumerator StartLevel()
    {
        ClearGrid();
        GenerateCards();

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

        int pairsNeeded = totalCards / 2;
        var deck = new List<(int id, Sprite sprite)>(totalCards);
        for (int i = 0; i < pairsNeeded; i++)
        {
            int faceIndex = i % cardFaces.Count;
            deck.Add((i, cardFaces[faceIndex]));
            deck.Add((i, cardFaces[faceIndex]));
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
            StartCoroutine(ResolveSelection());
        }
    }

    private IEnumerator ResolveSelection()
    {
        isBusy = true;

        if (firstSelection.CardId == secondSelection.CardId)
        {
            firstSelection.Match();
            secondSelection.Match();
            matchedCount += 2;
        }
        else
        {
            yield return new WaitForSeconds(mismatchDelay);
            firstSelection.Hide();
            secondSelection.Hide();
        }

        firstSelection = null;
        secondSelection = null;
        isBusy = false;

        if (matchedCount >= cards.Count)
        {
            StartCoroutine(AdvanceLevel());
        }
    }

    private IEnumerator AdvanceLevel()
    {
        isBusy = true;
        yield return new WaitForSeconds(levelCompleteDelay);
        levelIndex++;
        ApplyDifficulty();
        isBusy = false;
        StartCoroutine(StartLevel());
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
}
