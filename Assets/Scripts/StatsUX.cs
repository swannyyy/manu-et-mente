// SPDX-License-Identifier: MPL-2.0
//
// StatsUX — statistics page (main menu → Statistics): results against the AI per difficulty and
// online, how games were won and lost, streaks, accuracy from analysed games, and a list of recent
// games, each of which can be opened in the analysis screen.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StatsUX
{
    public static RectTransform Show(Transform parent)
    {
        var modal = UXUI.Modal(parent, "Statistics");

        // Scrollable area between the title and the Back button.
        var viewport = UXUI.Panel(modal, "Stats viewport", new Color(0, 0, 0, 0.01f));
        viewport.offsetMin = new Vector2(0, 110); viewport.offsetMax = new Vector2(0, -105);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = new GameObject("Stats content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
        content.offsetMin = content.offsetMax = Vector2.zero;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
        scroll.viewport = viewport;

        float y = 10;
        var games = GameHistory.All;
        var rated = games.Where(g => g.mode == "ai" || g.mode == "online").ToList();

        int puzzlesTried = PlayerPrefs.GetInt(PuzzleUX.KeyAttempted, 0);
        if (puzzlesTried > 0)
        {
            int solved = PlayerPrefs.GetInt(PuzzleUX.KeySolved, 0);
            y = Heading(content, "Puzzles", y);
            y = Text(content, "Solved first try: " + solved + " / " + puzzlesTried + " (" + Mathf.RoundToInt(100f * solved / puzzlesTried) + "%)\nCurrent streak " +
                     PlayerPrefs.GetInt(PuzzleUX.KeyStreak, 0) + " · best streak " + PlayerPrefs.GetInt(PuzzleUX.KeyBest, 0), y, 80);
        }

        if (games.Count == 0)
        {
            y = Text(content, "No games yet.\n\nFinished games against the AI, online and on the same device appear here, and each one can be analysed move by move.", y, 260, 26);
        }
        else
        {
            y = Heading(content, "Against the AI", y);
            foreach (var d in new[] { "Easy", "Medium", "Hard" })
            {
                var list = games.Where(g => g.mode == "ai" && g.difficulty == d).ToList();
                y = Text(content, d + " · " + Record(list), y);
            }

            y = Heading(content, "Online", y);
            var online = games.Where(g => g.mode == "online").ToList();
            y = Text(content, Record(online) + (PlayerProfile.HasProfile ? "\nRating points: " + PlayerProfile.Points : ""), y, PlayerProfile.HasProfile ? 80 : 46);

            int local = games.Count(g => g.mode == "local");
            if (local > 0) { y = Heading(content, "Same device", y); y = Text(content, local + " game" + (local == 1 ? "" : "s") + " played", y); }

            if (rated.Count > 0)
            {
                y = Heading(content, "Streaks (AI and online)", y);
                int cur = 0, best = 0, run = 0; string curKind = "";
                foreach (var g in rated)
                {
                    if (g.MyResult == "win") { run++; best = Math.Max(best, run); } else run = 0;
                }
                for (int i = rated.Count - 1; i >= 0; i--)
                {
                    string r = rated[i].MyResult;
                    if (curKind == "") curKind = r;
                    if (r != curKind) break;
                    cur++;
                }
                string kindName = curKind == "win" ? "win" : curKind == "loss" ? "loss" : "draw";
                y = Text(content, "Current: " + cur + " " + kindName + (cur == 1 ? "" : (kindName == "loss" ? "es" : "s")) + " · Best winning streak: " + best, y);

                y = Heading(content, "How you won", y);
                y = Text(content, Breakdown(rated.Where(g => g.MyResult == "win"), true), y, 0);
                y = Heading(content, "How you lost", y);
                y = Text(content, Breakdown(rated.Where(g => g.MyResult == "loss"), false), y, 0);
            }

            var analysed = games.Where(g => g.analysed && g.MyAccuracy >= 0).ToList();
            y = Heading(content, "Accuracy", y);
            if (analysed.Count == 0)
                y = Text(content, "Analyse a game to see your accuracy here.", y);
            else
            {
                float avg = analysed.Average(g => g.MyAccuracy);
                var recent = analysed.Skip(Math.Max(0, analysed.Count - 10)).ToList();
                float avgRecent = recent.Average(g => g.MyAccuracy);
                float ina = (float)analysed.Average(g => g.MyErrors[0]), mis = (float)analysed.Average(g => g.MyErrors[1]), blu = (float)analysed.Average(g => g.MyErrors[2]);
                y = Text(content, string.Format("Average {0:0}% over {1} analysed game{2} · last 10: {3:0}%\nPer game: {4:0.0} inaccuracies · {5:0.0} mistakes · {6:0.0} blunders",
                    avg, analysed.Count, analysed.Count == 1 ? "" : "s", avgRecent, ina, mis, blu), y, 80);
            }

            y = Heading(content, "Recent games", y);
            int shown = 0;
            for (int i = games.Count - 1; i >= 0 && shown < 40; i--, shown++)
                y = GameRow(content, games[i], y);
        }

        content.sizeDelta = new Vector2(0, y + 20);
        UXUI.Button(modal, "Back", 0, () => UnityEngine.Object.Destroy(modal.gameObject));
        // Pin the Back button to the bottom of the modal.
        var back = (RectTransform)modal.Find("Back");
        back.anchorMin = new Vector2(0, 0); back.anchorMax = new Vector2(1, 0); back.pivot = new Vector2(.5f, 0);
        back.offsetMin = new Vector2(24, 24); back.offsetMax = new Vector2(-24, 90);
        return modal;
    }

    static string Record(List<GameEntry> list)
    {
        if (list.Count == 0) return "no games yet";
        int w = list.Count(g => g.MyResult == "win"), l = list.Count(g => g.MyResult == "loss"), d = list.Count(g => g.MyResult == "draw");
        return list.Count + " game" + (list.Count == 1 ? "" : "s") + " · " + w + " W / " + l + " L / " + d + " D · " + Mathf.RoundToInt(100f * w / list.Count) + "% won";
    }

    // won: the list holds wins (a forfeit was the opponent leaving) or losses (the player left).
    static string Breakdown(IEnumerable<GameEntry> list, bool won)
    {
        var groups = list.GroupBy(g => g.victory).OrderByDescending(g => g.Count()).ToList();
        if (groups.Count == 0) return "–";
        return string.Join("\n", groups.Select(g => g.Count() + " × " + VictoryName(g.Key, won)));
    }

    public static string VictoryName(string victory, bool won = true)
    {
        game.VictoryType t;
        if (Enum.TryParse(victory, out t))
        {
            if (t == game.VictoryType.Draw) return "Draw (no piece move after 3 rotations)";
            if (t == game.VictoryType.Forfeit) return won ? "Opponent left the game" : "You left the game";
            string s = game.VictoryExplanation(t);
            if (!string.IsNullOrEmpty(s)) return s;
        }
        return string.IsNullOrEmpty(victory) ? "Unknown" : victory;
    }

    static float Heading(RectTransform content, string text, float y)
    {
        var t = UXUI.Label(content, text, y + 14, 44, 27);
        t.color = new Color(.95f, .78f, .45f);
        t.alignment = TextAnchor.MiddleLeft;
        return y + 62;
    }

    // height 0 = size to the number of lines.
    static float Text(RectTransform content, string text, float y, float height = 46, int size = 23)
    {
        if (height <= 0) height = 34 * (text.Split('\n').Length) + 10;
        var t = UXUI.Label(content, text, y, height, size);
        t.alignment = TextAnchor.UpperLeft;
        return y + height;
    }

    static float GameRow(RectTransform content, GameEntry g, float y)
    {
        var row = UXUI.Panel(content, "Game " + g.id, new Color(.12f, .19f, .22f));
        UXUI.Place(row, y, 92, 16);
        string result = g.MyResult == "win" ? "Won" : g.MyResult == "loss" ? "Lost" : g.MyResult == "draw" ? "Draw" :
                        g.winner == "draw" ? "Draw" : GameUX.Team(g.winner) + " won";
        string acc = g.analysed && g.MyAccuracy >= 0 ? " · accuracy " + Mathf.RoundToInt(g.MyAccuracy) + "%" : "";
        string date = g.When == DateTime.MinValue ? "" : g.When.ToString("d MMM HH:mm") + " · ";
        var t = UXUI.Label(row, date + g.ModeLabel + "\n" + result + " · " + g.Plies + " moves" + acc, 6, 80, 21);
        t.alignment = TextAnchor.MiddleLeft;
        t.rectTransform.offsetMax = new Vector2(-190, t.rectTransform.offsetMax.y);
        t.rectTransform.offsetMin = new Vector2(14, t.rectTransform.offsetMin.y);
        var b = UXUI.Button(row, "Analyse", 13, () => OpenAnalysis(g));
        var r = (RectTransform)b.transform;
        r.anchorMin = new Vector2(1, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(1, 1);
        r.sizeDelta = new Vector2(170, 66); r.anchoredPosition = new Vector2(-10, -13);
        return y + 102;
    }

    static void OpenAnalysis(GameEntry g)
    {
        AnalysisUX.Pending = g;
        NetworkController.GoOffline();
        SceneManager.LoadScene("Game");
    }
}
