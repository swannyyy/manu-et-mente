using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovePlate : MonoBehaviour
{
    // Reference to the game controller
    public GameObject controller;
    
    // The piece that created this MovePlate
    private GameObject reference;

    // Destination coordinates
    private int ringNumber;
    private int placeNumber;

    // Type of move plate
    public bool attack = false;

    public void Start()
    {
        GameVisualTheme.FinishMarker(GetComponent<SpriteRenderer>(), attack);
        // Color and segmented capture outlines distinguish the move types.
        if (attack)
        {
            // Coral for captures.
            GetComponent<SpriteRenderer>().color = new Color(1f, 0.39f, 0.28f, 0.85f);
        }
        else
        {
            // Mint for legal destinations.
            GetComponent<SpriteRenderer>().color = new Color(0.29f, 0.85f, 0.73f, 0.8f);
        }
    }

public void OnMouseUp()
{
    if (GameUX.BlockInput && !NetworkController.IsApplyingRemote) return;
    if (GameUX.Instance != null && GameUX.Instance.Learning && !PlayableLesson.AllowsMove(this)) return;
    if (reference == null) return;
    controller = GameObject.FindGameObjectWithTag("GameController");
    game gameScript = controller.GetComponent<game>();
    
    if (gameScript == null)
    {
        Debug.LogError("Game script not found!");
        return;
    }

    Chessman movingPiece = reference.GetComponent<Chessman>();
    
    if (movingPiece == null)
    {
        Debug.LogError("Chessman script not found on reference piece!");
        return;
    }

    if (movingPiece.player != gameScript.GetCurrentPlayer() || gameScript.IsGameOver()) return;
    gameObject.SetActive(false);
    int fromRing = movingPiece.GetRingNumber();
    int fromPlace = movingPiece.GetPlaceNumber();

    bool didCapture = false;
    bool didEnPassant = false;

    // Handle capture
    if (attack)
    {
        GameObject targetPiece = gameScript.GetPosition(ringNumber, placeNumber);

        if (targetPiece != null)
        {
            // Normal capture
            Destroy(targetPiece);
            didCapture = true;
            SoundManager.Instance?.PlayPieceCapture(); // SOUND: Capture
            Debug.Log($"Normal capture at ring {ringNumber}, place {placeNumber}");
        }
        else
        {
            // En passant capture
            game.MoveInfo lastMove = gameScript.GetLastMove();

            if (lastMove != null)
            {
                GameObject victim = gameScript.GetPosition(lastMove.toRing, lastMove.toPlace);

                if (victim != null)
                {
                    Chessman victimPiece = victim.GetComponent<Chessman>();

                    if (victimPiece != null && victimPiece.player != movingPiece.player)
                    {
                        gameScript.SetPositionEmpty(lastMove.toRing, lastMove.toPlace);
                        Destroy(victim);
                        didCapture = true;
                        didEnPassant = true;
                        SoundManager.Instance?.PlayEnPassant(); // SOUND: En Passant
                        Debug.Log($"En passant capture! {movingPiece.pieceType} captured {victimPiece.pieceType}");
                    }
                }
            }
        }
    }
    else
    {
        // Normal move (no capture)
        SoundManager.Instance?.PlayPieceMove(); // SOUND: Move
    }

    // Clear the original position
    gameScript.SetPositionEmpty(fromRing, fromPlace);

    // Update piece position
    movingPiece.SetRingNumber(ringNumber);
    movingPiece.SetPlaceNumber(placeNumber);
    movingPiece.SetCoords();

    // Update game matrix
    gameScript.SetPosition(reference);

    // Record move
    gameScript.RecordMove(
        movingPiece.player,
        movingPiece.pieceType,
        fromRing,
        fromPlace,
        ringNumber,
        placeNumber
    );

    // Record move in paper notation (for game archive / learning)
    gameScript.LogPieceMove(
        movingPiece.player,
        movingPiece.pieceType,
        fromRing,
        fromPlace,
        ringNumber,
        placeNumber,
        didCapture,
        didEnPassant
    );

    // Relay this move to the remote player in online matches. Replayed remote moves set
    // IsApplyingRemote, so we don't echo them back (which would loop forever).
    if (NetworkController.IsOnline && !NetworkController.IsApplyingRemote && NetworkController.Instance != null)
    {
        NetworkController.Instance.SendMove(fromRing, fromPlace, ringNumber, placeNumber);
    }

    // Next turn
    gameScript.NextTurn();

    // Clean up
    movingPiece.DestroyMovePlates();
}

    // Set the destination coordinates for this move plate
    public void SetCoords(int ring, int place)
    {
        ringNumber = ring;
        placeNumber = place;
    }

    // Set the piece that created this move plate
    public void SetReference(GameObject obj)
    {
        reference = obj;
    }

    // Get the piece that created this move plate
    public GameObject GetReference()
    {
        return reference;
    }

    // Get destination ring
    public int GetRingNumber()
    {
        return ringNumber;
    }

    // Get destination place
    public int GetPlaceNumber()
    {
        return placeNumber;
    }
}