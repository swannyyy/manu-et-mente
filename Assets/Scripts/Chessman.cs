using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Chessman : MonoBehaviour
{
    // Position on board
    public int ringNumber; // 1=A, 2=a, 3=B, 4=b, 5=C, 6=c, 7=D
    public int placeNumber; // 0-indexed position within ring

    // References
    public GameObject controller;
    public GameObject movePlate;

    // Player color
    public string player; // "orange" or "green"

    // Piece type
    public string pieceType; // "P", "A", "T", "S", "W", "H", "B"

    // Ring sizes
    public static readonly int[] RING_SIZES = { 48, 48, 24, 24, 12, 12, 4 }; // A, a, B, b, C, c, D
    public static readonly string[] RING_NAMES = { "A", "a", "B", "b", "C", "c", "D" };

    // Sprites for each piece type
    public Sprite mind_g, mind_o, hand_g, hand_o, tech_g, tech_o, 
                  wise_g, wise_o, schol_g, schol_o, app_g, app_o, skill_g, skill_o;



// ACW diagonals moving INWARD (toward center)
    private static readonly HashSet<int>[] ACW_INWARD = new HashSet<int>[]
    {
        // Ring 1 (A -> a): A1,A3,A5,A7,A9,A11,A13,A15,A17,A19,A21,A23,A25,A27,A29,A31,A33,A35,A37,A39,A41,A43,A45,A47
        new HashSet<int> {1,3,5,7,9,11,13,15,17,19,21,23,25,27,29,31,33,35,37,39,41,43,45,47,2,4,6,8,10,12,14,16,18,20,22,24,26,28,30,32,34,36,38,40,42,44,46,48},
        
        // Ring 2 (a -> B): a2,a4,a8,a12,a14,a16,a20,a24,a26,a28,a32,a36,a38,a40,a44,a48
        new HashSet<int> {2,4,6,8,10,12,14,16,18,20,22,24,26,28,30,32,34,36,38,40,42,44,46,48},
        
        // Ring 3 (B -> b): all positions have ACW
        new HashSet<int> {1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24},
        
        // Ring 4 (b -> C): b3,b4,b9,b10,b15,b16,b21,b22
        new HashSet<int> {2,4,6,8,10,12,14,16,18,20,22,24},
        
        // Ring 5 (C -> c): all positions have ACW
        new HashSet<int> {1,2,3,4,5,6,7,8,9,10,11,12},
        
        // Ring 6 (c -> D): all positions have ACW
        new HashSet<int> {3,6,9,12},
        
        // Ring 7 (D): no inward movement
        new HashSet<int> {}
    };

    // CW diagonals moving INWARD (toward center)
    private static readonly HashSet<int>[] CW_INWARD = new HashSet<int>[]
    {
        // Ring 1 (A -> a): A2,A6,A10,A14,A18,A22,A26,A30,A34,A38,A42,A46
        new HashSet<int> {1,3,5,7,9,11,13,15,17,19,21,23,25,27,29,31,33,35,37,39,41,43,45,47,2,4,6,8,10,12,14,16,18,20,22,24,26,28,30,32,34,36,38,40,42,44,46,48},
        
        // Ring 2 (a -> B): a1,a5,a9,a13,a17,a21,a25,a29,a33,a37,a41,a45
        new HashSet<int> {1,3,5,7,9,11,13,15,17,19,21,23,25,27,29,31,33,35,37,39,41,43,45,47},
        
        // Ring 3 (B -> b): all positions have CW
        new HashSet<int> {1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24},
        
        // Ring 4 (b -> C): b1,b4,b7,b10,b13,b16,b19,b22
        new HashSet<int> {1,3,5,7,9,11,13,15,17,19,21,23},
        
        // Ring 5 (C -> c): all positions have CW
        new HashSet<int> {1,2,3,4,5,6,7,8,9,10,11,12},
        
        // Ring 6 (c -> D): all positions have CW
        new HashSet<int> {1,4,7,10},
        
        // Ring 7 (D): no inward movement
        new HashSet<int> {}
    };

    // ACW diagonals moving OUTWARD (toward edge)
    private static readonly HashSet<int>[] ACW_OUTWARD = new HashSet<int>[]
    {
        // Ring 1 (A): no outward movement
        new HashSet<int> {},
        
        // Ring 2 (a -> A): a2,a4,a6,a8,a10,a12,a14,a16,a18,a20,a22,a24,a26,a28,a30,a32,a34,a36,a38,a40,a42,a44,a46,a48
        new HashSet<int> {1,3,5,7,9,11,13,15,17,19,21,23,25,27,29,31,33,35,37,39,41,43,45,47,2,4,6,8,10,12,14,16,18,20,22,24,26,28,30,32,34,36,38,40,42,44,46,48},
        
        // Ring 3 (B -> a): all positions have ACW
        new HashSet<int> {1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24},
        
        // Ring 4 (b -> B): all positions have ACW
        new HashSet<int> {1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24},
        
        // Ring 5 (C -> b): all positions have ACW
        new HashSet<int> {1,2,3,4,5,6,7,8,9,10,11,12},
        
        // Ring 6 (c -> C): all positions have ACW
        new HashSet<int> {1,2,3,4,5,6,7,8,9,10,11,12},
        
        // Ring 7 (D -> c): all positions have ACW
        new HashSet<int> {1,2,3,4}
    };

    // CW diagonals moving OUTWARD (toward edge)
    private static readonly HashSet<int>[] CW_OUTWARD = new HashSet<int>[]
    {
        // Ring 1 (A): no outward movement
        new HashSet<int> {},
        
        // Ring 2 (a -> A): a1,a3,a5,a7,a9,a11,a13,a15,a17,a19,a21,a23,a25,a27,a29,a31,a33,a35,a37,a39,a41,a43,a45,a47
        new HashSet<int> {1,3,5,7,9,11,13,15,17,19,21,23,25,27,29,31,33,35,37,39,41,43,45,47,2,4,6,8,10,12,14,16,18,20,22,24,26,28,30,32,34,36,38,40,42,44,46,48},
        
        // Ring 3 (B -> a): all positions have CW
        new HashSet<int> {1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24},
        
        // Ring 4 (b -> B): all positions have CW
        new HashSet<int> {1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24},
        
        // Ring 5 (C -> b): all positions have CW
        new HashSet<int> {1,2,3,4,5,6,7,8,9,10,11,12},
        
        // Ring 6 (c -> C): all positions have CW
        new HashSet<int> {1,2,3,4,5,6,7,8,9,10,11,12},
        
        // Ring 7 (D -> c): all positions have CW
        new HashSet<int> {1,2,3,4}
    };
    public void Activate()
    {
        controller = GameObject.FindGameObjectWithTag("GameController");
        SetCoords();

        // Set sprite based on piece type and player
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        GameVisualTheme.FinishPiece(spriteRenderer);
        switch (pieceType)
        {
            case "B": // Mind
                spriteRenderer.sprite = (player == "orange") ? mind_o : mind_g;
                break;
            case "H": // Hand
                spriteRenderer.sprite = (player == "orange") ? hand_o : hand_g;
                break;
            case "T": // Technician
                spriteRenderer.sprite = (player == "orange") ? tech_o : tech_g;
                break;
            case "W": // Sage (Wise)
                spriteRenderer.sprite = (player == "orange") ? wise_o : wise_g;
                break;
            case "S": // Scholar
                spriteRenderer.sprite = (player == "orange") ? schol_o : schol_g;
                break;
            case "A": // Apprentice
                spriteRenderer.sprite = (player == "orange") ? app_o : app_g;
                break;
            case "P": // Pawn (skill)
                spriteRenderer.sprite = (player == "orange") ? skill_o : skill_g;
                break;
        }
    }

    private bool hasVisualPosition;
    public static Vector3 PositionAt(int ring, int place)
    {
        float[] radii = {1.9f,1.6f,1.4f,1.1f,.87f,.6f,.3f};
        float angle=(place-.5f)*Mathf.PI*2/RING_SIZES[ring-1];
        return new Vector3(radii[ring-1]*Mathf.Cos(angle),radii[ring-1]*Mathf.Sin(angle),-1);
    }

    public void SetCoords()
    {
        float angle = CalculateAngle(placeNumber, ringNumber);
        float radius = CalculateRadius(ringNumber);
        var target = new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), -1);
        if (hasVisualPosition && GameUX.Instance != null && !GameUX.Instance.Board.IsTraining)
            GameUX.Instance.AnimatePiece(transform, target);
        else transform.position = target;
        hasVisualPosition = true;
    }

    private float CalculateAngle(int place, int ring)
{
    int ringSize = RING_SIZES[ring - 1];
    
    // Calculate angle to CENTER of sector, not edge
    // Each sector spans (2π / ringSize) radians
    // Center is at (place + 0.5) / ringSize
    float anglePerSector = 2 * Mathf.PI / ringSize;
    float angle = (place - 0.5f) * anglePerSector;
    
    return angle;
}

    private float CalculateRadius(int ring)
    {
        // Adjust these radii to match your board visualization
        float[] radii = { 1.9f,1.6f, 1.4f, 1.1f, 0.87f, 0.6f,0.3f}; // A, a, B, b, C, c, D
        return radii[ring - 1];
    }

   public void OnMouseUp()
{
    if (GameUX.BlockInput) return;
    if (UnityEngine.EventSystems.EventSystem.current != null && Input.touchCount == 0 &&
        UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
    if (GameUX.Instance != null && GameUX.Instance.Learning &&
        !PlayableLesson.AllowsSelection(this)) return;
    // In online play you may only pick up your own color's pieces (even during the brief window
    // where the local board's currentPlayer matches the opponent), and only once both players
    // have joined the room.
    bool canPlay = !NetworkController.IsOnline ||
                   (NetworkController.MatchReady && player == NetworkController.LocalColor);

    if (!controller.GetComponent<game>().IsGameOver() &&
        controller.GetComponent<game>().GetCurrentPlayer() == player &&
        canPlay)
    {
        // Play piece select sound
        SoundManager.Instance?.PlayPieceSelect();
        
        bool wasSelected = GameUX.Instance != null && GameUX.Instance.Selected == this;
        DestroyMovePlates();
        GameUX.Instance?.Select(this);
        if (!wasSelected) InitiateMovePlates();
    }
}

    public void DestroyMovePlates()
    {
        GameObject[] movePlates = GameObject.FindGameObjectsWithTag("MovePlate");
        foreach (GameObject plate in movePlates)
        {
            plate.SetActive(false);
            Destroy(plate);
        }
    }

    public void InitiateMovePlates()
    {
        Debug.Log($"Generating moves for {pieceType} at ring {ringNumber}, place {placeNumber}");
        game sc = controller.GetComponent<game>();

        switch (pieceType)
        {
            case "P": // Pawn
                PawnMove(sc);
                break;
            case "A": // Apprentice
                ApprenticeMove(sc);
                break;
            case "T": // Technician
                TechnicianMove(sc);
                break;
            case "S": // Scholar
                ScholarMove(sc);
                break;
            case "W": // Sage
                SageMove(sc);
                break;
            case "H": // Hand
                HandMove(sc);
                break;
            case "B": // Mind
                MindMove(sc);
                break;
        }
    }

    // ========== MOVEMENT IMPLEMENTATIONS ==========

private void PawnMove(game sc)
{
    HashSet<string> addedMoves = new HashSet<string>();
    
    void AddUniquePlate(int r, int p, bool attack = false)
    {
        if (p < 1 || p > RING_SIZES[r - 1])
        {
            Debug.LogError($"PawnMove: Invalid place {p} for ring {r}");
            return;
        }
        
        string key = r + "_" + p;
        if (!addedMoves.Contains(key))
        {
            addedMoves.Add(key);
            if (attack)
            {
                // En passant - spawn directly
                MovePlateAttackSpawn(r, p);
            }
            else
            {
                // Normal move - check if square is empty
                GameObject occupant = sc.GetPosition(r, p);
                if (occupant == null)
                {
                    MovePlateSpawn(r, p);
                }
                // If occupied, don't create plate
            }
        }
    }

    int ringSize = RING_SIZES[ringNumber - 1];

    // Horizontal movement
    AddUniquePlate(ringNumber, WrapPlace(placeNumber + 1, ringSize));
    AddUniquePlate(ringNumber, WrapPlace(placeNumber - 1, ringSize));

    // Radial inward
    if (ringNumber < 7)
    {
        int[] inwardPlaces = MapIndex(ringNumber, placeNumber, ringNumber + 1);
        foreach (int p in inwardPlaces)
            AddUniquePlate(ringNumber + 1, p);
    }

    // Radial outward
    if (ringNumber > 1)
    {
        int[] outwardPlaces = MapIndex(ringNumber, placeNumber, ringNumber - 1);
        foreach (int p in outwardPlaces)
            AddUniquePlate(ringNumber - 1, p);
    }

    // Diagonal movement
    int[] drifts = { 1, -1 };
    string[] directions = { "in", "out" };
    
    foreach (int drift in drifts)
    {
        foreach (string dir in directions)
        {
            int[] next = DiagonalStep(ringNumber, placeNumber, drift, dir);
            if (next != null && sc.PositionOnBoard(next[0], next[1]))
                AddUniquePlate(next[0], next[1]);
        }
    }

    // En passant capture
    AddEnPassantCapture(sc);
}
    private void ApprenticeMove(game sc)
{
    // Apprentices move diagonally any distance and CAN capture normally
    AddDiagonalRays(ringNumber, placeNumber, sc, true, "T", true); // true = can capture
}

    private void ScholarMove(game sc)
{
    // Scholars move diagonally but can ONLY capture via en passant
    AddDiagonalRays(ringNumber, placeNumber, sc, false, "W", false); // false = no normal capture
    
    // En passant capture
    AddEnPassantCapture(sc);
}

    private void TechnicianMove(game sc)
{
    // Horizontal with capture
    int ringSize = RING_SIZES[ringNumber - 1];
    PointMovePlateWithCapture(ringNumber, WrapPlace(placeNumber + 1, ringSize), sc);
    PointMovePlateWithCapture(ringNumber, WrapPlace(placeNumber - 1, ringSize), sc);
    
    // Radial with capture
    AddRadialMovesWithCapture(ringNumber, placeNumber, sc);
}

private void SageMove(game sc)
{
    // Horizontal (no capture)
    int ringSize = RING_SIZES[ringNumber - 1];
    PointMovePlateEnPassantOnly(ringNumber, WrapPlace(placeNumber + 1, ringSize), sc);
    PointMovePlateEnPassantOnly(ringNumber, WrapPlace(placeNumber - 1, ringSize), sc);
    
    // Radial (no capture)
    AddRadialMoves(ringNumber, placeNumber, sc);
    
    // En passant capture
    AddEnPassantCapture(sc);
}

    private void HandMove(game sc)
{
    // Horizontal (no capture)
    int ringSize = RING_SIZES[ringNumber - 1];
    PointMovePlate(ringNumber, WrapPlace(placeNumber + 1, ringSize), sc);
    PointMovePlate(ringNumber, WrapPlace(placeNumber - 1, ringSize), sc);
    
    // Radial (no capture)
    AddRadialMoves(ringNumber, placeNumber, sc);
}

    private void MindMove(game sc)
    {
        // Same as Hand
        HandMove(sc);
    }


    // ========== HELPER METHODS ==========

    private void AddRadialMoves(int ring, int place, game sc)
{
    // Inward
    if (ring < 7)
    {
        int[] destPlaces = MapIndex(ring, place, ring + 1);
        foreach (int destPlace in destPlaces)
        {
            if (sc.PositionOnBoard(ring + 1, destPlace))
            {
                PointMovePlate(ring + 1, destPlace, sc);
            }
        }
    }

    // Outward
    if (ring > 1)
    {
        int[] destPlaces = MapIndex(ring, place, ring - 1);
        foreach (int destPlace in destPlaces)
        {
            if (sc.PositionOnBoard(ring - 1, destPlace))
            {
                PointMovePlate(ring - 1, destPlace, sc);
            }
        }
    }
}


    private void AddRadialMovesWithCapture(int ring, int place, game sc)
{
    // Inward
    if (ring < 7)
    {
        int[] destPlaces = MapIndex(ring, place, ring + 1);
        foreach (int destPlace in destPlaces)
        {
            if (sc.PositionOnBoard(ring + 1, destPlace))
            {
                PointMovePlateWithCapture(ring + 1, destPlace, sc);
            }
        }
    }

    // Outward
    if (ring > 1)
    {
        int[] destPlaces = MapIndex(ring, place, ring - 1);
        foreach (int destPlace in destPlaces)
        {
            if (sc.PositionOnBoard(ring - 1, destPlace))
            {
                PointMovePlateWithCapture(ring - 1, destPlace, sc);
            }
        }
    }
}

    private void AddDiagonalSingleStep(int ring, int place, int drift, string direction, game sc)
    {
        int[] result = DiagonalStep(ring, place, drift, direction);
        if (result != null)
        {
            PointMovePlate(result[0], result[1], sc);
        }
    }

    private void AddDiagonalRays(int ring, int place, game sc, bool isApprentice, string enablerType, bool canCaptureNormally)
{
    // Four diagonal directions: ACW in, CW in, ACW out, CW out
    int[] drifts = { 1, -1 };
    string[] directions = { "in", "out" };

    foreach (int drift in drifts)
    {
        foreach (string dir in directions)
        {
            // Trace ray
            int currentRing = ring;
            int currentPlace = place;

            for (int step = 0; step < 10; step++)
            {
                int[] next = DiagonalStep(currentRing, currentPlace, drift, dir);
                if (next == null) break;

                int destRing = next[0];
                int destPlace = next[1];

                // Check if destination is valid
                if (!sc.PositionOnBoard(destRing, destPlace)) break;

                // Determine current and destination ring pair levels
                int currentPairLevel;
                if (currentRing <= 2) currentPairLevel = 1; // Aa
                else if (currentRing <= 4) currentPairLevel = 2; // Bb
                else if (currentRing <= 6) currentPairLevel = 3; // Cc
                else currentPairLevel = 4; // D
                
                int destPairLevel;
                if (destRing <= 2) destPairLevel = 1; // Aa
                else if (destRing <= 4) destPairLevel = 2; // Bb
                else if (destRing <= 6) destPairLevel = 3; // Cc
                else destPairLevel = 4; // D

                // Check if we're jumping to a new ring pair
                bool isJumpingPair = (destPairLevel != currentPairLevel);

                // If jumping to a new pair, check if enabler allows it
                if (isJumpingPair && !CanJumpToRing(destRing, sc, enablerType))
                {
                    Debug.Log($"Cannot jump from ring {currentRing} to ring {destRing} - enabler not present");
                    break;
                }

                // Check occupancy
                GameObject piece = sc.GetPosition(destRing, destPlace);
                
                if (piece == null)
                {
                    // Empty square - can move here
                    MovePlateSpawn(destRing, destPlace);
                }
                else
                {
                    // Occupied square
                    Chessman targetPiece = piece.GetComponent<Chessman>();
                    
                    if (targetPiece != null)
                    {
                        if (targetPiece.player != player && canCaptureNormally)
                        {
                            // Enemy piece and we can capture normally
                            MovePlateAttackSpawn(destRing, destPlace);
                        }
                        // Either way, can't move past this piece
                        break;
                    }
                }

                // Move to next position in ray
                currentRing = destRing;
                currentPlace = destPlace;
            }
        }
    }
}


    private bool HasEnablerInOrBeyond(int targetRing, game sc, string enablerType)
    {
        // Check if player has an enabler (T or W) in targetRing or any MORE INWARD ring
        // targetRing and beyond means: targetRing, targetRing+1, ..., up to ring 7 (D)
        for (int r = targetRing; r <= 7; r++)
        {
            for (int p = 0; p < RING_SIZES[r - 1]; p++)
            {
                GameObject piece = sc.GetPosition(r, p + 1); // Convert to 1-indexed
                if (piece != null)
                {
                    Chessman cm = piece.GetComponent<Chessman>();
                    if (cm.player == player && cm.pieceType == enablerType)
                    {
                        Debug.Log($"Found enabler {enablerType} at ring {r}, allowing jump to ring {targetRing}");
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private bool CanJumpToRing(int targetRing, game sc, string enablerType)
{
    // Determine which ring pair the target belongs to
    // Aa = rings 1-2, Bb = rings 3-4, Cc = rings 5-6, D = ring 7
    
    int targetPairLevel;
    if (targetRing <= 2) targetPairLevel = 1; // Aa
    else if (targetRing <= 4) targetPairLevel = 2; // Bb
    else if (targetRing <= 6) targetPairLevel = 3; // Cc
    else targetPairLevel = 4; // D
    
    // Check if enabler has reached this level or beyond
    for (int r = 1; r <= 7; r++)
    {
        int enablerPairLevel;
        if (r <= 2) enablerPairLevel = 1; // Aa
        else if (r <= 4) enablerPairLevel = 2; // Bb
        else if (r <= 6) enablerPairLevel = 3; // Cc
        else enablerPairLevel = 4; // D
        
        // Only check rings at target level or deeper
        if (enablerPairLevel < targetPairLevel) continue;
        
        for (int p = 1; p <= RING_SIZES[r - 1]; p++)
        {
            GameObject piece = sc.GetPosition(r, p);
            if (piece != null)
            {
                Chessman cm = piece.GetComponent<Chessman>();
                if (cm.player == player && cm.pieceType == enablerType)
                {
                    Debug.Log($"Enabler {enablerType} found at ring {r} (level {enablerPairLevel}), allowing access to ring {targetRing} (level {targetPairLevel})");
                    return true;
                }
            }
        }
    }
    
    Debug.Log($"No enabler {enablerType} found at level {targetPairLevel} or beyond, blocking ring {targetRing}");
    return false;
}
    private void AddEnPassantCapture(game sc)
    {
        // En passant: can capture the piece that just moved
        var lastMove = sc.GetLastMove();

        if (lastMove == null || lastMove.player == player)
        {
            return; // No last move or it was our own piece
        }

        Debug.Log($"=== EN PASSANT CHECK ===");
        Debug.Log($"Current piece: {pieceType} ({player}) at ring {ringNumber}, place {placeNumber}");
        Debug.Log($"Last move: {lastMove.pieceType} ({lastMove.player}) from ring {lastMove.fromRing}, place {lastMove.fromPlace} to ring {lastMove.toRing}, place {lastMove.toPlace}");

        // The victim is currently at (toRing, toPlace)
        int victimRing = lastMove.toRing;
        int victimPlace = lastMove.toPlace;

        // Verify the victim piece is still there
        GameObject victimPiece = sc.GetPosition(victimRing, victimPlace);
        if (victimPiece == null)
        {
            Debug.Log("En passant FAILED: no piece at victim location");
            return; // Victim is gone
        }

        Chessman victimChessman = victimPiece.GetComponent<Chessman>();
        if (victimChessman == null || victimChessman.player == player)
        {
            Debug.Log("En passant FAILED: victim is our own piece or invalid");
            return; // Not an enemy
        }

        Debug.Log($"Victim confirmed: {victimChessman.pieceType} at ring {victimRing}, place {victimPlace}");

        // The target square is where the victim moved FROM
        int targetRing = lastMove.fromRing;
        int targetPlace = lastMove.fromPlace;

        Debug.Log($"Target square (where victim moved FROM): ring {targetRing}, place {targetPlace}");

        // Verify the target square is empty
        GameObject pieceAtTarget = sc.GetPosition(targetRing, targetPlace);
        if (pieceAtTarget != null)
        {
            Debug.Log($"En passant FAILED: target square {targetRing},{targetPlace} is occupied by {pieceAtTarget.name}");
            return; // Square is occupied
        }

        Debug.Log($"Target square is empty ✓");

        // Check if current piece can reach this square
        bool isReachable = IsReachableForEnPassant(targetRing, targetPlace, sc);
        Debug.Log($"Is target reachable? {isReachable}");

        if (!isReachable)
        {
            Debug.Log($"En passant FAILED: target square {targetRing},{targetPlace} is not reachable");
            return;
        }

        // Verify the victim is adjacent to the target square
        bool isAdjacent = IsAdjacentCell(targetRing, targetPlace, victimRing, victimPlace);
        Debug.Log($"Is victim adjacent to target? {isAdjacent}");
        Debug.Log($"  - Target: ring {targetRing}, place {targetPlace}");
        Debug.Log($"  - Victim: ring {victimRing}, place {victimPlace}");

        if (!isAdjacent)
        {
            Debug.Log($"En passant FAILED: victim at {victimRing},{victimPlace} is not adjacent to target {targetRing},{targetPlace}");
            return;
        }

        // En passant is only legal if the landing square is one this piece could move to
        // normally. Normal move generation has already run this turn, so a non-capturing plate
        // exists at the target iff the move is legal for this piece. Gating on that prevents en
        // passant from creating an otherwise-forbidden move (which the AI was exploiting).
        if (!HasNormalMovePlateAt(targetRing, targetPlace))
        {
            Debug.Log($"En passant FAILED: {targetRing},{targetPlace} is not a legal normal move for this {pieceType}");
            return;
        }

        Debug.Log($"=== En passant SUCCESS: spawning attack plate at ring {targetRing}, place {targetPlace} ===");
        // The en passant landing square is an empty adjacent cell, so a normal (green) move
        // plate was already spawned there. Two overlapping plates at the same position make the
        // click land on the wrong one (a normal move, no capture). Remove the normal plate so the
        // capturing attack plate is the only one on that square.
        RemoveMovePlateAt(targetRing, targetPlace);
        MovePlateAttackSpawn(targetRing, targetPlace);
    }

    // Destroys any move plate currently occupying (ring, place). Uses DestroyImmediate so the
    // removal is synchronous within the same frame (Unity's Destroy is deferred to end of frame,
    // which would leave the stale plate clickable and visible to AI move enumeration this frame).
    private void RemoveMovePlateAt(int ring, int place)
    {
        GameObject[] plates = GameObject.FindGameObjectsWithTag("MovePlate");
        foreach (GameObject p in plates)
        {
            if (p == null) continue;
            MovePlate mp = p.GetComponent<MovePlate>();
            if (mp != null && mp.GetRingNumber() == ring && mp.GetPlaceNumber() == place)
                DestroyImmediate(p);
        }
    }

    // True if normal move generation already placed a non-capturing plate on (ring, place).
    // A normal plate existing there means this piece can legally land on that square, which is
    // the precondition for en passant: the capture is only allowed if the move itself is legal.
    private bool HasNormalMovePlateAt(int ring, int place)
    {
        GameObject[] plates = GameObject.FindGameObjectsWithTag("MovePlate");
        foreach (GameObject p in plates)
        {
            if (p == null) continue;
            MovePlate mp = p.GetComponent<MovePlate>();
            if (mp != null && !mp.attack && mp.GetRingNumber() == ring && mp.GetPlaceNumber() == place)
                return true;
        }
        return false;
    }

    private bool IsReachableForEnPassant(int targetRing, int targetPlace, game sc)
{
    // For Pawn: any square within 1 step (horizontal, radial, or diagonal)
    if (pieceType == "P")
    {
        // Check horizontal (same ring, adjacent place)
        if (targetRing == ringNumber)
        {
            int ringSize = RING_SIZES[ringNumber - 1];
            int diff = Mathf.Abs(targetPlace - placeNumber);
            if (diff == 1 || diff == ringSize - 1) // Adjacent including wrap
                return true;
        }

        // Check radial or diagonal (adjacent ring)
        if (Mathf.Abs(targetRing - ringNumber) == 1)
        {
            return IsAdjacentCell(ringNumber, placeNumber, targetRing, targetPlace);
        }
        
        return false;
    }

    // For Scholar: must be on a diagonal ray from current position
    if (pieceType == "S")
    {
        return IsOnDiagonalRay(ringNumber, placeNumber, targetRing, targetPlace);
    }

    // For Sage: must be horizontally or radially adjacent (1 step away)
    if (pieceType == "W")
    {
        // Horizontal (same ring, adjacent place only)
        if (targetRing == ringNumber)
        {
            int ringSize = RING_SIZES[ringNumber - 1];
            int diff = Mathf.Abs(targetPlace - placeNumber);
            
            // Must be exactly 1 step away, accounting for wrap-around
            if (diff == 1 || diff == ringSize - 1)
                return true;
            
            return false; // Not adjacent
        }
        
        // Radial (adjacent ring and adjacent place considering mapping)
        if (Mathf.Abs(targetRing - ringNumber) == 1)
        {
            return IsAdjacentCell(ringNumber, placeNumber, targetRing, targetPlace);
        }
        
        return false;
    }

    return false;
}
    private bool IsAdjacentCell(int ring1, int place1, int ring2, int place2)
{
    // Same ring, adjacent places
    if (ring1 == ring2)
    {
        int ringSize = RING_SIZES[ring1 - 1];
        int diff = Mathf.Abs(place1 - place2);
        
        // Adjacent means difference of 1, or wrapping around (1 and ringSize)
        return diff == 1 || diff == ringSize - 1;
    }
    
    // Adjacent rings (differ by 1)
    if (Mathf.Abs(ring1 - ring2) == 1)
    {
        // For radially adjacent, check if they map to each other
        int outerRing = Mathf.Max(ring1, ring2);
        int innerRing = Mathf.Min(ring1, ring2);
        int outerPlace = (ring1 > ring2) ? place1 : place2;
        int innerPlace = (ring1 < ring2) ? place1 : place2;
        
        // Check if outerPlace maps to innerPlace
        int[] mappedPlaces = MapIndex(outerRing, outerPlace, innerRing);
        foreach (int mapped in mappedPlaces)
        {
            if (mapped == innerPlace || mapped == innerPlace + 1 || mapped == innerPlace - 1)
                return true;
        }
    }
    
    return false;
}

    private bool IsOnDiagonalRay(int fromRing, int fromPlace, int toRing, int toPlace)
{
    // Trace all diagonal rays from the starting position
    int[] drifts = { 1, -1 };
    string[] directions = { "in", "out" };
    
    foreach (int drift in drifts)
    {
        foreach (string dir in directions)
        {
            int currentRing = fromRing;
            int currentPlace = fromPlace;
            
            // Trace this ray up to 10 steps
            for (int step = 0; step < 10; step++)
            {
                int[] next = DiagonalStep(currentRing, currentPlace, drift, dir);
                if (next == null) break;
                
                int destRing = next[0];
                int destPlace = next[1];
                
                // Found the target!
                if (destRing == toRing && destPlace == toPlace)
                    return true;
                
                currentRing = destRing;
                currentPlace = destPlace;
            }
        }
    }
    
    return false;
}
    // ========== INDEX MAPPING ==========

    private int[] MapIndex(int fromRing, int fromPlace, int toRing)
{
    // Returns array of possible destination indices when moving between rings
    // All positions are 1-indexed (1 to ringSize)
    
    int toRingSize = RING_SIZES[toRing - 1];

    // Adjacent same-letter pairs (1:1 mapping)
    if ((fromRing == 1 && toRing == 2) || (fromRing == 2 && toRing == 1)) // A ↔ a
        return new int[] { fromPlace };
    if ((fromRing == 3 && toRing == 4) || (fromRing == 4 && toRing == 3)) // B ↔ b
        return new int[] { fromPlace };
    if ((fromRing == 5 && toRing == 6) || (fromRing == 6 && toRing == 5)) // C ↔ c
        return new int[] { fromPlace };

    // a → B (2:1 inward)
    if (fromRing == 2 && toRing == 3)
    {
        // a1,a2→B1; a3,a4→B2; etc.
        int mapped = (fromPlace + 1) / 2; // Integer division, 1-indexed
        if (mapped < 1) mapped = 1;
        if (mapped > toRingSize) mapped = toRingSize;
        return new int[] { mapped };
    }

    // B → a (1:2 outward)
    if (fromRing == 3 && toRing == 2)
    {
        // B1→a1,a2; B2→a3,a4; etc.
        int pos1 = fromPlace * 2 - 1;
        int pos2 = fromPlace * 2;
        
        // Ensure positions are valid
        if (pos1 < 1) pos1 = 1;
        if (pos2 > toRingSize) pos2 = toRingSize;
        
        return new int[] { pos1, pos2 };
    }

    // b → C (2:1 inward)
    if (fromRing == 4 && toRing == 5)
    {
        // b1,b2→C1; b3,b4→C2; etc.
        int mapped = (fromPlace + 1) / 2;
        if (mapped < 1) mapped = 1;
        if (mapped > toRingSize) mapped = toRingSize;
        return new int[] { mapped };
    }

    // C → b (1:2 outward)
    if (fromRing == 5 && toRing == 4)
    {
        // C1→b1,b2; C2→b3,b4; etc.
        int pos1 = fromPlace * 2 - 1;
        int pos2 = fromPlace * 2;
        
        if (pos1 < 1) pos1 = 1;
        if (pos2 > toRingSize) pos2 = toRingSize;
        
        return new int[] { pos1, pos2 };
    }

    // c → D (3:1 inward)
    if (fromRing == 6 && toRing == 7)
    {
        // c1,c2,c3→D1; c4,c5,c6→D2; etc.
        int mapped = (fromPlace + 2) / 3;
        if (mapped < 1) mapped = 1;
        if (mapped > toRingSize) mapped = toRingSize;
        return new int[] { mapped };
    }

    // D → c (1:3 outward)
    if (fromRing == 7 && toRing == 6)
    {
        // D1→c1,c2,c3; D2→c4,c5,c6; etc.
        int pos1 = fromPlace * 3 - 2;
        int pos2 = fromPlace * 3 - 1;
        int pos3 = fromPlace * 3;
        
        if (pos1 < 1) pos1 = 1;
        if (pos3 > toRingSize) pos3 = toRingSize;
        
        return new int[] { pos1, pos2, pos3 };
    }

    // Default: same index
    return new int[] { fromPlace };
}

private int[] DiagonalStep(int fromRing, int fromPlace, int drift, string direction)
{
    // drift: +1 for ACW, -1 for CW
    // direction: "in" (toward center) or "out" (toward edge)

    int nextRing = direction == "in" ? fromRing + 1 : fromRing - 1;
    if (nextRing < 1 || nextRing > 7) return null;

    // Check if this specific diagonal exists using lookup tables
    if (!DiagonalExists(fromRing, fromPlace, drift, direction))
    {
        return null;
    }

    // Standard mapping
    int[] basePlaces = MapIndex(fromRing, fromPlace, nextRing);
    int basePlace;
    
    if (basePlaces.Length > 1)
    {
        // Multiple destinations: choose based on drift
        if (drift > 0)
            basePlace = basePlaces[basePlaces.Length - 1];
        else
            basePlace = basePlaces[0];
    }
    else
    {
        basePlace = basePlaces[0];
    }
    int nextRingSize = RING_SIZES[nextRing - 1];
    int destPlace = WrapPlace(basePlace + drift, nextRingSize);
    return new int[] { nextRing, destPlace };
}

private bool DiagonalExists(int fromRing, int fromPlace, int drift, string direction)
{
    // drift: +1 for ACW, -1 for CW
    // direction: "in" or "out"
    
    if (fromRing < 1 || fromRing > 7)
    {
        Debug.LogError($"DiagonalExists: fromRing {fromRing} out of bounds");
        return false;
    }
    
    int ringSize = RING_SIZES[fromRing - 1];
    if (fromPlace < 1 || fromPlace > ringSize)
    {
        Debug.LogError($"DiagonalExists: fromPlace {fromPlace} out of bounds for ring {fromRing} (size {ringSize})");
        return false;
    }
    
    bool isACW = (drift > 0);
    int ringIndex = fromRing - 1; // Convert to 0-based index
    
    try
    {
        if (direction == "in")
        {
            if (isACW)
            {
                return ACW_INWARD[ringIndex].Contains(fromPlace);
            }
            else
            {
                return CW_INWARD[ringIndex].Contains(fromPlace);
            }
        }
        else // direction == "out"
        {
            if (isACW)
            {
                return ACW_OUTWARD[ringIndex].Contains(fromPlace);
            }
            else
            {
                return CW_OUTWARD[ringIndex].Contains(fromPlace);
            }
        }
    }
    catch (System.Exception e)
    {
        Debug.LogError($"DiagonalExists exception: ring={fromRing}, place={fromPlace}, drift={drift}, dir={direction}, error={e.Message}");
        return false;
    }
}
    

    private bool IsUppercaseRing(int ring)
    {
        // Odd ring numbers: 1=A, 3=B, 5=C, 7=D
        return ring % 2 == 1;
    }

private bool IsLowercaseRing(int ring)
{
    // Even ring numbers: 2=a, 4=b, 6=c
    return ring % 2 == 0;
}


    // ========== MOVE PLATE SPAWNING ==========

  private void PointMovePlate(int ring, int place, game sc)
{
    // Validate inputs
    if (ring < 1 || ring > 7)
    {
        Debug.LogError($"PointMovePlate: invalid ring {ring}");
        return;
    }
    
    int ringSize = RING_SIZES[ring - 1];
    if (place < 1 || place > ringSize)
    {
        Debug.LogError($"PointMovePlate: invalid place {place} for ring {ring} (size {ringSize})");
        return;
    }
    
    // Check if position is occupied
    GameObject piece = sc.GetPosition(ring, place);
    
    if (piece == null)
    {
        // Empty square - can move here
        MovePlateSpawn(ring, place);
    }
    // If occupied by anyone, can't move here (no capture allowed)
}
    private void PointMovePlateWithCapture(int ring, int place, game sc)
    {
        // Validate inputs
        if (ring < 1 || ring > 7)
        {
            Debug.LogError($"PointMovePlateWithCapture: invalid ring {ring}");
            return;
        }

        int ringSize = RING_SIZES[ring - 1];
        if (place < 1 || place > ringSize)
        {
            Debug.LogError($"PointMovePlateWithCapture: invalid place {place} for ring {ring} (size {ringSize})");
            return;
        }

        // Check if position is occupied
        GameObject piece = sc.GetPosition(ring, place);

        if (piece == null)
        {
            // Empty square - can move here
            MovePlateSpawn(ring, place);
        }
        else
        {
            // Occupied - check if we can capture
            Chessman targetPiece = piece.GetComponent<Chessman>();
            if (targetPiece != null && targetPiece.player != player)
            {
                // Enemy piece - can capture normally
                MovePlateAttackSpawn(ring, place);
            }
            // If it's our own piece, don't create a move plate
        }
    }
    private void PointMovePlateEnPassantOnly(int ring, int place, game sc)
{
    // Validate inputs
    if (ring < 1 || ring > 7)
    {
        Debug.LogError($"PointMovePlateEnPassantOnly: invalid ring {ring}");
        return;
    }
    
    int ringSize = RING_SIZES[ring - 1];
    if (place < 1 || place > ringSize)
    {
        Debug.LogError($"PointMovePlateEnPassantOnly: invalid place {place} for ring {ring} (size {ringSize})");
        return;
    }
    
    // Check if position is occupied
    GameObject piece = sc.GetPosition(ring, place);
    
    if (piece == null)
    {
        // Empty square - can move here
        MovePlateSpawn(ring, place);
    }
    // If occupied, can't move here (can only capture via en passant, handled separately)
}

   private void MovePlateSpawn(int ring, int place)
{
    // place is 1-indexed, use it directly with CalculateAngle
    float angle = CalculateAngle(place, ring);
    float radius = CalculateRadius(ring);
    
    Vector3 position = new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), -3.0f);
    
    GameObject mp = Instantiate(movePlate, position, Quaternion.identity);
    MovePlate mpScript = mp.GetComponent<MovePlate>();
    mpScript.SetReference(gameObject);
    mpScript.SetCoords(ring, place); // Keep as 1-indexed for MovePlate
    mpScript.attack = false;
}

private void MovePlateAttackSpawn(int ring, int place)
{
    // place is 1-indexed, use it directly with CalculateAngle
    float angle = CalculateAngle(place, ring);
    float radius = CalculateRadius(ring);
    
    Vector3 position = new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), -3.0f);
    
    GameObject mp = Instantiate(movePlate, position, Quaternion.identity);
    MovePlate mpScript = mp.GetComponent<MovePlate>();
    mpScript.SetReference(gameObject);
    mpScript.SetCoords(ring, place); // Keep as 1-indexed for MovePlate
    mpScript.attack = true;
}

    // ========== UTILITY ==========

    private int Imod(int n, int m)
    {
        return ((n % m) + m) % m;
    }
    // Add this as a helper method in the Chessman class
    private int WrapPlace(int place, int ringSize)
    {
        // Convert to 0-indexed, wrap, convert back to 1-indexed
        int zeroIndexed = place - 1;
        int wrapped = Imod(zeroIndexed, ringSize);
        return wrapped + 1;
    }

    // ========== GETTERS/SETTERS ==========

    public void SetRingNumber(int ring) { ringNumber = ring; }
    public void SetPlaceNumber(int place) { placeNumber = place; }
    public int GetRingNumber() { return ringNumber; }
    public int GetPlaceNumber() { return placeNumber; }
}