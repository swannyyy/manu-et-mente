// SPDX-License-Identifier: MPL-2.0
//
// GameHistory — every finished game (vs AI, online, same-device) with its full move list, stored
// as JSON in Application.persistentDataPath/manu_history.json. Feeds the analysis screen and the
// statistics page. Practice lessons and AI-vs-AI test runs are not recorded.

using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GameEntry
{
    public string id;
    public string date;          // ISO 8601 local time
    public string mode;          // "ai" | "online" | "local"
    public string difficulty;    // AI games: "Easy" | "Medium" | "Hard"
    public string myColor;       // "orange" | "green" ("" for same-device games)
    public string opponent;      // online: opponent nickname (may arrive after the game ends)
    public string winner;        // "orange" | "green" | "draw"
    public string victory;       // game.VictoryType name
    public int points;           // online: player points after the game
    public List<string> moves = new List<string>();

    // Filled in once the game has been analysed.
    public bool analysed;
    public float accOrange = -1, accGreen = -1;
    public int[] errOrange = new int[3], errGreen = new int[3]; // inaccuracies, mistakes, blunders

    public int Plies => moves != null ? moves.Count : 0;

    // "win" | "loss" | "draw" from the local player's point of view; "" for same-device games.
    public string MyResult
    {
        get
        {
            if (winner == "draw") return "draw";
            if (string.IsNullOrEmpty(myColor)) return "";
            return winner == myColor ? "win" : "loss";
        }
    }

    public float MyAccuracy => myColor == "orange" ? accOrange : myColor == "green" ? accGreen : -1;
    public int[] MyErrors => myColor == "orange" ? errOrange : errGreen;

    public string ModeLabel =>
        mode == "ai" ? "vs AI · " + difficulty :
        mode == "online" ? "Online" + (string.IsNullOrEmpty(opponent) ? "" : " · " + opponent) :
        "Same device";

    public DateTime When
    {
        get { DateTime d; return DateTime.TryParse(date, null, System.Globalization.DateTimeStyles.RoundtripKind, out d) ? d : DateTime.MinValue; }
    }
}

public static class GameHistory
{
    [Serializable] class FileData { public List<GameEntry> games = new List<GameEntry>(); }

    public const int MaxGames = 500;
    static FileData data;

    static string PathName => System.IO.Path.Combine(Application.persistentDataPath, "manu_history.json");

    static FileData Data
    {
        get
        {
            if (data != null) return data;
            data = new FileData();
            try
            {
                if (System.IO.File.Exists(PathName))
                    data = JsonUtility.FromJson<FileData>(System.IO.File.ReadAllText(PathName)) ?? new FileData();
            }
            catch (Exception e) { Debug.LogWarning("GameHistory: could not read history (" + e.Message + ")"); }
            if (data.games == null) data.games = new List<GameEntry>();
            return data;
        }
    }

    // Newest last.
    public static IReadOnlyList<GameEntry> All => Data.games;

    public static GameEntry Last => Data.games.Count > 0 ? Data.games[Data.games.Count - 1] : null;

    public static void Add(GameEntry e)
    {
        Data.games.Add(e);
        if (Data.games.Count > MaxGames) Data.games.RemoveRange(0, Data.games.Count - MaxGames);
        Save();
    }

    public static void Save()
    {
        try { System.IO.File.WriteAllText(PathName, JsonUtility.ToJson(Data)); }
        catch (Exception e) { Debug.LogWarning("GameHistory: could not save history (" + e.Message + ")"); }
    }

    // The opponent's nickname only arrives over the network after the result; attach it then.
    public static void SetLastOnlineOpponent(string nickname)
    {
        GameEntry e = Last;
        if (e == null || e.mode != "online" || string.IsNullOrEmpty(nickname)) return;
        if ((DateTime.Now - e.When).TotalMinutes > 5) return;
        e.opponent = nickname;
        Save();
    }

    // Called by game.cs when a game ends.
    public static GameEntry RecordFinished(game g, string winner)
    {
        if (g == null || g.IsTraining) return null;
        if (GameUX.Instance != null && GameUX.Instance.Learning) return null;
        if (PuzzleUX.Active) return null;
        if (g.MoveCodes.Count == 0) return null;

        var e = new GameEntry
        {
            id = Guid.NewGuid().ToString("N"),
            date = DateTime.Now.ToString("o"),
            winner = winner,
            victory = g.LastVictoryType.ToString(),
            moves = new List<string>(g.MoveCodes)
        };

        AIAgent ai = UnityEngine.Object.FindAnyObjectByType<AIAgent>();
        if (NetworkController.IsOnline)
        {
            e.mode = "online";
            e.myColor = NetworkController.LocalColor;
            e.opponent = ""; // arrives after the result (SetLastOnlineOpponent); the static still holds the previous match's name
            e.points = PlayerProfile.Points;
        }
        else if (ai != null)
        {
            if (ai.aiVsAiTest) return null;
            e.mode = "ai";
            e.difficulty = ai.difficulty.ToString();
            e.myColor = ai.PlayerColor == "orange" ? "green" : "orange";
        }
        else
        {
            e.mode = "local";
            e.myColor = "";
        }
        Add(e);
        return e;
    }
}
