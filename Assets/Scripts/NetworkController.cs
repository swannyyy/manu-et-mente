// SPDX-License-Identifier: MPL-2.0
using UnityEngine;
using UnityEngine.SceneManagement;

// Online PvP transport for Manu et Mente.
//
// The game is turn-based and fully deterministic given a move command, so instead of syncing
// GameObjects we relay tiny commands and replay them with the SAME local game code on the other
// machine (lockstep). A piece move is just (fromRing, fromPlace, toRing, toPlace); a rotation is
// (ringPair, clockwise). Replaying through MovePlate.OnMouseUp regenerates the move plates, so
// captures and en passant are re-derived deterministically and never need to be transmitted.
//
// Backend: Photon PUN 2 (free tier). Matchmaking: room-code create/join (creator = orange/master,
// joiner = green). The PUN-dependent code is guarded by PHOTON_UNITY_NETWORKING (a define PUN's
// installer adds) so the project still compiles before the SDK is imported.

#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

public class NetworkController : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public const byte MOVE_EVENT = 1;
    public const byte ROTATE_EVENT = 2;
    public const byte RESULT_EVENT = 3;  // carries the sender's nickname + updated points after a match

    [Tooltip("Scene loaded once a room is joined. Reuses the local Game scene; lockstep replays the same code path on both clients.")]
    public string onlineSceneName = "Game";
    [Tooltip("Returned-to scene when the match ends or the opponent leaves.")]
    public string menuSceneName = "MainMenu";

    // --- Statics read by Chessman / MovePlate / game without a hard reference ---
    public static NetworkController Instance;
    public static bool IsOnline = false;
    public static string LocalColor = "orange";     // "orange" (creator) | "green" (joiner)
    public static bool IsApplyingRemote = false;    // true while replaying an incoming command
    public static bool MatchReady = false;          // true once both players are in the room
    public static bool IsMatchmaking = false;       // true while searching for a RANDOM opponent
    public static string StatusMessage = "";        // human-readable connection/room status

    // --- Post-match ranking display (online only) ---
    public static string RoomCode = "";
    public static bool SessionInterrupted;
    public static string LastResultMessage = "";    // "You won! +30 (now 530)" etc., for the victory panel
    public static string OpponentNickname = "";      // opponent's nickname, received at match end
    public static int OpponentPoints = 0;            // opponent's updated points, received at match end

    private game gameController;
    private readonly RaiseEventOptions toOthers = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
    private readonly SendOptions reliable = SendOptions.SendReliable;

    void Awake()
    {
        // Single persistent instance that survives the load into the online scene.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Re-acquire the board after each scene load (the controller lives in the scene, not here).
        gameController = FindAnyObjectByType<game>();
    }

    private void EnsureGameController()
    {
        if (gameController == null) gameController = FindAnyObjectByType<game>();
    }

    // ---------------- Lobby / matchmaking ----------------

    public void Connect()
    {
        if (PhotonNetwork.IsConnected) return;
        StatusMessage = "Connecting...";
        PhotonNetwork.AutomaticallySyncScene = false; // each client loads the board itself
        PhotonNetwork.ConnectUsingSettings();
    }

    public void CreateGame(string code)
    {
        SessionInterrupted = false;
        pendingCode = NormalizeCode(code);
        pendingCreate = true;
        pendingRandom = false;
        StartPending();
    }

    public void JoinGame(string code)
    {
        SessionInterrupted = false;
        pendingCode = NormalizeCode(code);
        pendingCreate = false;
        pendingRandom = false;
        StartPending();
    }

    // Random matchmaking: join any open 2-player room, or create one and wait for a stranger.
    public void QuickMatch()
    {
        SessionInterrupted = false;
        pendingCode = null;
        pendingCreate = false;
        pendingRandom = true;
        IsMatchmaking = true;
        StatusMessage = "Searching for opponent...";
        StartPending();
    }

    // Runs the queued action as soon as we're in a lobby. Handles all three connection states:
    // fully ready (act now), connected to Master but not in a lobby (rejoin lobby, e.g. right after
    // a cancelled search left a room), or not connected at all (connect from scratch).
    private void StartPending()
    {
        if (PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InLobby) DoPending();
        else if (PhotonNetwork.IsConnectedAndReady) PhotonNetwork.JoinLobby();
        else Connect();
    }

    // Cancel an in-progress random search (only valid before an opponent has been matched). Leaves
    // any room we created while waiting and returns to the idle, offline state on the menu.
    public void CancelMatchmaking()
    {
        pendingRandom = false;
        IsMatchmaking = false;
        IsOnline = false;
        MatchReady = false;
        StatusMessage = "Search cancelled.";
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
    }

    private string pendingCode = null;
    private bool pendingCreate = false;
    private bool pendingRandom = false;

    private static string NormalizeCode(string code)
    {
        return string.IsNullOrWhiteSpace(code) ? "" : code.Trim().ToUpperInvariant();
    }

    private void DoPending()
    {
        if (pendingRandom)
        {
            pendingRandom = false;
            StatusMessage = "Searching for opponent...";
            PhotonNetwork.JoinRandomOrCreateRoom(expectedMaxPlayers: 2,
                roomOptions: new RoomOptions { MaxPlayers = 2 });
            return;
        }

        if (string.IsNullOrEmpty(pendingCode)) return;

        if (pendingCreate)
        {
            StatusMessage = "Creating room " + pendingCode + "...";
            PhotonNetwork.CreateRoom(pendingCode, new RoomOptions { MaxPlayers = 2 });
        }
        else
        {
            StatusMessage = "Joining room " + pendingCode + "...";
            PhotonNetwork.JoinRoom(pendingCode);
        }
        pendingCode = null;
    }

    public override void OnConnectedToMaster()
    {
        StatusMessage = "Connected. Entering lobby...";
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        DoPending();
    }

    public override void OnJoinedRoom()
    {
        RoomCode = PhotonNetwork.CurrentRoom.Name;
        SessionInterrupted = false;
        LocalColor = PhotonNetwork.IsMasterClient ? "orange" : "green";
        IsOnline = true;
        MatchReady = PhotonNetwork.CurrentRoom.PlayerCount >= 2;

        // Random matchmaking that created a fresh room: keep waiting on the menu's "Searching..."
        // screen (with Cancel) until a stranger joins, instead of dropping into an empty board.
        if (IsMatchmaking && !MatchReady)
        {
            StatusMessage = "Searching for opponent...";
            return;
        }

        IsMatchmaking = false;
        StatusMessage = MatchReady ? "Opponent found. Starting..."
                                   : "Waiting for opponent. Share code: " + PhotonNetwork.CurrentRoom.Name;

        // Both clients load the board locally; lockstep keeps them in sync.
        SceneManager.LoadScene(onlineSceneName);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        MatchReady = PhotonNetwork.CurrentRoom.PlayerCount >= 2;
        if (!MatchReady) return;

        StatusMessage = "Opponent joined.";

        // A matchmaking host was waiting on the menu; now that a stranger arrived, load the board.
        if (IsMatchmaking)
        {
            IsMatchmaking = false;
            SceneManager.LoadScene(onlineSceneName);
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        MatchReady = false;
        StatusMessage = "Opponent left the game.";
        SessionInterrupted = true;
        IsOnline = false;
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (IsOnline || IsMatchmaking) SessionInterrupted = true;
        StatusMessage = "Disconnected: " + cause;
        IsOnline = false;
        MatchReady = false;
        IsMatchmaking = false;
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        StatusMessage = "Create failed: " + message;
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        StatusMessage = "Join failed (wrong code?): " + message;
    }

    private void LeaveToMenu()
    {
        IsOnline = false;
        MatchReady = false;
        IsMatchmaking = false;
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
        SceneManager.LoadScene(menuSceneName);
    }

    // Clears all online state and leaves any room still open. Offline entry points (local PvP,
    // vs-AI, tutorial) MUST call this: the statics live on a DontDestroyOnLoad singleton, so an
    // abandoned online room (IsOnline=true, MatchReady=false) would otherwise keep the Chessman
    // ownership gate shut and block selection in the next offline game.
    public static void GoOffline()
    {
        SessionInterrupted = false;
        RoomCode = "";
        StatusMessage = "";
        IsOnline = false;
        MatchReady = false;
        IsMatchmaking = false;
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
    }

    // ---------------- Sending local commands ----------------

    public void SendMove(int fromRing, int fromPlace, int toRing, int toPlace)
    {
        PhotonNetwork.RaiseEvent(MOVE_EVENT,
            new int[] { fromRing, fromPlace, toRing, toPlace }, toOthers, reliable);
    }

    public void SendRotation(int ringPair, int clockwise)
    {
        PhotonNetwork.RaiseEvent(ROTATE_EVENT,
            new int[] { ringPair, clockwise }, toOthers, reliable);
    }

    // Called once per client when the match ends. Lockstep guarantees both clients computed the same
    // winnerColor and point value, so each just applies its own delta locally, then sends its updated
    // nickname/points to the other for opponent display. winnerColor "draw" awards nothing.
    public void ReportResult(string winnerColor, int points)
    {
        if (!IsOnline) return;

        int delta = winnerColor == "draw" ? 0
                  : winnerColor == LocalColor ? points
                  : -points;

        PlayerProfile.AddPoints(delta);

        LastResultMessage = delta > 0 ? $"You won! +{delta} (now {PlayerProfile.Points})"
                          : delta < 0 ? $"You lost. {delta} (now {PlayerProfile.Points})"
                          : $"Draw (now {PlayerProfile.Points})";

        PhotonNetwork.RaiseEvent(RESULT_EVENT,
            new object[] { PlayerProfile.Nickname, PlayerProfile.Points }, toOthers, reliable);
    }

    // ---------------- Receiving & replaying remote commands ----------------

    public void OnEvent(EventData photonEvent)
    {
        byte code = photonEvent.Code;

        if (code == RESULT_EVENT)
        {
            object[] r = photonEvent.CustomData as object[];
            if (r != null && r.Length >= 2)
            {
                OpponentNickname = r[0] as string ?? "";
                OpponentPoints = r[1] is int p ? p : 0;
                GameHistory.SetLastOnlineOpponent(OpponentNickname);
            }
            return;
        }

        if (code != MOVE_EVENT && code != ROTATE_EVENT) return;

        EnsureGameController();
        if (gameController == null) return;

        int[] data = photonEvent.CustomData as int[];
        if (data == null) return;

        IsApplyingRemote = true;
        try
        {
            if (code == MOVE_EVENT) ApplyRemoteMove(data[0], data[1], data[2], data[3]);
            else ApplyRemoteRotation(data[0], data[1] == 1);
        }
        finally
        {
            IsApplyingRemote = false;
        }
    }

    // Replays a piece move by reusing the exact local code path (mirrors AIAgent.ExecuteMove):
    // regenerate the piece's plates, find the one at the destination, and "click" it.
    private void ApplyRemoteMove(int fromRing, int fromPlace, int toRing, int toPlace)
    {
        GameObject pieceObj = gameController.GetPosition(fromRing, fromPlace);
        if (pieceObj == null) return;

        Chessman cm = pieceObj.GetComponent<Chessman>();
        if (cm == null) return;

        ClearAllMovePlates();
        cm.InitiateMovePlates();

        GameObject[] plates = GameObject.FindGameObjectsWithTag("MovePlate");
        foreach (GameObject plateObj in plates)
        {
            MovePlate plate = plateObj.GetComponent<MovePlate>();
            if (plate != null && plate.GetRingNumber() == toRing && plate.GetPlaceNumber() == toPlace)
            {
                plate.OnMouseUp();
                break;
            }
        }

        ClearAllMovePlates();
    }

    private void ApplyRemoteRotation(int ringPair, bool clockwise)
    {
        gameController.RotateRing(ringPair, clockwise);
    }

    // DestroyImmediate so stale plates can't leak into the next generation within the same frame
    // (matches AIAgent.ClearAllMovePlates).
    private void ClearAllMovePlates()
    {
        GameObject[] plates = GameObject.FindGameObjectsWithTag("MovePlate");
        foreach (GameObject p in plates)
        {
            if (p != null) DestroyImmediate(p);
        }
    }
}

#else // PHOTON_UNITY_NETWORKING not defined — stub so the project compiles without the PUN SDK.

public class NetworkController : MonoBehaviour
{
    public static NetworkController Instance;
    public static bool IsOnline = false;
    public static string LocalColor = "orange";
    public static bool IsApplyingRemote = false;
    public static bool MatchReady = false;
    public static bool IsMatchmaking = false;
    public static string StatusMessage = "Photon PUN 2 not installed.";
    public static string RoomCode = "";
    public static bool SessionInterrupted;
    public static string LastResultMessage = "";
    public static string OpponentNickname = "";
    public static int OpponentPoints = 0;

    void Awake()
    {
        Debug.LogWarning("NetworkController: Photon PUN 2 is not installed. Online play is disabled. " +
                         "Import 'PUN 2 - FREE' from the Asset Store and set an App ID.");
    }

    public static void GoOffline()
    {
        SessionInterrupted = false;
        RoomCode = "";
        StatusMessage = "";
        IsOnline = false;
        MatchReady = false;
    }

    public void Connect() { }
    public void CreateGame(string code) { }
    public void JoinGame(string code) { }
    public void QuickMatch() { }
    public void CancelMatchmaking() { }
    public void SendMove(int fromRing, int fromPlace, int toRing, int toPlace) { }
    public void SendRotation(int ringPair, int clockwise) { }
    public void ReportResult(string winnerColor, int points) { }
}

#endif
