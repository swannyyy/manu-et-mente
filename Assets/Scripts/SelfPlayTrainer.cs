// SPDX-License-Identifier: MPL-2.0
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using ManuAI;

// Self-play trainer for the AI's evaluation weights.
//
// Training runs entirely inside ManuEngine/ManuTrainer (pure C#, no GameObjects), on worker
// threads, so it is thousands of times faster than playing games on the real board. You can run
// it either from the editor menu (Manu AI > Train AI) or by enabling this component in a scene
// and entering Play mode. The result is written to Assets/Resources/ManuAIWeights.txt, which
// AIAgent loads automatically (it overrides the weights baked into ManuWeights.cs).
public class SelfPlayTrainer : MonoBehaviour
{
    [Header("Training Budget")]
    [Tooltip("Rounds of self-play -> tuning -> gating match.")]
    public int rounds = 4;
    [Tooltip("Self-play games per round (more = better tuning data, slower).")]
    public int selfPlayGamesPerRound = 200;
    [Tooltip("Games in each round's gating match (new weights vs current best).")]
    public int gateGames = 100;
    [Tooltip("Search depth used during training games. 3 is a good balance.")]
    public int searchDepth = 3;
    [Tooltip("Start from the current trained weights (true) or from the hand-written prior (false).")]
    public bool continueFromCurrentWeights = true;
    public bool startOnPlay = true;

    private CancellationTokenSource cts;

    void Start()
    {
        if (startOnPlay) StartTraining();
    }

    [ContextMenu("Start Training")]
    public void StartTraining()
    {
        if (cts != null) return;
        cts = new CancellationTokenSource();
        var settings = new TrainerSettings
        {
            Rounds = rounds,
            SelfPlayGamesPerRound = selfPlayGamesPerRound,
            GateGames = gateGames,
            SelfPlayDepth = searchDepth,
            GateDepth = searchDepth,
            Seed = Random.Range(1, int.MaxValue / 2)
        };
        double[] start = continueFromCurrentWeights ? AIAgent.LoadWeights() : Eval.PriorWeights();
        string outPath = System.IO.Path.Combine(Application.dataPath, "Resources", ManuWeights.ResourceName + ".txt");
        RunInBackground(start, settings, outPath, cts.Token);
    }

    void OnDestroy()
    {
        if (cts != null) cts.Cancel();
    }

    // Shared by this component and the editor menu.
    public static Task RunInBackground(double[] start, TrainerSettings settings, string outPath, CancellationToken token)
    {
        ManuTrainer.Log = msg => Debug.Log("[ManuTrainer] " + msg);
        Debug.Log($"[ManuTrainer] Training started: {settings.Rounds} rounds x {settings.SelfPlayGamesPerRound} games " +
                  $"at depth {settings.SelfPlayDepth} on {settings.Threads} threads. Progress is logged here.");
        return Task.Run(() =>
        {
            try
            {
                double[] w = ManuTrainer.Train(start, settings, token);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath));
                System.IO.File.WriteAllText(outPath, ManuTrainer.ToText(w));
                Debug.Log("[ManuTrainer] Done. Weights saved to " + outPath +
                          " (refresh/reimport Assets so Unity picks the file up).");
            }
            catch (System.OperationCanceledException)
            {
                Debug.Log("[ManuTrainer] Training cancelled.");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[ManuTrainer] Training failed: " + e);
            }
        });
    }
}
