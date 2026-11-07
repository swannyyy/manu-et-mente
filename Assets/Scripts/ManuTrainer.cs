// SPDX-License-Identifier: MPL-2.0
//
// ManuTrainer — self-play training for the ManuEngine evaluation weights.
//
// Pure C# (no UnityEngine), so it runs both headless (mono/dotnet console) and from the Unity
// editor (menu: Manu AI > Train). Each round:
//   1. Self-play: the current weights play many games against themselves with the real search,
//      with randomised openings and a little root noise so games differ.
//   2. Tuning: every recorded position is labelled with the final result, and the weights are
//      fitted by logistic regression ("Texel tuning") so that sigmoid(eval) predicts the result.
//      A pull toward the previous weights keeps each round's change sensible.
//   3. Gating: the new weights play a match against the current champion and are only accepted
//      if they score better. Accepted weights become the champion for the next round.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ManuAI
{
    public sealed class TrainerSettings
    {
        public int Rounds = 6;
        public int SelfPlayGamesPerRound = 200;
        public int GateGames = 100;
        public int SelfPlayDepth = 3;
        public int GateDepth = 3;
        public int MaxPlies = 300;
        public int RandomOpeningPlies = 6;
        public double RootNoise = 8;
        public double Scale = 120;          // eval units per logit
        public double L2 = 2e-4;            // pull toward previous weights (per sample, scaled)
        public int Epochs = 400;
        public double LearningRate = 2.0;
        public int Threads = Environment.ProcessorCount;
        public int Seed = 2026;
        public double GateThreshold = 0.52; // required score fraction to accept new weights
    }

    public sealed class GameRecord
    {
        public List<double[]> Features = new List<double[]>();
        public List<int> SideToMove = new List<int>();
        public int Winner = -1; // -1 = draw (ply cap)
        public int Plies;
        public Victory WinType;
    }

    public static class ManuTrainer
    {
        public static Action<string> Log = Console.WriteLine;
        public static double MatchNoise = 0;

        // Plays one game. If `record` is non-null, stores features of each position (after the
        // random opening) for tuning. Returns the winner colour or -1 for a draw.
        public static int PlayGame(Searcher orange, Searcher green, Random rng, TrainerSettings st, GameRecord record)
        {
            var b = Board.Initial();
            var moves = new List<Move>();
            orange.ClearTT(); green.ClearTT();
            for (int ply = 0; ply < st.MaxPlies; ply++)
            {
                if (b.Winner >= 0) break;
                Move m;
                if (ply < st.RandomOpeningPlies)
                {
                    b.GenerateMoves(moves);
                    if (moves.Count == 0) break;
                    m = moves[rng.Next(moves.Count)];
                }
                else
                {
                    if (record != null)
                    {
                        var f = new double[Eval.Count];
                        Eval.Features(b, f);
                        record.Features.Add(f);
                        record.SideToMove.Add(b.Side);
                    }
                    Searcher s = b.Side == Pc.Orange ? orange : green;
                    var r = s.Search(b);
                    if (r.Best.IsNone) break;
                    m = r.Best;
                }
                Undo u = new Undo();
                b.Make(m, ref u);
            }
            if (record != null) { record.Winner = b.Winner; record.Plies = b.Ply; record.WinType = b.WinType; }
            return b.Winner;
        }

        static Searcher MakeSearcher(double[] w, int depth, double noise, int seed, bool legacy = false)
        {
            return new Searcher(w)
            {
                MaxDepth = depth, TimeLimitMs = 60000, RootNoise = noise, Rng = new Random(seed),
                UseLegacyEval = legacy, QuiescenceDepth = 4
            };
        }

        public static List<GameRecord> SelfPlay(double[] w, TrainerSettings st, int seed, CancellationToken ct)
        {
            var records = new GameRecord[st.SelfPlayGamesPerRound];
            int done = 0;
            Parallel.For(0, st.SelfPlayGamesPerRound, new ParallelOptions { MaxDegreeOfParallelism = st.Threads, CancellationToken = ct }, () =>
            {
                int tid = Thread.CurrentThread.ManagedThreadId;
                return new Searcher[] { MakeSearcher(w, st.SelfPlayDepth, st.RootNoise, seed * 7919 + tid), MakeSearcher(w, st.SelfPlayDepth, st.RootNoise, seed * 104729 + tid) };
            },
            (i, state, s) =>
            {
                var rec = new GameRecord();
                PlayGame(s[0], s[1], new Random(seed * 1000003 + i), st, rec);
                records[i] = rec;
                int d = Interlocked.Increment(ref done);
                if (d % 50 == 0) Log("  self-play " + d + "/" + st.SelfPlayGamesPerRound);
                return s;
            }, s => { });
            return new List<GameRecord>(records);
        }

        // Match: A vs B, alternating colours, shared random openings per pair of games.
        // Returns (winsA, winsB, draws).
        public static int[] Match(double[] wA, bool legacyA, double[] wB, bool legacyB, int games, int depth, TrainerSettings st, int seed, CancellationToken ct)
        {
            int wa = 0, wb = 0, dr = 0;
            Parallel.For(0, games, new ParallelOptions { MaxDegreeOfParallelism = st.Threads, CancellationToken = ct }, i =>
            {
                var a = MakeSearcher(wA, depth, MatchNoise, seed + i, legacyA);
                var bS = MakeSearcher(wB, depth, MatchNoise, seed + i + 99991, legacyB);
                bool aIsOrange = i % 2 == 0;
                var rng = new Random(seed * 31 + i / 2); // same opening for both colour assignments
                int winner = aIsOrange ? PlayGame(a, bS, rng, st, null) : PlayGame(bS, a, rng, st, null);
                if (winner < 0) Interlocked.Increment(ref dr);
                else if ((winner == Pc.Orange) == aIsOrange) Interlocked.Increment(ref wa);
                else Interlocked.Increment(ref wb);
            });
            return new[] { wa, wb, dr };
        }

        // Logistic regression of game outcome on features (Adam optimiser).
        public static double[] Tune(List<GameRecord> data, double[] start, TrainerSettings st)
        {
            var X = new List<double[]>(); var Y = new List<double>();
            foreach (var g in data)
            {
                for (int i = 0; i < g.Features.Count; i++)
                {
                    double y = g.Winner < 0 ? 0.5 : (g.Winner == g.SideToMove[i] ? 1.0 : 0.0);
                    X.Add(g.Features[i]); Y.Add(y);
                }
            }
            int n = X.Count, d = Eval.Count;
            if (n == 0) return (double[])start.Clone();
            double[] w = (double[])start.Clone();
            double[] m = new double[d], v = new double[d];
            double b1 = 0.9, b2 = 0.999, eps = 1e-8;
            for (int ep = 1; ep <= st.Epochs; ep++)
            {
                var grad = new double[d];
                double loss = 0;
                for (int i = 0; i < n; i++)
                {
                    double[] x = X[i];
                    double z = 0; for (int k = 0; k < d; k++) z += w[k] * x[k];
                    z /= st.Scale;
                    if (z > 30) z = 30; if (z < -30) z = -30;
                    double p = 1.0 / (1.0 + Math.Exp(-z));
                    double y = Y[i];
                    loss += -(y * Math.Log(p + 1e-12) + (1 - y) * Math.Log(1 - p + 1e-12));
                    double gz = (p - y) / st.Scale;
                    for (int k = 0; k < d; k++) if (x[k] != 0) grad[k] += gz * x[k];
                }
                for (int k = 0; k < d; k++)
                {
                    grad[k] = grad[k] / n + st.L2 * (w[k] - start[k]);
                    m[k] = b1 * m[k] + (1 - b1) * grad[k];
                    v[k] = b2 * v[k] + (1 - b2) * grad[k] * grad[k];
                    double mh = m[k] / (1 - Math.Pow(b1, ep)), vh = v[k] / (1 - Math.Pow(b2, ep));
                    w[k] -= st.LearningRate * mh / (Math.Sqrt(vh) + eps);
                }
                if (ep == 1 || ep == st.Epochs) Log(string.Format("  tune epoch {0}: loss {1:F4} on {2} positions", ep, loss / n, n));
            }
            return w;
        }

        public static string Describe(GameRecord[] recs)
        {
            int o = 0, g = 0, dr = 0; long plies = 0; var types = new Dictionary<Victory, int>();
            foreach (var r in recs)
            {
                plies += r.Plies;
                if (r.Winner < 0) dr++; else if (r.Winner == Pc.Orange) o++; else g++;
                if (r.Winner >= 0) types[r.WinType] = types.ContainsKey(r.WinType) ? types[r.WinType] + 1 : 1;
            }
            var sb = new StringBuilder();
            sb.AppendFormat("orange {0}, green {1}, draws {2}, avg plies {3:F0};", o, g, dr, plies / (double)Math.Max(1, recs.Length));
            foreach (var kv in types) sb.Append(" " + kv.Key + "=" + kv.Value);
            return sb.ToString();
        }

        // Full training loop. Returns the best weights found.
        public static double[] Train(double[] startWeights, TrainerSettings st, CancellationToken ct)
        {
            double[] champion = (double[])startWeights.Clone();
            var allData = new List<GameRecord>();
            var swTotal = Stopwatch.StartNew();
            for (int round = 1; round <= st.Rounds && !ct.IsCancellationRequested; round++)
            {
                Log("Round " + round + "/" + st.Rounds + ": self-play " + st.SelfPlayGamesPerRound + " games at depth " + st.SelfPlayDepth);
                var recs = SelfPlay(champion, st, st.Seed + round * 17, ct);
                Log("  results: " + Describe(recs.ToArray()));
                allData.AddRange(recs);
                // Keep the most recent data (older games were played by weaker weights).
                int maxGames = st.SelfPlayGamesPerRound * 3;
                if (allData.Count > maxGames) allData.RemoveRange(0, allData.Count - maxGames);

                double[] candidate = Tune(allData, champion, st);
                var res = Match(candidate, false, champion, false, st.GateGames, st.GateDepth, st, st.Seed + round * 1009, ct);
                double score = (res[0] + 0.5 * res[2]) / Math.Max(1, res[0] + res[1] + res[2]);
                Log(string.Format("  gate: candidate W{0} L{1} D{2} vs champion -> score {3:P1}", res[0], res[1], res[2], score));
                if (score >= st.GateThreshold) { champion = candidate; Log("  accepted new weights"); }
                else Log("  rejected (kept previous champion)");
                Log(string.Format("  elapsed {0:F0}s", swTotal.Elapsed.TotalSeconds));
            }
            return champion;
        }

        // SPSA: optimise weights directly on match results. Each iteration plays w+c*delta against
        // w-c*delta (delta = random +-1 per weight, scaled per weight) and steps toward the winner.
        public static double[] Spsa(double[] start, int iterations, int gamesPerIter, int depth, double c, double a,
                                    TrainerSettings st, int seed, CancellationToken ct)
        {
            double[] w = (double[])start.Clone();
            var rng = new Random(seed);
            int d = w.Length;
            for (int it = 1; it <= iterations && !ct.IsCancellationRequested; it++)
            {
                var delta = new double[d];
                var plus = new double[d]; var minus = new double[d];
                for (int k = 0; k < d; k++)
                {
                    double scale = Math.Max(2.0, Math.Abs(w[k]) * 0.15) * c;
                    delta[k] = (rng.Next(2) == 0 ? -1 : 1) * scale;
                    plus[k] = w[k] + delta[k]; minus[k] = w[k] - delta[k];
                }
                var res = Match(plus, false, minus, false, gamesPerIter, depth, st, seed + it * 7, ct);
                double result = (res[0] - res[1]) / (double)gamesPerIter; // in [-1, 1]
                for (int k = 0; k < d; k++) w[k] += a * result * delta[k];
                if (it % 10 == 0) Log(string.Format("  spsa iter {0}: last W{1} L{2} D{3}", it, res[0], res[1], res[2]));
            }
            return w;
        }

        public static string ToCSharpArray(double[] w)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            for (int i = 0; i < w.Length; i++)
            {
                sb.Append(w[i].ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
                if (i < w.Length - 1) sb.Append(i % 7 == 6 ? ",\n" : ", ");
            }
            sb.Append("\n}");
            return sb.ToString();
        }

        public static string ToText(double[] w)
        {
            var parts = new string[w.Length];
            for (int i = 0; i < w.Length; i++) parts[i] = w[i].ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            return string.Join(",", parts);
        }

        public static double[] FromText(string s)
        {
            var parts = s.Trim().Split(',');
            if (parts.Length != Eval.Count) return null;
            var w = new double[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                if (!double.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out w[i])) return null;
            return w;
        }
    }
}
