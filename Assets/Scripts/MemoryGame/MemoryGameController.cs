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

    private readonly List<CardView> cards = new List<CardView>();
    private CardView firstSelection;
    private CardView secondSelection;
    private bool isBusy;
    private int matchedCount;

    private void Start()
    {
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

        if (cardFaces.Count < totalCards / 2)
        {
            Debug.LogError("Not enough card faces to fill the grid.");
            return;
        }

        var availableFaces = new List<Sprite>(cardFaces);
        Shuffle(availableFaces);
        availableFaces = availableFaces.GetRange(0, totalCards / 2);

        var deck = new List<(int id, Sprite sprite)>();
        for (int i = 0; i < availableFaces.Count; i++)
        {
            deck.Add((i, availableFaces[i]));
            deck.Add((i, availableFaces[i]));
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
            Debug.Log("Level complete.");
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
