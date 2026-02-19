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
    [SerializeField] private float previewDecreasePerLevel = 0.1f;
    [SerializeField] private float minPreviewTime = 0.5f;
    [SerializeField] private float mismatchDecreasePerLevel = 0.05f;
    [SerializeField] private float minMismatchDelay = 0.2f;

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

        for (int i = 0; i < deck.Count; i++)
        {
            var card = Instantiate(cardPrefab, gridLayout.transform);
            card.Initialize(deck[i].id, deck[i].sprite, cardBackSprite);
            card.Clicked += HandleCardClicked;
            cards.Add(card);
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
        previewTime = Mathf.Max(minPreviewTime, startPreviewTime - (levelIndex * previewDecreasePerLevel));
        mismatchDelay = Mathf.Max(minMismatchDelay, startMismatchDelay - (levelIndex * mismatchDecreasePerLevel));
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
