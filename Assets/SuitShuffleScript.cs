using KModkit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class SuitShuffleScript : MonoBehaviour
{
    static int _moduleIdCounter = 1;
    int _moduleID = 0;
    static List<int> SYModules = new List<int>();
    static int Current = 0;

    public KMBombModule Module;
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMSelectable[] CardPs;
    public SpriteRenderer[] CardRs;
    public SpriteRenderer[] StageCards;
    public Sprite[] SuitSprites;
    public Sprite BackSprite;
    public Material[] Materials;

    private SpriteRenderer[] Highlights;
    private Color RandColour;
    private List<Vector3> InitPositions = new List<Vector3>();
    private readonly List<string> Coordinates = new List<string>() { "A1", "B1", "C1", "D1", "E1", "A2", "B2", "C2", "D2", "E2", "A3", "B3", "C3", "D3", "E3", "A4", "B4", "C4", "D4", "E4" };
    private const string SuitSymbols = "♣♥♠♦■▲";
    private List<int> Suits = new List<int>() { 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 5, 5 };
    private List<int> Flipped = new List<int>();
    private List<int> FlippedCurr = new List<int>();
    private List<int> CompletedSuits = new List<int>();
    private const float AltitudeMultiplier = 0.005f;
    private const float SwapTime = 0.5f;
    private const float SwapPause = 0.25f;
    private const float SwapSepar = 0.2f;
    private bool CannotHighlight = true;
    private bool Activated, Active, Solved;

    void Awake()
    {
        _moduleID = _moduleIdCounter++;
        SYModules = new List<int>();
        Current = 0;
        Highlights = CardPs.Select(x => x.GetComponentsInChildren<SpriteRenderer>()[1]).ToArray();
        InitPositions = CardPs.Select(x => x.transform.localPosition).ToList();
        RandColour = Rnd.ColorHSV(0, 1, 0.75f, 0.75f, 1, 1);
        for (int i = 0; i < 20; i++)
        {
            int x = i;
            CardPs[x].transform.localEulerAngles = new Vector3(90, Rnd.Range(-5f, 5f), 0);
            CardPs[x].OnHighlight += delegate { if (!CannotHighlight && !Flipped.Contains(x)) HandleHighlight(x); };
            CardPs[x].OnHighlightEnded += delegate { HandleHighlight(); };
            CardPs[x].OnInteract += delegate { if (Activated && !FlippedCurr.Contains(x) && !Flipped.Contains(x) && !Solved && !CannotHighlight) CardPress(x); return false; };
            CardRs[x].GetComponent<Renderer>().material.color = RandColour;
            Highlights[x].color = new Color(Highlights[i].color.r, Highlights[i].color.g, Highlights[i].color.b, 0);
            CardPs[x].transform.localPosition = new Vector3(-6.5f, (19 - i) * AltitudeMultiplier, 4);
        }
        for (int i = 0; i < 4; i++)
        {
            StageCards[i].transform.localEulerAngles = new Vector3(90, Rnd.Range(-5f, 5f), 0);
            StageCards[i].GetComponent<Renderer>().material.color = RandColour;
        }
        Module.OnActivate += delegate { StartCoroutine(IntroAnim()); };
        Debug.LogFormat("[Suit Shuffle #{0}] Wating to start stage 1...", _moduleID);
    }

    // Use this for initialization
    void Start()
    {
        SYModules.Add(_moduleID);
        SYModules.Sort();
    }

    void CardPress(int pos)
    {
        if (!Active)
        {
            Active = true;
            Debug.LogFormat("[Suit Shuffle #{0}] Starting stage {1}!", _moduleID, (Flipped.Count / 4) + 1);
            List<int> tempSuits = Suits.Where(x => !CompletedSuits.Contains(x)).ToList();
            List<int> tempSuits2 = new List<int>();
            tempSuits.Shuffle();
            int subtraction = 0;
            for (int i = 0; i < 20; i++)
            {
                if (CompletedSuits.Contains(Suits[i]))
                {
                    subtraction++;
                    tempSuits2.Add(Suits[i]);
                }
                else
                    tempSuits2.Add(tempSuits[i - subtraction]);
            }
            Suits = tempSuits2.ToList();
            Debug.LogFormat("[Suit Shuffle #{0}] Before shuffling, the cards are as follows:\n{1}", _moduleID, Suits.Select(x => SuitSymbols[x]).Select((x, i) => new { Index = i, Value = x }).GroupBy(x => x.Index / 5).Select(x => x.Select(v => v.Value).ToList()).ToList().Select(x => x.Join()).Join("\n"));
            StartCoroutine(HandleActivate());
        }
        else if (!CannotHighlight)
        {
            Audio.PlaySoundAtTransform("flip", CardPs[pos].transform);
            StartCoroutine(FlipCard(CardRs[pos], false, SuitSprites[Suits[pos]]));
            FlippedCurr.Add(pos);
            Debug.Log(FlippedCurr.Count);
            try
            {
                if (FlippedCurr.Count == 1)
                    Debug.LogFormat("[Suit Shuffle #{0}] You flipped card {1}, which is a {2}. All other cards must follow this suit.", _moduleID, Coordinates[pos], SuitSymbols[Suits[pos]]);
                else if (Suits[FlippedCurr.Last()] != Suits[FlippedCurr[FlippedCurr.Count - 2]])
                {
                    Debug.LogFormat("[Suit Shuffle #{0}] You flipped card {1}, which does not follow suit. Strike!", _moduleID, Coordinates[pos]);
                    StartCoroutine(Strike());
                }
                else if (FlippedCurr.Count >= 4)
                {
                    Debug.LogFormat("[Suit Shuffle #{0}] You flipped card {1}, which follows suit. Four cards have been flipped, so this stage is complete!", _moduleID, Coordinates[pos]);
                    StartCoroutine(Stage());
                }
                else
                    Debug.LogFormat("[Suit Shuffle #{0}] You flipped card {1}, which follows suit.", _moduleID, Coordinates[pos]);
            }
            catch { }
        }
    }

    void HandleHighlight(int pos = -1)
    {
        for (int i = 0; i < 20; i++)
            Highlights[i].color = new Color(Highlights[i].color.r, Highlights[i].color.g, Highlights[i].color.b, 0);
        if (pos != -1)
            Highlights[pos].color = new Color(Highlights[pos].color.r, Highlights[pos].color.g, Highlights[pos].color.b, 1);
    }

    private IEnumerator Strike()
    {
        CannotHighlight = true;
        HandleHighlight();
        List<int> order = Enumerable.Range(0, 20).ToList().Where(x => !FlippedCurr.Contains(x)).ToList().Shuffle();
        FlippedCurr = new List<int>();
        float timer = 0;
        while (timer < 0.25f)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        Audio.PlaySoundAtTransform("strike", Module.transform);
        Module.HandleStrike();
        yield return "strike";
        for (int i = 0; i < order.Count; i++)
            if (!Flipped.Contains(order[i]))
            {
                Audio.PlaySoundAtTransform("flip", CardPs[order[i]].transform);
                StartCoroutine(FlipCard(CardRs[order[i]], false, SuitSprites[Suits[order[i]]]));
                timer = 0;
                while (timer < 0.05f)
                {
                    yield return null;
                    timer += Time.deltaTime;
                }
            }
        timer = 0;
        while (timer < 1f)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        order = Enumerable.Range(0, 20).Where(x => !Flipped.Contains(x)).ToList().Shuffle();
        for (int i = 0; i < order.Count; i++)
        {
            Audio.PlaySoundAtTransform("flip", CardPs[order[i]].transform);
            StartCoroutine(FlipCard(CardRs[order[i]], true, BackSprite));
            timer = 0;
            while (timer < 0.05f)
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
        Active = false;
        StartCoroutine(GatherAndDeal());
    }

    private IEnumerator Stage()
    {
        CannotHighlight = true;
        float timer = 0;
        while (timer < 0.25f)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        CompletedSuits.Add(Suits[FlippedCurr[0]]);
        for (int i = 0; i < FlippedCurr.Count; i++)
            Flipped.Add(FlippedCurr[i]);
        StartCoroutine(FlipCard(StageCards[(Flipped.Count / 4) - 1], false, SuitSprites[Suits[FlippedCurr[0]]], isStageCard: true));
        Audio.PlaySoundAtTransform("flip", StageCards[(Flipped.Count / 4) - 1].transform);
        FlippedCurr = new List<int>();
        if (Flipped.Count >= 16)
        {
            Debug.LogFormat("[Suit Shuffle #{0}] Module solved!", _moduleID);
            StartCoroutine(Solve());
            Audio.PlaySoundAtTransform("solve", Module.transform);
            yield return "solve";
        }
        else
        {
            StartCoroutine(GatherAndDeal());
            Audio.PlaySoundAtTransform("stage", Module.transform);
        }
        Active = false;
    }

    private IEnumerator GatherAndDeal()
    {
        HandleHighlight();
        List<int> order = Enumerable.Range(0, 20).Where(x => !Flipped.Contains(x)).ToList().Shuffle();
        for (int i = 0; i < order.Count; i++)
        {
            CardRs[order[i]].sortingOrder = 100 - i;
            StartCoroutine(MoveTo(CardPs[order[i]], new Vector3(-6.5f, CardPs[order[i]].transform.localPosition.y, 4), 0.2f, false, i * AltitudeMultiplier));
            Audio.PlaySoundAtTransform("deal " + Rnd.Range(1, 4), CardPs[order[i]].transform);
            float timer = 0;
            while (timer < 0.075f)
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
        order.Shuffle();
        for (int i = 0; i < order.Count; i++)
        {
            StartCoroutine(MoveTo(CardPs[order[i]], InitPositions[order[i]], 0.2f, i == order.Count - 1));
            Audio.PlaySoundAtTransform("deal " + Rnd.Range(1, 4), CardPs[order[i]].transform);
            float timer = 0;
            while (timer < 0.075f)
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
    }

    private IEnumerator Solve()
    {
        Module.HandlePass();
        CannotHighlight = true;
        Solved = true;
        List<int> order = Enumerable.Range(0, 20).ToList().Shuffle();
        for (int i = 0; i < 20; i++)
        {
            if (!Flipped.Contains(order[i]))
            {
                StartCoroutine(FlipCard(CardRs[order[i]], false, SuitSprites[Suits[order[i]]]));
                Audio.PlaySoundAtTransform("flip", CardPs[order[i]].transform);
                float timer = 0;
                while (timer < 0.05f)
                {
                    yield return null;
                    timer += Time.deltaTime;
                }
            }
        }
        yield return null;
    }

    private IEnumerator HandleActivate()
    {
        bool secondTime = true;
        CannotHighlight = true;
        HandleHighlight();
        List<int> order = Enumerable.Range(0, 20).ToList().Shuffle();
        GoAgain:
        secondTime = !secondTime;
        for (int i = 0; i < 20; i++)
        {
            if (!CompletedSuits.Contains(Suits[order[i]]))
            {
                StartCoroutine(FlipCard(CardRs[order[i]], secondTime, secondTime ? BackSprite : SuitSprites[Suits[order[i]]]));
                Audio.PlaySoundAtTransform("flip", CardPs[order[i]].transform);
                if (i != 19)
                {
                    float timer = 0;
                    while (timer < 0.05f)
                    {
                        yield return null;
                        timer += Time.deltaTime;
                    }
                }
            }
        }
        if (!secondTime)
        {
            float timer2 = 0;
            while (timer2 < 2.5f)
            {
                yield return null;
                timer2 += Time.deltaTime;
            }
            goto GoAgain;
        }
        float time3 = 0;
        while (time3 < 0.25f)
        {
            yield return null;
            time3 += Time.deltaTime;
        }
        Debug.LogFormat("[Suit Shuffle #{0}] Since it is stage {1}, there will be {2} swaps.", _moduleID, (Flipped.Count / 4) + 1, (Flipped.Count / 2) + 3);
        for (int i = 0; i < (Flipped.Count / 2) + 3; i++)
        {
            int random1 = Rnd.Range(0, 20);
            while (Flipped.Contains(random1))
                random1 = Rnd.Range(0, 20);
            int random2 = Rnd.Range(0, 20);
            while (random2 == random1 || Flipped.Contains(random2))
                random2 = Rnd.Range(0, 20);
            Debug.LogFormat("[Suit Shuffle #{0}] Swapping cards {1} and {2}.", _moduleID, Coordinates[random1], Coordinates[random2]);
            int cache = Suits[random1];
            Suits[random1] = Suits[random2];
            Suits[random2] = cache;
            Vector3 position1 = CardPs[random1].transform.localPosition;
            Vector3 position2 = CardPs[random2].transform.localPosition;
            for (int j = 0; j < 2; j++)
            {
                StartCoroutine(SwapCards(j == 0 ? random1 : random2, j == 0 ? position1 : position2, j == 0 ? position2 : position1));
                float timer = 0;
                while (timer < (j * (SwapTime - SwapSepar)) + (i != (Flipped.Count / 2) + 2 ? j * SwapPause : 0) + SwapSepar)
                {
                    yield return null;
                    timer += Time.deltaTime;
                }
            }
            CardPs[random1].transform.localPosition = new Vector3(position1.x, 0, position1.z);
            CardPs[random2].transform.localPosition = new Vector3(position2.x, 0, position2.z);
            Vector3 cache2 = CardPs[random1].transform.localEulerAngles;
            CardPs[random1].transform.localEulerAngles = CardPs[random2].transform.localEulerAngles;
            CardPs[random2].transform.localEulerAngles = cache2;
        }
        Debug.LogFormat("[Suit Shuffle #{0}] After shuffling, the cards are as follows:\n{1}", _moduleID, Suits.Select(x => SuitSymbols[x]).Select((x, i) => new { Index = i, Value = x }).GroupBy(x => x.Index / 5).Select(x => x.Select(v => v.Value).ToList()).ToList().Select(x => x.Join()).Join("\n"));
        CannotHighlight = false;
    }

    private IEnumerator SwapCards(int random, Vector3 position1, Vector3 position2, float duration = 0.5f)
    {
        Audio.PlaySoundAtTransform("pick up", CardPs[random].transform);
        float timer = 0;
        while (timer < SwapTime)
        {
            yield return null;
            timer += Time.deltaTime;
            CardPs[random].transform.localPosition = new Vector3(Easing.InOutSine(timer, position1.x, position2.x, SwapTime), 0, Easing.InOutSine(timer, position1.z, position2.z, SwapTime));
        }
        CardPs[random].transform.localPosition = new Vector3(position2.x, 0, position2.z);
        Audio.PlaySoundAtTransform("place", CardPs[random].transform);
    }

    private IEnumerator FlipCard(SpriteRenderer card, bool type, Sprite sprite, float duration = 0.25f, bool isStageCard = false)
    {
        float timer = 0;
        float start = card.transform.localScale.x;
        while (timer < duration / 2)
        {
            yield return null;
            timer += Time.deltaTime;
            card.transform.localScale = new Vector3(Easing.InSine(timer, start, 0, duration / 2), card.transform.localScale.y, card.transform.localScale.z);
        }
        card.transform.localScale = new Vector3(0, card.transform.localScale.y, card.transform.localScale.z);
        if (!isStageCard)
        {
            CardRs[Array.IndexOf(CardRs, card)].sprite = sprite;
            CardRs[Array.IndexOf(CardRs, card)].material = Materials[type ? 0 : 1];
            CardRs[Array.IndexOf(CardRs, card)].material.color = type ? RandColour : new Color(1, 1, 1, 1);
        }
        else
        {
            StageCards[Array.IndexOf(StageCards, card)].sprite = sprite;
            StageCards[Array.IndexOf(StageCards, card)].material = Materials[type ? 0 : 1];
            StageCards[Array.IndexOf(StageCards, card)].material.color = type ? RandColour : new Color(1, 1, 1, 1);
        }
        timer = 0;
        while (timer < duration / 2)
        {
            yield return null;
            timer += Time.deltaTime;
            card.transform.localScale = new Vector3(Easing.OutSine(timer, 0, start, duration / 2), card.transform.localScale.y, card.transform.localScale.z);
        }
        card.transform.localScale = new Vector3(start, card.transform.localScale.y, card.transform.localScale.z);
    }

    private IEnumerator IntroAnim()
    {
        while (SYModules[Current] != _moduleID)
            yield return null;
        for (int i = 0; i < 20; i++)
        {
            StartCoroutine(MoveTo(CardPs[i], InitPositions[i], 0.2f, i == 19));
            Audio.PlaySoundAtTransform("deal " + Rnd.Range(1, 4), CardPs[i].transform);
            float timer = 0;
            while (timer < 0.075f)
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
    }

    private IEnumerator MoveTo(KMSelectable obj, Vector3 end, float duration, bool begin, float altitude = 0)
    {
        float timer = 0;
        Transform start = obj.transform;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            obj.transform.localPosition = new Vector3(Easing.OutSine(timer, start.localPosition.x, end.x, duration),
                Easing.InExpo(timer, start.localPosition.y, altitude, duration),
                Easing.OutSine(timer, start.localPosition.z, end.z, duration));
        }
        obj.transform.localPosition = new Vector3(end.x, altitude, end.z);
        if (begin)
        {
            CannotHighlight = false;
            Activated = true;
            Current++;
        }
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = "Use '!{0} a1 b2 c3' to press the cell at B4.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant();
        var commandArray = command.Split(' ');
        string[] coords = new[] { "a1", "b1", "c1", "d1", "e1", "a2", "b2", "c2", "d2", "e2", "a3", "b3", "c3", "d3", "e3", "a4", "b4", "c4", "d4", "e4" };
        for (int i = 0; i < commandArray.Length; i++)
            if (!coords.Contains(commandArray[i]))
            {
                yield return "sendtochaterror Invalid command.";
                yield break;
            }
        yield return null;
        for (int i = 0; i < commandArray.Length; i++)
        {
            if (FlippedCurr.Contains(Array.IndexOf(coords, commandArray[i])) || Flipped.Contains(Array.IndexOf(coords, commandArray[i])))
            {
                yield return "sendtochaterror Card " + commandArray[i] + " has already been flipped!";
                yield break;
            }
            yield return new WaitUntil(() => Activated && !CannotHighlight);
            CardPs[Array.IndexOf(coords, commandArray[i])].OnInteract();
            float timer = 0;
            while (timer < 0.1f)
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        while (!Solved)
        {
            if (!Active && !CannotHighlight)
            {
                CardPs[Enumerable.Range(0, CardPs.Length).Where(x => !Flipped.Contains(x)).First()].OnInteract();
                yield return true;
                while (CannotHighlight)
                    yield return true;
            }
            else
            {
                if (FlippedCurr.Count() == 0)
                    CardPs[Enumerable.Range(0, CardPs.Length).Where(x => Suits[x] < 4 && !CompletedSuits.Contains(Suits[x])).First()].OnInteract();
                else if (FlippedCurr.Count() < 4)
                    CardPs[Enumerable.Range(0, CardPs.Length).Where(x => Suits[x] == Suits[FlippedCurr.Last()] && !FlippedCurr.Contains(x)).First()].OnInteract();
                yield return true;
            }
        }
    }
}
