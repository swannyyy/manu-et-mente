using UnityEngine;
using UnityEngine.UI;

public class RotationButtonManager : MonoBehaviour
{
    [Header("Button References")]
    public Button btnAaACW;
    public Button btnAaCW;
    public Button btnBbACW;
    public Button btnBbCW;
    public Button btnCcACW;
    public Button btnCcCW;
    public Button btnDACW;
    public Button btnDCW;

    [Header("Colors")]
    public Color activeColor = Color.white;

    private game gameController;
    private GameObject selectedPiece = null;

    void Start()
    {
        gameController = GameObject.FindGameObjectWithTag("GameController").GetComponent<game>();
        
        // Add click listeners
        if (btnAaACW != null) btnAaACW.onClick.AddListener(() => OnRotateClick(1, false));
        if (btnAaCW != null) btnAaCW.onClick.AddListener(() => OnRotateClick(1, true));
        if (btnBbACW != null) btnBbACW.onClick.AddListener(() => OnRotateClick(2, false));
        if (btnBbCW != null) btnBbCW.onClick.AddListener(() => OnRotateClick(2, true));
        if (btnCcACW != null) btnCcACW.onClick.AddListener(() => OnRotateClick(3, false));
        if (btnCcCW != null) btnCcCW.onClick.AddListener(() => OnRotateClick(3, true));
        if (btnDACW != null) btnDACW.onClick.AddListener(() => OnRotateClick(4, false));
        if (btnDCW != null) btnDCW.onClick.AddListener(() => OnRotateClick(4, true));

        // Start with all buttons hidden
        HideAllButtons();
    }

    void Update()
    {
        // Check if a Scholar or Sage is selected
        CheckSelectedPiece();
    }

    private void CheckSelectedPiece()
    {
        // Check if there's a selected piece by looking for move plates
        GameObject[] movePlates = GameObject.FindGameObjectsWithTag("MovePlate");
        
        if (movePlates.Length > 0)
        {
            // Find the piece that created the move plates
            MovePlate plate = movePlates[0].GetComponent<MovePlate>();
            if (plate != null)
            {
                GameObject piece = plate.GetReference();
                if (piece != null)
                {
                    Chessman chessman = piece.GetComponent<Chessman>();
                    if (chessman != null)
                    {
                        string pieceType = chessman.pieceType;
                        
                        // Check if it's Scholar or Sage
                        if (pieceType == "S" || pieceType == "W")
                        {
                            selectedPiece = piece;
                            ShowButtonsForRing(chessman.GetRingNumber());
                            return;
                        }
                    }
                }
            }
        }
        
        // No valid piece selected
        selectedPiece = null;
        HideAllButtons();
    }

    private void ShowButtonsForRing(int ringNumber)
    {
        // First, hide all buttons
        HideAllButtons();
        
        // Determine which ring pair based on ring number
        int ringPair = GetRingPair(ringNumber);
        
        // Show only the buttons for this ring pair
        switch (ringPair)
        {
            case 1: // Aa (rings 1-2)
                ShowButton(btnAaACW);
                ShowButton(btnAaCW);
                break;
            case 2: // Bb (rings 3-4)
                ShowButton(btnBbACW);
                ShowButton(btnBbCW);
                break;
            case 3: // Cc (rings 5-6)
                ShowButton(btnCcACW);
                ShowButton(btnCcCW);
                break;
            case 4: // D (ring 7)
                ShowButton(btnDACW);
                ShowButton(btnDCW);
                break;
        }
    }

    private int GetRingPair(int ringNumber)
    {
        // Map ring number to ring pair
        // 1,2 → Aa (pair 1)
        // 3,4 → Bb (pair 2)
        // 5,6 → Cc (pair 3)
        // 7   → D  (pair 4)
        
        if (ringNumber <= 2) return 1;
        else if (ringNumber <= 4) return 2;
        else if (ringNumber <= 6) return 3;
        else return 4;
    }

    private void HideAllButtons()
    {
        HideButton(btnAaACW);
        HideButton(btnAaCW);
        HideButton(btnBbACW);
        HideButton(btnBbCW);
        HideButton(btnCcACW);
        HideButton(btnCcCW);
        HideButton(btnDACW);
        HideButton(btnDCW);
    }

    private void ShowButton(Button button)
    {
        if (button != null)
        {
            button.gameObject.SetActive(true);
            button.interactable = true;
            
            // Set color to active
            ColorBlock colors = button.colors;
            colors.normalColor = activeColor;
            colors.highlightedColor = activeColor;
            colors.selectedColor = activeColor;
            button.colors = colors;
        }
    }

    private void HideButton(Button button)
    {
        if (button != null)
        {
            button.gameObject.SetActive(false);
        }
    }

    private void OnRotateClick(int ringPair, bool clockwise)
    {
         SoundManager.Instance?.PlayButtonClick();
        // Verify that the clicked ring pair matches the selected piece's ring
        if (selectedPiece != null)
        {
            Chessman chessman = selectedPiece.GetComponent<Chessman>();
            int pieceRingPair = GetRingPair(chessman.GetRingNumber());
            
            if (pieceRingPair != ringPair)
            {
                Debug.LogWarning($"Cannot rotate ring pair {ringPair} when piece is on ring pair {pieceRingPair}");
                return;
            }
        }
        
        if (gameController != null)
        {
            // Destroy all move plates since this counts as the move
            GameObject[] movePlates = GameObject.FindGameObjectsWithTag("MovePlate");
            foreach (GameObject plate in movePlates)
            {
                Destroy(plate);
            }
            
            // Perform the rotation (this also calls NextTurn)
            gameController.RotateRing(ringPair, clockwise);
            
            // Hide buttons
            HideAllButtons();
            selectedPiece = null;
        }
    }
}