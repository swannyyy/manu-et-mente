// SPDX-License-Identifier: MPL-2.0
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LearnManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject tutorialPanel;
    public Text instructionText;
    public Text stepCounterText;
    public Button nextButton;
    public Button skipButton;
    public Image highlightCircle;
    public GameObject arrowPointer;

    [Header("Game References")]
    public GameObject ring1; // Aa
    public GameObject ring2; // Bb
    public GameObject ring3; // Cc
    public GameObject ring4; // D

    [Header("Piece References")]
    public GameObject tutorialOrangeHand;
    public GameObject tutorialOrangeTechnician;
    public GameObject tutorialOrangeScholar;
    public GameObject tutorialGreenPawn;

    [Header("Highlight Settings")]
    public Color highlightColor = new Color(1f, 1f, 0f, 0.5f); // Yellow transparent
    public float highlightPulseSpeed = 2f;

    private game gameController;
    private bool stepCompleted = false;
    private Coroutine pulse;

    void Start()
    {
        gameController = GetComponent<game>();
        
        // Disable normal game controls
        if (gameController != null)
        {
            gameController.enabled = false;
        }

        // Setup buttons
        if (nextButton != null)
        {
            nextButton.onClick.AddListener(NextStep);
        }

        if (skipButton != null)
        {
            skipButton.onClick.AddListener(SkipTutorial);
        }

        // Hide highlight initially
        if (highlightCircle != null)
        {
            highlightCircle.gameObject.SetActive(false);
        }

        if (arrowPointer != null)
        {
            arrowPointer.SetActive(false);
        }

        // Start tutorial
        StartCoroutine(RunTutorial());
    }

    IEnumerator RunTutorial()
    {
        yield return new WaitForSeconds(0.5f);

        // Step 0: Welcome
        ShowInstruction(
            "Welcome to Circular Chess!\n\nThis quick tutorial will teach you the basics in under 2 minutes.",
            "Tutorial (1/12)"
        );
        yield return WaitForNext();

        // Step 1: Board Introduction
        ShowInstruction(
            "This is the circular board with 7 rings:\nAa, Bb, Cc, and the center D ring.",
            "Tutorial (2/12)"
        );
        HighlightRing(ring4); // Highlight D ring
        yield return WaitForNext();

        // Step 2: Goal of the game
        ShowInstruction(
            "GOAL: Hand + Mind in D; or a commander + 3 Pawns in D; or, only after losing one of your commanders, your last commander in D + 4 Pawns in C/c. Capturing both enemy commanders also wins.",
            "Tutorial (3/12)"
        );
        HideHighlight();
        yield return WaitForNext();

        // Step 3: Piece Introduction
        ShowInstruction(
            "You control 16 pieces. Each type moves differently.\n\nLet's learn the most important ones!",
            "Tutorial (4/12)"
        );
        yield return WaitForNext();

        // Step 4: Hand piece (most important)
        ShowInstruction(
            "This is the HAND (H) - your most valuable piece!\n\nIt scores 1 point in the D ring.",
            "Tutorial (5/12)"
        );
        if (tutorialOrangeHand != null)
        {
            HighlightPiece(tutorialOrangeHand);
            PointArrowAt(tutorialOrangeHand);
        }
        yield return WaitForNext();

        // Step 5: Hand movement
        ShowInstruction(
            "Hand steps around a ring or radially (in/out), but CANNOT capture.\n\nClick the Hand to see its moves!",
            "Tutorial (6/12)"
        );
        yield return WaitForPlayerToSelectPiece(tutorialOrangeHand);

        // Step 6: Technician piece
        ShowInstruction(
            "This is the TECHNICIAN (T) - it CAN capture!\n\nIt moves horizontally AND radially (in/out).",
            "Tutorial (7/12)"
        );
        HideHighlight();
        if (tutorialOrangeTechnician != null)
        {
            HighlightPiece(tutorialOrangeTechnician);
            PointArrowAt(tutorialOrangeTechnician);
        }
        yield return WaitForNext();

        // Step 7: Enabler concept
        ShowInstruction(
            "IMPORTANT: Technician is an ENABLER.\n\nYou need enablers in D ring to unlock the full board!",
            "Tutorial (8/12)"
        );
        yield return WaitForNext();

        // Step 8: Scholar & Rotation
        ShowInstruction(
            "This is the SCHOLAR (S).\n\nScholars can ROTATE the rings! This is powerful!",
            "Tutorial (9/12)"
        );
        HideHighlight();
        if (tutorialOrangeScholar != null)
        {
            HighlightPiece(tutorialOrangeScholar);
            PointArrowAt(tutorialOrangeScholar);
        }
        yield return WaitForNext();

        // Step 9: Capturing
        ShowInstruction(
            "Some pieces CAN capture, others CANNOT.\n\nHand & Mind: NO capture\nTechnician & Apprentice: YES capture",
            "Tutorial (10/12)"
        );
        HideHighlight();
        yield return WaitForNext();

        // Step 10: Turn structure
        ShowInstruction(
            "On your turn:\n1. Move a piece OR\n2. Rotate rings (if you have Scholar/Sage)",
            "Tutorial (11/12)"
        );
        yield return WaitForNext();

        // Step 11: Victory condition reminder
        ShowInstruction(
            "Remember: complete a winning combination, or capture both enemy commanders!\n\nNow you're ready to play! Good luck!",
            "Tutorial (12/12)"
        );
        HighlightRing(ring4);
        yield return WaitForNext();

        // Tutorial complete
        CompleteTutorial();
    }

    void ShowInstruction(string message, string stepCounter)
    {
        if (instructionText != null)
        {
            instructionText.text = message;
        }

        if (stepCounterText != null)
        {
            stepCounterText.text = stepCounter;
        }

        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(true);
        }

        // Play instruction sound
        SoundManager.Instance?.PlayButtonHover();
    }

    void HighlightRing(GameObject ring)
    {
        if (ring == null || highlightCircle == null) return;

        highlightCircle.gameObject.SetActive(true);
        highlightCircle.transform.position = ring.transform.position;
        highlightCircle.transform.localScale = ring.transform.localScale * 1.2f;
        
        if (pulse != null) StopCoroutine(pulse);
        pulse = StartCoroutine(PulseHighlight());
    }

    void HighlightPiece(GameObject piece)
    {
        if (piece == null || highlightCircle == null) return;

        highlightCircle.gameObject.SetActive(true);
        highlightCircle.transform.position = piece.transform.position;
        highlightCircle.transform.localScale = Vector3.one * 2f;
        
        if (pulse != null) StopCoroutine(pulse);
        pulse = StartCoroutine(PulseHighlight());
    }

    void HideHighlight()
    {
        if (highlightCircle != null)
        {
            highlightCircle.gameObject.SetActive(false);
        }

        if (arrowPointer != null)
        {
            arrowPointer.SetActive(false);
        }

        if (pulse != null) { StopCoroutine(pulse); pulse = null; }
    }

    void PointArrowAt(GameObject target)
    {
        if (arrowPointer == null || target == null) return;

        arrowPointer.SetActive(true);
        arrowPointer.transform.position = target.transform.position + Vector3.up * 3f;
        
        // Point arrow down at target
        arrowPointer.transform.rotation = Quaternion.Euler(0, 0, 180);
    }

    IEnumerator PulseHighlight()
    {
        Vector3 baseline = highlightCircle.transform.localScale;
        while (highlightCircle != null && highlightCircle.gameObject.activeSelf)
        {
            float scale = 1f + Mathf.Sin(Time.time * highlightPulseSpeed) * 0.1f;
            highlightCircle.transform.localScale = baseline * scale;
            
            float alpha = 0.3f + Mathf.Sin(Time.time * highlightPulseSpeed) * 0.2f;
            Color color = highlightCircle.color;
            color.a = alpha;
            highlightCircle.color = color;
            
            yield return null;
        }
    }

    IEnumerator WaitForNext()
    {
        stepCompleted = false;

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(true);
        }

        while (!stepCompleted)
        {
            yield return null;
        }

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(false);
        }
    }

    IEnumerator WaitForPlayerToSelectPiece(GameObject piece)
    {
        stepCompleted = false;

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(true);
            Text btnText = nextButton.GetComponentInChildren<Text>();
            if (btnText != null)
            {
                btnText.text = "Skip";
            }
        }

        // Enable clicking for this piece only
        Chessman chessman = piece.GetComponent<Chessman>();
        if (chessman != null)
        {
            // Wait for player to click the piece or skip
            float timer = 0f;
            while (!stepCompleted && timer < 10f) // 10 second timeout
            {
                // Check if piece was clicked (move plates appeared)
                GameObject[] movePlates = GameObject.FindGameObjectsWithTag("MovePlate");
                if (movePlates.Length > 0)
                {
                    stepCompleted = true;
                    yield return new WaitForSeconds(2f); // Show moves for 2 seconds
                    
                    // Clean up move plates
                    foreach (GameObject plate in movePlates)
                    {
                        Destroy(plate);
                    }
                }

                timer += Time.deltaTime;
                yield return null;
            }
        }

        if (nextButton != null)
        {
            Text btnText = nextButton.GetComponentInChildren<Text>();
            if (btnText != null)
            {
                btnText.text = "Next";
            }
        }
    }

    public void NextStep()
    {
        stepCompleted = true;
        SoundManager.Instance?.PlayButtonClick();
    }

    public void SkipTutorial()
    {
        SoundManager.Instance?.PlayButtonClick();
        StopAllCoroutines();
        SceneManager.LoadScene("MainMenu");
    }

    void CompleteTutorial()
    {
        HideHighlight();
        
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
        }

        // Show completion message
        StartCoroutine(ShowCompletionMessage());
    }

    IEnumerator ShowCompletionMessage()
    {
        ShowInstruction(
            "Tutorial Complete! 🎉\n\nYou're ready to play!\n\nReturning to menu...",
            "Done!"
        );

        yield return new WaitForSeconds(3f);

        SceneManager.LoadScene("MainMenu");
    }
}