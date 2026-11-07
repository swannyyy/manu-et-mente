// SPDX-License-Identifier: MPL-2.0
//
// AIAgent — the computer opponent.
//
// The thinking happens in ManuEngine (pure C#): a fast copy of the board, exact move generation,
// all four positional wins plus commander capture, and an iterative-deepening alpha-beta search
// with a self-play-trained evaluation (weights in ManuWeights.cs). Search runs on a worker thread
// so the game keeps animating while the AI thinks; the chosen move is then played through the
// real game code (move plates / RotateRing), exactly as a human click would.
//
// The previous implementation is preserved in AI_backup_2026-11-06/ at the project root.

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using ManuAI;

public class AIAgent : MonoBehaviour
{
    private game gameController;
    private string aiPlayer = "green";

    [Header("AI Settings")]
    public bool isEnabled = true;
    public DifficultyLevel difficulty = DifficultyLevel.Medium;

    [Header("Think Time (seconds)")]
    public float easyThinkTimeMin = 1.5f;
    public float easyThinkTimeMax = 2.5f;
    public float mediumThinkTimeMin = 0.8f;
    public float mediumThinkTimeMax = 1.8f;
    [Tooltip("Search time budget for Hard. The AI stops deepening when this runs out.")]
    public float hardThinkTimeMax = 3f;

    [Header("Search Depth (plies)")]
    public int easySearchDepth = 2;
    public int mediumSearchDepth = 3;
    public int hardSearchDepth = 10;

    [Header("Variety")]
    [Tooltip("Easy: chance of playing a random legal move (never when a win is available).")]
    [Range(0f, 1f)] public float easyRandomMoveChance = 0.15f;
    [Tooltip("Random jitter (evaluation units) added to root moves so games are not identical.")]
    public float easyNoise = 30f;
    public float mediumNoise = 10f;
    public float hardNoise = 2f;

    [Header("Debug")]
    [Tooltip("Editor only: before each AI move, compare the engine's legal moves with the move " +
             "plates the real game spawns, and log a warning if they ever differ.")]
    public bool verifyEngineAgainstPlates = true;
    public bool logSearchInfo = true;
    [Tooltip("Testing: the AI plays BOTH colours (AI vs AI) so you can watch it and check for warnings.")]
    public bool aiVsAiTest = false;

    public string PlayerColor => aiPlayer;
    public bool IsThinking => isThinking;
    private bool isThinking = false;

    private double[] weights;
    private Searcher searcher;

    public enum DifficultyLevel
    {
        Easy,
        Medium,
        Hard
    }

    void Start()
    {
        gameController = GetComponent<game>();
        if (gameController == null)
        {
            Debug.LogError("AIAgent requires game component!");
            enabled = false;
            return;
        }

        weights = LoadWeights();
        searcher = new Searcher(weights);

        if (PlayerPrefs.HasKey("AIDifficulty"))
        {
            difficulty = (DifficultyLevel)PlayerPrefs.GetInt("AIDifficulty");
            Debug.Log($"AI difficulty loaded: {difficulty}");
        }
        else
        {
            difficulty = DifficultyLevel.Medium;
            Debug.Log("No difficulty saved, defaulting to Medium");
        }
    }

    // Trained weights: Resources/ManuAIWeights.txt (written by Manu AI > Train) overrides the
    // weights baked into ManuWeights.cs.
    public static double[] LoadWeights()
    {
        TextAsset ta = Resources.Load<TextAsset>(ManuWeights.ResourceName);
        if (ta != null)
        {
            double[] w = ManuTrainer.FromText(ta.text);
            if (w != null)
            {
                Debug.Log("AIAgent: using trained weights from Resources/" + ManuWeights.ResourceName + ".txt");
                return w;
            }
            Debug.LogWarning("AIAgent: Resources/" + ManuWeights.ResourceName + ".txt is invalid; using baked weights.");
        }
        return (double[])ManuWeights.Trained.Clone();
    }

    void OnDestroy()
    {
        if (searcher != null) searcher.CancelRequested = true;
    }

    // Project-local folder (next to Assets/ in the editor) where game.cs records PvC games.
    public static string GameLogsFolder()
    {
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "GameLogs"));
    }

    public string GetAIPlayer() { return aiPlayer; }

    void Update()
    {
        if (!isEnabled || gameController == null || gameController.IsGameOver())
            return;
        if (isThinking || GameUX.PauseAI)
            return;
        if (aiVsAiTest)
            aiPlayer = gameController.GetCurrentPlayer();
        if (gameController.GetCurrentPlayer() == aiPlayer)
            StartCoroutine(MakeAIMove());
    }

    private IEnumerator MakeAIMove()
    {
        isThinking = true;

        float minDelay, maxDelay;
        switch (difficulty)
        {
            case DifficultyLevel.Easy: minDelay = easyThinkTimeMin; maxDelay = easyThinkTimeMax; break;
            case DifficultyLevel.Medium: minDelay = mediumThinkTimeMin; maxDelay = mediumThinkTimeMax; break;
            default: minDelay = 0.3f; maxDelay = 0.3f; break;
        }
        float started = Time.time;
        float wanted = Random.Range(minDelay, maxDelay);

        yield return new WaitUntil(() => !GameUX.PauseAI);

        Board board = BuildBoard();

#if UNITY_EDITOR
        if (verifyEngineAgainstPlates) VerifyMoveGeneration(board);
#endif

        ConfigureSearcher();
        System.Random rng = new System.Random(Random.Range(int.MinValue, int.MaxValue));
        Task<SearchResult> task = Task.Run(() => searcher.Search(board));
        yield return new WaitUntil(() => task.IsCompleted);

        // Make the AI feel like it is thinking for at least the configured delay.
        float remaining = wanted - (Time.time - started);
        if (remaining > 0f) yield return new WaitForSeconds(remaining);
        yield return new WaitUntil(() => !GameUX.PauseAI);

        if (this == null || gameController == null || gameController.IsGameOver() ||
            gameController.GetCurrentPlayer() != aiPlayer)
        {
            isThinking = false;
            yield break;
        }

        SearchResult result = task.IsFaulted ? null : task.Result;
        if (task.IsFaulted) Debug.LogError("AIAgent: search failed: " + task.Exception);

        Move chosen = result != null ? result.Best : Move.None;

        // Easy: sometimes play a random move, unless a win is on the board.
        if (difficulty == DifficultyLevel.Easy && result != null &&
            result.Score < Searcher.WinScore / 2 && rng.NextDouble() < easyRandomMoveChance)
        {
            var all = new List<Move>();
            board.GenerateMoves(all);
            if (all.Count > 0) chosen = all[rng.Next(all.Count)];
        }

        if (logSearchInfo && result != null)
            Debug.Log($"AI ({difficulty}) chose {Describe(chosen, board)}: depth {result.Depth}, " +
                      $"{result.Nodes} nodes, {result.Millis} ms, score {result.Score:F0}");

        bool played = !chosen.IsNone && TryExecute(chosen, board);
        if (!played)
        {
            Debug.LogWarning("AIAgent: engine move could not be played; falling back to any legal move.");
            PlayAnyLegalMove();
        }

        isThinking = false;
    }

    private void ConfigureSearcher()
    {
        searcher.Weights = weights;
        searcher.UseLegacyEval = false;
        switch (difficulty)
        {
            case DifficultyLevel.Easy:
                searcher.MaxDepth = Mathf.Max(1, easySearchDepth);
                searcher.QuiescenceDepth = 0;
                searcher.TimeLimitMs = 500;
                searcher.RootNoise = easyNoise;
                break;
            case DifficultyLevel.Medium:
                searcher.MaxDepth = Mathf.Max(1, mediumSearchDepth);
                searcher.QuiescenceDepth = 3;
                searcher.TimeLimitMs = (long)(mediumThinkTimeMax * 1000);
                searcher.RootNoise = mediumNoise;
                break;
            default:
                searcher.MaxDepth = Mathf.Max(1, hardSearchDepth);
                searcher.QuiescenceDepth = 4;
                searcher.TimeLimitMs = (long)(hardThinkTimeMax * 1000);
                searcher.RootNoise = hardNoise;
                break;
        }
        searcher.Rng = new System.Random(Random.Range(int.MinValue, int.MaxValue));
    }

    // ========== Unity board <-> engine board ==========

    private Board BuildBoard()
    {
        var b = new Board();
        for (int ring = 1; ring <= 7; ring++)
        {
            for (int place = 1; place <= Geo.RingSize[ring - 1]; place++)
            {
                GameObject obj = gameController.GetPosition(ring, place);
                if (obj == null) continue;
                Chessman cm = obj.GetComponent<Chessman>();
                if (cm == null) continue;
                int type = Pc.FromLetter(cm.pieceType);
                if (type == Pc.Empty) continue;
                b.Sq[Geo.Cell(ring, place)] = (byte)Pc.Make(type, Pc.ColorFromName(cm.player));
            }
        }
        b.Side = Pc.ColorFromName(gameController.GetCurrentPlayer());
        b.RotStreak = gameController.ConsecutiveRotations;
        game.MoveInfo lm = gameController.GetLastMove();
        if (lm != null && lm.fromRing >= 1 && lm.toRing >= 1)
        {
            b.LmColor = Pc.ColorFromName(lm.player);
            b.LmFrom = Geo.Cell(lm.fromRing, lm.fromPlace);
            b.LmTo = Geo.Cell(lm.toRing, lm.toPlace);
            b.LmType = Pc.FromLetter(lm.pieceType);
        }
        b.RecomputeHash();
        return b;
    }

    private Chessman PieceAt(int cell)
    {
        GameObject obj = gameController.GetPosition(Geo.RingOf[cell], Geo.PlaceOf[cell]);
        return obj != null ? obj.GetComponent<Chessman>() : null;
    }

    private static string Describe(Move m, Board b)
    {
        if (m.IsNone) return "nothing";
        if (m.IsRotation) return $"rotate pair {m.Pair} {(m.Clockwise ? "CW" : "ACW")}";
        string piece = Pc.Letter(Pc.Type(b.Sq[m.From]));
        return $"{piece} ({Geo.RingOf[m.From]},{Geo.PlaceOf[m.From]}) {(m.IsCapture ? "x" : "-")} ({Geo.RingOf[m.To]},{Geo.PlaceOf[m.To]})";
    }

    // Plays an engine move through the real game code. Returns false if the game rejected it.
    private bool TryExecute(Move m, Board b)
    {
        Chessman piece = PieceAt(m.From);
        if (piece == null || piece.player != aiPlayer) return false;
        var move = new AIMove
        {
            piece = piece,
            fromRing = Geo.RingOf[m.From],
            fromPlace = Geo.PlaceOf[m.From],
            isRotation = m.IsRotation,
            ringPair = m.Pair,
            clockwise = m.Clockwise,
            toRing = m.IsRotation ? 0 : Geo.RingOf[m.To],
            toPlace = m.IsRotation ? 0 : Geo.PlaceOf[m.To]
        };
        string before = gameController.GetCurrentPlayer();
        ExecuteMove(move);
        return gameController.IsGameOver() || gameController.GetCurrentPlayer() != before;
    }

    // Last-resort safety net so the AI never stalls: play any move the real game allows.
    private void PlayAnyLegalMove()
    {
        List<AIMove> moves = GetPlateMoves(aiPlayer);
        if (moves.Count == 0) { Debug.LogWarning("AI has no valid moves!"); return; }
        ExecuteMove(moves[Random.Range(0, moves.Count)]);
    }

    // ========== Real-game move generation (via move plates) ==========

    private List<AIMove> GetPlateMoves(string player)
    {
        var moves = new List<AIMove>();
        foreach (Chessman piece in FindObjectsByType<Chessman>(FindObjectsInactive.Exclude))
        {
            if (piece.player != player) continue;
            // Skip pieces that were captured this frame (Destroy is deferred) and are off-board.
            GameObject at = gameController.GetPosition(piece.GetRingNumber(), piece.GetPlaceNumber());
            if (at != piece.gameObject) continue;

            foreach (Vector2Int dest in GetValidMovesForPiece(piece))
                moves.Add(new AIMove { piece = piece, fromRing = piece.GetRingNumber(), fromPlace = piece.GetPlaceNumber(), toRing = dest.x, toPlace = dest.y });

            if ((piece.pieceType == "S" || piece.pieceType == "W") && !gameController.RotationsBlocked)
            {
                int pair = GetRingPair(piece.GetRingNumber());
                moves.Add(new AIMove { piece = piece, isRotation = true, ringPair = pair, clockwise = false });
                moves.Add(new AIMove { piece = piece, isRotation = true, ringPair = pair, clockwise = true });
            }
        }
        return moves;
    }

#if UNITY_EDITOR
    // Compares engine move generation with the real move plates for the current position.
    private void VerifyMoveGeneration(Board b)
    {
        var engineSet = new HashSet<string>();
        var list = new List<Move>();
        b.GenerateMoves(list);
        foreach (Move m in list)
            if (!m.IsRotation) engineSet.Add($"{Geo.RingOf[m.From]},{Geo.PlaceOf[m.From]}->{Geo.RingOf[m.To]},{Geo.PlaceOf[m.To]}");

        var plateSet = new HashSet<string>();
        bool prevLog = Debug.unityLogger.logEnabled;
        Debug.unityLogger.logEnabled = false; // Chessman logs heavily during plate generation
        try
        {
            foreach (AIMove m in GetPlateMoves(aiPlayer))
                if (!m.isRotation) plateSet.Add($"{m.fromRing},{m.fromPlace}->{m.toRing},{m.toPlace}");
        }
        finally { Debug.unityLogger.logEnabled = prevLog; }

        if (!engineSet.SetEquals(plateSet))
        {
            var onlyEngine = new HashSet<string>(engineSet); onlyEngine.ExceptWith(plateSet);
            var onlyPlates = new HashSet<string>(plateSet); onlyPlates.ExceptWith(engineSet);
            Debug.LogWarning("AIAgent: engine/plate move mismatch! Engine-only: [" + string.Join(" ", onlyEngine) +
                             "]  Plate-only: [" + string.Join(" ", onlyPlates) + "]. Please report this position.");
        }
    }
#endif

    // Destroys all MovePlate objects synchronously (Destroy() is deferred to end of frame).
    private void ClearAllMovePlates()
    {
        foreach (GameObject p in GameObject.FindGameObjectsWithTag("MovePlate"))
            if (p != null) DestroyImmediate(p);
    }

    private List<Vector2Int> GetValidMovesForPiece(Chessman piece)
    {
        var validMoves = new List<Vector2Int>();
        ClearAllMovePlates();
        piece.InitiateMovePlates();
        foreach (GameObject plateObj in GameObject.FindGameObjectsWithTag("MovePlate"))
        {
            MovePlate plate = plateObj.GetComponent<MovePlate>();
            if (plate != null) validMoves.Add(new Vector2Int(plate.GetRingNumber(), plate.GetPlaceNumber()));
        }
        ClearAllMovePlates();
        return validMoves;
    }

    public void ExecuteMove(AIMove move)
    {
        if (move.isRotation)
        {
            gameController.RotateRing(move.ringPair, move.clockwise);
            return;
        }

        ClearAllMovePlates();
        move.piece.InitiateMovePlates();
        foreach (GameObject plateObj in GameObject.FindGameObjectsWithTag("MovePlate"))
        {
            MovePlate plate = plateObj.GetComponent<MovePlate>();
            if (plate != null && plate.GetRingNumber() == move.toRing && plate.GetPlaceNumber() == move.toPlace)
            {
                plate.OnMouseUp(); // simulate clicking the move plate
                break;
            }
        }
        ClearAllMovePlates();
    }

    private int GetRingPair(int ringNumber)
    {
        if (ringNumber <= 2) return 1;
        if (ringNumber <= 4) return 2;
        if (ringNumber <= 6) return 3;
        return 4;
    }

    public void SetDifficulty(DifficultyLevel newDifficulty)
    {
        difficulty = newDifficulty;
        Debug.Log($"AI difficulty set to: {difficulty}");
    }
}

// ========== AI MOVE CLASS ========== //

public class AIMove
{
    public Chessman piece;
    public int fromRing;
    public int fromPlace;
    public int toRing;
    public int toPlace;
    public bool isRotation;
    public int ringPair;
    public bool clockwise;

    public float score;

    public override string ToString()
    {
        if (isRotation)
            return $"Rotate ring pair {ringPair} {(clockwise ? "CW" : "ACW")} with {piece.pieceType} (score: {score:F1})";
        return $"Move {piece.pieceType} from ({fromRing},{fromPlace}) to ({toRing},{toPlace}) (score: {score:F1})";
    }
}
