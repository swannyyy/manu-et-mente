using System.Collections;
using System.Reflection;
using UnityEngine;

// Self-play trainer for the minimax AI's evaluation weights.
//
// Drop this on any GameObject in the GameAI (or Game) scene and press Play. It plays many
// fast AI-vs-AI games using the REAL game rules (via AIAgent + game + Chessman) and uses
// coevolutionary hill-climbing to learn the AIWeights. The best weight set is written to
// Application.persistentDataPath/manu_ai_weights.json, which AIAgent loads automatically on
// future runs. Training plays greedily (depth 1) for speed; at play time those learned
// weights feed the existing depth-2/4 minimax for full tactical lookahead.
public class SelfPlayTrainer : MonoBehaviour
{
    [Header("References (auto-found if left empty)")]
    public game gameController;
    public AIAgent aiAgent;

    [Header("Training Budget")]
    [Tooltip("Number of generations (each = gamesPerEval games of champion vs challenger).")]
    public int generations = 300;
    [Tooltip("Games played per generation. Kept even so colors are balanced.")]
    public int gamesPerEval = 8;
    [Tooltip("Ply cap per game; games exceeding this are scored as draws.")]
    public int maxMovesPerGame = 200;
    [Tooltip("How much each draw counts against the challenger when deciding promotion. " +
             "Higher = stronger pressure to evolve weights that win decisively instead of drawing. " +
             "Keep modest (e.g. 0.25): too high and no challenger can ever overcome draw-heavy " +
             "positions, stalling training.")]
    public float drawPenalty = 0.25f;

    [Header("Search / Exploration")]
    [Tooltip("Std-dev of the multiplicative Gaussian mutation applied to weights each generation.")]
    public float startSigma = 0.15f;
    public float minSigma = 0.02f;
    [Tooltip("Sigma is multiplied by this each generation (annealing toward minSigma).")]
    public float sigmaAnneal = 0.997f;
    [Tooltip("Random jitter added to move scores so repeated games diverge.")]
    public float explorationNoise = 3f;
    public int randomSeed = 12345;

    [Header("Control")]
    public bool startOnPlay = true;

    private System.Random rng;
    private bool isTraining = false;
    private string lastGameWinner = "draw";

    void Start()
    {
        if (gameController == null) gameController = FindAnyObjectByType<game>();
        if (aiAgent == null) aiAgent = FindAnyObjectByType<AIAgent>();

        if (startOnPlay)
            StartTraining();
    }

    [ContextMenu("Start Training")]
    public void StartTraining()
    {
        if (isTraining) return;

        if (gameController == null || aiAgent == null)
        {
            Debug.LogError("SelfPlayTrainer: could not find a 'game' and/or 'AIAgent' in the scene.");
            return;
        }

        StartCoroutine(TrainRoutine());
    }

    private IEnumerator TrainRoutine()
    {
        isTraining = true;
        rng = new System.Random(randomSeed);

        // Let every component's Start() finish (AIAgent caches its 'game' reference there).
        yield return null;

        // Headless/quiet mode: stop the AI's auto-play, mute audio, and silence the per-move
        // Debug.Log spam from game/Chessman (it would otherwise dominate runtime).
        float prevVolume = AudioListener.volume;
        bool prevLog = Debug.unityLogger.logEnabled;
        bool prevAiEnabled = aiAgent.isEnabled;

        aiAgent.isEnabled = false;
        gameController.SetMLTrainingMode(true);
        AudioListener.volume = 0f;
        Debug.unityLogger.logEnabled = false;

        Report($"Started: {generations} generations x {gamesPerEval} games = {generations * gamesPerEval} self-play games. Seed {randomSeed}.");

        AIWeights champion = AIWeights.Default();
        float sigma = startSigma;
        int promotions = 0;

        for (int gen = 0; gen < generations; gen++)
        {
            AIWeights challenger = Perturb(champion, sigma);

            int challengerWins = 0, championWins = 0, draws = 0;

            for (int g = 0; g < gamesPerEval; g++)
            {
                bool challengerIsOrange = (g % 2 == 0); // alternate colors for fairness
                AIWeights wOrange = challengerIsOrange ? challenger : champion;
                AIWeights wGreen = challengerIsOrange ? champion : challenger;

                yield return StartCoroutine(PlayGame(wOrange, wGreen));

                if (lastGameWinner == "draw")
                {
                    draws++;
                }
                else
                {
                    bool challengerWon = (lastGameWinner == "orange") == challengerIsOrange;
                    if (challengerWon) challengerWins++;
                    else championWins++;
                }
            }

            // Promote the challenger only if its decisive record beats the champion AFTER charging
            // it for draws. Draws are symmetric in a head-to-head game, so penalizing the incumbent
            // too would cancel out and change nothing; instead we make the challenger bear the cost,
            // so a draw-prone challenger fails to unseat the champion. Over generations this selects
            // for weights that convert games into wins rather than settling for draws.
            float challengerScore = challengerWins - championWins - drawPenalty * draws;
            if (challengerScore > 0f)
            {
                champion = challenger;
                promotions++;
            }

            sigma = Mathf.Max(minSigma, sigma * sigmaAnneal);

            if (gen % 5 == 0 || gen == generations - 1)
                Report($"Gen {gen + 1}/{generations}: challenger W-L-D = {challengerWins}-{championWins}-{draws}, sigma {sigma:F3}, total promotions {promotions}");

            yield return null; // keep the editor responsive between generations
        }

        SaveWeights(champion);
        aiAgent.SetWeights(champion); // apply immediately for this session too

        // Restore normal play state.
        AudioListener.volume = prevVolume;
        Debug.unityLogger.logEnabled = prevLog;
        gameController.SetMLTrainingMode(false);
        aiAgent.isEnabled = prevAiEnabled;
        isTraining = false;

        Report($"Done. {promotions} promotions over {generations} generations. Trained weights saved and applied.");
    }

    // Plays one greedy (depth-1) game and stores the result in lastGameWinner ("orange"/"green"/"draw").
    private IEnumerator PlayGame(AIWeights wOrange, AIWeights wGreen)
    {
        // Reset the board. game.Restart() early-returns while in training mode, so toggle it off
        // for the reset call only, then back on to keep rotation animations/sounds suppressed.
        gameController.SetMLTrainingMode(false);
        gameController.Restart();
        gameController.SetMLTrainingMode(true);

        lastGameWinner = "draw";

        for (int move = 0; move < maxMovesPerGame; move++)
        {
            if (gameController.IsGameOver())
            {
                lastGameWinner = gameController.GetWinner() ?? "draw";
                yield break;
            }

            string player = gameController.GetCurrentPlayer();
            AIWeights w = (player == "orange") ? wOrange : wGreen;

            AIMove chosen = aiAgent.PickGreedyMove(player, w, explorationNoise, rng);
            if (chosen == null)
                yield break; // no legal move available -> treat as a draw

            aiAgent.ExecuteMove(chosen);

            if (move % 20 == 0)
                yield return null; // breathe periodically within long games
        }
        // Reached the ply cap with no winner -> draw (lastGameWinner already "draw").
    }

    // Returns a mutated copy of the weights: each float field scaled by (1 + N(0,1)*sigma), clamped >= 0.
    private AIWeights Perturb(AIWeights src, float sigma)
    {
        AIWeights child = src.Clone();
        FieldInfo[] fields = typeof(AIWeights).GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (FieldInfo f in fields)
        {
            if (f.FieldType != typeof(float)) continue;

            float v = (float)f.GetValue(child);
            float mutated = v * (1f + (float)Gaussian() * sigma);
            if (mutated < 0f) mutated = 0f;
            f.SetValue(child, mutated);
        }

        return child;
    }

    // Standard normal sample via Box-Muller.
    private double Gaussian()
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        return System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2);
    }

    private void SaveWeights(AIWeights w)
    {
        string json = JsonUtility.ToJson(w, true);

        string path = System.IO.Path.Combine(Application.persistentDataPath, AIAgent.WeightsFileName);
        System.IO.File.WriteAllText(path, json);
        Report($"Saved trained weights to: {path}");

        // Refresh the online-learning baseline too, so post-game drift is measured against THIS
        // fresh training run rather than a stale snapshot.
        string basePath = System.IO.Path.Combine(Application.persistentDataPath, AIAgent.BaseWeightsFileName);
        System.IO.File.WriteAllText(basePath, json);
        Report($"Refreshed learning baseline at: {basePath}");
    }

    // Logs a status line even while general logging is suppressed during training.
    private void Report(string msg)
    {
        bool prev = Debug.unityLogger.logEnabled;
        Debug.unityLogger.logEnabled = true;
        Debug.Log("[SelfPlayTrainer] " + msg);
        Debug.unityLogger.logEnabled = prev;
    }
}
