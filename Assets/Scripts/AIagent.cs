// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using System.Linq;

// public class AIAgent : MonoBehaviour
// {
//     private game gameController;
//     private string aiPlayer = "green"; // AI plays as green
    
//     [Header("AI Settings")]
//     public bool isEnabled = true;
//     public DifficultyLevel difficulty = DifficultyLevel.Medium;
    
//     [Header("Think Time")]
//     public float easyThinkTimeMin = 1f;
//     public float easyThinkTimeMax = 2.5f;
//     public float mediumThinkTimeMin = 0.8f;
//     public float mediumThinkTimeMax = 1.8f;
//     public float hardThinkTimeMin = 0.3f;
//     public float hardThinkTimeMax = 1f;
    
//     private bool isThinking = false;

//     public enum DifficultyLevel
//     {
//         Easy,
//         Medium,
//         Hard
//     }

//     // Add this to the Start() method in AIAgent.cs
// void Start()
// {
//     gameController = GetComponent<game>();
//     if (gameController == null)
//     {
//         Debug.LogError("AIAgent requires game component!");
//         enabled = false;
//         return;
//     }
    
//     // Load difficulty from PlayerPrefs (saved from menu)
//     if (PlayerPrefs.HasKey("AIDifficulty"))
//     {
//         int savedDifficulty = PlayerPrefs.GetInt("AIDifficulty");
//         difficulty = (DifficultyLevel)savedDifficulty;
//         Debug.Log($"AI difficulty loaded from menu: {difficulty}");
//     }
//     else
//     {
//         // Default to Medium if not set
//         difficulty = DifficultyLevel.Medium;
//         Debug.Log("No difficulty selected, defaulting to Medium");
//     }
// }

//     void Update()
//     {
//         if (!isEnabled || gameController.IsGameOver() || isThinking)
//             return;

//         // Check if it's AI's turn
//         if (gameController.GetCurrentPlayer() == aiPlayer)
//         {
//             StartCoroutine(MakeAIMove());
//         }
//     }

//     private IEnumerator MakeAIMove()
//     {
//         isThinking = true;
        
//         // Think time based on difficulty
//         float minTime, maxTime;
//         switch (difficulty)
//         {
//             case DifficultyLevel.Easy:
//                 minTime = easyThinkTimeMin;
//                 maxTime = easyThinkTimeMax;
//                 break;
//             case DifficultyLevel.Medium:
//                 minTime = mediumThinkTimeMin;
//                 maxTime = mediumThinkTimeMax;
//                 break;
//             case DifficultyLevel.Hard:
//                 minTime = hardThinkTimeMin;
//                 maxTime = hardThinkTimeMax;
//                 break;
//             default:
//                 minTime = 0.5f;
//                 maxTime = 1.5f;
//                 break;
//         }
        
//         float thinkTime = Random.Range(minTime, maxTime);
//         yield return new WaitForSeconds(thinkTime);
        
//         Debug.Log($"AI ({difficulty}) is thinking...");
        
//         // Get all possible moves
//         List<AIMove> possibleMoves = GetAllPossibleMoves();
        
//         if (possibleMoves.Count == 0)
//         {
//             Debug.LogWarning("AI has no valid moves!");
//             isThinking = false;
//             yield break;
//         }
        
//         // Choose move based on difficulty
//         AIMove bestMove = ChooseMoveByDifficulty(possibleMoves);
        
//         if (bestMove != null)
//         {
//             Debug.Log($"AI ({difficulty}) chose: {bestMove.ToString()}");
//             ExecuteMove(bestMove);
//         }
//         else
//         {
//             Debug.LogWarning("AI couldn't choose a move!");
//         }
        
//         isThinking = false;
//     }

//     private AIMove ChooseMoveByDifficulty(List<AIMove> moves)
//     {
//         if (moves.Count == 0)
//             return null;

//         switch (difficulty)
//         {
//             case DifficultyLevel.Easy:
//                 return ChooseEasyMove(moves);
            
//             case DifficultyLevel.Medium:
//                 return ChooseMediumMove(moves);
            
//             case DifficultyLevel.Hard:
//                 return ChooseHardMove(moves);
            
//             default:
//                 return moves[Random.Range(0, moves.Count)];
//         }
//     }

//     private AIMove ChooseEasyMove(List<AIMove> moves)
//     {
//         // Easy: 70% random, 30% okay moves
//         // Makes mistakes frequently
        
//         if (Random.value < 0.7f)
//         {
//             // Completely random move
//             return moves[Random.Range(0, moves.Count)];
//         }
//         else
//         {
//             // Pick from bottom half of moves (worse moves)
//             moves.Sort((a, b) => a.score.CompareTo(b.score)); // Ascending
//             int halfPoint = Mathf.Max(1, moves.Count / 2);
//             return moves[Random.Range(0, halfPoint)];
//         }
//     }

//     private AIMove ChooseMediumMove(List<AIMove> moves)
//     {
//         // Medium: Mix of strategy and randomness
//         // 30% random, 70% decent strategy
        
//         if (Random.value < 0.3f)
//         {
//             // Random move
//             return moves[Random.Range(0, moves.Count)];
//         }
//         else
//         {
//             // Pick from top 50% of moves
//             moves.Sort((a, b) => b.score.CompareTo(a.score)); // Descending
//             int topHalf = Mathf.Max(1, moves.Count / 2);
            
//             // Add some variety within good moves
//             return moves[Random.Range(0, topHalf)];
//         }
//     }

//     private AIMove ChooseHardMove(List<AIMove> moves)
//     {
//         // Hard: Strategic play with occasional variety
//         // Enhanced evaluation for all moves
        
//         foreach (AIMove move in moves)
//         {
//             // Apply advanced strategic bonuses
//             move.score += EvaluateStrategic(move);
//         }
        
//         // Sort best to worst
//         moves.Sort((a, b) => b.score.CompareTo(a.score));
        
//         if (Random.value < 0.1f)
//         {
//             // 10% chance: pick from top 5 moves for variety
//             int topN = Mathf.Min(5, moves.Count);
//             return moves[Random.Range(0, topN)];
//         }
//         else
//         {
//             // 90% chance: pick best move
//             return moves[0];
//         }
//     }

//     private float EvaluateStrategic(AIMove move)
//     {
//         float bonus = 0f;
        
//         // Strategic thinking for hard mode
        
//         // Prioritize getting Hand/Mind to D ring (winning move!)
//         if (move.toRing == 7 && (move.piece.pieceType == "H" || move.piece.pieceType == "B"))
//         {
//             bonus += 25f; // Very high priority - this scores points!
//         }
        
//         // Advance enablers (Technician/Sage) strategically
//         if (move.piece.pieceType == "T" || move.piece.pieceType == "W")
//         {
//             if (move.movesTowardCenter)
//             {
//                 bonus += 8f; // These unlock the board
//             }
            
//             // Extra bonus if getting enabler to D ring
//             if (move.toRing == 7)
//             {
//                 bonus += 12f;
//             }
//         }
        
//         // Protect valuable pieces already in D ring
//         if (move.fromRing == 7 && (move.piece.pieceType == "H" || move.piece.pieceType == "B"))
//         {
//             // Moving out of D ring is bad unless capturing something valuable
//             if (!move.isCapture)
//             {
//                 bonus -= 15f;
//             }
//         }
        
//         // Block opponent from D ring
//         if (move.toRing == 7)
//         {
//             // Check if opponent has pieces near D
//             bonus += CheckOpponentThreat();
//         }
        
//         // Prefer captures of high-value pieces
//         if (move.isCapture)
//         {
//             GameObject targetPiece = gameController.GetPosition(move.toRing, move.toPlace);
//             if (targetPiece != null)
//             {
//                 Chessman target = targetPiece.GetComponent<Chessman>();
//                 if (target != null)
//                 {
//                     if (target.pieceType == "H" || target.pieceType == "B")
//                         bonus += 20f; // Prevent opponent from scoring
//                     else if (target.pieceType == "T" || target.pieceType == "W")
//                         bonus += 15f; // Slow opponent's board access
                    
//                     // Extra bonus if capturing from D ring
//                     if (target.GetRingNumber() == 7)
//                         bonus += 10f;
//                 }
//             }
//         }
        
//         // Use rotations tactically
//         if (move.isRotation)
//         {
//             bonus += EvaluateRotation(move);
//         }
        
//         // Positional play - control inner rings
//         if (move.toRing >= 5 && move.toRing < 7)
//         {
//             bonus += 3f; // Good central control
//         }
        
//         // Scholar positioning (they enable rotations)
//         if (move.piece.pieceType == "S" && move.toRing >= 4)
//         {
//             bonus += 4f; // Scholars in good position for future rotations
//         }
        
//         return bonus;
//     }

//     private float CheckOpponentThreat()
//     {
//         // Check if opponent has pieces close to D ring
//         float threat = 0f;
//         string opponent = aiPlayer == "green" ? "orange" : "green";
        
//         // Check ring 6 (c) for opponent pieces threatening D ring
//         for (int p = 1; p <= 12; p++)
//         {
//             GameObject piece = gameController.GetPosition(6, p);
//             if (piece != null)
//             {
//                 Chessman cm = piece.GetComponent<Chessman>();
//                 if (cm != null && cm.player == opponent)
//                 {
//                     if (cm.pieceType == "H" || cm.pieceType == "B")
//                         threat += 6f; // Opponent close to scoring
//                     else if (cm.pieceType == "T" || cm.pieceType == "W")
//                         threat += 4f; // Opponent enablers advancing
//                 }
//             }
//         }
        
//         return threat;
//     }

//     private float EvaluateRotation(AIMove move)
//     {
//         float rotationValue = 2f;
        
//         // Sage rotations are generally more valuable (affect more rings)
//         if (move.piece.pieceType == "W")
//         {
//             rotationValue += 3f;
//         }
        
//         // Could be enhanced to check if rotation aligns our pieces better
//         // or disrupts opponent positions
        
//         return rotationValue;
//     }

//     private List<AIMove> GetAllPossibleMoves()
//     {
//         List<AIMove> moves = new List<AIMove>();
        
//         // Find all AI pieces
//         GameObject[] allPieces = GameObject.FindGameObjectsWithTag("Piece");
        
//         foreach (GameObject pieceObj in allPieces)
//         {
//             Chessman piece = pieceObj.GetComponent<Chessman>();
//             if (piece == null || piece.player != aiPlayer)
//                 continue;
            
//             int fromRing = piece.GetRingNumber();
//             int fromPlace = piece.GetPlaceNumber();
            
//             // Get valid moves for this piece
//             List<Vector2Int> validDestinations = GetValidMovesForPiece(piece);
            
//             foreach (Vector2Int dest in validDestinations)
//             {
//                 AIMove move = new AIMove
//                 {
//                     piece = piece,
//                     fromRing = fromRing,
//                     fromPlace = fromPlace,
//                     toRing = dest.x,
//                     toPlace = dest.y,
//                     isRotation = false
//                 };
                
//                 move.EvaluateMove(gameController, aiPlayer);
//                 moves.Add(move);
//             }
            
//             // Check for rotation moves (Scholar and Sage only)
//             if (piece.pieceType == "S" || piece.pieceType == "W")
//             {
//                 int ringPair = GetRingPair(fromRing);
                
//                 // ACW rotation
//                 AIMove rotateACW = new AIMove
//                 {
//                     piece = piece,
//                     fromRing = fromRing,
//                     fromPlace = fromPlace,
//                     ringPair = ringPair,
//                     clockwise = false,
//                     isRotation = true
//                 };
//                 rotateACW.EvaluateMove(gameController, aiPlayer);
//                 moves.Add(rotateACW);
                
//                 // CW rotation
//                 AIMove rotateCW = new AIMove
//                 {
//                     piece = piece,
//                     fromRing = fromRing,
//                     fromPlace = fromPlace,
//                     ringPair = ringPair,
//                     clockwise = true,
//                     isRotation = true
//                 };
//                 rotateCW.EvaluateMove(gameController, aiPlayer);
//                 moves.Add(rotateCW);
//             }
//         }
        
//         return moves;
//     }

//     private List<Vector2Int> GetValidMovesForPiece(Chessman piece)
//     {
//         List<Vector2Int> validMoves = new List<Vector2Int>();
        
//         // Temporarily generate move plates to see valid moves
//         piece.InitiateMovePlates();
        
//         // Find all move plates
//         GameObject[] movePlates = GameObject.FindGameObjectsWithTag("MovePlate");
        
//         foreach (GameObject plateObj in movePlates)
//         {
//             MovePlate plate = plateObj.GetComponent<MovePlate>();
//             if (plate != null)
//             {
//                 validMoves.Add(new Vector2Int(plate.GetRingNumber(), plate.GetPlaceNumber()));
//             }
//         }
        
//         // Clean up move plates
//         foreach (GameObject plateObj in movePlates)
//         {
//             Destroy(plateObj);
//         }
        
//         return validMoves;
//     }

//     private void ExecuteMove(AIMove move)
//     {
//         if (move.isRotation)
//         {
//             // Execute rotation
//             gameController.RotateRing(move.ringPair, move.clockwise);
//         }
//         else
//         {
//             // Execute piece move
//             move.piece.InitiateMovePlates();
            
//             GameObject[] movePlates = GameObject.FindGameObjectsWithTag("MovePlate");
//             foreach (GameObject plateObj in movePlates)
//             {
//                 MovePlate plate = plateObj.GetComponent<MovePlate>();
//                 if (plate != null && 
//                     plate.GetRingNumber() == move.toRing && 
//                     plate.GetPlaceNumber() == move.toPlace)
//                 {
//                     // Simulate clicking the move plate
//                     plate.OnMouseUp();
//                     break;
//                 }
//             }
            
//             // Clean up any remaining move plates
//             movePlates = GameObject.FindGameObjectsWithTag("MovePlate");
//             foreach (GameObject plateObj in movePlates)
//             {
//                 Destroy(plateObj);
//             }
//         }
//     }

//     private int GetRingPair(int ringNumber)
//     {
//         if (ringNumber <= 2) return 1;
//         else if (ringNumber <= 4) return 2;
//         else if (ringNumber <= 6) return 3;
//         else return 4;
//     }

//     // Public method to change difficulty during gameplay
//     public void SetDifficulty(DifficultyLevel newDifficulty)
//     {
//         difficulty = newDifficulty;
//         Debug.Log($"AI difficulty set to: {difficulty}");
//     }
// }

// // Class to represent a potential move
// public class AIMove
// {
//     public Chessman piece;
//     public int fromRing;
//     public int fromPlace;
//     public int toRing;
//     public int toPlace;
//     public bool isRotation;
//     public int ringPair;
//     public bool clockwise;
    
//     public float score;
//     public bool isCapture;
//     public bool movesTowardCenter;
//     public bool threatensOpponent;
//     public void EvaluateMove(game gameController, string aiPlayer)
//     {
//         score = 0f;
        
//         if (isRotation)
//         {
//             EvaluateRotation(gameController, aiPlayer);
//             return;
//         }
        
//         // Check if move is toward center (higher ring number = closer to center)
//         movesTowardCenter = toRing > fromRing;
//         if (movesTowardCenter)
//             score += 3f;
        
//         // Check if destination is in D ring
//         if (toRing == 7)
//         {
//             // Moving to D ring is very valuable
//             if (piece.pieceType == "H" || piece.pieceType == "B")
//                 score += 20f; // Hand and Mind score points!
//             else if (piece.pieceType == "P")
//                 score += 7f; // Pawns score 1/3 point
//             else if (piece.pieceType == "T" || piece.pieceType == "W")
//                 score += 10f; // Enablers in D unlock board
//             else
//                 score += 5f;
//         }
        
//         // Check if move captures opponent piece
//         GameObject targetPiece = gameController.GetPosition(toRing, toPlace);
//         if (targetPiece != null)
//         {
//             Chessman target = targetPiece.GetComponent<Chessman>();
//             if (target != null && target.player != aiPlayer)
//             {
//                 isCapture = true;
//                 score += 8f;
                
//                 // Bonus for capturing valuable pieces
//                 if (target.pieceType == "H" || target.pieceType == "B")
//                     score += 12f; // Prevent opponent from scoring
//                 else if (target.pieceType == "T" || target.pieceType == "W")
//                     score += 10f; // Slow down opponent's board access
//                 else if (target.GetRingNumber() == 7)
//                     score += 8f; // Remove opponent from D ring
//             }
//         }
        
//         // Bonus for advancing enablers (Technician and Sage)
//         if ((piece.pieceType == "T" || piece.pieceType == "W") && movesTowardCenter)
//         {
//             score += 5f;
//         }
        
//         // Protect valuable pieces (avoid danger)
//         if (piece.pieceType == "H" || piece.pieceType == "B")
//         {
//             // Check if destination is safer
//             if (toRing > 3) // Deeper in board = safer
//                 score += 2f;
//         }
//     }
    
//     private void EvaluateRotation(game gameController, string aiPlayer)
//     {
//         // Rotations can create tactical advantages
//         score = 3f; // Base value for rotation
        
//         // Sage rotations affect more rings, more valuable
//         if (piece.pieceType == "W")
//         {
//             score += 2f;
//         }
        
//         // TODO: More sophisticated rotation evaluation
//         // Could check if rotation would:
//         // - Align our pieces better
//         // - Disrupt opponent's positions
//         // - Create capture opportunities
//         // - Block opponent's advancement
//     }
    
//     public override string ToString()
//     {
//         if (isRotation)
//             return $"Rotate ring pair {ringPair} {(clockwise ? "CW" : "ACW")} with {piece.pieceType} (score: {score:F1})";
//         else
//             return $"Move {piece.pieceType} from ({fromRing},{fromPlace}) to ({toRing},{toPlace}) {(isCapture ? "[CAPTURE]" : "")} (score: {score:F1})";
//     }
// }
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Diagnostics;

public class AIAgent : MonoBehaviour
{
    private game gameController;
    private string aiPlayer = "green";

    // Evaluation weights. Defaults reproduce the original hand-tuned behavior.
    // Overwritten at runtime by a self-play-trained file if present (see Start()).
    private AIWeights weights = AIWeights.Default();
    public const string WeightsFileName = "manu_ai_weights.json";
    // Frozen copy of the last self-play-trained weights. Online learning is clamped to a band
    // around these so a streak of unusual human games can never collapse the trained AI.
    public const string BaseWeightsFileName = "manu_ai_weights.base.json";
    private AIWeights baselineWeights;
    private bool hasLearnedThisGame = false;

    [Header("Online Learning (learn from human games)")]
    [Tooltip("Master switch. When off, weights never change from playing human games.")]
    public bool learnFromGames = true;
    [Tooltip("Step size when the AI LOSES — relevant weights are nudged up by this fraction.")]
    public float learnRate = 0.03f;
    [Tooltip("Much smaller step when the AI WINS, to gently reinforce rather than overshoot.")]
    public float winReinforceRate = 0.01f;
    [Tooltip("Each weight is clamped to baseline * [driftMin .. driftMax]. Keeps drift bounded.")]
    public float driftMin = 0.5f;
    public float driftMax = 2.0f;

    [Header("AI Settings")]
    public bool isEnabled = true;
    public DifficultyLevel difficulty = DifficultyLevel.Medium;
    
    [Header("Think Time Limits")]
    public float easyThinkTimeMin = 1f;
    public float easyThinkTimeMax = 2.5f;
    public float mediumThinkTimeMin = 0.8f;
    public float mediumThinkTimeMax = 1.8f;
    public float hardThinkTimeMax = 3f; // Maximum 3 seconds
    
    [Header("Search Depth Settings")]
    public int easyMaxDepth = 1;
    public int mediumMaxDepth = 2;
    public int hardMaxDepth = 4; // Will go as deep as time allows
    
    public string PlayerColor => aiPlayer;
    public bool IsThinking => isThinking;
    private bool isThinking = false;
    private Stopwatch searchTimer;
    private long maxSearchTimeMs;

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
            UnityEngine.Debug.LogError("AIAgent requires game component!");
            enabled = false;
            return;
        }
        
        searchTimer = new Stopwatch();

        LoadTrainedWeights();
        LoadOrCreateBaseline();

        // Fold in any game records left unconsumed from previous sessions before play begins.
        if (learnFromGames)
            LearnFromArchive();

        // Load difficulty from PlayerPrefs
        if (PlayerPrefs.HasKey("AIDifficulty"))
        {
            int savedDifficulty = PlayerPrefs.GetInt("AIDifficulty");
            difficulty = (DifficultyLevel)savedDifficulty;
            UnityEngine.Debug.Log($"AI difficulty loaded: {difficulty}");
        }
        else
        {
            difficulty = DifficultyLevel.Medium;
            UnityEngine.Debug.Log("No difficulty saved, defaulting to Medium");
        }
    }

    private void LoadTrainedWeights()
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, WeightsFileName);
        if (System.IO.File.Exists(path))
        {
            try
            {
                string json = System.IO.File.ReadAllText(path);
                AIWeights loaded = JsonUtility.FromJson<AIWeights>(json);
                if (loaded != null)
                {
                    weights = loaded;
                    UnityEngine.Debug.Log($"AIAgent: loaded trained weights from {path}");
                    return;
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"AIAgent: failed to load trained weights ({e.Message}); using defaults.");
            }
        }
        else
        {
            UnityEngine.Debug.Log("AIAgent: no trained weights file found; using default weights.");
        }
    }

    // The baseline is the frozen reference online learning is clamped around. On first run we seed
    // it from whatever weights are currently live (the self-play-trained set, or defaults if none),
    // so the very first human game can still only drift within driftMin..driftMax of that snapshot.
    private void LoadOrCreateBaseline()
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, BaseWeightsFileName);
        if (System.IO.File.Exists(path))
        {
            try
            {
                AIWeights loaded = JsonUtility.FromJson<AIWeights>(System.IO.File.ReadAllText(path));
                if (loaded != null)
                {
                    baselineWeights = loaded;
                    UnityEngine.Debug.Log($"AIAgent: loaded learning baseline from {path}");
                    return;
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"AIAgent: failed to load baseline ({e.Message}); reseeding it.");
            }
        }

        // No baseline yet: snapshot the current live weights and persist it.
        baselineWeights = weights.Clone();
        try
        {
            System.IO.File.WriteAllText(path, JsonUtility.ToJson(baselineWeights, true));
            UnityEngine.Debug.Log($"AIAgent: created learning baseline at {path}");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning($"AIAgent: could not write baseline ({e.Message}).");
        }
    }

    // Project-local folder (sits next to Assets/ in the editor) where PvC games are recorded.
    // Kept local, not in persistentDataPath, so the records are easy to find, inspect, and reuse.
    public static string GameLogsFolder()
    {
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "GameLogs"));
    }

    // Which side the AI plays — written into each game record's footer so the learner knows whose
    // result to learn from.
    public string GetAIPlayer() { return aiPlayer; }

    // Nudges the relevant weights from one game's outcome: a loss pushes the AI toward fixing the
    // weakness that just beat it; a win gently reinforces what worked.
    private void ApplyOutcomeNudge(bool aiWon, bool opponentReachedD, bool aiLostCommanders,
                                   bool aiReachedD, bool opponentLostCommanders)
    {
        if (!aiWon)
        {
            float s = learnRate;
            // Lost to a center push: strengthen center defense and threat awareness.
            if (opponentReachedD)
            {
                weights.opponentCommanderProximity *= 1f + s;
                weights.defensiveMultiplier        *= 1f + s;
                weights.threatCommander            *= 1f + s;
                weights.captureDCommander          *= 1f + s;
            }
            // Lost both commanders: value commanders more and react harder to their being threatened.
            if (aiLostCommanders)
            {
                weights.valueHand        *= 1f + s;
                weights.valueMind        *= 1f + s;
                weights.threatCommander  *= 1f + s;
            }
        }
        else
        {
            float s = winReinforceRate;
            // Won by reaching the center: gently reinforce the aggressive race.
            if (aiReachedD)
            {
                weights.aggressiveAdvance   *= 1f + s;
                weights.arriveDCommander    *= 1f + s;
                weights.advanceTowardCenter *= 1f + s;
            }
            // Won by eliminating commanders: gently reinforce the finishing incentive.
            if (opponentLostCommanders)
            {
                weights.captureLastCommander *= 1f + s;
                weights.captureDCommander    *= 1f + s;
            }
        }
    }

    // Improvement source of truth: the recorded game files. Reads each game record not yet consumed
    // (tracked in a manifest so each is applied exactly once, even across sessions), applies an
    // outcome nudge from its footer, then clamps to the baseline and saves. Driving learning from
    // the accumulated archive — rather than reacting to a single live game — keeps the signal far
    // less noisy and lets games recorded in earlier sessions still contribute.
    [ContextMenu("Learn From Recorded Games")]
    public void LearnFromArchive()
    {
        if (baselineWeights == null) LoadOrCreateBaseline();

        string folder = GameLogsFolder();
        if (!System.IO.Directory.Exists(folder)) return;

        string manifestPath = System.IO.Path.Combine(folder, "processed_manifest.txt");
        var processed = new HashSet<string>();
        if (System.IO.File.Exists(manifestPath))
        {
            foreach (string line in System.IO.File.ReadAllLines(manifestPath))
                if (!string.IsNullOrWhiteSpace(line)) processed.Add(line.Trim());
        }

        int applied = 0, newFiles = 0;

        foreach (string file in System.IO.Directory.GetFiles(folder, "*.txt"))
        {
            string name = System.IO.Path.GetFileName(file);
            if (name == "processed_manifest.txt" || processed.Contains(name)) continue;

            string result = null, decidedBy = null, aiSide = null;
            foreach (string line in System.IO.File.ReadAllLines(file))
            {
                if (line.StartsWith("Result:")) result = line.Substring("Result:".Length).Trim();
                else if (line.StartsWith("Decided-by:")) decidedBy = line.Substring("Decided-by:".Length).Trim();
                else if (line.StartsWith("AI-side:")) aiSide = line.Substring("AI-side:".Length).Trim();
            }

            // Only learn from games with a clear result and a known AI side.
            if (!string.IsNullOrEmpty(result) && !string.IsNullOrEmpty(aiSide) && aiSide != "unknown")
            {
                bool aiWon = result == aiSide;
                bool byD = decidedBy == "dring";
                bool byCommanders = decidedBy == "commanders";

                ApplyOutcomeNudge(
                    aiWon,
                    opponentReachedD: !aiWon && byD,
                    aiLostCommanders: !aiWon && byCommanders,
                    aiReachedD: aiWon && byD,
                    opponentLostCommanders: aiWon && byCommanders);

                applied++;
            }

            processed.Add(name); // mark consumed even if unparseable, so it isn't rescanned forever
            newFiles++;
        }

        if (applied > 0)
        {
            ClampToBaseline();
            SaveLiveWeights();
            UnityEngine.Debug.Log($"AIAgent: learned from {applied} recorded game(s); weights updated.");
        }
        if (newFiles > 0)
            System.IO.File.WriteAllText(manifestPath, string.Join("\n", processed));
    }

    // Constrain every weight to baseline * [driftMin .. driftMax] so online drift stays bounded.
    private void ClampToBaseline()
    {
        if (baselineWeights == null) return;

        var fields = typeof(AIWeights).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        foreach (var f in fields)
        {
            if (f.FieldType != typeof(float)) continue;

            float baseVal = (float)f.GetValue(baselineWeights);
            float live = (float)f.GetValue(weights);

            float lo = baseVal * driftMin;
            float hi = baseVal * driftMax;
            if (lo > hi) { float t = lo; lo = hi; hi = t; } // guard if baseVal is negative

            f.SetValue(weights, Mathf.Clamp(live, lo, hi));
        }
    }

    private void SaveLiveWeights()
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, WeightsFileName);
        try
        {
            System.IO.File.WriteAllText(path, JsonUtility.ToJson(weights, true));
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning($"AIAgent: could not save learned weights ({e.Message}).");
        }
    }

    // Discard all online learning and restore the frozen self-play baseline (live file + memory).
    [ContextMenu("Reset Weights To Baseline")]
    public void ResetWeightsToBaseline()
    {
        if (baselineWeights == null)
        {
            UnityEngine.Debug.LogWarning("AIAgent: no baseline available to reset to.");
            return;
        }
        weights = baselineWeights.Clone();
        SaveLiveWeights();
        UnityEngine.Debug.Log("AIAgent: weights reset to trained baseline.");
    }

    void Update()
    {
        // isEnabled is false during self-play training, so online learning never fires there.
        if (!isEnabled)
            return;

        if (gameController.IsGameOver())
        {
            // The game just ended; game.cs has already written the record file synchronously, so
            // learn from the archive once to pick up this game (and any other unconsumed records).
            if (!hasLearnedThisGame)
            {
                hasLearnedThisGame = true;
                if (learnFromGames)
                    LearnFromArchive();
            }
            return;
        }

        // Not over: a fresh game is in progress (e.g. after Restart), so re-arm the learn-once flag.
        hasLearnedThisGame = false;

        if (isThinking || GameUX.PauseAI)
            return;

        if (gameController.GetCurrentPlayer() == aiPlayer)
        {
            StartCoroutine(MakeAIMove());
        }
    }

    private IEnumerator MakeAIMove()
    {
        isThinking = true;
        
        // Visual delay before AI starts "thinking"
        float visualDelay = 0f;
        switch (difficulty)
        {
            case DifficultyLevel.Easy:
                visualDelay = Random.Range(easyThinkTimeMin, easyThinkTimeMax);
                break;
            case DifficultyLevel.Medium:
                visualDelay = Random.Range(mediumThinkTimeMin, mediumThinkTimeMax);
                break;
            case DifficultyLevel.Hard:
                visualDelay = 0.3f; // Quick start for hard mode
                break;
        }
        
        yield return new WaitForSeconds(visualDelay);
        
        UnityEngine.Debug.Log($"AI ({difficulty}) is thinking...");
        
        yield return new WaitUntil(() => !GameUX.PauseAI);
        // Get best move using appropriate algorithm
        AIMove bestMove = null;
        
        switch (difficulty)
        {
            case DifficultyLevel.Easy:
                bestMove = GetEasyMove();
                break;
            case DifficultyLevel.Medium:
                bestMove = GetMediumMove();
                break;
            case DifficultyLevel.Hard:
                bestMove = GetHardMove();
                break;
        }
        
        if (bestMove != null)
        {
            UnityEngine.Debug.Log($"AI ({difficulty}) chose: {bestMove.ToString()}");
            ExecuteMove(bestMove);
        }
        else
        {
            UnityEngine.Debug.LogWarning("AI couldn't find a valid move!");
        }
        
        isThinking = false;
    }

    // ========== EASY MODE: Mostly Random ========== //
    
    private AIMove GetEasyMove()
    {
        List<AIMove> possibleMoves = GetAllPossibleMoves(aiPlayer);
        
        if (possibleMoves.Count == 0)
            return null;
        
        // 70% random, 30% evaluate
        if (Random.value < 0.7f)
        {
            return possibleMoves[Random.Range(0, possibleMoves.Count)];
        }
        else
        {
            // Evaluate at depth 1
            foreach (AIMove move in possibleMoves)
            {
                move.score = EvaluateMove(move);
            }
            
            possibleMoves.Sort((a, b) => a.score.CompareTo(b.score));
            int halfPoint = Mathf.Max(1, possibleMoves.Count / 2);
            return possibleMoves[Random.Range(0, halfPoint)];
        }
    }

    // ========== MEDIUM MODE: Depth 2 Search ========== //
    
    private AIMove GetMediumMove()
    {
        List<AIMove> possibleMoves = GetAllPossibleMoves(aiPlayer);
        
        if (possibleMoves.Count == 0)
            return null;
        
        // 30% random for variety
        if (Random.value < 0.3f)
        {
            possibleMoves.Sort((a, b) => Random.Range(-1, 2));
            return possibleMoves[0];
        }
        
        // Use Minimax at depth 2
        maxSearchTimeMs = (long)(mediumThinkTimeMax * 1000);
        searchTimer.Restart();
        
        AIMove bestMove = null;
        float bestScore = float.MinValue;
        
        foreach (AIMove move in possibleMoves)
        {
            if (searchTimer.ElapsedMilliseconds > maxSearchTimeMs)
                break;
            
            float score = MinimaxSearch(move, mediumMaxDepth - 1, false, float.MinValue, float.MaxValue);
            move.score = score;
            
            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }
        
        searchTimer.Stop();
        UnityEngine.Debug.Log($"Medium AI searched to depth {mediumMaxDepth}, took {searchTimer.ElapsedMilliseconds}ms");
        
        return bestMove ?? possibleMoves[0];
    }

    // ========== HARD MODE: Deep Search with Time Limit ========== //
    
    private AIMove GetHardMove()
    {
        List<AIMove> possibleMoves = GetAllPossibleMoves(aiPlayer);
        
        if (possibleMoves.Count == 0)
            return null;
        
        // Iterative deepening: start shallow, go deeper if time allows
        maxSearchTimeMs = (long)(hardThinkTimeMax * 1000);
        searchTimer.Restart();
        
        AIMove bestMove = possibleMoves[0];
        float bestScore = float.MinValue;
        int depthReached = 1;
        
        // Try increasing depths until time runs out
        for (int depth = 1; depth <= hardMaxDepth; depth++)
        {
            if (searchTimer.ElapsedMilliseconds > maxSearchTimeMs * 0.9f) // 90% of time used
                break;
            
            float iterationBestScore = float.MinValue;
            AIMove iterationBestMove = null;
            
            foreach (AIMove move in possibleMoves)
            {
                if (searchTimer.ElapsedMilliseconds > maxSearchTimeMs)
                    break;
                
                float score = MinimaxSearch(move, depth - 1, false, float.MinValue, float.MaxValue);
                move.score = score;
                
                if (score > iterationBestScore)
                {
                    iterationBestScore = score;
                    iterationBestMove = move;
                }
            }
            
            // Only update best move if we completed this depth
            if (searchTimer.ElapsedMilliseconds <= maxSearchTimeMs)
            {
                bestMove = iterationBestMove ?? bestMove;
                bestScore = iterationBestScore;
                depthReached = depth;
            }
            else
            {
                break;
            }
        }
        
        searchTimer.Stop();
        UnityEngine.Debug.Log($"Hard AI searched to depth {depthReached}, evaluated nodes, took {searchTimer.ElapsedMilliseconds}ms, score: {bestScore:F1}");
        
        // 10% chance to pick from top 3 for variety
        if (Random.value < 0.1f && possibleMoves.Count >= 3)
        {
            possibleMoves.Sort((a, b) => b.score.CompareTo(a.score));
            return possibleMoves[Random.Range(0, 3)];
        }
        
        return bestMove;
    }

    // ========== MINIMAX ALGORITHM WITH ALPHA-BETA PRUNING ========== //
    
    private float MinimaxSearch(AIMove move, int depth, bool isMaximizing, float alpha, float beta)
    {
        // Time check
        if (searchTimer.ElapsedMilliseconds > maxSearchTimeMs)
            return 0f;

        // Apply the move first, so every evaluation below reflects the resulting position
        // (the old code evaluated the leaf BEFORE applying the move, which hid its effect).
        BoardState state = SimulateMove(move);

        // Check for game over
        if (IsTerminalState(state))
        {
            float terminalScore = EvaluateTerminalState(state, depth);
            UndoMove(move, state);
            return terminalScore;
        }

        // Leaf: evaluate the resulting board statically (side-symmetric), not the move's deltas.
        if (depth == 0)
        {
            float leafScore = EvaluateLeaf(move);
            UndoMove(move, state);
            return leafScore;
        }

        string currentPlayer = isMaximizing ? aiPlayer : GetOpponent(aiPlayer);
        List<AIMove> nextMoves = GetAllPossibleMoves(currentPlayer);

        if (nextMoves.Count == 0)
        {
            float leafScore = EvaluateLeaf(move);
            UndoMove(move, state);
            return leafScore;
        }
        
        float bestValue;
        
        if (isMaximizing)
        {
            // AI's turn - maximize score
            bestValue = float.MinValue;
            foreach (AIMove nextMove in nextMoves)
            {
                if (searchTimer.ElapsedMilliseconds > maxSearchTimeMs)
                    break;
                
                float value = MinimaxSearch(nextMove, depth - 1, false, alpha, beta);
                bestValue = Mathf.Max(bestValue, value);
                alpha = Mathf.Max(alpha, value);
                
                if (beta <= alpha)
                    break; // Beta cutoff
            }
        }
        else
        {
            // Opponent's turn - minimize score
            bestValue = float.MaxValue;
            foreach (AIMove nextMove in nextMoves)
            {
                if (searchTimer.ElapsedMilliseconds > maxSearchTimeMs)
                    break;
                
                float value = MinimaxSearch(nextMove, depth - 1, true, alpha, beta);
                bestValue = Mathf.Min(bestValue, value);
                beta = Mathf.Min(beta, value);
                
                if (beta <= alpha)
                    break; // Alpha cutoff
            }
        }
        
        UndoMove(move, state);
        return bestValue;
    }

    // ========== BOARD STATE MANAGEMENT ========== //
    
    // ========== BOARD STATE MANAGEMENT ========== //

private class BoardState
{
    public GameObject capturedPiece;
    public int fromRing;
    public int fromPlace;
    public int toRing;
    public int toPlace;
    public Chessman movedPiece;
}

private BoardState SimulateMove(AIMove move)
{
    BoardState state = new BoardState();
    
    if (move.isRotation)
    {
        // Rotations are harder to simulate properly
        // For now, we'll give them a fixed evaluation
        return state;
    }
    
    state.movedPiece = move.piece;
    state.fromRing = move.fromRing;
    state.fromPlace = move.fromPlace;
    state.toRing = move.toRing;
    state.toPlace = move.toPlace;
    
    // Capture piece if present
    state.capturedPiece = gameController.GetPosition(move.toRing, move.toPlace);
    
    // Simulate move in game matrix
    gameController.SetPositionEmpty(state.fromRing, state.fromPlace);
    
    // Update piece position temporarily
    move.piece.SetRingNumber(move.toRing);
    move.piece.SetPlaceNumber(move.toPlace);
    
    // Set new position (pass the GameObject, not the Chessman)
    gameController.SetPosition(move.piece.gameObject);  // ← FIX: Use .gameObject
    
    return state;
}

private void UndoMove(AIMove move, BoardState state)
{
    if (move.isRotation)
        return;
    
    // Restore piece position
    move.piece.SetRingNumber(state.fromRing);
    move.piece.SetPlaceNumber(state.fromPlace);
    
    // Restore board matrix
    gameController.SetPositionEmpty(state.toRing, state.toPlace);
    gameController.SetPosition(move.piece.gameObject);  // ← FIX: Use .gameObject
    
    // Restore captured piece if any
    if (state.capturedPiece != null)
    {
        gameController.SetPosition(state.capturedPiece);  // Already a GameObject
    }
}

    // ========== EVALUATION FUNCTIONS ========== //
    
    private float EvaluateMove(AIMove move)
    {
        float score = 0f;
        
        if (move.isRotation)
        {
            return EvaluateRotation(move);
        }
        
        // Material evaluation
        score += EvaluateMaterial();
        
        // Positional evaluation
        score += EvaluatePosition(move);
        
        // Tactical evaluation
        score += EvaluateTactics(move);
        
        return score;
    }
    
    private float EvaluateMaterial()
    {
        float score = 0f;
        
        // Count material for both players
        Chessman[] allPieces = FindObjectsByType<Chessman>(FindObjectsInactive.Exclude);
        
        foreach (Chessman piece in allPieces)
        {
            float pieceValue = GetPieceValue(piece.pieceType);
            
            // Bonus for pieces in D ring
            if (piece.GetRingNumber() == 7)
            {
                if (piece.pieceType == "H" || piece.pieceType == "B")
                    pieceValue += weights.materialDRingCommander; // These score points!
                else
                    pieceValue += weights.materialDRingOther;
            }
            
            if (piece.player == aiPlayer)
                score += pieceValue;
            else
                score -= pieceValue;
        }
        
        return score;
    }

    // Static, side-symmetric board evaluation used at minimax leaves. Reads the position matrix
    // (which SimulateMove keeps current, including removed captures) so deep search correctly
    // values material, captures, and piece placement for BOTH players. This is what fixes the
    // AI ignoring free captures: the move-delta EvaluateMove only scored the leaf move from the
    // AI's own perspective, so internal captures and opponent threats were invisible.
    // Deepest ring (7 = center/D) reached by each side's commanders (H/B). Drives the strategy
    // shift in EvaluateBoard: it tells us who is winning the race to the center.
    private void CommanderDepths(out int aiDepth, out int oppDepth)
    {
        aiDepth = 0;
        oppDepth = 0;
        int[] sizes = { 48, 48, 24, 24, 12, 12, 4 };
        for (int ring = 1; ring <= 7; ring++)
        {
            for (int place = 1; place <= sizes[ring - 1]; place++)
            {
                GameObject obj = gameController.GetPosition(ring, place);
                if (obj == null) continue;
                Chessman cm = obj.GetComponent<Chessman>();
                if (cm == null || (cm.pieceType != "H" && cm.pieceType != "B")) continue;

                if (cm.player == aiPlayer) { if (ring > aiDepth) aiDepth = ring; }
                else { if (ring > oppDepth) oppDepth = ring; }
            }
        }
    }

    private float EvaluateBoard()
    {
        float score = 0f;
        int[] sizes = { 48, 48, 24, 24, 12, 12, 4 };

        // Decide the AI's stance for this position. When the opponent has a commander deep enough
        // to be threatening, the AI commits: if it is ahead or tied in the race to the center it
        // turns AGGRESSIVE (push its own commander in to win first); if it is behind it turns
        // DEFENSIVE (the proximity penalty below is amplified so stopping the rush dominates).
        CommanderDepths(out int aiDepth, out int oppDepth);
        bool underThreat = oppDepth >= (int)weights.threatTriggerRing;
        bool aggressive = aiDepth >= oppDepth;

        for (int ring = 1; ring <= 7; ring++)
        {
            for (int place = 1; place <= sizes[ring - 1]; place++)
            {
                GameObject obj = gameController.GetPosition(ring, place);
                if (obj == null) continue;

                Chessman cm = obj.GetComponent<Chessman>();
                if (cm == null) continue;

                float v = GetPieceValue(cm.pieceType);

                // Occupying the D ring (ring 7)
                if (ring == 7)
                    v += (cm.pieceType == "H" || cm.pieceType == "B")
                        ? weights.materialDRingCommander
                        : weights.materialDRingOther;

                // Central control (rings 5-6)
                if (ring >= 5 && ring < 7)
                    v += weights.centralControl;

                // Mild gradient rewarding advancement toward the center
                v += weights.advanceTowardCenter * ring;

                bool isCommander = cm.pieceType == "H" || cm.pieceType == "B";

                // Extra, asymmetric penalty for the OPPONENT's commanders (H/B) nearing the center.
                // Graded by ring (7 = center) so the closer they get, the worse this scores. Since an
                // opponent piece's value is subtracted below, adding to v here deepens that penalty,
                // pushing the AI to contest a Hand/Mind rush before it reaches the D ring -- even
                // when the win is beyond the minimax horizon. In DEFENSIVE mode this is amplified so
                // blocking/capturing the threat dominates the AI's choice.
                if (isCommander && cm.player != aiPlayer)
                {
                    float prox = weights.opponentCommanderProximity * ring;
                    if (underThreat && !aggressive)
                        prox *= weights.defensiveMultiplier;
                    v += prox;
                }

                // AGGRESSIVE mode: when threatened but ahead/tied in the race, reward pushing OUR
                // own commander inward so the AI tries to reach the center first instead of trading
                // tempo on defense.
                if (isCommander && cm.player == aiPlayer && underThreat && aggressive)
                    v += weights.aggressiveAdvance * ring;

                score += (cm.player == aiPlayer) ? v : -v;
            }
        }

        return score;
    }

    // Leaf evaluation for minimax: the static board, plus the rotation incentive for rotation
    // moves (SimulateMove does not model ring rotations, so EvaluateBoard can't see their effect).
    private float EvaluateLeaf(AIMove move)
    {
        float score = EvaluateBoard();
        if (move.isRotation)
            score += EvaluateRotation(move);
        return score;
    }

    private float EvaluatePosition(AIMove move)
    {
        float score = 0f;
        
        // Reward moving toward center
        if (move.toRing > move.fromRing)
            score += (move.toRing - move.fromRing) * weights.advanceTowardCenter;

        // Huge bonus for getting scoring pieces to D ring
        if (move.toRing == 7)
        {
            if (move.piece.pieceType == "H" || move.piece.pieceType == "B")
                score += weights.arriveDCommander;
            else if (move.piece.pieceType == "T" || move.piece.pieceType == "W")
                score += weights.arriveDEnabler;
            else
                score += weights.arriveDOther;
        }

        // Penalty for moving valuable pieces out of D ring
        if (move.fromRing == 7 && (move.piece.pieceType == "H" || move.piece.pieceType == "B"))
        {
            score -= weights.leaveDCommanderPenalty;
        }

        // Reward central control (rings 5-6)
        if (move.toRing >= 5 && move.toRing < 7)
        {
            score += weights.centralControl;
        }

        // Advance enablers
        if ((move.piece.pieceType == "T" || move.piece.pieceType == "W") && move.toRing > move.fromRing)
        {
            score += weights.advanceEnabler;
        }
        
        return score;
    }
    
    private float EvaluateTactics(AIMove move)
    {
        float score = 0f;
        
        // Check if this is a capture
        GameObject targetPiece = gameController.GetPosition(move.toRing, move.toPlace);
        if (targetPiece != null)
        {
            Chessman target = targetPiece.GetComponent<Chessman>();
            if (target != null && target.player != aiPlayer)
            {
                // Reward capturing opponent pieces
                score += GetPieceValue(target.pieceType) * weights.captureMultiplier;

                // Extra bonus for capturing pieces in D ring
                if (target.GetRingNumber() == 7)
                {
                    if (target.pieceType == "H" || target.pieceType == "B")
                        score += weights.captureDCommander; // Prevent opponent from scoring!
                    else
                        score += weights.captureDOther;
                }

                // Decisive: capturing the opponent's LAST Hand/Mind ends the game (they lose).
                if ((target.pieceType == "H" || target.pieceType == "B") &&
                    CountCommanders(target.player) <= 1)
                {
                    score += weights.captureLastCommander;
                }
            }
        }

        // Check opponent threats to D ring
        score += CheckOpponentThreats();

        return score;
    }
    
    private float EvaluateRotation(AIMove move)
    {
        float score = weights.rotationBase; // Base value

        // Sage rotations are more valuable
        if (move.piece.pieceType == "W")
        {
            score += weights.rotationSage;
        }
        else if (move.piece.pieceType == "S")
        {
            score += weights.rotationScholar;
        }

        return score;
    }
    
    private float CheckOpponentThreats()
    {
        float threat = 0f;
        string opponent = GetOpponent(aiPlayer);
        
        // Check if opponent has pieces close to D ring
        for (int p = 1; p <= 12; p++)
        {
            GameObject piece = gameController.GetPosition(6, p);
            if (piece != null)
            {
                Chessman cm = piece.GetComponent<Chessman>();
                if (cm != null && cm.player == opponent)
                {
                    if (cm.pieceType == "H" || cm.pieceType == "B")
                        threat += weights.threatCommander;
                    else if (cm.pieceType == "T" || cm.pieceType == "W")
                        threat += weights.threatEnabler;
                }
            }
        }
        
        return threat;
    }
    
    private float GetPieceValue(string pieceType)
    {
        switch (pieceType)
        {
            case "H": return weights.valueHand;       // Hand
            case "B": return weights.valueMind;       // Mind
            case "T": return weights.valueTech;       // Technician
            case "W": return weights.valueSage;       // Sage
            case "S": return weights.valueScholar;    // Scholar
            case "P": return weights.valueSkill;      // Skill
            case "A": return weights.valueApprentice; // Apprentice
            default:  return weights.valueDefault;
        }
    }
    
    private bool IsTerminalState(BoardState state)
    {
        // Detect terminal conditions on the SIMULATED board, not the real game flag.
        // gameController.IsGameOver() only reflects moves actually played, so during minimax it
        // stays false even when a simulated line wins or loses. Reading the matrix (which
        // SimulateMove keeps current) lets the search see, e.g., the human rushing Hand+Mind into
        // the D ring and react to it.
        if (CountCommanders(aiPlayer) == 0) return true;
        if (CountCommanders(GetOpponent(aiPlayer)) == 0) return true;
        if (CountPointsInD(aiPlayer) >= 2.0f) return true;
        if (CountPointsInD(GetOpponent(aiPlayer)) >= 2.0f) return true;
        return false;
    }
    
    private float EvaluateTerminalState(BoardState state, int depth)
    {
        // Depth-aware terminal scoring. `depth` is the search depth REMAINING at this node, so a
        // terminal found with more depth left was reached in fewer moves from the root. Adding it
        // makes the AI prefer faster wins and, crucially, delay losses as long as possible — a
        // delayed loss means it found a move that blocks the opponent's winning line (e.g. a
        // Hand+Mind center rush) instead of being indifferent to losing now vs. later.
        float win = 10000f + depth;
        float loss = -10000f - depth;

        // Decisive win condition: a player who has lost both Hand and Mind loses.
        if (CountCommanders(GetOpponent(aiPlayer)) == 0)
            return win;  // Opponent has no commanders left -> we won!
        if (CountCommanders(aiPlayer) == 0)
            return loss; // We have no commanders left -> we lost!

        // Check who won by counting points in D ring
        float aiPoints = CountPointsInD(aiPlayer);
        float opponentPoints = CountPointsInD(GetOpponent(aiPlayer));

        if (aiPoints >= 2.0f)
            return win; // We won!
        else if (opponentPoints >= 2.0f)
            return loss; // We lost!

        return 0f;
    }

    // Counts how many Hand (H) / Mind (B) pieces a player still has on the board.
    // Reads the live position matrix, which SimulateMove keeps up to date during search.
    private int CountCommanders(string player)
    {
        int count = 0;
        int[] sizes = { 48, 48, 24, 24, 12, 12, 4 };
        for (int ring = 1; ring <= 7; ring++)
        {
            for (int place = 1; place <= sizes[ring - 1]; place++)
            {
                GameObject piece = gameController.GetPosition(ring, place);
                if (piece == null) continue;

                Chessman cm = piece.GetComponent<Chessman>();
                if (cm != null && cm.player == player &&
                    (cm.pieceType == "H" || cm.pieceType == "B"))
                {
                    count++;
                }
            }
        }
        return count;
    }
    
    private float CountPointsInD(string player)
    {
        float points = 0f;
        
        for (int i = 1; i <= 4; i++)
        {
            GameObject piece = gameController.GetPosition(7, i);
            if (piece != null)
            {
                Chessman cm = piece.GetComponent<Chessman>();
                if (cm != null && cm.player == player)
                {
                    if (cm.pieceType == "H" || cm.pieceType == "B")
                        points += 1f;
                    else if (cm.pieceType == "P")
                        points += 0.33f;
                }
            }
        }
        
        return points;
    }

    // ========== UTILITY FUNCTIONS ========== //
    
    private string GetOpponent(string player)
    {
        return player == "orange" ? "green" : "orange";
    }

    private List<AIMove> GetAllPossibleMoves(string player)
    {
        List<AIMove> moves = new List<AIMove>();
        
        // Find all pieces for this player
        Chessman[] allPieces = FindObjectsByType<Chessman>(FindObjectsInactive.Exclude);
        
        foreach (Chessman piece in allPieces)
        {
            if (piece.player != player)
                continue;
            
            int fromRing = piece.GetRingNumber();
            int fromPlace = piece.GetPlaceNumber();
            
            // Get valid moves for this piece
            List<Vector2Int> validDestinations = GetValidMovesForPiece(piece);
            
            foreach (Vector2Int dest in validDestinations)
            {
                AIMove move = new AIMove
                {
                    piece = piece,
                    fromRing = fromRing,
                    fromPlace = fromPlace,
                    toRing = dest.x,
                    toPlace = dest.y,
                    isRotation = false
                };
                
                moves.Add(move);
            }
            
            // Check for rotation moves (Scholar and Sage only)
            if (piece.pieceType == "S" || piece.pieceType == "W")
            {
                int ringPair = GetRingPair(fromRing);
                
                // ACW rotation
                AIMove rotateACW = new AIMove
                {
                    piece = piece,
                    fromRing = fromRing,
                    fromPlace = fromPlace,
                    ringPair = ringPair,
                    clockwise = false,
                    isRotation = true
                };
                moves.Add(rotateACW);
                
                // CW rotation
                AIMove rotateCW = new AIMove
                {
                    piece = piece,
                    fromRing = fromRing,
                    fromPlace = fromPlace,
                    ringPair = ringPair,
                    clockwise = true,
                    isRotation = true
                };
                moves.Add(rotateCW);
            }
        }
        
        return moves;
    }

    // Destroys all MovePlate objects synchronously. Unity's Destroy() is deferred to end of
    // frame, but move generation runs for many pieces within a single frame; deferred plates
    // from one piece would leak into the next piece's FindGameObjectsWithTag results and
    // produce illegal moves. DestroyImmediate keeps each generation isolated.
    private void ClearAllMovePlates()
    {
        GameObject[] plates = GameObject.FindGameObjectsWithTag("MovePlate");
        foreach (GameObject p in plates)
        {
            if (p != null) DestroyImmediate(p);
        }
    }

    private List<Vector2Int> GetValidMovesForPiece(Chessman piece)
    {
        List<Vector2Int> validMoves = new List<Vector2Int>();

        // Start from a clean slate so stale plates can't leak into this piece's results.
        ClearAllMovePlates();

        // Temporarily generate move plates to see valid moves
        piece.InitiateMovePlates();

        // Find all move plates
        GameObject[] movePlates = GameObject.FindGameObjectsWithTag("MovePlate");

        foreach (GameObject plateObj in movePlates)
        {
            MovePlate plate = plateObj.GetComponent<MovePlate>();
            if (plate != null)
            {
                validMoves.Add(new Vector2Int(plate.GetRingNumber(), plate.GetPlaceNumber()));
            }
        }

        // Remove synchronously so the next piece starts clean within the same frame.
        ClearAllMovePlates();

        return validMoves;
    }

    public void ExecuteMove(AIMove move)
    {
        if (move.isRotation)
        {
            // Execute rotation
            gameController.RotateRing(move.ringPair, move.clockwise);
        }
        else
        {
            // Clear any stale plates first so this piece's plates are the only ones found.
            // Unity's Destroy() is deferred to end-of-frame, so leftover plates from prior
            // generation would otherwise be picked up here and cause illegal moves.
            ClearAllMovePlates();

            // Execute piece move
            move.piece.InitiateMovePlates();

            GameObject[] movePlates = GameObject.FindGameObjectsWithTag("MovePlate");
            foreach (GameObject plateObj in movePlates)
            {
                MovePlate plate = plateObj.GetComponent<MovePlate>();
                if (plate != null &&
                    plate.GetRingNumber() == move.toRing &&
                    plate.GetPlaceNumber() == move.toPlace)
                {
                    // Simulate clicking the move plate
                    plate.OnMouseUp();
                    break;
                }
            }

            // Clean up any remaining move plates immediately
            ClearAllMovePlates();
        }
    }

    private int GetRingPair(int ringNumber)
    {
        if (ringNumber <= 2) return 1;
        else if (ringNumber <= 4) return 2;
        else if (ringNumber <= 6) return 3;
        else return 4;
    }

    // Public method to change difficulty during gameplay
    public void SetDifficulty(DifficultyLevel newDifficulty)
    {
        difficulty = newDifficulty;
        UnityEngine.Debug.Log($"AI difficulty set to: {difficulty}");
    }

    // ========== SELF-PLAY TRAINING SUPPORT ========== //
    // These let SelfPlayTrainer drive both sides synchronously with arbitrary weight sets.

    public AIWeights GetWeights() { return weights; }
    public void SetWeights(AIWeights w) { if (w != null) weights = w; }

    // Picks the best move for `player` using a depth-1 (greedy) evaluation under weight set `w`.
    // `noise` adds bounded random jitter to scores so repeated self-play games diverge (exploration).
    public AIMove PickGreedyMove(string player, AIWeights w, float noise, System.Random rng)
    {
        string savedPlayer = aiPlayer;
        AIWeights savedWeights = weights;

        aiPlayer = player;   // evaluate from this side's perspective
        weights = w;

        try
        {
            List<AIMove> moves = GetAllPossibleMoves(player);
            if (moves.Count == 0) return null;

            AIMove best = null;
            float bestScore = float.MinValue;

            foreach (AIMove m in moves)
            {
                float s = EvaluateMove(m);
                if (noise > 0f && rng != null)
                    s += (float)(rng.NextDouble() * 2.0 - 1.0) * noise;

                if (s > bestScore)
                {
                    bestScore = s;
                    best = m;
                }
            }

            return best;
        }
        finally
        {
            aiPlayer = savedPlayer;
            weights = savedWeights;
        }
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
        else
            return $"Move {piece.pieceType} from ({fromRing},{fromPlace}) to ({toRing},{toPlace}) (score: {score:F1})";
    }
}

// ========== AI EVALUATION WEIGHTS ========== //
// Every tunable coefficient the evaluation uses. Defaults reproduce the original
// hand-tuned behavior, so the AI is unchanged until a self-play-trained file is loaded.
// Serializable with public fields so JsonUtility can round-trip it to/from disk.
[System.Serializable]
public class AIWeights
{
    // Piece base values (H/B and T/W start equal but can diverge through training)
    public float valueHand = 50f;        // H
    public float valueMind = 50f;        // B
    public float valueTech = 30f;        // T
    public float valueSage = 30f;        // W
    public float valueScholar = 15f;     // S
    public float valueSkill = 10f;       // P
    public float valueApprentice = 8f;   // A
    public float valueDefault = 5f;

    // Material: bonus for occupying the D ring (ring 7)
    public float materialDRingCommander = 100f; // H/B in D
    public float materialDRingOther = 10f;

    // Positional
    public float advanceTowardCenter = 5f;      // per ring closer to center
    public float arriveDCommander = 150f;       // H/B reaching D
    public float arriveDEnabler = 30f;          // T/W reaching D
    public float arriveDOther = 10f;            // anything else reaching D
    public float leaveDCommanderPenalty = 100f; // H/B leaving D (subtracted)
    public float centralControl = 8f;           // occupying rings 5-6
    public float advanceEnabler = 15f;          // T/W advancing inward

    // Tactical
    public float captureMultiplier = 2f;        // * captured piece value
    public float captureDCommander = 120f;      // capturing H/B sitting in D
    public float captureDOther = 20f;           // capturing anything else in D
    public float captureLastCommander = 500f;   // capturing opponent's LAST H/B (decisive)
    public float threatCommander = 15f;         // opponent H/B poised on ring 6
    public float threatEnabler = 8f;            // opponent T/W poised on ring 6
    public float opponentCommanderProximity = 3f; // per-ring penalty as an opponent H/B nears center

    // Strategy shift: when an opponent commander reaches threatTriggerRing or deeper, the AI
    // switches mode based on the center race. Ahead/tied -> aggressive (race our own commander in);
    // behind -> defensive (multiply the proximity penalty so blocking/capturing dominates).
    public float threatTriggerRing = 5f;        // opponent commander at >= this ring arms the shift
    public float aggressiveAdvance = 6f;        // per-ring bonus for OUR commander racing in (aggressive)
    public float defensiveMultiplier = 2.5f;    // scales the proximity penalty when behind (defensive)

    // Ring rotation moves
    public float rotationBase = 5f;
    public float rotationSage = 8f;             // W rotations
    public float rotationScholar = 5f;          // S rotations

    public static AIWeights Default()
    {
        return new AIWeights();
    }

    public AIWeights Clone()
    {
        return (AIWeights)MemberwiseClone();
    }
}