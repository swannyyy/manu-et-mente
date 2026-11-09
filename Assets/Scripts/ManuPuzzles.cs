// SPDX-License-Identifier: MPL-2.0
//
// ManuPuzzles — "find the best move" puzzles generated from engine games.
//
// A candidate position is reached by letting the engine play a randomised game for a random
// number of moves. Every legal move in that position is then scored with a separate search, and the
// position is kept only if one move (or at most two) is clearly better than everything else.
// Puzzles are stored as the move list from the start (so en passant and the rotation streak are
// exact), the accepted answers, and a theme.
//
// Pure C#: used headless to build the shipped bank and inside Unity (worker thread) to generate
// fresh puzzles on the device.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ManuAI
{
    public enum PuzzleTheme { BestMove = 0, WinMaterial, Defend, ForcedWin, WinNow }

    public sealed class Puzzle
    {
        public PuzzleTheme Theme;
        public List<string> Moves = new List<string>();      // from the start position
        public List<string> Solutions = new List<string>();  // accepted answers (move codes)
        public string Id;

        public static string Prompt(PuzzleTheme t)
        {
            switch (t)
            {
                case PuzzleTheme.WinNow: return "Find the winning move";
                case PuzzleTheme.ForcedWin: return "Find the move that forces a win";
                case PuzzleTheme.Defend: return "Your opponent threatens to win. Stop it!";
                case PuzzleTheme.WinMaterial: return "Win material";
                default: return "Find the best move";
            }
        }

        // One line: theme|moves|solutions   (moves and solutions space-separated)
        public string Serialize()
        {
            return ((int)Theme) + "|" + string.Join(" ", Moves.ToArray()) + "|" + string.Join(" ", Solutions.ToArray());
        }

        public static Puzzle Parse(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;
            string[] parts = line.Trim().Split('|');
            if (parts.Length != 3) return null;
            int theme;
            if (!int.TryParse(parts[0], out theme)) return null;
            var p = new Puzzle { Theme = (PuzzleTheme)theme };
            if (parts[1].Length > 0) p.Moves.AddRange(parts[1].Split(' '));
            p.Solutions.AddRange(parts[2].Split(' '));
            p.Id = StableId(p.Serialize());
            return p.Solutions.Count > 0 ? p : null;
        }

        // Rebuilds the puzzle position; null if the stored moves are not legal.
        public Board Position()
        {
            var b = Board.Initial();
            var buf = new List<Move>();
            foreach (string code in Moves)
            {
                Move m;
                if (b.Winner >= 0 || !MoveCodec.TryFind(b, code, buf, out m)) return null;
                Undo u = new Undo(); b.Make(m, ref u);
            }
            return b.Winner >= 0 ? null : b;
        }

        public bool IsSolution(string code) { return Solutions.Contains(code); }

        // FNV-1a hash: the same on every run and platform (string.GetHashCode is not guaranteed to be).
        public static string StableId(string s)
        {
            uint h = 2166136261;
            foreach (char c in s) { h ^= c; h *= 16777619; }
            return h.ToString("x8");
        }
    }

    public sealed class PuzzleGenerator
    {
        public double[] Weights;
        public int ScoreDepth = 3;           // search depth for scoring each candidate move
        public long ScoreMsPerMove = 400;
        public double AcceptMargin = 0.04;   // answers within this win prob of the best are accepted
        public double MinGap = 0.10;         // best must beat every other move by this much
        public int MaxSolutions = 2;
        public Random Rng;
        public string LastReject; // diagnostics

        public PuzzleGenerator(double[] weights, int seed)
        {
            Weights = weights; Rng = new Random(seed);
        }

        // Tries random positions until one makes a good puzzle (or attempts run out).
        public Puzzle Generate(int attempts, CancellationToken ct)
        {
            // Each attempt plays one randomised engine game and tests its tactical positions.
            for (int a = 0; a < attempts && !ct.IsCancellationRequested; a++)
            {
                var s = new Searcher(Weights) { MaxDepth = 2, TimeLimitMs = 2000, RootNoise = 25, Rng = Rng };
                var b = Board.Initial();
                var moves = new List<string>();
                var buf = new List<Move>();
                int start = 12 + Rng.Next(40);
                int tested = 0;
                for (int ply = 0; ply < 300 && b.Winner < 0 && tested < 6 && !ct.IsCancellationRequested; ply++)
                {
                    if (ply >= start && Interesting(b) && Rng.NextDouble() < 0.5)
                    {
                        tested++;
                        Puzzle p = TryMake(b, new List<string>(moves), ct);
                        if (p != null) return p;
                    }
                    Move m;
                    if (ply < 4) { b.GenerateMoves(buf); if (buf.Count == 0) break; m = buf[Rng.Next(buf.Count)]; }
                    else { var r = s.Search(b); if (r.Best.IsNone) break; m = r.Best; }
                    moves.Add(MoveCodec.Encode(m));
                    Undo u = new Undo(); b.Make(m, ref u);
                }
            }
            return null;
        }

        // Cheap filter: something tactical is going on (a capture or a win for either side).
        static bool Interesting(Board b)
        {
            var buf = new List<Move>();
            b.GenerateMoves(buf);
            foreach (Move m in buf)
            {
                if (m.IsCapture) return true;
                if (!m.IsRotation && Geo.RingOf[m.To] == 7 && Pc.IsCommander(Pc.Type(b.Sq[m.From]))) return true;
            }
            return OpponentThreatensWin(b);
        }

        // Does the side NOT to move have a move that wins on the spot (i.e. a threat to answer)?
        static bool OpponentThreatensWin(Board b)
        {
            Board c = b.Clone();
            c.Side ^= 1; c.LmColor = -1; c.RotStreak = 0; c.RecomputeHash();
            var buf = new List<Move>();
            c.GenerateMoves(buf);
            foreach (Move m in buf)
            {
                Undo u = new Undo(); c.Make(m, ref u);
                bool win = c.Winner == (b.Side ^ 1);
                c.Unmake(m, ref u);
                if (win) return true;
            }
            return false;
        }

        // Shallow pass: does the best move stand out at all? Rejects flat positions cheaply.
        bool QuickGap(Board b, List<Move> buf, int me)
        {
            var q = new Searcher(Weights) { MaxDepth = 1, TimeLimitMs = 1000, QuiescenceDepth = 3 };
            double best = -1, second = -1;
            foreach (Move m in buf)
            {
                Board c = b.Clone(); Undo u = new Undo(); c.Make(m, ref u);
                double w;
                if (c.Winner >= 0) w = c.Winner == me ? 1 : 0;
                else { var probe = new List<Move>(); c.GenerateMoves(probe); w = probe.Count == 0 ? .5 : 1 - GameAnalysis.WinProb(q.Search(c).Score); }
                if (w > best) { second = best; best = w; } else if (w > second) second = w;
            }
            return best - second >= MinGap * 0.5 || best >= 0.999;
        }

        Puzzle TryMake(Board b, List<string> moves, CancellationToken ct)
        {
            var buf = new List<Move>();
            b.GenerateMoves(buf);
            if (buf.Count < 3) return null;
            int me = b.Side;
            if (!QuickGap(b, buf, me)) { LastReject = "flat (quick check)"; return null; }
            var s = new Searcher(Weights) { MaxDepth = ScoreDepth, TimeLimitMs = ScoreMsPerMove, QuiescenceDepth = 4 };
            var wp = new double[buf.Count];
            int immediateWins = 0;
            for (int i = 0; i < buf.Count; i++)
            {
                if (ct.IsCancellationRequested) return null;
                Board c = b.Clone(); Undo u = new Undo(); c.Make(buf[i], ref u);
                if (c.Winner >= 0) { wp[i] = c.Winner == me ? 1 : 0; if (c.Winner == me) immediateWins++; continue; }
                var probe = new List<Move>(); c.GenerateMoves(probe);
                if (probe.Count == 0) { wp[i] = 0.5; continue; } // drawn
                s.ClearTT();
                SearchResult r = s.Search(c);
                wp[i] = 1 - GameAnalysis.WinProb(r.Score);
            }
            int best = 0;
            for (int i = 1; i < wp.Length; i++) if (wp[i] > wp[best]) best = i;
            double bestWp = wp[best];
            var accepted = new List<int>();
            double bestOther = 0;
            for (int i = 0; i < wp.Length; i++)
            {
                if (wp[i] >= bestWp - AcceptMargin) accepted.Add(i);
                else if (wp[i] > bestOther) bestOther = wp[i];
            }
            LastReject = string.Format("moves {0} best {1:F2} other {2:F2} accepted {3}", wp.Length, bestWp, bestOther, accepted.Count);
            if (accepted.Count > MaxSolutions || accepted.Count == wp.Length) return null;
            if (bestWp - bestOther < MinGap) return null;
            if (bestWp < 0.5) return null; // the answer should lead to a good position

            Move bm = buf[best];
            PuzzleTheme theme;
            if (immediateWins > 0) theme = PuzzleTheme.WinNow;
            else if (bestWp >= 0.999) theme = PuzzleTheme.ForcedWin;
            else if (OpponentThreatensWin(b)) theme = PuzzleTheme.Defend;
            else if (bm.IsCapture) theme = PuzzleTheme.WinMaterial;
            else theme = PuzzleTheme.BestMove;

            var p = new Puzzle { Theme = theme, Moves = moves };
            foreach (int i in accepted) p.Solutions.Add(MoveCodec.Encode(buf[i]));
            p.Id = Puzzle.StableId(p.Serialize());
            return p;
        }
    }
}
