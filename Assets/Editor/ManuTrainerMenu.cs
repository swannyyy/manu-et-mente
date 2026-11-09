// SPDX-License-Identifier: MPL-2.0
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using ManuAI;

// Editor menu for training and benchmarking the AI without entering Play mode.
public static class ManuTrainerMenu
{
    static CancellationTokenSource cts;
    static Task running;

    [MenuItem("Manu AI/Train AI (continue from current weights)")]
    static void TrainContinue() { Start(AIAgent.LoadWeights()); }

    [MenuItem("Manu AI/Train AI (from scratch)")]
    static void TrainScratch() { Start(Eval.PriorWeights()); }

    [MenuItem("Manu AI/Cancel Training")]
    static void Cancel()
    {
        if (cts != null) { cts.Cancel(); Debug.Log("[ManuTrainer] Cancelling after the current game batch..."); }
    }

    [MenuItem("Manu AI/Delete Resources Weights (use baked weights)")]
    static void DeleteResourceWeights()
    {
        string path = "Assets/Resources/" + ManuWeights.ResourceName + ".txt";
        if (AssetDatabase.DeleteAsset(path)) Debug.Log("Deleted " + path + "; AIAgent will use ManuWeights.cs.");
        else Debug.Log("No " + path + " to delete.");
    }

    // Testing aid: plays a quick engine-vs-engine game and stores it in the game history, so the
    // statistics page and the analysis screen have something to show.
    [MenuItem("Manu AI/Add sample game to history (testing)")]
    static void AddSampleGame()
    {
        var w = AIAgent.LoadWeights();
        var rng = new System.Random();
        var a = new Searcher(w) { MaxDepth = 2, TimeLimitMs = 2000, RootNoise = 20, Rng = rng };
        var b = Board.Initial();
        var codes = new System.Collections.Generic.List<string>();
        while (b.Winner < 0 && codes.Count < 300)
        {
            var r = a.Search(b);
            if (r.Best.IsNone) break;
            codes.Add(MoveCodec.Encode(r.Best));
            ManuAI.Undo u = new ManuAI.Undo(); b.Make(r.Best, ref u);
        }
        var e = new GameEntry
        {
            id = System.Guid.NewGuid().ToString("N"),
            date = System.DateTime.Now.ToString("o"),
            mode = "ai", difficulty = "Medium", myColor = "orange",
            winner = b.Winner < 0 ? "draw" : Pc.ColorName(b.Winner),
            victory = b.Winner < 0 ? "Draw" : b.WinType.ToString(),
            moves = codes
        };
        GameHistory.Add(e);
        Debug.Log("Added sample game to history: " + codes.Count + " moves, " + e.winner + " (" + e.victory + ").");
    }

    static void Start(double[] startWeights)
    {
        if (running != null && !running.IsCompleted)
        {
            Debug.LogWarning("[ManuTrainer] Training is already running.");
            return;
        }
        cts = new CancellationTokenSource();
        var settings = new TrainerSettings { Rounds = 4, SelfPlayGamesPerRound = 200, GateGames = 100, Seed = Random.Range(1, int.MaxValue / 2) };
        string outPath = System.IO.Path.Combine(Application.dataPath, "Resources", ManuWeights.ResourceName + ".txt");
        running = SelfPlayTrainer.RunInBackground(startWeights, settings, outPath, cts.Token);
        running.ContinueWith(_ => EditorApplication.delayCall += AssetDatabase.Refresh);
    }
}
