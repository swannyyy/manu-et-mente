// SPDX-License-Identifier: MPL-2.0
//
// AnalysisUX — lichess-style review of a finished game.
//
// Opens over the game board (after any game, or from the statistics page). The real pieces are
// hidden and each position of the game is redrawn from ManuEngine, so you can step through the
// game freely. A worker thread searches every position; as results arrive the screen shows:
//   * an advantage graph (tap/drag it to jump to a move),
//   * each move's classification (best / inaccuracy ?! / mistake ? / blunder ??) and win% lost,
//   * the engine's best move as an arrow on the board,
//   * accuracy and error counts for both players.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ManuAI;

public class AnalysisUX : MonoBehaviour
{
    // Set by the statistics page before loading the Game scene; consumed by GameUX.
    public static GameEntry Pending;
    public static bool IsOpen => current != null;
    static AnalysisUX current;

    const int AnalysisDepth = 4;
    const long AnalysisMsPerPosition = 350;

    GameEntry entry;
    bool fromMenu;
    game board;
    GameUX ux;
    GameAnalysis ga;
    CancellationTokenSource cts;
    int cursor;
    bool showBest = true;
    bool savedSummary;

    readonly List<GameObject> hiddenPieces = new List<GameObject>();
    readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
    Chessman spriteSource;
    SpriteRenderer sourceRenderer;
    Vector3 pieceScale = Vector3.one;

    Transform[] rings = new Transform[4];
    Quaternion[] ringStart = new Quaternion[4];   // ring sprite rotation at the start of the game
    Quaternion[] ringLive = new Quaternion[4];    // rotation to restore when closing
    int[][] netTurns;                             // [position][pair] net quarter turns (ACW positive)

    RectTransform canvasRoot, top, bottom;
    Text title, progress, moveText, summary, bestToggleLabel;
    RawImage graph; Texture2D tex; Color32[] pixels;
    const int GW = 480, GH = 120;
    int drawnCompleted = -1, drawnCursor = -1;
    float nextRedraw;

    Material lineMaterial;
    LineRenderer fromLine, toLine, ringLine, arrow;

    // ---------------------------------------------------------------- opening / closing

    public static AnalysisUX Open(GameEntry e, bool fromMenu)
    {
        if (e == null || current != null) return current;
        var go = new GameObject("Game analysis");
        var a = go.AddComponent<AnalysisUX>();
        a.Init(e, fromMenu);
        return a;
    }

    void Init(GameEntry e, bool menu)
    {
        current = this;
        entry = e; fromMenu = menu;
        ux = GameUX.Instance;
        board = FindAnyObjectByType<game>();
        if (ux != null) { ux.Reviewing = true; ux.CloseModal(); ux.Deselect(); }

        ga = GameAnalysis.Build(e.moves);
        PrepareBoard();
        BuildUI();

        cts = new CancellationTokenSource();
        double[] weights = AIAgent.LoadWeights();
        GameAnalysis local = ga; CancellationToken token = cts.Token;
        Task.Run(() => local.Run(weights, AnalysisDepth, AnalysisMsPerPosition, token));

        cursor = fromMenu ? 0 : ga.PlyCount;
        Show(cursor);
    }

    public void Close()
    {
        if (cts != null) cts.Cancel();
        SaveSummaryIfFinished();
        foreach (var r in pool) if (r != null) Destroy(r.gameObject);
        pool.Clear();
        foreach (var go in hiddenPieces) if (go != null) go.SetActive(true);
        for (int p = 0; p < 4; p++) if (rings[p] != null) rings[p].rotation = ringLive[p];
        if (canvasRoot != null) Destroy(canvasRoot.root.gameObject);
        foreach (var l in new[] { fromLine, toLine, ringLine, arrow }) if (l != null) Destroy(l.gameObject);
        if (lineMaterial != null) Destroy(lineMaterial);
        if (tex != null) Destroy(tex);
        current = null;
        if (ux != null) ux.Reviewing = false;

        if (fromMenu)
        {
            MenuUX.OpenStatsOnLoad = true;
            SceneManager.LoadScene("MainMenu");
        }
        else if (ux != null) ux.ShowResultScreen();
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (cts != null) cts.Cancel();
        if (current == this) current = null;
    }

    void SaveSummaryIfFinished()
    {
        if (savedSummary || ga == null || !ga.Finished || ga.PlyCount == 0) return;
        savedSummary = true;
        double ao = ga.Accuracy(Pc.Orange), ag = ga.Accuracy(Pc.Green);
        entry.analysed = true;
        entry.accOrange = double.IsNaN(ao) ? -1 : (float)ao;
        entry.accGreen = double.IsNaN(ag) ? -1 : (float)ag;
        entry.errOrange = ga.Errors(Pc.Orange);
        entry.errGreen = ga.Errors(Pc.Green);
        GameHistory.Save();
    }

    // ---------------------------------------------------------------- board drawing

    void PrepareBoard()
    {
        foreach (var plate in FindObjectsByType<MovePlate>(FindObjectsInactive.Exclude)) Destroy(plate.gameObject);
        foreach (var cm in FindObjectsByType<Chessman>(FindObjectsInactive.Exclude))
        {
            if (spriteSource == null) spriteSource = cm;
            hiddenPieces.Add(cm.gameObject);
            cm.gameObject.SetActive(false);
        }
        if (spriteSource != null)
        {
            sourceRenderer = spriteSource.GetComponent<SpriteRenderer>();
            pieceScale = spriteSource.transform.localScale;
        }

        // Ring sprites: work out their rotation at the start of the game from the live rotation.
        if (board != null)
        {
            rings[0] = board.ring1 != null ? board.ring1.transform : null;
            rings[1] = board.ring2 != null ? board.ring2.transform : null;
            rings[2] = board.ring3 != null ? board.ring3.transform : null;
            rings[3] = board.ring4 != null ? board.ring4.transform : null;
        }
        netTurns = new int[ga.Positions.Count][];
        var net = new int[4];
        for (int i = 0; i < ga.Positions.Count; i++)
        {
            netTurns[i] = (int[])net.Clone();
            if (i < ga.Moves.Count && ga.Moves[i].IsRotation) net[ga.Moves[i].Pair - 1] += ga.Moves[i].Clockwise ? -1 : 1;
        }
        // In a live game the rings currently show the final position; from the menu the scene is
        // freshly loaded, so they show the start.
        int[] liveTurns = fromMenu ? new int[4] : netTurns[netTurns.Length - 1];
        for (int p = 0; p < 4; p++)
        {
            if (rings[p] == null) continue;
            ringLive[p] = rings[p].rotation;
            ringStart[p] = rings[p].rotation * Quaternion.Euler(0, 0, -90f * liveTurns[p]);
        }

        lineMaterial = new Material(Shader.Find("Sprites/Default"));
        fromLine = MakeLine("Analysis from", new Color(.85f, .75f, .45f, .8f), .01f, 96, true);
        toLine = MakeLine("Analysis to", new Color(.3f, .9f, .7f), .014f, 96, true);
        ringLine = MakeLine("Analysis ring", new Color(.9f, .75f, .4f, .7f), .02f, 96, true);
        arrow = MakeLine("Analysis best move", new Color(.35f, .65f, 1f, .9f), .035f, 5, false);
    }

    LineRenderer MakeLine(string name, Color color, float width, int points, bool loop)
    {
        var go = new GameObject(name); go.transform.SetParent(transform);
        var l = go.AddComponent<LineRenderer>();
        l.sharedMaterial = lineMaterial; l.startColor = l.endColor = color; l.startWidth = l.endWidth = width;
        l.positionCount = points; l.loop = loop; l.useWorldSpace = true; l.sortingOrder = 30;
        go.SetActive(false);
        return l;
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
        Vector3 tip = b, back = b - d * .09f;
        arrow.SetPosition(0, a);
        arrow.SetPosition(1, back);
        arrow.SetPosition(2, back + n * .06f);
        arrow.SetPosition(3, tip);
        arrow.SetPosition(4, back - n * .06f);
    }

    Sprite SpriteFor(int type, int color)
    {
        if (spriteSource == null) return null;
        bool o = color == Pc.Orange; var s = spriteSource;
        switch (type)
        {
            case Pc.B: return o ? s.mind_o : s.mind_g;
            case Pc.H: return o ? s.hand_o : s.hand_g;
            case Pc.T: return o ? s.tech_o : s.tech_g;
            case Pc.W: return o ? s.wise_o : s.wise_g;
            case Pc.S: return o ? s.schol_o : s.schol_g;
            case Pc.A: return o ? s.app_o : s.app_g;
            default: return o ? s.skill_o : s.skill_g;
        }
    }

    SpriteRenderer PooledRenderer(int i)
    {
        while (pool.Count <= i)
        {
            var go = new GameObject("Analysis piece");
            go.transform.SetParent(transform);
            go.transform.localScale = pieceScale;
            var r = go.AddComponent<SpriteRenderer>();
            if (sourceRenderer != null)
            {
                r.sharedMaterial = sourceRenderer.sharedMaterial;
                r.sortingLayerID = sourceRenderer.sortingLayerID;
                r.sortingOrder = sourceRenderer.sortingOrder;
                r.color = sourceRenderer.color;
            }
            pool.Add(r);
        }
        return pool[i];
    }

    void Show(int position)
    {
        cursor = Mathf.Clamp(position, 0, ga.Positions.Count - 1);
        Board b = ga.Positions[cursor];

        int n = 0;
        for (int c = 0; c < Geo.NumCells; c++)
        {
            int code = b.Sq[c];
            if (code == 0) continue;
            var r = PooledRenderer(n++);
            r.gameObject.SetActive(true);
            r.sprite = SpriteFor(Pc.Type(code), Pc.Color(code));
            r.transform.position = Chessman.PositionAt(Geo.RingOf[c], Geo.PlaceOf[c]);
        }
        for (int i = n; i < pool.Count; i++) pool[i].gameObject.SetActive(false);

        for (int p = 0; p < 4; p++)
            if (rings[p] != null) rings[p].rotation = ringStart[p] * Quaternion.Euler(0, 0, 90f * netTurns[cursor][p]);

        // Last move played.
        fromLine.gameObject.SetActive(false); toLine.gameObject.SetActive(false); ringLine.gameObject.SetActive(false);
        if (cursor > 0)
        {
            Move m = ga.Moves[cursor - 1];
            if (m.IsRotation) Circle(ringLine, Vector3.zero, new[] { 2f, 1.5f, 1f, .5f }[m.Pair - 1]);
            else
            {
                Circle(fromLine, Chessman.PositionAt(Geo.RingOf[m.From], Geo.PlaceOf[m.From]), .075f);
                Circle(toLine, Chessman.PositionAt(Geo.RingOf[m.To], Geo.PlaceOf[m.To]), .085f);
            }
        }
        UpdateArrow();
        UpdateTexts();
        drawnCursor = -1; // force graph redraw
    }

    void UpdateArrow()
    {
        arrow.gameObject.SetActive(false);
        if (!showBest || !ga.IsDone(cursor)) return;
        Move best = ga.Evals[cursor].Best;
        if (best.IsNone || best.IsRotation) return;
        Arrow(Chessman.PositionAt(Geo.RingOf[best.From], Geo.PlaceOf[best.From]),
              Chessman.PositionAt(Geo.RingOf[best.To], Geo.PlaceOf[best.To]));
    }

    // ---------------------------------------------------------------- text

    static string Square(int cell) { return Chessman.RING_NAMES[Geo.RingOf[cell] - 1] + Geo.PlaceOf[cell]; }

    static string Describe(Board before, Move m)
    {
        if (m.IsNone) return "-";
        if (m.IsRotation)
            return "Rotate " + new[] { "A/a", "B/b", "C/c", "D" }[m.Pair - 1] + (m.Clockwise ? " ↷" : " ↶");
        string piece = GameUX.PieceName(Pc.Letter(Pc.Type(before.Sq[m.From])));
        return piece + " " + Square(m.From) + (m.IsCapture ? " × " : " → ") + Square(m.To);
    }

    string WinText(int position)
    {
        double w = ga.OrangeWin(position);
        if (double.IsNaN(w)) return "evaluating…";
        if (w >= .995) return "Orange winning";
        if (w <= .005) return "Green winning";
        return "Orange " + Mathf.RoundToInt((float)(w * 100)) + "% · Green " + Mathf.RoundToInt((float)((1 - w) * 100)) + "%";
    }

    void UpdateTexts()
    {
        string acc(int c) { double a = ga.Accuracy(c); return double.IsNaN(a) ? "–" : Mathf.RoundToInt((float)a) + "%"; }
        title.text = "Analysis · Orange " + acc(Pc.Orange) + " · Green " + acc(Pc.Green);
        progress.text = ga.Finished ? WinText(cursor)
            : "Analysing " + Mathf.RoundToInt(100f * ga.Completed / Mathf.Max(1, ga.Positions.Count)) + "% · " + WinText(cursor);
        if (!string.IsNullOrEmpty(ga.Error)) progress.text += "\n" + ga.Error;

        string line;
        if (cursor == 0) line = "Start of the game · Orange to move";
        else
        {
            int i = cursor - 1;
            Board before = ga.Positions[i];
            string who = before.Side == Pc.Orange ? "Orange" : "Green";
            MoveClass mc = ga.Classify(i);
            line = (i + 1) + ". " + who + ": " + Describe(before, ga.Moves[i]) + "  " + GameAnalysis.Symbol(mc);
            if (mc == MoveClass.Unknown) line += "\nanalysing…";
            else if (mc == MoveClass.Best) line += "\nBest move";
            else
            {
                double loss = ga.Loss(i);
                line += "\n" + GameAnalysis.Name(mc);
                if (loss >= 1) line += " (−" + Mathf.RoundToInt((float)loss) + "% win chance)";
                Move best = ga.Evals[i].Best;
                if (!best.IsNone && mc != MoveClass.Good) line += " · Best was " + Describe(before, best);
            }
        }
        if (cursor == ga.PlyCount && ga.Positions[cursor].Winner >= 0) line += "\nGame over";
        else if (ga.IsDone(cursor) && !ga.Evals[cursor].Best.IsNone && showBest)
            line += "\nEngine suggests: " + Describe(ga.Positions[cursor], ga.Evals[cursor].Best);
        moveText.text = line;

        int[] eo = ga.Errors(Pc.Orange), eg = ga.Errors(Pc.Green);
        summary.text = "Orange " + eo[0] + " ?! · " + eo[1] + " ? · " + eo[2] + " ??     Green " + eg[0] + " ?! · " + eg[1] + " ? · " + eg[2] + " ??";
    }

    // ---------------------------------------------------------------- UI

    void BuildUI()
    {
        RectTransform safe = UXUI.Canvas("Analysis interface", 150);
        canvasRoot = safe;
        top = UXUI.Panel(safe, "Analysis header", UXUI.Ink);
        UXUI.Place(top, 0, 245, 0);
        title = UXUI.Label(top, "Analysis", 4, 36, 25);
        progress = UXUI.Label(top, "", 38, 26, 19);
        var g = new GameObject("Advantage graph", typeof(RectTransform), typeof(RawImage));
        g.transform.SetParent(top, false);
        graph = g.GetComponent<RawImage>();
        UXUI.Place(graph.rectTransform, 66, 92, 24);
        tex = new Texture2D(GW, GH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        pixels = new Color32[GW * GH];
        graph.texture = tex;
        var input = g.AddComponent<AnalysisGraphInput>();
        input.OnPick = f => Show(Mathf.RoundToInt(f * (ga.Positions.Count - 1)));
        moveText = UXUI.Label(top, "", 160, 84, 20);

        bottom = UXUI.Panel(safe, "Analysis controls", UXUI.Surface);
        bottom.anchorMin = Vector2.zero; bottom.anchorMax = new Vector2(1, 0); bottom.pivot = new Vector2(.5f, 0);
        bottom.sizeDelta = new Vector2(0, 290); bottom.anchoredPosition = Vector2.zero;
        Quarter(UXUI.Button(bottom, "« Start", 10, () => Show(0)), 0);
        Quarter(UXUI.Button(bottom, "‹ Back", 10, () => Show(cursor - 1)), 1);
        Quarter(UXUI.Button(bottom, "Next ›", 10, () => Show(cursor + 1)), 2);
        Quarter(UXUI.Button(bottom, "End »", 10, () => Show(ga.Positions.Count - 1)), 3);
        Half(UXUI.Button(bottom, "‹ Previous error", 84, () => JumpError(-1)), false);
        Half(UXUI.Button(bottom, "Next error ›", 84, () => JumpError(1)), true);
        var toggle = UXUI.Button(bottom, "Best move: On", 158, ToggleBest);
        bestToggleLabel = toggle.GetComponentInChildren<Text>();
        Half(toggle, false);
        Half(UXUI.Button(bottom, fromMenu ? "Back to statistics" : "Close analysis", 158, Close, true), true);
        summary = UXUI.Label(bottom, "", 230, 50, 20);
    }

    static void Half(Button b, bool right)
    {
        var r = (RectTransform)b.transform;
        r.anchorMin = new Vector2(right ? .5f : 0, 1); r.anchorMax = new Vector2(right ? 1 : .5f, 1);
        r.offsetMin = new Vector2(12, r.offsetMin.y); r.offsetMax = new Vector2(-12, r.offsetMax.y);
    }

    static void Quarter(Button b, int i)
    {
        var r = (RectTransform)b.transform;
        r.anchorMin = new Vector2(i * .25f, 1); r.anchorMax = new Vector2((i + 1) * .25f, 1);
        r.offsetMin = new Vector2(8, r.offsetMin.y); r.offsetMax = new Vector2(-8, r.offsetMax.y);
    }

    void ToggleBest()
    {
        showBest = !showBest;
        bestToggleLabel.text = "Best move: " + (showBest ? "On" : "Off");
        UpdateArrow(); UpdateTexts();
    }

    void JumpError(int dir)
    {
        for (int k = cursor + dir; k >= 1 && k <= ga.PlyCount; k += dir)
        {
            MoveClass mc = ga.Classify(k - 1);
            if (mc == MoveClass.Inaccuracy || mc == MoveClass.Mistake || mc == MoveClass.Blunder) { Show(k); return; }
        }
        if (ux != null) ux.Notify(dir > 0 ? "No more errors found (yet)." : "No earlier errors.");
    }

    void Update()
    {
        if (ga == null) return;
        if (Input.GetKeyDown(KeyCode.RightArrow)) Show(cursor + 1);
        if (Input.GetKeyDown(KeyCode.LeftArrow)) Show(cursor - 1);
        if (Input.GetKeyDown(KeyCode.Home)) Show(0);
        if (Input.GetKeyDown(KeyCode.End)) Show(ga.Positions.Count - 1);
        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }

        int completed = ga.Completed;
        if ((completed != drawnCompleted || cursor != drawnCursor) && Time.unscaledTime >= nextRedraw)
        {
            bool progressed = completed != drawnCompleted;
            drawnCompleted = completed; drawnCursor = cursor;
            nextRedraw = Time.unscaledTime + .2f;
            DrawGraph();
            if (progressed) { UpdateArrow(); UpdateTexts(); }
        }
        if (ga.Finished) SaveSummaryIfFinished();
    }

    void DrawGraph()
    {
        var bg = new Color32(22, 36, 44, 255);
        var orange = new Color32(240, 140, 50, 255);
        var green = new Color32(70, 190, 110, 255);
        var mid = new Color32(90, 105, 112, 255);
        for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;
        int last = ga.Positions.Count - 1;
        int half = GH / 2;
        for (int x = 0; x < GW; x++)
        {
            int p = last == 0 ? 0 : Mathf.RoundToInt((float)x / (GW - 1) * last);
            double w = ga.OrangeWin(p);
            if (double.IsNaN(w)) continue;
            int y = Mathf.Clamp(Mathf.RoundToInt((float)w * (GH - 1)), 0, GH - 1);
            if (y >= half) for (int yy = half; yy <= y; yy++) pixels[yy * GW + x] = orange;
            else for (int yy = y; yy < half; yy++) pixels[yy * GW + x] = green;
        }
        for (int x = 0; x < GW; x++) pixels[half * GW + x] = mid;

        // Error markers on the move that caused them.
        for (int i = 0; i < ga.PlyCount; i++)
        {
            MoveClass mc = ga.Classify(i);
            Color32 col;
            if (mc == MoveClass.Blunder) col = new Color32(235, 70, 60, 255);
            else if (mc == MoveClass.Mistake) col = new Color32(240, 170, 40, 255);
            else if (mc == MoveClass.Inaccuracy) col = new Color32(110, 170, 240, 255);
            else continue;
            int x = Mathf.RoundToInt((float)(i + 1) / Mathf.Max(1, last) * (GW - 1));
            double w = ga.OrangeWin(i + 1);
            int y = Mathf.Clamp(Mathf.RoundToInt((float)w * (GH - 1)), 3, GH - 4);
            for (int dx = -3; dx <= 3; dx++) for (int dy = -3; dy <= 3; dy++)
            {
                int px = x + dx, py = y + dy;
                if (px >= 0 && px < GW && dx * dx + dy * dy <= 10) pixels[py * GW + px] = col;
            }
        }

        int cx = last == 0 ? 0 : Mathf.RoundToInt((float)cursor / last * (GW - 1));
        for (int y = 0; y < GH; y++) for (int dx = 0; dx < 2; dx++)
            if (cx + dx < GW) pixels[y * GW + cx + dx] = new Color32(245, 235, 210, 255);

        tex.SetPixels32(pixels);
        tex.Apply(false);
    }
}

// Tap or drag on the advantage graph to jump to that point of the game.
public class AnalysisGraphInput : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public System.Action<float> OnPick;
    public void OnPointerDown(PointerEventData e) { Pick(e); }
    public void OnDrag(PointerEventData e) { Pick(e); }
    void Pick(PointerEventData e)
    {
        var r = (RectTransform)transform;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(r, e.position, e.pressEventCamera, out local)) return;
        float f = Mathf.InverseLerp(r.rect.xMin, r.rect.xMax, local.x);
        OnPick?.Invoke(Mathf.Clamp01(f));
    }
}
