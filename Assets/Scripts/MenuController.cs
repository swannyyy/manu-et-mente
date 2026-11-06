// SPDX-License-Identifier: MPL-2.0
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


public class MenuController : MonoBehaviour
{
    [Header("Main Menu Buttons")]
    public Button startButton;
    public Button TutorialButton;
    public Button playAIButton;
    public Button quitButton;
    
    [Header("Difficulty Selection Panel")]
    public GameObject difficultyPanel;
    public Button easyButton;
    public Button mediumButton;
    public Button hardButton;
    public Button backButton;
    
    [Header("Optional - Difficulty Descriptions")]
    public Text difficultyDescriptionText;

    [Header("Online Play Panel")]
    public Button onlineButton;          // opens the online panel from the main menu
    public GameObject onlinePanel;
    public Button createGameButton;
    public Button joinGameButton;
    public Button quickMatchButton;      // find a random opponent (matchmaking)
    public Button cancelSearchButton;    // cancel an in-progress random search
    public Button onlineBackButton;
    public InputField joinCodeInput;     // where the joiner types the room code
    public Text codeDisplayText;         // shows the created room code to share
    public Text onlineStatusText;        // connection / room status
    public Text profileInfoText;         // shows the local nickname + points on the online panel

    [Header("Nickname Panel (first-run prompt)")]
    public GameObject nicknamePanel;     // shown the first time Online is pressed
    public InputField nicknameInput;     // where the player types their nickname
    public Button nicknameConfirmButton; // saves the nickname and opens the online panel
    public Text nicknameErrorText;       // validation message ("Enter a nickname.")

    void Start()
    {
        // Hide difficulty panel initially
        if (difficultyPanel != null)
        {
            difficultyPanel.SetActive(false);
        }

        if (onlinePanel != null)
        {
            onlinePanel.SetActive(false);
        }

        if (nicknamePanel != null)
        {
            nicknamePanel.SetActive(false);
        }

        // Setup main menu buttons
        SetupMainMenuButtons();

        // Setup difficulty selection buttons
        SetupDifficultyButtons();

        // Setup online play buttons
        SetupOnlineButtons();
    }

    void Update()
    {
        if (onlinePanel == null || !onlinePanel.activeSelf) return;

        // Mirror the network layer's status into the UI while the online panel is open.
        if (onlineStatusText != null)
            onlineStatusText.text = NetworkController.StatusMessage;

        // While searching for a random opponent, surface Cancel and lock the other online actions.
        bool searching = NetworkController.IsMatchmaking;
        if (cancelSearchButton != null) cancelSearchButton.gameObject.SetActive(searching);
        if (quickMatchButton != null) quickMatchButton.interactable = !searching;
        if (createGameButton != null) createGameButton.interactable = !searching;
        if (joinGameButton != null) joinGameButton.interactable = !searching;
    }
    
    private void SetupMainMenuButtons()
    {
        if (startButton != null)
        {
            startButton.onClick.AddListener(() => {
                SoundManager.Instance?.PlayButtonClick();
                StartGame();
            });
        }
        
        if (TutorialButton != null)
        {
            TutorialButton.onClick.AddListener(() => {
                SoundManager.Instance?.PlayButtonClick();
                OpenTutorial();
            });
        }
        
        if (playAIButton != null)
        {
            playAIButton.onClick.AddListener(() => {
                SoundManager.Instance?.PlayButtonClick();
                ShowDifficultySelection();
            });
        }
        
        if (quitButton != null)
        {
            quitButton.onClick.AddListener(() => {
                SoundManager.Instance?.PlayButtonClick();
                QuitGame();
            });
        }
    }
    
    private void SetupDifficultyButtons()
    {
        if (easyButton != null)
        {
            easyButton.onClick.AddListener(() => {
                SoundManager.Instance?.PlayButtonClick();
                SelectDifficulty(AIAgent.DifficultyLevel.Easy);
            });
        }
        
        if (mediumButton != null)
        {
            mediumButton.onClick.AddListener(() => {
                SoundManager.Instance?.PlayButtonClick();
                SelectDifficulty(AIAgent.DifficultyLevel.Medium);
            });
        }
        
        if (hardButton != null)
        {
            hardButton.onClick.AddListener(() => {
                SoundManager.Instance?.PlayButtonClick();
                SelectDifficulty(AIAgent.DifficultyLevel.Hard);
            });
        }
        
        if (backButton != null)
        {
            backButton.onClick.AddListener(() => {
                SoundManager.Instance?.PlayButtonClick();
                HideDifficultySelection();
            });
        }
    }
    
    private void SetupOnlineButtons()
    {
        if (onlineButton != null)
        {
            onlineButton.onClick.AddListener(() => {
                SoundManager.Instance?.PlayButtonClick();
                OnOnlinePressed();
            });
        }

        if (nicknameConfirmButton != null)
        {
            nicknameConfirmButton.onClick.AddListener(() => {
                SoundManager.Instance?.PlayButtonClick();
                ConfirmNickname();
            });
        }

        if (createGameButton != null)
        {
            createGameButton.onClick.AddListener(() => {
                SoundManager.Instance?.PlayButtonClick();
                CreateOnlineGame();
            });
        }

        if (joinGameButton != null)
        {
            joinGameButton.onClick.AddListener(() => {
                SoundManager.Instance?.PlayButtonClick();
                JoinOnlineGame();
            });
        }

        if (quickMatchButton != null)
        {
            quickMatchButton.onClick.AddListener(() => {
                SoundManager.Instance?.PlayButtonClick();
                QuickMatchGame();
            });
        }

        if (cancelSearchButton != null)
        {
            cancelSearchButton.onClick.AddListener(() => {
                SoundManager.Instance?.PlayButtonClick();
                CancelSearch();
            });
        }

        if (onlineBackButton != null)
        {
            onlineBackButton.onClick.AddListener(() => {
                SoundManager.Instance?.PlayButtonClick();
                HideOnlinePanel();
            });
        }
    }

    // First time Online is pressed (no saved profile) we prompt for a nickname; afterwards we go
    // straight to the online panel. The nickname is stored locally and labels this device's player.
    private void OnOnlinePressed()
    {
        if (PlayerProfile.HasProfile) ShowOnlinePanel();
        else ShowNicknamePanel();
    }

    private void ShowNicknamePanel()
    {
        if (nicknamePanel != null) nicknamePanel.SetActive(true);
        if (nicknameInput != null) nicknameInput.text = "";
        if (nicknameErrorText != null) nicknameErrorText.text = "";
    }

    private void ConfirmNickname()
    {
        string nick = nicknameInput != null ? nicknameInput.text : "";
        if (string.IsNullOrWhiteSpace(nick))
        {
            if (nicknameErrorText != null) nicknameErrorText.text = "Enter a nickname.";
            return;
        }
        PlayerProfile.Create(nick);
        if (nicknamePanel != null) nicknamePanel.SetActive(false);
        ShowOnlinePanel();
    }

    public void ShowOnlinePanel()
    {
        if (onlinePanel != null) onlinePanel.SetActive(true);
        if (codeDisplayText != null) codeDisplayText.text = "";
        if (cancelSearchButton != null) cancelSearchButton.gameObject.SetActive(false);
        if (profileInfoText != null)
            profileInfoText.text = PlayerProfile.Nickname + " — " + PlayerProfile.Points + " pts";
    }

    public void HideOnlinePanel()
    {
        // Don't leave the player silently queued if they back out mid-search.
        if (NetworkController.IsMatchmaking && NetworkController.Instance != null)
            NetworkController.Instance.CancelMatchmaking();
        if (onlinePanel != null) onlinePanel.SetActive(false);
    }

    // Creator hosts a room under a short shareable code and becomes orange.
    public void CreateOnlineGame()
    {
        string code = GenerateRoomCode();
        if (codeDisplayText != null) codeDisplayText.text = "Code: " + code;
        NetworkLayer().CreateGame(code);
    }

    // Joiner enters the creator's code and becomes green.
    public void JoinOnlineGame()
    {
        string code = joinCodeInput != null ? joinCodeInput.text : "";
        if (string.IsNullOrWhiteSpace(code))
        {
            if (onlineStatusText != null) onlineStatusText.text = "Enter a room code first.";
            return;
        }
        NetworkLayer().JoinGame(code);
    }

    // Matchmaking: pair up with a random opponent. Stays on this panel showing "Searching..." with
    // a Cancel button until someone is matched, then drops into the board.
    public void QuickMatchGame()
    {
        if (codeDisplayText != null) codeDisplayText.text = "";
        NetworkLayer().QuickMatch();
    }

    public void CancelSearch()
    {
        if (NetworkController.Instance != null) NetworkController.Instance.CancelMatchmaking();
    }

    // Ambiguous-looking characters (0/O, 1/I) excluded so codes are easy to read aloud.
    private static string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var sb = new System.Text.StringBuilder(4);
        for (int i = 0; i < 4; i++)
            sb.Append(chars[Random.Range(0, chars.Length)]);
        return sb.ToString();
    }

    // The NetworkController is a DontDestroyOnLoad singleton; create one on demand if absent.
    private NetworkController NetworkLayer()
    {
        if (NetworkController.Instance != null) return NetworkController.Instance;
        GameObject go = new GameObject("NetworkController");
        return go.AddComponent<NetworkController>();
    }

    public void ShowDifficultySelection()
    {
        if (difficultyPanel != null)
        {
            difficultyPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Difficulty panel not assigned! Starting with Medium difficulty.");
            SelectDifficulty(AIAgent.DifficultyLevel.Medium);
        }
    }
    
    public void HideDifficultySelection()
    {
        if (difficultyPanel != null)
        {
            difficultyPanel.SetActive(false);
        }
    }
    
    public void SelectDifficulty(AIAgent.DifficultyLevel difficulty)
    {
        PlayerPrefs.SetInt("AIDifficulty", (int)difficulty);
        PlayerPrefs.Save();
        
        Debug.Log($"Difficulty selected: {difficulty}");
        StartAIGame();
    }
    
    public void StartAIGame()
    {
        NetworkController.GoOffline();
        SceneManager.LoadScene("GameAI");
    }

    public void StartGame()
    {
        NetworkController.GoOffline();
        SceneManager.LoadScene("Game");
    }

    public void OpenTutorial()
    {
        NetworkController.GoOffline();
        SceneManager.LoadScene("Tutorial");
    }

    public void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}