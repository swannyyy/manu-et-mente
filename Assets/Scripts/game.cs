using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; 
using UnityEngine.SceneManagement;
// Ring sprite references

public class game : MonoBehaviour
{
    private bool isMLTraining = false;
    public bool IsTraining => isMLTraining;

    public GameObject VictoryPanel;
    public TMPro.TextMeshProUGUI VictoryText;
    public Button PlayAgainBTN;
    public Button MainMenuBTN;
    public GameObject ring1; // Aa
    public GameObject ring2; // Bb
    public GameObject ring3; // Cc
    public GameObject ring4; // D
    public GameObject piecePrefab;

    public Button BackToMenuBTN;

    public void BackToMainMenu()
    {
        if (GameUX.Instance != null) GameUX.Instance.ShowMenu();
        else SceneManager.LoadScene("MainMenu");
    }


    // Position matrix: [ring-1][place] = GameObject
    private GameObject[][] positions = new GameObject[7][];
    
    // Player pieces
    private GameObject[] orangePlayer = new GameObject[16];
    private GameObject[] greenPlayer = new GameObject[16];

    private string currentPlayer = "orange";
    private bool gameOver = false;
    private string winner = null; // set when the game ends; read by SelfPlayTrainer

    // Last move info for en passant
    public class MoveInfo
    {
        public string player;
        public string pieceType;
        public int fromRing;
        public int fromPlace;
        public int toRing;
        public int toPlace;
    }

    private MoveInfo lastMove = null;

    // ---- PvC game recording (notation -> dated local text file) ----
    // One token per half-move, in turn order (orange, green, orange, ...). Written to disk in
    // Winner(). Only populated for player-vs-computer games (an AIAgent is present, not training).
    private readonly List<string> notationLog = new List<string>();
    private AIAgent cachedAI;
    private static readonly string[] RING_LETTERS = { "A", "a", "B", "b", "C", "c", "D" };

    // Ring sizes
    private static readonly int[] RING_SIZES = { 48, 48, 24, 24, 12, 12, 4 };

    void Start()
    {
        // A PvC game has an AIAgent in the scene; PvP scenes do not. Recording is gated on this.
        cachedAI = FindAnyObjectByType<AIAgent>();
        notationLog.Clear();

        // Initialize position matrix
        for (int i = 0; i < 7; i++)
        {
            positions[i] = new GameObject[RING_SIZES[i]];
        }

        // Create pieces - Orange player (starting positions as per your design)
        orangePlayer = new GameObject[] {
            Create("app_o", "A", 1, 27, "orange"),    // A27
            Create("schol_o", "S", 1, 28, "orange"),  // A28
            Create("wise_o", "W", 1, 29, "orange"),   // A29
            Create("mind_o", "B", 1, 30, "orange"),   // A30
            Create("hand_o", "H", 1, 31, "orange"),   // A31
            Create("tech_o", "T", 1, 32, "orange"),   // A32
            Create("schol_o", "S", 1, 33, "orange"),  // A33
            Create("app_o", "A", 1, 34, "orange"),    // A34
            Create("skill_o", "P", 2, 27, "orange"),  // a27
            Create("skill_o", "P", 2, 28, "orange"),  // a28
            Create("skill_o", "P", 2, 29, "orange"),  // a29
            Create("skill_o", "P", 2, 30, "orange"),  // a30
            Create("skill_o", "P", 2, 31, "orange"),  // a31
            Create("skill_o", "P", 2, 32, "orange"),  // a32
            Create("skill_o", "P", 2, 33, "orange"),  // a33
            Create("skill_o", "P", 2, 34, "orange")   // a34
        };

        // Create pieces - Green player
        greenPlayer = new GameObject[] {
            Create("app_g", "A", 1, 3, "green"),      // A3
            Create("schol_g", "S", 1, 4, "green"),    // A4
            Create("wise_g", "W", 1, 5, "green"),     // A5
            Create("mind_g", "B", 1, 6, "green"),     // A6
            Create("hand_g", "H", 1, 7, "green"),     // A7
            Create("tech_g", "T", 1, 8, "green"),     // A8
            Create("schol_g", "S", 1, 9, "green"),    // A9
            Create("app_g", "A", 1, 10, "green"),     // A10
            Create("skill_g", "P", 2, 3, "green"),    // a3
            Create("skill_g", "P", 2, 4, "green"),    // a4
            Create("skill_g", "P", 2, 5, "green"),    // a5
            Create("skill_g", "P", 2, 6, "green"),    // a6
            Create("skill_g", "P", 2, 7, "green"),    // a7
            Create("skill_g", "P", 2, 8, "green"),    // a8
            Create("skill_g", "P", 2, 9, "green"),    // a9
            Create("skill_g", "P", 2, 10, "green")    // a10
        };

        // Set all pieces on the board
        for (int i = 0; i < orangePlayer.Length; i++)
        {
            SetPosition(orangePlayer[i]);
            SetPosition(greenPlayer[i]);
        }

        if (BackToMenuBTN != null)
        {
            BackToMenuBTN.onClick.AddListener(BackToMainMenu);
        }
         // Setup victory buttons
        if (PlayAgainBTN != null)
        {
            PlayAgainBTN.onClick.AddListener(PlayAgain);
        }
    
    
        if (MainMenuBTN != null)
            {
                MainMenuBTN.onClick.AddListener(VictoryBackToMenu);
            }
    
    // Make sure victory panel is hidden at start
    if (VictoryPanel != null)
    {
        VictoryPanel.SetActive(false);
    }
    
    }

    public void PreparePractice()
    {
        if (GameUX.Instance == null || !GameUX.Instance.Learning) return;
        GameUX.Instance.Deselect();
        for (int r=1;r<=7;r++) for(int p=1;p<=RING_SIZES[r-1];p++)
        {
            var obj=GetPosition(r,p);
            if(obj!=null){obj.SetActive(false);Destroy(obj);}
            SetPositionEmpty(r,p);
        }
        currentPlayer="orange";gameOver=false;winner=null;lastMove=null;
        if(VictoryPanel!=null)VictoryPanel.SetActive(false);
    }

    public GameObject Create(string name, string pieceType, int ring, int place, string player)
    {
        GameObject obj = Instantiate(piecePrefab, new Vector3(0, 0, -1), Quaternion.identity);
        Chessman cm = obj.GetComponent<Chessman>();
        cm.name = name;
        cm.pieceType = pieceType;
        cm.SetRingNumber(ring);
        cm.SetPlaceNumber(place);
        cm.player = player;
        cm.Activate();
        return obj;
    }

    // Guarantees the position matrix and every per-ring array exist before they are indexed.
    // positions[] is allocated in Start(), but a Chessman's OnMouseUp can fire (or move
    // generation can run) before/around that init, and an unallocated inner array would
    // otherwise NullReference in GetPosition/SetPosition. There is no valid state where an
    // inner array should be null, so ensuring allocation here is always safe.
    private void EnsurePositionsInitialized()
    {
        if (positions == null)
            positions = new GameObject[7][];

        for (int i = 0; i < 7; i++)
        {
            if (positions[i] == null)
                positions[i] = new GameObject[RING_SIZES[i]];
        }
    }

   public void SetPosition(GameObject obj)
{
    EnsurePositionsInitialized();
    Chessman cm = obj.GetComponent<Chessman>();
    int ring = cm.GetRingNumber();
    int place = cm.GetPlaceNumber();
    
    // Validate
    if (ring < 1 || ring > 7)
    {
        Debug.LogError($"SetPosition: invalid ring {ring}");
        return;
    }
    
    int ringSize = RING_SIZES[ring - 1];
    if (place < 1 || place > ringSize)
    {
        Debug.LogError($"SetPosition: invalid place {place} for ring {ring} (size {ringSize})");
        return;
    }
    
    // Convert 1-indexed place to 0-indexed for array access
    positions[ring - 1][place - 1] = obj;
}

    public void SetPositionEmpty(int ring, int place)
{
    EnsurePositionsInitialized();
    // ring and place are 1-indexed
    if (ring < 1 || ring > 7) return;
    
    int ringSize = RING_SIZES[ring - 1];
    if (place < 1 || place > ringSize) return;
    
    // Convert to 0-indexed for array access
    positions[ring - 1][place - 1] = null;
}

    public GameObject GetPosition(int ring, int place)
{
    EnsurePositionsInitialized();
    // ring and place are 1-indexed
    if (ring < 1 || ring > 7) return null;
    
    int ringSize = RING_SIZES[ring - 1];
    if (place < 1 || place > ringSize) return null;
    
    // Convert to 0-indexed for array access
    return positions[ring - 1][place - 1];
}



    public bool PositionOnBoard(int ring, int place)
    {
        // ring and place are 1-indexed
        if (ring < 1 || ring > 7) return false;

        int ringSize = RING_SIZES[ring - 1];
        if (place < 1 || place > ringSize) return false;

        return true;
    }

    public string GetCurrentPlayer()
    {
        return currentPlayer;
    }

    public bool IsGameOver()
    {
        return gameOver;
    }

    public void NextTurn()
    {
        currentPlayer = GameUX.Instance != null && GameUX.Instance.Learning ? "orange" : (currentPlayer == "orange" ? "green" : "orange");
        CheckVictory();
    }

    public void RecordMove(string player, string pieceType, int fromRing, int fromPlace, int toRing, int toPlace)
    {
        lastMove = new MoveInfo
        {
            player = player,
            pieceType = pieceType,
            fromRing = fromRing,
            fromPlace = fromPlace,
            toRing = toRing,
            toPlace = toPlace
        };
        GameUX.Instance?.Record(lastMove);
    }

    public MoveInfo GetLastMove()
    {
        return lastMove;
    }

    // ---- PvC game recording ----

    private bool ShouldRecord()
    {
        return !isMLTraining && cachedAI != null;
    }

    private static string SquareName(int ring, int place)
    {
        if (ring < 1 || ring > 7) return $"?{ring}.{place}";
        return RING_LETTERS[ring - 1] + place;
    }

    // Internal "B" is the Mind; on paper it is written "M". Every other code matches its symbol.
    private static string PieceSymbol(string pieceType)
    {
        return pieceType == "B" ? "M" : pieceType;
    }

    // Records a piece move/capture in notation. Called from MovePlate, which knows whether the move
    // was a capture and whether it was en passant. Long form (origin + destination) for clarity.
    public void LogPieceMove(string player, string pieceType, int fromRing, int fromPlace,
                             int toRing, int toPlace, bool capture, bool enPassant)
    {
        if (!ShouldRecord()) return;
        string token = PieceSymbol(pieceType) + SquareName(fromRing, fromPlace)
                     + (capture ? "x" : "-") + SquareName(toRing, toPlace)
                     + (enPassant ? " e.p." : "");
        notationLog.Add(token);
    }

    // Records a ring-pair rotation. The mover is implied by turn order, so the token is just the
    // ring pair plus a direction arrow, matching MOVE_NOTATION.md (e.g. "Cc↻").
    public void LogRotation(int ringPair, bool clockwise)
    {
        if (!ShouldRecord()) return;
        string[] pairs = { "Aa", "Bb", "Cc", "D" };
        string pair = (ringPair >= 1 && ringPair <= 4) ? pairs[ringPair - 1] : "?";
        notationLog.Add(pair + (clockwise ? "↻" : "↺"));
    }

    private void WriteGameRecord(string winnerPlayer)
    {
        try
        {
            string folder = AIAgent.GameLogsFolder();
            System.IO.Directory.CreateDirectory(folder);

            System.DateTime now = System.DateTime.Now;
            string file = System.IO.Path.Combine(folder, now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt");

            // If the loser has no commander left, the game was decided by commander elimination;
            // otherwise the winner reached 2 points in the D ring.
            string loser = winnerPlayer == "orange" ? "green" : "orange";
            string decidedBy = !PlayerHasCommander(loser) ? "commanders" : "dring";
            string aiSide = cachedAI != null ? cachedAI.GetAIPlayer() : "unknown";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Manu et Mente — game record");
            sb.AppendLine("Date: " + now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            sb.AppendLine("       ORANGE          GREEN");
            for (int i = 0; i < notationLog.Count; i += 2)
            {
                int turn = i / 2 + 1;
                string orange = notationLog[i];
                string green = (i + 1 < notationLog.Count) ? notationLog[i + 1] : "";
                sb.AppendLine($"{turn,4}. {orange,-15} {green}");
            }
            sb.AppendLine();
            sb.AppendLine("Result: " + winnerPlayer);
            sb.AppendLine("Decided-by: " + decidedBy);
            sb.AppendLine("AI-side: " + aiSide);

            System.IO.File.WriteAllText(file, sb.ToString());
            Debug.Log($"Game recorded to {file}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to write game record: {e.Message}");
        }
    }

    // The five ways to win, ordered by the points they award (see VictoryPoints). Used to drive
    // online ranking; offline games just end. Draw exists for completeness but is never auto-detected
    // (the game has no stalemate rule) — it's reserved for a future agreed-draw flow.
    public enum VictoryType
    {
        None = 0,
        CommandersTaken,        // opponent lost both Hand and Mind            +40
        HandAndMindInD,         // own Hand AND Mind both in the D ring        +30
        HandPlus3PawnsInD,      // own Hand + 3 pawns in the D ring            +20
        MindPlus3PawnsInD,      // own Mind + 3 pawns in the D ring            +20
        CommanderPlus4PawnsCc,  // own Hand or Mind in D + 4 pawns in C/c      +10
        Draw                    //                                              0
    }

    public static int VictoryPoints(VictoryType t)
    {
        switch (t)
        {
            case VictoryType.CommandersTaken:       return 40;
            case VictoryType.HandAndMindInD:        return 30;
            case VictoryType.HandPlus3PawnsInD:     return 20;
            case VictoryType.MindPlus3PawnsInD:     return 20;
            case VictoryType.CommanderPlus4PawnsCc: return 10;
            default:                                return 0;
        }
    }

    public VictoryType LastVictoryType { get; private set; } = VictoryType.None;

    private void CheckVictory()
    {
        Debug.Log("Checking victory condition...");

        // Type 5 (decisive loss, +40): a player who lost BOTH their Hand and Mind loses. Applies to
        // PvP and vs-AI alike since every move routes through NextTurn -> CheckVictory.
        if (!PlayerHasCommander("orange"))
        {
            Debug.Log("Orange lost both Hand and Mind. Green wins!");
            Winner("green", VictoryType.CommandersTaken);
            return;
        }
        if (!PlayerHasCommander("green"))
        {
            Debug.Log("Green lost both Hand and Mind. Orange wins!");
            Winner("orange", VictoryType.CommandersTaken);
            return;
        }

        // Types 1-4: positional wins decided by what's parked in the inner rings.
        VictoryType orangeType = ClassifyPositionalVictory("orange");
        if (orangeType != VictoryType.None)
        {
            Debug.Log($"Orange wins by {orangeType}!");
            Winner("orange", orangeType);
            return;
        }

        VictoryType greenType = ClassifyPositionalVictory("green");
        if (greenType != VictoryType.None)
        {
            Debug.Log($"Green wins by {greenType}!");
            Winner("green", greenType);
            return;
        }

        Debug.Log("No winner yet.");
    }

    // Detects victory Types 1-4 for one player from the board. D ring is ring 7 (4 cells); the C/c
    // ring is rings 5 and 6. Returns the highest-value type that applies, or None.
    private VictoryType ClassifyPositionalVictory(string player)
    {
        bool handInD = false, mindInD = false;
        int pawnsInD = 0;
        for (int i = 1; i <= RING_SIZES[6]; i++) // D ring = ring 7
        {
            GameObject piece = GetPosition(7, i);
            if (piece == null) continue;
            Chessman cm = piece.GetComponent<Chessman>();
            if (cm == null || cm.player != player) continue;
            if (cm.pieceType == "H") handInD = true;
            else if (cm.pieceType == "B") mindInD = true;
            else if (cm.pieceType == "P") pawnsInD++;
        }

        int pawnsInCc = 0;
        for (int ring = 5; ring <= 6; ring++) // C and c
        {
            int size = RING_SIZES[ring - 1];
            for (int place = 1; place <= size; place++)
            {
                GameObject piece = GetPosition(ring, place);
                if (piece == null) continue;
                Chessman cm = piece.GetComponent<Chessman>();
                if (cm != null && cm.player == player && cm.pieceType == "P") pawnsInCc++;
            }
        }

        if (handInD && mindInD) return VictoryType.HandAndMindInD;                       // +30
        if (handInD && pawnsInD >= 3) return VictoryType.HandPlus3PawnsInD;              // +20
        if (mindInD && pawnsInD >= 3) return VictoryType.MindPlus3PawnsInD;              // +20
        if ((handInD || mindInD) && pawnsInCc >= 4) return VictoryType.CommanderPlus4PawnsCc; // +10
        return VictoryType.None;
    }
public void SetMLTrainingMode(bool training)
{
    isMLTraining = training;
}


    // Returns true if the player still has at least one Hand (H) or Mind (B) on the board.
    private bool PlayerHasCommander(string player)
    {
        for (int ring = 1; ring <= 7; ring++)
        {
            int ringSize = RING_SIZES[ring - 1];
            for (int place = 1; place <= ringSize; place++)
            {
                GameObject piece = GetPosition(ring, place);
                if (piece == null) continue;

                Chessman cm = piece.GetComponent<Chessman>();
                if (cm != null && cm.player == player &&
                    (cm.pieceType == "H" || cm.pieceType == "B"))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private float CountPointsInD(string player)
    {
        float points = 0f;

        Debug.Log($"=== Counting points in D ring for {player} ===");

        // D ring has 4 cells (positions 1-4)
        for (int i = 1; i <= 4; i++)
        {
            GameObject piece = GetPosition(7, i);
            if (piece != null)
            {
                Chessman cm = piece.GetComponent<Chessman>();
                if (cm != null)
                {
                    Debug.Log($"D ring position {i}: {cm.pieceType} ({cm.player})");

                    if (cm.player == player)
                    {
                        switch (cm.pieceType)
                        {
                            case "H": // Hand
                            case "B": // Mind
                                points += 1.0f;
                                Debug.Log($"  Added 1 point for {cm.pieceType}, total: {points}");
                                break;
                            case "P": // Pawn
                                points += 0.34f;
                                Debug.Log($"  Added 0.33 point for Pawn, total: {points}");
                                break;
                            // Other pieces worth 0 points
                            default:
                                Debug.Log($"  {cm.pieceType} worth 0 points");
                                break;
                        }
                    }
                }
            }
            else
            {
                Debug.Log($"D ring position {i}: empty");
            }
        }

        Debug.Log($"=== Total points for {player}: {points} ===");
        return points;
    }
    public void PlayAgain()
    {
        // Reload the current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }


    public void VictoryBackToMenu()
    {
        NetworkController.GoOffline();
        SceneManager.LoadScene("MainMenu");
    }

    public string GetWinner()
    {
        return winner;
    }

    public void Winner(string playerWinner)
    {
        Winner(playerWinner, VictoryType.None);
    }

    public static string VictoryExplanation(VictoryType type)
    {
        switch(type)
        {
            case VictoryType.CommandersTaken:return "Both enemy commanders captured";
            case VictoryType.HandAndMindInD:return "Hand and Mind reached D";
            case VictoryType.HandPlus3PawnsInD:return "Hand and 3 Pawns reached D";
            case VictoryType.MindPlus3PawnsInD:return "Mind and 3 Pawns reached D";
            case VictoryType.CommanderPlus4PawnsCc:return "Commander in D and 4 Pawns in C/c";
            default:return "";
        }
    }

    private bool resultReported = false;

    public void Winner(string playerWinner, VictoryType victoryType)
{
    gameOver = true;
    winner = playerWinner;
    LastVictoryType = victoryType;

    // Record the PvC game in notation: mark the deciding move with '#', then write the dated file.
    if (ShouldRecord())
    {
        if (notationLog.Count > 0)
            notationLog[notationLog.Count - 1] += "#";
        WriteGameRecord(playerWinner);
    }

    // Online ranking: each client deterministically reaches the same winner/type via lockstep, so
    // both apply their own delta locally (and swap nicknames/points for display). Online matches only.
    if (!resultReported && NetworkController.IsOnline && NetworkController.MatchReady
        && NetworkController.Instance != null)
    {
        resultReported = true;
        NetworkController.Instance.ReportResult(playerWinner, VictoryPoints(victoryType));
    }

    // Play victory sound
    SoundManager.Instance?.PlayVictory();

    Debug.Log(playerWinner + " wins!");

    // Show victory panel with animation
    if (VictoryPanel != null && VictoryText != null)
    {
        string winnerColor = playerWinner == "orange" ? "Orange" : "Green";
        VictoryText.text = winnerColor + " Wins!\n" + VictoryExplanation(victoryType);
        if (NetworkController.IsOnline && !string.IsNullOrEmpty(NetworkController.LastResultMessage))
            VictoryText.text += "\n" + NetworkController.LastResultMessage;
        
        // Change text color based on winner
        if (playerWinner == "orange")
        {
            VictoryText.color = new Color(1f, 0.5f, 0f); // Orange color
        }
        else
        {
            VictoryText.color = new Color(0f, 0.8f, 0f); // Green color
        }
        
        StartCoroutine(ShowVictoryAnimation());
    }
    else
    {
        // Fallback if Victory Panel is not set up
        Debug.LogWarning("Victory Panel not assigned! Please set up Victory UI in Inspector.");
        
        // Try to find old UI elements (compatibility)
        GameObject winnerTextObj = GameObject.FindGameObjectWithTag("WinnerText");
        GameObject restartTextObj = GameObject.FindGameObjectWithTag("RestartText");
        
        if (winnerTextObj != null)
        {
            UnityEngine.UI.Text winnerText = winnerTextObj.GetComponent<UnityEngine.UI.Text>();
            if (winnerText != null)
            {
                winnerText.enabled = true;
                winnerText.text = playerWinner + " is the winner!";
            }
        }
        
        if (restartTextObj != null)
        {
            UnityEngine.UI.Text restartText = restartTextObj.GetComponent<UnityEngine.UI.Text>();
            if (restartText != null)
            {
                restartText.enabled = true;
            }
        }
    }
}

private IEnumerator ShowVictoryAnimation()
{
    // Activate the panel
    VictoryPanel.SetActive(true);
    
    // Get the CanvasGroup for fading (add one if it doesn't exist)
    CanvasGroup canvasGroup = VictoryPanel.GetComponent<CanvasGroup>();
    if (canvasGroup == null)
    {
        canvasGroup = VictoryPanel.AddComponent<CanvasGroup>();
    }
    
    // Start invisible
    canvasGroup.alpha = 0f;
    VictoryText.transform.localScale = Vector3.zero;
    
    // Fade in the background
    float duration = 0.5f;
    float elapsed = 0f;
    
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
        yield return null;
    }
    
    // Pop in the text
    duration = 0.3f;
    elapsed = 0f;
    
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;
        
        // Overshoot scale animation
        float scale = Mathf.Lerp(0f, 1.2f, t);
        VictoryText.transform.localScale = Vector3.one * scale;
        yield return null;
    }
    
    // Bounce back to normal size
    duration = 0.2f;
    elapsed = 0f;
    
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;
        float scale = Mathf.Lerp(1.2f, 1f, t);
        VictoryText.transform.localScale = Vector3.one * scale;
        yield return null;
    }
    
    VictoryText.transform.localScale = Vector3.one;
}

    // Optional: Ring rotation functionality for Scholars and Sage
    public void RotateRing(int ringPair, bool clockwise)
    {
        SoundManager.Instance?.PlayRingRotate();
        // ringPair: 1=Aa, 2=Bb, 3=Cc, 4=D
        // Determine which rings to rotate and by how much
        int ring1, ring2;
        int shiftAmount;
        GameObject ringSprite = null;

        switch (ringPair)
        {
            case 1: // Aa - rotate 90° = 12 positions (48/4)
                ring1 = 1; // A
                ring2 = 2; // a
                shiftAmount = 12;
                ringSprite = this.ring1;
                break;
            case 2: // Bb - rotate 90° = 6 positions (24/4)
                ring1 = 3; // B
                ring2 = 4; // b
                shiftAmount = 6;
                ringSprite = this.ring2;
                break;
            case 3: // Cc - rotate 90° = 3 positions (12/4)
                ring1 = 5; // C
                ring2 = 6; // c
                shiftAmount = 3;
                ringSprite = this.ring3;
                break;
            case 4: // D - rotate 90° = 1 position (4/4)
                ring1 = 7; // D
                ring2 = 7; // D (single ring)
                shiftAmount = 1;
                ringSprite = this.ring4;
                break;
            default:
                Debug.LogError($"Invalid ring pair: {ringPair}");
                return;
        }

        // For CW (clockwise), shift is negative (go backwards)
        // For ACW (counter-clockwise), shift is positive (go forwards)
        if (clockwise) shiftAmount = -shiftAmount;

        Debug.Log($"Rotating ring pair {ringPair} (rings {ring1},{ring2}) {(clockwise ? "CW" : "ACW")} by {Mathf.Abs(shiftAmount)} positions");

        // Rotate the piece positions
        RotateSingleRing(ring1, shiftAmount);
        if (ring2 != ring1)
        {
            RotateSingleRing(ring2, shiftAmount);
        }

        // Rotate the ring sprite (90 degrees) - skipped during ML/self-play training (no visuals).
        if (!isMLTraining)
        {
            float rotationAngle = clockwise ? -90f : 90f;

            if (ringSprite != null)
            {
                RotateRingSprite(ringSprite, rotationAngle);
            }
        }

        // Relay this rotation to the remote player in online matches (suppressed while replaying
        // an incoming rotation, to avoid an echo loop).
        if (NetworkController.IsOnline && !NetworkController.IsApplyingRemote && NetworkController.Instance != null)
        {
            NetworkController.Instance.SendRotation(ringPair, clockwise ? 1 : 0);
        }

        LogRotation(ringPair, clockwise);
        GameUX.Instance?.Rotated(ringPair, clockwise);

        NextTurn();
    }
private void RotateRingSprite(GameObject ringSprite, float angle)
{
    if (ringSprite != null)
    {
        StartCoroutine(AnimateRingRotation(ringSprite, angle));
    }
}

    private IEnumerator AnimateRingRotation(GameObject ringSprite, float targetAngle)
    {
        float duration = .4f;
        float elapsed = 0f;
        Quaternion startRotation = ringSprite.transform.rotation;
        Quaternion endRotation = startRotation * Quaternion.Euler(0, 0, targetAngle);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Smooth interpolation
            ringSprite.transform.rotation = Quaternion.Slerp(startRotation, endRotation, t);

            yield return null;
        }

        // Ensure final rotation is exact
        ringSprite.transform.rotation = endRotation;
    }



   private void RotateSingleRing(int ring, int shift)
{
    int size = RING_SIZES[ring - 1];
    GameObject[] temp = new GameObject[size];

    // Copy all pieces to temp array with rotation
    for (int i = 0; i < size; i++)
    {
        GameObject piece = positions[ring - 1][i];
        if (piece != null)
        {
            // Calculate new index (0-indexed)
            int newIndex = ((i + shift) % size + size) % size;
            temp[newIndex] = piece;

            // Update the piece's placeNumber (convert to 1-indexed)
            Chessman cm = piece.GetComponent<Chessman>();
            if (cm != null)
            {
                cm.SetPlaceNumber(newIndex + 1); // Convert to 1-indexed
                Vector3 start = cm.transform.position;
                cm.transform.position = Chessman.PositionAt(ring, newIndex + 1);
                if (GameUX.Instance != null && !isMLTraining)
                {
                    Vector3 target = cm.transform.position;
                    cm.transform.position = start;
                    GameUX.Instance.AnimatePiece(cm.transform, target, shift * 360f / size);
                }
            }
        }
    }

    // Copy back to positions array
    for (int i = 0; i < size; i++)
    {
        positions[ring - 1][i] = temp[i];
    }
    
    Debug.Log($"Rotated ring {ring} by {shift} positions");
}

    // Optional: Restart game
    public void Restart()
    {
        if (isMLTraining)
    {
        // During ML training, don't restart - let agents handle episode end
        return;
    }
    
        gameOver = false;
        winner = null;
        currentPlayer = "orange";
        lastMove = null;

        // Destroy all existing pieces
        foreach (GameObject piece in orangePlayer)
        {
            if (piece != null) Destroy(piece);
        }
        foreach (GameObject piece in greenPlayer)
        {
            if (piece != null) Destroy(piece);
        }

        // Clear positions
        for (int i = 0; i < 7; i++)
        {
            for (int j = 0; j < RING_SIZES[i]; j++)
            {
                positions[i][j] = null;
            }
        }

        // Restart
        Start();
    }
}