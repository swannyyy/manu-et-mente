// SPDX-License-Identifier: MPL-2.0
//
// ManuAnalysis — post-game analysis in the spirit of lichess.
//
// A finished game is stored as a list of compact move codes (see MoveCodec). The analysis replays
// them through ManuEngine to rebuild every position, searches each one, and from the evaluations
// derives a win probability per position, a loss per move, a move classification
// (inaccuracy / mistake / blunder) and an accuracy score per player.
//
// Pure C# (no UnityEngine): runs on a worker thread inside Unity and can be tested headless.

using System;
using System.Collections.Generic;
using System.Threading;

namespace ManuAI
{
    // Compact, human-readable move codes stored in the game history.
    //   piece move: "fromRing,fromPlace>toRing,toPlace"   e.g. "2,28>3,15"
    //   rotation:   "R" + pair + ("+" clockwise | "-" counterclockwise)   e.g. "R3+"
    public static class MoveCodec
    {
        public static string PieceMove(int fromRing, int fromPlace, int toRing, int toPlace)
        {
            return fromRing + "," + fromPlace + ">" + toRing + "," + toPlace;
        }

        public static string Rotation(int pair, bool clockwise)
        {
            return "R" + pair + (clockwise ? "+" : "-");
        }

        public static string Encode(Move m)
        {
            if (m.IsRotation) return Rotation(m.Pair, m.Clockwise);
            return PieceMove(Geo.RingOf[m.From], Geo.PlaceOf[m.From], Geo.RingOf[m.To], Geo.PlaceOf[m.To]);
        }

        // Finds the legal move in `b` matching `code`. False if the code is malformed or illegal.
        public static bool TryFind(Board b, string code, List<Move> buffer, out Move found)
        {
            found = Move.None;
            if (string.IsNullOrEmpty(code)) return false;
            b.GenerateMoves(buffer);
            if (code[0] == 'R')
            {
                if (code.Length < 3) return false;
                int pair = code[1] - '0';
                bool cw = code[2] == '+';
                foreach (Move m in buffer)
                    if (m.IsRotation && m.Pair == pair && m.Clockwise == cw) { found = m; return true; }
                return false;
            }
            string[] halves = code.Split('>');
            if (halves.Length != 2) return false;
            string[] a = halves[0].Split(','), c = halves[1].Split(',');
            if (a.Length != 2 || c.Length != 2) return false;
            int fr, fp, tr, tp;
            if (!int.TryParse(a[0], out fr) || !int.TryParse(a[1], out fp) || !int.TryParse(c[0], out tr) || !int.TryParse(c[1], out tp)) return false;
            if (fr < 1 || fr > 7 || tr < 1 || tr > 7) return false;
            if (fp < 1 || fp > Geo.RingSize[fr - 1] || tp < 1 || tp > Geo.RingSize[tr - 1]) return false;
            int from = Geo.Cell(fr, fp), to = Geo.Cell(tr, tp);
            foreach (Move m in buffer)
                if (!m.IsRotation && m.From == from && m.To == to) { found = m; return true; }
            return false;
        }
    }

    public enum MoveClass { Unknown = 0, Best, Good, Inaccuracy, Mistake, Blunder }

    public sealed class PositionEval
    {
        public bool Done;
        public double Score;      // side-to-move perspective (engine units; +-WinScore = forced result)
        public Move Best = Move.None;
        public int Depth;
    }

    public sealed class GameAnalysis
    {
        // Thresholds on the mover's loss of win probability (percentage points). Lower than lichess (10/20/30 on
        // its -100..100 scale) because advantages in this game build up gradually rather than in single drops.
        public const double InaccuracyLoss = 4, MistakeLoss = 8, BlunderLoss = 14;
        public const double Scale = 120; // eval units per logit (matches ManuTrainer)

        public readonly List<Board> Positions = new List<Board>(); // Positions[i] is before Moves[i]
        public readonly List<Move> Moves = new List<Move>();
        public PositionEval[] Evals;
        public string Error;              // set if the move list could not be fully replayed
        public int Completed;             // positions analysed so far (written by the worker thread)
        public bool Finished;

        // Rebuilds every position from the start of the game. Stops (and sets Error) at the first
        // code that is not a legal move, so a damaged record still analyses up to that point.
        public static GameAnalysis Build(IList<string> codes)
        {
            var ga = new GameAnalysis();
            var b = Board.Initial();
            var buffer = new List<Move>();
            ga.Positions.Add(b.Clone());
            for (int i = 0; i < codes.Count; i++)
            {
                Move m;
                if (b.Winner >= 0 || !MoveCodec.TryFind(b, codes[i], buffer, out m))
                {
                    ga.Error = "Move " + (i + 1) + " (" + codes[i] + ") could not be replayed; analysis stops there.";
                    break;
                }
                Undo u = new Undo();
                b.Make(m, ref u);
                ga.Moves.Add(m);
                ga.Positions.Add(b.Clone());
            }
            ga.Evals = new PositionEval[ga.Positions.Count];
            for (int i = 0; i < ga.Evals.Length; i++) ga.Evals[i] = new PositionEval();
            return ga;
        }

        public int PlyCount { get { return Moves.Count; } }

        // Searches every position in order. Safe to call on a worker thread; the UI reads Evals as
        // they complete (each PositionEval is fully written before Done is set).
        public void Run(double[] weights, int maxDepth, long msPerPosition, CancellationToken ct)
        {
            var s = new Searcher(weights) { MaxDepth = maxDepth, TimeLimitMs = msPerPosition, QuiescenceDepth = 4, RootNoise = 0 };
            for (int i = 0; i < Positions.Count; i++)
            {
                if (ct.IsCancellationRequested) return;
                AnalysePosition(i, s, weights);
            }
            Finished = true;
        }

        // Single-threaded variant (browser build): analyses positions until about frameBudgetMs has passed
        // (at least one), then returns so the caller can draw a frame. Returns true when all are done.
        Searcher stepSearcher; double[] stepWeights;
        public bool Step(double[] weights, int maxDepth, long msPerPosition, long frameBudgetMs)
        {
            if (stepSearcher == null)
            {
                stepWeights = weights;
                stepSearcher = new Searcher(weights) { MaxDepth = maxDepth, TimeLimitMs = msPerPosition, QuiescenceDepth = 4, RootNoise = 0 };
            }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            do
            {
                if (Completed >= Positions.Count) break;
                AnalysePosition(Completed, stepSearcher, stepWeights);
            } while (sw.ElapsedMilliseconds < frameBudgetMs);
            if (Completed >= Positions.Count) Finished = true;
            return Finished;
        }

        void AnalysePosition(int i, Searcher s, double[] weights)
        {
            Board b = Positions[i];
            PositionEval e = Evals[i];
            if (b.Winner >= 0)
            {
                e.Score = b.Winner == b.Side ? Searcher.WinScore : -Searcher.WinScore;
            }
            else
            {
                var buf = new List<Move>();
                b.GenerateMoves(buf);
                if (buf.Count == 0) e.Score = 0; // drawn (no piece move after the rotation limit)
                else
                {
                    SearchResult r = s.Search(b);
                    e.Score = r.Score; e.Best = r.Best; e.Depth = r.Depth;
                    // A single legal move is returned without searching; evaluate it properly.
                    if (buf.Count == 1)
                    {
                        Board c = b.Clone(); Undo u = new Undo(); c.Make(buf[0], ref u);
                        e.Score = c.Winner >= 0 ? (c.Winner == b.Side ? Searcher.WinScore : -Searcher.WinScore)
                                                : -Eval.Evaluate(c, weights);
                    }
                }
            }
            e.Done = true;
            Completed = i + 1;
        }

        // Win probability (0..1) for the side to move, from an engine score.
        public static double WinProb(double score)
        {
            if (score >= Searcher.WinScore / 2) return 1;
            if (score <= -Searcher.WinScore / 2) return 0;
            return 1.0 / (1.0 + Math.Exp(-score / Scale));
        }

        public bool IsDone(int position) { return position >= 0 && position < Evals.Length && Evals[position].Done; }

        // Orange's win probability at position i (NaN if not analysed yet).
        public double OrangeWin(int position)
        {
            if (!IsDone(position)) return double.NaN;
            double p = WinProb(Evals[position].Score);
            return Positions[position].Side == Pc.Orange ? p : 1 - p;
        }

        // Colour that played Moves[i].
        public int Mover(int moveIndex) { return Positions[moveIndex].Side; }

        // Mover's loss of win probability (percentage points) for Moves[i]; NaN if not ready.
        public double Loss(int moveIndex)
        {
            if (moveIndex < 0 || moveIndex >= Moves.Count || !IsDone(moveIndex) || !IsDone(moveIndex + 1)) return double.NaN;
            if (!Evals[moveIndex].Best.IsNone && Moves[moveIndex].SameAs(Evals[moveIndex].Best)) return 0;
            double before = WinProb(Evals[moveIndex].Score);
            double after = 1 - WinProb(Evals[moveIndex + 1].Score);
            return Math.Max(0, (before - after) * 100);
        }

        public MoveClass Classify(int moveIndex)
        {
            double loss = Loss(moveIndex);
            if (double.IsNaN(loss)) return MoveClass.Unknown;
            if (!Evals[moveIndex].Best.IsNone && Moves[moveIndex].SameAs(Evals[moveIndex].Best)) return MoveClass.Best;
            // Don't nag about moves in positions that were already decided either way.
            double before = WinProb(Evals[moveIndex].Score);
            if (before < 0.03 || (before > 0.97 && 1 - WinProb(Evals[moveIndex + 1].Score) > 0.97)) return MoveClass.Good;
            if (loss >= BlunderLoss) return MoveClass.Blunder;
            if (loss >= MistakeLoss) return MoveClass.Mistake;
            if (loss >= InaccuracyLoss) return MoveClass.Inaccuracy;
            return MoveClass.Good;
        }

        // Lichess move-accuracy curve, averaged over the player's analysed moves (0..100, NaN if none).
        public double Accuracy(int color)
        {
            double sum = 0; int n = 0;
            for (int i = 0; i < Moves.Count; i++)
            {
                if (Mover(i) != color) continue;
                double loss = Loss(i);
                if (double.IsNaN(loss)) continue;
                double acc = 103.1668 * Math.Exp(-0.04354 * loss) - 3.1669;
                sum += Math.Max(0, Math.Min(100, acc)); n++;
            }
            return n == 0 ? double.NaN : sum / n;
        }

        // [inaccuracies, mistakes, blunders] for a colour.
        public int[] Errors(int color)
        {
            var c = new int[3];
            for (int i = 0; i < Moves.Count; i++)
            {
                if (Mover(i) != color) continue;
                switch (Classify(i))
                {
                    case MoveClass.Inaccuracy: c[0]++; break;
                    case MoveClass.Mistake: c[1]++; break;
                    case MoveClass.Blunder: c[2]++; break;
                }
            }
            return c;
        }

        public static string Symbol(MoveClass c)
        {
            switch (c)
            {
                case MoveClass.Best: return "★";
                case MoveClass.Inaccuracy: return "?!";
                case MoveClass.Mistake: return "?";
                case MoveClass.Blunder: return "??";
                default: return "";
            }
        }

        public static string Name(MoveClass c)
        {
            switch (c)
            {
                case MoveClass.Best: return "Best move";
                case MoveClass.Good: return "Good move";
                case MoveClass.Inaccuracy: return "Inaccuracy";
                case MoveClass.Mistake: return "Mistake";
                case MoveClass.Blunder: return "Blunder";
                default: return "";
            }
        }
    }
}
