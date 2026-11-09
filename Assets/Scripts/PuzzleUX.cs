// SPDX-License-Identifier: MPL-2.0
//
// PuzzleUX — "find the best move" puzzles (Learn by playing → Puzzles).
//
// Runs in the Game scene on the real board: the puzzle position is loaded with game.LoadPosition,
// the player makes one move with the normal controls (moves or rotations), and the move is checked
// against the engine-verified answers. Puzzles come from Resources/ManuPuzzles.txt (generated and
// quality-checked offline); once all of those are solved, new ones are generated on the device.

using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using ManuAI;

public class PuzzleUX : MonoBehaviour
{
    public static bool Requested;            // set by the menu before loading the Game scene
    public static bool Active => current != null;
    static PuzzleUX current;

    // Persistent puzzle statistics (also shown on the statistics page).
    public const string KeySolved = "Puzzle.SolvedFirstTry", KeyAttempted = "Puzzle.Attempted",
                        KeyStreak = "Puzzle.Streak", KeyBest = "Puzzle.BestStreak", KeyDone = "Puzzle.DoneIds";

    enum State { Loading, Solving, Correct, Wrong, Revealed }
    State state = State.Loading;

    game board; GameUX ux;
    Puzzle puzzle; Board position;
    readonly List<Puzzle> bank = new List<Puzzle>();
    HashSet<string> done = new HashSet<string>();
    bool failed, counted;
    CancellationTokenSource cts;
    Task<Puzzle> generating;

    Text title, prompt, status;
    GameObject puzzleCanvas;
    Button hintButton, solutionButton, nextButton;
    Material lineMaterial; LineRenderer hintCircle, arrow;

    public static void Install(GameUX gameUx)
    {
        Requested = false;
        var go = new GameObject("Puzzles");
        var p = go.AddComponent<PuzzleUX>();
        p.ux = gameUx;
    }

    IEnumerator Start()
    {
        current = this;
        board = FindAnyObjectByType<game>();
        cts = new CancellationTokenSource();
        LoadBank();
        BuildUI();
        yield return null; // let game.Start finish placing the starting pieces
        NextPuzzle();
    }

    void OnDestroy()
    {
        if (cts != null) cts.Cancel();
        if (current == this) current = null;
        if (lineMaterial != null) Destroy(lineMaterial);
    }

    // ---------------------------------------------------------------- puzzle supply

    void LoadBank()
    {
        string doneCsv = PlayerPrefs.GetString(KeyDone, "");
        if (doneCsv.Length > 0) done = new HashSet<string>(doneCsv.Split(','));
        TextAsset ta = Resources.Load<TextAsset>("ManuPuzzles");
        if (ta == null) return;
        foreach (string line in ta.text.Split('\n'))
        {
            Puzzle p = Puzzle.Parse(line);
            if (p != null) bank.Add(p);
        }
    }

    void NextPuzzle()
    {
        ClearMarks();
        var fresh = bank.FindAll(p => !done.Contains(p.Id));
        if (fresh.Count > 0) { Load(fresh[Random.Range(0, fresh.Count)]); return; }
        StartCoroutine(GenerateAndLoad());
    }

    IEnumerator GenerateAndLoad()
    {
        state = State.Loading;
        SetStatus("All built-in puzzles solved. Generating a new puzzle…");
        prompt.text = "";
        SetButtons(false, false, false);
        Puzzle p = null;
#if UNITY_WEBGL && !UNITY_EDITOR
        // The browser build is single-threaded, so new puzzles are not generated; replay the built-in ones.
        yield return null;
#else
        if (generating == null)
        {
            double[] w = AIAgent.LoadWeights(); int seed = Random.Range(1, int.MaxValue); CancellationToken token = cts.Token;
            generating = Task.Run(() => new PuzzleGenerator(w, seed) { ScoreDepth = 2, ScoreMsPerMove = 250 }.Generate(30, token));
        }
        while (!generating.IsCompleted) yield return null;
        p = generating.IsFaulted ? null : generating.Result;
        generating = null;
#endif
        if (p != null) { bank.Add(p); Load(p); }
        else if (bank.Count > 0) Load(bank[Random.Range(0, bank.Count)]); // replay an old one
        else SetStatus("Could not generate a puzzle. Try again.");
    }

    void Load(Puzzle p)
    {
        Board b = p.Position();
        if (b == null) { done.Add(p.Id); NextPuzzle(); return; } // stale/invalid entry
        puzzle = p; position = b;
        failed = false; counted = false;
        Reset();
        string side = Pc.ColorName(b.Side) == "orange" ? "Orange" : "Green";
        title.text = side + " to move · Puzzle streak " + PlayerPrefs.GetInt(KeyStreak, 0);
        prompt.text = Puzzle.Prompt(p.Theme);
        SetStatus("Make your move. Scholars and Sages can also rotate.");
    }

    // Puts the puzzle position back on the board (also after a wrong try).
    void Reset()
    {
        board.LoadPosition(position);
        if (board.VictoryPanel != null) board.VictoryPanel.SetActive(false);
        if (ux != null)
        {
            ux.Reviewing = false;
            ux.CloseModal();
            ShowOpponentLastMove();
        }
        state = State.Solving;
        SetButtons(true, true, false);
    }

    void ShowOpponentLastMove()
    {
        if (puzzle.Moves.Count == 0) return;
        var before = new Puzzle { Moves = puzzle.Moves.GetRange(0, puzzle.Moves.Count - 1) }.Position();
        if (before == null) return;
        var buf = new List<Move>(); Move m;
        if (!MoveCodec.TryFind(before, puzzle.Moves[puzzle.Moves.Count - 1], buf, out m)) return;
        if (m.IsRotation) ux.Rotated(m.Pair, m.Clockwise);
        else ux.Record(new game.MoveInfo
        {
            player = Pc.ColorName(before.Side), pieceType = Pc.Letter(Pc.Type(before.Sq[m.From])),
            fromRing = Geo.RingOf[m.From], fromPlace = Geo.PlaceOf[m.From], toRing = Geo.RingOf[m.To], toPlace = Geo.PlaceOf[m.To]
        });
    }

    // ---------------------------------------------------------------- checking the answer

    void Update()
    {
        // Get out of the way of the game's own dialogs (rotation preview, menu), which use the header.
        if (puzzleCanvas != null && ux != null && puzzleCanvas.activeSelf == ux.ModalOpen) puzzleCanvas.SetActive(!ux.ModalOpen);
        if (state != State.Solving || board == null || board.MoveCodes.Count == 0) return;
        string played = board.MoveCodes[0];
        if (ux != null) ux.Reviewing = true; // freeze the board until the next step
        if (board.VictoryPanel != null) board.VictoryPanel.SetActive(false); // a winning answer ends the game

        if (puzzle.IsSolution(played))
        {
            state = State.Correct;
            Count(true);
            done.Add(puzzle.Id); SaveDone();
            SetStatus(failed ? "Correct, well found!" : "Correct! ✓");
            title.text = (Pc.ColorName(position.Side) == "orange" ? "Orange" : "Green") + " · Puzzle streak " + PlayerPrefs.GetInt(KeyStreak, 0);
            SetButtons(false, false, true);
        }
        else
        {
            state = State.Wrong;
            Count(false);
            SetStatus("Not the best move. Try again!");
            StartCoroutine(RetryAfterDelay());
        }
    }

    IEnumerator RetryAfterDelay()
    {
        yield return new WaitForSeconds(1.4f);
        if (state == State.Wrong) Reset();
    }

    // Records the first attempt of each puzzle in the statistics.
    void Count(bool correct)
    {
        if (!correct) failed = true;
        if (counted) return;
        counted = true;
        PlayerPrefs.SetInt(KeyAttempted, PlayerPrefs.GetInt(KeyAttempted, 0) + 1);
        if (correct && !failed)
        {
            PlayerPrefs.SetInt(KeySolved, PlayerPrefs.GetInt(KeySolved, 0) + 1);
            int streak = PlayerPrefs.GetInt(KeyStreak, 0) + 1;
            PlayerPrefs.SetInt(KeyStreak, streak);
            if (streak > PlayerPrefs.GetInt(KeyBest, 0)) PlayerPrefs.SetInt(KeyBest, streak);
        }
        else PlayerPrefs.SetInt(KeyStreak, 0);
        PlayerPrefs.Save();
    }

    void SaveDone()
    {
        PlayerPrefs.SetString(KeyDone, string.Join(",", new List<string>(done).ToArray()));
        PlayerPrefs.Save();
    }

    // ---------------------------------------------------------------- hint / solution

    void Hint()
    {
        if (state != State.Solving) return;
        Count(false); // a hint means it wasn't solved unaided
        Move m = SolutionMove();
        if (m.IsNone) return;
        if (m.IsRotation) SetStatus("Hint: the answer is a rotation of the " + new[] { "A/a", "B/b", "C/c", "D" }[m.Pair - 1] + " rings.");
        else
        {
            Circle(hintCircle, Chessman.PositionAt(Geo.RingOf[m.From], Geo.PlaceOf[m.From]), .1f);
            SetStatus("Hint: move the highlighted " + GameUX.PieceName(Pc.Letter(Pc.Type(position.Sq[m.From]))) + ".");
        }
    }

    void ShowSolution()
    {
        if (state != State.Solving && state != State.Wrong) return;
        Count(false);
        state = State.Revealed;
        if (ux != null) ux.Reviewing = true;
        Move m = SolutionMove();
        string text = "Solution: " + Describe(m);
        if (puzzle.Solutions.Count > 1) text += " (another move was also accepted)";
        SetStatus(text);
        if (!m.IsNone && !m.IsRotation)
            Arrow(Chessman.PositionAt(Geo.RingOf[m.From], Geo.PlaceOf[m.From]), Chessman.PositionAt(Geo.RingOf[m.To], Geo.PlaceOf[m.To]));
        done.Add(puzzle.Id); SaveDone();
        SetButtons(false, false, true);
    }

    Move SolutionMove()
    {
        var buf = new List<Move>(); Move m;
        return MoveCodec.TryFind(position, puzzle.Solutions[0], buf, out m) ? m : Move.None;
    }

    string Describe(Move m)
    {
        if (m.IsNone) return "?";
        if (m.IsRotation) return "rotate " + new[] { "A/a", "B/b", "C/c", "D" }[m.Pair - 1] + (m.Clockwise ? " clockwise" : " counterclockwise");
        return GameUX.PieceName(Pc.Letter(Pc.Type(position.Sq[m.From]))) + " " +
               Chessman.RING_NAMES[Geo.RingOf[m.From] - 1] + Geo.PlaceOf[m.From] + (m.IsCapture ? " × " : " → ") +
               Chessman.RING_NAMES[Geo.RingOf[m.To] - 1] + Geo.PlaceOf[m.To];
    }

    // ---------------------------------------------------------------- UI

    void BuildUI()
    {
        RectTransform safe = UXUI.Canvas("Puzzle interface", 140);
        safe.parent.SetParent(transform, false);
        puzzleCanvas = safe.parent.gameObject;
        var top = UXUI.Panel(safe, "Puzzle header", UXUI.Ink);
        UXUI.Place(top, 0, 245, 0);
        title = UXUI.Label(top, "Puzzles", 6, 40, 27);
        prompt = UXUI.Label(top, "", 46, 40, 25);
        prompt.color = new Color(.95f, .78f, .45f);
        status = UXUI.Label(top, "Loading…", 88, 62, 21);
        hintButton = Quarter(UXUI.Button(top, "Hint", 160, Hint), 0);
        solutionButton = Quarter(UXUI.Button(top, "Solution", 160, ShowSolution), 1);
        nextButton = Quarter(UXUI.Button(top, "Next ›", 160, NextPuzzle, true), 2);
        Quarter(UXUI.Button(top, "Exit", 160, () => { if (ux != null) ux.Leave(); }), 3);

        lineMaterial = new Material(Shader.Find("Sprites/Default"));
        hintCircle = MakeLine("Puzzle hint", new Color(.35f, .65f, 1f, .95f), .018f, 96, true);
        arrow = MakeLine("Puzzle solution", new Color(.35f, .65f, 1f, .9f), .035f, 5, false);
    }

    static Button Quarter(Button b, int i)
    {
        var r = (RectTransform)b.transform;
        r.anchorMin = new Vector2(i * .25f, 1); r.anchorMax = new Vector2((i + 1) * .25f, 1);
        r.offsetMin = new Vector2(8, r.offsetMin.y); r.offsetMax = new Vector2(-8, r.offsetMax.y);
        return b;
    }

    void SetButtons(bool hint, bool solution, bool next)
    {
        hintButton.interactable = hint; solutionButton.interactable = solution; nextButton.interactable = next;
    }

    void SetStatus(string s) { status.text = s; }

    LineRenderer MakeLine(string name, Color color, float width, int points, bool loop)
    {
        var go = new GameObject(name); go.transform.SetParent(transform);
        var l = go.AddComponent<LineRenderer>();
        l.sharedMaterial = lineMaterial; l.startColor = l.endColor = color; l.startWidth = l.endWidth = width;
        l.positionCount = points; l.loop = loop; l.useWorldSpace = true; l.sortingOrder = 30;
        go.SetActive(false);
        return l;
    }

    void ClearMarks()
    {
        if (hintCircle != null) hintCircle.gameObject.SetActive(false);
        if (arrow != null) arrow.gameObject.SetActive(false);
    }

    void Circle(LineRenderer l, Vector3 c, float r)
    {
        l.gameObject.SetActive(true);
        for (int i = 0; i < l.positionCount; i++)
        {
            float a = i * Mathf.PI * 2 / l.positionCount;
            l.SetPosition(i, new Vector3(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r, -4));
        }
    }

    void Arrow(Vector3 a, Vector3 b)
    {
        arrow.gameObject.SetActive(true);
        a.z = b.z = -4.5f;
        Vector3 d = (b - a).normalized; Vector3 n = new Vector3(-d.y, d.x, 0);
        Vector3 back = b - d * .09f;
        arrow.SetPosition(0, a); arrow.SetPosition(1, back); arrow.SetPosition(2, back + n * .06f);
        arrow.SetPosition(3, b); arrow.SetPosition(4, back - n * .06f);
    }
}
